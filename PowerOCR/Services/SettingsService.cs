using System;
using System.IO;
using System.Text.Json;
using PowerOCR.Settings;

namespace PowerOCR.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string AppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PowerOCR");

    public static string SettingsFilePath =>
        Path.Combine(AppDataDirectory, "appsettings.json");

    public AppSettings Settings { get; private set; } = new();
    public LlmConfig LlmConfig { get; private set; } = new();

    public void Load()
    {
        LlmConfig = LlmConfig.Load();

        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    Settings = settings;
                    return;
                }
            }

            // If appdata config doesn't exist, check app directory for default appsettings.json
            var appDirSettings = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(appDirSettings))
            {
                var json = File.ReadAllText(appDirSettings);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    Settings = settings;
                    Save(); // Save a copy to AppData
                    return;
                }
            }

            // Fallback: Default settings and save
            Settings = new AppSettings();
            Save();
        }
        catch (Exception)
        {
            // If corrupt or inaccessible, fallback to defaults
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(AppDataDirectory))
            {
                Directory.CreateDirectory(AppDataDirectory);
            }

            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Best effort save
        }
    }
}
