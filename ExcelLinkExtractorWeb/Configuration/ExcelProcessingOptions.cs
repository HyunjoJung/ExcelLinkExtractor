using System.ComponentModel.DataAnnotations;

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
    [Range(1, 100, ErrorMessage = "MaxFileSizeMB must be between 1 and 100.")]
    public int MaxFileSizeMB { get; set; } = 10;

    /// <summary>
    /// Maximum number of rows to search for headers. Default: 10.
    /// </summary>
    [Range(1, 50, ErrorMessage = "MaxHeaderSearchRows must be between 1 and 50.")]
    public int MaxHeaderSearchRows { get; set; } = 10;

    /// <summary>
    /// Maximum URL length for Excel hyperlinks. Default: 2000 characters.
    /// </summary>
    [Range(100, 10000, ErrorMessage = "MaxUrlLength must be between 100 and 10000.")]
    public int MaxUrlLength { get; set; } = 2000;

    /// <summary>
    /// Rate limit: Maximum requests per minute per IP. Default: 100.
    /// </summary>
    [Range(10, 10000, ErrorMessage = "RateLimitPerMinute must be between 10 and 10000.")]
    public int RateLimitPerMinute { get; set; } = 100;

    /// <summary>
    /// Gets the maximum file size in bytes.
    /// </summary>
    public int MaxFileSizeBytes => MaxFileSizeMB * 1024 * 1024;
}
