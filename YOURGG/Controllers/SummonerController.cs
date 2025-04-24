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
        MatchDetailResult result = await _riotApiService.GetLatestLiftMatchDetailBySummonerNameAsync(summonerName);

        if (!result.IsSummonerFound)
        {
            ViewBag.ErrorMessage = "해당 소환사를 찾을 수 없습니다.";
            return View(new MatchDetailViewModel());
        }

        if (!result.IsMatchFound)
        {
            ViewBag.ErrorMessage = "최근 협곡 매치 정보를 찾을 수 없습니다.";
            return View(new MatchDetailViewModel { SummonerName = summonerName });
        }

        return View(result.MatchDetail);
    }
}