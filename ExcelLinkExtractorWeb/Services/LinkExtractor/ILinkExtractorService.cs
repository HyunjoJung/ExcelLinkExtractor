using ExcelLinkExtractorWeb.Services.LinkExtractor.Models;

namespace ExcelLinkExtractorWeb.Services.LinkExtractor;

public interface ILinkExtractorService
{
    Task<ExtractionResult> ExtractLinksAsync(Stream fileStream, string linkColumnName = "Title");
    Task<MergeResult> MergeFromFileAsync(Stream fileStream);
    byte[] CreateTemplate();
    byte[] CreateMergeTemplate();
}
