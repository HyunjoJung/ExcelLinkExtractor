using Microsoft.Playwright;
using NUnit.Framework;

namespace ExcelLinkExtractorWeb.E2ETests;

[TestFixture]
public class DarkModeAndAccessibilityTests : SheetLinkPageTest
{
    [Test]
    public async Task DarkModeToggle_ShouldBeVisible()
    {
        await Page.AddInitScriptAsync("if(!localStorage.getItem('sheetlink-theme')) localStorage.setItem('sheetlink-theme','light');");
        await Page.GotoAsync(BaseUrl);
        await WaitForHomeInteractiveAsync();

        // Check theme toggle button
        var themeToggle = Page.Locator(".theme-toggle");
        await Expect(themeToggle).ToBeVisibleAsync();
    }

    [Test]
    public async Task DarkModeToggle_ShouldChangeTheme()
    {
        await Page.AddInitScriptAsync("if(!localStorage.getItem('sheetlink-theme')) localStorage.setItem('sheetlink-theme','light');");
        await Page.GotoAsync(BaseUrl);
        await WaitForHomeInteractiveAsync();

        // Wait for page to load
        await Page.WaitForTimeoutAsync(1000);

        // Get initial theme
        var initialTheme = await Page.Locator("html").GetAttributeAsync("data-theme");

        // Click theme toggle
        var themeToggle = Page.Locator(".theme-toggle");
        await themeToggle.ClickAsync();

        // Wait for theme to change
        await Page.WaitForTimeoutAsync(500);

        // Get new theme
        var newTheme = await Page.Locator("html").GetAttributeAsync("data-theme");

        // Theme should have changed
        Assert.That(newTheme, Is.Not.EqualTo(initialTheme));
    }

    [Test]
    public async Task DarkModeToggle_ShouldPersistTheme()
    {
        await Page.AddInitScriptAsync("if(!localStorage.getItem('sheetlink-theme')) localStorage.setItem('sheetlink-theme','light');");
        await Page.GotoAsync(BaseUrl);
        await WaitForHomeInteractiveAsync();

        // Wait for page to load
        await Page.WaitForTimeoutAsync(1000);

        // Click theme toggle to set dark mode
        var themeToggle = Page.Locator(".theme-toggle");
        await themeToggle.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Get theme after toggle
        var theme = await Page.Locator("html").GetAttributeAsync("data-theme");

        // Reload page
        await Page.ReloadAsync();
        await Page.WaitForTimeoutAsync(1000);

        // Theme should persist
        var persistedTheme = await Page.Locator("html").GetAttributeAsync("data-theme");
        Assert.That(persistedTheme, Is.EqualTo(theme));
    }

    [Test]
    public async Task DarkModeToggle_ShouldHaveAccessibleLabel()
    {
        await Page.AddInitScriptAsync("if(!localStorage.getItem('sheetlink-theme')) localStorage.setItem('sheetlink-theme','light');");
        await Page.GotoAsync(BaseUrl);
        await WaitForHomeInteractiveAsync();

        // Check aria-label on theme toggle
        var themeToggle = Page.Locator(".theme-toggle");
        var ariaLabel = await themeToggle.GetAttributeAsync("aria-label");

        Assert.That(ariaLabel, Is.Not.Null);
        Assert.That(ariaLabel, Does.Contain("mode").IgnoreCase);
    }

    [Test]
    public async Task DarkModeToggle_ShouldShowCorrectIcon()
    {
        await Page.AddInitScriptAsync("if(!localStorage.getItem('sheetlink-theme')) localStorage.setItem('sheetlink-theme','light');");
        await Page.GotoAsync(BaseUrl);
        await WaitForHomeInteractiveAsync();

        var themeToggle = Page.Locator(".theme-toggle");

        // In light mode the icon should be moon
        await Expect(themeToggle).ToHaveTextAsync("🌙");

        await themeToggle.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // After toggling to dark mode the icon should be sun
        await Expect(themeToggle).ToHaveTextAsync("☀️");
    }

