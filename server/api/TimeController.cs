using Microsoft.AspNetCore.Mvc;
using service;

namespace api;

public class TimeController
{

    private AccuralService accuralService;

    public TimeController(AccuralService accuralService)
    {
        this.accuralService = accuralService;
    }

    [HttpPost(nameof(next_month))]
    public void next_month()
    {
        Console.WriteLine("sup");
    }
}