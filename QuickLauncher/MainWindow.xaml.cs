using System.Text;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QuickLauncher.Models;
using QuickLauncher.Services;

namespace QuickLauncher;

public partial class MainWindow : Window {
    private const double CompactHeight = 118;
    private const double ExpandedHeight = 400;
    private readonly AppSearchService _searchService;
    private readonly LaunchService _launchService;
    private readonly RecentUsageService _recentUsageService;
    private readonly HiddenIndexService _hiddenIndexService;
    private readonly ObservableCollection<SearchResult> _results = [];
    private bool _allowClose;
    private bool _hideAfterLaunch = true;
    private bool _isLaunching;

    public event EventHandler? SettingsRequested;

    public event EventHandler? IndexInvalidated;

    public MainWindow(
        AppSearchService searchService,
        LaunchService launchService,
        RecentUsageService recentUsageService,
        HiddenIndexService hiddenIndexService) {
        _searchService = searchService;
        _launchService = launchService;
        _recentUsageService = recentUsageService;
        _hiddenIndexService = hiddenIndexService;

        InitializeComponent();
        ResultsListBox.ItemsSource = _results;
        UpdateLauncherLayout(false);

        SearchTextBox.GotKeyboardFocus += (_, _) => UpdatePlaceholder();
        SearchTextBox.LostKeyboardFocus += (_, _) => UpdatePlaceholder();
    }

    public void ShowLauncher() {
        PositionAtMouse();

        if (!IsVisible) {
            Show();
        }

        ResetSearchState();
        WindowState = WindowState.Normal;
        Topmost = true;
        Activate();
        Topmost = false;
        FocusSearchBox();
    }

    public void HideLauncher() {
        Hide();
    }

    public void PrepareForExit() {
        _allowClose = true;
    }

    public void SetHideAfterLaunch(bool hideAfterLaunch) {
        _hideAfterLaunch = hideAfterLaunch;
    }

    public void RefreshResults() {
        var query = GetCurrentQuery();
        if (string.IsNullOrWhiteSpace(query)) {
            _results.Clear();
            ResultsListBox.SelectedIndex = -1;
            UpdateEmptyState(query);
            UpdateLauncherLayout(false);
            return;
        }

        var results = _searchService.Search(query);

        _results.Clear();
        foreach (var result in results) {
            _results.Add(result);
        }

        ResultsListBox.SelectedIndex = _results.Count > 0 ? 0 : -1;
        UpdateEmptyState(query);
        UpdateLauncherLayout(true);
    }

    public void UpdateHotkeyDisplay(string text) {
        _ = text;
    }

