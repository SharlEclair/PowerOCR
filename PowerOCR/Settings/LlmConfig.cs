using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PowerOCR.Settings;

public sealed class LlmConfig
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    [JsonPropertyName("Provider")]
    public string Provider { get; set; } = "OpenAI";

    [JsonPropertyName("Endpoint")]
    public string Endpoint { get; set; } = "https://api.openai.com/v1/chat/completions";

    [JsonPropertyName("Model")]
    public string Model { get; set; } = "gpt-4o-mini";

    [JsonPropertyName("ApiKey")]
    public string ApiKey { get; set; } = string.Empty;

    public static string SettingsFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PowerOCR", "llm_settings.json");

    public static LlmConfig Load()
    {
        try
        {
            string path = SettingsFilePath;
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<LlmConfig>(json, JsonOptions);
                if (config != null)
                {
                    return config;
                }
            }
        }
        catch
        {
            // Silently fallback to default configuration on error
        }

        var defaultConfig = new LlmConfig();
        defaultConfig.Save();
        return defaultConfig;
    }

    public void Save()
    {
        try
        {
            string path = SettingsFilePath;
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch
        {
            // Best effort save
        }
    }
}
