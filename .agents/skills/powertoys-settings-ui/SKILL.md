---
name: powertoys-settings-ui
description: Architecture, IPC contracts, and WinUI schema patterns for configuring PowerOCR in PowerToys Settings UI.
---

# PowerToys Settings UI & PowerOCR Configuration Guide

## 1. Overview
PowerOCR utilizes a decoupled configuration architecture:
1. **Application Settings (`appsettings.json`)**: Configures the global activation hotkey and OCR language preference.
2. **Modular LLM Configuration (`llm_settings.json`)**: Configures the AI post-processing provider, endpoint, model, and authentication credentials.

## 2. Settings File Architectures

### General Settings (`%APPDATA%\PowerOCR\appsettings.json`)
Managed by `PowerOCR.Services.SettingsService`. Auto-seeded on first launch.
```json
{
  "Hotkey": {
    "Modifiers": "Win+Shift",
    "Key": "O"
  },
  "PreferredLanguage": ""
}
```

### LLM Settings (`%LOCALAPPDATA%\PowerOCR\llm_settings.json`)
Managed by `PowerOCR.Settings.LlmConfig`. Auto-seeded with OpenAI defaults.
```json
{
  "Provider": "OpenAI",
  "Endpoint": "https://api.openai.com/v1/chat/completions",
  "Model": "gpt-4o-mini",
  "ApiKey": ""
}
```

#### Supported Providers & Endpoint Defaults:
- **OpenAI**: `https://api.openai.com/v1/chat/completions` (Model: `gpt-4o-mini`)
- **Gemini**: `https://generativelanguage.googleapis.com/v1beta/openai/chat/completions` (Model: `gemini-2.5-flash`)
- **Ollama (Native)**: `http://localhost:11434/api/generate` (Model: `llama3.2`)
- **Ollama (OpenAI-compatible)**: `http://localhost:11434/v1/chat/completions` (Model: `llama3.2`)

## 3. Settings UI WinUI 3 Integration Patterns

When integrating PowerOCR into the PowerToys Settings UI (`src/settings-ui/`):
- **Activation Shortcut**: Bind to a WinUI 3 `ShortcutControl` mapping to `Hotkey.Modifiers` and `Hotkey.Key`.
- **LLM Provider Selector**: `ComboBox` bound to `Provider` (`OpenAI`, `Gemini`, `Ollama`, `Custom`).
- **Endpoint URL**: `TextBox` with validation for valid HTTP/HTTPS URI format.
- **Model Name**: `TextBox` or editable `ComboBox` with popular model recommendations (`gpt-4o-mini`, `gemini-2.5-flash`, `llama3.2`).
- **API Key**: `PasswordBox` with reveal button; serializes to `ApiKey` in `llm_settings.json`.

## 4. Hotkey Re-Registration on Settings Change
When the user updates settings in the UI:
1. Save the updated JSON to `%APPDATA%\PowerOCR\appsettings.json`.
2. Notify `PowerOCR.MainWindow` via named pipe or IPC signal.
3. `HotkeyManager.Register()` unregisters the old hotkey and registers the new key sequence on the persistent anchor window `HWND`.

