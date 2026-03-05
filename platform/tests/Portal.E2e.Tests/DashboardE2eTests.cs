using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using Portal.E2e.Tests.Pages;

namespace Portal.E2e.Tests;

/// <summary>
/// E2E tests for the portal dashboard page.
/// Prerequisites: portal SPA must be built before running.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class DashboardE2eTests : PortalE2eTestBase
{
    [SetUp]
    public void RequireSpa()
    {
        Assume.That(GlobalSetup.SpaDistPath, Is.Not.Null,
            "Portal SPA dist/ not found. Run 'npm run build' in portal/ before E2E tests.");

    }

    private async Task LoginAsync()
    {
        var loginPage = new LoginPage(Page);
        await loginPage.GoToAsync();
        await loginPage.LoginAsync(E2eSeeder.UserEmail, E2eSeeder.UserPassword);
        await Page.WaitForURLAsync(u => !u.Contains("/login"), new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Dashboard_AfterLogin_IsAccessible()
    {
        await LoginAsync();

        var dashboard = new DashboardPage(Page);
        await dashboard.WaitForLoadAsync();

        Page.Url.Should().NotContain("/login");
    }

    [Test]
    public async Task Dashboard_NavigatesToDocuments()
    {
        await LoginAsync();

        // Click on the Documents link in the sidebar
        var docsLink = Page.Locator("a[href*='/documents'], [data-testid='nav-documents']");
        await docsLink.ClickAsync();

        await Page.WaitForURLAsync(u => u.Contains("/documents"), new() { Timeout = 10_000 });
        Page.Url.Should().Contain("/documents");
    }

    [Test]
    public async Task Dashboard_NavigatesToSettings()
    {
        await LoginAsync();

        var settingsLink = Page.Locator("a[href*='/settings'], [data-testid='nav-settings']");
        await settingsLink.ClickAsync();

        await Page.WaitForURLAsync(u => u.Contains("/settings"), new() { Timeout = 10_000 });
        Page.Url.Should().Contain("/settings");
    }

    [Test]
    public async Task Dashboard_ShowsUsageStats()
    {
        await LoginAsync();

        var dashboard = new DashboardPage(Page);
        await dashboard.WaitForLoadAsync();

        // Dashboard stat cards have class p-5 shadow-sm (bg-white rounded-lg border)
        var statsArea = Page.Locator(
            "[data-testid='usage-card'], .p-5.shadow-sm, .stat-card");
        await Expect(statsArea.First).ToBeVisibleAsync(new() { Timeout = 10_000 });
    }

    [Test]
    public async Task Logout_RedirectsToLogin()
    {
        await LoginAsync();

        // Find and click logout (Layout uses "Sign Out" text)
        var logoutBtn = Page.Locator(
            "[data-testid='logout-button'], button:has-text('Sign Out'), button:has-text('Logout'), button:has-text('Sign out')");
        await logoutBtn.ClickAsync();

        await Page.WaitForURLAsync(u => u.Contains("/login"), new() { Timeout = 10_000 });
        Page.Url.Should().Contain("/login");
    }
}
