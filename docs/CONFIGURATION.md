# SheetLink Configuration Guide

## Overview

SheetLink uses ASP.NET Core's configuration system with strongly-typed options. All application settings are configured via `appsettings.json` files and can be overridden with environment-specific files or environment variables.

## Configuration Files

### File Hierarchy

```
ExcelLinkExtractorWeb/
├── appsettings.json                 # Base configuration (all environments)
└── appsettings.Production.json      # Production overrides
```

**Load Order** (later files override earlier):
1. `appsettings.json` - Base settings
2. `appsettings.{Environment}.json` - Environment-specific overrides
3. Environment variables - Runtime overrides
4. Command-line arguments - Highest priority

### appsettings.json (Development)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "localhost;sheetlink.hyunjo.uk",
  "ExcelProcessing": {
    "MaxFileSizeMB": 10,
    "MaxHeaderSearchRows": 10,
    "MaxUrlLength": 2000,
    "RateLimitPerMinute": 100
  }
}
```

### appsettings.Production.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "sheetlink.hyunjo.uk"
}
```

## Configuration Sections

### 1. Logging

Controls application logging verbosity.

**Properties**:
- `LogLevel.Default` - Default log level for all loggers
- `LogLevel.Microsoft.AspNetCore` - Log level for ASP.NET Core framework

**Values**: `Trace` | `Debug` | `Information` | `Warning` | `Error` | `Critical` | `None`

**Recommendations**:
- **Development**: `Information` (see all app events)
- **Production**: `Warning` (reduce log noise)

**Example**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "ExcelLinkExtractorWeb.Services": "Debug"
    }
  }
}
```

### 2. AllowedHosts

**Type**: `string` (semicolon-separated)

**Purpose**: Prevents host header injection attacks by validating the `Host` header in requests.

**Syntax**: `"host1;host2;host3"` or `"*"` (allow all, NOT recommended)

**Examples**:
- Development: `"localhost;127.0.0.1;sheetlink.hyunjo.uk"`
- Production: `"sheetlink.hyunjo.uk"`
- Self-hosted: `"yourdomain.com"`
- Multiple domains: `"example.com;www.example.com"`

**Security Note**: ALWAYS restrict this in production. Never use `"*"` in production.

### 3. ExcelProcessing

**Type**: `ExcelProcessingOptions` (strongly-typed)

**Location**: `ExcelLinkExtractorWeb/Configuration/ExcelProcessingOptions.cs`

#### MaxFileSizeMB

**Type**: `int`
**Default**: `10`
**Unit**: Megabytes (MB)
**Range**: `1` - `100` (recommended)

**Purpose**: Maximum file size accepted for upload. Files exceeding this limit are rejected with error message.

**Calculated Property**: `MaxFileSizeBytes` returns `MaxFileSizeMB * 1024 * 1024`

**Considerations**:
- **Memory Usage**: Files are processed in-memory, so limit should be ~10% of available RAM
- **Processing Time**: Larger files take longer to process
- **User Experience**: Files >10MB are rarely needed for link extraction
- **DoS Protection**: Prevents attackers from exhausting memory

**Examples**:
```json
// Small files only (low-traffic scenarios)
"MaxFileSizeMB": 5

// Standard (recommended)
"MaxFileSizeMB": 10

// Large files (if you have sufficient RAM)
"MaxFileSizeMB": 25
```

#### MaxHeaderSearchRows

**Type**: `int`
**Default**: `10`
**Range**: `1` - `50` (recommended)

**Purpose**: Number of rows to search for column headers (Title, URL, etc.) before giving up.

**Why Limited**:
- Most Excel files have headers in row 1-3
- Searching too many rows impacts performance
- Prevents malicious files from causing excessive processing

**Examples**:
```json
// Strict (headers must be in first 5 rows)
"MaxHeaderSearchRows": 5

// Standard (recommended)
"MaxHeaderSearchRows": 10

// Lenient (for files with metadata at top)
"MaxHeaderSearchRows": 20
```

#### MaxUrlLength

**Type**: `int`
**Default**: `2000`
**Unit**: Characters
**Range**: `100` - `2000` (recommended)

**Purpose**: Maximum length for extracted/merged URLs. Longer URLs are truncated or rejected.

**Why 2000**:
- **Excel Limit**: Excel hyperlinks support up to ~2000 characters
- **Browser Compatibility**: Most browsers support ~2000 character URLs
- **Standards**: HTTP specifications recommend 2000-8000 characters

**Examples**:
```json
// Strict (most URLs are <500 characters)
"MaxUrlLength": 500

