using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using QuickLauncher.Models;
using QuickLauncher.Services;

namespace QuickLauncher;

public partial class App : System.Windows.Application {
    private readonly SettingsService _settingsService = new();
    private readonly StartupService _startupService = new();
    private readonly AppIndexService _appIndexService = new();
    private readonly RecentUsageService _recentUsageService = new();
    private readonly AppIconService _appIconService = new();
    private readonly HiddenIndexService _hiddenIndexService = new();
    private readonly AppSearchService _searchService;
    private readonly LaunchService _launchService = new();
    private TrayService? _trayService;
    private HotkeyService? _hotkeyService;
    private MainWindow? _mainWindow;
    private UserSettings _settings = UserSettings.CreateDefault();
    private IndexSummary _indexSummary = IndexSummary.Empty();

    public App() {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _searchService = new AppSearchService(new SearchPinyinService(), _recentUsageService, _appIconService, _hiddenIndexService);
    }

    protected override async void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);

        try {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }
        catch (Exception ex) {
            ShowStartupError("设置 ShutdownMode", ex);
            Shutdown(1);
            return;
        }

        try {
            _settings = _settingsService.Load();
        }
        catch (Exception ex) {
            ShowStartupError("加载配置文件", ex);
            Shutdown(1);
            return;
        }

        try {
            _mainWindow = new MainWindow(_searchService, _launchService, _recentUsageService, _hiddenIndexService);
            _mainWindow.SettingsRequested += (_, _) => OpenSettingsWindow();
            _mainWindow.IndexInvalidated += (_, _) => RefreshSearchIndex();
            _mainWindow.SetHideAfterLaunch(_settings.HideAfterLaunch);
            _mainWindow.UpdateHotkeyDisplay(_settings.Hotkey.GetDisplayText());
        }
        catch (Exception ex) {
            ShowStartupError("创建主窗口", ex);
            Shutdown(1);
            return;
        }

        try {
            var windowHandle = new WindowInteropHelper(_mainWindow).EnsureHandle();
            _hotkeyService = new HotkeyService(windowHandle);
            _hotkeyService.Activated += (_, _) => ToggleLauncherVisibility();
            if (!_hotkeyService.Register(_settings.Hotkey)) {
                System.Windows.MessageBox.Show(
                    $"快捷键 {_settings.Hotkey.GetDisplayText()} 注册失败，可能已被其他程序占用。",
                    "QuickLauncher",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex) {
            ShowStartupError("注册全局快捷键", ex);
            Shutdown(1);
            return;
        }

        try {
            _trayService = new TrayService();
            _trayService.ShowRequested += (_, _) => Dispatcher.Invoke(ShowLauncher);
            _trayService.SettingsRequested += (_, _) => Dispatcher.Invoke(OpenSettingsWindow);
            _trayService.ExitRequested += (_, _) => Dispatcher.Invoke(ExitApplication);
        }
        catch (Exception ex) {
            ShowStartupError("创建系统托盘图标", ex);
            Shutdown(1);
            return;
        }

        try {
            _startupService.Apply(_settings.RunAtStartup);
        }
        catch (Exception ex) {
            ShowStartupError("设置开机自启", ex);
        }

        try {
            _indexSummary = await _appIndexService.LoadAsync(_settings);
            _searchService.RefreshIndex(_appIndexService.Entries);
            _mainWindow.UpdateIndexStatus(_indexSummary);
            _mainWindow.RefreshResults();
        }
        catch (Exception ex) {
            ShowStartupError("加载应用索引", ex);
        }

        _ = CheckForUpdateAsync();
    }

    protected override void OnExit(ExitEventArgs e) {
        _trayService?.Dispose();
        _hotkeyService?.Dispose();
        base.OnExit(e);
    }

    private void ShowLauncher() {
        _mainWindow?.ShowLauncher();
    }

    private void ToggleLauncherVisibility() {
        if (_mainWindow is null) {
            return;
        }

        if (_mainWindow.IsVisible && _mainWindow.IsActive) {
            _mainWindow.HideLauncher();
            return;
        }

        _mainWindow.ShowLauncher();
    }

    private void OpenSettingsWindow() {
        var settingsWindow = new SettingsWindow(_settings, _indexSummary, _hiddenIndexService);
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (version is not null) {
            settingsWindow.VersionTextBlock.Text = $"QuickLauncher v{version.ToString(3)}";
        }
        settingsWindow.RebuildRequested += async (_, rebuildSettings) =>
            await RebuildIndexFromWindowAsync(settingsWindow, rebuildSettings);
        settingsWindow.SettingsSaved += (_, newSettings) => ApplySettings(newSettings);
        settingsWindow.HiddenChanged += (_, _) => RefreshSearchIndex();
        settingsWindow.ShowDialog();
    }

    private void RefreshSearchIndex() {
        _searchService.RefreshIndex(_appIndexService.Entries);
        _mainWindow?.RefreshResults();
    }

    private void ApplySettings(UserSettings newSettings) {
        var previousSettings = _settings.Clone();
        var hotkeyChanged = _hotkeyService is null || _hotkeyService.Register(newSettings.Hotkey);
        if (!hotkeyChanged) {
            System.Windows.MessageBox.Show(
                $"快捷键 {_settings.Hotkey.GetDisplayText()} 已保留，新的快捷键 {newSettings.Hotkey.GetDisplayText()} 未能注册。",
                "QuickLauncher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            newSettings.Hotkey = _settings.Hotkey.Clone();
        }

        _settings = newSettings.Clone();
        _settingsService.Save(_settings);
        _startupService.Apply(_settings.RunAtStartup);
        _mainWindow?.SetHideAfterLaunch(_settings.HideAfterLaunch);
        _mainWindow?.UpdateHotkeyDisplay(_settings.Hotkey.GetDisplayText());

        if (HasIndexScopeChanged(previousSettings, _settings)) {
            _ = RebuildIndexAsync(_settings);
        }
    }

    private async Task CheckForUpdateAsync() {
        UpdateInfo? updateInfo;
        try {
            var updateService = new UpdateService();
            updateInfo = await updateService.CheckForUpdateAsync();
            if (updateInfo is null) {
                return;
            }
        }
        catch (Exception ex) {
            Dispatcher.Invoke(() => System.Windows.MessageBox.Show(
                $"检查更新失败：{ex.Message}",
                "QuickLauncher - 版本更新",
                MessageBoxButton.OK,
                MessageBoxImage.Warning));
            return;
        }

        var result = System.Windows.MessageBox.Show(
            $"发现新版本 v{updateInfo.NewVersion}（当前 v{updateInfo.CurrentVersion}）\n\n{updateInfo.Description}\n\n是否立即更新？",
            "QuickLauncher - 版本更新",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (result != MessageBoxResult.Yes) {
            return;
        }

        // 下载 + 落盘校验在后台进行；完成后由批处理脚本替换并重启主程序。
        // 下载期间用一个无边框置顶提示遮住主窗口，避免误以为"点了没反应"。
        System.Windows.Window? progressWindow = null;
        Dispatcher.Invoke(() => {
            progressWindow = new System.Windows.Window {
                Width = 320,
                Height = 120,
                WindowStyle = System.Windows.WindowStyle.None,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                Topmost = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Content = new System.Windows.Controls.Border {
                    Background = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("BgPanel"),
                    CornerRadius = new System.Windows.CornerRadius(12),
                    Padding = new System.Windows.Thickness(20),
                    Child = new System.Windows.Controls.StackPanel {
                        VerticalAlignment = System.Windows.VerticalAlignment.Center,
                        Children = {
                            new System.Windows.Controls.TextBlock {
                                Text = "正在下载更新…",
                                FontSize = 14,
                                FontWeight = System.Windows.FontWeights.SemiBold,
                                Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("TextPrimary"),
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                            },
                            new System.Windows.Controls.TextBlock {
                                Text = $"v{updateInfo.NewVersion}",
                                FontSize = 11,
                                Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("TextSecondary"),
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                                Margin = new System.Windows.Thickness(0, 6, 0, 0)
                            }
                        }
                    }
                }
            };
            progressWindow.Show();
        });

        try {
            var applyService = new UpdateService();
            await applyService.DownloadAndApplyAsync(updateInfo);
            Dispatcher.Invoke(() => {
                progressWindow?.Close();
                ExitApplication(true);
            });
        }
        catch (Exception ex) {
            Dispatcher.Invoke(() => {
                progressWindow?.Close();
                System.Windows.MessageBox.Show(
                    $"更新失败：{ex.Message}\n\n可前往 GitHub 手动下载新版本。",
                    "QuickLauncher - 版本更新",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            });
        }
    }

    private void ExitApplication() {
        ExitApplication(false);
    }

    private void ExitApplication(bool isUpdating) {
        _trayService?.Dispose();
        _trayService = null;

        _hotkeyService?.Dispose();
        _hotkeyService = null;

        if (_mainWindow is not null) {
            _mainWindow.PrepareForExit();
            _mainWindow.Close();
            _mainWindow = null;
        }

        Shutdown();
    }

    private async Task RebuildIndexFromWindowAsync(SettingsWindow settingsWindow, UserSettings rebuildSettings) {
        settingsWindow.SetRebuildState(true);

        try {
            _indexSummary = await _appIndexService.RebuildAsync(rebuildSettings);
            _searchService.RefreshIndex(_appIndexService.Entries);
            settingsWindow.UpdateIndexStatus(_indexSummary);
            _mainWindow?.UpdateIndexStatus(_indexSummary);
            _mainWindow?.RefreshResults();
        }
        finally {
            settingsWindow.SetRebuildState(false);
        }
    }

    private async Task RebuildIndexAsync(UserSettings settings) {
        _indexSummary = await _appIndexService.RebuildAsync(settings);
        _searchService.RefreshIndex(_appIndexService.Entries);
        _mainWindow?.UpdateIndexStatus(_indexSummary);
        _mainWindow?.RefreshResults();
    }

    private static bool HasIndexScopeChanged(UserSettings previousSettings, UserSettings newSettings) {
        if (previousSettings.SearchStartMenu != newSettings.SearchStartMenu ||
            previousSettings.SearchDesktop != newSettings.SearchDesktop ||
            previousSettings.SearchUwp != newSettings.SearchUwp) {
            return true;
        }

        if (previousSettings.CustomFolders.Count != newSettings.CustomFolders.Count) {
            return true;
        }

        return previousSettings.CustomFolders
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .SequenceEqual(
                newSettings.CustomFolders.OrderBy(path => path, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase) == false;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
        ShowErrorDialog("未处理的 UI 线程异常", e.Exception);
        e.Handled = true;
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e) {
        if (e.ExceptionObject is Exception ex) {
            ShowErrorDialog("未处理的应用域异常", ex);
        }
        else {
            System.Windows.MessageBox.Show(
                $"发生未知异常: {e.ExceptionObject}",
                "QuickLauncher - 致命错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) {
        ShowErrorDialog("未处理的异步任务异常", e.Exception);
        e.SetObserved();
    }

    private static void ShowStartupError(string step, Exception ex) {
        var info = new StringBuilder();
        info.AppendLine($"启动步骤: {step}");
        info.AppendLine();
        info.AppendLine($"异常类型: {ex.GetType().FullName}");
        info.AppendLine($"异常信息: {ex.Message}");
        if (ex.InnerException is not null) {
            info.AppendLine();
            info.AppendLine($"内部异常: {ex.InnerException.GetType().FullName}");
            info.AppendLine($"内部信息: {ex.InnerException.Message}");
        }
        info.AppendLine();
        info.AppendLine("--- 环境信息 ---");
        info.AppendLine($"操作系统: {RuntimeInformation.OSDescription}");
        info.AppendLine($"架构: {RuntimeInformation.OSArchitecture}");
        info.AppendLine($"运行时: {RuntimeInformation.FrameworkDescription}");
        info.AppendLine($"进程路径: {Environment.ProcessPath}");
        info.AppendLine($"工作目录: {Environment.CurrentDirectory}");
        info.AppendLine($"AppData: {Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}");
        info.AppendLine();
        info.AppendLine("--- 堆栈跟踪 ---");
        info.AppendLine(ex.StackTrace);

        System.Windows.MessageBox.Show(
            info.ToString(),
            $"QuickLauncher - 启动失败 [{step}]",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static void ShowErrorDialog(string title, Exception ex) {
        var info = new StringBuilder();
        info.AppendLine($"异常类型: {ex.GetType().FullName}");
        info.AppendLine($"异常信息: {ex.Message}");
        if (ex.InnerException is not null) {
            info.AppendLine();
            info.AppendLine($"内部异常: {ex.InnerException.GetType().FullName}");
            info.AppendLine($"内部信息: {ex.InnerException.Message}");
        }
        info.AppendLine();
        info.AppendLine("--- 堆栈跟踪 ---");
        info.AppendLine(ex.StackTrace);

        System.Windows.MessageBox.Show(
            info.ToString(),
            $"QuickLauncher - {title}",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