    public void UpdateIndexStatus(QuickLauncher.Models.IndexSummary summary) {
        _ = summary;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e) {
        if (_allowClose) {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        HideLauncher();
    }

    private string GetCurrentQuery() {
        return SearchTextBox.Text.Trim();
    }

    private void FocusSearchBox() {
        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }

    private void ResetSearchState() {
        SearchTextBox.Text = string.Empty;
        _results.Clear();
        ResultsListBox.SelectedIndex = -1;
        UpdateEmptyState(string.Empty);
        UpdateLauncherLayout(false);
        UpdatePlaceholder();
    }

    private void UpdateEmptyState(string query) {
        if (string.IsNullOrWhiteSpace(query)) {
            EmptyStateTextBlock.Visibility = Visibility.Collapsed;
            return;
        }

        if (_results.Count > 0) {
            EmptyStateTextBlock.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyStateTextBlock.Text = string.IsNullOrWhiteSpace(query)
            ? "暂无推荐结果"
            : $"没有匹配“{query}”的应用";
        EmptyStateTextBlock.Visibility = Visibility.Visible;
    }

    private void UpdateLauncherLayout(bool showResults) {
        ResultsContainer.Visibility = showResults ? Visibility.Visible : Visibility.Collapsed;
        HintsBar.Visibility = showResults ? Visibility.Visible : Visibility.Collapsed;
        Height = showResults ? ExpandedHeight : CompactHeight;
    }

    private void UpdatePlaceholder() {
        PlaceholderText.Visibility =
            string.IsNullOrEmpty(SearchTextBox.Text) && !SearchTextBox.IsKeyboardFocused
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void PositionAtMouse() {
        var dpi = VisualTreeHelper.GetDpi(this);
        var scaleX = dpi.DpiScaleX;
        var scaleY = dpi.DpiScaleY;

        var cursor = System.Windows.Forms.Cursor.Position;
        var screen = System.Windows.Forms.Screen.FromPoint(cursor);
        var workArea = screen.WorkingArea;

        Left = (workArea.Left + (workArea.Width - Width * scaleX) / 2) / scaleX;
        Top = (workArea.Top + (workArea.Height - Height * scaleY) / 2) / scaleY;
    }

    private void MoveSelection(int offset) {
        if (_results.Count == 0) {
            return;
        }

        var nextIndex = ResultsListBox.SelectedIndex + offset;
        if (nextIndex < 0) {
            nextIndex = _results.Count - 1;
        }
        else if (nextIndex >= _results.Count) {
            nextIndex = 0;
        }

        ResultsListBox.SelectedIndex = nextIndex;
        ResultsListBox.ScrollIntoView(ResultsListBox.SelectedItem);
    }

    private async Task LaunchSelectedAsync() {
        if (_isLaunching || ResultsListBox.SelectedItem is not SearchResult result) {
            return;
        }

        _isLaunching = true;

        try {
            await _launchService.LaunchAsync(result.App);
            _recentUsageService.RecordLaunch(result.App.Id);
            RefreshResults();

            if (_hideAfterLaunch) {
                HideLauncher();
            }
            else {
                FocusSearchBox();
            }
        }
        catch (Exception exception) {
            System.Windows.MessageBox.Show(
                $"启动失败：{exception.Message}",
                "QuickLauncher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally {
            _isLaunching = false;
        }
    }

    private void SearchTextBox_OnTextChanged(object sender, TextChangedEventArgs e) {
        if (!IsLoaded) {
            return;
        }

        UpdatePlaceholder();
        RefreshResults();
    }

    private async void SearchTextBox_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
        if (e.Key == Key.Escape) {
            HideLauncher();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Control) {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down || e.Key == Key.NumPad2) {
            MoveSelection(1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up || e.Key == Key.NumPad8) {
            MoveSelection(-1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter) {
            e.Handled = true;
            await LaunchSelectedAsync();
        }
    }

    private async void ResultsListBox_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
        if (ItemsControl.ContainerFromElement(ResultsListBox, e.OriginalSource as DependencyObject) is not ListBoxItem) {
            return;
        }

        await LaunchSelectedAsync();
    }

    private void ResultsListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (ResultsListBox.SelectedItem is not null) {
            ResultsListBox.ScrollIntoView(ResultsListBox.SelectedItem);
        }
    }

    private void OpenSettingsMenuItem_OnClick(object sender, RoutedEventArgs e) {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ResultItemContextMenu_OnOpened(object sender, RoutedEventArgs e) {
        // 右键时把选中项切换为当前所悬停的条目，避免对错项操作。
        if (sender is ContextMenu menu && menu.PlacementTarget is ListBoxItem item) {
            ResultsListBox.SelectedItem = item.DataContext;
        }
    }

    private async void LaunchMenuItem_OnClick(object sender, RoutedEventArgs e) {
        await LaunchSelectedAsync();
    }

    private async void DeleteIndexMenuItem_OnClick(object sender, RoutedEventArgs e) {
        if (ResultsListBox.SelectedItem is not SearchResult result) {
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            $"确定要将“{result.App.DisplayName}”从搜索结果中删除吗？\n删除后可在设置 - 已删除的索引中恢复。",
            "QuickLauncher - 删除索引",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) {
            return;
        }

        try {
            _hiddenIndexService.Hide(result.App, result.SourceText);
            IndexInvalidated?.Invoke(this, EventArgs.Empty);
            RefreshResults();
        }
        catch (Exception exception) {
            System.Windows.MessageBox.Show(
                $"删除失败：{exception.Message}",
                "QuickLauncher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        await Task.CompletedTask;
    }
}
