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
            CreateResponse(HttpStatusCode.OK, BuildSearchPayload("Death Note", 1535))
        });

        var delays = new List<TimeSpan>();
        var httpClient = CreateHttpClient(handler);
        var client = new JikanApiClient(httpClient, delay =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        var result = await client.SearchAsync("Death Note");

        Assert.Single(result);
        Assert.Equal("Death Note", result[0].Title);
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

    private static string BuildSearchPayload(string title, int malId)
    {
        return $$"""
        {
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

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;

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
