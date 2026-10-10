namespace StockViewer.Models;

/// <summary>单个大盘指数的行情数据。</summary>
public class IndexQuote
{
    public string Name { get; set; } = "";

    public string Code { get; set; } = "";

    /// <summary>市场标签：A股 / 港股 / 美股。</summary>
    public string Market { get; set; } = "";

    /// <summary>最新点位。</summary>
    public double Current { get; set; }

    /// <summary>涨跌额。</summary>
    public double Change { get; set; }

    /// <summary>涨跌幅（百分比数值，例如 0.05 表示 0.05%）。</summary>
    public double ChangePercent { get; set; }

    /// <summary>数据源返回的时间，可为空。</summary>
    public string UpdateTime { get; set; } = "";

    /// <summary>今日状态：休市 / 未开盘 / 盘中 / 已收盘。</summary>
    public string Status { get; set; } = "";
}