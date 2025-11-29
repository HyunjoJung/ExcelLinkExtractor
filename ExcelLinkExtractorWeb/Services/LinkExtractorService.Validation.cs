using Microsoft.Extensions.Logging;

namespace ExcelLinkExtractorWeb.Services;

public partial class LinkExtractorService
{
    /// <summary>
    /// Validates that the uploaded file is a valid Excel file.
    /// </summary>
    /// <param name="fileStream">The file stream to validate</param>
    /// <param name="fileName">The name of the file for logging purposes</param>
    /// <exception cref="InvalidFileFormatException">Thrown when file is invalid or too large</exception>
    private void ValidateExcelFile(Stream fileStream, string fileName = "unknown")
    {
        if (fileStream.Length > _options.MaxFileSizeBytes)
        {
            _logger.LogWarning("File {FileName} exceeds maximum size: {FileSize} bytes", fileName, fileStream.Length);
            throw new InvalidFileFormatException(
                message: $"File size ({fileStream.Length / 1024 / 1024}MB) exceeds maximum allowed size of {_options.MaxFileSizeMB}MB.",
                recoverySuggestion: "💡 Tip: Try reducing the file size by removing unnecessary columns, rows, or formatting. Or split your data into smaller files."
            );
        }

        if (fileStream.Length == 0)
        {
            _logger.LogWarning("File {FileName} is empty", fileName);
            throw new InvalidFileFormatException(
                message: "File is empty (0 bytes).",
                recoverySuggestion: "💡 Tip: Make sure the file uploaded correctly. Try re-saving your Excel file and uploading again."
            );
        }

        // Validate file signature (magic bytes)
        var buffer = new byte[8];
        var originalPosition = fileStream.Position;
        fileStream.Position = 0;

        var bytesRead = fileStream.Read(buffer, 0, buffer.Length);
        fileStream.Position = originalPosition;

        if (bytesRead < 4)
        {
            _logger.LogWarning("File {FileName} is too small to be a valid Excel file", fileName);
            throw new InvalidFileFormatException(
                message: "File is too small to be a valid Excel file.",
                recoverySuggestion: "💡 Tip: The file may be corrupted. Try opening it in Excel and re-saving as .xlsx format."
            );
        }

        // Check for .xlsx signature (ZIP/PK format)
        bool isXlsx = buffer[0] == XlsxSignature[0] &&
                      buffer[1] == XlsxSignature[1] &&
                      buffer[2] == XlsxSignature[2] &&
                      buffer[3] == XlsxSignature[3];

        // Check for .xls signature (OLE2 format)
        bool isXls = bytesRead >= 8 &&
                     buffer[0] == XlsSignature[0] &&
                     buffer[1] == XlsSignature[1] &&
                     buffer[2] == XlsSignature[2] &&
                     buffer[3] == XlsSignature[3] &&
                     buffer[4] == XlsSignature[4] &&
                     buffer[5] == XlsSignature[5] &&
                     buffer[6] == XlsSignature[6] &&
                     buffer[7] == XlsSignature[7];

        if (!isXlsx && !isXls)
        {
            _logger.LogWarning("File {FileName} has invalid Excel file signature", fileName);
            throw new InvalidFileFormatException(
                message: "File is not a valid Excel file (.xlsx or .xls).",
                recoverySuggestion: "💡 Tip: Make sure the file is actually an Excel file. If it's a CSV or other format, open it in Excel and save it as '.xlsx' format."
            );
        }

        _logger.LogInformation("File {FileName} validated successfully ({FileSize} bytes, {FileType})",
            fileName, fileStream.Length, isXlsx ? "XLSX" : "XLS");
    }
}
