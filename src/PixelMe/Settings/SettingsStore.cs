using System.Text.Json;
using System.Text.Json.Serialization;
using PixelMe.Infrastructure;

namespace PixelMe.Settings;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private SettingsStore(string filePath, AppSettings current)
    {
        FilePath = filePath;
        Current = current;
    }

    public event Action<AppSettings, AppSettings>? Changed;

    public string FilePath { get; }

    public AppSettings Current { get; private set; }

    public static SettingsStore Load()
    {
        var path = AppPaths.SettingsFile;
        AppSettings settings;
        try
        {
            settings = File.Exists(path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception ex)
        {
            Log.Warn("Settings file is unreadable, defaults are used instead", ex);
            Backup(path);
            settings = new AppSettings();
        }

        settings.Normalize();
        return new SettingsStore(path, settings);
    }

    public void Update(Action<AppSettings> change)
    {
        var previous = Current;
        var next = Clone(previous);
        change(next);
        next.Normalize();
        Current = next;
        Save();
        Changed?.Invoke(previous, next);
    }

    private static AppSettings Clone(AppSettings source)
    {
        var json = JsonSerializer.Serialize(source, JsonOptions);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
    }

    private static void Backup(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Copy(path, path + ".broken", overwrite: true);
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Could not back up the broken settings file", ex);
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Current, JsonOptions));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Error("Saving settings failed", ex);
        }
    }
}
