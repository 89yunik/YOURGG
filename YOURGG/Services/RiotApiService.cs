using System.Text.Json;
using YOURGG.Models;

namespace YOURGG.Services
{
    public class RiotApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _riotApiKey;

        public RiotApiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _riotApiKey = configuration["RiotApiKey"] ?? throw new ArgumentNullException("RiotApiKey must be provided.");
        }
        public async Task<MatchDetailViewModel?> GetLatestLiftMatchDetailBySummonerNameAsync(string summonerName)
        {
            // Riot API Key
            string riotApiBaseUrl = "https://asia.api.riotgames.com";
            _httpClient.DefaultRequestHeaders.Add("X-Riot-Token", _riotApiKey);

            // 1. 소환사 ID 조회
            string? encodedSummonerName = summonerName.Replace("#", "/");
            string getPuuidByRiotIdUrl = $"{riotApiBaseUrl}/riot/account/v1/accounts/by-riot-id/{encodedSummonerName}";
            JsonElement summonerRes = await _httpClient.GetFromJsonAsync<JsonElement>(getPuuidByRiotIdUrl);
            string? puuid = summonerRes.GetProperty("puuid").GetString();

            // 2. 매치 리스트 조회
            int count = 100;
            string getMatchIdsByRiotPuuidUrl = $"{riotApiBaseUrl}/lol/match/v5/matches/by-puuid/{puuid}/ids?&start=0&count={count}";
            string[]? matchIds = await _httpClient.GetFromJsonAsync<string[]>(getMatchIdsByRiotPuuidUrl);

            if (matchIds is null || matchIds.Length == 0) return null;
            foreach (string matchId in matchIds){
                // 3. 매치 상세 조회
                string getLatestSummonersLiftMatchDetailUrl = $"{riotApiBaseUrl}/lol/match/v5/matches/{matchId}";
                JsonElement match = await _httpClient.GetFromJsonAsync<JsonElement>(getLatestSummonersLiftMatchDetailUrl);

                JsonElement matchDetail = match.GetProperty("info");
                int mapId = matchDetail.GetProperty("mapId").GetInt32();
                if (mapId == 11) // 11 = Summoner's Rift
                {
                    int queueId = matchDetail.GetProperty("queueId").GetInt32();
                    if (queueId == 420 || queueId == 430 || queueId == 440) // 420: Solo, 430: Normal, 440: Flex
                    {
                        JsonElement.ArrayEnumerator participants = matchDetail.GetProperty("participants").EnumerateArray();

                        JsonElement summonerData = participants.First(p => p.GetProperty("puuid").GetString() == puuid);

                        return new MatchDetailViewModel
                        {
                            SummonerName = summonerName,
                            ChampionName = summonerData.GetProperty("championName").GetString(),
                            Role = summonerData.GetProperty("teamPosition").GetString(),
                            Result = summonerData.GetProperty("win").GetBoolean() ? "Win" : "Lose",
                            Kills = summonerData.GetProperty("kills").GetInt32(),
                            Deaths = summonerData.GetProperty("deaths").GetInt32(),
                            Assists = summonerData.GetProperty("assists").GetInt32(),
                            GameMode = matchDetail.GetProperty("gameMode").GetString()
                        };
                    }
                }
            }

            return null;
        }
    }
    public class RiotAccountResponse
    {
        public required string puuid { get; set; }
        public required string gameName { get; set; }
        public required string tagLine { get; set; }
    }
}