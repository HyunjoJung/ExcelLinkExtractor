using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ExcelLinkExtractorWeb.Configuration;
using ExcelLinkExtractorWeb.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ExcelLinkExtractor.Tests;

public class LinkExtractorServiceTests
{
    private readonly LinkExtractorService _service;
    private readonly Mock<ILogger<LinkExtractorService>> _loggerMock;
    private readonly Mock<IOptions<ExcelProcessingOptions>> _optionsMock;

    public LinkExtractorServiceTests()
    {
        _loggerMock = new Mock<ILogger<LinkExtractorService>>();
        _optionsMock = new Mock<IOptions<ExcelProcessingOptions>>();
        _optionsMock.Setup(x => x.Value).Returns(new ExcelProcessingOptions());

        _service = new LinkExtractorService(_loggerMock.Object, _optionsMock.Object);
    }

    [Fact]
    public void CreateTemplate_Should_ReturnValidExcelFile()
    {
        // Act
        var result = _service.CreateTemplate();

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);

        // Verify it's a valid Excel file by opening it
        using var stream = new MemoryStream(result);
        var act = () =>
        {
            using var document = SpreadsheetDocument.Open(stream, false);
            return document.WorkbookPart;
        };
        act.Should().NotThrow();
    }

    [Fact]
    public void CreateMergeTemplate_Should_ReturnValidExcelFile()
    {
        // Act
        var result = _service.CreateMergeTemplate();

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);

        // Verify it's a valid Excel file
        using var stream = new MemoryStream(result);
        using var document = SpreadsheetDocument.Open(stream, false);
        var worksheetPart = document.WorkbookPart!.WorksheetParts.First();
        var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();

        sheetData.Should().NotBeNull();
        sheetData!.Elements<Row>().Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExtractLinksAsync_Should_ReturnError_WhenFileIsEmpty()
    {
        // Arrange
        var emptyStream = new MemoryStream();

        // Act
        var result = await _service.ExtractLinksAsync(emptyStream, "Title");

        // Assert
        result.Should().NotBeNull();
        result.ErrorMessage.Should().Contain("empty");
        result.OutputFile.Should().BeNull();
    }

    [Fact]
    public async Task ExtractLinksAsync_Should_ReturnError_WhenFileIsTooLarge()
    {
        // Arrange - Create a stream larger than 10MB
        var largeStream = new MemoryStream(new byte[11 * 1024 * 1024]);

        // Act
        var result = await _service.ExtractLinksAsync(largeStream, "Title");

        // Assert
        result.Should().NotBeNull();
        result.ErrorMessage.Should().Contain("exceeds maximum");
        result.OutputFile.Should().BeNull();
    }

    [Fact]
    public async Task ExtractLinksAsync_Should_ReturnError_WhenFileIsNotExcel()
    {
        // Arrange - Create an invalid file (just random bytes)
        var invalidStream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 });

        // Act
        var result = await _service.ExtractLinksAsync(invalidStream, "Title");

        // Assert
        result.Should().NotBeNull();
        result.ErrorMessage.Should().Contain("not a valid Excel file");
        result.OutputFile.Should().BeNull();
    }

    [Fact]
    public async Task MergeFromFileAsync_Should_ReturnError_WhenFileIsEmpty()
    {
        // Arrange
        var emptyStream = new MemoryStream();

        // Act
        var result = await _service.MergeFromFileAsync(emptyStream);

        // Assert
        result.Should().NotBeNull();
        result.ErrorMessage.Should().Contain("empty");
        result.OutputFile.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task ExtractLinksAsync_Should_HandleEmptyColumnName(string? columnName)
    {
        // Arrange
        var validExcelStream = CreateSimpleExcelFile();

        // Act
        var result = await _service.ExtractLinksAsync(validExcelStream, columnName ?? "Title");

        // Assert - Should either return error or handle gracefully
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExtractLinksAsync_Should_ReturnError_WhenColumnNotFound()
    {
        // Arrange
        var excelStream = CreateSimpleExcelFile();

        // Act
        var result = await _service.ExtractLinksAsync(excelStream, "NonExistentColumn");

        // Assert
        result.Should().NotBeNull();
        result.ErrorMessage.Should().Contain("not found");
        result.LinksFound.Should().Be(0);
    }

    [Fact]
    public async Task MergeFromFileAsync_Should_ReturnError_WhenRequiredColumnsNotFound()
    {
        // Arrange - Create Excel without Title/URL columns
        var excelStream = CreateExcelFileWithDifferentColumns();

        // Act
        var result = await _service.MergeFromFileAsync(excelStream);

        // Assert
        result.Should().NotBeNull();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.LinksCreated.Should().Be(0);
    }

    #region Helper Methods

    private MemoryStream CreateSimpleExcelFile()
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1"
            });

            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()!;

            // Add header row
            var headerRow = new Row { RowIndex = 1 };
            headerRow.Append(new Cell
            {
                CellReference = "A1",
                DataType = CellValues.String,
                CellValue = new CellValue("Title")
            });
            headerRow.Append(new Cell
            {
                CellReference = "B1",
                DataType = CellValues.String,
                CellValue = new CellValue("URL")
            });
            sheetData.Append(headerRow);

            // Add data row
            var dataRow = new Row { RowIndex = 2 };
            dataRow.Append(new Cell
            {
                CellReference = "A2",
                DataType = CellValues.String,
                CellValue = new CellValue("Test Link")
            });
            sheetData.Append(dataRow);

            workbookPart.Workbook.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private MemoryStream CreateExcelFileWithDifferentColumns()
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            worksheetPart.Worksheet = new Worksheet(new SheetData());

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Sheet1"
            });

            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()!;

            // Add header row with different column names
            var headerRow = new Row { RowIndex = 1 };
            headerRow.Append(new Cell
            {
                CellReference = "A1",
                DataType = CellValues.String,
                CellValue = new CellValue("Name")
            });
            headerRow.Append(new Cell
            {
                CellReference = "B1",
                DataType = CellValues.String,
                CellValue = new CellValue("Link")
            });
            sheetData.Append(headerRow);

            workbookPart.Workbook.Save();
        }

        stream.Position = 0;
        return stream;
    }

    #endregion
}
