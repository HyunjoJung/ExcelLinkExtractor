using System.Diagnostics;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ExcelLinkExtractorWeb.Services.LinkExtractor.Models;

namespace ExcelLinkExtractorWeb.Services.LinkExtractor;

public partial class LinkExtractorService
{
    public async Task<MergeResult> MergeFromFileAsync(Stream fileStream)
    {
        return await Task.Run(() => MergeFromFile(fileStream));
    }

    private MergeResult MergeFromFile(Stream fileStream)
    {
        var result = new MergeResult();
        var context = new ProcessContext { InputBytes = fileStream.Length };
        var sw = Stopwatch.StartNew();

        try
        {
            ValidateExcelFile(fileStream);

            using var document = SpreadsheetDocument.Open(fileStream, false);
            var workbookPart = document.WorkbookPart!;
            var worksheetPart = workbookPart.WorksheetParts.First();
            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()!;

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
            RecordPrometheusMetrics("merge", result.ErrorMessage == null ? "success" : "failed", context, context.Duration);
        }

        return result;
    }
}
