// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ManagedCommon;
using PowerOCR.Helpers;
using PowerOCR.UI;

namespace PowerOCR.Services;

/// <summary>
/// Orchestrates the OCR text extraction post-processing pipeline.
/// Copies raw OCR text immediately to the clipboard as fallback, then asynchronously
/// cleans and formats the text with an LLM and updates the clipboard when resolved.
/// </summary>
public static class OcrPostProcessingPipeline
{
    private static readonly TimeSpan PipelineTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Executes the non-blocking OCR post-processing pipeline.
    /// </summary>
    /// <param name="rawText">The raw OCR text extracted from the screen.</param>
    /// <param name="dispatcher">The WPF STA Dispatcher used to update the clipboard.</param>
    /// <param name="postProcessor">Optional custom ILlmPostProcessor instance.</param>
    public static void ProcessExtractedText(
        string rawText,
        Dispatcher? dispatcher = null,
        ILlmPostProcessor? postProcessor = null)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return;
        }

        // Step 1: Immediately copy raw OCR text to the clipboard as fallback
        bool rawCopied = ClipboardHelper.SetClipboardText(rawText, dispatcher);
        if (rawCopied)
        {
            DebugLogger.LogInfo($"Raw OCR text ({rawText.Length} chars) successfully copied to clipboard as immediate fallback.");
        }
        else
        {
            DebugLogger.LogWarning("Failed to copy initial raw OCR text to clipboard.");
        }

        // Show floating HUD pill indicator: Cleaning in progress
        OcrStatusToast.ShowStatus("Cleaning text with Gemini...", "\uE946", "#60CDFF", autoCloseMs: 0);

        // Step 2: Asynchronously invoke LLM post-processing in the background
        ILlmPostProcessor processor = postProcessor ?? LlmPostProcessor.Instance;

        _ = Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource(PipelineTimeout);
                string? cleanedText = await processor.ProcessTextAsync(rawText, cts.Token).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(cleanedText) &&
                    !string.Equals(cleanedText, rawText, StringComparison.Ordinal))
                {
                    DebugLogger.LogInfo("LLM post-processing succeeded. Updating clipboard with cleaned text...");
                    bool updated = ClipboardHelper.SetClipboardText(cleanedText, dispatcher);
                    if (updated)
                    {
                        DebugLogger.LogInfo("Clipboard successfully updated with LLM post-processed text!");
                        OcrStatusToast.UpdateStatus("Cleaned with Gemini — Ready to paste! ✨", "\uE73E", "#6CCB5F", autoCloseMs: 2500);
                    }
                    else
                    {
                        DebugLogger.LogWarning("Failed to update clipboard with LLM post-processed text.");
                        OcrStatusToast.UpdateStatus("Clipboard update failed", "\uE7BA", "#FFA066", autoCloseMs: 2500);
                    }
                }
                else
                {
                    DebugLogger.LogInfo("LLM post-processing completed without changes or returned empty output.");
                    OcrStatusToast.UpdateStatus("Text copied to clipboard", "\uE8C8", "#60CDFF", autoCloseMs: 2000);
                }
            }
            catch (OperationCanceledException)
            {
                DebugLogger.LogWarning("LLM post-processing timed out or was cancelled.");
                OcrStatusToast.UpdateStatus("AI timed out — Raw text on clipboard", "\uE7BA", "#FFA066", autoCloseMs: 2500);
            }
            catch (Exception ex)
            {
                DebugLogger.LogWarning($"Background LLM post-processing pipeline encountered an error: {ex.Message}");
                OcrStatusToast.UpdateStatus("AI unavailable — Raw text on clipboard", "\uE7BA", "#FFA066", autoCloseMs: 2500);
            }
        });
    }

    /// <summary>
    /// Executes the direct Gemini Vision multimodal OCR pipeline on screenshot bytes.
    /// </summary>
    /// <param name="imageBytes">The cropped screenshot PNG bytes.</param>
    /// <param name="fallbackRawText">Optional raw OCR text to place on clipboard as immediate fallback.</param>
    /// <param name="dispatcher">The WPF STA Dispatcher used to update the clipboard.</param>
    /// <param name="postProcessor">Optional custom ILlmPostProcessor instance.</param>
    public static void ProcessExtractedImage(
        byte[] imageBytes,
        string? fallbackRawText = null,
        Dispatcher? dispatcher = null,
        ILlmPostProcessor? postProcessor = null)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            return;
        }

        // Step 1: Copy fallback text to clipboard if available
        if (!string.IsNullOrWhiteSpace(fallbackRawText))
        {
            ClipboardHelper.SetClipboardText(fallbackRawText, dispatcher);
        }

        // Show floating HUD pill indicator: Vision OCR in progress
        OcrStatusToast.ShowStatus("Gemini Vision analyzing screenshot...", "\uE7B3", "#60CDFF", autoCloseMs: 0);

        // Step 2: Asynchronously invoke Gemini Vision in the background
        ILlmPostProcessor processor = postProcessor ?? LlmPostProcessor.Instance;

        _ = Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource(PipelineTimeout);
                string? visionText = await processor.ProcessImageAsync(imageBytes, cts.Token).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(visionText))
                {
                    DebugLogger.LogInfo("Gemini Vision OCR succeeded. Updating clipboard with transcribed text...");
                    bool updated = ClipboardHelper.SetClipboardText(visionText, dispatcher);
                    if (updated)
                    {
                        DebugLogger.LogInfo("Clipboard successfully updated with Gemini Vision OCR text!");
                        OcrStatusToast.UpdateStatus("Cleaned with Gemini Vision — Ready to paste! ✨", "\uE73E", "#6CCB5F", autoCloseMs: 2500);
                    }
                    else
                    {
                        DebugLogger.LogWarning("Failed to update clipboard with Gemini Vision OCR text.");
                        OcrStatusToast.UpdateStatus("Clipboard update failed", "\uE7BA", "#FFA066", autoCloseMs: 2500);
                    }
                }
                else
                {
                    DebugLogger.LogInfo("Gemini Vision completed without content or returned empty output.");
                    OcrStatusToast.UpdateStatus("Text copied to clipboard", "\uE8C8", "#60CDFF", autoCloseMs: 2000);
                }
            }
            catch (OperationCanceledException)
            {
                DebugLogger.LogWarning("Gemini Vision OCR timed out or was cancelled.");
                OcrStatusToast.UpdateStatus("Vision timed out — Raw text on clipboard", "\uE7BA", "#FFA066", autoCloseMs: 2500);
            }
            catch (Exception ex)
            {
                DebugLogger.LogWarning($"Gemini Vision pipeline encountered an error: {ex.Message}");
                OcrStatusToast.UpdateStatus("Vision unavailable — Raw text on clipboard", "\uE7BA", "#FFA066", autoCloseMs: 2500);
            }
        });
    }
}
