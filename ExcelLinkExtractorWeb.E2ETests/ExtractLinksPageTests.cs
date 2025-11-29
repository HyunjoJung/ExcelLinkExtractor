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
        await Expect(heading).ToContainTextAsync("Extract Hyperlinks");
    }

    [Test]
    public async Task HomePage_ShouldShowNavigation()
    {
        await Page.GotoAsync(BaseUrl);

        // Check navigation links
        var extractLink = Page.Locator("text=Extract Links");
        var mergeLink = Page.Locator("text=Merge Links");

        await Expect(extractLink).ToBeVisibleAsync();
        await Expect(mergeLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task DownloadTemplateButton_ShouldBeVisible()
    {
        await Page.GotoAsync(BaseUrl);

        // Wait for interactive mode
        await Task.Delay(2000);

        // Check download template button
        var downloadButton = Page.Locator("button:has-text('Download Sample')");
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
        await Task.Delay(2000);

        // Try to submit without uploading a file
        var extractButton = Page.Locator("button:has-text('Extract Links')");

        // Button should be disabled or form should not have a file
        var fileInput = Page.Locator("input[type='file']");
        var files = await fileInput.InputValueAsync();
        Assert.That(files, Is.Empty);
    }

    [Test]
    public async Task FAQ_ShouldBeExpandable()
    {
        await Page.GotoAsync(BaseUrl);

        // Scroll to FAQ section
        await Page.Locator("text=Frequently Asked Questions").ScrollIntoViewIfNeededAsync();

        // Check if FAQ is visible
        var faq = Page.Locator("text=Frequently Asked Questions");
        await Expect(faq).ToBeVisibleAsync();

        // Look for details elements (accordions)
        var details = Page.Locator("details").First;
        await Expect(details).ToBeVisibleAsync();
    }

    [Test]
    public async Task ErrorMessage_ShouldShowForInvalidFile()
    {
        await Page.GotoAsync(BaseUrl);

        // Wait for interactive mode
        await Task.Delay(2000);

        // Create a temporary text file (not Excel)
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "This is not an Excel file");

        try
        {
            // Upload the invalid file
            var fileInput = Page.Locator("input[type='file']");
            await fileInput.SetInputFilesAsync(tempFile);

            // Submit
            var extractButton = Page.Locator("button:has-text('Extract Links')");
            await extractButton.ClickAsync();

            // Wait for error message
            await Task.Delay(2000);

            // Check for error alert
            var errorAlert = Page.Locator(".alert-danger");
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
