// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PowerOCR.Helpers;

namespace PowerOCR.UI;

public partial class OcrStatusToast : Window
{
    private static OcrStatusToast? _currentInstance;
    private DispatcherTimer? _autoCloseTimer;

    private const int WsExNoActivate = 0x08000000;
    private const int GwlExStyle = -20;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public OcrStatusToast()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GwlExStyle);
            _ = SetWindowLong(hwnd, GwlExStyle, exStyle | WsExNoActivate);

            var workingArea = SystemParameters.WorkArea;
            Left = workingArea.Right - ActualWidth - 24;
            Top = workingArea.Bottom - ActualHeight - 24;

            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200));
            BeginAnimation(OpacityProperty, fadeIn);
        }
        catch (Exception ex)
        {
            DebugLogger.LogError($"Error in OcrStatusToast OnLoaded: {ex.Message}", ex);
        }
    }

    public static void ShowStatus(string message, string icon = "\uE946", string? hexColor = null, int autoCloseMs = 2500)
    {
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(() => ShowStatus(message, icon, hexColor, autoCloseMs));
            return;
        }

        try
        {
            if (_currentInstance != null)
            {
                _currentInstance._autoCloseTimer?.Stop();
                _currentInstance.Close();
                _currentInstance = null;
            }

            var toast = new OcrStatusToast();
            toast.MessageTextBlock.Text = message;
            toast.IconTextBlock.Text = icon;

            if (!string.IsNullOrEmpty(hexColor))
            {
                var brush = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString(hexColor);
                if (brush != null)
                {
                    toast.IconTextBlock.Foreground = brush;
                }
            }

            _currentInstance = toast;
            toast.Show();

            if (autoCloseMs > 0)
            {
                toast._autoCloseTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(autoCloseMs),
                };
                toast._autoCloseTimer.Tick += (s, args) =>
                {
                    toast._autoCloseTimer?.Stop();
                    toast.FadeOutAndClose();
                };
                toast._autoCloseTimer.Start();
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogError($"Error displaying OcrStatusToast: {ex.Message}", ex);
        }
    }

    public static void UpdateStatus(string message, string icon = "\uE946", string? hexColor = null, int autoCloseMs = 2500)
    {
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(() => UpdateStatus(message, icon, hexColor, autoCloseMs));
            return;
        }

        if (_currentInstance != null && _currentInstance.IsLoaded)
        {
            _currentInstance.MessageTextBlock.Text = message;
            _currentInstance.IconTextBlock.Text = icon;
            if (!string.IsNullOrEmpty(hexColor))
            {
                var brush = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFromString(hexColor);
                if (brush != null)
                {
                    _currentInstance.IconTextBlock.Foreground = brush;
                }
            }

            _currentInstance._autoCloseTimer?.Stop();
            if (autoCloseMs > 0)
            {
                _currentInstance._autoCloseTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(autoCloseMs),
                };
                _currentInstance._autoCloseTimer.Tick += (s, args) =>
                {
                    _currentInstance._autoCloseTimer?.Stop();
                    _currentInstance.FadeOutAndClose();
                };
                _currentInstance._autoCloseTimer.Start();
            }
        }
        else
        {
            ShowStatus(message, icon, hexColor, autoCloseMs);
        }
    }

    private void FadeOutAndClose()
    {
        var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(250));
        fadeOut.Completed += (s, e) =>
        {
            try
            {
                Close();
                if (_currentInstance == this)
                {
                    _currentInstance = null;
                }
            }
            catch
            {
            }
        };
        BeginAnimation(OpacityProperty, fadeOut);
    }
}
