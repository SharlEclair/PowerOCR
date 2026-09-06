# PowerOCR — Intelligent AI & Vision-Powered Text Extractor

**PowerOCR** is an enhanced, intelligent screen text and layout extractor for Windows. It combines 100% offline local OCR via Windows WinRT APIs with cloud-accelerated **Direct Gemini Vision OCR** and **AI Code/Table Reconstruction**.

---

## ✨ Features

- **⚡ Direct Gemini Vision OCR (`Vision` / `V`)**: Multimodal screen crop analysis that bypasses local OCR engine limitations. Transcribes code, complex tables, mathematical formulas, and terminal output directly from screenshot pixels with extreme fidelity.
- **🤖 AI Text Clean Mode (`AI` / `A`)**: Reconstructs broken indentation, joins wrapped lines within sentences, fixes OCR glyph typos (e.g. `down1oad` $\rightarrow$ `download`, slashed `Ø` $\rightarrow$ `0`), and formats tables into clean Markdown without modifying numeric values or JSON hierarchies.
- **🔔 Fluent Floating Acrylic HUD (`OcrStatusToast`)**: Modern, non-intrusive Windows 11 status pill rendered with `WS_EX_NOACTIVATE` that displays live processing updates without stealing window focus from your active editor.
- **🔒 100% Privacy by Default**: When `AI` and `Vision` modes are turned off, the tool operates 100% offline using the built-in Windows WinRT OCR engine with zero network traffic.
- **🛡️ Extreme Data Fidelity**: System prompts are fine-tuned to never invent values, never calculate numbers, never truncate keys, and never rewrite code syntax.

---

## 📊 Modes Comparison

| Mode | Shortcut | How It Works | Best For | Privacy / Network |
|---|---|---|---|---|
| **Standard OCR** | Default (All off) | Windows WinRT OCR Engine | Plain English prose, quick text grabs | 100% Offline (No network) |
| **AI Text Clean** | `AI` / **`A`** | Local OCR $\rightarrow$ Gemini Text LLM cleanup | Code formatting, table alignment, typo fixing | Network required for cleanup |
| **Direct Vision OCR** | `Vision` / **`V`** | Screenshot Crop $\rightarrow$ Gemini Multimodal Vision API | Math, complex grids, JSON, syntax from images | Network required for vision |
| **Single Line** | **`S`** | Formats text into a continuous single line | URLs, paths, terminal one-liners | Local / Offline |
| **Table Mode** | **`T`** | Tab-delimit columnar layout | Pasting into Excel or Google Sheets | Local / Offline |

---

## ⌨️ Shortcuts & Hotkeys

While the selection overlay is active:

| Key | Action |
|---|---|
| **`A`** | Toggle **AI Text Clean** mode ON / OFF |
| **`V`** | Toggle **Direct Gemini Vision OCR** mode ON / OFF |
| **`S`** | Toggle **Single Line** mode |
| **`T`** | Toggle **Table** mode |
| **`1 - 9`** | Switch OCR recognition language |
| **`Esc`** | Cancel and close overlay |

---

## 🏗️ Architecture

