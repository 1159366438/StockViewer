using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace StockViewer.Converters;

/// <summary>根据涨跌数值返回画笔颜色：涨为红、跌为绿、平为灰（A 股习惯）。</summary>
public class TrendToBrushConverter : IValueConverter
{
    private static readonly Brush Up = new SolidColorBrush(Color.FromRgb(0xE0, 0x3A, 0x2E));
    private static readonly Brush Down = new SolidColorBrush(Color.FromRgb(0x00, 0x8A, 0x4B));
    private static readonly Brush Flat = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            if (d > 0) return Up;
            if (d < 0) return Down;
        }
        return Flat;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>把数值格式化为带正负号的字符串，正数显示 + 号，可通过 parameter 追加后缀（如 %）。</summary>
public class SignedNumberConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            var sign = d > 0 ? "+" : string.Empty;
            return sign + d.ToString("0.00", culture) + (parameter as string ?? string.Empty);
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>把字符串非空映射为可见（空字符串折叠隐藏），用于按需显示状态标签。</summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}