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
        var puuid = await _riotApiService.GetPuuidByRiotIdAsync(gameName, tagLine);
        return Ok(puuid); // ← JSON 반환
    }
}