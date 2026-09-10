---
name: powertoys-build-and-run
description: Detailed procedures for building, testing, launching, and diagnosing the PowerToys Text Extractor (PowerOCR) standalone WinUI 3 module and its LLM post-processing pipeline.
---

# PowerToys Text Extractor (PowerOCR) Build & Diagnostics Guide

## 1. Environment & Prerequisites
- **OS**: Windows 10 (v1903+) or Windows 11 (Developer Mode enabled)
- **Framework**: .NET 10.0 Windows SDK (`net10.0-windows10.0.26100.0`)
- **Windows App SDK**: 1.8+ (`Microsoft.WindowsAppSDK` NuGet)
- **CLI Tooling**: WinApp CLI 0.6+ (`winapp`)

## 2. Project Location
- Project Path: `PowerOCR\PowerOCR.csproj`
- Type: Unpackaged WinUI 3 C# Desktop Application (`<WindowsPackageType>None</WindowsPackageType>`)
- Anchor Architecture: Hidden persistent `MainWindow` (`WS_EX_TOOLWINDOW`) managing global hotkeys via Win32 subclassing.

## 3. Pre-Build Process Clean-up
If `PowerOCR.exe` is running or locked by background tasks, stop it before rebuilding:

```powershell
Stop-Process -Name "PowerOCR" -Force -ErrorAction SilentlyContinue
```

## 4. Build Commands

### Build with .NET CLI:
```powershell
cd PowerOCR
dotnet build -c Debug
```

### Build and Run with WinApp CLI (Recommended):
```powershell
cd PowerOCR
winapp run . --debug-output
```
*Note: WinApp CLI automatically restores, compiles for `win-x64`, launches the app, and attaches live debugger output streaming.*

## 5. End-to-End Pipeline Verification
Launch the application with the diagnostic pipeline flag to run an automated end-to-end OCR extraction and LLM post-processing test on startup:

```powershell
winapp run . --debug-output --args "--test-pipeline"
```

Expected diagnostic output:
```text
✅ Built PowerOCR in 4.1s
✅ Launched PowerOCR (PID: <PID>)
[PowerOCR] MainWindow initialized (HWND: 0x...).
[PowerOCR] Global hotkey Win+Shift+O REGISTERED successfully!
[PowerOCR] --test-pipeline flag active. Running end-to-end OCR & LLM pipeline test...
[PowerOCR] Raw OCR extracted (24 chars): PowerOCR Pipeline Active
[PowerOCR] Stage 1: Raw text immediately copied to clipboard.
[PowerOCR] Stage 2: Invoking LLM service (Provider: <Provider>, Model: <Model>)...
[PowerOCR] Stage 2: Clipboard successfully updated with LLM-enhanced text.
```

Verify the Windows Clipboard content:
```powershell
Get-Clipboard
```

## 6. Configuration & Persistence Paths
- **General Settings**: `%APPDATA%\PowerOCR\appsettings.json` (Hotkey modifiers, key, language)
- **LLM Settings**: `%LOCALAPPDATA%\PowerOCR\llm_settings.json` (Provider, Endpoint, Model, ApiKey)
- **Diagnostic Crop Artifact**: `%APPDATA%\PowerOCR\last_crop.png`

## 7. Interactive Controls & Hotkeys
- **Trigger Capture**: <kbd>Win</kbd> + <kbd>Shift</kbd> + <kbd>O</kbd> (configurable)
- **Dismiss Overlay**: <kbd>Escape</kbd> or Mouse Right-Click
- **Region Selection**: Left-Click & Drag marquee across any monitor (Per-Monitor v2 DPI scaled)
- **Undo LLM Formatting**: Click the **Undo** action button on the Windows Toast notification to revert the clipboard to raw OCR text.

