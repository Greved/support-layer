using System.Net;
using System.Text.Json;
using Api.Admin.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Admin.Tests;

[TestFixture]
public class FeedbackTests
{
    private AdminApiFactory _factory = null!;
    private HttpClient _client = null!;
    private readonly Guid _adminId = Guid.NewGuid();

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new AdminApiFactory();
        await _factory.InitAsync();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _factory.DisposeAsync();

    private AppDbContext Db()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    private void Auth() => _client.SetAdminToken(_adminId);

    [Test]
    public async Task GetFeedback_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.GetAsync("/admin/feedback");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetFeedback_FiltersByTenantAndFlags()
    {
        Auth();
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, $"feedback-list-{Guid.NewGuid():N}");
        var session = await SeedHelper.SeedChatSessionAsync(db, tenant);
        var message = await SeedHelper.SeedChatMessageAsync(db, session, role: "assistant", content: "The answer body");
        var flagged = await SeedHelper.SeedFeedbackAsync(db, tenant, message, rating: "down", comment: "wrong", flagged: true, promoted: false);

        var otherSession = await SeedHelper.SeedChatSessionAsync(db, tenant);
        var otherMessage = await SeedHelper.SeedChatMessageAsync(db, otherSession, role: "assistant", content: "second");
        await SeedHelper.SeedFeedbackAsync(db, tenant, otherMessage, rating: "up", comment: null, flagged: false, promoted: false);

        var resp = await _client.GetAsync($"/admin/feedback?tenantId={tenant.Id}&flaggedOnly=true&includePromoted=false");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.ValueKind.Should().Be(JsonValueKind.Array);
        body.GetArrayLength().Should().Be(1);
        var row = body.EnumerateArray().First();
        row.GetProperty("id").GetString().Should().Be(flagged.Id.ToString());
        row.GetProperty("flagged").GetBoolean().Should().BeTrue();
    }

    [Test]
    public async Task PromoteFeedback_ExistingEntry_MarksAsPromoted()
    {
        Auth();
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, $"feedback-promote-{Guid.NewGuid():N}");
        var session = await SeedHelper.SeedChatSessionAsync(db, tenant);
        var message = await SeedHelper.SeedChatMessageAsync(db, session, role: "assistant", content: "Need correction");
        var feedback = await SeedHelper.SeedFeedbackAsync(db, tenant, message, promoted: false);

        var resp = await _client.PostAsync($"/admin/feedback/{feedback.Id}/promote", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("id").GetString().Should().Be(feedback.Id.ToString());
        body.GetProperty("promoted").GetBoolean().Should().BeTrue();
        body.GetProperty("promotedAt").GetString().Should().NotBeNullOrWhiteSpace();

        var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await verifyDb.ChatMessageFeedback.FindAsync(feedback.Id);
        updated.Should().NotBeNull();
        updated!.Promoted.Should().BeTrue();
        updated.PromotedAt.Should().NotBeNull();
    }

    [Test]
    public async Task PromoteFeedback_CreatesEvalDatasetRow_AndIsIdempotent()
    {
        Auth();
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, $"feedback-promote-eval-{Guid.NewGuid():N}");
        var session = await SeedHelper.SeedChatSessionAsync(db, tenant);
        _ = await SeedHelper.SeedChatMessageAsync(
            db,
            session,
            role: "user",
            content: "How do I reset my password?");
        var assistant = await SeedHelper.SeedChatMessageAsync(
            db,
            session,
            role: "assistant",
            content: "Please check your profile settings.");
        var feedback = await SeedHelper.SeedFeedbackAsync(
            db,
            tenant,
            assistant,
            rating: "down",
            comment: "Expected a direct reset link");

        var firstResp = await _client.PostAsync($"/admin/feedback/{feedback.Id}/promote", null);
        firstResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResp = await _client.PostAsync($"/admin/feedback/{feedback.Id}/promote", null);
        secondResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var datasets = verifyDb.EvalDatasets
            .Where(d => d.SourceFeedbackId == feedback.Id)
            .ToList();
        datasets.Should().HaveCount(1);
        datasets[0].TenantId.Should().Be(tenant.Id);
        datasets[0].Question.Should().Be("How do I reset my password?");
        datasets[0].GroundTruth.Should().Be("Expected a direct reset link");
        datasets[0].QuestionType.Should().Be("feedback_negative");
    }

    [Test]
    public async Task PromoteFeedback_Unknown_Returns404()
    {
        Auth();
        var resp = await _client.PostAsync($"/admin/feedback/{Guid.NewGuid()}/promote", null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
