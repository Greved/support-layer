using Microsoft.Playwright;

namespace Portal.E2e.Tests.Pages;

/// <summary>Page object for the portal dashboard page (/portal/dashboard).</summary>
public class DashboardPage(IPage page)
{
    public async Task GoToAsync() =>
        await page.GotoAsync(GlobalSetup.BaseUrl + "/dashboard");

    public async Task<bool> IsLoadedAsync() =>
        await page.IsVisibleAsync("[data-testid='dashboard'], main, .dashboard", new() { Timeout = 10_000 });

    public async Task<bool> HasUsageDataAsync() =>
        await page.IsVisibleAsync("[data-testid='queries-count'], [data-testid='usage-card']",
            new() { Timeout = 5_000 });

    public async Task WaitForLoadAsync() =>
        await page.WaitForSelectorAsync("main, [data-testid='dashboard']", new() { Timeout = 15_000 });
}
