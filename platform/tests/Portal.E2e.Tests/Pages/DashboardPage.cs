using Microsoft.Playwright;

namespace Portal.E2e.Tests.Pages;

/// <summary>Page object for the portal dashboard page (/portal/dashboard).</summary>
public class DashboardPage(IPage page)
{
    public async Task GoToAsync() =>
        await page.GotoAsync(GlobalSetup.BaseUrl + "/dashboard");

    public Task<bool> IsLoadedAsync() =>
        IsVisibleWithinAsync("[data-testid='dashboard'], main, .dashboard", 10_000);

    public Task<bool> HasUsageDataAsync() =>
        IsVisibleWithinAsync("[data-testid='queries-count'], [data-testid='usage-card']", 5_000);

    public async Task WaitForLoadAsync() =>
        await page.WaitForSelectorAsync("main, [data-testid='dashboard']", new() { Timeout = 15_000 });

    private async Task<bool> IsVisibleWithinAsync(string selector, float timeoutMs)
    {
        try
        {
            await page.WaitForSelectorAsync(selector, new()
            {
                State = WaitForSelectorState.Visible,
                Timeout = timeoutMs,
            });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