// Standard (Excel limit)
"MaxUrlLength": 2000

// Lenient (for very long URLs)
"MaxUrlLength": 4000
```

#### RateLimitPerMinute

**Type**: `int`
**Default**: `100`
**Unit**: Requests per minute per IP address
**Range**: `10` - `1000`

**Purpose**: Maximum number of requests allowed from a single IP address within a 1-minute window.

**Algorithm**: Fixed Window Rate Limiting
**Response**: HTTP 429 (Too Many Requests) when exceeded

**Considerations**:
- **Traffic**: Adjust based on expected legitimate usage
- **DoS Protection**: Lower values provide better protection
- **User Experience**: Too low may frustrate legitimate users
- **NAT/Proxies**: Users behind corporate NAT share the same IP
- **Testing**: For CI/E2E runs, set `ExcelProcessing__RateLimitPerMinute` to a high value (e.g., `10000`) to avoid 429s during parallel tests.

**Examples**:
```json
// Conservative (low-traffic personal site)
"RateLimitPerMinute": 50

// Standard (recommended for public sites)
"RateLimitPerMinute": 100

// Permissive (high-traffic or trusted users)
"RateLimitPerMinute": 200
```

## Environment-Specific Configuration

### Development Environment

**File**: `appsettings.json`

**Characteristics**:
- Verbose logging (`Information`)
- Allow localhost access
- Lenient rate limits for testing
- Detailed error messages

**Example**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AllowedHosts": "localhost;127.0.0.1",
  "ExcelProcessing": {
    "MaxFileSizeMB": 10,
    "RateLimitPerMinute": 1000
  }
}
```

### Production Environment

**File**: `appsettings.Production.json`

**Characteristics**:
- Minimal logging (`Warning` or `Error`)
- Strict AllowedHosts (only production domain)
- Standard rate limits
- Generic error messages

**Example**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Error"
    }
  },
  "AllowedHosts": "sheetlink.hyunjo.uk",
  "ExcelProcessing": {
    "MaxFileSizeMB": 10,
    "RateLimitPerMinute": 100
  }
}
```

### Testing Environment

**File**: `appsettings.Testing.json` (create if needed)

**Characteristics**:
- Debug logging
- No rate limits
- Small file sizes (for quick tests)

**Example**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  },
  "ExcelProcessing": {
    "MaxFileSizeMB": 1,
    "RateLimitPerMinute": 10000
  }
}
```

## Common Configuration Scenarios

### Scenario 1: Low-Traffic Personal Site

**Goal**: Minimize resource usage, conservative limits

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  },
  "AllowedHosts": "mysite.com",
  "ExcelProcessing": {
    "MaxFileSizeMB": 5,
    "MaxHeaderSearchRows": 5,
    "MaxUrlLength": 1000,
    "RateLimitPerMinute": 50
  }
}
```

### Scenario 2: High-Traffic Public Site

**Goal**: Handle more users, maintain security

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  },
  "AllowedHosts": "sheetlink.com",
  "ExcelProcessing": {
    "MaxFileSizeMB": 10,
    "MaxHeaderSearchRows": 10,
    "MaxUrlLength": 2000,
    "RateLimitPerMinute": 200
  }
}
```

### Scenario 3: Corporate Internal Deployment

**Goal**: Larger files, trusted users, less restrictive

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AllowedHosts": "sheetlink.internal.company.com",
  "ExcelProcessing": {
    "MaxFileSizeMB": 25,
    "MaxHeaderSearchRows": 20,
    "MaxUrlLength": 2000,
    "RateLimitPerMinute": 500
  }
}
```

### Scenario 4: Development/Testing

**Goal**: Fast iteration, detailed logging

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "ExcelLinkExtractorWeb.Services.LinkExtractorService": "Trace"
    }
  },
  "AllowedHosts": "*",
  "ExcelProcessing": {
    "MaxFileSizeMB": 1,
    "MaxHeaderSearchRows": 5,
    "MaxUrlLength": 500,
    "RateLimitPerMinute": 10000
  }
}
```

## Configuration Validation

### At Startup

The application validates configuration on startup. Invalid values will cause startup failure.

**Validation Rules**:
- `MaxFileSizeMB` must be > 0
- `MaxHeaderSearchRows` must be > 0
- `MaxUrlLength` must be > 0
- `RateLimitPerMinute` must be > 0
- Data annotations (`[Range]`) are enforced via `ValidateDataAnnotations()` with `ValidateOnStart()` for fail-fast startup if values are out of range.

