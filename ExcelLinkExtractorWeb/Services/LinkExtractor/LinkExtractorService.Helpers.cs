using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ExcelLinkExtractorWeb.Services.LinkExtractor;

public partial class LinkExtractorService
{
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

    private static string? SanitizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        url = url.Trim();

        if (url.Length > 2000)
            return null;

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        if (uri.Scheme != "http" && uri.Scheme != "https" && uri.Scheme != "mailto")
            return null;

        return uri.AbsoluteUri;
    }
}
