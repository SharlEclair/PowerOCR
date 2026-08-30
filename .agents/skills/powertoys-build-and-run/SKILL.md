---
name: powertoys-build-and-run
description: Detailed procedures for building, testing, launching, and diagnosing the PowerToys Text Extractor (PowerOCR) module and its LLM post-processing pipeline.
---

# PowerToys Text Extractor Build & Diagnostics Guide

## 1. Environment & Prerequisites
- **Framework**: .NET 10.0 Windows SDK (`net10.0-windows10.0.26100.0`)
- **Native Dependencies**: NuGet packages stored in `packages\` (e.g. `Microsoft.Windows.CppWinRT`). If missing, restore with `tools\nuget.exe restore`.

## 2. Build Commands
Build the Text Extractor (PowerOCR) module individually without building the entire PowerToys solution:

```powershell
powershell -ExecutionPolicy Bypass -File "tools\build\build.ps1" -Path "src\modules\PowerOCR\PowerOCR" -Platform x64 -Configuration Debug "/p:SpectreMitigation=false"
```

## 3. Pre-Build Process Clean-up
If the output binary `x64\Debug\PowerToys.PowerOCR.exe` is locked by running processes during build, stop them first:

```powershell
Stop-Process -Name "PowerToys.PowerOCR" -Force -ErrorAction SilentlyContinue
```

## 4. Run & Test Execution
Run standalone with Gemini API Key and model configured:

```powershell
$env:GEMINI_API_KEY="<your-gemini-api-key>"
$env:TEXT_EXTRACTOR_LLM_MODEL="gemini-flash-lite-latest"
powershell -ExecutionPolicy Bypass -File ".\test_diagnostics.ps1"
```

## 5. Live Diagnostics & Logs
All execution logs, hotkey registration events, OCR extractions, and LLM requests/responses are logged real-time to:
`$env:TEMP\PowerToys_TextExtractor_Debug.log`

Monitor logs live:
```powershell
Get-Content "$env:TEMP\PowerToys_TextExtractor_Debug.log" -Wait -Tail 30
```
