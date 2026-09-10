using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using PowerOCR.Helpers;
using PowerOCR.UI;

namespace PowerOCR.Services;

public sealed class OverlayCoordinator
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly List<CaptureOverlayWindow> _activeOverlays = new();
    private readonly List<MonitorCapture> _currentCaptures = new();
    private bool _isCapturing;

    public event EventHandler<Bitmap>? CaptureSucceeded;

    public OverlayCoordinator(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    public async Task StartCaptureAsync()
    {
        if (_isCapturing) return;
        _isCapturing = true;

        // Concurrency safety: cancel any inflight LLM requests from prior snips
        OcrPipelineManager.Shared.CancelInflight();

        // 1. Capture all monitors in the background BEFORE spawning any UI windows
        List<MonitorCapture> captures;
        try
        {
            captures = await Task.Run(ScreenCaptureHelper.CaptureAllMonitors);
        }
        catch (Exception ex)
        {
            _isCapturing = false;
            NotificationService.ShowToast("PowerOCR Error", $"Screen capture failed: {ex.Message}");
            return;
        }

        // 2. Dispatch to UI thread to create and activate overlay windows
        _dispatcherQueue.TryEnqueue(() =>
        {
            _currentCaptures.Clear();
            _currentCaptures.AddRange(captures);

            foreach (var capture in captures)
            {
                var overlay = new CaptureOverlayWindow(capture);
                overlay.SelectionCompleted += OnSelectionCompleted;
                overlay.CaptureCancelled += OnCaptureCancelled;

                _activeOverlays.Add(overlay);
                overlay.Activate();
            }
        });
    }

    private void OnSelectionCompleted(object? sender, Bitmap croppedBitmap)
    {
        CloseAllOverlays();

        try
        {
            // Save cropped bitmap for verification
            string saveDir = SettingsService.AppDataDirectory;
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }
            string cropPath = Path.Combine(saveDir, "last_crop.png");
            croppedBitmap.Save(cropPath, ImageFormat.Png);
        }
        catch { }

        // Execute dual-stage clipboard pipeline
        _ = OcrPipelineManager.Shared.ProcessCroppedBitmapAsync(croppedBitmap);

        CaptureSucceeded?.Invoke(this, croppedBitmap);
    }

    private void OnCaptureCancelled(object? sender, EventArgs e)
    {
        CloseAllOverlays();
        NotificationService.ShowToast("PowerOCR", "Capture cancelled.");
    }

    private void CloseAllOverlays()
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            foreach (var overlay in _activeOverlays)
            {
                try
                {
                    overlay.Close();
                }
                catch { }
            }
            _activeOverlays.Clear();

            foreach (var capture in _currentCaptures)
            {
                try
                {
                    capture.Dispose();
                }
                catch { }
            }
            _currentCaptures.Clear();

            _isCapturing = false;
        });
    }
}
