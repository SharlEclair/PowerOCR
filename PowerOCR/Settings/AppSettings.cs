using System.Text.Json.Serialization;

namespace PowerOCR.Settings;

public sealed class AppSettings
{
    [JsonPropertyName("Hotkey")]
    public HotkeyConfig Hotkey { get; set; } = new();

    [JsonPropertyName("Llm")]
    public LlmConfig Llm { get; set; } = new();

    [JsonPropertyName("Ocr")]
    public OcrConfig Ocr { get; set; } = new();
}

public sealed class HotkeyConfig
{
    [JsonPropertyName("Modifiers")]
    public string Modifiers { get; set; } = "Win+Shift";

    [JsonPropertyName("Key")]
    public string Key { get; set; } = "O";
}



public sealed class OcrConfig
{
    [JsonPropertyName("Language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("ImmediateRawCopy")]
    public bool ImmediateRawCopy { get; set; } = true;

    [JsonPropertyName("PlaySound")]
    public bool PlaySound { get; set; } = true;
}
