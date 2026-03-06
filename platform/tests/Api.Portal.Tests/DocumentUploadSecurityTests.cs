using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Api.Portal.Services;
using Api.Portal.Tests.Helpers;
using Core.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Portal.Tests;

[TestFixture]
public class DocumentUploadSecurityTests
{
    private PortalApiFactory _factory = null!;
    private Guid _tenantId;
    private Guid _userId;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new PortalApiFactory();
        await _factory.InitAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = await SeedHelper.SeedTenantAsync(db, "upload-security-corp");
        var user = await SeedHelper.SeedUserAsync(db, tenant, "upload-security@test.com");
        _tenantId = tenant.Id;
        _userId = user.Id;
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _factory.DisposeAsync();

    [Test]
    public async Task Upload_InfectedPayload_Returns422VirusDetected()
    {
        using var client = CreateClientWithScanner(new StubInfectedAntivirusScanner());
        client.SetPortalToken(_userId, _tenantId);

        using var form = CreatePdfUpload(
            "eicar.pdf",
            "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");

        var resp = await client.PostAsync("/portal/documents", form);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("virus_detected");
    }

    [Test]
    public async Task Upload_AntivirusUnavailable_Returns503()
    {
        using var client = CreateClientWithScanner(new StubUnavailableAntivirusScanner());
        client.SetPortalToken(_userId, _tenantId);

        using var form = CreatePdfUpload("safe.pdf", "%PDF-1.4 test payload");
        var resp = await client.PostAsync("/portal/documents", form);

        resp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("antivirus_unavailable");
    }

    [Test]
    public async Task Upload_CleanPayload_Returns200()
    {
        using var client = CreateClientWithScanner(new StubCleanAntivirusScanner());
        client.SetPortalToken(_userId, _tenantId);

        using var form = CreatePdfUpload("safe.pdf", "%PDF-1.4 clean content");
        var resp = await client.PostAsync("/portal/documents", form);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private HttpClient CreateClientWithScanner(IAntivirusScanner scanner)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAntivirusScanner>();
                services.AddSingleton(scanner);
            });
        }).CreateClient();
    }

    private static MultipartFormDataContent CreatePdfUpload(string fileName, string content)
    {
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

        var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", fileName);
        return form;
    }
}
