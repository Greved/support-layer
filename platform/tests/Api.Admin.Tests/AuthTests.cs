using System.Net;
using System.Text.Json;
using Api.Admin.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Admin.Tests;

public class AuthTests(AdminApiFactory factory) : IClassFixture<AdminApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private AppDbContext Db()
    {
        var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        await SeedHelper.SeedAdminUserAsync(Db(), "auth_valid@test.com");

        var resp = await _client.PostAsync("/admin/auth/login",
            HttpHelper.Json(new { Email = "auth_valid@test.com", Password = "password123" }));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.ReadJson<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("email").GetString().Should().Be("auth_valid@test.com");
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        await SeedHelper.SeedAdminUserAsync(Db(), "auth_wrongpw@test.com");

        var resp = await _client.PostAsync("/admin/auth/login",
            HttpHelper.Json(new { Email = "auth_wrongpw@test.com", Password = "wrong" }));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var resp = await _client.PostAsync("/admin/auth/login",
            HttpHelper.Json(new { Email = "nobody@test.com", Password = "password123" }));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_InactiveAdmin_Returns401()
    {
        var db = Db();
        var admin = await SeedHelper.SeedAdminUserAsync(db, "auth_inactive@test.com");
        admin.IsActive = false;
        await db.SaveChangesAsync();

        var resp = await _client.PostAsync("/admin/auth/login",
            HttpHelper.Json(new { Email = "auth_inactive@test.com", Password = "password123" }));

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
