using System.Text.Json;
using YOURGG.Models;
using Microsoft.Extensions.Caching.Memory;

namespace YOURGG.Services
{
    public class RiotApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly string _riotApiKey;

        public RiotApiService(HttpClient httpClient, IConfiguration configuration, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _cache = cache;
            _riotApiKey = configuration["RiotApiKey"] ?? throw new ArgumentNullException("RiotApiKey must be provided.");
        }
        public async Task<MatchDetailResult> GetLatestLiftMatchDetailBySummonerNameAsync(string summonerName)
        {
            MatchDetailResult result = new MatchDetailResult();
            if (_cache.TryGetValue(summonerName, out MatchDetailViewModel? cachedMatch))
            {
                result.IsSummonerFound = true;
                result.IsMatchFound = true;
                result.MatchDetail = cachedMatch;
                return result;
            }

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
            
            var tasks = queueIds.Select(queueId =>
                _httpClient.GetFromJsonAsync<string[]>($"{getMatchIdsByRiotPuuidUrl}&queue={queueId}")
            ).ToList();
            var results = await Task.WhenAll(tasks);

            foreach (var matchIds in results)
            {
                if (matchIds != null && matchIds.Length > 0) allMatchIds.AddRange(matchIds);
            }

            string? latestMatchId = allMatchIds.Distinct().OrderByDescending(id => id).FirstOrDefault();

            if (allMatchIds.Count == 0 || latestMatchId is null) return result;
            // 3. 매치 상세 조회
            string getLatestSummonersLiftMatchDetailUrl = $"{riotApiBaseUrl}/lol/match/v5/matches/{latestMatchId}";
            JsonElement match = await _httpClient.GetFromJsonAsync<JsonElement>(getLatestSummonersLiftMatchDetailUrl);

            JsonElement rawMatchDetail = match.GetProperty("info");

            JsonElement.ArrayEnumerator participants = rawMatchDetail.GetProperty("participants").EnumerateArray();

            JsonElement summonerData = participants.First(p => p.GetProperty("puuid").GetString() == puuid);

            result.IsMatchFound = true;
            MatchDetailViewModel matchDetail = new MatchDetailViewModel
            {
                SummonerName = summonerName,
                ChampionName = summonerData.GetProperty("championName").GetString(),
                Role = summonerData.GetProperty("teamPosition").GetString(),
                Result = summonerData.GetProperty("win").GetBoolean() ? "Win" : "Lose",
                Kills = summonerData.GetProperty("kills").GetInt32(),
                Deaths = summonerData.GetProperty("deaths").GetInt32(),
                Assists = summonerData.GetProperty("assists").GetInt32(),
                GameMode = rawMatchDetail.GetProperty("gameMode").GetString()
            };

            result.MatchDetail = matchDetail;
            _cache.Set(summonerName, matchDetail, TimeSpan.FromMinutes(5)); // 캐시 만료 시간 설정

            return result;
        }
    }
}