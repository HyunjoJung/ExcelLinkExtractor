using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Logging;

namespace ExcelLinkExtractorWeb.Services;

/// <summary>
/// Service for extracting and merging hyperlinks in Excel spreadsheets.
/// Provides functionality to extract URLs from cells and merge Title + URL columns into clickable hyperlinks.
/// </summary>
public class LinkExtractorService
{
    private readonly ILogger<LinkExtractorService> _logger;

    // File validation constants
    private const int MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB
    private const int MaxHeaderSearchRows = 10;
    private const int MaxUrlLength = 2000; // Excel hyperlink limit (~2083 chars, use 2000 for safety)

    // Excel file signatures (magic bytes)
    private static readonly byte[] XlsxSignature = { 0x50, 0x4B, 0x03, 0x04 }; // PK.. (ZIP format)
    private static readonly byte[] XlsSignature = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }; // OLE2 format

    public LinkExtractorService(ILogger<LinkExtractorService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates that the uploaded file is a valid Excel file.
    /// </summary>
    /// <param name="fileStream">The file stream to validate</param>
    /// <param name="fileName">The name of the file for logging purposes</param>
    /// <exception cref="InvalidFileFormatException">Thrown when file is invalid or too large</exception>
    private void ValidateExcelFile(Stream fileStream, string fileName = "unknown")
    {
        // Validate file size
        if (fileStream.Length > MaxFileSizeBytes)
        {
            _logger.LogWarning("File {FileName} exceeds maximum size: {FileSize} bytes", fileName, fileStream.Length);
            throw new InvalidFileFormatException($"File size exceeds maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)}MB.");
        }

        if (fileStream.Length == 0)
        {
            _logger.LogWarning("File {FileName} is empty", fileName);
            throw new InvalidFileFormatException("File is empty.");
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
            throw new InvalidFileFormatException("File is too small to be a valid Excel file.");
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
            throw new InvalidFileFormatException("File is not a valid Excel file (.xlsx or .xls). Please upload a valid Excel spreadsheet.");
        }

        _logger.LogInformation("File {FileName} validated successfully ({FileSize} bytes, {FileType})",
            fileName, fileStream.Length, isXlsx ? "XLSX" : "XLS");
    }

    /// <summary>
    /// Result of link extraction operation.
    /// </summary>
    public class ExtractionResult
    {
        public int TotalRows { get; set; }
        public int LinksFound { get; set; }
        public List<LinkInfo> Links { get; set; } = new();
        public byte[]? OutputFile { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Information about an extracted link.
    /// </summary>
    public class LinkInfo
    {
        public int Row { get; set; }
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
    }

    /// <summary>
    /// Extracts hyperlinks from a column in an Excel spreadsheet asynchronously.
    /// </summary>
    /// <param name="fileStream">The Excel file stream to process</param>
    /// <param name="linkColumnName">Name of the column containing hyperlinks (default: "Title")</param>
    /// <returns>Extraction result containing found links and output file</returns>
    public async Task<ExtractionResult> ExtractLinksAsync(Stream fileStream, string linkColumnName = "Title")
    {
        return await Task.Run(() => ExtractLinks(fileStream, linkColumnName));
    }

    /// <summary>
    /// Extracts hyperlinks from a column in an Excel spreadsheet.
    /// </summary>
    /// <param name="fileStream">The Excel file stream to process</param>
    /// <param name="linkColumnName">Name of the column containing hyperlinks</param>
    /// <returns>Extraction result containing found links and output file</returns>
    private ExtractionResult ExtractLinks(Stream fileStream, string linkColumnName)
    {
        var result = new ExtractionResult();

        try
        {
            // Validate file before processing
            ValidateExcelFile(fileStream);

            _logger.LogInformation("Starting link extraction for column '{ColumnName}'", linkColumnName);

            using var document = SpreadsheetDocument.Open(fileStream, false);
            var workbookPart = document.WorkbookPart!;
            var worksheetPart = workbookPart.WorksheetParts.First();
            var worksheet = worksheetPart.Worksheet;
            var sheetData = worksheet.GetFirstChild<SheetData>()!;

            // Find header row
            int? headerRowIndex = null;
            int? targetColumnIndex = null;

            foreach (var row in sheetData.Elements<Row>().Take(MaxHeaderSearchRows))
            {
                foreach (var cell in row.Elements<Cell>())
                {
                    var cellValue = GetCellValue(cell, workbookPart);
                    if (cellValue.Equals(linkColumnName, StringComparison.OrdinalIgnoreCase))
                    {
                        headerRowIndex = (int)row.RowIndex!.Value;
                        targetColumnIndex = GetColumnIndex(cell.CellReference!.Value!);
                        break;
                    }
                }
                if (headerRowIndex != null) break;
            }

            if (targetColumnIndex == null || headerRowIndex == null)
            {
                _logger.LogWarning("Column '{ColumnName}' not found in spreadsheet", linkColumnName);
                throw new InvalidColumnException(linkColumnName);
            }

            _logger.LogDebug("Found column '{ColumnName}' at column index {ColumnIndex}, header row {HeaderRow}",
                linkColumnName, targetColumnIndex, headerRowIndex);

            // Create new workbook
            var outputStream = new MemoryStream();
            using (var newDocument = SpreadsheetDocument.Create(outputStream, SpreadsheetDocumentType.Workbook))
            {
                var newWorkbookPart = newDocument.AddWorkbookPart();
                newWorkbookPart.Workbook = new Workbook();

                var newWorksheetPart = newWorkbookPart.AddNewPart<WorksheetPart>();
                newWorksheetPart.Worksheet = new Worksheet(new SheetData());

                var sheets = newWorkbookPart.Workbook.AppendChild(new Sheets());
                var sheet = new Sheet()
                {
                    Id = newWorkbookPart.GetIdOfPart(newWorksheetPart),
                    SheetId = 1,
                    Name = "Extracted Links"
                };
                sheets.Append(sheet);

                var newSheetData = newWorksheetPart.Worksheet.GetFirstChild<SheetData>()!;

                // Create stylesheet
                var stylesPart = newWorkbookPart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = CreateStylesheet();

                // Copy header row with bold style
                var headerRow = new Row { RowIndex = 1 };
                var originalHeaderRow = sheetData.Elements<Row>().First(r => r.RowIndex == headerRowIndex);

                foreach (var cell in originalHeaderRow.Elements<Cell>())
                {
                    var newCell = new Cell
                    {
                        CellReference = GetCellReference(1, GetColumnIndex(cell.CellReference!.Value!)),
                        DataType = CellValues.String,
                        CellValue = new CellValue(GetCellValue(cell, workbookPart)),
                        StyleIndex = 1 // Bold style
                    };
                    headerRow.Append(newCell);
                }
                newSheetData.Append(headerRow);

                // Process data rows
                uint newRowIndex = 2;
                var rows = sheetData.Elements<Row>().Where(r => r.RowIndex > headerRowIndex).ToList();

                foreach (var row in rows)
                {
                    var newRow = new Row { RowIndex = newRowIndex };
                    bool hasData = false;

                    foreach (var cell in row.Elements<Cell>())
                    {
                        var colIndex = GetColumnIndex(cell.CellReference!.Value!);
                        var cellValue = GetCellValue(cell, workbookPart);

                        if (!string.IsNullOrWhiteSpace(cellValue))
                            hasData = true;

                        var newCell = new Cell
                        {
                            CellReference = GetCellReference(newRowIndex, colIndex),
                            DataType = CellValues.String,
                            CellValue = new CellValue(cellValue)
                        };

                        // Check for hyperlink
                        var hyperlink = GetHyperlink(worksheetPart, cell.CellReference!.Value!);
                        if (hyperlink != null)
                        {
                            newCell.StyleIndex = 2; // Hyperlink style

                            if (colIndex == targetColumnIndex)
                            {
                                result.Links.Add(new LinkInfo
                                {
                                    Row = (int)row.RowIndex!.Value,
                                    Title = cellValue,
                                    Url = hyperlink
                                });
                            }
                        }

                        newRow.Append(newCell);
                    }

                    if (hasData)
                    {
                        // Add extracted URL to column B
                        var hyperlink = GetHyperlink(worksheetPart, GetCellReference((uint)row.RowIndex!.Value, targetColumnIndex.Value));
                        if (!string.IsNullOrEmpty(hyperlink))
                        {
                            var urlCell = newRow.Elements<Cell>().FirstOrDefault(c => GetColumnIndex(c.CellReference!.Value!) == 2);
                            if (urlCell == null)
                            {
                                urlCell = new Cell
                                {
                                    CellReference = GetCellReference(newRowIndex, 2),
                                    DataType = CellValues.String,
                                    CellValue = new CellValue(hyperlink)
                                };
                                newRow.Append(urlCell);
                            }
                            else
                            {
                                urlCell.CellValue = new CellValue(hyperlink);
                            }
                        }

                        newSheetData.Append(newRow);
                        newRowIndex++;
                    }
                }

                result.TotalRows = (int)(newRowIndex - 2);
                result.LinksFound = result.Links.Count;

                // Add column widths
                var columns = new Columns();
                columns.Append(new Column { Min = 1, Max = 1, Width = 30, CustomWidth = true });
                columns.Append(new Column { Min = 2, Max = 2, Width = 50, CustomWidth = true });
                newWorksheetPart.Worksheet.InsertBefore(columns, newSheetData);

                newWorkbookPart.Workbook.Save();
            }

            result.OutputFile = outputStream.ToArray();

            _logger.LogInformation("Link extraction completed successfully. Total rows: {TotalRows}, Links found: {LinksFound}",
                result.TotalRows, result.LinksFound);
        }
        catch (InvalidFileFormatException ex)
        {
            _logger.LogError(ex, "Invalid file format during link extraction");
            result.ErrorMessage = ex.Message;
        }
        catch (InvalidColumnException ex)
        {
            _logger.LogError(ex, "Column not found during link extraction");
            result.ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during link extraction");
            result.ErrorMessage = $"Error processing file: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Creates a sample template file for link extraction.
    /// </summary>
    /// <returns>Byte array containing the template Excel file</returns>
    public byte[] CreateTemplate()
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            var sheet = new Sheet()
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Data"
            };
            sheets.Append(sheet);

            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()!;

            // Create stylesheet
            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = CreateStylesheet();

            // Header row with blue background
            var headerRow = new Row { RowIndex = 1 };
            headerRow.Append(new Cell
            {
                CellReference = "A1",
                DataType = CellValues.String,
                CellValue = new CellValue("Title"),
                StyleIndex = 3 // Header with blue background
            });
            headerRow.Append(new Cell
            {
                CellReference = "B1",
                DataType = CellValues.String,
                CellValue = new CellValue("URL"),
                StyleIndex = 3
            });
            sheetData.Append(headerRow);

            // Sample data with hyperlinks
            var row2 = new Row { RowIndex = 2 };
            row2.Append(new Cell
            {
                CellReference = "A2",
                DataType = CellValues.String,
                CellValue = new CellValue("Example Link 1"),
                StyleIndex = 2 // Hyperlink style
            });
            sheetData.Append(row2);

            var row3 = new Row { RowIndex = 3 };
            row3.Append(new Cell
            {
                CellReference = "A3",
                DataType = CellValues.String,
                CellValue = new CellValue("Example Link 2"),
                StyleIndex = 2
            });
            sheetData.Append(row3);

            // Add hyperlinks
            var hyperlinks = new Hyperlinks();
            hyperlinks.Append(new Hyperlink { Reference = "A2", Id = AddHyperlinkRelationship(worksheetPart, "https://www.example.com") });
            hyperlinks.Append(new Hyperlink { Reference = "A3", Id = AddHyperlinkRelationship(worksheetPart, "https://www.google.com") });
            worksheetPart.Worksheet.Append(hyperlinks);

            // Instruction text
            var row5 = new Row { RowIndex = 5 };
            row5.Append(new Cell
            {
                CellReference = "A5",
                DataType = CellValues.String,
                CellValue = new CellValue("Add hyperlinks to Title column. URLs will be extracted automatically."),
                StyleIndex = 4 // Gray text
            });
            sheetData.Append(row5);

            // Column widths
            var columns = new Columns();
            columns.Append(new Column { Min = 1, Max = 1, Width = 30, CustomWidth = true });
            columns.Append(new Column { Min = 2, Max = 2, Width = 50, CustomWidth = true });
            worksheetPart.Worksheet.InsertBefore(columns, sheetData);

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Creates a sample template file for link merging.
    /// </summary>
    /// <returns>Byte array containing the template Excel file</returns>
    public byte[] CreateMergeTemplate()
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            var sheet = new Sheet()
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Data"
            };
            sheets.Append(sheet);

            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()!;

            // Create stylesheet
            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = CreateStylesheet();

            // Header row with green background
            var headerRow = new Row { RowIndex = 1 };
            headerRow.Append(new Cell
            {
                CellReference = "A1",
                DataType = CellValues.String,
                CellValue = new CellValue("Title"),
                StyleIndex = 5 // Header with green background
            });
            headerRow.Append(new Cell
            {
                CellReference = "B1",
                DataType = CellValues.String,
                CellValue = new CellValue("URL"),
                StyleIndex = 5
            });
            sheetData.Append(headerRow);

            // Sample data
            var samples = new[] {
                ("Google", "https://www.google.com"),
                ("GitHub", "https://www.github.com"),
                ("YouTube", "https://www.youtube.com")
            };

            uint rowIndex = 2;
            foreach (var (title, url) in samples)
            {
                var row = new Row { RowIndex = rowIndex };
                row.Append(new Cell
                {
                    CellReference = $"A{rowIndex}",
                    DataType = CellValues.String,
                    CellValue = new CellValue(title)
                });
                row.Append(new Cell
                {
                    CellReference = $"B{rowIndex}",
                    DataType = CellValues.String,
                    CellValue = new CellValue(url)
                });
                sheetData.Append(row);
                rowIndex++;
            }

            // Instruction text
            var instructionRow = new Row { RowIndex = 6 };
            instructionRow.Append(new Cell
            {
                CellReference = "A6",
                DataType = CellValues.String,
                CellValue = new CellValue("Paste your Title and URL data here. Hyperlinks will be created automatically."),
                StyleIndex = 4 // Gray text
            });
            sheetData.Append(instructionRow);

            // Column widths
            var columns = new Columns();
            columns.Append(new Column { Min = 1, Max = 1, Width = 30, CustomWidth = true });
            columns.Append(new Column { Min = 2, Max = 2, Width = 50, CustomWidth = true });
            worksheetPart.Worksheet.InsertBefore(columns, sheetData);

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Result of link merging operation.
    /// </summary>
    public class MergeResult
    {
        public int TotalRows { get; set; }
        public int LinksCreated { get; set; }
        public List<LinkInfo> Links { get; set; } = new();
        public byte[]? OutputFile { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Merges Title and URL columns into clickable hyperlinks asynchronously.
    /// </summary>
    /// <param name="fileStream">The Excel file stream to process</param>
    /// <returns>Merge result containing created links and output file</returns>
    public async Task<MergeResult> MergeFromFileAsync(Stream fileStream)
    {
        return await Task.Run(() => MergeFromFile(fileStream));
    }

    /// <summary>
    /// Merges Title and URL columns into clickable hyperlinks.
    /// </summary>
    /// <param name="fileStream">The Excel file stream to process</param>
    /// <returns>Merge result containing created links and output file</returns>
    private MergeResult MergeFromFile(Stream fileStream)
    {
        var result = new MergeResult();

        try
        {
            // Validate file before processing
            ValidateExcelFile(fileStream);

            _logger.LogInformation("Starting link merge operation");

            using var document = SpreadsheetDocument.Open(fileStream, false);
            var workbookPart = document.WorkbookPart!;
            var worksheetPart = workbookPart.WorksheetParts.First();
            var worksheet = worksheetPart.Worksheet;
            var sheetData = worksheet.GetFirstChild<SheetData>()!;

            // Find header row
            int? headerRowIndex = null;
            int? titleColumnIndex = null;
            int? urlColumnIndex = null;

            foreach (var row in sheetData.Elements<Row>().Take(MaxHeaderSearchRows))
            {
                foreach (var cell in row.Elements<Cell>())
                {
                    var value = GetCellValue(cell, workbookPart).ToLower();
                    if (value == "title")
                    {
                        headerRowIndex = (int)row.RowIndex!.Value;
                        titleColumnIndex = GetColumnIndex(cell.CellReference!.Value!);
                    }
                    else if (value == "url")
                    {
                        urlColumnIndex = GetColumnIndex(cell.CellReference!.Value!);
                    }
                }
                if (titleColumnIndex != null && urlColumnIndex != null) break;
            }

            if (titleColumnIndex == null || urlColumnIndex == null)
            {
                _logger.LogWarning("Required columns not found. Title column: {TitleFound}, URL column: {UrlFound}",
                    titleColumnIndex != null, urlColumnIndex != null);

                if (titleColumnIndex == null && urlColumnIndex == null)
                    throw new ExcelProcessingException("Could not find 'Title' and 'URL' columns in the spreadsheet.");
                else if (titleColumnIndex == null)
                    throw new InvalidColumnException("Title");
                else
                    throw new InvalidColumnException("URL");
            }

            _logger.LogDebug("Found columns - Title: {TitleColumn}, URL: {UrlColumn}, Header row: {HeaderRow}",
                titleColumnIndex, urlColumnIndex, headerRowIndex);

            // Create new workbook
            var outputStream = new MemoryStream();
            using (var newDocument = SpreadsheetDocument.Create(outputStream, SpreadsheetDocumentType.Workbook))
            {
                var newWorkbookPart = newDocument.AddWorkbookPart();
                newWorkbookPart.Workbook = new Workbook();

                var newWorksheetPart = newWorkbookPart.AddNewPart<WorksheetPart>();
                newWorksheetPart.Worksheet = new Worksheet(new SheetData());

                var sheets = newWorkbookPart.Workbook.AppendChild(new Sheets());
                var sheet = new Sheet()
                {
                    Id = newWorkbookPart.GetIdOfPart(newWorksheetPart),
                    SheetId = 1,
                    Name = "Merged Links"
                };
                sheets.Append(sheet);

                var newSheetData = newWorksheetPart.Worksheet.GetFirstChild<SheetData>()!;

                // Create stylesheet
                var stylesPart = newWorkbookPart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = CreateStylesheet();

                // Header row
                var headerRow = new Row { RowIndex = 1 };
                headerRow.Append(new Cell
                {
                    CellReference = "A1",
                    DataType = CellValues.String,
                    CellValue = new CellValue("Title"),
                    StyleIndex = 5 // Green background
                });
                headerRow.Append(new Cell
                {
                    CellReference = "B1",
                    DataType = CellValues.String,
                    CellValue = new CellValue("URL"),
                    StyleIndex = 5
                });
                newSheetData.Append(headerRow);

                // Process data rows
                uint newRowIndex = 2;
                var hyperlinks = new Hyperlinks();
                var rows = sheetData.Elements<Row>().Where(r => r.RowIndex > headerRowIndex).ToList();

                foreach (var row in rows)
                {
                    var titleCell = row.Elements<Cell>().FirstOrDefault(c => GetColumnIndex(c.CellReference!.Value!) == titleColumnIndex);
                    var urlCell = row.Elements<Cell>().FirstOrDefault(c => GetColumnIndex(c.CellReference!.Value!) == urlColumnIndex);

                    var title = titleCell != null ? GetCellValue(titleCell, workbookPart).Trim() : "";
                    var url = urlCell != null ? GetCellValue(urlCell, workbookPart).Trim() : "";

                    if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(url))
                        continue;

                    var newRow = new Row { RowIndex = newRowIndex };
                    var newTitleCell = new Cell
                    {
                        CellReference = $"A{newRowIndex}",
                        DataType = CellValues.String,
                        CellValue = new CellValue(title)
                    };

                    var sanitizedUrl = SanitizeUrl(url);
                    if (!string.IsNullOrEmpty(sanitizedUrl))
                    {
                        try
                        {
                            var relationshipId = AddHyperlinkRelationship(newWorksheetPart, sanitizedUrl);
                            hyperlinks.Append(new Hyperlink { Reference = $"A{newRowIndex}", Id = relationshipId });
                            newTitleCell.StyleIndex = 2; // Hyperlink style
                            result.LinksCreated++;
                        }
                        catch
                        {
                            // Invalid URL - skip hyperlink
                        }
                    }

                    newRow.Append(newTitleCell);
                    newRow.Append(new Cell
                    {
                        CellReference = $"B{newRowIndex}",
                        DataType = CellValues.String,
                        CellValue = new CellValue(url)
                    });

                    newSheetData.Append(newRow);

                    result.Links.Add(new LinkInfo
                    {
                        Row = (int)newRowIndex,
                        Title = title,
                        Url = url
                    });

                    newRowIndex++;
                }

                result.TotalRows = (int)(newRowIndex - 2);

                // Add hyperlinks if any
                if (hyperlinks.ChildElements.Count > 0)
                {
                    newWorksheetPart.Worksheet.Append(hyperlinks);
                }

                // Column widths
                var columns = new Columns();
                columns.Append(new Column { Min = 1, Max = 1, Width = 40, CustomWidth = true });
                columns.Append(new Column { Min = 2, Max = 2, Width = 60, CustomWidth = true });
                newWorksheetPart.Worksheet.InsertBefore(columns, newSheetData);

                newWorkbookPart.Workbook.Save();
            }

            result.OutputFile = outputStream.ToArray();

            _logger.LogInformation("Link merge completed successfully. Total rows: {TotalRows}, Links created: {LinksCreated}",
                result.TotalRows, result.LinksCreated);
        }
        catch (InvalidFileFormatException ex)
        {
            _logger.LogError(ex, "Invalid file format during link merge");
            result.ErrorMessage = ex.Message;
        }
        catch (InvalidColumnException ex)
        {
            _logger.LogError(ex, "Column not found during link merge");
            result.ErrorMessage = ex.Message;
        }
        catch (ExcelProcessingException ex)
        {
            _logger.LogError(ex, "Excel processing error during link merge");
            result.ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during link merge");
            result.ErrorMessage = $"Error processing file: {ex.Message}";
        }

        return result;
    }

    private static Stylesheet CreateStylesheet()
    {
        var stylesheet = new Stylesheet();

        // Fonts
        var fonts = new Fonts();
        fonts.Append(new Font()); // 0 - Default
        fonts.Append(new Font(new Bold())); // 1 - Bold
        fonts.Append(new Font( // 2 - Blue hyperlink
            new Color { Theme = 10 },
            new Underline()
        ));
        fonts.Append(new Font(new Color { Rgb = "FF808080" })); // 3 - Gray
        fonts.Count = (uint)fonts.ChildElements.Count;

        // Fills
        var fills = new Fills();
        fills.Append(new Fill(new PatternFill { PatternType = PatternValues.None })); // 0 - Default
        fills.Append(new Fill(new PatternFill { PatternType = PatternValues.Gray125 })); // 1 - Required
        fills.Append(new Fill(new PatternFill( // 2 - Light Blue
            new ForegroundColor { Rgb = "FFADD8E6" }
        ) { PatternType = PatternValues.Solid }));
        fills.Append(new Fill(new PatternFill( // 3 - Light Green
            new ForegroundColor { Rgb = "FF90EE90" }
        ) { PatternType = PatternValues.Solid }));
        fills.Count = (uint)fills.ChildElements.Count;

        // Borders
        var borders = new Borders();
        borders.Append(new Border()); // 0 - Default
        borders.Count = (uint)borders.ChildElements.Count;

        // Cell formats
        var cellFormats = new CellFormats();
        cellFormats.Append(new CellFormat()); // 0 - Default
        cellFormats.Append(new CellFormat { FontId = 1, FillId = 0, BorderId = 0, ApplyFont = true }); // 1 - Bold
        cellFormats.Append(new CellFormat { FontId = 2, FillId = 0, BorderId = 0, ApplyFont = true }); // 2 - Hyperlink (blue underline)
        cellFormats.Append(new CellFormat { FontId = 1, FillId = 2, BorderId = 0, ApplyFont = true, ApplyFill = true }); // 3 - Header blue
        cellFormats.Append(new CellFormat { FontId = 3, FillId = 0, BorderId = 0, ApplyFont = true }); // 4 - Gray text
        cellFormats.Append(new CellFormat { FontId = 1, FillId = 3, BorderId = 0, ApplyFont = true, ApplyFill = true }); // 5 - Header green
        cellFormats.Count = (uint)cellFormats.ChildElements.Count;

        stylesheet.Fonts = fonts;
        stylesheet.Fills = fills;
        stylesheet.Borders = borders;
        stylesheet.CellFormats = cellFormats;

        return stylesheet;
    }

    private static string GetCellValue(Cell cell, WorkbookPart workbookPart)
    {
        if (cell.CellValue == null)
            return "";

        var value = cell.CellValue.Text;

        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
        {
            var stringTable = workbookPart.SharedStringTablePart!.SharedStringTable;
            return stringTable.ElementAt(int.Parse(value)).InnerText;
        }

        return value;
    }

    private static string? GetHyperlink(WorksheetPart worksheetPart, string cellReference)
    {
        var hyperlinks = worksheetPart.Worksheet.GetFirstChild<Hyperlinks>();
        if (hyperlinks == null)
            return null;

        var hyperlink = hyperlinks.Elements<Hyperlink>().FirstOrDefault(h => h.Reference == cellReference);
        if (hyperlink?.Id == null)
            return null;

        var hyperlinkRelationship = worksheetPart.HyperlinkRelationships.FirstOrDefault(r => r.Id == hyperlink.Id);
        return hyperlinkRelationship?.Uri?.ToString();
    }

    private static string AddHyperlinkRelationship(WorksheetPart worksheetPart, string url)
    {
        var relationship = worksheetPart.AddHyperlinkRelationship(new Uri(url, UriKind.Absolute), true);
        return relationship.Id;
    }

    private static int GetColumnIndex(string cellReference)
    {
        var columnName = new string(cellReference.Where(char.IsLetter).ToArray());
        int columnIndex = 0;
        for (int i = 0; i < columnName.Length; i++)
        {
            columnIndex *= 26;
            columnIndex += (columnName[i] - 'A' + 1);
        }
        return columnIndex;
    }

    private static string GetCellReference(uint rowIndex, int columnIndex)
    {
        string columnName = "";
        while (columnIndex > 0)
        {
            int modulo = (columnIndex - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            columnIndex = (columnIndex - modulo) / 26;
        }
        return columnName + rowIndex;
    }

    /// <summary>
    /// Sanitizes and validates a URL for use in Excel hyperlinks.
    /// </summary>
    /// <param name="url">The URL to sanitize</param>
    /// <returns>Sanitized URL or null if invalid</returns>
    private static string? SanitizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        url = url.Trim();

        // Excel hyperlink limit
        if (url.Length > MaxUrlLength)
            return null;

        // Add protocol if missing
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        // Validate URL format
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        // Only allow http, https, mailto schemes
        if (uri.Scheme != "http" && uri.Scheme != "https" && uri.Scheme != "mailto")
            return null;

        return uri.AbsoluteUri;
    }
}