    [Test]
    public async Task SkipToContentLink_ShouldWork()
    {
        await Page.GotoAsync(BaseUrl);
        await WaitForHomeInteractiveAsync();

        // Wait a bit for page to fully render
        await Page.WaitForTimeoutAsync(1000);

        // Tab to the skip link to make it visible and active
        await Page.Keyboard.PressAsync("Tab");

        // Check if skip link exists and has correct href (with longer timeout)
        var skipLink = Page.Locator(".skip-to-content");

        // Verify the skip link is now visible (with 10s timeout instead of 5s)
        await Expect(skipLink).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Verify it has the right href
        var href = await skipLink.GetAttributeAsync("href");
        Assert.That(href, Is.EqualTo("#main-content"), "Skip link should point to #main-content");

        // Verify main content exists and has correct id
        var mainContent = Page.Locator("#main-content");
        await Expect(mainContent).ToBeAttachedAsync();
    }

    [Test]
    public async Task FormInputs_ShouldHaveLabels()
    {
        await Page.GotoAsync(BaseUrl);
        await WaitForHomeInteractiveAsync();

        // Check file input has associated label
        var fileInput = Page.Locator("input#fileInput");
        await Expect(fileInput).ToBeVisibleAsync();

        // Check for associated label element
        var label = Page.Locator("label[for='fileInput']");
        await Expect(label).ToBeAttachedAsync();

        // Label should exist (even if visually hidden)
        var labelText = await label.TextContentAsync();
        Assert.That(labelText, Is.Not.Null.Or.Empty);
    }

    [Test]
    public async Task KeyboardNavigation_ShouldWork()
    {
        await Page.GotoAsync(BaseUrl);
        await WaitForHomeInteractiveAsync();

        // Verify theme toggle is focusable by checking it can receive focus
        var themeToggle = Page.Locator(".theme-toggle");
        await themeToggle.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        // Focus the theme toggle directly
        await themeToggle.FocusAsync();

        // Verify it's focused
        var isFocused = await Page.EvaluateAsync<bool>("document.activeElement && document.activeElement.classList.contains('theme-toggle')");

        Assert.That(isFocused, Is.True, "Theme toggle should be focusable");
    }

    [Test]
    public async Task ExternalLinks_ShouldHaveSecurityAttributes()
    {
        await Page.GotoAsync(BaseUrl);
        await WaitForHomeInteractiveAsync();

        // Check all external links
        var externalLinks = Page.Locator("a[target='_blank']");
        var count = await externalLinks.CountAsync();

        Assert.That(count, Is.GreaterThan(0), "Should have at least one external link");

        // Check each external link has proper security attributes
        for (int i = 0; i < count; i++)
        {
            var link = externalLinks.Nth(i);
            var rel = await link.GetAttributeAsync("rel");

            Assert.That(rel, Is.Not.Null, $"External link {i} should have rel attribute");
            Assert.That(rel, Does.Contain("noopener"), $"External link {i} should have noopener");
            Assert.That(rel, Does.Contain("noreferrer"), $"External link {i} should have noreferrer");
        }
    }

    [Test]
    public async Task HeadingHierarchy_ShouldBeCorrect()
    {
        await Page.GotoAsync(BaseUrl);
        await WaitForHomeInteractiveAsync();

        // Check for h1
        var h1 = Page.Locator("h1").First;
        await Expect(h1).ToBeVisibleAsync();

        // Should only have one h1
        var h1Count = await Page.Locator("h1").CountAsync();
        Assert.That(h1Count, Is.EqualTo(1), "Page should have exactly one h1");
    }

    [Test]
    public async Task ImagesAndIcons_ShouldHaveAltText()
    {
        await Page.GotoAsync(BaseUrl);
        await WaitForHomeInteractiveAsync();

        // Check all img elements have alt attribute
        var images = Page.Locator("img");
        var imageCount = await images.CountAsync();

        for (int i = 0; i < imageCount; i++)
        {
            var image = images.Nth(i);
            var alt = await image.GetAttributeAsync("alt");

            Assert.That(alt, Is.Not.Null, $"Image {i} should have alt attribute");
        }
    }

    [Test]
    public async Task LoadingSkeletons_OrContent_ShouldEventuallyAppear()
    {
        await Page.GotoAsync(BaseUrl);
        await WaitForHomeInteractiveAsync();

        // Eventually, either skeleton or actual content should be visible
        // We'll wait for the download button which replaces the skeleton
        var downloadButton = Page.Locator("button:has-text('Download Sample')");
        await downloadButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 20000 });

        // At this point, content has loaded (skeleton is gone)
        await Expect(downloadButton).ToBeVisibleAsync();
    }
}
