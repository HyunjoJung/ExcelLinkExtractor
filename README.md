# SheetLink

Free online tool to extract hyperlinks from spreadsheet files and merge Title + URL into clickable links.

🔗 **Live Site**: [sheetlink.hyunjo.uk](https://sheetlink.hyunjo.uk)

## Features

- **Excel Link Extraction**: Extract hyperlinks from Excel/spreadsheet cells and export URLs
- **Excel Link Merging**: Combine separate Title and URL columns into clickable hyperlinks in Excel
- **Excel Format Support**: Works with Microsoft Excel files (.xlsx, .xls)
- **No Server Storage**: All processing happens in-memory (privacy-focused)
- **Free & Open Source**: No registration required

## Tech Stack

- **Backend**: ASP.NET Core 10.0 (Blazor Server)
- **Excel Processing**: ClosedXML
- **Deployment**: Cloudflare Tunnel
- **Hosting**: Self-hosted on Ubuntu 24.04

## Quick Start

### Running Locally

```bash
dotnet run --project ExcelLinkExtractorWeb
```

Visit `http://localhost:5050`

### Building

```bash
dotnet build
```

### Publishing

```bash
dotnet publish ExcelLinkExtractorWeb -c Release -o ./publish
```

## Project Structure

```
ExcelLinkExtractor/
├── ExcelLinkExtractorWeb/          # Main web application
│   ├── Components/
│   │   ├── Pages/                  # Blazor pages (Home, Merge, FAQ)
│   │   └── Layout/                 # Layout components
│   ├── Controllers/                # API endpoints
│   │   └── FileController.cs       # File upload/download
│   ├── Services/                   # Business logic
│   │   └── LinkExtractorService.cs # Excel processing
│   └── wwwroot/                    # Static files
└── LICENSE                         # Apache License 2.0
```

## Key Features

### URL Sanitization

All URLs are validated and sanitized:
- Automatically adds `https://` if missing
- Validates URL format with `Uri.TryCreate`
- Restricts to `http`, `https`, `mailto` schemes only
- 2000 character limit (Excel hyperlink limitation)

### Excel Processing

- Searches first 10 rows for header columns
- Supports `.xlsx` and `.xls` formats
- 10MB file size limit
- Preserves cell styling where possible

## Contributing

Suggestions and bug reports are welcome via GitHub issues.

## Disclaimer

**SheetLink is an independent, open-source project and is not affiliated with, endorsed by, or sponsored by Microsoft Corporation.**

"Excel" and "Microsoft Excel" are trademarks of Microsoft Corporation. This tool provides functionality to work with Excel file formats (.xlsx, .xls) but is developed and maintained independently. All Excel-related file processing is performed using open-source libraries (ClosedXML).

## License

Apache License 2.0 - see [LICENSE](LICENSE) for details.

## Author

Created by [HyunjoJung](https://github.com/HyunjoJung)
