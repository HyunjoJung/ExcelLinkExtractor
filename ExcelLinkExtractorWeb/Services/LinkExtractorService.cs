using ClosedXML.Excel;

namespace ExcelLinkExtractorWeb.Services;

public class LinkExtractorService
{
    public class ExtractionResult
    {
        public int TotalRows { get; set; }
        public int LinksFound { get; set; }
        public List<LinkInfo> Links { get; set; } = new();
        public byte[]? OutputFile { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class LinkInfo
    {
        public int Row { get; set; }
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public async Task<ExtractionResult> ExtractLinksAsync(Stream fileStream, string linkColumnName = "제목")
    {
        return await Task.Run(() => ExtractLinks(fileStream, linkColumnName));
    }

    private ExtractionResult ExtractLinks(Stream fileStream, string linkColumnName)
    {
        var result = new ExtractionResult();

        try
        {
            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheet(1);

            // 헤더 행 찾기
            int? headerRowNumber = null;
            int? targetColumnIndex = null;

            for (int i = 1; i <= 10; i++)
            {
                var row = worksheet.Row(i);
                foreach (var cell in row.CellsUsed())
                {
                    if (cell.GetValue<string>().Equals(linkColumnName, StringComparison.OrdinalIgnoreCase))
                    {
                        headerRowNumber = i;
                        targetColumnIndex = cell.Address.ColumnNumber;
                        break;
                    }
                }
                if (headerRowNumber != null) break;
            }

            if (targetColumnIndex == null)
            {
                result.ErrorMessage = $"Column '{linkColumnName}' not found.";
                return result;
            }

            // Create new workbook
            var newWorkbook = new XLWorkbook();
            var newWorksheet = newWorkbook.Worksheets.Add("Extracted Links");

            // 헤더 복사
            var originalHeaderRow = worksheet.Row(headerRowNumber.Value);
            foreach (var cell in originalHeaderRow.CellsUsed())
            {
                var newCell = newWorksheet.Cell(1, cell.Address.ColumnNumber);
                newCell.Value = cell.Value;
                newCell.Style.Font.Bold = true;
            }

            // 데이터 행 순회
            int dataStartRow = headerRowNumber.Value + 1;
            int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

            for (int i = dataStartRow; i <= lastRow; i++)
            {
                var row = worksheet.Row(i);
                int newRowIndex = i - dataStartRow + 2;

                var titleCell = row.Cell(targetColumnIndex.Value);
                string extractedUrl = "";

                // 하이퍼링크 추출
                if (titleCell.HasHyperlink)
                {
                    var hyperlink = titleCell.GetHyperlink();
                    extractedUrl = hyperlink.ExternalAddress?.ToString() ?? hyperlink.InternalAddress ?? "";

                    result.Links.Add(new LinkInfo
                    {
                        Row = i,
                        Title = titleCell.GetValue<string>(),
                        Url = extractedUrl
                    });
                }

                // 모든 열 복사
                foreach (var cell in row.CellsUsed())
                {
                    var newCell = newWorksheet.Cell(newRowIndex, cell.Address.ColumnNumber);
                    newCell.Value = cell.Value;

                    if (cell.HasHyperlink)
                    {
                        var hyperlink = cell.GetHyperlink();
                        string url = hyperlink.ExternalAddress?.ToString() ?? hyperlink.InternalAddress ?? "";
                        newCell.SetHyperlink(new XLHyperlink(url));
                        newCell.Style.Font.FontColor = XLColor.Blue;
                        newCell.Style.Font.Underline = XLFontUnderlineValues.Single;
                    }
                }

                // B열에 URL 텍스트 추가
                if (!string.IsNullOrEmpty(extractedUrl))
                {
                    var linkTextCell = newWorksheet.Cell(newRowIndex, 2);
                    linkTextCell.Value = extractedUrl;
                }
            }

            result.TotalRows = lastRow - dataStartRow + 1;
            result.LinksFound = result.Links.Count;

            // 열 너비 조정
            newWorksheet.Columns().AdjustToContents();

            // 메모리 스트림으로 저장
            using var outputStream = new MemoryStream();
            newWorkbook.SaveAs(outputStream);
            result.OutputFile = outputStream.ToArray();
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Error processing file: {ex.Message}";
        }

        return result;
    }

    public byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Data");

        // 헤더
        worksheet.Cell(1, 1).Value = "Title";
        worksheet.Cell(1, 2).Value = "URL";

        // 헤더 스타일
        var headerRange = worksheet.Range(1, 1, 1, 2);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;

        // 예시 데이터 (하이퍼링크 포함)
        worksheet.Cell(2, 1).Value = "Example Link 1";
        worksheet.Cell(2, 1).SetHyperlink(new XLHyperlink("https://www.example.com"));
        worksheet.Cell(2, 1).Style.Font.FontColor = XLColor.Blue;
        worksheet.Cell(2, 1).Style.Font.Underline = XLFontUnderlineValues.Single;

        worksheet.Cell(3, 1).Value = "Example Link 2";
        worksheet.Cell(3, 1).SetHyperlink(new XLHyperlink("https://www.google.com"));
        worksheet.Cell(3, 1).Style.Font.FontColor = XLColor.Blue;
        worksheet.Cell(3, 1).Style.Font.Underline = XLFontUnderlineValues.Single;

        // 안내 텍스트
        worksheet.Cell(5, 1).Value = "Add hyperlinks to Title column. URLs will be extracted automatically.";
        worksheet.Cell(5, 1).Style.Font.FontColor = XLColor.Gray;

        // 열 너비 조정
        worksheet.Column(1).Width = 30;
        worksheet.Column(2).Width = 50;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] CreateMergedFile(List<string> titles, List<string> urls)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Merged Links");

        // 헤더
        worksheet.Cell(1, 1).Value = "Title";
        worksheet.Cell(1, 2).Value = "URL";
        var headerRange = worksheet.Range(1, 1, 1, 2);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGreen;

        // 데이터 추가
        for (int i = 0; i < titles.Count; i++)
        {
            var rowIndex = i + 2;
            var titleCell = worksheet.Cell(rowIndex, 1);
            titleCell.Value = titles[i];

            if (!string.IsNullOrEmpty(urls[i]))
            {
                titleCell.SetHyperlink(new XLHyperlink(urls[i]));
                titleCell.Style.Font.FontColor = XLColor.Blue;
                titleCell.Style.Font.Underline = XLFontUnderlineValues.Single;
            }

            worksheet.Cell(rowIndex, 2).Value = urls[i];
        }

        // 열 너비 조정
        worksheet.Column(1).Width = 40;
        worksheet.Column(2).Width = 60;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
