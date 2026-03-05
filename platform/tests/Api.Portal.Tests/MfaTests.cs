using System.Net;
using System.Text.Json;
using Api.Portal.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Portal.Tests;

[TestFixture]
public class MfaTests
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
    public async Task Enroll_AuthenticatedUser_Returns200WithTotpUri()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "mfa-enroll-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "enroll@mfa.com");
        _client.SetPortalToken(user.Id, tenant.Id);

        var resp = await _client.PostAsync("/portal/auth/mfa/enroll", HttpHelper.Json(new { }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("totpUri").GetString().Should().StartWith("otpauth://totp/");
        body.GetProperty("secret").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("backupCodes").GetArrayLength().Should().Be(10);
    }

    [Test]
    public async Task Enroll_UnauthenticatedUser_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.PostAsync("/portal/auth/mfa/enroll", HttpHelper.Json(new { }));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Enroll_SavesSecretToDatabase()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "mfa-secret-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "secret@mfa.com");
        _client.SetPortalToken(user.Id, tenant.Id);

        await _client.PostAsync("/portal/auth/mfa/enroll", HttpHelper.Json(new { }));

        var db2 = Db();
        var updatedUser = db2.Users.First(u => u.Id == user.Id);
        updatedUser.TotpSecret.Should().NotBeNullOrWhiteSpace();
        updatedUser.MfaEnabled.Should().BeFalse(); // not verified yet
    }

    [Test]
    public async Task Verify_WrongCode_Returns400()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "mfa-verify-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "verify@mfa.com");
        _client.SetPortalToken(user.Id, tenant.Id);

        // Enroll first
        await _client.PostAsync("/portal/auth/mfa/enroll", HttpHelper.Json(new { }));

        // Verify with wrong code
        var resp = await _client.PostAsync("/portal/auth/mfa/verify",
            HttpHelper.Json(new { Code = "000000" }));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Login_MfaEnabledUser_Returns200WithMfaRequired()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "mfa-login-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "mfalogin@mfa.com");

        // Enable MFA directly on user
        var db2 = Db();
        var u = db2.Users.First(x => x.Id == user.Id);
        u.TotpSecret = "JBSWY3DPEHPK3PXP"; // known test secret
        u.MfaEnabled = true;
        await db2.SaveChangesAsync();

        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.PostAsync("/portal/auth/login",
            HttpHelper.Json(new { Email = user.Email, Password = "password123" }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("mfaRequired").GetBoolean().Should().BeTrue();
        body.GetProperty("tempToken").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Login_MfaDisabledUser_ReturnsTokensDirectly()
    {
        var db = Db();
        var tenant = await SeedHelper.SeedTenantAsync(db, "mfa-no-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "nomfa@mfa.com");
        _client.DefaultRequestHeaders.Authorization = null;

        var resp = await _client.PostAsync("/portal/auth/login",
            HttpHelper.Json(new { Email = user.Email, Password = "password123" }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("refreshToken").GetString().Should().NotBeNullOrWhiteSpace();
        body.TryGetProperty("mfaRequired", out _).Should().BeFalse();
    }
}
