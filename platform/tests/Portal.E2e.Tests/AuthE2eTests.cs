using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using Portal.E2e.Tests.Pages;

namespace Portal.E2e.Tests;

/// <summary>
/// E2E tests for the portal authentication flows.
/// Prerequisites: portal SPA must be built (npm run build in portal/) before running.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class AuthE2eTests : PortalE2eTestBase
{
    [SetUp]
    public void RequireSpa()
    {
        // Skip all tests in this class if the SPA hasn't been built yet
        Assume.That(GlobalSetup.SpaDistPath, Is.Not.Null,
            "Portal SPA dist/ not found. Run 'npm run build' in portal/ before E2E tests.");
    }

    [Test]
    public async Task Login_WithValidCredentials_RedirectsToDashboard()
    {
        var loginPage = new LoginPage(Page);
        await loginPage.GoToAsync();

        await loginPage.LoginAsync(E2eSeeder.UserEmail, E2eSeeder.UserPassword);

        // After successful login, should land on dashboard
        await Page.WaitForURLAsync(u => !u.Contains("/login"), new() { Timeout = 15_000 });
        Page.Url.Should().NotContain("/login");
    }

    [Test]
    public async Task Login_WithWrongPassword_ShowsErrorMessage()
    {
        var loginPage = new LoginPage(Page);
        await loginPage.GoToAsync();

        await loginPage.LoginAsync(E2eSeeder.UserEmail, "wrong-password-123");

        // Should remain on login page with an error
        await Expect(loginPage.ErrorMessage).ToBeVisibleAsync(new() { Timeout = 10_000 });
        (await loginPage.IsOnLoginPageAsync()).Should().BeTrue();
    }

    [Test]
    public async Task Login_WithEmptyFields_DoesNotSubmit()
    {
        var loginPage = new LoginPage(Page);
        await loginPage.GoToAsync();

        // Click submit without filling in fields
        await loginPage.SubmitAsync();

        // Should remain on login page (HTML5 validation or API error)
        (await loginPage.IsOnLoginPageAsync()).Should().BeTrue();
    }

    [Test]
    public async Task ProtectedRoute_WhenUnauthenticated_RedirectsToLogin()
    {
        // Try to navigate directly to dashboard without logging in
        await Page.GotoAsync(GlobalSetup.BaseUrl + "/dashboard");

        // Should be redirected to login
        await Page.WaitForURLAsync(u => u.Contains("/login"), new() { Timeout = 10_000 });
        Page.Url.Should().Contain("/login");
    }

    [Test]
    public async Task ForgotPassword_PageLoads()
    {
        await Page.GotoAsync(GlobalSetup.BaseUrl + "/forgot-password");

        // Should render the forgot password form
        await Expect(Page.Locator("input[type='email'], input[name='email']"))
            .ToBeVisibleAsync(new() { Timeout = 10_000 });
    }

    [Test]
    public async Task ForgotPassword_WithValidEmail_ShowsSuccessMessage()
    {
        await Page.GotoAsync(GlobalSetup.BaseUrl + "/forgot-password");

        await Page.FillAsync("input[type='email'], input[name='email']", E2eSeeder.UserEmail);
        await Page.ClickAsync("button[type='submit']");

        // Should show success/confirmation message (ForgotPasswordPage shows green bg-green-50 div)
        var successLocator = Page.Locator("[data-testid='success-message'], .bg-green-50, [role='status']");
        await Expect(successLocator).ToBeVisibleAsync(new() { Timeout = 10_000 });
    }
}
