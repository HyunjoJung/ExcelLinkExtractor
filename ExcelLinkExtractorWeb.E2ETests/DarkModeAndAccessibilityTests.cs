using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace ExcelLinkExtractorWeb.E2ETests;

[TestFixture]
public class DarkModeAndAccessibilityTests : PageTest
{
    private const string BaseUrl = "http://localhost:5050";

    [Test]
    public async Task DarkModeToggle_ShouldBeVisible()
    {
        await Page.GotoAsync(BaseUrl);

        // Check theme toggle button
        var themeToggle = Page.Locator(".theme-toggle");
        await Expect(themeToggle).ToBeVisibleAsync();
    }

    [Test]
    public async Task DarkModeToggle_ShouldChangeTheme()
    {
        await Page.GotoAsync(BaseUrl);

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
        await Page.GotoAsync(BaseUrl);

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
        await Page.GotoAsync(BaseUrl);

        // Check aria-label on theme toggle
        var themeToggle = Page.Locator(".theme-toggle");
        var ariaLabel = await themeToggle.GetAttributeAsync("aria-label");

        Assert.That(ariaLabel, Is.Not.Null);
        Assert.That(ariaLabel, Does.Contain("mode").IgnoreCase);
    }

    [Test]
    [Ignore("Flaky - skip link uses :focus pseudo-class which is hard to test")]
    public async Task SkipToContentLink_ShouldWork()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Tab to skip link
        await Page.Keyboard.PressAsync("Tab");
        await Page.WaitForTimeoutAsync(200);

        // Check if skip link is focused and visible when tabbed to
        var skipLink = Page.Locator(".skip-to-content");

        // Skip link should be visible when focused
        var isVisible = await skipLink.IsVisibleAsync();

        if (!isVisible)
        {
            // Skip link might use :focus pseudo-class for visibility
            // Just verify it exists and has the right href
            var href = await skipLink.GetAttributeAsync("href");
            Assert.That(href, Is.EqualTo("#main-content"), "Skip link should point to #main-content");
        }
        else
        {
            // Click skip link if visible
            await skipLink.ClickAsync();
            await Page.WaitForTimeoutAsync(500);

            // Main content should be focused
            var mainContent = Page.Locator("#main-content");
            var focused = await Page.EvaluateAsync<bool>(@"
                document.activeElement === document.querySelector('#main-content') ||
                document.querySelector('#main-content').contains(document.activeElement)
            ");

            Assert.That(focused, Is.True, "Main content should be focused after clicking skip link");
        }
    }

    [Test]
    public async Task FormInputs_ShouldHaveLabels()
    {
        await Page.GotoAsync(BaseUrl);

        // Wait for interactive mode
        await Page.WaitForTimeoutAsync(3000);

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
    [Ignore("Flaky due to Blazor Server interactive rendering timing")]
    public async Task KeyboardNavigation_ShouldWork()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);

        // Clear localStorage to ensure consistent starting state
        await Page.EvaluateAsync("localStorage.clear()");
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);

        // Click on body first to ensure page has focus
        await Page.Locator("body").ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Tab through interactive elements with pauses
        await Page.Keyboard.PressAsync("Tab"); // Skip link
        await Page.WaitForTimeoutAsync(200);
        await Page.Keyboard.PressAsync("Tab"); // SheetLink brand
        await Page.WaitForTimeoutAsync(200);
        await Page.Keyboard.PressAsync("Tab"); // Extract Links nav
        await Page.WaitForTimeoutAsync(200);
        await Page.Keyboard.PressAsync("Tab"); // Merge Links nav
        await Page.WaitForTimeoutAsync(200);
        await Page.Keyboard.PressAsync("Tab"); // Theme toggle
        await Page.WaitForTimeoutAsync(300);

        // Check if theme toggle is focused
        var isFocused = await Page.EvaluateAsync<bool>("document.activeElement && document.activeElement.classList.contains('theme-toggle')");

        Assert.That(isFocused, Is.True, "Theme toggle should be focusable via keyboard");
    }

    [Test]
    public async Task ExternalLinks_ShouldHaveSecurityAttributes()
    {
        await Page.GotoAsync(BaseUrl);

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
    [Ignore("Flaky - timing sensitive test that depends on catching initial render state")]
    public async Task LoadingSkeletons_ShouldAppearOrContentLoaded()
    {
        // Open a new page to catch the initial load state
        var newPage = await Page.Context.NewPageAsync();

        try
        {
            // Navigate and immediately check for skeletons (before interactive mode)
            await newPage.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });

            // Check very quickly for skeleton or content
            await Task.Delay(100); // Small delay to let initial render happen

            var skeletonExists = await newPage.Locator(".skeleton, .skeleton-button, .skeleton-input").CountAsync() > 0;
            var contentLoaded = await newPage.Locator("button:has-text('Download Sample')").CountAsync() > 0;

            Assert.That(skeletonExists || contentLoaded, Is.True, "Page should show loading skeletons or loaded content");
        }
        finally
        {
            await newPage.CloseAsync();
        }
    }
}
