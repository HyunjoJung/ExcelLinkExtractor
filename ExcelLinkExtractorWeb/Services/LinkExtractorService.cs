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

    public async Task<ExtractionResult> ExtractLinksAsync(Stream fileStream, string linkColumnName = "Title")
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

            // Find header row
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

            // Copy header
            var originalHeaderRow = worksheet.Row(headerRowNumber.Value);
            foreach (var cell in originalHeaderRow.CellsUsed())
            {
                var newCell = newWorksheet.Cell(1, cell.Address.ColumnNumber);
                newCell.Value = cell.Value;
                newCell.Style.Font.Bold = true;
            }

            // Iterate data rows
            int dataStartRow = headerRowNumber.Value + 1;
            int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

            for (int i = dataStartRow; i <= lastRow; i++)
            {
                var row = worksheet.Row(i);
                int newRowIndex = i - dataStartRow + 2;

                var titleCell = row.Cell(targetColumnIndex.Value);
                string extractedUrl = "";

                // Extract hyperlink
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

                // Copy all columns
                foreach (var cell in row.CellsUsed())
                {
                    var newCell = newWorksheet.Cell(newRowIndex, cell.Address.ColumnNumber);
                    newCell.Value = cell.Value;

                    if (cell.HasHyperlink)
                    {
                        var hyperlink = cell.GetHyperlink();
                        string url = hyperlink.ExternalAddress?.ToString() ?? hyperlink.InternalAddress ?? "";
                        var sanitized = SanitizeUrl(url);
                        if (!string.IsNullOrEmpty(sanitized))
                        {
                            try
                            {
                                newCell.SetHyperlink(new XLHyperlink(sanitized));
                                newCell.Style.Font.FontColor = XLColor.Blue;
                                newCell.Style.Font.Underline = XLFontUnderlineValues.Single;
                            }
                            catch { }
                        }
                    }
                }

                // Add URL text to column B
                if (!string.IsNullOrEmpty(extractedUrl))
                {
                    var linkTextCell = newWorksheet.Cell(newRowIndex, 2);
                    linkTextCell.Value = extractedUrl;
                }
            }

            result.TotalRows = lastRow - dataStartRow + 1;
            result.LinksFound = result.Links.Count;

            // Adjust column width
            newWorksheet.Columns().AdjustToContents();

            // Save to memory stream
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

        // Header
        worksheet.Cell(1, 1).Value = "Title";
        worksheet.Cell(1, 2).Value = "URL";

        // Header style
        var headerRange = worksheet.Range(1, 1, 1, 2);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;

        // Sample data (with hyperlinks)
        worksheet.Cell(2, 1).Value = "Example Link 1";
        worksheet.Cell(2, 1).SetHyperlink(new XLHyperlink("https://www.example.com"));
        worksheet.Cell(2, 1).Style.Font.FontColor = XLColor.Blue;
        worksheet.Cell(2, 1).Style.Font.Underline = XLFontUnderlineValues.Single;

        worksheet.Cell(3, 1).Value = "Example Link 2";
        worksheet.Cell(3, 1).SetHyperlink(new XLHyperlink("https://www.google.com"));
        worksheet.Cell(3, 1).Style.Font.FontColor = XLColor.Blue;
        worksheet.Cell(3, 1).Style.Font.Underline = XLFontUnderlineValues.Single;

        // Instruction text
        worksheet.Cell(5, 1).Value = "Add hyperlinks to Title column. URLs will be extracted automatically.";
        worksheet.Cell(5, 1).Style.Font.FontColor = XLColor.Gray;

        // Adjust column width
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

        // Header
        worksheet.Cell(1, 1).Value = "Title";
        worksheet.Cell(1, 2).Value = "URL";
        var headerRange = worksheet.Range(1, 1, 1, 2);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGreen;

        // Add data
        for (int i = 0; i < titles.Count; i++)
        {
            var rowIndex = i + 2;
            var titleCell = worksheet.Cell(rowIndex, 1);
            titleCell.Value = titles[i];

            var sanitizedUrl = SanitizeUrl(urls[i]);
            if (!string.IsNullOrEmpty(sanitizedUrl))
            {
                try
                {
                    titleCell.SetHyperlink(new XLHyperlink(sanitizedUrl));
                    titleCell.Style.Font.FontColor = XLColor.Blue;
                    titleCell.Style.Font.Underline = XLFontUnderlineValues.Single;
                }
                catch
                {
                    // Invalid URL - skip hyperlink but keep text
                }
            }

            worksheet.Cell(rowIndex, 2).Value = urls[i];
        }

        // Adjust column width
        worksheet.Column(1).Width = 40;
        worksheet.Column(2).Width = 60;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] CreateMergeTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Data");

        // Header
        worksheet.Cell(1, 1).Value = "Title";
        worksheet.Cell(1, 2).Value = "URL";

        // Header style
        var headerRange = worksheet.Range(1, 1, 1, 2);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGreen;

        // Sample data
        worksheet.Cell(2, 1).Value = "Google";
        worksheet.Cell(2, 2).Value = "https://www.google.com";

        worksheet.Cell(3, 1).Value = "GitHub";
        worksheet.Cell(3, 2).Value = "https://www.github.com";

        worksheet.Cell(4, 1).Value = "YouTube";
        worksheet.Cell(4, 2).Value = "https://www.youtube.com";

        // Instruction text
        worksheet.Cell(6, 1).Value = "Paste your Title and URL data here. Hyperlinks will be created automatically.";
        worksheet.Cell(6, 1).Style.Font.FontColor = XLColor.Gray;

        // Adjust column width
        worksheet.Column(1).Width = 30;
        worksheet.Column(2).Width = 50;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public class MergeResult
    {
        public int TotalRows { get; set; }
        public int LinksCreated { get; set; }
        public List<LinkInfo> Links { get; set; } = new();
        public byte[]? OutputFile { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public async Task<MergeResult> MergeFromFileAsync(Stream fileStream)
    {
        return await Task.Run(() => MergeFromFile(fileStream));
    }

    private MergeResult MergeFromFile(Stream fileStream)
    {
        var result = new MergeResult();

        try
        {
            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheet(1);

            // Find header row
            int? headerRowNumber = null;
            int? titleColumnIndex = null;
            int? urlColumnIndex = null;

            for (int i = 1; i <= 10; i++)
            {
                var row = worksheet.Row(i);
                foreach (var cell in row.CellsUsed())
                {
                    var value = cell.GetValue<string>().ToLower();
                    if (value == "title")
                    {
                        headerRowNumber = i;
                        titleColumnIndex = cell.Address.ColumnNumber;
                    }
                    else if (value == "url")
                    {
                        urlColumnIndex = cell.Address.ColumnNumber;
                    }
                }
                if (titleColumnIndex != null && urlColumnIndex != null) break;
            }

            if (titleColumnIndex == null || urlColumnIndex == null)
            {
                result.ErrorMessage = "Could not find 'Title' and 'URL' columns.";
                return result;
            }

            // Create new workbook with hyperlinks
            var newWorkbook = new XLWorkbook();
            var newWorksheet = newWorkbook.Worksheets.Add("Merged Links");

            // Header
            newWorksheet.Cell(1, 1).Value = "Title";
            newWorksheet.Cell(1, 2).Value = "URL";
            var headerRange = newWorksheet.Range(1, 1, 1, 2);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGreen;

            // Process data rows
            int dataStartRow = headerRowNumber!.Value + 1;
            int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
            int newRowIndex = 2;

            for (int i = dataStartRow; i <= lastRow; i++)
            {
                var row = worksheet.Row(i);
                var title = row.Cell(titleColumnIndex.Value).GetValue<string>().Trim();
                var url = row.Cell(urlColumnIndex.Value).GetValue<string>().Trim();

                if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(url))
                    continue;

                var titleCell = newWorksheet.Cell(newRowIndex, 1);
                titleCell.Value = title;

                var sanitizedUrl = SanitizeUrl(url);
                if (!string.IsNullOrEmpty(sanitizedUrl))
                {
                    try
                    {
                        titleCell.SetHyperlink(new XLHyperlink(sanitizedUrl));
                        titleCell.Style.Font.FontColor = XLColor.Blue;
                        titleCell.Style.Font.Underline = XLFontUnderlineValues.Single;
                        result.LinksCreated++;
                    }
                    catch
                    {
                        // Invalid URL - skip hyperlink but keep text
                    }
                }

                newWorksheet.Cell(newRowIndex, 2).Value = url;

                result.Links.Add(new LinkInfo
                {
                    Row = newRowIndex,
                    Title = title,
                    Url = url
                });

                newRowIndex++;
            }

            result.TotalRows = newRowIndex - 2;

            // Adjust column width
            newWorksheet.Column(1).Width = 40;
            newWorksheet.Column(2).Width = 60;

            // Save to memory stream
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

    private static string? SanitizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        url = url.Trim();

        // Excel hyperlink limit is about 2083 characters
        if (url.Length > 2000)
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
