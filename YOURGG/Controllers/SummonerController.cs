using Microsoft.AspNetCore.Mvc;
using YOURGG.Models;
using YOURGG.Services;

public class SummonerController : Controller
{
    private readonly RiotApiService _riotApiService;

    public SummonerController(RiotApiService riotApiService)
    {
        _riotApiService = riotApiService;
    }

    [HttpGet]
    public IActionResult Search()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> MatchDetail(string summonerName)
    {
        var matchDetail = await _riotApiService.GetLatestMatchDetailAsync(summonerName);

        if (matchDetail == null)
            return NotFound();

        return View(matchDetail);
    }

    [HttpGet("info")]
    public async Task<IActionResult> GetSummonerInfo(string gameName, string tagLine)
    {
        string? puuid = await _riotApiService.GetPuuidByRiotIdAsync(gameName, tagLine);
        if (puuid != null) 
        {
            List<string>? matchIds = await _riotApiService.GetMatchIdsByRiotPuuid(puuid, 1);
            return Ok(matchIds);
        }
        else return NotFound("Summoner not found or puuid is null.");
    }
}