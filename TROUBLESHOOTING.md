# SheetLink Troubleshooting Guide

This guide helps you diagnose and resolve common issues with SheetLink.

## Table of Contents

- [File Upload Issues](#file-upload-issues)
- [Link Extraction Issues](#link-extraction-issues)
- [Link Merging Issues](#link-merging-issues)
- [Download Issues](#download-issues)
- [Performance Issues](#performance-issues)
- [Deployment Issues](#deployment-issues)
- [Development Issues](#development-issues)
- [Configuration Issues](#configuration-issues)

## File Upload Issues

### Issue: "File size exceeds maximum allowed size of 10MB"

**Cause**: The uploaded file is larger than the configured maximum file size.

**Solutions**:

1. **Reduce file size**:
   - Remove unnecessary columns or rows
   - Delete hidden sheets
   - Remove embedded images or charts
   - Save as `.xlsx` instead of `.xls` (usually smaller)

2. **Increase limit** (self-hosting only):
   ```json
   // appsettings.json
   {
     "ExcelProcessing": {
       "MaxFileSizeMB": 25  // Increase from 10 to 25
     }
   }
   ```
   Restart the application after changing configuration.

**Prevention**: Keep Excel files focused on data only, remove formatting and media when possible.

---

### Issue: "File appears to be empty or invalid"

**Cause**: The file has 0 bytes or is corrupted.

**Solutions**:

1. **Verify file integrity**:
   - Open the file in Excel to confirm it's valid
   - Check file size (should be > 0 bytes)
   - Try re-saving the file as a new copy

2. **Check file format**:
   - Ensure file extension is `.xlsx` or `.xls`
   - Avoid `.xlsm` (macro-enabled) or `.xlsb` (binary)
   - Re-save as `.xlsx` if necessary

3. **Check for corruption**:
   - Open in Excel and use "File → Info → Check for Issues → Inspect Document"
   - Repair if needed: "File → Open → Browse → Select file → Open dropdown → Open and Repair"

**Prevention**: Always save Excel files properly before closing. Use `.xlsx` format.

---

### Issue: "File is not a valid Excel file (invalid signature)"

**Cause**: File signature (magic bytes) doesn't match Excel format.

**Solutions**:

1. **Verify file type**:
   ```bash
   # Linux/macOS
   file your-file.xlsx

   # Should show: Microsoft Excel 2007+
   ```

2. **Check file extension**:
   - File must actually be an Excel file, not renamed from another format
   - CSV files renamed to `.xlsx` won't work - open in Excel and save as `.xlsx`

3. **Re-save in Excel**:
   - Open the file in Microsoft Excel
   - Save As → Excel Workbook (*.xlsx)

**Prevention**: Always create/save files using Excel or compatible tools (LibreOffice, Google Sheets export).

---

## Link Extraction Issues

### Issue: "Column 'Title' not found in the first 10 rows"

**Cause**: The header row with "Title" column is beyond row 10, or the column name doesn't match.

**Solutions**:

1. **Check column name**:
   - Ensure column header is exactly "Title" (case-sensitive)
   - Remove extra spaces: "Title " or " Title" won't match
   - Check for hidden characters

2. **Move header row**:
   - Move the header row to row 1-10
   - Delete empty rows above the header

3. **Increase search range** (self-hosting only):
   ```json
   // appsettings.json
   {
     "ExcelProcessing": {
       "MaxHeaderSearchRows": 20  // Increase from 10 to 20
     }
   }
   ```

**Example Fix**:

```
Before (Won't Work):
Row 1: Company Logo
Row 2: Report Date
Row 3-11: Empty
Row 12: Title | URL    <-- Header row too far down
Row 13: Link 1 | https://example.com

After (Will Work):
Row 1: Title | URL     <-- Header row moved up
Row 2: Link 1 | https://example.com
```

**Prevention**: Keep headers in the first row, avoid metadata rows above data.

---

### Issue: "0 links found" but file has hyperlinks

**Cause**: Links are not in the expected format or column.

**Solutions**:

1. **Verify hyperlinks**:
   - Links must be Excel hyperlinks (blue, underlined, clickable)
   - Plain text URLs like "https://example.com" without hyperlink formatting won't be extracted
   - Right-click cell → "Edit Hyperlink" should show hyperlink dialog

2. **Add hyperlinks in Excel**:
   - Select cell with URL
   - Ctrl+K (Windows) or Cmd+K (Mac)
   - Paste URL in "Address" field
   - Click OK

3. **Batch add hyperlinks**:
   - Use the **Link Merger** feature
   - Create two columns: "Title" and "URL"
   - Upload to /merge to create hyperlinks

**Example**:

```
Won't Extract:
Cell A1: https://example.com    (plain text)

Will Extract:
Cell A1: Example                (Excel hyperlink to https://example.com)
         ^^^^^
         Blue and underlined
```

**Prevention**: Always use Excel's hyperlink feature (Ctrl+K), not plain text URLs.

---

### Issue: Links extracted but some URLs are missing

**Cause**: URLs exceed maximum length or are invalid.

**Solutions**:

1. **Check URL length**:
   - Maximum URL length is 2000 characters by default
   - Shorten very long URLs using a URL shortener

2. **Check URL format**:
   - URLs must start with `http://`, `https://`, or `mailto:`
   - Invalid protocols like `javascript:` or `file://` are rejected for security

3. **Increase limit** (self-hosting only):
   ```json
   // appsettings.json
   {
     "ExcelProcessing": {
       "MaxUrlLength": 4000  // Increase from 2000 to 4000
     }
   }
   ```

**Prevention**: Keep URLs under 2000 characters. Use URL shorteners for long tracking URLs.

---

## Link Merging Issues

### Issue: "Column 'Title' not found" or "Column 'URL' not found"

**Cause**: The file doesn't have both "Title" and "URL" columns.

**Solutions**:

1. **Check column names**:
   - Must have exactly "Title" and "URL" (case-sensitive)
   - Check spelling: "Titile", "URl", "Url" won't work

2. **Download template**:
   - Use "Download Sample (.xlsx)" button on /merge page
   - Copy your data into the template

3. **Rename columns**:
   - Rename columns to exactly "Title" and "URL"
   - Remove extra spaces

**Example**:

```
Won't Work:
Name     | Link
---------|----------
Google   | https://google.com

Will Work:
Title    | URL
---------|----------
Google   | https://google.com
```

**Prevention**: Use the sample template as a starting point.

---

### Issue: Links created but don't work when clicked

**Cause**: Invalid URL format or unsupported protocol.

**Solutions**:

1. **Check URL format**:
   - URLs must be complete: `https://example.com`, not `example.com`
   - Add protocol if missing (application adds `https://` automatically)

2. **Test URL**:
   - Copy URL from Excel and paste in browser
   - If it doesn't work in browser, it won't work in Excel

3. **Check for special characters**:
   - Encode special characters in URLs
   - Spaces should be `%20`
   - Use URL encoding for non-ASCII characters

**Valid URL Examples**:
- `https://example.com`
- `https://example.com/page?id=123`
- `mailto:user@example.com`
- `https://example.com/file%20name.pdf` (encoded space)

**Invalid URL Examples**:
- `example.com` (missing protocol, though app will add `https://`)
- `javascript:alert('test')` (blocked for security)
- `file:///etc/passwd` (blocked for security)

**Prevention**: Always include `https://` or `http://` in URLs.

---

## Download Issues

### Issue: Download doesn't start or file is 0 bytes

**Cause**: Browser blocking download or JavaScript error.

**Solutions**:

1. **Check browser console**:
   - Press F12 → Console tab
   - Look for errors
   - Refresh page and try again

2. **Disable browser extensions**:
   - Ad blockers may block downloads
   - Try incognito/private mode

3. **Check browser permissions**:
   - Allow downloads from sheetlink.hyunjo.uk
   - Check "Settings → Privacy → Downloads"

4. **Try different browser**:
   - Test in Chrome, Firefox, or Edge
   - Update browser to latest version

**Prevention**: Keep browser updated, whitelist sheetlink.hyunjo.uk in extensions.

---

### Issue: Downloaded file won't open in Excel

**Cause**: File corruption during download or browser issue.

**Solutions**:

1. **Re-download**:
   - Clear browser cache
   - Download again

2. **Check file size**:
   - File should be > 0 bytes
   - Compare with original upload size

3. **Try "Open and Repair"**:
   - Excel → File → Open → Browse
   - Select file → Open dropdown → "Open and Repair"

4. **Check antivirus**:
   - Antivirus may quarantine file
   - Check quarantine folder
   - Temporarily disable antivirus (carefully)

**Prevention**: Use modern, updated browser. Avoid interrupting downloads.

---

## Performance Issues

### Issue: Processing takes very long (>30 seconds)

**Cause**: Large file with many rows or complex formatting.

**Solutions**:

1. **Reduce file size**:
   - Remove unnecessary columns
   - Delete empty rows
   - Remove formatting, images, charts

2. **Split large files**:
   - Process 10,000 rows at a time
   - Merge results manually

3. **Check server load** (self-hosting):
   - Monitor CPU and memory usage
   - Restart application if needed
   - Check logs for errors

**Expected Performance**:
- 1,000 rows: ~2-5 seconds
- 10,000 rows: ~10-20 seconds
- 50,000 rows: ~30-60 seconds

**Prevention**: Keep files under 10,000 rows for best performance.

---

### Issue: "HTTP 429 - Too Many Requests"

**Cause**: Rate limit exceeded (100 requests per minute per IP).

**Solutions**:

1. **Wait 1 minute**:
   - Rate limit resets after 1 minute
   - Try again after waiting

2. **Self-host** (unlimited requests):
   - Deploy your own instance
   - Set `RateLimitPerMinute` to higher value

3. **Check for automated requests**:
   - Don't use scripts to make rapid requests
   - Process files manually

**For Self-Hosters**:

```json
// appsettings.json
{
  "ExcelProcessing": {
    "RateLimitPerMinute": 500  // Increase limit
  }
}
```

**Prevention**: Process files one at a time, avoid automation.

---

## Deployment Issues

### Issue: Application won't start - "Could not find .NET runtime"

**Cause**: .NET 10 runtime not installed.

**Solutions**:

1. **Install .NET 10 Runtime**:
   ```bash
   # Ubuntu/Debian
   wget https://dot.net/v1/dotnet-install.sh
   bash dotnet-install.sh --channel 10.0 --runtime aspnetcore

   # Or use official packages
   sudo apt-get install -y aspnetcore-runtime-10.0
   ```

2. **Verify installation**:
   ```bash
   dotnet --list-runtimes
   # Should show: Microsoft.AspNetCore.App 10.0.x
   ```

3. **Check systemd service**:
   ```bash
   sudo systemctl status excellinkextractor
   journalctl -u excellinkextractor -n 50
   ```

**Prevention**: Always install matching .NET runtime version before deployment.

---

### Issue: "Unable to bind to https://localhost:5050" or port in use

**Cause**: Port 5050 already in use or permissions issue.

**Solutions**:

1. **Check port usage**:
   ```bash
   # Linux
   sudo lsof -i :5050
   sudo netstat -tulpn | grep 5050

   # Windows
   netstat -ano | findstr :5050
   ```

2. **Kill existing process**:
   ```bash
   # Linux
   sudo kill <PID>

   # Windows
   taskkill /PID <PID> /F
   ```

3. **Change port**:
   ```bash
   # Command line
   dotnet run --urls http://localhost:5055

   # Or in Program.cs
   builder.WebHost.UseUrls("http://localhost:5055");
   ```

**Prevention**: Use unique ports, check for conflicts before starting.

---

### Issue: Application accessible locally but not remotely

**Cause**: Firewall or binding to localhost only.

**Solutions**:

1. **Bind to all interfaces**:
   ```bash
   dotnet run --urls http://0.0.0.0:5050
   ```

2. **Configure firewall**:
   ```bash
   # Ubuntu UFW
   sudo ufw allow 5050/tcp
   sudo ufw reload

   # iptables
   sudo iptables -A INPUT -p tcp --dport 5050 -j ACCEPT
   ```

3. **Use reverse proxy or tunnel**:
   - Cloudflare Tunnel (recommended)
   - Nginx reverse proxy
   - Apache reverse proxy

**Production Setup** (Cloudflare Tunnel):

```bash
# Install cloudflared
curl -L https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64.deb -o cloudflared.deb
sudo dpkg -i cloudflared.deb

# Create tunnel
cloudflared tunnel create sheetlink
cloudflared tunnel route dns sheetlink sheetlink.yourdomain.com
cloudflared tunnel run sheetlink
```

**Prevention**: Always use Cloudflare Tunnel or reverse proxy for production, never expose Kestrel directly.

---

### Issue: Changes to appsettings.json not taking effect

**Cause**: Configuration loaded at startup, or wrong environment.

**Solutions**:

1. **Restart application**:
   ```bash
   # systemd
   sudo systemctl restart excellinkextractor

   # Manual
   # Stop running process (Ctrl+C)
   dotnet run
   ```

2. **Check environment**:
   ```bash
   echo $ASPNETCORE_ENVIRONMENT
   # Should be: Production, Development, or empty
   ```

3. **Verify file location**:
   ```bash
   # Config file must be in same directory as DLL
   ls -la /var/www/ExcelLinkExtractor/
   # Should show: ExcelLinkExtractorWeb.dll, appsettings.json, appsettings.Production.json
   ```

4. **Validate JSON**:
   ```bash
   # Check for syntax errors
   cat appsettings.json | jq .
   # Should output formatted JSON (no errors)
   ```

**Prevention**: Always restart after config changes. Validate JSON before deploying.

---

## Development Issues

### Issue: Build fails with "The type or namespace could not be found"

**Cause**: Missing NuGet packages or SDK mismatch.

**Solutions**:

1. **Restore packages**:
   ```bash
   dotnet restore
   dotnet clean
   dotnet build
   ```

2. **Check .NET SDK version**:
   ```bash
   dotnet --version
   # Should be: 10.0.x
   ```

3. **Install .NET 10 SDK**:
   - Download from https://dotnet.microsoft.com/download/dotnet/10.0
   - Install and restart terminal

4. **Clear NuGet cache**:
   ```bash
   dotnet nuget locals all --clear
   dotnet restore
   ```

**Prevention**: Keep .NET SDK updated. Always run `dotnet restore` after cloning.

---

### Issue: Tests fail with "System.InvalidOperationException"

**Cause**: Missing mock setup or incorrect test configuration.

**Solutions**:

1. **Check mock setup**:
   ```csharp
   // Ensure all dependencies are mocked
   _optionsMock.Setup(x => x.Value).Returns(new ExcelProcessingOptions());
   ```

2. **Run tests individually**:
   ```bash
   # Isolate failing test
   dotnet test --filter "FullyQualifiedName~TestName"
   ```

3. **Check test output**:
   ```bash
   # Verbose output
   dotnet test --logger "console;verbosity=detailed"
   ```

**Prevention**: Always mock all dependencies. Follow existing test patterns.

---

### Issue: Hot reload not working in development

**Cause**: .NET hot reload limitations or file watching issues.

**Solutions**:

1. **Enable hot reload**:
   ```bash
   dotnet watch run
   ```

2. **Check file permissions**:
   - Ensure project directory is writable
   - Disable antivirus scanning of project folder

3. **Restart watch**:
   - Press Ctrl+R in the watch console
   - Or restart `dotnet watch`

**Limitations**:
- Hot reload doesn't work for all changes (e.g., method signatures)
- Some changes require full restart

**Prevention**: Use `dotnet watch run` for development. Expect occasional restarts.

---

## Configuration Issues

### Issue: How do I know which configuration file is being used?

**Solution**:

Check environment variable and corresponding file:

```bash
# Check environment
echo $ASPNETCORE_ENVIRONMENT

# Files loaded (in order):
# 1. appsettings.json (always)
# 2. appsettings.{Environment}.json (if Environment is set)
# 3. Environment variables (override)
```

**Examples**:
- `ASPNETCORE_ENVIRONMENT=Production` → loads `appsettings.Production.json`
- `ASPNETCORE_ENVIRONMENT=Development` → loads `appsettings.Development.json`
- No environment set → only loads `appsettings.json`

---

### Issue: How do I override configuration temporarily?

**Solutions**:

1. **Environment variables**:
   ```bash
   # Linux/macOS
   export ExcelProcessing__MaxFileSizeMB=25
   dotnet run

   # Windows (PowerShell)
   $env:ExcelProcessing__MaxFileSizeMB="25"
   dotnet run
   ```

2. **Command-line arguments**:
   ```bash
   dotnet run --ExcelProcessing:MaxFileSizeMB=25
   ```

3. **appsettings.Local.json** (development only):
   ```json
   // Create appsettings.Local.json (not in git)
   {
     "ExcelProcessing": {
       "MaxFileSizeMB": 25
     }
   }
   ```

**Prevention**: Use environment variables for temporary overrides. Don't modify production config files.

---

## Still Having Issues?

### Check Logs

**Development**:
```bash
# Console output shows all logs
dotnet run
```

**Production (systemd)**:
```bash
# View recent logs
journalctl -u excellinkextractor -n 100

# Follow logs in real-time
journalctl -u excellinkextractor -f

# Logs since last hour
journalctl -u excellinkextractor --since "1 hour ago"
```

### Enable Verbose Logging

```json
// appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",  // Change from Information/Warning
      "ExcelLinkExtractorWeb.Services": "Trace"  // Detailed service logs
    }
  }
}
```

### Report a Bug

If none of these solutions work:

1. **Check existing issues**: https://github.com/HyunjoJung/sheetlink/issues
2. **Create new issue**: Include:
   - Detailed description
   - Steps to reproduce
   - Error messages / screenshots
   - Environment (OS, browser, .NET version)
   - Configuration (without secrets)

### Get Help

- **GitHub Issues**: https://github.com/HyunjoJung/sheetlink/issues
- **GitHub Discussions**: https://github.com/HyunjoJung/sheetlink/discussions

## Related Documentation

- [Configuration Guide](CONFIGURATION.md) - Configuration options
- [Security Policy](SECURITY.md) - Security guidelines
- [Architecture](ARCHITECTURE.md) - System design
- [Contributing](CONTRIBUTING.md) - Development setup

---

**Last Updated**: 2025-01-29
