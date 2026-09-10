using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using PowerOCR.Interop;
using Windows.Storage.Streams;

namespace PowerOCR.Helpers;

public sealed class MonitorInfo
{
    public IntPtr Handle { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public NativeMethods.RECT Bounds { get; set; }
    public uint DpiX { get; set; } = 96;
    public uint DpiY { get; set; } = 96;
    public double DpiScale => DpiX / 96.0;
    public bool IsPrimary { get; set; }
}

public sealed class MonitorCapture : IDisposable
{
    public MonitorInfo Monitor { get; }
    public Bitmap Screenshot { get; }

    public MonitorCapture(MonitorInfo monitor, Bitmap screenshot)
    {
        Monitor = monitor;
        Screenshot = screenshot;
    }

    public void Dispose()
    {
        Screenshot?.Dispose();
    }
}

public static class ScreenCaptureHelper
{
    private const uint MONITORINFOF_PRIMARY = 0x00000001;

    public static List<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rc, IntPtr data) =>
        {
            var mi = new NativeMethods.MONITORINFOEX();
            mi.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFOEX));

            if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
            {
                uint dpiX = 96;
                uint dpiY = 96;

                try
                {
                    int hr = NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MonitorDpiType.MDT_EFFECTIVE_DPI, out dpiX, out dpiY);
                    if (hr != 0)
                    {
                        dpiX = 96;
                        dpiY = 96;
                    }
                }
                catch
                {
                    dpiX = 96;
                    dpiY = 96;
                }

                monitors.Add(new MonitorInfo
                {
                    Handle = hMonitor,
                    DeviceName = mi.szDevice,
                    Bounds = mi.rcMonitor,
                    DpiX = dpiX,
                    DpiY = dpiY,
                    IsPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0
                });
            }

            return true;
        }, IntPtr.Zero);

        return monitors;
    }

    public static List<MonitorCapture> CaptureAllMonitors()
    {
        var monitors = GetMonitors();
        var captures = new List<MonitorCapture>();

        foreach (var monitor in monitors)
        {
            int width = Math.Max(1, monitor.Bounds.Width);
            int height = Math.Max(1, monitor.Bounds.Height);

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(
                    monitor.Bounds.left,
                    monitor.Bounds.top,
                    0,
                    0,
                    new Size(width, height),
                    CopyPixelOperation.SourceCopy);
            }

            captures.Add(new MonitorCapture(monitor, bitmap));
        }

        return captures;
    }

    public static Bitmap CropBitmap(Bitmap source, Rectangle cropRect)
    {
        // Clamp to source bounds
        int x = Math.Clamp(cropRect.X, 0, Math.Max(0, source.Width - 1));
        int y = Math.Clamp(cropRect.Y, 0, Math.Max(0, source.Height - 1));
        int width = Math.Clamp(cropRect.Width, 1, source.Width - x);
        int height = Math.Clamp(cropRect.Height, 1, source.Height - y);

        var clampedRect = new Rectangle(x, y, width, height);
        return source.Clone(clampedRect, source.PixelFormat);
    }

    public static async Task<BitmapImage> ConvertToBitmapImageAsync(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        byte[] bytes = ms.ToArray();

        var ras = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(ras.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
        }

        ras.Seek(0);
        var bitmapImage = new BitmapImage();
        await bitmapImage.SetSourceAsync(ras);
        return bitmapImage;
    }
}
