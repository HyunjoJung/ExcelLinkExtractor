using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ExcelLinkExtractorWeb.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExcelLinkExtractorWeb.Services;

/// <summary>
/// Service for extracting and merging hyperlinks in Excel spreadsheets.
/// Provides functionality to extract URLs from cells and merge Title + URL columns into clickable hyperlinks.
/// </summary>
public partial class LinkExtractorService : ILinkExtractorService
{
    private readonly ILogger<LinkExtractorService> _logger;
    private readonly ExcelProcessingOptions _options;

    // Excel file signatures (magic bytes)
    private static readonly byte[] XlsxSignature = { 0x50, 0x4B, 0x03, 0x04 }; // PK.. (ZIP format)
    private static readonly byte[] XlsSignature = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }; // OLE2 format

    // Cached stylesheet is no longer used to ensure thread-safety. A new one is created on each call.

    public LinkExtractorService(
        ILogger<LinkExtractorService> logger,
        IOptions<ExcelProcessingOptions> options)
    {
        _logger = logger;
        _options = options.Value;
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

            foreach (var row in sheetData.Elements<Row>().Take(_options.MaxHeaderSearchRows))
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
                throw new InvalidColumnException(linkColumnName, _options.MaxHeaderSearchRows);
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
                stylesPart.Stylesheet = GetStylesheet();

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
            result.ErrorMessage = ex.GetFullMessage();
        }
        catch (InvalidColumnException ex)
        {
            _logger.LogError(ex, "Column not found during link extraction");
            result.ErrorMessage = ex.GetFullMessage();
        }
        catch (ExcelProcessingException ex)
        {
            _logger.LogError(ex, "Excel processing error during link extraction");
            result.ErrorMessage = ex.GetFullMessage();
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
            stylesPart.Stylesheet = GetStylesheet();

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
            stylesPart.Stylesheet = GetStylesheet();

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

            foreach (var row in sheetData.Elements<Row>().Take(_options.MaxHeaderSearchRows))
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
                {
                    throw new ExcelProcessingException(
                        message: $"Could not find 'Title' and 'URL' columns in the first {_options.MaxHeaderSearchRows} rows.",
                        recoverySuggestion: "💡 Tip: Make sure your Excel file has both 'Title' and 'URL' column headers (case-sensitive) in the first few rows. Download the sample template to see the expected format."
                    );
                }
                else if (titleColumnIndex == null)
                    throw new InvalidColumnException("Title", _options.MaxHeaderSearchRows);
                else
                    throw new InvalidColumnException("URL", _options.MaxHeaderSearchRows);
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
                stylesPart.Stylesheet = GetStylesheet();

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
            result.ErrorMessage = ex.GetFullMessage();
        }
        catch (InvalidColumnException ex)
        {
            _logger.LogError(ex, "Column not found during link merge");
            result.ErrorMessage = ex.GetFullMessage();
        }
        catch (ExcelProcessingException ex)
        {
            _logger.LogError(ex, "Excel processing error during link merge");
            result.ErrorMessage = ex.GetFullMessage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during link merge");
            result.ErrorMessage = $"Error processing file: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Creates a new stylesheet for each call to ensure thread-safety in parallel operations.
    /// </summary>
    /// <returns>A new Stylesheet instance with the same formatting</returns>
    private static Stylesheet GetStylesheet()
    {
        // Create a new stylesheet for each call to ensure thread safety.
        return CreateStylesheet();
    }

    /// <summary>
    /// Creates the standard stylesheet with fonts, fills, and cell formats.
    /// </summary>
    /// <returns>Configured Stylesheet instance</returns>
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

}