```
                               ┌───────────────────────────┐
                               │   Win + Shift + T Hotkey  │
                               └─────────────┬─────────────┘
                                             │
                                             ▼
                               ┌───────────────────────────┐
                               │        OCROverlay         │
                               │  (Fullscreen WPF Canvas)  │
                               └───────┬───────────┬───────┘
                                       │           │
                     ┌─────────────────┘           └──────────────────┐
                     │ (Standard / AI Mode)                           │ (Vision Mode)
                     ▼                                                ▼
       ┌───────────────────────────┐                    ┌───────────────────────────┐
       │   Windows WinRT OCR       │                    │   ImageMethods Crop       │
       │  (Local Text Extraction)  │                    │  (PNG Base64 Encoding)    │
       └─────────────┬─────────────┘                    └─────────────┬─────────────┘
                     │                                                │
                     └─────────────────┬──────────────────────────────┘
                                       │
                                       ▼
                       ┌───────────────────────────────┐
                       │   OcrPostProcessingPipeline   │
                       │  - Fast Fallback Clipboard    │
                       │  - Acrylic HUD Pill Toast     │
                       │  - 30s Timeout Protection     │
                       └───────────────┬───────────────┘
                                       │
                                       ▼
                       ┌───────────────────────────────┐
                       │       LlmPostProcessor        │
                       │  - gemini-flash-lite-latest   │
                       │  - OpenAI-Compatible REST     │
                       │  - Extreme Fidelity Prompts   │
                       └───────────────┬───────────────┘
                                       │
                                       ▼
                       ┌───────────────────────────────┐
                       │       Windows Clipboard       │
                       │   (Updated with Clean Text)   │
                       └───────────────────────────────┘
```

---

## 🛠️ Configuration

Configure your AI provider using Windows User Environment Variables:

### Option A: Google Gemini (Recommended)
```powershell
# Set permanent User environment variables (replace placeholder with your key)
[System.Environment]::SetEnvironmentVariable("GEMINI_API_KEY", "<your-gemini-api-key>", "User")
[System.Environment]::SetEnvironmentVariable("TEXT_EXTRACTOR_LLM_MODEL", "gemini-flash-lite-latest", "User")
```

### Option B: Local LLM (Ollama, LocalAI, vLLM)
```powershell
[System.Environment]::SetEnvironmentVariable("TEXT_EXTRACTOR_LLM_ENDPOINT", "http://localhost:11434/v1/chat/completions", "User")
[System.Environment]::SetEnvironmentVariable("TEXT_EXTRACTOR_LLM_MODEL", "llama3", "User")
```

### Option C: OpenAI API
```powershell
[System.Environment]::SetEnvironmentVariable("TEXT_EXTRACTOR_LLM_API_KEY", "<your-openai-api-key>", "User")
[System.Environment]::SetEnvironmentVariable("TEXT_EXTRACTOR_LLM_MODEL", "gpt-4o-mini", "User")
```

---

## 🚀 Building & Running

### 1. Build PowerOCR:
```powershell
powershell -ExecutionPolicy Bypass -File "tools\build\build.ps1" -Path "src\modules\PowerOCR\PowerOCR" -Platform x64 -Configuration Debug "/p:SpectreMitigation=false"
```

### 2. Launch Standalone:
```powershell
.\x64\Debug\PowerToys.PowerOCR.exe
```

### 3. Autostart on Windows Login:
To have PowerOCR run automatically in the background on startup:

```powershell
$startupDir = [System.Environment]::GetFolderPath("Startup")
$vbsPath = Join-Path $startupDir "PowerToys_TextExtractor_Startup.vbs"
$exePath = (Resolve-Path ".\x64\Debug\PowerToys.PowerOCR.exe").Path

$vbsContent = @"
Set WshShell = CreateObject("WScript.Shell")
WshShell.Run """$exePath""", 0, False
"@

Set-Content -Path $vbsPath -Value $vbsContent
Write-Host "Autostart configured at: $vbsPath"
```

---

## 🔍 Troubleshooting & Diagnostics

- **Live Logs**: PowerOCR logs all startup events, hotkey registrations, OCR recognition results, and LLM requests real-time to:
  `$env:TEMP\PowerToys_TextExtractor_Debug.log`
  ```powershell
  Get-Content "$env:TEMP\PowerToys_TextExtractor_Debug.log" -Wait -Tail 30
  ```
- **Language Packs**: If local OCR fails or shows 0 available languages, install the Windows OCR language pack for your language via Windows Settings $\rightarrow$ Time & Language $\rightarrow$ Language.
- **Process Cleanup**: If a previous instance is running, close it via:
  ```powershell
  Stop-Process -Name "PowerToys.PowerOCR" -Force -ErrorAction SilentlyContinue
  ```
