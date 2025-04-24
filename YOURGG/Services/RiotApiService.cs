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
        public async Task<MatchDetailResult> GetLatestLiftMatchDetailBySummonerNameAsync(string summonerName)
        {
            MatchDetailResult result = new MatchDetailResult();

            // Riot API Key
            string riotApiBaseUrl = "https://asia.api.riotgames.com";
            _httpClient.DefaultRequestHeaders.Add("X-Riot-Token", _riotApiKey);

            if (!summonerName.Contains("#")) return result;
            // 1. 소환사 ID 조회
            string? encodedSummonerName = summonerName.Replace("#", "/");
            string getPuuidByRiotIdUrl = $"{riotApiBaseUrl}/riot/account/v1/accounts/by-riot-id/{encodedSummonerName}";
            JsonElement summonerRes = await _httpClient.GetFromJsonAsync<JsonElement>(getPuuidByRiotIdUrl);
            string? puuid = summonerRes.GetProperty("puuid").GetString();

            if (string.IsNullOrEmpty(puuid)) return result;
            result.IsSummonerFound = true;

            // 2. 매치 리스트 조회
            List<string> allMatchIds = new List<string>();

            int count = 1;
            int[] queueIds = [420, 440, 400];
            string getMatchIdsByRiotPuuidUrl = $"{riotApiBaseUrl}/lol/match/v5/matches/by-puuid/{puuid}/ids?start=0&count={count}";
            foreach (int queueId in queueIds){
                string[]? matchIds = await _httpClient.GetFromJsonAsync<string[]>(getMatchIdsByRiotPuuidUrl+$"&queue={queueId}");
                
                if (matchIds != null && matchIds.Length > 0)
                {
                    allMatchIds.AddRange(matchIds);
                }
            }

            string? latestMatchId = allMatchIds.Distinct().OrderByDescending(id => id).FirstOrDefault();

            if (allMatchIds.Count == 0 || latestMatchId is null) return result;
            // 3. 매치 상세 조회
            string getLatestSummonersLiftMatchDetailUrl = $"{riotApiBaseUrl}/lol/match/v5/matches/{latestMatchId}";
            JsonElement match = await _httpClient.GetFromJsonAsync<JsonElement>(getLatestSummonersLiftMatchDetailUrl);

            JsonElement matchDetail = match.GetProperty("info");

            JsonElement.ArrayEnumerator participants = matchDetail.GetProperty("participants").EnumerateArray();

            JsonElement summonerData = participants.First(p => p.GetProperty("puuid").GetString() == puuid);

            result.IsMatchFound = true;
            result.MatchDetail = new MatchDetailViewModel
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

            return result;
        }
    }
}