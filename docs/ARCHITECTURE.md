# SheetLink Architecture

## Overview

SheetLink is a server-side Blazor web application that processes Excel files to extract and merge hyperlinks. The application follows clean architecture principles with clear separation of concerns.

## Technology Stack

- **Framework**: ASP.NET Core 10.0 (Blazor Server)
- **UI**: Blazor Server with SignalR for real-time updates
- **Excel Processing**: DocumentFormat.OpenXml 3.2.0 (Microsoft official library)
- **Testing**: XUnit, FluentAssertions, Moq
- **Deployment**: Self-hosted on Ubuntu 24.04 with Cloudflare Tunnel
- **Health**: `/health` endpoint using ASP.NET Core health checks (memory/disk)
- **Caching**: Response caching enabled; static assets cached for 1 week
- **Metrics**: `/metrics` endpoint (in-memory counters for processed files/errors)

## Project Structure

```
ExcelLinkExtractor/
├── ExcelLinkExtractorWeb/              # Main web application
│   ├── Components/
│   │   ├── Pages/                      # Blazor pages
│   │   │   ├── Home.razor              # Link extraction page
│   │   │   ├── Merge.razor             # Link merging page
│   │   │   └── FAQ.razor               # FAQ page
│   │   ├── Shared/                     # Reusable UI pieces (e.g., SkeletonPlaceholder)
│   │   └── Layout/                     # Layout components
│   │       ├── MainLayout.razor        # Main application layout
│   │       └── App.razor               # Root component
│   ├── Services/                       # Business logic layer
│   │   ├── ILinkExtractorService.cs    # Service interface
│   │   ├── LinkExtractorService.cs     # Core Excel processing (partial class)
│   │   ├── LinkExtractorService.Validation.cs # File validation (size/signature)
│   │   ├── LinkExtractorService.Helpers.cs    # OpenXML helpers + URL sanitization
│   │   └── ExcelProcessingException.cs # Custom exceptions
│   ├── Configuration/                  # Configuration classes
│   │   └── ExcelProcessingOptions.cs   # App settings configuration
│   ├── wwwroot/                        # Static files
│   ├── Program.cs                      # Application startup
│   └── appsettings.json                # Configuration
├── ExcelLinkExtractor.Tests/           # Unit tests
│   └── LinkExtractorServiceTests.cs    # Service tests (11 tests)
└── scripts/                            # Deployment scripts
    └── deploy.sh                       # Automated deployment
```

## Architecture Layers

### 1. Presentation Layer (Blazor Components)

**Rendering Mode**: `InteractiveServer`
- Server-side rendering with SignalR for real-time updates
- Prerendering for initial page load
- `PersistentComponentState` for state management across prerender/interactive modes

**Key Components**:
- `Home.razor` (189 lines) - Link extraction interface
- `Merge.razor` (189 lines) - Link merging interface
- `FAQ.razor` (235 lines) - SEO-optimized FAQ with JSON-LD schema

**State Management**:
```csharp
- Uses PersistingComponentStateSubscription for state persistence
- Stores: result, outputBase64, originalFileName
- Survives prerender → interactive transition
```

### 2. Service Layer

**ILinkExtractorService Interface**:
```csharp
public interface ILinkExtractorService
{
    Task<ExtractionResult> ExtractLinksAsync(Stream fileStream, string linkColumnName);
    Task<MergeResult> MergeFromFileAsync(Stream fileStream);
    byte[] CreateTemplate();
    byte[] CreateMergeTemplate();
}
```

**LinkExtractorService** (900 lines):
- **File Validation**: Magic bytes (XLSX/XLS), file size, empty file detection
- **Excel Processing**: DocumentFormat.OpenXml for reading/writing
- **Header Detection**: Searches first 10 rows for column headers
- **Link Extraction**: Extracts hyperlinks from cells, validates URLs
- **Link Merging**: Combines Title + URL columns into clickable hyperlinks
- **Template Generation**: Creates sample Excel files with proper formatting

**Performance Optimizations**:
- **Stylesheet Caching**: `Lazy<Stylesheet>` singleton, cloned for each use
- **In-Memory Processing**: No disk I/O, files processed in MemoryStream
- **Async Operations**: Task-based async for non-blocking processing

### 3. Configuration Layer

**ExcelProcessingOptions** (appsettings.json):
```json
{
  "ExcelProcessing": {
    "MaxFileSizeMB": 10,
    "MaxHeaderSearchRows": 10,
    "MaxUrlLength": 2000,
    "RateLimitPerMinute": 100
  }
}
```

Injected via `IOptions<ExcelProcessingOptions>` for runtime configuration.

### 4. Data Flow

#### Link Extraction Flow:
```
User uploads file (Home.razor)
    ↓
InputFile component → MemoryStream (max 10MB)
    ↓
ILinkExtractorService.ExtractLinksAsync()
    ↓
ValidateExcelFile() → Check magic bytes, size
    ↓
SpreadsheetDocument.Open() → Read Excel
    ↓
Find header row (search first 10 rows)
    ↓
Extract hyperlinks from target column
    ↓
Create new Excel with extracted data
    ↓
Return ExtractionResult + byte[]
    ↓
Convert to base64 → JSInterop download
```

#### Link Merging Flow:
```
User uploads file (Merge.razor)
    ↓
ILinkExtractorService.MergeFromFileAsync()
    ↓
ValidateExcelFile()
    ↓
Find 'Title' and 'URL' columns
    ↓
SanitizeUrl() for each URL
    ↓
Create new Excel with hyperlinks
    ↓
AddHyperlinkRelationship() for each link
    ↓
Return MergeResult + byte[]
    ↓
JSInterop download
```

