namespace ExcelLinkExtractorWeb.Services;

/// <summary>
/// Exception thrown when Excel file processing fails.
/// </summary>
public class ExcelProcessingException : Exception
{
    public ExcelProcessingException(string message) : base(message)
    {
    }

    public ExcelProcessingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when required column is not found in spreadsheet.
/// </summary>
public class InvalidColumnException : ExcelProcessingException
{
    public string ColumnName { get; }

    public InvalidColumnException(string columnName)
        : base($"Column '{columnName}' not found in the spreadsheet.")
    {
        ColumnName = columnName;
    }
}

/// <summary>
/// Exception thrown when uploaded file is not a valid Excel file.
/// </summary>
public class InvalidFileFormatException : ExcelProcessingException
{
    public InvalidFileFormatException(string message) : base(message)
    {
    }

    public InvalidFileFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
