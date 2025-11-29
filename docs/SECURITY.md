# Security Policy

## Overview

SheetLink takes security and privacy seriously. This document outlines our security measures, reporting procedures, and best practices.

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| Latest  | :white_check_mark: |
| < Latest| :x:                |

We only support the latest version. Please always use the most recent release.

## Security Measures

### 1. File Validation

#### Magic Byte Validation
All uploaded files are validated against known Excel file signatures:
- **XLSX**: `50 4B 03 04` (ZIP/PK format)
- **XLS**: `D0 CF 11 E0 A1 B1 1A E1` (OLE2 format)

Files with invalid signatures are rejected immediately.

```csharp
// Example validation in LinkExtractorService.cs:40-86
private void ValidateExcelFile(Stream fileStream)
{
    // Magic bytes check prevents malicious file uploads
    // Even if renamed, non-Excel files are detected
}
```

#### File Size Limits
- **Default Maximum**: 10MB
- **Configurable**: via `appsettings.json`
- **Prevents**: DoS attacks via large file uploads

#### Empty File Detection
Empty files (0 bytes) are rejected with clear error messages.

### 2. Rate Limiting

**Protection Against**: DoS attacks, brute force, abuse

**Implementation**: ASP.NET Core `RateLimiter` middleware

**Default Configuration**:
- **100 requests per minute per IP address**
- **Algorithm**: Fixed Window
- **Response**: HTTP 429 (Too Many Requests)

**Configuration**:
```json
{
  "ExcelProcessing": {
    "RateLimitPerMinute": 100
  }
}
```

**Location**: `Program.cs:25-40`

### 3. Security Headers

All HTTP responses include comprehensive security headers:

#### Content-Security-Policy (CSP)
```
Content-Security-Policy:
  default-src 'self';
  script-src 'self' 'unsafe-inline' 'unsafe-eval';  // Blazor requirement
  style-src 'self' 'unsafe-inline';                 // Blazor requirement
  img-src 'self' data:;
  font-src 'self';
  connect-src 'self' wss: ws:;                      // SignalR WebSocket
  frame-ancestors 'none';                           // Prevent clickjacking
  base-uri 'self';
  form-action 'self';
```

**Note**: `unsafe-eval` and `unsafe-inline` are required for Blazor Server to function. This is a framework requirement, not a security weakness.

#### Additional Security Headers
```
X-Content-Type-Options: nosniff           // Prevent MIME sniffing
X-Frame-Options: DENY                     // Prevent clickjacking
X-XSS-Protection: 1; mode=block           // Enable XSS protection
Referrer-Policy: strict-origin-when-cross-origin
Permissions-Policy: geolocation=(), microphone=(), camera=()
```

**Location**: `Program.cs:46-68`

### 4. URL Sanitization

All URLs extracted or merged are validated and sanitized:

#### Validation Rules:
1. **Protocol Enforcement**: Only `http`, `https`, `mailto` allowed
2. **Length Limit**: 2000 characters (Excel limit)
3. **Format Validation**: Must be valid URI
4. **Automatic Protocol**: Adds `https://` if missing

```csharp
// Location: LinkExtractorService.cs:881-918
private static string? SanitizeUrl(string? url)
{
    // Prevents XSS, injection attacks via malicious URLs
}
```

**Rejected URLs**:
- `javascript:alert('xss')`
- `file:///etc/passwd`
- `data:text/html,<script>...</script>`
- URLs > 2000 characters

### 5. Allowed Hosts

**Production**: Only `sheetlink.hyunjo.uk` is allowed

```json
{
  "AllowedHosts": "sheetlink.hyunjo.uk"
}
```

**Purpose**: Prevents host header injection attacks

### 6. Privacy Protection

#### No File Storage
- **All processing happens in-memory**
- Files are NEVER saved to disk
- MemoryStreams are disposed after processing
- No temporary files created

#### No User Data Collection
- No cookies (except Blazor session)
- No analytics tracking
- No user accounts
- No IP logging (except rate limiting)

#### Verifiable
- **Open Source**: [GitHub Repository](https://github.com/HyunjoJung/sheetlink)
- **Transparent**: All code is public and auditable
- Review `LinkExtractorService.cs` for proof of in-memory processing

### 7. Dependency Security

#### Third-Party Libraries
| Library | Version | License | Security |
|---------|---------|---------|----------|
| DocumentFormat.OpenXml | 3.2.0 | MIT | ✅ Official Microsoft |
| ASP.NET Core | 10.0 | MIT | ✅ Microsoft-maintained |

**Update Policy**: Dependencies are kept up-to-date for security patches.

**No Known Vulnerabilities**: All dependencies are scanned regularly.

### 8. Input Validation

#### Excel File Processing
- **Column Names**: Validated against expected patterns
- **Cell Values**: String sanitization
- **Hyperlink Relationships**: Validated before creation
- **No Macros**: DocumentFormat.OpenXml does not execute macros

#### User Input
- **File Upload**: Limited to 10MB, validated file types
- **Form Data**: Blazor's built-in validation
- **Antiforgery**: Enabled by default (`app.UseAntiforgery()`)

### 9. Exception Handling

**No Information Disclosure**:
- Generic error messages shown to users
- Detailed errors logged server-side only
- Stack traces never exposed

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error");  // Server-side only
    result.ErrorMessage = "Error processing file: {ex.Message}";  // Generic message
}
```

### 10. HTTPS/TLS

**Production Deployment**:
- **Cloudflare Tunnel**: Automatic TLS termination
- **HSTS**: Enabled in production (`app.UseHsts()`)
- **TLS 1.2+**: Minimum version enforced

## Security Best Practices for Users

### For End Users

1. **Only upload trusted Excel files**
   - This tool processes Excel files server-side
   - Malicious files are rejected, but be cautious

2. **Use HTTPS**
   - Always access via `https://sheetlink.hyunjo.uk`
   - Never ignore SSL certificate warnings