### Runtime Validation

Configuration is loaded into `ExcelProcessingOptions` class via `IOptions<T>` pattern.

**Access in Code**:
```csharp
public class LinkExtractorService : ILinkExtractorService
{
    private readonly ExcelProcessingOptions _options;

    public LinkExtractorService(IOptions<ExcelProcessingOptions> options)
    {
        _options = options.Value;

        // Use configuration
        if (fileStream.Length > _options.MaxFileSizeBytes)
        {
            throw new InvalidFileFormatException(
                $"File size exceeds maximum allowed size of {_options.MaxFileSizeMB}MB.");
        }
    }
}
```

## Overriding Configuration

### Environment Variables

Override any setting using environment variables with `__` (double underscore) as separator.

**Syntax**: `SectionName__PropertyName=Value`

**Examples**:
```bash
# Linux/macOS
export ExcelProcessing__MaxFileSizeMB=25
export ExcelProcessing__RateLimitPerMinute=200

# Windows (PowerShell)
$env:ExcelProcessing__MaxFileSizeMB="25"
$env:ExcelProcessing__RateLimitPerMinute="200"

# Docker
docker run -e ExcelProcessing__MaxFileSizeMB=25 ...
```

### Command-Line Arguments

Override settings via command-line arguments.

**Syntax**: `--SectionName:PropertyName=Value`

**Examples**:
```bash
dotnet run --ExcelProcessing:MaxFileSizeMB=25 --ExcelProcessing:RateLimitPerMinute=200
```

### systemd Service (Production)

**File**: `/etc/systemd/system/excellinkextractor.service`

```ini
[Unit]
Description=SheetLink Excel Link Extractor

[Service]
WorkingDirectory=/var/www/ExcelLinkExtractor
ExecStart=/usr/bin/dotnet /var/www/ExcelLinkExtractor/ExcelLinkExtractorWeb.dll
Restart=always
RestartSec=10
SyslogIdentifier=excellinkextractor
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ExcelProcessing__MaxFileSizeMB=10
Environment=ExcelProcessing__RateLimitPerMinute=100

[Install]
WantedBy=multi-user.target
```

## Configuration Best Practices

### 1. Never Commit Secrets

**DON'T**:
```json
{
  "ConnectionStrings": {
    "Database": "Server=localhost;Password=secretpassword123"
  }
}
```

**DO** (use environment variables or Azure Key Vault):
```bash
export ConnectionStrings__Database="Server=localhost;Password=secretpassword123"
```

### 2. Use Environment-Specific Files

**Structure**:
```
appsettings.json              # Base settings (committed to git)
appsettings.Development.json  # Development overrides (committed)
appsettings.Production.json   # Production overrides (committed)
appsettings.Local.json        # Local developer overrides (in .gitignore)
```

### 3. Document Your Changes

Add comments to configuration files (JSON supports `//` comments in .NET):

```json
{
  "ExcelProcessing": {
    // Reduced from 10MB to 5MB due to memory constraints (2024-01-15)
    "MaxFileSizeMB": 5,

    // Increased from 100 to 200 to handle peak traffic (2024-01-20)
    "RateLimitPerMinute": 200
  }
}
```

### 4. Test Configuration Changes

Always test configuration changes before deploying to production:

```bash
# Test with Production configuration locally
export ASPNETCORE_ENVIRONMENT=Production
dotnet run

# Verify settings are loaded correctly (check logs)
```

### 5. Monitor Configuration Impact

After changing configuration in production:
- **MaxFileSizeMB**: Monitor memory usage
- **RateLimitPerMinute**: Monitor 429 responses
- **MaxHeaderSearchRows**: Monitor processing time
- **Logging**: Monitor log volume and disk space

## Troubleshooting

### Issue: "File size exceeds maximum allowed size"

**Cause**: File larger than `MaxFileSizeMB`

**Solution**:
1. Check current limit: Look in `appsettings.json` → `ExcelProcessing.MaxFileSizeMB`
2. Increase if needed (ensure sufficient RAM)
3. Restart application

```json
{
  "ExcelProcessing": {
    "MaxFileSizeMB": 25  // Increased from 10
  }
}
```

### Issue: "Column 'Title' not found in the first X rows"

**Cause**: Headers are beyond `MaxHeaderSearchRows`

**Solution**: Increase `MaxHeaderSearchRows`

```json
{
  "ExcelProcessing": {
    "MaxHeaderSearchRows": 20  // Increased from 10
  }
}
```

