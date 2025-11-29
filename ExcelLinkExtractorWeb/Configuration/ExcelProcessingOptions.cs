namespace ExcelLinkExtractorWeb.Configuration;

/// <summary>
/// Configuration options for Excel file processing.
/// </summary>
public class ExcelProcessingOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "ExcelProcessing";

    /// <summary>
    /// Maximum file size in megabytes. Default: 10MB.
    /// </summary>
    public int MaxFileSizeMB { get; set; } = 10;

    /// <summary>
    /// Maximum number of rows to search for headers. Default: 10.
    /// </summary>
    public int MaxHeaderSearchRows { get; set; } = 10;

    /// <summary>
    /// Maximum URL length for Excel hyperlinks. Default: 2000 characters.
    /// </summary>
    public int MaxUrlLength { get; set; } = 2000;

    /// <summary>
    /// Rate limit: Maximum requests per minute per IP. Default: 100.
    /// </summary>
    public int RateLimitPerMinute { get; set; } = 100;

    /// <summary>
    /// Gets the maximum file size in bytes.
    /// </summary>
    public int MaxFileSizeBytes => MaxFileSizeMB * 1024 * 1024;
}
