using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace ExcelLinkExtractorWeb.E2ETests;

/// <summary>
/// Shared helpers to make UI waits consistent and reduce flakiness when tests run together.
/// </summary>
public abstract class SheetLinkPageTest : PageTest
{
    private static Process? _serverProcess;
    private static string? _baseUrl;

    protected static string BaseUrl => _baseUrl ?? "http://localhost:5050";

    [OneTimeSetUp]
    public async Task StartServerIfNeeded()
    {
        if (_serverProcess != null)
        {
            return;
        }

        var port = GetFreeTcpPort();
        _baseUrl = $"http://localhost:{port}";

        var webProjectPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "ExcelLinkExtractorWeb"));

        var startInfo = new ProcessStartInfo
        {
            FileName = "/home/dev/.dotnet/dotnet",
            Arguments = $"run --project \"{webProjectPath}\" --urls {_baseUrl}",
            WorkingDirectory = webProjectPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.Environment["DOTNET_ROOT"] = "/home/dev/.dotnet";
        startInfo.Environment["ExcelProcessing__RateLimitPerMinute"] = "10000";

        _serverProcess = Process.Start(startInfo);
        if (_serverProcess == null)
        {
            throw new InvalidOperationException("Failed to start SheetLink server for E2E tests.");
        }

        // Wait for the server to respond
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var started = false;
        for (var i = 0; i < 120 && !started; i++)
        {
            try
            {
                var response = await client.GetAsync(BaseUrl);
                if ((int)response.StatusCode < 500)
                {
                    var html = await response.Content.ReadAsStringAsync();
                    started = html.Length > 0 || response.IsSuccessStatusCode;
                    if (started) break;
                }
            }
            catch
            {
                // ignore and retry
            }
            await Task.Delay(1000);
        }

        if (!started)
        {
            StopServer();
            throw new InvalidOperationException("SheetLink server did not become ready for E2E tests.");
        }
    }

    [OneTimeTearDown]
    public void StopServer()
    {
        if (_serverProcess == null)
        {
            return;
        }

        try
        {
            if (!_serverProcess.HasExited)
            {
                _serverProcess.Kill(entireProcessTree: true);
                _serverProcess.WaitForExit(TimeSpan.FromSeconds(10));
            }
        }
        finally
        {
            _serverProcess.Dispose();
            _serverProcess = null;
        }
    }

    [SetUp]
    public void SetUpDefaults()
    {
        // Give Blazor time to hydrate and replace skeleton placeholders
        Page.SetDefaultTimeout(30000);
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    protected async Task WaitForHomeInteractiveAsync()
    {
        if (!Page.Url.StartsWith(BaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            await Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });
        }

        var response = await Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });
        if (response != null && response.Status >= 400)
        {
            throw new InvalidOperationException($"Home page returned status code {response.Status}");
        }

        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var heading = Page.Locator("h1");
        await Expect(heading).ToBeVisibleAsync(new() { Timeout = 30000 });
        await Expect(heading).ToContainTextAsync("SheetLink", new() { Timeout = 30000 });

        // Home page replaces skeleton buttons once interactive; wait for the download button
        var downloadButton = Page.Locator("button:has-text('Download Sample')");
        await downloadButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
    }

    protected async Task WaitForMergeInteractiveAsync()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.Locator("h1")).ToContainTextAsync("Link Merger", new() { Timeout = 10000 });

        // Merge page becomes interactive when the template/download controls render
        var mergeFileInput = Page.Locator("#mergeFileInput");
        var mergeButton = Page.Locator("button:has-text('Upload & Merge')");
        await mergeFileInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
        await mergeButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
    }
}
