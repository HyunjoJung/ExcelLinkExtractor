namespace ExcelLinkExtractorWeb.Services.LinkExtractor.Models;

public class ExtractionResult
{
    public int TotalRows { get; set; }
    public int LinksFound { get; set; }
    public List<LinkInfo> Links { get; set; } = new();
    public byte[]? OutputFile { get; set; }
    public string? ErrorMessage { get; set; }
}

public class LinkInfo
{
    public int Row { get; set; }
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
}
