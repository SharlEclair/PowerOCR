# PowerOCR — AI & Vision-Powered Text Extractor

**PowerOCR** is an enhanced, intelligent screen text and layout extractor for Windows. It provides both 100% offline local OCR via Windows WinRT APIs and cloud-accelerated **Gemini Vision OCR** and **AI Code/Table Cleanup**.

---

## ✨ Features

- **⚡ Direct Gemini Vision OCR (`Vision` / `V`)**: Multimodal screen crop analysis that bypasses local OCR engine limitations. Transcribes code, complex tables, mathematical formulas, and terminal output directly from pixels with extreme fidelity.
- **🤖 AI Text Clean Mode (`AI` / `A`)**: Reconstructs broken indentation, joins wrapped lines within sentences, fixes OCR glyph typos (e.g. `down1oad` $\rightarrow$ `download`, slashed `Ø` $\rightarrow$ `0`), and formats tables into clean Markdown without modifying numeric values or JSON hierarchies.
- **🔔 Fluent Floating Acrylic HUD**: Lightweight, non-intrusive Windows 11 status pill (`OcrStatusToast`) that displays live processing updates without stealing window focus.
- **🔒 100% Privacy by Default**: When `AI` and `Vision` modes are turned off, the tool works 100% offline using local Windows WinRT OCR with zero network traffic.

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

## 🛠️ Configuration

Configure your AI provider using environment variables:

```powershell
# Recommended: Set permanent User environment variables
[System.Environment]::SetEnvironmentVariable("GEMINI_API_KEY", "<your-gemini-api-key>", "User")
[System.Environment]::SetEnvironmentVariable("TEXT_EXTRACTOR_LLM_MODEL", "gemini-flash-lite-latest", "User")
```

Custom / Local endpoints (Ollama, LocalAI, vLLM) are also supported:
```powershell
[System.Environment]::SetEnvironmentVariable("TEXT_EXTRACTOR_LLM_ENDPOINT", "http://localhost:11434/v1/chat/completions", "User")
[System.Environment]::SetEnvironmentVariable("TEXT_EXTRACTOR_LLM_MODEL", "llama3", "User")
```

---

## 🚀 Building & Running

### Build:
```powershell
powershell -ExecutionPolicy Bypass -File "tools\build\build.ps1" -Path "src\modules\PowerOCR\PowerOCR" -Platform x64 -Configuration Debug "/p:SpectreMitigation=false"
```

### Launch Standalone:
```powershell
.\x64\Debug\PowerToys.PowerOCR.exe
```

### Autostart on Windows Login:
Create a silent background launcher in your Windows Startup directory (`shell:startup`) so PowerOCR starts silently on boot listening for `Win + Shift + T`.
