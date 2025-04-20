namespace RandomWeather.ContainerApp.Models;

public class WeatherResponse
{
    public string? City { get; set; }
    public IEnumerable<WeatherForecast>? Forecasts { get; set; }
}