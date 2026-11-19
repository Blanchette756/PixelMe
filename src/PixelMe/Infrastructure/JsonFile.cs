using System.Text.Json;
using System.Text.Json.Serialization;

namespace PixelMe.Infrastructure;

public static class JsonFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static T Load<T>(string name)
        where T : new()
    {
        var path = PathFor(name);
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? new T()
                : new T();
        }
        catch (Exception ex)
        {
            Log.Warn($"Data file '{name}' is unreadable, starting empty", ex);
            return new T();
        }
    }

    public static void Save<T>(string name, T value)
    {
        var path = PathFor(name);
        try
        {
            Directory.CreateDirectory(AppPaths.RoamingFolder);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Error($"Saving data file '{name}' failed", ex);
        }
    }

    private static string PathFor(string name) => Path.Combine(AppPaths.RoamingFolder, name + ".json");
}
