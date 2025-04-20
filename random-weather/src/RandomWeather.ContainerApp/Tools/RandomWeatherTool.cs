using System.ComponentModel;

using ModelContextProtocol.Server;

using RandomWeather.ContainerApp.Models;

namespace RandomWeather.ContainerApp.Tools;

[McpServerToolType]
public class RandomWeatherTool(ILoggerFactory loggerFactory)
{
    private readonly ILogger<RandomWeatherTool> _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<RandomWeatherTool>();

    private static Random random = Random.Shared;
    private static string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

    [McpServerTool(Name = "get_random_weather", Title = "Get Random Weather")]
    [Description("Gets the list of random weather in the next given days in a given city.")]
    public async Task<WeatherResponse> GetRandomWeatherAsync(
        [Description("The name of the city.")] string city,
        [Description("The number of days for the weather forecast.")] int days = 5
    )
    {
        await Task.Delay(1000); // Simulate some async work

        var forecast = Enumerable.Range(1, days).Select(index =>
            new WeatherForecast
            (
                DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                random.Next(-20, 55),
                summaries[random.Next(summaries.Length)]
            ))
            .ToArray();

        var response = new WeatherResponse { City = city, Forecasts = forecast };
        return response;
    }
}
