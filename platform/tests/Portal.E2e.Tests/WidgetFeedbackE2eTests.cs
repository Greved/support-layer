using System.Text.Json;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Portal.E2e.Tests;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class WidgetFeedbackE2eTests : PortalE2eTestBase
{
    private string? _widgetBundlePath;

    [SetUp]
    public void RequireWidgetBundle()
    {
        _widgetBundlePath = FindWidgetBundlePath();
        Assume.That(_widgetBundlePath, Is.Not.Null,
            "Widget bundle not found. Run 'npm run build' in widget/ before Widget E2E tests.");
    }

    [Test]
    public async Task WidgetFeedback_DownvoteWithComment_SubmitsExpectedPayload()
    {
        var sessionId = Guid.NewGuid();
        var assistantMessageId = Guid.NewGuid();
        var feedbackRequestTcs = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await SetupWidgetApiRoutesAsync(sessionId, assistantMessageId, feedbackRequestTcs, feedbackStatus: 201);
        await LoadWidgetHostAsync();
        await SendOneMessageAsync("Need refund policy details");

        await Page.ClickAsync("[data-testid='widget-feedback-down']");
        var comment = "Wrong policy details in this answer.";
        await Page.FillAsync("[data-testid='widget-feedback-down-editor'] textarea", comment);
        await Page.ClickAsync("[data-testid='widget-feedback-down-submit']");
        await Expect(Page.Locator("text=Thanks")).ToBeVisibleAsync(new() { Timeout = 10_000 });

        var postedJson = await feedbackRequestTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var payload = JsonDocument.Parse(postedJson);
        Assert.That(payload.RootElement.GetProperty("rating").GetString(), Is.EqualTo("down"));
        Assert.That(payload.RootElement.GetProperty("messageId").GetString(),
            Is.EqualTo(assistantMessageId.ToString()));
        Assert.That(payload.RootElement.GetProperty("comment").GetString(), Is.EqualTo(comment));
    }

    [Test]
    public async Task WidgetFeedback_Upvote_SubmitsWithoutComment()
    {
        var sessionId = Guid.NewGuid();
        var assistantMessageId = Guid.NewGuid();
        var feedbackRequestTcs = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await SetupWidgetApiRoutesAsync(sessionId, assistantMessageId, feedbackRequestTcs, feedbackStatus: 201);
        await LoadWidgetHostAsync();
        await SendOneMessageAsync("How can I reset my password?");

        await Page.ClickAsync("[data-testid='widget-feedback-up']");
        await Expect(Page.Locator("text=Thanks")).ToBeVisibleAsync(new() { Timeout = 10_000 });

        var postedJson = await feedbackRequestTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var payload = JsonDocument.Parse(postedJson);
        Assert.That(payload.RootElement.GetProperty("rating").GetString(), Is.EqualTo("up"));
        Assert.That(payload.RootElement.GetProperty("messageId").GetString(),
            Is.EqualTo(assistantMessageId.ToString()));
        Assert.That(payload.RootElement.TryGetProperty("comment", out _), Is.False,
            "Upvote feedback should not send a comment field by default.");
    }

    [Test]
    public async Task WidgetFeedback_WhenApiFails_ShowsInlineError()
    {
        var sessionId = Guid.NewGuid();
        var assistantMessageId = Guid.NewGuid();
        var feedbackRequestTcs = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await SetupWidgetApiRoutesAsync(sessionId, assistantMessageId, feedbackRequestTcs, feedbackStatus: 500);
        await LoadWidgetHostAsync();
        await SendOneMessageAsync("Explain billing limits");

        await Page.ClickAsync("[data-testid='widget-feedback-down']");
        await Page.FillAsync("[data-testid='widget-feedback-down-editor'] textarea", "Still incorrect");
        await Page.ClickAsync("[data-testid='widget-feedback-down-submit']");

        await Expect(Page.Locator("text=Failed to submit feedback. Please try again."))
            .ToBeVisibleAsync(new() { Timeout = 10_000 });
    }

    private async Task SetupWidgetApiRoutesAsync(
        Guid sessionId,
        Guid assistantMessageId,
        TaskCompletionSource<string> feedbackRequestTcs,
        int feedbackStatus)
    {
        await Page.RouteAsync("**/v1/session", async route =>
        {
            await route.FulfillAsync(new()
            {
                Status = 200,
                ContentType = "application/json",
                Body = JsonSerializer.Serialize(new { id = sessionId }),
            });
        });

        await Page.RouteAsync("**/v1/chat/stream", async route =>
        {
            var sseBody = string.Join("\n\n", [
                $"data: {JsonSerializer.Serialize(new { type = "sources", sources = Array.Empty<object>() })}",
                $"data: {JsonSerializer.Serialize(new { type = "token", chunk = "Stub streamed answer." })}",
                $"data: {JsonSerializer.Serialize(new { type = "done", answer = "Stub streamed answer.", session_id = sessionId, message_id = assistantMessageId })}",
                "data: [DONE]",
                string.Empty
            ]);

            await route.FulfillAsync(new()
            {
                Status = 200,
                ContentType = "text/event-stream",
                Body = sseBody,
            });
        });

        await Page.RouteAsync("**/v1/feedback", async route =>
        {
            feedbackRequestTcs.TrySetResult(route.Request.PostData ?? "{}");
            await route.FulfillAsync(new()
            {
                Status = feedbackStatus,
                ContentType = "application/json",
                Body = feedbackStatus >= 400
                    ? "{\"error\":\"feedback_failed\"}"
                    : JsonSerializer.Serialize(new
                    {
                        id = Guid.NewGuid(),
                        messageId = assistantMessageId,
                        rating = "down",
                        comment = "test",
                        flagged = true,
                        promoted = false,
                        createdAt = DateTime.UtcNow,
                    }),
            });
        });
    }

    private async Task LoadWidgetHostAsync()
    {
        var pageErrors = new List<string>();
        var consoleErrors = new List<string>();
        Page.PageError += (_, msg) => pageErrors.Add(msg);
        Page.Console += (_, msg) =>
        {
            if (msg.Type is "error" or "warning")
                consoleErrors.Add($"{msg.Type}: {msg.Text}");
        };

        var html = """
<!doctype html>
<html>
  <head><meta charset="utf-8" /></head>
  <body>
    <script
      data-api-key="widget-e2e-api-key"
      data-api-base="https://widget-e2e.local"
      data-title="Support Bot"
      data-color="#2563eb"
      data-position="inline"
    ></script>
  </body>
</html>
""";
        await Page.SetContentAsync(html, new() { WaitUntil = WaitUntilState.Load });
        await Page.AddScriptTagAsync(new() { Path = _widgetBundlePath! });
        await Task.Delay(400);
        var hasRoot = await Page.EvaluateAsync<bool>("() => !!document.getElementById('sl-widget-root')");
        if (!hasRoot)
        {
            var bodyPreview = await Page.EvaluateAsync<string>("() => document.body.innerHTML.slice(0, 1000)");
            throw new AssertionException(
                $"Widget root not rendered. PageErrors=[{string.Join(" | ", pageErrors)}], " +
                $"Console=[{string.Join(" | ", consoleErrors)}], BodyPreview={bodyPreview}");
        }

        var root = Page.Locator("#sl-widget-root");
        var rootHtml = await Page.EvaluateAsync<string>(
            "() => document.getElementById('sl-widget-root')?.innerHTML ?? ''");
        if (string.IsNullOrWhiteSpace(rootHtml))
        {
            throw new AssertionException(
                $"Widget root rendered empty. PageErrors=[{string.Join(" | ", pageErrors)}], " +
                $"Console=[{string.Join(" | ", consoleErrors)}]");
        }

        var launcher = Page.Locator("#sl-widget-root button").First;
        await Expect(launcher).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await launcher.ClickAsync();
        await Expect(Page.Locator("textarea[placeholder='Type your message...']"))
            .ToBeVisibleAsync(new() { Timeout = 10_000 });

        if (pageErrors.Count > 0)
        {
            TestContext.Out.WriteLine("Widget page errors:");
            foreach (var err in pageErrors)
                TestContext.Out.WriteLine(err);
        }
    }

    private async Task SendOneMessageAsync(string query)
    {
        await Page.FillAsync("textarea[placeholder='Type your message...']", query);
        await Page.ClickAsync("button[aria-label='Send message']");
        await Expect(Page.Locator("text=Stub streamed answer.")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Expect(Page.Locator("[data-testid='widget-feedback-up']")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Expect(Page.Locator("[data-testid='widget-feedback-down']")).ToBeVisibleAsync(new() { Timeout = 10_000 });
    }

    private static string? FindWidgetBundlePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "widget", "dist", "widget.umd.js");
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        return null;
    }
}
