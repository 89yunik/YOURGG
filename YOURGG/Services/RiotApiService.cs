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
        private readonly string ddragonUrl = "https://ddragon.leagueoflegends.com";

        public RiotApiService(HttpClient httpClient, IConfiguration configuration, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _cache = cache;
            _riotApiKey = configuration["RiotApiKey"] ?? throw new ArgumentNullException("RiotApiKey must be provided.");
        }
        public async Task<MatchDetailResult> GetLatestLiftMatchDetailBySummonerNameAsync(string summonerName)
        {
            MatchDetailResult result = new MatchDetailResult();

            try 
            {
                // 캐시 확인
                if (_cache.TryGetValue(summonerName, out MatchDetailViewModel? cachedMatch))
                {
                    result.IsSummonerFound = true;
                    result.IsMatchFound = true;
                    result.MatchDetail = cachedMatch;
                    return result;
                }

                string latestVersion = await GetLatestVersionAsync() ?? "15.8.1";

                // Riot API
                string riotApiBaseUrl = "https://asia.api.riotgames.com";
                string riotImgUrl = $"{ddragonUrl}/cdn/{latestVersion}/img";
                _httpClient.DefaultRequestHeaders.Add("X-Riot-Token", _riotApiKey);

                // 1. 소환사 ID 조회
                if (!summonerName.Contains("#")) return result;
                
                string? encodedSummonerName = summonerName.Replace("#", "/");
                string getPuuidByRiotIdUrl = $"{riotApiBaseUrl}/riot/account/v1/accounts/by-riot-id/{encodedSummonerName}";
                JsonElement summonerRes = await _httpClient.GetFromJsonAsync<JsonElement>(getPuuidByRiotIdUrl);
                string? puuid = summonerRes.GetProperty("puuid").GetString();
                
                if (string.IsNullOrEmpty(puuid)) return result;
                result.IsSummonerFound = true;

                // 2. 매치 리스트 조회
                List<string> allMatchIds = new List<string>();

                int count = 1;
                Dictionary<int, string> queueIdGameType = new Dictionary<int, string>
                {
                    { 420, "솔로랭크" },
                    { 440, "자유랭크" },
                    { 400, "일반" }
                };
                List<int> queueIds = queueIdGameType.Keys.ToList();
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

                JsonElement.ArrayEnumerator rawParticipants = rawMatchDetail.GetProperty("participants").EnumerateArray();

                JsonElement summonerData = rawParticipants.First(p => p.GetProperty("puuid").GetString() == puuid);

                result.IsMatchFound = true;

                int totalMinionsKilled = summonerData.GetProperty("totalMinionsKilled").GetInt32();
                int neutralMinionsKilled = summonerData.GetProperty("neutralMinionsKilled").GetInt32();
                int totalCS = totalMinionsKilled + neutralMinionsKilled;

                long gameStartTimestamp = rawMatchDetail.GetProperty("gameStartTimestamp").GetInt64();
                string gameDate = DateTimeOffset.FromUnixTimeMilliseconds(gameStartTimestamp).DateTime.ToString("MM/dd");

                string gameDuration = TimeSpan.FromSeconds(rawMatchDetail.GetProperty("gameDuration").GetInt32()).ToString(@"mm\:ss");
                List<string> items = new List<string>();
                for(int i=0;i<7;i++) {
                    int itemNum = summonerData.GetProperty($"item{i}").GetInt32();
                    string itemImgUrl = itemNum>0 ? $"{riotImgUrl}/item/{itemNum}.png" : "";
                    items.Add(itemImgUrl);
                }

                List<string> spellImgUrls = new List<string>();
                for(int i=1;i<3;i++){
                    int spellId = summonerData.GetProperty($"summoner{i}Id").GetInt32();
                    string? spellImgUrl = await GetSpellImageUrlAsync(spellId, latestVersion);
                    if(spellImgUrl is not null) spellImgUrls.Add(spellImgUrl);
                }

                string? rawChampionName = summonerData.GetProperty("championName").GetString();
                string championImgUrl = $"{riotImgUrl}/champion/{rawChampionName}.png";

                List<List<string>> participants = new List<List<string>>();

                foreach (JsonElement rawParticipant in rawParticipants)
                {
                    if (rawParticipant.TryGetProperty("championName", out JsonElement championName) && 
                    rawParticipant.TryGetProperty("riotIdGameName", out JsonElement riotIdGameName))
                    {
                        string champImgUrl = $"{riotImgUrl}/champion/{championName}.png";
                        List<string>? participant = new List<string>{ champImgUrl, riotIdGameName.GetString() };
                        participants.Add(participant);
                    }
                }

                MatchDetailViewModel matchDetail = new MatchDetailViewModel
                {
                    GameDate = gameDate,
                    GameDuration = gameDuration,
                    SummonerName = summonerData.GetProperty("riotIdGameName").GetString(),
                    ChampionImgUrl = championImgUrl,
                    ChampLevel = summonerData.GetProperty("champLevel").GetInt32(),
                    Participants = participants,
                    Spells = spellImgUrls,
                    Items = items,
                    Result = summonerData.GetProperty("win").GetBoolean() ? "WIN" : "LOSS",
                    Kills = summonerData.GetProperty("kills").GetInt32(),
                    Deaths = summonerData.GetProperty("deaths").GetInt32(),
                    Assists = summonerData.GetProperty("assists").GetInt32(),
                    TotalCS = totalCS,
                    GameType = queueIdGameType[rawMatchDetail.GetProperty("queueId").GetInt32()]
                };

                result.MatchDetail = matchDetail;
                _cache.Set(summonerName, matchDetail, TimeSpan.FromMinutes(5)); // 캐시 만료 시간 설정

                return result;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error fetching match details: {ex.Message}");
                return result;
            }
        }

        public async Task<string?> GetLatestVersionAsync()
        {
            var url = $"{ddragonUrl}/api/versions.json";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var versions = JsonSerializer.Deserialize<List<string>>(json);

                return versions?.FirstOrDefault(); // 최신 버전은 항상 첫 번째
            }

            return null;
        }

        public async Task<string?> GetSpellImageUrlAsync(int spellId, string version)
        {
            var url = $"{ddragonUrl}/cdn/{version}/data/ko_KR/summoner.json";
            var json = await _httpClient.GetStringAsync(url);
            var doc = JsonDocument.Parse(json);
            var spells = doc.RootElement.GetProperty("data");

            foreach (var spell in spells.EnumerateObject())
            {
                var value = spell.Value;
                if (value.GetProperty("key").GetString() == spellId.ToString())
                {
                    var imageName = value.GetProperty("image").GetProperty("full").GetString();
                    return $"{ddragonUrl}/cdn/{version}/img/spell/{imageName}";
                }
            }

            return null;
        }
    }
}