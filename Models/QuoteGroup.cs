using System.Collections.Generic;

namespace StockViewer.Models;

/// <summary>一组行情，带分组标题（如 美股、加密货币）。</summary>
public class QuoteGroup
{
    public string Title { get; set; } = "";

    public List<IndexQuote> Items { get; set; } = new();
}