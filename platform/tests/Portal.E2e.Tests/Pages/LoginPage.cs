using Microsoft.Playwright;

namespace Portal.E2e.Tests.Pages;

/// <summary>Page object for the portal login page (/portal/login or /).</summary>
public class LoginPage(IPage page)
{
    public async Task GoToAsync()
    {
        await page.GotoAsync(GlobalSetup.BaseUrl + "/");
        // If the SPA redirects unauthenticated users to /login, wait for it
        await page.WaitForURLAsync(u => u.Contains("/login") || u == GlobalSetup.BaseUrl + "/",
            new() { Timeout = 10_000 });
    }

    public async Task FillEmailAsync(string email) =>
        await page.FillAsync("[data-testid='email-input'], input[type='email'], input[name='email']", email);

    public async Task FillPasswordAsync(string password) =>
        await page.FillAsync("[data-testid='password-input'], input[type='password'], input[name='password']", password);

    public async Task SubmitAsync() =>
        await page.ClickAsync("[data-testid='login-button'], button[type='submit']");

    public async Task LoginAsync(string email, string password)
    {
        await FillEmailAsync(email);
        await FillPasswordAsync(password);
        await SubmitAsync();
    }

    public ILocator ErrorMessage =>
        page.Locator("[data-testid='error-message'], .bg-red-50, [role='alert']");

    public async Task<bool> IsOnLoginPageAsync() =>
        page.Url.Contains("/login") || await page.IsVisibleAsync("input[type='password']");
}
