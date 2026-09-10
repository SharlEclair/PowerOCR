using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using PowerOCR.Helpers;
using PowerOCR.Settings;
using Windows.ApplicationModel.DataTransfer;

namespace PowerOCR.Services;

public sealed class OcrPipelineManager
{
    private static readonly Lazy<OcrPipelineManager> _instance = new(() => new OcrPipelineManager());
    public static OcrPipelineManager Shared => _instance.Value;

    private readonly LlmFormattingService _llmService = new();
    private readonly object _syncLock = new();
    private CancellationTokenSource? _inflightCts;
    private DispatcherQueue? _dispatcherQueue;

    private string? _lastRawText;
    private string? _lastCleanedText;

    public OcrPipelineManager()
    {
    }

    public void Initialize(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    public void CancelInflight()
    {
        lock (_syncLock)
        {
            if (_inflightCts != null)
            {
                try
                {
                    _inflightCts.Cancel();
                    _inflightCts.Dispose();
                }
                catch { }
                _inflightCts = null;
            }
        }
    }

    public async Task ProcessCroppedBitmapAsync(Bitmap croppedBitmap)
    {
        CancellationToken token;
        lock (_syncLock)
        {
            // Concurrency safety: cancel any inflight LLM requests from prior snips
            CancelInflight();
            _inflightCts = new CancellationTokenSource();
            token = _inflightCts.Token;
        }

        // Extract raw OCR text
        string rawText;
        try
        {
            rawText = await OcrHelper.ExtractTextAsync(croppedBitmap);
        }
        catch (Exception ex)
        {
            NotificationService.ShowToast("PowerOCR OCR Error", $"Failed to extract text: {ex.Message}");
            return;
        }

        if (string.IsNullOrWhiteSpace(rawText))
        {
            NotificationService.ShowToast("PowerOCR", "No text detected in selected region.");
            return;
        }

        _lastRawText = rawText;
        Console.WriteLine($"[PowerOCR] Raw OCR extracted ({rawText.Length} chars):\n---\n{rawText}\n---");
        System.Diagnostics.Debug.WriteLine($"[PowerOCR] Raw OCR extracted ({rawText.Length} chars):\n---\n{rawText}\n---");

        // Stage 1: Immediately copy raw text to clipboard
        EnqueueOnUIThread(() =>
        {
            SetClipboardText(rawText);
            Console.WriteLine("[PowerOCR] Stage 1: Raw text immediately copied to clipboard.");
            System.Diagnostics.Debug.WriteLine("[PowerOCR] Stage 1: Raw text immediately copied to clipboard.");
        });

        // Stage 2: Fire LlmFormattingService in a background Task.Run
        _ = Task.Run(async () =>
        {
            if (token.IsCancellationRequested) return;

            var llmConfig = LlmConfig.Load();
            Console.WriteLine($"[PowerOCR] Stage 2: Invoking LLM service (Provider: {llmConfig.Provider}, Model: {llmConfig.Model})...");
            System.Diagnostics.Debug.WriteLine($"[PowerOCR] Stage 2: Invoking LLM service (Provider: {llmConfig.Provider}, Model: {llmConfig.Model})...");

            string cleanedText = await _llmService.CleanAndFormatOcrTextAsync(rawText, llmConfig, token);

            if (token.IsCancellationRequested)
            {
                Console.WriteLine("[PowerOCR] LLM processing was cancelled.");
                return;
            }

            // Only overwrite if the cleaned text differs from raw text
            if (!string.IsNullOrWhiteSpace(cleanedText) &&
                !string.Equals(cleanedText.Trim(), rawText.Trim(), StringComparison.Ordinal))
            {
                _lastCleanedText = cleanedText;
                Console.WriteLine($"[PowerOCR] Stage 2: LLM formatting complete ({cleanedText.Length} chars):\n---\n{cleanedText}\n---");
                System.Diagnostics.Debug.WriteLine($"[PowerOCR] Stage 2: LLM formatting complete ({cleanedText.Length} chars):\n---\n{cleanedText}\n---");

                EnqueueOnUIThread(async () =>
                {
                    try
                    {
                        if (token.IsCancellationRequested) return;

                        // Concurrency / race condition safety check:
                        // If the user modified the clipboard in the meantime, abort overwrite
                        string? currentClipboard = await GetClipboardTextAsync();
                        if (!string.Equals(currentClipboard?.Trim(), rawText.Trim(), StringComparison.Ordinal))
                        {
                            Console.WriteLine("[PowerOCR] Clipboard modified by user; LLM overwrite aborted.");
                            NotificationService.ShowToast("PowerOCR", "Clipboard modified by user; LLM formatting aborted.");
                            return;
                        }

                        // Overwrite clipboard with cleaned text
                        SetClipboardText(cleanedText);
                        Console.WriteLine("[PowerOCR] Stage 2: Clipboard successfully updated with LLM-enhanced text.");
                        System.Diagnostics.Debug.WriteLine("[PowerOCR] Stage 2: Clipboard successfully updated with LLM-enhanced text.");

                        // Show Toast Notification with interactive "Undo" button
                        ShowEnhancedToast();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PowerOCR] Error during clipboard update: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"[PowerOCR] Error during clipboard update: {ex.Message}");
                    }
                });
            }
            else
            {
                Console.WriteLine("[PowerOCR] Stage 2: Cleaned text identical to raw text or empty; no clipboard overwrite needed.");
                System.Diagnostics.Debug.WriteLine("[PowerOCR] Stage 2: Cleaned text identical to raw text or empty; no clipboard overwrite needed.");
            }
        }, token);
    }

    private void ShowEnhancedToast()
    {
        try
        {
            var notification = new AppNotificationBuilder()
                .AddText("PowerOCR: Text Enhanced")
                .AddText("Text was cleaned and formatted by LLM.")
                .AddButton(new AppNotificationButton("Undo")
                    .AddArgument("action", "undo"))
                .BuildNotification();

            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to show enhanced toast: {ex.Message}");
        }
    }

    public void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        if (args.Arguments != null &&
            args.Arguments.TryGetValue("action", out var action) &&
            action.Equals("undo", StringComparison.OrdinalIgnoreCase))
        {
            UndoLastFormatting();
        }
    }

    public void UndoLastFormatting()
    {
        if (string.IsNullOrEmpty(_lastRawText)) return;

        EnqueueOnUIThread(() =>
        {
            SetClipboardText(_lastRawText);
            NotificationService.ShowToast("PowerOCR", "Reverted clipboard to raw OCR text.");
        });
    }

    private void EnqueueOnUIThread(Action action)
    {
        if (_dispatcherQueue != null && !_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(() => action());
        }
        else
        {
            action();
        }
    }

    private static void SetClipboardText(string text)
    {
        try
        {
            var dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Clipboard SetContent failed: {ex.Message}");
        }
    }

    private static async Task<string?> GetClipboardTextAsync()
    {
        try
        {
            DataPackageView view = Clipboard.GetContent();
            if (view.Contains(StandardDataFormats.Text))
            {
                return await view.GetTextAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Clipboard GetContent failed: {ex.Message}");
        }

        return null;
    }
}
