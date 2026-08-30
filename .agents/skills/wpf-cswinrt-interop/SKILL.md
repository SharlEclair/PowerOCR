---
name: wpf-cswinrt-interop
description: Guidelines and patterns for WPF STA UI thread safety, WinRT APIs, Windows Clipboard handling, and P/Invoke Win32 interop in PowerToys.
---

# WPF STA Threading, WinRT & Clipboard Guidelines

## 1. WPF STA Threading Rules
- All WPF `Window`, `Control`, and `Clipboard` operations **must** be executed on the WPF STA UI thread.
- Check thread access before interacting with UI or Clipboard:
```csharp
if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
{
    Application.Current.Dispatcher.Invoke(() => Action());
    return;
}
```

## 2. Robust Clipboard Access
Clipboard operations can transiently fail if another application holds a lock on the Windows clipboard (`CLIPBRD_E_CANT_OPEN`). Always use retry backoff:

```csharp
public static bool SetClipboardText(string text, Dispatcher? dispatcher = null, int maxRetries = 5, int delayMs = 50)
{
    // Retry loop handling COMException / ExternalException
}
```

## 3. WinRT OCR API Integration
- Uses `Windows.Media.Ocr.OcrEngine` and `Windows.Graphics.Imaging.SoftwareBitmap`.
- Enumerates installed Windows OCR languages via `OcrEngine.AvailableRecognizerLanguages`.

## 4. Win32 Hooking & Window Activation
- Low-level keyboard hook: `SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, user32Handle, 0)`.
- Window activation: `AttachThreadInput` + `SetForegroundWindow` to steal focus onto full-screen transparent OCR overlay.
- Keep overlay windows `Topmost = true` so they layer over active applications.
