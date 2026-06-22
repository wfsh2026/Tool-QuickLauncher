using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using QuickLauncher.Models;
using QuickLauncher.Services;
using Forms = System.Windows.Forms;

namespace QuickLauncher;

public partial class SettingsWindow : Window {
    private readonly ObservableCollection<string> _customFolders;
    private readonly ObservableCollection<HiddenEntry> _hiddenEntries;
    private readonly HiddenIndexService _hiddenIndexService;
    private bool _isLoading = true;

    public event EventHandler<UserSettings>? SettingsSaved;
    public event EventHandler<UserSettings>? RebuildRequested;
    public event EventHandler? HiddenChanged;

    public string ApplicationVersion { get; set; } = string.Empty;

    public SettingsWindow(
        UserSettings currentSettings,
        IndexSummary indexSummary,
        HiddenIndexService hiddenIndexService) {
        InitializeComponent();

        _hiddenIndexService = hiddenIndexService;
        _customFolders = new ObservableCollection<string>(currentSettings.CustomFolders);
        CustomFoldersListBox.ItemsSource = _customFolders;

        _hiddenEntries = new ObservableCollection<HiddenEntry>(_hiddenIndexService.GetAll());
        HiddenEntriesListBox.ItemsSource = _hiddenEntries;
        UpdateHiddenEmptyState();

        PopulateKeys();
        LoadSettings(currentSettings.Clone());
        UpdateIndexStatus(indexSummary);

        _isLoading = false;
    }

    private void PopulateKeys() {
        KeyComboBox.ItemsSource = BuildSupportedKeys();
        KeyComboBox.SelectedItem = Key.Space;
    }

    private void LoadSettings(UserSettings settings) {
        CtrlCheckBox.IsChecked = settings.Hotkey.Control;
        AltCheckBox.IsChecked = settings.Hotkey.Alt;
        ShiftCheckBox.IsChecked = settings.Hotkey.Shift;
        WinCheckBox.IsChecked = settings.Hotkey.Windows;
        KeyComboBox.SelectedItem = settings.Hotkey.Key;

        RunAtStartupCheckBox.IsChecked = settings.RunAtStartup;
        HideAfterLaunchCheckBox.IsChecked = settings.HideAfterLaunch;
        SearchStartMenuCheckBox.IsChecked = settings.SearchStartMenu;
        SearchDesktopCheckBox.IsChecked = settings.SearchDesktop;
        SearchUwpCheckBox.IsChecked = settings.SearchUwp;
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e) {
        AutoSave();
    }

    private void OnSettingChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
        AutoSave();
    }

    private void AutoSave() {
        if (_isLoading) {
            return;
        }

        var settings = BuildSettings();
        if (settings is not null) {
            SettingsSaved?.Invoke(this, settings);
        }
    }

    private void AddFolderButton_OnClick(object sender, RoutedEventArgs e) {
        using var dialog = new Forms.FolderBrowserDialog {
            Description = "选择需要加入搜索范围的目录"
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath)) {
            return;
        }

        if (_customFolders.Any(path => string.Equals(path, dialog.SelectedPath, StringComparison.OrdinalIgnoreCase))) {
            return;
        }

        _customFolders.Add(dialog.SelectedPath);
        AutoSave();
    }

    private void RemoveFolderButton_OnClick(object sender, RoutedEventArgs e) {
        if (CustomFoldersListBox.SelectedItem is string selectedFolder) {
            _customFolders.Remove(selectedFolder);
            AutoSave();
        }
    }

    public void UpdateIndexStatus(IndexSummary summary) {
        if (summary.EntryCount <= 0 || summary.UpdatedAt is null) {
            IndexStatusTextBlock.Text = "暂无索引缓存。";
            return;
        }

        IndexStatusTextBlock.Text = $"已索引 {summary.EntryCount} 个应用，上次更新 {summary.UpdatedAt:yyyy-MM-dd HH:mm:ss}";
    }

    public void SetRebuildState(bool isBusy) {
        RebuildIndexButton.IsEnabled = !isBusy;
        RebuildIndexButton.Content = isBusy ? "重建中..." : "重建索引";
    }

    private void RebuildIndexButton_OnClick(object sender, RoutedEventArgs e) {
        var settings = BuildSettings();
        if (settings is null) {
            return;
        }

        RebuildRequested?.Invoke(this, settings);
    }

    private void UnhideButton_OnClick(object sender, RoutedEventArgs e) {
        if (sender is not System.Windows.Controls.Button button ||
            button.DataContext is not HiddenEntry entry) {
            return;
        }

        if (_hiddenIndexService.Unhide(entry.Key)) {
            _hiddenEntries.Remove(entry);
            UpdateHiddenEmptyState();
            HiddenChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateHiddenEmptyState() {
        if (_hiddenEntries.Count > 0) {
            HiddenEntriesEmptyTextBlock.Visibility = Visibility.Collapsed;
            HiddenEntriesListBox.Visibility = Visibility.Visible;
        }
        else {
            HiddenEntriesEmptyTextBlock.Visibility = Visibility.Visible;
            HiddenEntriesListBox.Visibility = Visibility.Collapsed;
        }
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        if (e.ClickCount == 1) {
            DragMove();
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) {
        Close();
    }

    private UserSettings? BuildSettings() {
        var hotkey = new HotkeyGesture {
            Control = CtrlCheckBox.IsChecked == true,
            Alt = AltCheckBox.IsChecked == true,
            Shift = ShiftCheckBox.IsChecked == true,
            Windows = WinCheckBox.IsChecked == true,
            Key = KeyComboBox.SelectedItem is Key key ? key : Key.None
        };

        if (!hotkey.IsValid()) {
            return null;
        }

        return new UserSettings {
            Hotkey = hotkey,
            RunAtStartup = RunAtStartupCheckBox.IsChecked == true,
            HideAfterLaunch = HideAfterLaunchCheckBox.IsChecked == true,
            SearchStartMenu = SearchStartMenuCheckBox.IsChecked == true,
            SearchDesktop = SearchDesktopCheckBox.IsChecked == true,
            SearchUwp = SearchUwpCheckBox.IsChecked == true,
            CustomFolders = [.. _customFolders]
        };
    }

    private static List<Key> BuildSupportedKeys() {
        var keys = new List<Key> { Key.Space };

        for (var key = Key.A; key <= Key.Z; key++) {
            keys.Add(key);
        }

        for (var key = Key.D0; key <= Key.D9; key++) {
            keys.Add(key);
        }

        for (var key = Key.F1; key <= Key.F12; key++) {
            keys.Add(key);
        }

        return keys;
    }
}

