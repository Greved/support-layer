using System.Diagnostics;
using Api.Admin.Models.Responses;
using Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Admin.Services;

public class InfraHealthService(IConfiguration configuration, IHttpClientFactory httpFactory, AppDbContext db) : IInfraHealthService
{
    public async Task<InfraHealthResponse> CheckAllAsync(CancellationToken ct = default)
    {
        var checks = await Task.WhenAll(
            CheckHttpAsync("rag-core", $"{configuration["RagCore:BaseUrl"]}/healthz", ct),
            CheckHttpAsync("qdrant", $"{configuration["Qdrant:BaseUrl"]}/healthz", ct),
            CheckDatabaseAsync(ct));

        var overall = checks.All(c => c.Status == "healthy") ? "healthy" : "degraded";
        return new InfraHealthResponse(overall, checks);
    }

    private async Task<ServiceHealth> CheckHttpAsync(string name, string url, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync(url, ct);
            sw.Stop();
            return response.IsSuccessStatusCode
                ? new ServiceHealth(name, "healthy", LatencyMs: sw.ElapsedMilliseconds)
                : new ServiceHealth(name, "degraded", $"HTTP {(int)response.StatusCode}", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ServiceHealth(name, "unhealthy", ex.Message, sw.ElapsedMilliseconds);
        }
    }

    private async Task<ServiceHealth> CheckDatabaseAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
            sw.Stop();
            return new ServiceHealth("postgres", "healthy", LatencyMs: sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ServiceHealth("postgres", "unhealthy", ex.Message, sw.ElapsedMilliseconds);
        }
    }
}
