---
name: powertoys-settings-ui
description: Architecture, IPC contracts, and WinUI schema patterns for adding custom utility settings to the PowerToys Settings UI app.
---

# PowerToys Settings UI & IPC Integration Guide

## 1. Overview
PowerToys configuration is managed through a central WinUI Settings app (`src/settings-ui/`) communicating with runner and modules via Named Pipes and JSON schemas.

## 2. Settings File Architecture
- User settings are persisted in `%LOCALAPPDATA%\Microsoft\PowerToys\TextExtractor\settings.json`.
- Settings structure:
```json
{
  "properties": {
    "ActivationShortcut": { "value": "Win + Shift + T" },
    "PreferredLanguage": { "value": "English (United States)" },
    "EnableLlmCleanup": { "value": true },
    "LlmModel": { "value": "gemini-2.5-flash-lite" }
  }
}
```

## 3. Runner & Settings IPC Rules
- Always update both `src/runner/` and `src/settings-ui/` when adding new IPC properties or settings fields.
- Use `ThrottledActionInvoker` to debounced-save settings updates to disk.
- Retain backwards compatibility when reading older `settings.json` schemas.
