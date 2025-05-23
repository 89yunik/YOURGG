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
            ViewBag.ErrorMessage = "소환사를 찾을 수 없습니다.";
            return View(new MatchDetailViewModel());
        }

        if (!result.IsMatchFound)
        {
            ViewBag.ErrorMessage = "매치 데이터를 불러올 수 없습니다.";
            return View(new MatchDetailViewModel { SummonerName = summonerName });
        }

        return View(result.MatchDetail);
    }
    
    [HttpGet]
    public IActionResult Error()
    {
        return View();
    }
}