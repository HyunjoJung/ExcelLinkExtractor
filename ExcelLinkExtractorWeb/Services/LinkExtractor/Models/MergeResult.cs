namespace ExcelLinkExtractorWeb.Services.LinkExtractor.Models;

public class MergeResult
{
    public int TotalRows { get; set; }
    public int LinksCreated { get; set; }
    public List<MergeLinkInfo> Links { get; set; } = new();
    public byte[]? OutputFile { get; set; }
    public string? ErrorMessage { get; set; }
}

public class MergeLinkInfo
{
    public int Row { get; set; }
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
}
