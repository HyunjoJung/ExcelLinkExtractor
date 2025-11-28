# ExcelLinkExtractor

Free online tool to extract hyperlinks from Excel files and merge Title + URL into clickable links.

🔗 **Live Site**: [excellink.hyunjo.uk](https://excellink.hyunjo.uk)

## Features

- **Link Extraction**: Extract hyperlinks from Excel cells and export URLs
- **Link Merging**: Combine separate Title and URL columns into clickable hyperlinks
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

Visit `http://localhost:5000`

### Deployment

See [docs/DEPLOY-NOW.md](docs/DEPLOY-NOW.md) for quick deployment guide.

Full documentation:
- [DEPLOYMENT.md](docs/DEPLOYMENT.md) - Detailed deployment instructions
- [QUICKSTART.md](docs/QUICKSTART.md) - Step-by-step deployment guide
- [CLAUDE.md](CLAUDE.md) - Codebase architecture and guidance

## Project Structure

```
ExcelLinkExtractor/
├── ExcelLinkExtractorWeb/          # Main web application
│   ├── Components/
│   │   ├── Pages/                  # Blazor pages (Home, Merge)
│   │   └── Layout/                 # Layout components
│   ├── Controllers/                # API endpoints
│   │   └── FileController.cs       # File upload/download
│   ├── Services/                   # Business logic
│   │   └── LinkExtractorService.cs # Excel processing
│   └── wwwroot/                    # Static files
├── docs/                           # Documentation
│   ├── DEPLOYMENT.md               # Deployment guide
│   ├── QUICKSTART.md               # Quick start guide
│   └── DEPLOY-NOW.md               # Simplified deployment
├── scripts/                        # Deployment scripts
│   ├── deploy.sh                   # Auto-deploy script
│   ├── setup-server.sh             # Server setup
│   └── cloudflare-tunnel-setup.sh  # Cloudflare Tunnel setup
└── CLAUDE.md                       # Code architecture
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

## Development

### Building

```bash
dotnet build
```

### Publishing

```bash
dotnet publish ExcelLinkExtractorWeb -c Release -o ./publish
```

### Testing Deployment

```bash
# Deploy to server
./scripts/deploy.sh

# Check service status
ssh joe@192.168.0.8 "sudo systemctl status excellinkextractor"

# View logs
ssh joe@192.168.0.8 "sudo journalctl -u excellinkextractor -f"
```

## Contributing

This is a personal project, but suggestions and bug reports are welcome via GitHub issues.

## License

MIT License - feel free to use this code for your own projects.

## Author

Created for personal use and made available as open source.

---

For detailed code architecture and development guidelines, see [CLAUDE.md](CLAUDE.md).
