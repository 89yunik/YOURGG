namespace YOURGG.Models
{
    public class MatchDetailResult
    {
        public bool IsSummonerFound { get; set; }
        public bool IsMatchFound { get; set; }
        public MatchDetailViewModel? MatchDetail { get; set; }
    }
}