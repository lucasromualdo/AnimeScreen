using System.Net;
using System.Net.Http.Headers;
using System.Text;
using MyAnimeScreen.App.Services.Api;

namespace MyAnimeScreen.App.Tests.Services.Api;

public sealed class JikanApiClientRetryTests
{
    [Fact]
    public async Task SearchAsync_QuandoRecebe429ERetryAfter_DeveTentarNovamenteERespeitarAtraso()
    {
        var handler = new SequenceHttpMessageHandler(new[]
        {
            CreateResponse(HttpStatusCode.TooManyRequests, @"{""data"":[]}", retryAfterSeconds: 2),
            CreateResponse(HttpStatusCode.OK, BuildSearchPayload("Death Note", 1535, hasNextPage: true))
        });

        var delays = new List<TimeSpan>();
        var httpClient = CreateHttpClient(handler);
        var client = new JikanApiClient(httpClient, delay =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        var result = await client.SearchAsync("Death Note");

        Assert.Single(result.Items);
        Assert.Equal("Death Note", result.Items[0].Title);
        Assert.True(result.HasMore);
        Assert.Equal(2, handler.CallCount);
        Assert.Single(delays);
        Assert.Equal(TimeSpan.FromSeconds(2), delays[0]);
    }

    [Fact]
    public async Task SearchAsync_QuandoRecebe500AteOLimite_DeveRetornarErroTemporario()
    {
        var handler = new SequenceHttpMessageHandler(new[]
        {
            CreateResponse(HttpStatusCode.InternalServerError, @"{""data"":[]}"),
            CreateResponse(HttpStatusCode.InternalServerError, @"{""data"":[]}"),
            CreateResponse(HttpStatusCode.InternalServerError, @"{""data"":[]}"),
            CreateResponse(HttpStatusCode.InternalServerError, @"{""data"":[]}")
        });

        var httpClient = CreateHttpClient(handler);
        var client = new JikanApiClient(httpClient, _ => Task.CompletedTask);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync("Bleach"));

        Assert.Equal(4, handler.CallCount);
        Assert.Contains("Falha temporaria na API Jikan", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchAsync_QuandoRecebe400_DeveRetornarErroDefinitivoSemRetry()
    {
        var handler = new SequenceHttpMessageHandler(new[]
        {
            CreateResponse(HttpStatusCode.BadRequest, @"{""data"":[]}")
        });

        var httpClient = CreateHttpClient(handler);
        var client = new JikanApiClient(httpClient, _ => Task.CompletedTask);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchAsync("One Piece"));

        Assert.Equal(1, handler.CallCount);
        Assert.Contains("Falha definitiva na API Jikan", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchAsync_QuandoPaginaInformada_DeveEnviarPaginaNoRequest()
    {
        var handler = new SequenceHttpMessageHandler(new[]
        {
            CreateResponse(HttpStatusCode.OK, BuildSearchPayload("Naruto", 20, hasNextPage: false))
        });

        var httpClient = CreateHttpClient(handler);
        var client = new JikanApiClient(httpClient, _ => Task.CompletedTask);

        var result = await client.SearchAsync("Naruto", page: 2, maxRows: 25);

        Assert.Single(result.Items);
        Assert.False(result.HasMore);
        Assert.Single(handler.RequestedPaths);
        Assert.Contains("page=2", handler.RequestedPaths[0], StringComparison.Ordinal);
        Assert.Contains("limit=25", handler.RequestedPaths[0], StringComparison.Ordinal);
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.jikan.moe/v4/")
        };
    }

    private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string body, int? retryAfterSeconds = null)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        if (retryAfterSeconds.HasValue)
        {
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(retryAfterSeconds.Value));
        }

        return response;
    }

    private static string BuildSearchPayload(string title, int malId, bool hasNextPage = false)
    {
        return $$"""
        {
          "pagination": {
            "has_next_page": {{hasNextPage.ToString().ToLowerInvariant()}}
          },
          "data": [
            {
              "mal_id": {{malId}},
              "title": "{{title}}",
              "title_japanese": "{{title}}",
              "synopsis": "teste",
              "episodes": 37,
              "score": 8.9,
              "year": 2006,
              "season": "fall",
              "images": {
                "jpg": { "image_url": "https://example.com/jpg.jpg" },
                "webp": { "image_url": "https://example.com/webp.webp" }
              }
            }
          ]
        }
        """;
    }

    private sealed class SequenceHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public SequenceHttpMessageHandler(IEnumerable<HttpResponseMessage> responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public int CallCount { get; private set; }
        public List<string> RequestedPaths { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            RequestedPaths.Add(request.RequestUri?.PathAndQuery ?? string.Empty);

            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(@"{""data"":[]}", Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
