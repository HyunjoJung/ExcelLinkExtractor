using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ExcelLinkExtractorWeb.E2ETests;

/// <summary>
/// Helper methods for creating test Excel files
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Creates a test Excel file with hyperlinks in memory
    /// </summary>
    public static byte[] CreateTestExcelWithLinks()
    {
        using var memoryStream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(memoryStream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.AppendChild(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1"
            });

            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()!;

            // Add header row
            var headerRow = new Row { RowIndex = 1 };
            headerRow.Append(
                new Cell { CellReference = "A1", CellValue = new CellValue("Title"), DataType = CellValues.String },
                new Cell { CellReference = "B1", CellValue = new CellValue("URL"), DataType = CellValues.String }
            );
            sheetData.Append(headerRow);

            // Add data rows with hyperlinks
            AddRowWithHyperlink(sheetData, 2, "Google", "https://www.google.com");
            AddRowWithHyperlink(sheetData, 3, "GitHub", "https://github.com");
            AddRowWithHyperlink(sheetData, 4, "Stack Overflow", "https://stackoverflow.com");

            worksheetPart.Worksheet.Save();
        }

        return memoryStream.ToArray();
    }

    /// <summary>
    /// Creates a test Excel file for merge testing (Title and URL in separate columns)
    /// </summary>
    public static byte[] CreateTestExcelForMerge()
    {
        using var memoryStream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(memoryStream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.AppendChild(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1"
            });

            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()!;

            // Add header row
            var headerRow = new Row { RowIndex = 1 };
            headerRow.Append(
                new Cell { CellReference = "A1", CellValue = new CellValue("Title"), DataType = CellValues.String },
                new Cell { CellReference = "B1", CellValue = new CellValue("URL"), DataType = CellValues.String }
            );
            sheetData.Append(headerRow);

            // Add data rows (plain text, no hyperlinks)
            AddRowWithText(sheetData, 2, "Google", "https://www.google.com");
            AddRowWithText(sheetData, 3, "GitHub", "https://github.com");
            AddRowWithText(sheetData, 4, "Stack Overflow", "https://stackoverflow.com");

            worksheetPart.Worksheet.Save();
        }

        return memoryStream.ToArray();
    }

    private static void AddRowWithHyperlink(SheetData sheetData, uint rowIndex, string title, string url)
    {
        var row = new Row { RowIndex = rowIndex };

        // Title cell with hyperlink
        var titleCell = new Cell
        {
            CellReference = $"A{rowIndex}",
            CellValue = new CellValue(title),
            DataType = CellValues.String
        };

        row.Append(titleCell);
        sheetData.Append(row);
    }

    private static void AddRowWithText(SheetData sheetData, uint rowIndex, string title, string url)
    {
        var row = new Row { RowIndex = rowIndex };

        row.Append(
            new Cell { CellReference = $"A{rowIndex}", CellValue = new CellValue(title), DataType = CellValues.String },
            new Cell { CellReference = $"B{rowIndex}", CellValue = new CellValue(url), DataType = CellValues.String }
        );

        sheetData.Append(row);
    }
}
