using System;
using System.Drawing;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PowerOCR.Helpers;
using PowerOCR.Interop;
using Windows.Foundation;
using Windows.System;

namespace PowerOCR.UI;

public sealed partial class CaptureOverlayWindow : Window
{
    private readonly MonitorCapture _capture;
    private bool _isDragging;
    private Windows.Foundation.Point _startPoint;

    public event EventHandler<Bitmap>? SelectionCompleted;
    public event EventHandler? CaptureCancelled;

    public CaptureOverlayWindow(MonitorCapture capture)
    {
        _capture = capture;
        InitializeComponent();

        ConfigureWindowChrome();

        RootGrid.Loaded += async (s, e) =>
        {
            try
            {
                BackgroundImage.Source = await ScreenCaptureHelper.ConvertToBitmapImageAsync(_capture.Screenshot);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load background image: {ex.Message}");
            }
        };

        RootGrid.KeyDown += OnRootGridKeyDown;
    }

    private void ConfigureWindowChrome()
    {
        ExtendsContentIntoTitleBar = true;
        if (AppWindow.TitleBar != null)
        {
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        }

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        // Position and size the window to cover the physical monitor bounds
        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
            _capture.Monitor.Bounds.left,
            _capture.Monitor.Bounds.top,
            _capture.Monitor.Bounds.Width,
            _capture.Monitor.Bounds.Height));
    }

    private void OnCanvasPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(DrawingCanvas).Properties;
        if (properties.IsRightButtonPressed)
        {
            CaptureCancelled?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _startPoint = e.GetCurrentPoint(DrawingCanvas).Position;
            DrawingCanvas.CapturePointer(e.Pointer);

            Canvas.SetLeft(SelectionBorder, _startPoint.X);
            Canvas.SetTop(SelectionBorder, _startPoint.Y);
            SelectionBorder.Width = 0;
            SelectionBorder.Height = 0;
            SelectionBorder.Visibility = Visibility.Visible;
        }
    }

    private void OnCanvasPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging) return;

        var currentPoint = e.GetCurrentPoint(DrawingCanvas).Position;
        double x = Math.Min(_startPoint.X, currentPoint.X);
        double y = Math.Min(_startPoint.Y, currentPoint.Y);
        double width = Math.Abs(currentPoint.X - _startPoint.X);
        double height = Math.Abs(currentPoint.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionBorder, x);
        Canvas.SetTop(SelectionBorder, y);
        SelectionBorder.Width = width;
        SelectionBorder.Height = height;
    }

    private void OnCanvasPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        DrawingCanvas.ReleasePointerCapture(e.Pointer);

        var endPoint = e.GetCurrentPoint(DrawingCanvas).Position;
        double dipX = Math.Min(_startPoint.X, endPoint.X);
        double dipY = Math.Min(_startPoint.Y, endPoint.Y);
        double dipWidth = Math.Abs(endPoint.X - _startPoint.X);
        double dipHeight = Math.Abs(endPoint.Y - _startPoint.Y);

        SelectionBorder.Visibility = Visibility.Collapsed;

        // CRITICAL DPI MATH (winui-wpf-migration):
        // Pointer positions in WinUI 3 are in DIPs. Multiply by Monitor.DpiScale to map to physical pixels.
        double dpiScale = _capture.Monitor.DpiScale;
        int physX = (int)Math.Round(dipX * dpiScale);
        int physY = (int)Math.Round(dipY * dpiScale);
        int physWidth = (int)Math.Round(dipWidth * dpiScale);
        int physHeight = (int)Math.Round(dipHeight * dpiScale);

        // Cancel if selection was an accidental click (< 5 physical pixels)
        if (physWidth < 5 || physHeight < 5)
        {
            CaptureCancelled?.Invoke(this, EventArgs.Empty);
            return;
        }

        var cropRect = new System.Drawing.Rectangle(physX, physY, physWidth, physHeight);
        var croppedBitmap = ScreenCaptureHelper.CropBitmap(_capture.Screenshot, cropRect);

        SelectionCompleted?.Invoke(this, croppedBitmap);
    }

    private void OnEscapeInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _isDragging = false;
        DrawingCanvas.ReleasePointerCaptures();
        SelectionBorder.Visibility = Visibility.Collapsed;
        CaptureCancelled?.Invoke(this, EventArgs.Empty);
    }

    private void OnRootGridKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            CaptureCancelled?.Invoke(this, EventArgs.Empty);
        }
    }
}
