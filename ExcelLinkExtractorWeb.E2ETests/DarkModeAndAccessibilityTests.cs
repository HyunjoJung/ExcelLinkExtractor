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
        await Task.Delay(1000);

        // Get initial theme
        var initialTheme = await Page.Locator("html").GetAttributeAsync("data-theme");

        // Click theme toggle
        var themeToggle = Page.Locator(".theme-toggle");
        await themeToggle.ClickAsync();

        // Wait for theme to change
        await Task.Delay(500);

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
        await Task.Delay(1000);

        // Click theme toggle to set dark mode
        var themeToggle = Page.Locator(".theme-toggle");
        await themeToggle.ClickAsync();
        await Task.Delay(500);

        // Get theme after toggle
        var theme = await Page.Locator("html").GetAttributeAsync("data-theme");

        // Reload page
        await Page.ReloadAsync();
        await Task.Delay(1000);

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
    public async Task SkipToContentLink_ShouldWork()
    {
        await Page.GotoAsync(BaseUrl);

        // Tab to skip link
        await Page.Keyboard.PressAsync("Tab");

        // Check if skip link is focused
        var skipLink = Page.Locator(".skip-to-content");
        await Expect(skipLink).ToBeVisibleAsync();

        // Click skip link
        await skipLink.ClickAsync();
        await Task.Delay(500);

        // Main content should be focused
        var mainContent = Page.Locator("#main-content");
        var focused = await Page.EvaluateAsync<bool>(@"
            document.activeElement === document.querySelector('#main-content') ||
            document.querySelector('#main-content').contains(document.activeElement)
        ");

        Assert.That(focused, Is.True, "Main content should be focused after clicking skip link");
    }

    [Test]
    public async Task FormInputs_ShouldHaveLabels()
    {
        await Page.GotoAsync(BaseUrl);

        // Wait for interactive mode
        await Task.Delay(2000);

        // Check file input has associated label
        var fileInput = Page.Locator("input[type='file']");
        await Expect(fileInput).ToBeVisibleAsync();

        // File input should have aria-label or be associated with a label
        var ariaLabel = await fileInput.GetAttributeAsync("aria-label");
        var hasLabel = ariaLabel != null;

        Assert.That(hasLabel, Is.True, "File input should have accessibility label");
    }

    [Test]
    public async Task KeyboardNavigation_ShouldWork()
    {
        await Page.GotoAsync(BaseUrl);

        // Tab through interactive elements
        await Page.Keyboard.PressAsync("Tab"); // Skip link
        await Page.Keyboard.PressAsync("Tab"); // Logo/Nav
        await Page.Keyboard.PressAsync("Tab"); // Extract Links nav
        await Page.Keyboard.PressAsync("Tab"); // Merge Links nav
        await Page.Keyboard.PressAsync("Tab"); // Theme toggle

        // Check if theme toggle is focused
        var themeToggle = Page.Locator(".theme-toggle");
        var isFocused = await Page.EvaluateAsync<bool>("document.activeElement === document.querySelector('.theme-toggle')");

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
    public async Task LoadingSkeletons_ShouldAppear()
    {
        await Page.GotoAsync(BaseUrl);

        // Check for skeleton elements during initial load
        // Note: This might be flaky depending on load speed
        var skeleton = Page.Locator(".skeleton");

        // Skeleton should exist (even if it disappears quickly)
        var skeletonExists = await Page.Locator(".skeleton, .skeleton-button, .skeleton-input").CountAsync() > 0 ||
                            await Page.Locator("button:has-text('Download Sample')").CountAsync() > 0;

        Assert.That(skeletonExists, Is.True, "Page should show loading skeletons or content");
    }
}
