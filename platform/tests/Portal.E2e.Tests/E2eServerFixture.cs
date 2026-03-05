using System.Net;
using Api.Portal.Services;
using Core.Data;
using Hangfire;
using Hangfire.InMemory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Portal.E2e.Tests;

/// <summary>
/// Starts Api.Portal on a real Kestrel port (for Playwright) alongside a TestServer
/// (for WebApplicationFactory's CreateClient compatibility).
/// The PostgreSQL database is provided by Testcontainers.
/// If portal/dist/ exists, static files are served so the React SPA is accessible.
/// </summary>
public class E2eServerFixture : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("e2e_portal")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private IHost? _kestrelHost;

    public string BaseUrl { get; private set; } = "";
    public string? SpaDistPath { get; private set; }

    public async Task InitAsync()
    {
        await _postgres.StartAsync();
        SpaDistPath = FindPortalDistPath();

        // Trigger host creation (lazy in WebApplicationFactory)
        using var _ = CreateDefaultClient();
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
            services.AddScoped<IRagClient, E2eStubRagClient>();

            services.RemoveAll<IEmailService>();
            services.AddScoped<IEmailService, E2eStubEmailService>();

            // Replace Hangfire storage with in-memory
            services.RemoveAll<IGlobalConfiguration>();
            services.AddHangfire(config => config.UseInMemoryStorage());

            // Serve portal SPA static files if built
            if (SpaDistPath is not null)
                services.AddSingleton<IStartupFilter>(new SpaStaticFilesFilter(SpaDistPath));

            // Run migrations
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();

            // Seed a default E2E test user
            E2eSeeder.SeedDefaultUserAsync(scope.ServiceProvider).GetAwaiter().GetResult();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // 1. Build the TestServer-based host (required by WebApplicationFactory internals)
        var testHost = base.CreateHost(builder);

        // 2. Find a free port and bind Kestrel to it explicitly.
        //    UseUrls() populates IServerAddressesFeature.Addresses; Listen(0) does not.
        var port = GetFreePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        builder.ConfigureWebHost(webBuilder =>
        {
            webBuilder.UseKestrel();  // Override the in-memory TestServer with real Kestrel
            webBuilder.UseUrls(BaseUrl);
        });

        _kestrelHost = builder.Build();
        _kestrelHost.Start();

        return testHost;
    }

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public new async Task DisposeAsync()
    {
        if (_kestrelHost is not null)
        {
            await _kestrelHost.StopAsync();
            _kestrelHost.Dispose();
        }
        await _postgres.StopAsync();
        await base.DisposeAsync();
    }

    private static string? FindPortalDistPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var dist = Path.Combine(dir.FullName, "portal", "dist");
            if (Directory.Exists(dist))
                return dist;
            dir = dir.Parent;
        }
        return null;
    }
}

/// <summary>
/// Injects static file serving of the React SPA dist/ before API middleware runs.
/// </summary>
file class SpaStaticFilesFilter(string distPath) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            var provider = new PhysicalFileProvider(distPath);
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = provider });
            next(app);

            // SPA fallback: non-API, non-static paths → index.html
            app.Use(async (ctx, nxt) =>
            {
                if (!ctx.Request.Path.StartsWithSegments("/portal") &&
                    !ctx.Request.Path.StartsWithSegments("/hangfire") &&
                    !Path.HasExtension(ctx.Request.Path))
                {
                    ctx.Response.ContentType = "text/html; charset=utf-8";
                    await using var fs = File.OpenRead(Path.Combine(distPath, "index.html"));
                    await fs.CopyToAsync(ctx.Response.Body);
                }
                else
                {
                    await nxt();
                }
            });
        };
    }
}

public class E2eStubRagClient : IRagClient
{
    public Task<RagQueryResult> QueryAsync(string tenantSlug, string query)
        => Task.FromResult(new RagQueryResult("E2E stub answer.", []));

    public Task<RagIngestResult> IngestAsync(string tenantSlug, string documentId,
        string fileName, byte[] fileBytes, string contentType)
        => Task.FromResult(new RagIngestResult(1, documentId));
}

public class E2eStubEmailService : IEmailService
{
    public Task SendPasswordResetAsync(string toEmail, string resetLink) => Task.CompletedTask;
    public Task SendInviteAsync(string toEmail, string inviteLink, string tenantName) => Task.CompletedTask;
}
