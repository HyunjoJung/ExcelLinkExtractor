# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ExcelLinkExtractor is an ASP.NET Core Blazor web application (.NET 10.0) that provides two main features:
1. **Link Extraction**: Extracts hyperlinks from Excel files (finds cells with hyperlinks and outputs URLs)
2. **Link Merging**: Combines separate Title and URL columns into Excel cells with clickable hyperlinks

The application uses Blazor Server with interactive components and provides a REST API for file processing.

## Build & Run Commands

```bash
# Run the application in development
dotnet run --project ExcelLinkExtractorWeb

# Build the application
dotnet build

# Restore dependencies
dotnet restore

# Publish for production
dotnet publish ExcelLinkExtractorWeb -c Release
```

The application runs on HTTPS with ports configured in `ExcelLinkExtractorWeb/Properties/launchSettings.json`.

## Architecture

### Core Components

**Services Layer** (`ExcelLinkExtractorWeb/Services/`):
- `LinkExtractorService`: Core business logic for Excel processing
  - Uses ClosedXML library for Excel manipulation
  - Implements URL sanitization with validation (http/https/mailto only, 2000 char limit)
  - Searches first 10 rows for header columns
  - All operations are async with `Task.Run` for CPU-bound work

**API Controllers** (`ExcelLinkExtractorWeb/Controllers/`):
- `FileController`: REST endpoints for file upload/download
  - POST `/api/file/extract`: Upload Excel, extract hyperlinks
  - POST `/api/file/merge-upload`: Upload Excel, merge Title+URL columns
  - GET `/api/file/template`: Download extraction template
  - GET `/api/file/merge-template`: Download merge template
  - 10MB file size limit enforced
  - Returns Base64-encoded Excel files in JSON responses

**Blazor Pages** (`ExcelLinkExtractorWeb/Components/Pages/`):
- `Home.razor` (`/`): Link extraction interface
- `Merge.razor` (`/merge`): Link merging interface
- Both use `@rendermode InteractiveServer`
- JavaScript interop for file upload/download via App.razor inline scripts

### Data Flow

1. User uploads Excel file via Blazor component
2. JavaScript interop sends file to API endpoint via FormData
3. Controller validates file (size, extension), creates MemoryStream
4. Service processes Excel using ClosedXML, generates new workbook
5. Result (stats + Base64 file) returned to Blazor component
6. User downloads processed file via JavaScript blob download

### Key Technical Details

- **Excel Processing**: ClosedXML library handles .xlsx/.xls files
- **URL Validation**: `SanitizeUrl()` method in LinkExtractorService:401
  - Adds https:// prefix if missing
  - Validates with `Uri.TryCreate`
  - Restricts to http/https/mailto schemes
  - 2000 character limit (Excel hyperlink limitation)
- **Header Detection**: Searches first 10 rows for column names (case-insensitive)
- **Dependency Injection**: LinkExtractorService registered as scoped in Program.cs:12
- **SEO**: Extensive meta tags in App.razor with structured data (JSON-LD)

## Important Notes

- No database - all processing happens in-memory
- Files are not stored on server (privacy-focused design)
- Blazor Server means WebSocket connection for interactivity
- The solution file has a Korean name ("새 폴더.sln") - this is the actual solution file
- URL sanitization is critical for security - always use `SanitizeUrl()` when setting hyperlinks
- Bootstrap 5 is used for styling (loaded from wwwroot/lib)

## Common Operations

When adding new Excel processing features:
1. Add method to `LinkExtractorService`
2. Create corresponding API endpoint in `FileController`
3. Build Blazor page with upload/download UI
4. Use JavaScript interop for file handling (see App.razor:66-118)

When modifying URL handling:
- Update `SanitizeUrl()` method in LinkExtractorService:401
- Applied in both `ExtractLinks()` and `MergeFromFile()` methods
