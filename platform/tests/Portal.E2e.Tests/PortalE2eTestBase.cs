using Microsoft.Playwright.NUnit;
using NUnit.Framework.Interfaces;

namespace Portal.E2e.Tests;

/// <summary>
/// Base class for all portal E2E tests.
/// Captures a full-page screenshot automatically when a test fails.
/// </summary>
public abstract class PortalE2eTestBase : PageTest
{
    [TearDown]
    public async Task TakeScreenshotOnFailure()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed)
        {
            var dir = Path.Combine(
                TestContext.CurrentContext.TestDirectory, "screenshots");
            Directory.CreateDirectory(dir);

            var testName = TestContext.CurrentContext.Test.MethodName ?? "unknown";
            var filename = $"{testName}_{DateTime.UtcNow:HHmmss}.png";
            var screenshotPath = Path.Combine(dir, filename);

            await Page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true });

            TestContext.AddTestAttachment(screenshotPath, "Failure screenshot");
            TestContext.Out.WriteLine($"Screenshot saved: {screenshotPath}");
        }
    }
}
