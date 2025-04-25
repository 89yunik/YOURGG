namespace YOURGG.Models
{
    public class MatchDetailViewModel
    {
        public string? GameDate { get; set; }
        public string? GameDuration { get; set; }
        public string? SummonerName { get; set; }
        public string? ChampionImgUrl { get; set; }
        public int? ChampLevel { get; set; }
        public string? Result { get; set; }
        public List<List<string>>? Participants { get; set; }
        public List<string>? Spells { get; set; }
        public List<string>? Items { get; set; }
        public int? Kills { get; set; }
        public int? Deaths { get; set; }
        public int? Assists { get; set; }
        public int? TotalCS { get; set; }
        public string? GameType { get; set; }
        
    }
}