using Xunit;
using Moq;
using Moq.Contrib.HttpClient;
using Microsoft.Extensions.Caching.Memory;
using YOURGG.Services;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using YOURGG.Models;

public class RiotApiServiceTests
{
    [Fact]
    public async Task GetLatestLiftMatchDetailBySummonerNameAsync_ReturnsMatchDetail_WhenApiSucceeds()
    {
        // Arrange
        HttpResponseMessage mockResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(@"{
                ""puuid"": ""fake-puuid""
            }")
        };

        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri!.ToString().Contains("/riot/account/v1/accounts"))
                return mockResponse;

            if (req.RequestUri!.ToString().Contains("/matches/by-puuid"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(@"[ ""match1"" ]")
                };

            if (req.RequestUri!.ToString().Contains("/lol/match/v5/matches/match1"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(@"{
                        ""info"": {
                            ""gameMode"": ""CLASSIC"",
                            ""participants"": [{
                                ""puuid"": ""fake-puuid"",
                                ""championName"": ""Ahri"",
                                ""teamPosition"": ""MID"",
                                ""win"": true,
                                ""kills"": 5,
                                ""deaths"": 3,
                                ""assists"": 7
                            }]
                        }
                    }")
                };

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        HttpClient httpClient = new HttpClient(handler);
        MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "RiotApiKey", "test-key" }
        }).Build();

        RiotApiService service = new RiotApiService(httpClient, config, memoryCache);

        // Act
        var result = await service.GetLatestLiftMatchDetailBySummonerNameAsync("YOURGG#GenG");

        // Assert
        Assert.True(result.IsSummonerFound);
        Assert.True(result.IsMatchFound);
        Assert.NotNull(result.MatchDetail);
        Assert.Equal("Ahri", result.MatchDetail!.ChampionName);
    }
}

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handlerFunc;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handlerFunc)
    {
        _handlerFunc = handlerFunc;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handlerFunc(request));
    }
}