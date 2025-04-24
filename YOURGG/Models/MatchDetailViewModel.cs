namespace YOURGG.Models
{
    public class MatchDetailViewModel
    {
        public string? SummonerName { get; set; }
        public string? ChampionName { get; set; }
        public string? Result { get; set; }
        public string? Role { get; set; }
        public int? Kills { get; set; }
        public int? Deaths { get; set; }
        public int? Assists { get; set; }
        public string? GameMode { get; set; }
        
    }
}