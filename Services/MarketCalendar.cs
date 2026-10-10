using System;

namespace StockViewer.Services;

/// <summary>指数所属市场。</summary>
public enum Market
{
    AShare,
    HK,
    US,
    Crypto,
    Metal,
}

/// <summary>
/// 计算指数"今日状态"：休市 / 未开盘 / 盘中 / 已收盘。
/// 入参 allTime 均为北京时间。
/// </summary>
public static class MarketCalendar
{
    private static readonly TimeZoneInfo ChinaStandardTime =
        TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
    private static readonly TimeZoneInfo UsEasternTime =
        TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

    // A 股 2026 年休市安排（自然日区间）。
    private static readonly (DateOnly Start, DateOnly End)[] AShareHolidays =
    {
        (new(2026, 1, 1), new(2026, 1, 3)),   // 元旦
        (new(2026, 2, 15), new(2026, 2, 23)), // 春节
        (new(2026, 4, 4), new(2026, 4, 6)),   // 清明
        (new(2026, 5, 1), new(2026, 5, 5)),   // 劳动节
        (new(2026, 6, 19), new(2026, 6, 21)), // 端午
        (new(2026, 9, 25), new(2026, 9, 27)), // 中秋
        (new(2026, 10, 1), new(2026, 10, 7)), // 国庆
    };

    // 港股 2026 年休市安排（自然日区间）。
    private static readonly (DateOnly Start, DateOnly End)[] HkHolidays =
    {
        (new(2026, 1, 1), new(2026, 1, 1)),     // 元旦
        (new(2026, 2, 16), new(2026, 2, 18)),   // 农历新年
        (new(2026, 4, 3), new(2026, 4, 7)),     // 耶稣受难节 + 复活节
        (new(2026, 5, 1), new(2026, 5, 1)),     // 劳动节
        (new(2026, 5, 25), new(2026, 5, 25)),   // 佛诞
        (new(2026, 6, 19), new(2026, 6, 19)),   // 端午节
        (new(2026, 7, 1), new(2026, 7, 1)),     // 香港特区成立纪念日
        (new(2026, 10, 1), new(2026, 10, 1)),   // 国庆
        (new(2026, 10, 19), new(2026, 10, 19)), // 重阳节
        (new(2026, 12, 25), new(2026, 12, 25)), // 圣诞节
        (new(2026, 12, 26), new(2026, 12, 26)), // 节礼日
    };

    // 美股（纽交所）2026 年休市日。
    private static readonly (int Month, int Day)[] UsHolidays =
    {
        (1, 1),   // 元旦
        (1, 19),  // 马丁·路德·金纪念日
        (2, 16),  // 总统日
        (4, 3),   // 耶稣受难节
        (5, 25),  // 阵亡将士纪念日
        (6, 19),  // 六月节
        (7, 3),   // 独立日（7/4 为周六，顺延至周五休市）
        (9, 7),   // 劳动节
        (11, 26), // 感恩节
        (12, 25), // 圣诞节
    };

    /// <summary>根据市场标签字符串解析为 <see cref="Market"/>。</summary>
    public static Market FromLabel(string label) => label switch
    {
        "A股" => Market.AShare,
        "港股" => Market.HK,
        "美股" => Market.US,
        "加密货币" => Market.Crypto,
        "黄金" => Market.Metal,
        _ => Market.US,
    };

    /// <summary>返回指数今日状态文案。</summary>
    public static string GetStatus(Market market, DateTime beijingNow)
    {
        // 加密货币无开市/休市状态。
        if (market == Market.Crypto) return "";

        // 黄金（伦敦金/纽约金）周末休市，工作日近似全天交易。
        if (market == Market.Metal)
            return beijingNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? "休市" : "盘中";

        var date = beijingNow.Date;
        var time = beijingNow.TimeOfDay;

        if (market == Market.US)
            (date, time) = ToUsEastern(beijingNow);

        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return "休市";
        if (IsHoliday(market, date))
            return "休市";

        var (open, close) = SessionHours(market);
        if (time < open) return "未开盘";
        if (time >= close) return "已收盘";
        return "盘中";
    }

    private static bool IsHoliday(Market market, DateTime date) => market switch
    {
        Market.AShare => InRange(date, AShareHolidays),
        Market.HK => InRange(date, HkHolidays),
        Market.US => Array.Exists(UsHolidays, h => h.Month == date.Month && h.Day == date.Day),
        _ => false,
    };

    private static bool InRange(DateTime date, (DateOnly Start, DateOnly End)[] ranges)
    {
        var d = DateOnly.FromDateTime(date);
        foreach (var (start, end) in ranges)
            if (d >= start && d <= end) return true;
        return false;
    }

    private static (TimeSpan open, TimeSpan close) SessionHours(Market market) => market switch
    {
        Market.AShare => (new(9, 30, 0), new(15, 0, 0)),
        Market.HK => (new(9, 30, 0), new(16, 0, 0)),
        _ => (new(9, 30, 0), new(16, 0, 0)),
    };

    /// <summary>把北京时间换算为美东时间并拆出日期与时刻。</summary>
    private static (DateTime date, TimeSpan time) ToUsEastern(DateTime beijingNow)
    {
        var utc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(beijingNow, DateTimeKind.Unspecified), ChinaStandardTime);
        var eastern = TimeZoneInfo.ConvertTimeFromUtc(utc, UsEasternTime);
        return (eastern.Date, eastern.TimeOfDay);
    }
}