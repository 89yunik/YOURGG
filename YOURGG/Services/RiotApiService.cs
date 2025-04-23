using YOURGG.Models;

namespace YOURGG.Services
{
    public class RiotApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _riotApiKey;
        private const string RiotAccountApiUrl = "https://asia.api.riotgames.com/riot/account/v1/accounts/by-riot-id";

        public RiotApiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _riotApiKey = configuration["RiotApiKey"] ?? throw new ArgumentNullException("RiotApiKey must be provided.");
        }

        public async Task<string?> GetPuuidByRiotIdAsync(string gameName, string tagLine)
        {
            string requestUrl = $"{RiotAccountApiUrl}/{gameName}/{tagLine}";

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Add("X-Riot-Token", _riotApiKey);

            HttpResponseMessage response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return null;

            RiotAccountResponse? result = await response.Content.ReadFromJsonAsync<RiotAccountResponse>();
            return result?.puuid;
        }

        public async Task<MatchDetailViewModel?> GetLatestMatchDetailAsync(string summonerName)
        {
            var result = new MatchDetailViewModel {
                SummonerName = summonerName,
                ChampionName = "Veigar",
                Result = "Lose",
                Kills = 4,
                Deaths = 8,
                Assists = 10,
                GameMode = "Flex Rank"
            };

            return result;
        }
    }
    public class RiotAccountResponse
    {
        public required string puuid { get; set; }
        public required string gameName { get; set; }
        public required string tagLine { get; set; }
    }
}