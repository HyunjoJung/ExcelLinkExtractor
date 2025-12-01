using System.Diagnostics;
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
        var sw = Stopwatch.StartNew();

        try
        {
            ValidateExcelFile(fileStream);

            _logger.LogInformation("Starting link extraction for column '{ColumnName}'", linkColumnName);

            using var document = SpreadsheetDocument.Open(fileStream, false);
            var workbookPart = document.WorkbookPart!;
            var worksheetPart = workbookPart.WorksheetParts.First();
            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()!;
            var hyperlinkMap = BuildHyperlinkMap(worksheetPart);

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
                // Collect links first (no copy of original sheet)
                var extractedLinks = new List<LinkInfo>();
                var rows = sheetData.Elements<Row>()
                    .Where(r => r.RowIndex != null && r.RowIndex.Value > headerRowIndex)
                    .ToList();

                foreach (var row in rows)
                {
                    foreach (var cell in row.Elements<Cell>())
                    {
                        if (cell.CellReference == null || string.IsNullOrEmpty(cell.CellReference.Value))
                        {
                            continue;
                        }

                        var colIndex = GetColumnIndex(cell.CellReference.Value);
                        var cellValue = GetCellValue(cell, workbookPart);
                        var hyperlink = GetHyperlink(worksheetPart, cell.CellReference.Value, hyperlinkMap);

                        if (hyperlink != null && colIndex == targetColumnIndex)
                        {
                            var rowNumber = row.RowIndex?.Value != null ? (int)row.RowIndex.Value : 1;
                            extractedLinks.Add(new LinkInfo
                            {
                                Row = rowNumber,
                                Title = cellValue,
                                Url = hyperlink
                            });
                        }
                    }
                }

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

                var headerRow = new Row { RowIndex = 1 };
                headerRow.Append(new Cell
                {
                    CellReference = "A1",
                    DataType = CellValues.String,
                    CellValue = new CellValue("Row"),
                    StyleIndex = 1
                });
                headerRow.Append(new Cell
                {
                    CellReference = "B1",
                    DataType = CellValues.String,
                    CellValue = new CellValue(linkColumnName),
                    StyleIndex = 1
                });
                headerRow.Append(new Cell
                {
                    CellReference = "C1",
                    DataType = CellValues.String,
                    CellValue = new CellValue("URL"),
                    StyleIndex = 1
                });
                newSheetData.Append(headerRow);

                uint newRowIndex = 2;
                foreach (var link in extractedLinks)
                {
                    var newRow = new Row { RowIndex = newRowIndex };

                    newRow.Append(new Cell
                    {
                        CellReference = $"A{newRowIndex}",
                        DataType = CellValues.Number,
                        CellValue = new CellValue(link.Row.ToString())
                    });

                    newRow.Append(new Cell
                    {
                        CellReference = $"B{newRowIndex}",
                        DataType = CellValues.String,
                        CellValue = new CellValue(link.Title)
                    });

                    newRow.Append(new Cell
                    {
                        CellReference = $"C{newRowIndex}",
                        DataType = CellValues.String,
                        CellValue = new CellValue(link.Url),
                        StyleIndex = 2
                    });

                    newSheetData.Append(newRow);
                    newRowIndex++;
                }

                result.TotalRows = (int)(newRowIndex - 2);
                result.LinksFound = extractedLinks.Count;
                result.Links = extractedLinks;
                context.Rows = result.TotalRows;

                var columns = new Columns();
                columns.Append(new Column { Min = 1, Max = 1, Width = 10, CustomWidth = true });
                columns.Append(new Column { Min = 2, Max = 2, Width = 40, CustomWidth = true });
                columns.Append(new Column { Min = 3, Max = 3, Width = 60, CustomWidth = true });
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
            RecordPrometheusMetrics("extract", result.ErrorMessage == null ? "success" : "failed", context, context.Duration);
        }

        return result;
    }
}
