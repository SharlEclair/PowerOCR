---
name: wpf-cswinrt-interop
description: Guidelines and patterns for Windows UI threading (WinUI 3 DispatcherQueue & WPF STA), WinRT APIs, Windows Clipboard handling, and P/Invoke Win32 interop.
---

# Windows Desktop Threading, WinRT & Clipboard Interop Guidelines

## 1. Modern WinUI 3 Threading & Marshaling (PowerOCR Standard)
- **UI Thread Dispatching**: In WinUI 3, all XAML elements, `AppWindow`, and Windows Runtime Clipboard APIs must execute on the UI thread via `Microsoft.UI.Dispatching.DispatcherQueue`:
  ```csharp
  if (dispatcherQueue != null && !dispatcherQueue.HasThreadAccess)
  {
      dispatcherQueue.TryEnqueue(() => Action());
  }
  else
  {
      Action();
  }
  ```
- **Async Void Protection**: When dispatching asynchronous tasks through `DispatcherQueue.TryEnqueue(async () => ...)`, always wrap the entire lambda body in `try / catch (Exception ex)` to prevent unhandled task exceptions from crashing the host process.

## 2. Modern WinRT Clipboard Access (`Windows.ApplicationModel.DataTransfer`)
In WinUI 3 desktop apps, use the WinRT `DataPackage` API instead of legacy WPF `System.Windows.Clipboard`:
```csharp
var dataPackage = new DataPackage();
dataPackage.RequestedOperation = DataPackageOperation.Copy;
dataPackage.SetText(text);
Clipboard.SetContent(dataPackage);
Clipboard.Flush();
```
*Tip: Always execute `Clipboard.SetContent` on the UI thread or STA thread.*

## 3. Safe GDI to WinRT OCR Bridge (`OcrEngine`)
To pass a GDI `System.Drawing.Bitmap` to `Windows.Media.Ocr.OcrEngine` without COM exceptions:
1. Save bitmap to a `MemoryStream` as PNG.
2. Convert to WinRT `IRandomAccessStream` via `WindowsRuntimeStreamExtensions.AsRandomAccessStream(memoryStream)`.
3. Decode using `BitmapDecoder.CreateAsync(randomAccessStream)`.
4. Normalize `SoftwareBitmap` to `BitmapPixelFormat.Bgra8` and `BitmapAlphaMode.Premultiplied` (required by `OcrEngine.RecognizeAsync`).
5. Explicitly dispose `SoftwareBitmap` after recognition to prevent unmanaged imaging memory leaks.

## 4. Win32 Subclassing & Global Hotkeys
In WinUI 3, `Window` does not expose a native `WndProc` override. Intercept window messages (`WM_HOTKEY = 0x0312`) using safe Win32 subclassing:
```csharp
NativeMethods.SetWindowSubclass(hWnd, _subclassProc, SubclassId, nuint.Zero);
```
- Retain a strong reference to the `SubclassProc` delegate to prevent garbage collection.
- Always call `DefSubclassProc` for unhandled messages.
- Clean up with `RemoveWindowSubclass` in `Dispose()`.

## 5. Legacy WPF STA Compatibility (Older Modules)
For legacy WPF modules still present in PowerToys:
- Use `Application.Current.Dispatcher.CheckAccess()` and `Application.Current.Dispatcher.Invoke()`.
- Handle `CLIPBRD_E_CANT_OPEN` errors with exponential backoff retries when accessing `System.Windows.Clipboard`.

