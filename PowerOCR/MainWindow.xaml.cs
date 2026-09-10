using System;
using Microsoft.UI.Xaml;
using PowerOCR.Interop;
using PowerOCR.Services;

namespace PowerOCR;

/// <summary>
/// Persistent, hidden background anchor window for PowerOCR.
/// Keeps the process alive, manages global hotkey hooks via Win32 Subclassing, and coordinates OCR requests.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly HotkeyManager _hotkeyManager;
    private readonly OverlayCoordinator _overlayCoordinator;

    public MainWindow()
    {
        InitializeComponent();

        // Initialize settings
        _settingsService = new SettingsService();
        _settingsService.Load();

        // Initialize overlay coordinator and OCR pipeline
        _overlayCoordinator = new OverlayCoordinator(this.DispatcherQueue);
        OcrPipelineManager.Shared.Initialize(this.DispatcherQueue);

        // Prevent accidental termination on close
        AppWindow.Closing += (sender, args) =>
        {
            args.Cancel = true;
            AppWindow.Hide();
        };

        // Obtain native HWND for Win32 message interception
        IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Hide from taskbar and Alt+Tab by ensuring WS_EX_TOOLWINDOW style
        long exStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW;
        exStyle &= ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_HIDE);
        AppWindow.Hide();

        Console.WriteLine($"[PowerOCR] MainWindow initialized (HWND: 0x{hWnd:X}).");
        System.Diagnostics.Debug.WriteLine($"[PowerOCR] MainWindow initialized (HWND: 0x{hWnd:X}).");

        // Initialize Hotkey Manager and hook message loop via SetWindowSubclass
        _hotkeyManager = new HotkeyManager(hWnd);
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;

        // Register the global hotkey
        bool registered = _hotkeyManager.Register(_settingsService.Settings.Hotkey, out string error);
        if (!registered)
        {
            Console.WriteLine($"[PowerOCR] Global hotkey registration FAILED: {error}");
            System.Diagnostics.Debug.WriteLine($"[PowerOCR] Global hotkey registration FAILED: {error}");
            NotificationService.ShowToast("PowerOCR Hotkey Error", error);
        }
        else
        {
            Console.WriteLine($"[PowerOCR] Global hotkey {_settingsService.Settings.Hotkey.Modifiers}+{_settingsService.Settings.Hotkey.Key} REGISTERED successfully!");
            System.Diagnostics.Debug.WriteLine($"[PowerOCR] Global hotkey {_settingsService.Settings.Hotkey.Modifiers}+{_settingsService.Settings.Hotkey.Key} REGISTERED successfully!");
            NotificationService.ShowToast("PowerOCR", $"Listening for {_settingsService.Settings.Hotkey.Modifiers}+{_settingsService.Settings.Hotkey.Key} in background.");
        }

        // Hide again to ensure window never displays
        AppWindow.Hide();
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        Console.WriteLine("[PowerOCR] Global hotkey triggered! Initiating desktop overlay capture...");
        System.Diagnostics.Debug.WriteLine("[PowerOCR] Global hotkey triggered! Initiating desktop overlay capture...");
        _ = _overlayCoordinator.StartCaptureAsync();
    }
}