### Issue: HTTP 429 (Too Many Requests)

**Cause**: Exceeded `RateLimitPerMinute`

**Solutions**:
1. **For legitimate users**: Increase rate limit
2. **For attackers**: Keep limit low, consider IP blocking
3. **For testing**: Set high limit in Development environment

```json
{
  "ExcelProcessing": {
    "RateLimitPerMinute": 200  // Increased from 100
  }
}
```

### Issue: Configuration Not Loaded

**Symptoms**: Changes to `appsettings.json` not taking effect

**Checklist**:
1. **Restart required**: Configuration is loaded at startup only
2. **File location**: Must be in same directory as `.dll`
3. **JSON syntax**: Validate JSON (no trailing commas, proper quotes)
4. **Environment**: Check `ASPNETCORE_ENVIRONMENT` variable
5. **Deployment**: Ensure file is copied during publish

**Verify**:
```bash
# Check which configuration file is being used
ls -la /var/www/ExcelLinkExtractor/appsettings*.json

# Check environment
echo $ASPNETCORE_ENVIRONMENT

# Restart service
sudo systemctl restart excellinkextractor

# Check logs
journalctl -u excellinkextractor -n 50
```

### Issue: Invalid JSON Syntax

**Error**: `Unhandled exception. System.Text.Json.JsonException`

**Common Mistakes**:
- Trailing commas
- Missing quotes
- Comments in wrong format

**Validate JSON**:
```bash
# Using Python
python3 -m json.tool appsettings.json

# Using jq
jq . appsettings.json

# Online: https://jsonlint.com/
```

## Configuration Reference

### Complete Example

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "ExcelLinkExtractorWeb.Services": "Information"
    }
  },
  "AllowedHosts": "localhost;sheetlink.hyunjo.uk",
  "ExcelProcessing": {
    "MaxFileSizeMB": 10,
    "MaxHeaderSearchRows": 10,
    "MaxUrlLength": 2000,
    "RateLimitPerMinute": 100
  }
}
```

### ExcelProcessingOptions Class

**Source**: `ExcelLinkExtractorWeb/Configuration/ExcelProcessingOptions.cs`

```csharp
public class ExcelProcessingOptions
{
    public const string SectionName = "ExcelProcessing";

    /// <summary>
    /// Maximum file size in megabytes. Default: 10MB
    /// </summary>
    public int MaxFileSizeMB { get; set; } = 10;

    /// <summary>
    /// Maximum number of rows to search for column headers. Default: 10
    /// </summary>
    public int MaxHeaderSearchRows { get; set; } = 10;

    /// <summary>
    /// Maximum URL length in characters. Default: 2000
    /// </summary>
    public int MaxUrlLength { get; set; } = 2000;

    /// <summary>
    /// Rate limit per minute per IP address. Default: 100
    /// </summary>
    public int RateLimitPerMinute { get; set; } = 100;

    /// <summary>
    /// Calculated property: MaxFileSizeMB converted to bytes
    /// </summary>
    public int MaxFileSizeBytes => MaxFileSizeMB * 1024 * 1024;
}
```

## Security Considerations

### 1. AllowedHosts

**Risk**: Host header injection attacks

**Mitigation**: Always restrict to known domains in production

**Example**:
```json
// ❌ NEVER in production
"AllowedHosts": "*"

// ✅ Always restrict
"AllowedHosts": "sheetlink.hyunjo.uk"
```

### 2. Rate Limiting

**Risk**: DoS attacks, resource exhaustion

**Mitigation**: Set appropriate limits based on server capacity

**Monitoring**: Track 429 responses to detect abuse

### 3. File Size Limits

**Risk**: Memory exhaustion, DoS

**Mitigation**: Keep `MaxFileSizeMB` low relative to available RAM

**Formula**: `MaxFileSizeMB` should be ≤ 10% of available RAM

### 4. Logging

**Risk**: Information disclosure via logs

**Mitigation**:
- Use `Warning` or `Error` in production
- Never log sensitive data (file contents, URLs with tokens)
- Rotate logs regularly

### 5. Configuration Files

**Risk**: Secrets in version control

**Mitigation**:
- Never commit secrets to git
- Use environment variables for sensitive data
- Add `appsettings.Local.json` to `.gitignore`

## Related Documentation

- **Architecture**: See [ARCHITECTURE.md](ARCHITECTURE.md)
- **Security**: See [SECURITY.md](SECURITY.md)
- **Deployment**: See [README.md](README.md#deployment)
- **ASP.NET Core Configuration**: [Microsoft Docs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