3. **Sensitive Data**
   - Files are processed in-memory and immediately discarded
   - Still, avoid uploading files with sensitive personal data

4. **Verify the Site**
   - Check the URL is exactly `sheetlink.hyunjo.uk`
   - Look for the padlock icon (HTTPS)

### For Self-Hosters

1. **Update Regularly**
   ```bash
   git pull
   dotnet publish -c Release
   systemctl restart excellinkextractor
   ```

2. **Configure Firewall**
   - Only expose necessary ports
   - Use Cloudflare Tunnel or reverse proxy

3. **Set Strong AllowedHosts**
   ```json
   {
     "AllowedHosts": "your-domain.com"
   }
   ```

4. **Monitor Logs**
   - Check for unusual activity
   - Watch for failed validations
   - Review rate limit hits

5. **Adjust Rate Limits**
   ```json
   {
     "ExcelProcessing": {
       "RateLimitPerMinute": 50  // Lower if needed
     }
   }
   ```

## Reporting a Vulnerability

### How to Report

If you discover a security vulnerability, please report it responsibly:

1. **DO NOT** open a public GitHub issue
2. **DO** email the maintainer directly: [Create a security advisory on GitHub]
3. **Include**:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Suggested fix (if any)

### Response Timeline

- **Initial Response**: Within 48 hours
- **Status Update**: Within 7 days
- **Fix Timeline**: Depends on severity
  - Critical: 24-72 hours
  - High: 1-2 weeks
  - Medium: 2-4 weeks
  - Low: Next release

### Disclosure Policy

- We will acknowledge your report within 48 hours
- We will provide an estimated fix timeline
- We will notify you when the fix is deployed
- We will credit you in the release notes (if desired)
- **Coordinated Disclosure**: Please wait for our fix before public disclosure

## Security Checklist

### For Administrators

- [ ] HTTPS enabled (Cloudflare Tunnel or similar)
- [ ] AllowedHosts configured correctly
- [ ] Rate limiting configured appropriately
- [ ] Logs monitored regularly
- [ ] Dependencies updated monthly
- [ ] Security headers verified (`curl -I`)
- [ ] File size limits appropriate for your use case
- [ ] Backups configured (if needed)

### For Developers

- [ ] All inputs validated
- [ ] Exceptions properly caught
- [ ] No sensitive data in logs
- [ ] SQL injection N/A (no database)
- [ ] XSS prevention (URL sanitization)
- [ ] CSRF protection (Antiforgery enabled)
- [ ] All dependencies up-to-date
- [ ] Unit tests passing

## Security Audits

### Internal Reviews
- Code reviewed before each release
- Security headers validated
- Dependencies scanned for vulnerabilities

### External Audits
- Open source (community review)
- Lighthouse security audit: Best Practices 78/100
  - ⚠️ AdSense cookies (user choice, not our code)
  - ✅ HTTPS, CSP, security headers

## Known Limitations

1. **Blazor Server Limitations**
   - Requires `unsafe-eval` in CSP (Blazor framework requirement)
   - Persistent SignalR connection (can be targeted for DoS)

2. **In-Memory Processing**
   - Files limited by available RAM
   - Large files (>10MB) rejected by default

3. **Single-Server Deployment**
   - No distributed rate limiting
   - Server restart clears rate limit counters

## Compliance

### GDPR Compliance
- ✅ No personal data collected
- ✅ No cookies (except session)
- ✅ Files immediately discarded
- ✅ Privacy policy in FAQ

### Data Retention
- **Files**: 0 seconds (processed in-memory)
- **Logs**: Server logs rotated per systemd configuration
- **User Data**: None collected

## Security Contact

- **GitHub Issues**: https://github.com/HyunjoJung/sheetlink/issues
- **Security Advisories**: https://github.com/HyunjoJung/sheetlink/security/advisories

## References

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [ASP.NET Core Security](https://learn.microsoft.com/en-us/aspnet/core/security/)
- [Content Security Policy](https://developer.mozilla.org/en-US/docs/Web/HTTP/CSP)
- [Rate Limiting in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)
