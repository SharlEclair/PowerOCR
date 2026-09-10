using System;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using PowerOCR.Services;

namespace PowerOCR;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // Explicitly register for toast notifications (required for unpackaged apps)
        // Handlers must be registered BEFORE calling Register()
        try
        {
            AppNotificationManager.Default.NotificationInvoked += OcrPipelineManager.Shared.OnNotificationInvoked;
            AppNotificationManager.Default.Register();
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                try
                {
                    AppNotificationManager.Default.Unregister();
                }
                catch
                {
                }
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppNotificationManager registration failed: {ex.Message}");
        }

        _window = new MainWindow();
        _window.Activate();
        _window.AppWindow.Hide();

        // Check for --test-pipeline argument to run a non-destructive diagnostic verification
        string[] cmdArgs = Environment.GetCommandLineArgs();
        foreach (var arg in cmdArgs)
        {
            if (arg.Equals("--test-pipeline", StringComparison.OrdinalIgnoreCase))
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    Console.WriteLine("[PowerOCR] --test-pipeline flag active. Running end-to-end OCR & LLM pipeline test...");
                    System.Diagnostics.Debug.WriteLine("[PowerOCR] --test-pipeline flag active. Running end-to-end OCR & LLM pipeline test...");

                    using var testBitmap = new System.Drawing.Bitmap(500, 120);
                    using (var g = System.Drawing.Graphics.FromImage(testBitmap))
                    {
                        g.Clear(System.Drawing.Color.White);
                        using var font = new System.Drawing.Font("Segoe UI", 18, System.Drawing.FontStyle.Regular);
                        using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);
                        g.DrawString("PowerOCR Pipeline Active", font, brush, 15, 40);
                    }

                    await OcrPipelineManager.Shared.ProcessCroppedBitmapAsync(testBitmap);
                });
                break;
            }
        }
    }
}
