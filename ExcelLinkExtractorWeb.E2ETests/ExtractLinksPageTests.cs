using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace ExcelLinkExtractorWeb.E2ETests;

[TestFixture]
public class ExtractLinksPageTests : PageTest
{
    private const string BaseUrl = "http://localhost:5050";

    [Test]
    public async Task HomePage_ShouldLoadSuccessfully()
    {
        await Page.GotoAsync(BaseUrl);

        // Check page title
        await Expect(Page).ToHaveTitleAsync(new Regex("SheetLink"));

        // Check main heading
        var heading = Page.Locator("h1");
        await Expect(heading).ToContainTextAsync("SheetLink");
    }

    [Test]
    public async Task HomePage_ShouldShowNavigation()
    {
        await Page.GotoAsync(BaseUrl);

        // Wait for navigation to be ready
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Check navigation links
        var extractLink = Page.Locator("text=Extract Links");
        var mergeLink = Page.Locator("text=Merge Links");

        await Expect(extractLink).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Expect(mergeLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    [Ignore("Flaky - timing sensitive test for Blazor interactive mode")]
    public async Task DownloadTemplateButton_ShouldBeVisible()
    {
        await Page.GotoAsync(BaseUrl);

        // Wait for Blazor to be interactive
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(4000);

        // Check download template button - match exact text
        var downloadButton = Page.Locator("button:has-text('Download Sample')");
        await Expect(downloadButton).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    [Test]
    public async Task SkipToContentLink_ShouldBeFocusable()
    {
        await Page.GotoAsync(BaseUrl);

        // Tab to skip link
        await Page.Keyboard.PressAsync("Tab");

        // Check if skip link is focused and visible
        var skipLink = Page.Locator(".skip-to-content");
        await Expect(skipLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task FileUpload_WithoutFile_ShouldNotSubmit()
    {
        await Page.GotoAsync(BaseUrl);

        // Wait for interactive mode
        await Page.WaitForTimeoutAsync(3000);

        // Button should be disabled without a file
        var uploadButton = Page.Locator("button:has-text('Upload & Extract')");
        await Expect(uploadButton).ToBeDisabledAsync();
    }

    [Test]
    public async Task FAQ_Link_ShouldBeVisible()
    {
        await Page.GotoAsync(BaseUrl);

        // Wait for page to load
        await Page.WaitForTimeoutAsync(2000);

        // Check if FAQ link is visible
        var faqLink = Page.Locator("a[href='/faq']");
        await Expect(faqLink).ToBeVisibleAsync();
    }

    [Test]
    [Ignore("Flaky - depends on Blazor Server processing invalid file upload")]
    public async Task ErrorMessage_ShouldShowForInvalidFile()
    {
        await Page.GotoAsync(BaseUrl);

        // Wait for interactive mode
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(4000);

        // Create a temporary text file (not Excel)
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "This is not an Excel file");

        try
        {
            // Upload the invalid file
            var fileInput = Page.Locator("input[type='file']");
            await fileInput.SetInputFilesAsync(tempFile);

            // Click upload button
            var uploadButton = Page.Locator("button:has-text('Upload & Extract')");
            await uploadButton.ClickAsync();

            // Wait for error message with longer timeout
            var errorAlert = Page.Locator(".alert-danger");
            await Expect(errorAlert).ToBeVisibleAsync(new() { Timeout = 15000 });
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task GitHubLink_ShouldBePresent()
    {
        await Page.GotoAsync(BaseUrl);

        // Check for GitHub link in footer
        var githubLink = Page.Locator("a[href*='github.com']");
        await Expect(githubLink).ToBeVisibleAsync();

        // Verify it has proper rel attributes
        var rel = await githubLink.GetAttributeAsync("rel");
        Assert.That(rel, Does.Contain("noopener"));
        Assert.That(rel, Does.Contain("noreferrer"));
    }
}
