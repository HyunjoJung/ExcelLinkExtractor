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
    public async Task DownloadTemplateButton_ShouldBeVisible()
    {
        await Page.GotoAsync(BaseUrl);

        // Wait for Blazor to be interactive by waiting for skeleton to disappear
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for skeleton to be replaced by actual button
        var downloadButton = Page.Locator("button:has-text('Download Sample')");

        // Wait for button to appear (skeleton will be replaced)
        await downloadButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 20000 });
        await Expect(downloadButton).ToBeVisibleAsync();
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
    public async Task ErrorMessage_ShouldShowForInvalidFile()
    {
        await Page.GotoAsync(BaseUrl);

        // Wait for interactive mode - wait for actual button to appear
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var uploadButton = Page.Locator("button:has-text('Upload & Extract')");
        await uploadButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 20000 });

        // Create a temporary text file (not Excel)
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "This is not an Excel file");

        try
        {
            // Upload the invalid file
            var fileInput = Page.Locator("input[type='file']");
            await fileInput.SetInputFilesAsync(tempFile);

            // Click upload button
            await uploadButton.ClickAsync();

            // Wait for error message
            var errorAlert = Page.Locator(".alert-danger");
            await errorAlert.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 20000 });
            await Expect(errorAlert).ToBeVisibleAsync();
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