## Security Architecture

### Defense in Depth

1. **Input Validation**:
   - File signature validation (magic bytes)
   - File size limits (10MB default)
   - Column name validation
   - URL sanitization (protocol, length, format)

2. **Rate Limiting**:
   - Fixed window: 100 req/min per IP
   - Returns HTTP 429 when exceeded
   - Configurable via appsettings.json

3. **Security Headers**:
   - Content-Security-Policy (strict, Blazor-compatible)
   - X-Content-Type-Options: nosniff
   - X-Frame-Options: DENY
   - X-XSS-Protection: 1; mode=block
   - Referrer-Policy: strict-origin-when-cross-origin
   - Permissions-Policy: geolocation(), microphone(), camera()

4. **Allowed Hosts**:
   - Production: `sheetlink.hyunjo.uk`
   - Development: `localhost;sheetlink.hyunjo.uk`

5. **Privacy**:
   - All processing in-memory
   - No file storage
   - No user data collection
   - Files discarded after processing

## Exception Handling

### Custom Exceptions:
- `ExcelProcessingException` - Base exception for all Excel errors
- `InvalidColumnException` - Column not found in spreadsheet
- `InvalidFileFormatException` - Invalid or corrupted file

### Error Flow:
```csharp
try
{
    ValidateExcelFile();  // Throws InvalidFileFormatException
    ProcessExcel();       // Throws InvalidColumnException
}
catch (InvalidFileFormatException ex)
{
    _logger.LogError(ex, "Invalid file format");
    result.ErrorMessage = ex.Message;
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error");
    result.ErrorMessage = $"Error: {ex.Message}";
}
```

## Dependency Injection

```csharp
// Configuration
builder.Services.Configure<ExcelProcessingOptions>(
    builder.Configuration.GetSection(ExcelProcessingOptions.SectionName));

// Services
builder.Services.AddScoped<ILinkExtractorService, LinkExtractorService>();

// Components inject the interface
@inject ILinkExtractorService ExtractorService
```

## Testing Strategy

### Unit Tests (11 tests, all passing):
- Template creation validation
- File validation (empty, too large, invalid format)
- Column detection (missing columns, empty names)
- Error handling (graceful failures)
- In-memory fixtures (no external dependencies)

### Test Coverage:
- Critical paths: ✅ Covered
- Edge cases: ✅ Covered
- Error scenarios: ✅ Covered
- Integration tests: ⏳ Future enhancement

## Performance Characteristics

- **File Processing**: O(n) where n = number of rows
- **Header Detection**: O(k) where k ≤ 10 rows
- **Memory Usage**: ~2x file size (input + output in memory)
- **Stylesheet Creation**: O(1) after first call (cached)

## Scalability Considerations

**Current Scale** (Optimized for):
- Files: Up to 10MB
- Rows: Up to ~100,000 rows
- Concurrent users: ~100 (rate limited)

**Bottlenecks**:
- Blazor SignalR connections (1 per user)
- In-memory processing (limited by RAM)
- Single-server deployment

**Future Enhancements** (if needed):
- Azure Blob Storage for large files
- Background job processing (Hangfire)
- Horizontal scaling (multiple servers)
- Distributed caching (Redis)

## Deployment Architecture

```
User → Cloudflare Tunnel (SSL/TLS)
    ↓
Ubuntu 24.04 Server (192.168.0.8)
    ↓
Kestrel (localhost:5050)
    ↓
ASP.NET Core 10.0 Blazor Server
    ↓
DocumentFormat.OpenXml → Excel Processing
```

**Deployment Process**:
1. Build: `dotnet publish -c Release`
2. Transfer: `rsync` to production server
3. Restart: `systemctl restart excellinkextractor`
4. Verify: Check service status

## Design Decisions

### Why Blazor Server (not WASM)?
- ✅ Server-side processing (Excel library runs on server)
- ✅ No file size limits from browser
- ✅ Better security (code not visible to client)
- ✅ Smaller initial download
- ❌ Requires persistent connection (SignalR)

### Why DocumentFormat.OpenXml (not ClosedXML)?
- ✅ Official Microsoft library
- ✅ MIT license
- ✅ Better performance
- ✅ Lower memory usage
- ✅ Active maintenance

### Why In-Memory Processing (not disk)?
- ✅ Better performance (no I/O)
- ✅ Better privacy (no file storage)
- ✅ Simpler deployment (no file permissions)
- ❌ Limited by RAM

### Why Interface-Based DI?
- ✅ Testability (easy mocking)
- ✅ Flexibility (swap implementations)
- ✅ Loose coupling
- ✅ Better for future refactoring

## Future Architecture Improvements

1. **Microservices** (if scale increases):
   - Separate Excel processing service
   - API Gateway pattern
   - Message queue for async processing

2. **Caching**:
   - Template caching (already implemented)
   - Response caching for static content
   - Distributed cache for session state

3. **Monitoring**:
   - Application Insights
   - Custom metrics (files processed, errors)
   - Performance counters

4. **Service Split** (if needed):
   - `IExcelValidationService`
   - `IHeaderDetectionService`
   - `ILinkProcessingService`
   - `IExcelWriterService`

## References

- [ASP.NET Core Blazor](https://docs.microsoft.com/en-us/aspnet/core/blazor/)
- [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK)
- [ASP.NET Core Rate Limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)
