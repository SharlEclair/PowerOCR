# Text Extractor (PowerOCR)

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/text-extractor)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Text%20Extractor%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20label%3A%22Product-Text%20Extractor%22)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3A%22Product-Text+Extractor%22)

---

## Overview

**Text Extractor (PowerOCR)** is a high-performance Windows productivity utility that enables users to extract, clean, format, and copy text, tables, and code from anywhere on their screen.

In addition to local offline OCR powered by Windows WinRT OCR APIs, PowerOCR includes:
1. **AI Text Clean Mode (`AI` / `A`)**: Reconstructs broken code syntax, formats messy columnar text into Markdown tables, and fixes OCR typos using fine-tuned Gemini/OpenAI-compatible LLM prompts.
2. **Direct Gemini Vision OCR Mode (`Vision` / `V`)**: Multimodal screen crop analysis that bypasses local OCR engine limitations to transcribe complex layouts, code, math, and tables directly from screenshot pixels with extreme fidelity.
3. **Non-Intrusive Floating HUD Toast**: Modern Windows 11 Acrylic pill (`OcrStatusToast`) that displays live status indicators without stealing window focus.

---

## Operating Modes

| Mode | Trigger / Hotkey | Description | Best For |
|---|---|---|---|
| **Standard OCR** | Default (Both toggles OFF) | 100% offline local Windows WinRT OCR engine. | Plain prose, simple sentences, offline environments. |
| **AI Text Clean** | `AI` Button / **`A`** | Local OCR $\rightarrow$ Gemini Text LLM cleanup with strict fidelity rules. | Restoring code indentation, repairing word-wrap, fixing OCR noise. |
| **Direct Vision OCR** | `Vision` Button / **`V`** | Direct Screen Crop $\rightarrow$ Gemini Multimodal API (PNG base64). | Complex tables, math formulas, code from videos/images, unusual fonts. |
| **Single Line** | `Single Line` / **`S`** | Formats extracted text into a single continuous line. | URLs, paths, single-line commands. |
| **Table Mode** | `Table` / **`T`** | Tab-delimits columnar layout. | Pasting into spreadsheets (Excel, Google Sheets). |

---

## Architecture & Components

```
┌─────────────────────────────────────────────────────────────────┐
│                          OCROverlay                             │
│     (Fullscreen WPF STA Overlay Canvas with Toolbar & Hotkeys)   │
└──────────────┬───────────────────────────────────┬──────────────┘
               │ (Standard / AI Mode)              │ (Vision Mode)
               ▼                                   ▼
┌──────────────────────────────┐     ┌────────────────────────────┐
│   Windows WinRT OCR Engine   │     │  ImageMethods Crop Capture │
│ (Local offline text extract) │     │ (Selected Region to Bitmap)│
└──────────────┬───────────────┘     └─────────────┬──────────────┘
               │                                   │
               ▼                                   ▼
┌─────────────────────────────────────────────────────────────────┐
│                   OcrPostProcessingPipeline                     │
│  - Copies immediate fallback text to clipboard                  │
│  - Displays floating HUD status pill (OcrStatusToast)           │
│  - Dispatches non-blocking async network calls                  │
└──────────────┬───────────────────────────────────┬──────────────┘
               │                                   │
               ▼                                   ▼
┌──────────────────────────────┐     ┌────────────────────────────┐
│   LlmPostProcessor (Text)    │     │   LlmPostProcessor (Vision)│
│  - Strict Fidelity Prompt    │     │  - Multimodal Base64 Image │
│  - No Math / No Mutation     │     │  - Direct Pixel OCR Engine │
└──────────────┬───────────────┘     └─────────────┬──────────────┘
               │                                   │
               └─────────────────┬─────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Windows Clipboard                          │
│     (Updated with cleaned, formatted result + Success Toast)    │
└─────────────────────────────────────────────────────────────────┘
```

### Core Components:
- **`OCROverlay.xaml / .cs`**: Topmost WPF fullscreen canvas supporting multi-monitor bounds, DPI scaling, selection drawing, keyboard shortcuts (`A`, `V`, `S`, `T`, `1-9`), and language selection.
- **`OcrPostProcessingPipeline.cs`**: Orchestrates fallback clipboard assignment, background task scheduling, timeout controls (30s), and HUD toast updates.
- **`LlmPostProcessor.cs`**: Service managing OpenAI-compatible and Google Gemini REST endpoints, connection pooling via `SocketsHttpHandler`, prompt sanitization, and multimodal image payloads.
- **`OcrStatusToast.xaml / .cs`**: Lightweight acrylic status pill rendered with `WS_EX_NOACTIVATE` to notify the user of AI/Vision completion without stealing focus from active editors.
- **`ImageMethods.cs`**: Screen capture helpers, memory-safe bitmap padding, and cropped region extraction.

---

## Configuration & Environment Variables

PowerOCR reads settings from environment variables (User or Process scope):

| Variable | Description | Default |
|---|---|---|
| `GEMINI_API_KEY` | Google Gemini API key for AI Clean and Vision OCR. | None |
| `TEXT_EXTRACTOR_LLM_MODEL` | LLM model identifier. | `gemini-flash-lite-latest` |
| `TEXT_EXTRACTOR_LLM_ENDPOINT` | Custom OpenAI-compatible or local endpoint URL. | Gemini API endpoint |
| `TEXT_EXTRACTOR_LLM_API_KEY` | Generic OpenAI/custom endpoint API key. | `GEMINI_API_KEY` value |

### Example Setup (PowerShell):
```powershell
# Set permanent User environment variables (replace placeholder with your key)
[System.Environment]::SetEnvironmentVariable("GEMINI_API_KEY", "<your-gemini-api-key>", "User")
[System.Environment]::SetEnvironmentVariable("TEXT_EXTRACTOR_LLM_MODEL", "gemini-flash-lite-latest", "User")
```

---

## Building & Running Standalone

### Build:
```powershell
powershell -ExecutionPolicy Bypass -File "tools\build\build.ps1" -Path "src\modules\PowerOCR\PowerOCR" -Platform x64 -Configuration Debug "/p:SpectreMitigation=false"
```

### Run:
```powershell
.\x64\Debug\PowerToys.PowerOCR.exe
```
