using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace ExcelLinkExtractorWeb.E2ETests;

[TestFixture]
public class MergeLinksPageTests : PageTest
{
    private const string BaseUrl = "http://localhost:5050";

    [Test]
    public async Task MergePage_ShouldLoadSuccessfully()
    {
        await Page.GotoAsync($"{BaseUrl}/merge");

        // Check page title
        await Expect(Page).ToHaveTitleAsync(new Regex("SheetLink"));

        // Check main heading
        var heading = Page.Locator("h1");
        await Expect(heading).ToContainTextAsync("Merge Title");
    }

    [Test]
    public async Task MergePage_ShouldShowDescription()
    {
        await Page.GotoAsync($"{BaseUrl}/merge");

        // Check description text
        var description = Page.Locator("text=Upload a spreadsheet file");
        await Expect(description).ToBeVisibleAsync();
    }

    [Test]
    public async Task DownloadMergeTemplateButton_ShouldBeVisible()
    {
        await Page.GotoAsync($"{BaseUrl}/merge");

        // Wait for interactive mode
        await Task.Delay(2000);

        // Check download template button
        var downloadButton = Page.Locator("button:has-text('Download Merge Sample')");
        await Expect(downloadButton).ToBeVisibleAsync();
    }

    [Test]
    public async Task NavigationBetweenPages_ShouldWork()
    {
        // Start on home page
        await Page.GotoAsync(BaseUrl);
        await Expect(Page.Locator("h1")).ToContainTextAsync("Extract");

        // Navigate to Merge page
        await Page.Locator("text=Merge Links").ClickAsync();
        await Task.Delay(1000);

        // Check we're on merge page
        await Expect(Page.Locator("h1")).ToContainTextAsync("Merge");

        // Navigate back to Extract page
        await Page.Locator("text=Extract Links").ClickAsync();
        await Task.Delay(1000);

        // Check we're back on extract page
        await Expect(Page.Locator("h1")).ToContainTextAsync("Extract");
    }

    [Test]
    public async Task MergePage_ShouldShowFileInput()
    {
        await Page.GotoAsync($"{BaseUrl}/merge");

        // Wait for interactive mode
        await Task.Delay(2000);

        // Check file input exists
        var fileInput = Page.Locator("input[type='file']");
        await Expect(fileInput).ToBeVisibleAsync();
    }

    [Test]
    public async Task MergePage_ShouldShowMergeButton()
    {
        await Page.GotoAsync($"{BaseUrl}/merge");

        // Wait for interactive mode
        await Task.Delay(2000);

        // Check merge button
        var mergeButton = Page.Locator("button:has-text('Merge into Hyperlinks')");
        await Expect(mergeButton).ToBeVisibleAsync();
    }

    [Test]
    public async Task MergePage_ErrorMessage_ShouldShowForInvalidFile()
    {
        await Page.GotoAsync($"{BaseUrl}/merge");

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
            var mergeButton = Page.Locator("button:has-text('Merge into Hyperlinks')");
            await mergeButton.ClickAsync();

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
}
