using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using StockViewer.Models;
using StockViewer.Services;

namespace StockViewer;

/// <summary>
/// 桌面右下角可拖动的悬浮行情窗口。
/// 分三组滚动显示（指数 3+3、加密/黄金 4），每 5 秒切换；鼠标悬停时停止切换与数据刷新。
/// </summary>
public partial class TickerWindow : Window, INotifyPropertyChanged
{
    private readonly QuotesService _service = new();
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _switchTimer;

    private readonly List<QuoteGroup> _groups = new();
    private int _groupIndex;
    private bool _refreshing;

    public ObservableCollection<IndexQuote> CurrentItems { get; } = new();

    private string _currentTitle = "";
    public string CurrentTitle
    {
        get => _currentTitle;
        private set { if (_currentTitle != value) { _currentTitle = value; OnPropertyChanged(nameof(CurrentTitle)); } }
    }

    private int _columns = 3;
    public int Columns
    {
        get => _columns;
        private set { if (_columns != value) { _columns = value; OnPropertyChanged(nameof(Columns)); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public TickerWindow()
    {
        InitializeComponent();
        DataContext = this;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();

        _switchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _switchTimer.Tick += (_, _) => SwitchGroup();

        Loaded += async (_, _) => await RefreshAsync();
        Loaded += (_, _) => PositionBottomRight();
        Loaded += (_, _) => { _refreshTimer.Start(); _switchTimer.Start(); };
    }

    private async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            var quotes = await _service.FetchAllAsync();
            if (quotes.Count < 10) return;

            _groups.Clear();
            _groups.AddRange(QuotesService.GroupQuotes(quotes));

            if (_groupIndex >= _groups.Count) _groupIndex = 0;
            ApplyGroup();
        }
        catch
        {
            // 拉取失败时静默忽略，保持既有内容。
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void SwitchGroup()
    {
        if (_groups.Count == 0) return;
        _groupIndex = (_groupIndex + 1) % _groups.Count;
        ApplyGroup();
    }

    private void ApplyGroup()
    {
        var group = _groups[_groupIndex];
        CurrentTitle = group.Title;
        Columns = Math.Min(group.Items.Count, 3);
        CurrentItems.Clear();
        foreach (var q in group.Items) CurrentItems.Add(q);
    }

    private void PositionBottomRight()
    {
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - Width - 16;
        Top = wa.Bottom - Height - 16;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        // 悬停时暂停切换与刷新，便于查看。
        _switchTimer.Stop();
        _refreshTimer.Stop();
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        _switchTimer.Start();
        _refreshTimer.Start();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}