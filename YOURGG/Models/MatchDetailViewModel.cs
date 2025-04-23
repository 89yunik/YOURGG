namespace YOURGG.Models
{
    public class MatchDetailViewModel
    {
        public required string SummonerName { get; set; }
        public required string ChampionName { get; set; }
        public required string Result { get; set; }
        public required int Kills { get; set; }
        public required int Deaths { get; set; }
        public required int Assists { get; set; }
        public required string GameMode { get; set; }
    }
}