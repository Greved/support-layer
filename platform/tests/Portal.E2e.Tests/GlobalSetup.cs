namespace Portal.E2e.Tests;

/// <summary>
/// Assembly-level fixture that starts the portal API server once for all E2E tests.
/// Playwright tests reference GlobalSetup.BaseUrl to navigate the browser.
/// </summary>
[SetUpFixture]
public class GlobalSetup
{
    private static E2eServerFixture? _fixture;

    public static string BaseUrl => _fixture?.BaseUrl
        ?? throw new InvalidOperationException("E2E server not started.");

    public static string? SpaDistPath => _fixture?.SpaDistPath;

    [OneTimeSetUp]
    public async Task StartServer()
    {
        _fixture = new E2eServerFixture();
        await _fixture.InitAsync();
    }

    [OneTimeTearDown]
    public async Task StopServer()
    {
        if (_fixture is not null)
            await _fixture.DisposeAsync();
    }
}
