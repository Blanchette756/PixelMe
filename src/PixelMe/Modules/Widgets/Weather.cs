using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using PixelMe.Infrastructure;

namespace PixelMe.Modules.Widgets;

public sealed record WeatherReport(string Place, double Temperature, int Code, DateTimeOffset FetchedAt);

public static class Weather
{
    public static async Task<WeatherReport?> FetchAsync(string city)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return null;
        }

        try
        {
            var geocode = $"https://geocoding-api.open-meteo.com/v1/search?count=1&name={Uri.EscapeDataString(city.Trim())}";
            using var place = JsonDocument.Parse(await Http.Client.GetStringAsync(geocode));
            if (!place.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            {
                return null;
            }

            var first = results[0];
            var latitude = first.GetProperty("latitude").GetDouble().ToString(CultureInfo.InvariantCulture);
            var longitude = first.GetProperty("longitude").GetDouble().ToString(CultureInfo.InvariantCulture);
            var name = first.GetProperty("name").GetString() ?? city;

            var forecast = $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,weather_code";
            using var current = JsonDocument.Parse(await Http.Client.GetStringAsync(forecast));
            var now = current.RootElement.GetProperty("current");
            return new WeatherReport(name, now.GetProperty("temperature_2m").GetDouble(), now.GetProperty("weather_code").GetInt32(), DateTimeOffset.Now);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Log.Warn("Weather could not be fetched", ex);
            return null;
        }
    }

    public static string GlyphFor(int code) => code switch
    {
        0 => "",
        1 or 2 => "",
        3 => "",
        45 or 48 => "",
        >= 51 and <= 67 or >= 80 and <= 82 => "",
        >= 71 and <= 77 or 85 or 86 => "",
        >= 95 => "",
        _ => "",
    };
}
