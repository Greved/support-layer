using Api.Portal.Services;
using Core.Data;
using Hangfire;
using Hangfire.InMemory;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Api.Portal.Tests;

public class PortalApiFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("test_portal")
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

            // Replace external HTTP services with stubs
            services.RemoveAll<IRagClient>();
            services.AddScoped<IRagClient, StubRagClient>();

            // Replace email with stub (already NoOp, but explicit)
            services.RemoveAll<IEmailService>();
            services.AddScoped<IEmailService, StubEmailService>();

            // Replace Hangfire PostgreSQL storage with in-memory (no real DB needed for jobs in tests)
            services.RemoveAll<IGlobalConfiguration>();
            services.AddHangfire(config => config.UseInMemoryStorage());

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
