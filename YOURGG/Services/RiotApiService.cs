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
        private readonly string _ddragonUrl = "https://ddragon.leagueoflegends.com";
        private readonly string _riotApiAsiaUrl = "https://asia.api.riotgames.com";
        private readonly Dictionary<int, string> _queueIdGameType = new Dictionary<int, string>
        {
            { 420, "솔로랭크" },
            { 440, "자유랭크" },
            { 400, "일반" }
        };

        public RiotApiService(HttpClient httpClient, IConfiguration configuration, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _cache = cache;
            _riotApiKey = configuration["RiotApiKey"] ?? throw new ArgumentNullException("RiotApiKey must be provided.");
            _httpClient.DefaultRequestHeaders.Add("X-Riot-Token", _riotApiKey);
        }

        public async Task<MatchDetailResult> GetLatestLiftMatchDetailBySummonerNameAsync(string summonerName)
        {
            var result = new MatchDetailResult();

            try
            {
                if (TryGetFromCache(summonerName, out var cachedMatch) && cachedMatch is not null) 
                    return BuildResultFromCache(cachedMatch);

                if (!IsValidSummonerName(summonerName)) return result;

                var latestVersion = await GetLatestVersionAsync() ?? "15.8.1";
                var puuid = await GetPuuidAsync(summonerName);
                if (puuid == null) return result;
                result.IsSummonerFound = true;

                var latestMatchId = await GetLatestMatchIdAsync(puuid);
                if (latestMatchId == null) return result;

                var matchInfo = await GetMatchInfoAsync(latestMatchId);
                if (matchInfo == null) return result;
                result.IsMatchFound = true;

                var matchDetail = await BuildMatchDetailViewModelAsync(matchInfo.Value, puuid, latestVersion);
                result.MatchDetail = matchDetail;

                SetCache(summonerName, matchDetail);

                return result;
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"[HTTP ERROR] {ex.Message}");
                return result;
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"[JSON PARSE ERROR] {ex.Message}");
                return result;
            }
            catch (ArgumentNullException ex)
            {
                Console.Error.WriteLine($"[CONFIG ERROR] {ex.Message}");
                return result;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[UNEXPECTED ERROR] {ex.Message}");
                return result;
            }
        }

        private bool TryGetFromCache(string summonerName, out MatchDetailViewModel? cachedMatch)
        {
            return _cache.TryGetValue(summonerName, out cachedMatch);
        }

        private void SetCache(string summonerName, MatchDetailViewModel matchDetail)
        {
            _cache.Set(summonerName, matchDetail, TimeSpan.FromMinutes(5));
        }

        private MatchDetailResult BuildResultFromCache(MatchDetailViewModel cachedMatch)
        {
            return new MatchDetailResult
            {
                IsSummonerFound = true,
                IsMatchFound = true,
                MatchDetail = cachedMatch
            };
        }

        private bool IsValidSummonerName(string summonerName)
        {
            return summonerName.Contains("#");
        }

        private async Task<string?> GetPuuidAsync(string summonerName)
        {
            var encodedName = summonerName.Replace("#", "/");
            var url = $"{_riotApiAsiaUrl}/riot/account/v1/accounts/by-riot-id/{encodedName}";
            var response = await GetJsonAsync(url);
            return response?.GetProperty("puuid").GetString();
        }

        private async Task<string?> GetLatestMatchIdAsync(string puuid)
        {
            var tasks = _queueIdGameType.Keys.Select(queueId =>
                GetJsonAsync($"{_riotApiAsiaUrl}/lol/match/v5/matches/by-puuid/{puuid}/ids?start=0&count=1&queue={queueId}")
            );
            var results = await Task.WhenAll(tasks);

            var allMatchIds = results // [ ["KR_1", "KR_2"], ["KR_3"], ["KR_4", "KR_5"] ]
                .Where(r => r != null)
                .SelectMany(r => r!.Value.EnumerateArray().Select(x => x.GetString())) // flat : [ "KR_1", "KR_2", "KR_3", "KR_4", "KR_5" ]
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .OrderByDescending(id => id)
                .FirstOrDefault();

            return allMatchIds;
        }

        private async Task<JsonElement?> GetMatchInfoAsync(string matchId)
        {
            var url = $"{_riotApiAsiaUrl}/lol/match/v5/matches/{matchId}";
            return (await GetJsonAsync(url))?.GetProperty("info");
        }

        private async Task<MatchDetailViewModel> BuildMatchDetailViewModelAsync(JsonElement matchInfo, string puuid, string version)
        {
            var participants = matchInfo.GetProperty("participants");
            var summoner = participants.EnumerateArray().FirstOrDefault(p => p.GetProperty("puuid").GetString() == puuid);

            var riotImgUrl = $"{_ddragonUrl}/cdn/{version}/img";

            var matchDetail = new MatchDetailViewModel
            {
                GameDate = FormatGameDate(matchInfo.GetProperty("gameStartTimestamp").GetInt64()),
                GameDuration = FormatGameDuration(matchInfo.GetProperty("gameDuration").GetInt32()),
                SummonerName = summoner.GetProperty("riotIdGameName").GetString(),
                ChampionImgUrl = $"{riotImgUrl}/champion/{summoner.GetProperty("championName").GetString()}.png",
                ChampLevel = summoner.GetProperty("champLevel").GetInt32(),
                Participants = BuildParticipants(participants, riotImgUrl),
                Spells = await BuildSpellsAsync(summoner, version),
                Items = BuildItems(summoner, riotImgUrl),
                Result = summoner.GetProperty("win").GetBoolean() ? "WIN" : "LOSS",
                Kills = summoner.GetProperty("kills").GetInt32(),
                Deaths = summoner.GetProperty("deaths").GetInt32(),
                Assists = summoner.GetProperty("assists").GetInt32(),
                TotalCS = GetTotalCS(summoner),
                GameType = _queueIdGameType[matchInfo.GetProperty("queueId").GetInt32()]
            };

            return matchDetail;
        }

        private List<List<string>> BuildParticipants(JsonElement participants, string riotImgUrl)
        {
            return participants.EnumerateArray()
                .Select(p => new List<string> {
                    $"{riotImgUrl}/champion/{p.GetProperty("championName").GetString()}.png",
                    p.GetProperty("riotIdGameName").GetString() ?? ""
                })
                .ToList();
        }

        private async Task<List<string>> BuildSpellsAsync(JsonElement summoner, string version)
        {
            var spells = new List<string>();

            for (int i = 1; i <= 2; i++)
            {
                var spellId = summoner.GetProperty($"summoner{i}Id").GetInt32();
                var spellUrl = await GetSpellImageUrlAsync(spellId, version);
                if (spellUrl != null)
                    spells.Add(spellUrl);
            }

            return spells;
        }

        private List<string> BuildItems(JsonElement summoner, string riotImgUrl)
        {
            var items = new List<string>();

            for (int i = 0; i < 7; i++)
            {
                var itemId = summoner.GetProperty($"item{i}").GetInt32();
                items.Add(itemId > 0 ? $"{riotImgUrl}/item/{itemId}.png" : "");
            }

            return items;
        }

        private int GetTotalCS(JsonElement summoner)
        {
            return summoner.GetProperty("totalMinionsKilled").GetInt32() +
                   summoner.GetProperty("neutralMinionsKilled").GetInt32();
        }

        private string FormatGameDate(long timestamp)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime.ToString("MM/dd");
        }

        private string FormatGameDuration(int seconds)
        {
            return TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
        }

        public async Task<string?> GetLatestVersionAsync()
        {
            var response = await GetJsonAsync($"{_ddragonUrl}/api/versions.json");
            return response?.EnumerateArray().FirstOrDefault().GetString();
        }

        public async Task<string?> GetSpellImageUrlAsync(int spellId, string version)
        {
            var response = await GetJsonAsync($"{_ddragonUrl}/cdn/{version}/data/ko_KR/summoner.json");
            if (response == null) return null;

            var spells = response.Value.GetProperty("data");

            foreach (var spell in spells.EnumerateObject())
            {
                var value = spell.Value;
                if (value.GetProperty("key").GetString() == spellId.ToString())
                {
                    var imageName = value.GetProperty("image").GetProperty("full").GetString();
                    return $"{_ddragonUrl}/cdn/{version}/img/spell/{imageName}";
                }
            }
            return null;
        }

        private async Task<JsonElement?> GetJsonAsync(string url)
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(content).RootElement;
        }
    }
}