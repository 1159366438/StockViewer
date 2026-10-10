using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using StockViewer.Models;

namespace StockViewer.Services;

/// <summary>
/// 通过新浪财经接口获取大盘指数实时行情。
/// 接口返回 GBK 编码文本，需在应用启动时调用
/// <c>Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)</c>。
/// </summary>
public class QuotesService
{
    // 代码按返回内容中各市场的字段结构区分：
    // A 股指数（s_）、港股指数（rt_hk）、美股指数（gb_）。
    private static readonly (string Sina, string Code, string Market)[] Indexes =
    {
        ("s_sh000001", "000001.SH", "A股"),
        ("s_sz399001", "399001.SZ", "A股"),
        ("rt_hkHSTECH", "HSTECH", "港股"),
        ("gb_dji", "DJI", "美股"),
        ("gb_ixic", "IXIC", "美股"),
        ("gb_inx", "INX", "美股"),
    };

    // 加密货币（gate.io 现货，交易对 → 显示名）。
    private static readonly (string Pair, string Name)[] Cryptos =
    {
        ("BTC_USDT", "BTC"),
        ("SOL_USDT", "SOL"),
        ("ETH_USDT", "ETH"),
    };

    private static readonly Encoding Gbk = Encoding.GetEncoding("GBK");

    private readonly HttpClient _http;

    public QuotesService()
    {
        _http = new HttpClient(new HttpClientHandler(), disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        _http.DefaultRequestHeaders.Add("Referer", "https://finance.sina.com.cn/");
        _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    }

    public async Task<List<IndexQuote>> FetchAllAsync(CancellationToken ct = default)
    {
        var quotes = new List<IndexQuote>(Indexes.Length + Cryptos.Length + 1);

        // 1. 指数（新浪，GBK）。
        var codes = string.Join(",", Indexes.Select(i => i.Sina));
        var text = Gbk.GetString(await _http.GetByteArrayAsync("https://hq.sinajs.cn/list=" + codes, ct));
        foreach (var (sina, code, market) in Indexes)
        {
            var parts = ExtractFields(text, sina);
            if (parts is null) continue;

            var quote = sina switch
            {
                "s_sh000001" or "s_sz399001" => ParseA(parts, code, market),
                "rt_hkHSTECH" => ParseHk(parts, code, market),
                _ => ParseUs(parts, code, market),
            };
            quote.Status = MarketCalendar.GetStatus(MarketCalendar.FromLabel(market), DateTime.Now);
            quotes.Add(quote);
        }

        // 2. 加密货币（gate.io，JSON）。
        quotes.AddRange(await FetchCryptoAsync(ct));

        // 3. 黄金（新浪，GBK）。
        var goldText = Gbk.GetString(await _http.GetByteArrayAsync("https://hq.sinajs.cn/list=hf_XAU", ct));
        var gold = ParseGold(ExtractFields(goldText, "hf_XAU"));
        if (gold is not null)
        {
            gold.Status = MarketCalendar.GetStatus(MarketCalendar.FromLabel(gold.Market), DateTime.Now);
            quotes.Add(gold);
        }

        return quotes;
    }

    /// <summary>提取某指数行内双引号包裹的逗号分隔字段。</summary>
    private static string[]? ExtractFields(string text, string sina)
    {
        var marker = "hq_str_" + sina + "=\"";
        var start = text.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return null;
        start += marker.Length;
        var end = text.IndexOf('"', start);
        if (end < 0) return null;
        return text[start..end].Split(',');
    }

    /// <summary>A 股：名称,最新,涨跌额,涨跌幅%,成交量,成交额。</summary>
    private static IndexQuote ParseA(string[] p, string code, string market) => new()
    {
        Name = p.Length > 0 ? p[0] : code,
        Code = code,
        Market = market,
        Current = Safe(p, 1),
        Change = Safe(p, 2),
        ChangePercent = Safe(p, 3),
    };

    /// <summary>港股：1=名称,4=最新,7=涨跌额,8=涨跌幅%,17=日期,18=时间。</summary>
    private static IndexQuote ParseHk(string[] p, string code, string market) => new()
    {
        Name = p.Length > 1 ? p[1] : "恒生科技指数",
        Code = code,
        Market = market,
        Current = Safe(p, 4),
        Change = Safe(p, 7),
        ChangePercent = Safe(p, 8),
        UpdateTime = p.Length > 18 ? p[17] + " " + p[18] : "",
    };

    /// <summary>美股：0=名称,1=最新,2=涨跌幅%,3=时间,4=涨跌额。</summary>
    private static IndexQuote ParseUs(string[] p, string code, string market) => new()
    {
        Name = p.Length > 0 ? p[0] : code,
        Code = code,
        Market = market,
        Current = Safe(p, 1),
        Change = Safe(p, 4),
        ChangePercent = Safe(p, 2),
        UpdateTime = p.Length > 3 ? p[3] : "",
    };

    /// <summary>黄金（新浪 hf_XAU）：0=最新,7=昨收,12=日期。</summary>
    private static IndexQuote? ParseGold(string[]? p)
    {
        if (p is null || p.Length < 8) return null;

        var current = Safe(p, 0);
        var prev = Safe(p, 7);
        if (prev <= 0) prev = Safe(p, 1);
        var change = current - prev;

        return new IndexQuote
        {
            Name = "黄金",
            Code = "XAU",
            Market = "黄金",
            Current = current,
            Change = change,
            ChangePercent = prev > 0 ? change / prev * 100 : 0,
            UpdateTime = p.Length > 12 ? p[12] : "",
        };
    }

    /// <summary>拉取 BTC/SOL/ETH（gate.io 现货行情，24 小时涨跌幅）。</summary>
    private async Task<List<IndexQuote>> FetchCryptoAsync(CancellationToken ct)
    {
        var list = new List<IndexQuote>(Cryptos.Length);
        foreach (var (pair, name) in Cryptos)
        {
            try
            {
                var json = await _http.GetStringAsync(
                    "https://api.gateio.ws/api/v4/spot/tickers?currency_pair=" + pair, ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var last = GateDouble(item, "last");
                    var pct = GateDouble(item, "change_percentage");
                    var prev = 1 + pct / 100 > 0 ? last / (1 + pct / 100) : 0;
                    list.Add(new IndexQuote
                    {
                        Name = name,
                        Code = pair,
                        Market = "加密货币",
                        Current = last,
                        Change = last - prev,
                        ChangePercent = pct,
                    });
                    break;
                }
            }
            catch
            {
                // 单个币种拉取失败时跳过，保留其余结果。
            }
        }
        return list;
    }

    private static double GateDouble(JsonElement e, string prop)
    {
        if (!e.TryGetProperty(prop, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number) return v.TryGetDouble(out var d) ? d : 0;
        if (v.ValueKind == JsonValueKind.String &&
            double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) return s;
        return 0;
    }

    private static double Safe(string[] parts, int index) =>
        index < parts.Length && double.TryParse(parts[index], out var v) ? v : 0;

    /// <summary>按市场类别把平坦行情列表分组（标题顺序固定）。</summary>
    public static List<QuoteGroup> GroupQuotes(List<IndexQuote> quotes)
    {
        string[] order = { "A股", "港股", "美股", "加密货币", "黄金" };
        var groups = new List<QuoteGroup>();
        foreach (var title in order)
        {
            var items = quotes.Where(q => q.Market == title).ToList();
            if (items.Count > 0) groups.Add(new QuoteGroup { Title = title, Items = items });
        }
        return groups;
    }
}