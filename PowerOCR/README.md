# PowerOCR (Text Extractor) — Developer Guide

PowerOCR is a standalone, high-performance WinUI 3 desktop application that enables instant, freeze-frame region capture, offline Windows OCR extraction, and background LLM post-processing for markdown tables, code syntax restoration, and OCR noise cleanup.

---

## Architecture Overview

```text
┌───────────────────────────────────────────────────────────────────────────────┐
│                           MainWindow (Hidden Anchor)                          │
│   - Keeps process alive in background (WS_EX_TOOLWINDOW)                     │
│   - Hooks WM_HOTKEY (0x0312) via SetWindowSubclass (Win + Shift + T)         │
│   - Hosts DispatcherQueue for thread-safe UI & clipboard operations           │
└──────────────────────────────────────┬────────────────────────────────────────┘
                                       │ Hotkey Triggered
                                       ▼
┌───────────────────────────────────────────────────────────────────────────────┐
│                           ScreenCaptureHelper                                 │
│   - Queries all monitors via EnumDisplayMonitors & GetDpiForMonitor           │
│   - Captures instant 32bpp GDI freeze-frame before spawning overlay UI       │
│   - Handles complex multi-monitor layouts (negative virtual coordinates)      │
└──────────────────────────────────────┬────────────────────────────────────────┘
                                       │ Freeze-frame captures ready
                                       ▼
┌───────────────────────────────────────────────────────────────────────────────┐
│                           CaptureOverlayWindow                                │
│   - Borderless, topmost WinUI 3 window for each active display               │
│   - Dimmed screenshot background with DPI-scaled pointer selection marquee   │
│   - Dismissible via Escape key (KeyboardAccelerator) or right-click           │
└──────────────────────────────────────┬────────────────────────────────────────┘
                                       │ Region Selected
                                       ▼
┌───────────────────────────────────────────────────────────────────────────────┐
│                           OcrPipelineManager                                  │
│                                                                               │
│  [STAGE 1: Immediate Local OCR]                                               │
│  1. Extracts text via OcrHelper (Windows.Media.Ocr.OcrEngine).               │
│  2. Copies raw text immediately to Windows Clipboard.                         │
│  3. Closes all overlay windows instantly.                                     │
│                                                                               │
│  [STAGE 2: Background LLM Formatting]                                         │
│  1. Fires LlmFormattingService in Task.Run (Ollama / OpenAI / Gemini).        │
│  2. Enforces strict 5-second timeout; silently falls back on failure.         │
│  3. Verifies clipboard was not modified by user before overwriting.          │
│  4. Updates clipboard with cleaned Markdown/code on UI DispatcherQueue.      │
│  5. Shows Windows Toast notification with interactive "Undo" button.         │
└───────────────────────────────────────────────────────────────────────────────┘
```

---

## Directory Structure

```text
PowerOCR/
├── App.xaml / App.xaml.cs             # Application entrypoint & toast notification lifecycle
├── MainWindow.xaml / .cs              # Hidden anchor window, Win32 subclass, hotkey dispatcher
├── Helpers/
│   ├── OcrHelper.cs                   # GDI to WinRT IRandomAccessStream & OcrEngine bridge
│   └── ScreenCaptureHelper.cs         # Win32 monitor enumeration & freeze-frame capture
├── Interop/
│   └── NativeMethods.cs               # P/Invoke signatures (User32, Comctl32, Shcore)
├── Services/
│   ├── HotkeyManager.cs               # Win32 RegisterHotKey and SetWindowSubclass hook
│   ├── LlmFormattingService.cs        # Singleton HttpClient, 5s timeout, system prompt
│   ├── NotificationService.cs         # Windows Toast builder (AppNotificationBuilder)
│   ├── OcrPipelineManager.cs          # Dual-stage pipeline, concurrency, Undo handler
│   ├── OverlayCoordinator.cs          # Multi-window coordinator & screenshot distributor
│   └── SettingsService.cs             # AppData settings persistence
├── Settings/
│   ├── AppSettings.cs                 # Hotkey & language preferences model
│   └── LlmConfig.cs                   # LLM provider, endpoint, model, and API key model
├── UI/
│   ├── CaptureOverlayWindow.xaml      # XAML layout (Image -> Tint -> Selection Canvas)
│   └── CaptureOverlayWindow.xaml.cs   # DPI selection math & pointer interaction
└── appsettings.json                   # Default application settings
```

---

## Prerequisites

- **OS**: Windows 10 (Build 17763+) or Windows 11 (Developer Mode enabled)
- **.NET SDK**: 10.0+ (`net10.0-windows10.0.26100.0`)
- **Windows App SDK**: 1.8+
- **WinApp CLI**: 0.6+ (`winapp`)

---

## Build & Run

### 1. Build with .NET CLI
```powershell
cd PowerOCR
dotnet build -c Debug
```

### 2. Run with WinApp CLI (Attached Debug Stream)
```powershell
winapp run . --debug-output
```
*Attaches live debugger output streaming, capturing `OutputDebugString`, stdout, and WinUI stowed-exception triage.*

### 3. Automated End-to-End Diagnostic Test
```powershell
winapp run . --debug-output --args "--test-pipeline"
```
*Automatically draws a synthetic text bitmap, runs it through the WinRT OCR engine, copies raw text to clipboard, executes the LLM formatting service, updates the clipboard with enhanced Markdown, and displays diagnostic logs.*

### 4. Stop Running Processes
```powershell
Stop-Process -Name "PowerOCR" -Force -ErrorAction SilentlyContinue
```

---

## Configuration

### General Settings
Stored in `%APPDATA%\PowerOCR\appsettings.json`:
```json
{
  "Hotkey": {
    "Modifiers": "Win+Shift",
    "Key": "T"
  },
  "PreferredLanguage": ""
}
```

### LLM Settings
Stored in `%LOCALAPPDATA%\PowerOCR\llm_settings.json`:
```json
{
  "Provider": "OpenAI",
  "Endpoint": "https://api.openai.com/v1/chat/completions",
  "Model": "gpt-4o-mini",
  "ApiKey": ""
}
```

#### Supported LLM Endpoints:
- **Local Ollama (Native)**: `http://localhost:11434/api/generate` (Model: `llama3.2`)
- **Local Ollama (OpenAI API)**: `http://localhost:11434/v1/chat/completions` (Model: `llama3.2`)
- **OpenAI**: `https://api.openai.com/v1/chat/completions` (Model: `gpt-4o-mini`)
- **Google Gemini**: `https://generativelanguage.googleapis.com/v1beta/openai/chat/completions` (Model: `gemini-2.5-flash`)

---

## User Controls & Keybindings

| Key / Action | Context | Result |
|---|---|---|
| <kbd>Win</kbd> + <kbd>Shift</kbd> + <kbd>O</kbd> | Global | Freezes all displays and activates selection overlay |
| Left-Click + Drag | Overlay | Draws rectangular selection marquee |
| Left-Click Release | Overlay | Crops selected region, extracts OCR, and closes overlay |
| <kbd>Escape</kbd> | Overlay | Dismisses capture overlay immediately |
| Right-Click | Overlay | Dismisses capture overlay immediately |
| Click "Undo" on Toast | Post-Capture | Reverts clipboard to raw, unformatted OCR text |
