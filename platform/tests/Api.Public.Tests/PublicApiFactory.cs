using Api.Public.Services;
using Core.Auth;
using Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Testcontainers.PostgreSql;

namespace Api.Public.Tests;

public class PublicApiFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("test_public")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public async Task InitAsync()
    {
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace real DB with Testcontainers Postgres
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));

            // Replace Redis and rate limiter with always-allow stub
            services.RemoveAll<IConnectionMultiplexer>();
            services.RemoveAll<IRateLimiter>();
            services.AddSingleton<IRateLimiter>(new AlwaysAllowRateLimiter());

            // Replace RAG client with stub
            services.RemoveAll<IPublicRagClient>();
            services.AddScoped<IPublicRagClient, StubPublicRagClient>();

            // Run migrations
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        });
    }

    public new async Task DisposeAsync()
    {
        await _postgres.StopAsync();
        await base.DisposeAsync();
    }
}
