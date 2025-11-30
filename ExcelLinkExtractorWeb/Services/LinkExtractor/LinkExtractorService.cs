using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ExcelLinkExtractorWeb.Configuration;
using ExcelLinkExtractorWeb.Services.LinkExtractor.Models;
using ExcelLinkExtractorWeb.Services.Metrics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExcelLinkExtractorWeb.Services.LinkExtractor;

/// <summary>
/// Service for extracting and merging hyperlinks in Excel spreadsheets.
/// </summary>
public partial class LinkExtractorService : ILinkExtractorService
{
    private readonly ILogger<LinkExtractorService> _logger;
    private readonly ExcelProcessingOptions _options;
    private readonly IMemoryCache _cache;
    private readonly IMetricsService _metrics;

    // Excel file signatures (magic bytes)
    private static readonly byte[] XlsxSignature = { 0x50, 0x4B, 0x03, 0x04 }; // PK.. (ZIP format)
    private static readonly byte[] XlsSignature = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }; // OLE2 format

    public LinkExtractorService(
        ILogger<LinkExtractorService> logger,
        IOptions<ExcelProcessingOptions> options,
        IMemoryCache cache,
        IMetricsService metrics)
    {
        _logger = logger;
        _options = options.Value;
        _cache = cache;
        _metrics = metrics;
    }

    public async Task<ExtractionResult> ExtractLinksAsync(Stream fileStream, string linkColumnName = "Title")
    {
        return await Task.Run(() => ExtractLinks(fileStream, linkColumnName));
    }

    private ExtractionResult ExtractLinks(Stream fileStream, string linkColumnName)
    {
        var result = new ExtractionResult();
        var context = new ProcessContext { InputBytes = fileStream.Length };
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            ValidateExcelFile(fileStream);

            _logger.LogInformation("Starting link extraction for column '{ColumnName}'", linkColumnName);

            using var document = SpreadsheetDocument.Open(fileStream, false);
            var workbookPart = document.WorkbookPart!;
            var worksheetPart = workbookPart.WorksheetParts.First();
            var worksheet = worksheetPart.Worksheet;
            var sheetData = worksheet.GetFirstChild<SheetData>()!;

            int? headerRowIndex = null;
            int? targetColumnIndex = null;

            foreach (var row in sheetData.Elements<Row>().Take(_options.MaxHeaderSearchRows))
            {
                foreach (var cell in row.Elements<Cell>())
                {
                    if (cell.CellReference == null || string.IsNullOrEmpty(cell.CellReference.Value))
                        continue;

                    var cellValue = GetCellValue(cell, workbookPart);
                    if (cellValue.Equals(linkColumnName, StringComparison.OrdinalIgnoreCase))
                    {
                        var rowIndexValue = row.RowIndex?.Value ?? 1;
                        headerRowIndex = (int)(cell.CellReference.Value.Any(char.IsDigit) ? rowIndexValue : 1);
                        targetColumnIndex = GetColumnIndex(cell.CellReference.Value);
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

                var stylesPart = newWorkbookPart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = GetStylesheet();

                var originalHeaderRow = sheetData.Elements<Row>()
                    .FirstOrDefault(r => r.RowIndex != null && r.RowIndex.Value == headerRowIndex);
                if (originalHeaderRow == null)
                {
                    throw new InvalidColumnException(linkColumnName, _options.MaxHeaderSearchRows);
                }

                var headerRow = new Row { RowIndex = 1 };
                foreach (var cell in originalHeaderRow.Elements<Cell>())
                {
                    if (cell.CellReference == null || string.IsNullOrEmpty(cell.CellReference.Value))
                        continue;

                    var newCell = new Cell
                    {
                        CellReference = GetCellReference(1, GetColumnIndex(cell.CellReference.Value)),
                        DataType = CellValues.String,
                        CellValue = new CellValue(GetCellValue(cell, workbookPart)),
                        StyleIndex = 1
                    };
                    headerRow.Append(newCell);
                }
                newSheetData.Append(headerRow);

                uint newRowIndex = 2;
                var rows = sheetData.Elements<Row>()
                    .Where(r => r.RowIndex != null && r.RowIndex.Value > headerRowIndex)
                    .ToList();

                foreach (var row in rows)
                {
                    var newRow = new Row { RowIndex = newRowIndex };
                    bool hasData = false;

                    foreach (var cell in row.Elements<Cell>())
                    {
                        if (cell.CellReference == null || string.IsNullOrEmpty(cell.CellReference.Value))
                        {
                            continue;
                        }

                        var colIndex = GetColumnIndex(cell.CellReference.Value);
                        var cellValue = GetCellValue(cell, workbookPart);

                        if (!string.IsNullOrWhiteSpace(cellValue))
                            hasData = true;

                        var newCell = new Cell
                        {
                            CellReference = GetCellReference(newRowIndex, colIndex),
                            DataType = CellValues.String,
                            CellValue = new CellValue(cellValue)
                        };

                        var hyperlink = GetHyperlink(worksheetPart, cell.CellReference.Value);
                        if (hyperlink != null)
                        {
                            newCell.StyleIndex = 2;

                            if (colIndex == targetColumnIndex)
                            {
                                var rowNumber = row.RowIndex?.Value != null ? (int)row.RowIndex.Value : (int)newRowIndex;
                                result.Links.Add(new LinkInfo
                                {
                                    Row = rowNumber,
                                    Title = cellValue,
                                    Url = hyperlink
                                });
                            }
                        }

                        newRow.Append(newCell);
                    }

                    if (hasData)
                    {
                        newSheetData.Append(newRow);
                        newRowIndex++;
                    }
                }

                result.TotalRows = (int)(newRowIndex - 2);
                result.LinksFound = result.Links.Count;
                context.Rows = result.TotalRows;

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
            result.ErrorMessage = $"E001: {ex.GetFullMessage()}";
        }
        catch (InvalidColumnException ex)
        {
            _logger.LogError(ex, "Column not found during link extraction");
            result.ErrorMessage = $"E002: {ex.GetFullMessage()}";
        }
        catch (OutOfMemoryException)
        {
            result.ErrorMessage = "E010: File is too large to process. Please reduce the file size and try again.";
        }
        catch (IOException ex)
        {
            result.ErrorMessage = $"E011: Could not read the file. Check if it is corrupted or locked. Details: {ex.Message}";
        }
        catch (UnauthorizedAccessException)
        {
            result.ErrorMessage = "E012: Permission denied while reading the file. Please check file permissions.";
        }
        catch (ExcelProcessingException ex)
        {
            _logger.LogError(ex, "Excel processing error during link extraction");
            result.ErrorMessage = $"E003: {ex.GetFullMessage()}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during link extraction");
            result.ErrorMessage = $"E999: Error processing file: {ex.Message}";
        }
        finally
        {
            sw.Stop();
            context.Duration = sw.Elapsed;
            _metrics.RecordFileProcessed(context.InputBytes, context.Rows, context.Duration);
        }

        return result;
    }

    public async Task<MergeResult> MergeFromFileAsync(Stream fileStream)
    {
        return await Task.Run(() => MergeFromFile(fileStream));
    }

    private MergeResult MergeFromFile(Stream fileStream)
    {
        var result = new MergeResult();
        var context = new ProcessContext { InputBytes = fileStream.Length };
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            ValidateExcelFile(fileStream);

            using var document = SpreadsheetDocument.Open(fileStream, false);
            var workbookPart = document.WorkbookPart!;
            var worksheetPart = workbookPart.WorksheetParts.First();
            var worksheet = worksheetPart.Worksheet;
            var sheetData = worksheet.GetFirstChild<SheetData>()!;

            var titleColumnIndex = FindColumnIndex(sheetData, workbookPart, "Title");
            var urlColumnIndex = FindColumnIndex(sheetData, workbookPart, "URL");

            if (titleColumnIndex == null || urlColumnIndex == null)
            {
                _logger.LogWarning("Required columns not found for merge. Title: {Title}, URL: {Url}", titleColumnIndex, urlColumnIndex);
                if (titleColumnIndex == null)
                    throw new InvalidColumnException("Title", _options.MaxHeaderSearchRows);
                else
                    throw new InvalidColumnException("URL", _options.MaxHeaderSearchRows);
            }

            _logger.LogDebug("Found columns - Title: {TitleColumn}, URL: {UrlColumn}, Header row: {HeaderRow}",
                titleColumnIndex, urlColumnIndex, FindHeaderRow(sheetData, workbookPart));

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

                var stylesPart = newWorkbookPart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = GetStylesheet();

                var headerRow = new Row { RowIndex = 1 };
                headerRow.Append(new Cell
                {
                    CellReference = "A1",
                    DataType = CellValues.String,
                    CellValue = new CellValue("Title"),
                    StyleIndex = 5
                });
                headerRow.Append(new Cell
                {
                    CellReference = "B1",
                    DataType = CellValues.String,
                    CellValue = new CellValue("URL"),
                    StyleIndex = 5
                });
                newSheetData.Append(headerRow);

                uint newRowIndex = 2;
                var hyperlinks = new Hyperlinks();
                var rows = sheetData.Elements<Row>()
                    .Where(r => r.RowIndex != null && r.RowIndex.Value > 1)
                    .ToList();

                foreach (var row in rows)
                {
                    var titleCell = row.Elements<Cell>().FirstOrDefault(c => c.CellReference != null && !string.IsNullOrEmpty(c.CellReference.Value) && GetColumnIndex(c.CellReference.Value) == titleColumnIndex);
                    var urlCell = row.Elements<Cell>().FirstOrDefault(c => c.CellReference != null && !string.IsNullOrEmpty(c.CellReference.Value) && GetColumnIndex(c.CellReference.Value) == urlColumnIndex);

                    var title = titleCell != null ? GetCellValue(titleCell, workbookPart).Trim() : "";
                    var url = urlCell != null ? GetCellValue(urlCell, workbookPart).Trim() : "";

                    if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(url))
                        continue;

                    var sanitizedUrl = SanitizeUrl(url);
                    if (sanitizedUrl == null)
                    {
                        result.ErrorMessage = "E002: Invalid URL format.";
                        continue;
                    }

                    var newRow = new Row { RowIndex = newRowIndex };
                    var newTitleCell = new Cell
                    {
                        CellReference = $"A{newRowIndex}",
                        DataType = CellValues.String,
                        CellValue = new CellValue(title)
                    };
                    var newUrlCell = new Cell
                    {
                        CellReference = $"B{newRowIndex}",
                        DataType = CellValues.String,
                        CellValue = new CellValue(sanitizedUrl),
                        StyleIndex = 2
                    };

                    var hyperlinkId = AddHyperlinkRelationship(newWorksheetPart, sanitizedUrl);
                    hyperlinks.Append(new Hyperlink
                    {
                        Reference = $"B{newRowIndex}",
                        Id = hyperlinkId
                    });

                    newRow.Append(newTitleCell);
                    newRow.Append(newUrlCell);
                    newSheetData.Append(newRow);

                    result.LinksCreated++;
                    result.Links.Add(new MergeLinkInfo
                    {
                        Row = (int)newRowIndex,
                        Title = title,
                        Url = sanitizedUrl
                    });

                    newRowIndex++;
                }

                result.TotalRows = (int)(newRowIndex - 2);
                context.Rows = result.TotalRows;

                if (hyperlinks.ChildElements.Count > 0)
                {
                    newWorksheetPart.Worksheet.Append(hyperlinks);
                }

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
            result.ErrorMessage = $"E001: {ex.GetFullMessage()}";
        }
        catch (InvalidColumnException ex)
        {
            _logger.LogError(ex, "Column not found during link merge");
            result.ErrorMessage = $"E002: {ex.GetFullMessage()}";
        }
        catch (OutOfMemoryException)
        {
            result.ErrorMessage = "E010: File is too large to process. Please reduce the file size and try again.";
        }
        catch (IOException ex)
        {
            result.ErrorMessage = $"E011: Could not read the file. Check if it is corrupted or locked. Details: {ex.Message}";
        }
        catch (UnauthorizedAccessException)
        {
            result.ErrorMessage = "E012: Permission denied while reading the file. Please check file permissions.";
        }
        catch (ExcelProcessingException ex)
        {
            _logger.LogError(ex, "Excel processing error during link merge");
            result.ErrorMessage = $"E003: {ex.GetFullMessage()}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during link merge");
            result.ErrorMessage = $"E999: Error processing file: {ex.Message}";
        }
        finally
        {
            sw.Stop();
            context.Duration = sw.Elapsed;
            _metrics.RecordFileProcessed(context.InputBytes, context.Rows, context.Duration);
        }

        return result;
    }

    private int? FindColumnIndex(SheetData sheetData, WorkbookPart workbookPart, string columnName)
    {
        foreach (var row in sheetData.Elements<Row>().Take(_options.MaxHeaderSearchRows))
        {
            foreach (var cell in row.Elements<Cell>())
            {
                if (cell.CellReference == null || string.IsNullOrEmpty(cell.CellReference.Value))
                    continue;

                var cellValue = GetCellValue(cell, workbookPart);
                if (cellValue.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return GetColumnIndex(cell.CellReference.Value);
                }
            }
        }
        return null;
    }

    private int? FindHeaderRow(SheetData sheetData, WorkbookPart workbookPart)
    {
        foreach (var row in sheetData.Elements<Row>().Take(_options.MaxHeaderSearchRows))
        {
            foreach (var cell in row.Elements<Cell>())
            {
                if (cell.CellReference == null || string.IsNullOrEmpty(cell.CellReference.Value))
                    continue;

                var cellValue = GetCellValue(cell, workbookPart);
                if (!string.IsNullOrEmpty(cellValue))
                {
                    return row.RowIndex?.Value != null ? (int)row.RowIndex.Value : 1;
                }
            }
        }
        return null;
    }

    public byte[] CreateTemplate()
    {
        if (_cache.TryGetValue("template:extract", out byte[]? cachedTemplate) && cachedTemplate != null)
        {
            return cachedTemplate;
        }

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

            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = GetStylesheet();

            var headerRow = new Row { RowIndex = 1 };
            headerRow.Append(new Cell
            {
                CellReference = "A1",
                DataType = CellValues.String,
                CellValue = new CellValue("Title"),
                StyleIndex = 3
            });
            headerRow.Append(new Cell
            {
                CellReference = "B1",
                DataType = CellValues.String,
                CellValue = new CellValue("URL"),
                StyleIndex = 3
            });
            sheetData.Append(headerRow);

            var row2 = new Row { RowIndex = 2 };
            row2.Append(new Cell
            {
                CellReference = "A2",
                DataType = CellValues.String,
                CellValue = new CellValue("Example Link 1"),
                StyleIndex = 2
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

            var hyperlinks = new Hyperlinks();
            hyperlinks.Append(new Hyperlink { Reference = "A2", Id = AddHyperlinkRelationship(worksheetPart, "https://www.example.com") });
            hyperlinks.Append(new Hyperlink { Reference = "A3", Id = AddHyperlinkRelationship(worksheetPart, "https://www.google.com") });
            worksheetPart.Worksheet.Append(hyperlinks);

            var row5 = new Row { RowIndex = 5 };
            row5.Append(new Cell
            {
                CellReference = "A5",
                DataType = CellValues.String,
                CellValue = new CellValue("Add hyperlinks to Title column. URLs will be extracted automatically."),
                StyleIndex = 4
            });
            sheetData.Append(row5);

            var columns = new Columns();
            columns.Append(new Column { Min = 1, Max = 1, Width = 30, CustomWidth = true });
            columns.Append(new Column { Min = 2, Max = 2, Width = 50, CustomWidth = true });
            worksheetPart.Worksheet.InsertBefore(columns, sheetData);

            workbookPart.Workbook.Save();
        }

        var bytes = stream.ToArray();
        _cache.Set("template:extract", bytes, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(2)
        });

        return bytes;
    }

    public byte[] CreateMergeTemplate()
    {
        if (_cache.TryGetValue("template:merge", out byte[]? cachedTemplate) && cachedTemplate != null)
        {
            return cachedTemplate;
        }

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

            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = GetStylesheet();

            var headerRow = new Row { RowIndex = 1 };
            headerRow.Append(new Cell
            {
                CellReference = "A1",
                DataType = CellValues.String,
                CellValue = new CellValue("Title"),
                StyleIndex = 5
            });
            headerRow.Append(new Cell
            {
                CellReference = "B1",
                DataType = CellValues.String,
                CellValue = new CellValue("URL"),
                StyleIndex = 5
            });
            sheetData.Append(headerRow);

            var samples = new[] {
                ("Google", "https://www.google.com"),
                ("GitHub", "https://github.com"),
                ("Stack Overflow", "https://stackoverflow.com")
            };

            uint currentRow = 2;
            foreach (var (title, url) in samples)
            {
                var row = new Row { RowIndex = currentRow };
                row.Append(new Cell
                {
                    CellReference = $"A{currentRow}",
                    DataType = CellValues.String,
                    CellValue = new CellValue(title)
                });
                row.Append(new Cell
                {
                    CellReference = $"B{currentRow}",
                    DataType = CellValues.String,
                    CellValue = new CellValue(url)
                });
                sheetData.Append(row);
                currentRow++;
            }

            var infoRow = new Row { RowIndex = currentRow + 1 };
            infoRow.Append(new Cell
            {
                CellReference = $"A{currentRow + 1}",
                DataType = CellValues.String,
                CellValue = new CellValue("Add your Title and URL values. URLs will be converted to hyperlinks."),
                StyleIndex = 4
            });
            sheetData.Append(infoRow);

            var columns = new Columns();
            columns.Append(new Column { Min = 1, Max = 1, Width = 30, CustomWidth = true });
            columns.Append(new Column { Min = 2, Max = 2, Width = 50, CustomWidth = true });
            worksheetPart.Worksheet.InsertBefore(columns, sheetData);

            workbookPart.Workbook.Save();
        }

        var bytes = stream.ToArray();
        _cache.Set("template:merge", bytes, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(2)
        });

        return bytes;
    }

    private static Stylesheet GetStylesheet()
    {
        return CreateStylesheet();
    }

    private static Stylesheet CreateStylesheet()
    {
        var stylesheet = new Stylesheet();

        var fonts = new Fonts();
        fonts.Append(new Font());
        fonts.Append(new Font(new Bold()));
        fonts.Append(new Font(new Bold(), new Color { Rgb = new HexBinaryValue { Value = "FF0000FF" } }, new Underline()));
        fonts.Append(new Font(new Color { Rgb = new HexBinaryValue { Value = "FF888888" } }));
        fonts.Count = (uint)fonts.ChildElements.Count;

        var fills = new Fills();
        fills.Append(new Fill(new PatternFill { PatternType = PatternValues.None }));
        fills.Append(new Fill(new PatternFill { PatternType = PatternValues.Gray125 }));
        fills.Append(new Fill(new PatternFill(new ForegroundColor { Rgb = new HexBinaryValue { Value = "FFD9EAF7" } }) { PatternType = PatternValues.Solid }));
        fills.Append(new Fill(new PatternFill(new ForegroundColor { Rgb = new HexBinaryValue { Value = "FFD9F7E8" } }) { PatternType = PatternValues.Solid }));
        fills.Count = (uint)fills.ChildElements.Count;

        var borders = new Borders();
        borders.Append(new Border());
        borders.Count = (uint)borders.ChildElements.Count;

        var cellFormats = new CellFormats();
        cellFormats.Append(new CellFormat());
        cellFormats.Append(new CellFormat { FontId = 1, FillId = 0, BorderId = 0, ApplyFont = true });
        cellFormats.Append(new CellFormat { FontId = 2, FillId = 0, BorderId = 0, ApplyFont = true });
        cellFormats.Append(new CellFormat { FontId = 1, FillId = 2, BorderId = 0, ApplyFont = true, ApplyFill = true });
        cellFormats.Append(new CellFormat { FontId = 3, FillId = 0, BorderId = 0, ApplyFont = true });
        cellFormats.Append(new CellFormat { FontId = 1, FillId = 3, BorderId = 0, ApplyFont = true, ApplyFill = true });
        cellFormats.Count = (uint)cellFormats.ChildElements.Count;

        stylesheet.Fonts = fonts;
        stylesheet.Fills = fills;
        stylesheet.Borders = borders;
        stylesheet.CellFormats = cellFormats;

        return stylesheet;
    }

    private sealed class ProcessContext
    {
        public long InputBytes { get; init; }
        public int Rows { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
