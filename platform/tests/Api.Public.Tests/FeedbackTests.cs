using System.Net;
using System.Text;
using System.Text.Json;
using Api.Public.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Public.Tests;

[TestFixture]
public class FeedbackTests
{
    private PublicApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new PublicApiFactory();
        await _factory.InitAsync();
        _client = _factory.CreateClient();

        var db = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, _, plaintext) = await SeedHelper.SeedTenantWithApiKeyAsync(db, "feedback-corp");
        _client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", plaintext);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _factory.DisposeAsync();

    private static StringContent JsonBody(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private async Task<Guid> CreateAssistantMessageAsync(string query = "Need help with password reset")
    {
        var chatResp = await _client.PostAsync("/v1/chat", JsonBody(new { query }));
        chatResp.StatusCode.Should().Be(HttpStatusCode.OK);
        using var chatDoc = JsonDocument.Parse(await chatResp.Content.ReadAsStringAsync());
        var sessionId = chatDoc.RootElement.GetProperty("sessionId").GetGuid();

        var sessionResp = await _client.GetAsync($"/v1/session/{sessionId}");
        sessionResp.StatusCode.Should().Be(HttpStatusCode.OK);
        using var sessionDoc = JsonDocument.Parse(await sessionResp.Content.ReadAsStringAsync());
        var assistantMessage = sessionDoc.RootElement
            .GetProperty("messages")
            .EnumerateArray()
            .FirstOrDefault(m => string.Equals(
                m.GetProperty("role").GetString(),
                "assistant",
                StringComparison.OrdinalIgnoreCase));

        assistantMessage.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        return assistantMessage.GetProperty("id").GetGuid();
    }

    [Test]
    public async Task Feedback_DownvoteWithComment_CreatesFlaggedEntry()
    {
        var messageId = await CreateAssistantMessageAsync();

        var resp = await _client.PostAsync(
            "/v1/feedback",
            JsonBody(new { messageId, rating = "down", comment = "Answer is incorrect" }));

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("messageId").GetGuid().Should().Be(messageId);
        body.RootElement.GetProperty("rating").GetString().Should().Be("down");
        body.RootElement.GetProperty("flagged").GetBoolean().Should().BeTrue();

        var db = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();
        var row = db.ChatMessageFeedback.FirstOrDefault(f => f.ChatMessageId == messageId);
        row.Should().NotBeNull();
        row!.Flagged.Should().BeTrue();
    }

    [Test]
    public async Task Feedback_InvalidRating_Returns422()
    {
        var messageId = await CreateAssistantMessageAsync("another prompt");

        var resp = await _client.PostAsync(
            "/v1/feedback",
            JsonBody(new { messageId, rating = "meh", comment = "invalid rating value" }));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task Feedback_ExistingEntry_UpdatesAndUnflagsOnUpvote()
    {
        var messageId = await CreateAssistantMessageAsync("one more prompt");

        var createResp = await _client.PostAsync(
            "/v1/feedback",
            JsonBody(new { messageId, rating = "down", comment = "not good" }));
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        var updateResp = await _client.PostAsync(
            "/v1/feedback",
            JsonBody(new { messageId, rating = "up", comment = "" }));
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = JsonDocument.Parse(await updateResp.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("rating").GetString().Should().Be("up");
        body.RootElement.GetProperty("flagged").GetBoolean().Should().BeFalse();
    }
}
