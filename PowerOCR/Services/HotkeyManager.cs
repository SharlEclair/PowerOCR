using System;
using System.Runtime.InteropServices;
using PowerOCR.Interop;
using PowerOCR.Settings;

namespace PowerOCR.Services;

public sealed class HotkeyManager : IDisposable
{
    private const int HotkeyId = 9001;
    private const nuint SubclassId = 1001;

    private readonly IntPtr _hWnd;
    private readonly NativeMethods.SubclassProc _subclassProc;
    private bool _isSubclassed;
    private bool _isRegistered;
    private bool _disposed;

    public event EventHandler? HotkeyPressed;

    public HotkeyManager(IntPtr hWnd)
    {
        _hWnd = hWnd;
        // Keep reference to delegate to prevent GC collection
        _subclassProc = OnSubclassProc;
    }

    public bool Register(HotkeyConfig config, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (_disposed || _hWnd == IntPtr.Zero)
        {
            errorMessage = "Invalid window handle or already disposed.";
            return false;
        }

        // Install subclass hook first if not already subclassed
        if (!_isSubclassed)
        {
            _isSubclassed = NativeMethods.SetWindowSubclass(_hWnd, _subclassProc, SubclassId, nuint.Zero);
            if (!_isSubclassed)
            {
                int err = Marshal.GetLastWin32Error();
                errorMessage = $"Failed to hook window message loop (SetWindowSubclass error {err}).";
                return false;
            }
        }

        uint modifiers = NativeMethods.MOD_NOREPEAT;
        var modParts = config.Modifiers.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var mod in modParts)
        {
            if (mod.Equals("Win", StringComparison.OrdinalIgnoreCase) || mod.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= NativeMethods.MOD_WIN;
            }
            else if (mod.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= NativeMethods.MOD_SHIFT;
            }
            else if (mod.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || mod.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= NativeMethods.MOD_CONTROL;
            }
            else if (mod.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= NativeMethods.MOD_ALT;
            }
        }

        uint vk = 0x4F; // Default 'O'
        if (!string.IsNullOrEmpty(config.Key))
        {
            if (Enum.TryParse<Windows.System.VirtualKey>(config.Key, true, out var parsedKey))
            {
                vk = (uint)parsedKey;
            }
            else if (config.Key.Length == 1)
            {
                vk = char.ToUpperInvariant(config.Key[0]);
            }
        }

        if (_isRegistered)
        {
            NativeMethods.UnregisterHotKey(_hWnd, HotkeyId);
            _isRegistered = false;
        }

        _isRegistered = NativeMethods.RegisterHotKey(_hWnd, HotkeyId, modifiers, vk);
        if (!_isRegistered)
        {
            int err = Marshal.GetLastWin32Error();
            errorMessage = $"Failed to register global hotkey {config.Modifiers}+{config.Key} (Win32 error {err}). It may be used by another application.";
            return false;
        }

        return true;
    }

    private IntPtr OnSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, nuint dwRefData)
    {
        if (uMsg == NativeMethods.WM_HOTKEY)
        {
            if (wParam.ToInt32() == HotkeyId)
            {
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
                return IntPtr.Zero;
            }
        }

        return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_isRegistered && _hWnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_hWnd, HotkeyId);
            _isRegistered = false;
        }

        if (_isSubclassed && _hWnd != IntPtr.Zero)
        {
            NativeMethods.RemoveWindowSubclass(_hWnd, _subclassProc, SubclassId);
            _isSubclassed = false;
        }
    }
}
