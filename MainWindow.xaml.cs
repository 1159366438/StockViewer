using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using StockViewer.Models;
using StockViewer.Services;

namespace StockViewer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly QuotesService _service = new();
    private readonly DispatcherTimer _timer;
    private readonly TickerWindow? _ticker;
    private bool _refreshing;

    public ObservableCollection<QuoteGroup> Groups { get; } = new();

    private string _statusText = "正在加载…";
    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();

        _ticker = new TickerWindow();

        Loaded += async (_, _) => await RefreshAsync();
        Loaded += (_, _) => _ticker?.Show();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void TickerToggle_Changed(object sender, RoutedEventArgs e)
    {
        // XAML 中 IsChecked="True" 会在 InitializeComponent 期间触发本事件，
        // 此时 _ticker 尚未初始化，需判空跳过。
        if (_ticker is null) return;

        if (TickerToggle.IsChecked == true) _ticker.Show();
        else _ticker.Hide();
    }

    private async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            var quotes = await _service.FetchAllAsync();
            Groups.Clear();
            foreach (var g in QuotesService.GroupQuotes(quotes)) Groups.Add(g);
            StatusText = $"更新于 {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusText = $"更新失败：{ex.Message}";
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}