using YOURGG.Models;

namespace YOURGG.Services
{
    public class RiotApiService
    {
        private readonly HttpClient _http;

        public RiotApiService(HttpClient http)
        {
            _http = http;
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
}