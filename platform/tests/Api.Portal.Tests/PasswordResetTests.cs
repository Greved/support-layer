using System.Net;
using System.Text.Json;
using Api.Portal.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Portal.Tests;

[TestFixture]
public class PasswordResetTests
{
    private PortalApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new PortalApiFactory();
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

    [Test]
    public async Task Request_WithRegisteredEmail_Returns200()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "pw-reset-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "reset@pwtest.com");

        var resp = await _client.PostAsync("/portal/auth/password-reset/request",
            HttpHelper.Json(new { Email = user.Email }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("message").GetString().Should().Contain("reset link");
    }

    [Test]
    public async Task Request_WithUnknownEmail_Returns200_NoEnumeration()
    {
        var resp = await _client.PostAsync("/portal/auth/password-reset/request",
            HttpHelper.Json(new { Email = "nobody@unknown.com" }));

        // Must return 200 regardless — no enumeration leak
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Request_CreatesTokenInDatabase()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "pw-token-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "tokentest@pwtest.com");

        await _client.PostAsync("/portal/auth/password-reset/request",
            HttpHelper.Json(new { Email = user.Email }));

        var db2 = Db();
        var token = db2.PasswordResetTokens.FirstOrDefault(t => t.UserId == user.Id);
        token.Should().NotBeNull();
        token!.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        token.UsedAt.Should().BeNull();
    }

    [Test]
    public async Task Confirm_WithValidToken_Returns200AndUpdatesPassword()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "pw-confirm-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "confirm@pwtest.com");

        await _client.PostAsync("/portal/auth/password-reset/request",
            HttpHelper.Json(new { Email = user.Email }));

        // Extract raw token from the DB hash by seeding a known raw token directly
        var rawToken = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899";
        var tokenHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));

        var db2 = Db();
        db2.PasswordResetTokens.Add(new Core.Entities.PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow,
        });
        await db2.SaveChangesAsync();

        var resp = await _client.PostAsync("/portal/auth/password-reset/confirm",
            HttpHelper.Json(new { Token = rawToken, NewPassword = "NewPass123!" }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var db3 = Db();
        var updatedUser = db3.Users.First(u => u.Id == user.Id);
        BCrypt.Net.BCrypt.Verify("NewPass123!", updatedUser.PasswordHash).Should().BeTrue();
    }

    [Test]
    public async Task Confirm_WithExpiredToken_Returns400()
    {
        var rawToken = "expiredtoken00112233445566778899aabbccddeeff00112233445566778899";
        var tokenHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));

        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "pw-expired-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "expired@pwtest.com");

        db.PasswordResetTokens.Add(new Core.Entities.PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1), // expired
            CreatedAt = DateTime.UtcNow.AddMinutes(-31),
        });
        await db.SaveChangesAsync();

        var resp = await _client.PostAsync("/portal/auth/password-reset/confirm",
            HttpHelper.Json(new { Token = rawToken, NewPassword = "NewPass123!" }));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Confirm_ReuseSameToken_Returns400()
    {
        var rawToken = "reusedtoken00112233445566778899aabbccddeeff00112233445566778899";
        var tokenHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));

        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "pw-reuse-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "reuse@pwtest.com");

        db.PasswordResetTokens.Add(new Core.Entities.PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            UsedAt = DateTime.UtcNow.AddMinutes(-5), // already used
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
        });
        await db.SaveChangesAsync();

        var resp = await _client.PostAsync("/portal/auth/password-reset/confirm",
            HttpHelper.Json(new { Token = rawToken, NewPassword = "NewPass123!" }));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
