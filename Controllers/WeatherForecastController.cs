using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestIdentity.Identity.Filters;

namespace TestIdentity.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        [HttpGet]
        public IEnumerable<WeatherForecast> Get()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }

        [HttpGet("single")]
        [PermissionRequirement("ReadSingleForecast")]
        public WeatherForecast GetForecast()
        {
            var result = Enumerable.Range(1, 1).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            });
            return result.First();
        }

        [HttpPost]
        [PermissionRequirement("CreateForecast")]
        public IActionResult PostForecast()
        {
            return Created("/api/weatherforecast", new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now),
                TemperatureC = 10,
                Summary = "I wish it was this cold here"
            });
        }
    }
}
