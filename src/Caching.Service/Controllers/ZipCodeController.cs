
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Caching.Service.Controllers;
public class ZipCodeController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ZipCodeController> _logger;
    private static readonly Random _random = new();

    public ZipCodeController(ApplicationDbContext context, ILogger<ZipCodeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("zipcode/{code}")]
    public async Task<IActionResult> Get([FromRoute] string code)
    {
        _logger.LogInformation("Attempting to find {type}, for zip code {code}", nameof(CityData), code);
        await Task.Delay(_random.Next(300, 800));
        return Ok(await _context.CityData.FirstAsync(c => c.ZipCode == code));
    }
}
