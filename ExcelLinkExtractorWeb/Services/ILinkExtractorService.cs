namespace ExcelLinkExtractorWeb.Services;

/// <summary>
/// Service interface for extracting and merging hyperlinks in Excel spreadsheets.
/// </summary>
public interface ILinkExtractorService
{
    /// <summary>
    /// Extracts hyperlinks from a column in an Excel spreadsheet asynchronously.
    /// </summary>
    /// <param name="fileStream">The Excel file stream to process</param>
    /// <param name="linkColumnName">Name of the column containing hyperlinks (default: "Title")</param>
    /// <returns>Extraction result containing found links and output file</returns>
    Task<LinkExtractorService.ExtractionResult> ExtractLinksAsync(Stream fileStream, string linkColumnName = "Title");

    /// <summary>
    /// Merges Title and URL columns into clickable hyperlinks asynchronously.
    /// </summary>
    /// <param name="fileStream">The Excel file stream to process</param>
    /// <returns>Merge result containing created links and output file</returns>
    Task<LinkExtractorService.MergeResult> MergeFromFileAsync(Stream fileStream);

    /// <summary>
    /// Creates a sample template file for link extraction.
    /// </summary>
    /// <returns>Byte array containing the template Excel file</returns>
    byte[] CreateTemplate();

    /// <summary>
    /// Creates a sample template file for link merging.
    /// </summary>
    /// <returns>Byte array containing the template Excel file</returns>
    byte[] CreateMergeTemplate();
}
