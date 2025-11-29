namespace ExcelLinkExtractorWeb.Services;

/// <summary>
/// Exception thrown when Excel file processing fails.
/// </summary>
public class ExcelProcessingException : Exception
{
    /// <summary>
    /// Gets the recovery suggestion for resolving this error.
    /// </summary>
    public string? RecoverySuggestion { get; init; }

    public ExcelProcessingException(string message, string? recoverySuggestion = null) : base(message)
    {
        RecoverySuggestion = recoverySuggestion;
    }

    public ExcelProcessingException(string message, Exception innerException, string? recoverySuggestion = null)
        : base(message, innerException)
    {
        RecoverySuggestion = recoverySuggestion;
    }

    /// <summary>
    /// Gets the full error message including recovery suggestion.
    /// </summary>
    public string GetFullMessage()
    {
        return string.IsNullOrEmpty(RecoverySuggestion)
            ? Message
            : $"{Message} {RecoverySuggestion}";
    }
}

/// <summary>
/// Exception thrown when required column is not found in spreadsheet.
/// </summary>
public class InvalidColumnException : ExcelProcessingException
{
    public string ColumnName { get; }

    public InvalidColumnException(string columnName)
        : base(
            message: $"Column '{columnName}' not found in the first {10} rows of the spreadsheet.",
            recoverySuggestion: $"💡 Tip: Ensure your Excel file has a column header named exactly '{columnName}' (case-sensitive) in the first few rows. Check for extra spaces or different spelling."
        )
    {
        ColumnName = columnName;
    }

    public InvalidColumnException(string columnName, int maxSearchRows)
        : base(
            message: $"Column '{columnName}' not found in the first {maxSearchRows} rows of the spreadsheet.",
            recoverySuggestion: $"💡 Tip: Move the header row containing '{columnName}' to row 1-{maxSearchRows}, or check the column name spelling (case-sensitive)."
        )
    {
        ColumnName = columnName;
    }
}

/// <summary>
/// Exception thrown when uploaded file is not a valid Excel file.
/// </summary>
public class InvalidFileFormatException : ExcelProcessingException
{
    public InvalidFileFormatException(string message, string? recoverySuggestion = null)
        : base(message, recoverySuggestion)
    {
    }

    public InvalidFileFormatException(string message, Exception innerException, string? recoverySuggestion = null)
        : base(message, innerException, recoverySuggestion)
    {
    }
}
