using System.Net;
using System.Net.Http;
using System.Text;
using Core.Entities;
using Core.Evals;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Admin.Tests;

[TestFixture]
public class EvalScoringServiceLiveQueryTests
{
    [Test]
    public void ScoreAsync_FailsHard_WhenRealEvalCommandFails_EvenWithLiveRagQueryPayload()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["RagCore:BaseUrl"] = "http://rag.local",
            ["RagCore:InternalSecret"] = "secret",
            ["Phase6:EvalRunner:UseLiveRagQuery"] = "true",
            ["Phase6:EvalRunner:RequireLiveRagQuery"] = "false",
            ["Phase6:EvalRunner:Command"] = "definitely-not-a-real-python-command",
        });
        var requestCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            var json = """
                       {
                         "answer": "Live answer from rag-core.",
                         "sources": [
                           {
                             "file": "kb/refund.md",
                             "page": null,
                             "offset": null,
                             "relevance_score": 0.9,
                             "brief_content": "Refunds are accepted within 30 days."
                           }
                         ]
                       }
                       """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        });
        var httpFactory = new StubHttpClientFactory(new HttpClient(handler));
        var service = new PythonEvalScoringService(config, httpFactory, NullLogger<PythonEvalScoringService>.Instance);

        var datasetRows = new[]
        {
            new EvalDataset
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                Question = "What is refund policy?",
                GroundTruth = "Refunds are accepted within 30 days.",
                SourceChunkIdsJson = """["chunk-refund-policy"]""",
                QuestionType = "synthetic",
                DatasetVersion = "test",
                CreatedAt = DateTime.UtcNow,
            }
        };

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ScoreAsync("tenant-live", "run-live", datasetRows));

        ex.Should().NotBeNull();
        ex!.Message.Should().Contain("Real eval scoring failed");
        requestCount.Should().Be(1, "live RAG query hydration should still occur before eval command execution");
    }

    [Test]
    public void ScoreAsync_Throws_WhenLiveRagQueryIsRequiredAndFails()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["RagCore:BaseUrl"] = "http://rag.local",
            ["RagCore:InternalSecret"] = "secret",
            ["Phase6:EvalRunner:UseLiveRagQuery"] = "true",
            ["Phase6:EvalRunner:RequireLiveRagQuery"] = "true",
            ["Phase6:EvalRunner:Command"] = "python",
        });
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        });
        var httpFactory = new StubHttpClientFactory(new HttpClient(handler));
        var service = new PythonEvalScoringService(config, httpFactory, NullLogger<PythonEvalScoringService>.Instance);

        var datasetRows = new[]
        {
            new EvalDataset
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                Question = "Q",
                GroundTruth = "G",
                SourceChunkIdsJson = "[]",
                QuestionType = "synthetic",
                DatasetVersion = "test",
                CreatedAt = DateTime.UtcNow,
            }
        };

        Assert.ThrowsAsync<HttpRequestException>(async () =>
            await service.ScoreAsync("tenant-live", "run-live", datasetRows));
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> callback)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = callback(request);
            return Task.FromResult(response);
        }
    }
}
