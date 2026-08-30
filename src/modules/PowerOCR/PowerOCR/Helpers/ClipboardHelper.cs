// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ManagedCommon;

namespace PowerOCR.Helpers;

internal static class ClipboardHelper
{
    private const int MaxRetries = 5;
    private const int RetryDelayMs = 50;

    /// <summary>
    /// Sets text to the Windows Clipboard safely dispatched to the WPF STA UI thread.
    /// Retries multiple times to handle transient locks by other applications.
    /// </summary>
    /// <param name="text">The text to copy to the clipboard.</param>
    /// <param name="dispatcher">Optional Dispatcher. If null, Application.Current.Dispatcher is used.</param>
    /// <returns>True if clipboard text was set successfully, false otherwise.</returns>
    public static bool SetClipboardText(string text, Dispatcher? dispatcher = null)
    {
        if (text == null)
        {
            return false;
        }

        Dispatcher? targetDispatcher = dispatcher ?? Application.Current?.Dispatcher;

        if (targetDispatcher != null && !targetDispatcher.CheckAccess())
        {
            try
            {
                return targetDispatcher.Invoke(() => SetClipboardTextInternal(text));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to dispatch clipboard update to STA thread: {ex.Message}", ex);
                return false;
            }
        }

        return SetClipboardTextInternal(text);
    }

    private static bool SetClipboardTextInternal(string text)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(text, true);
                return true;
            }
            catch (COMException comEx)
            {
                Logger.LogWarning($"Clipboard copy attempt {attempt}/{MaxRetries} failed with COMException: {comEx.Message}");
            }
            catch (ExternalException extEx)
            {
                Logger.LogWarning($"Clipboard copy attempt {attempt}/{MaxRetries} failed with ExternalException: {extEx.Message}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Clipboard copy attempt {attempt}/{MaxRetries} failed with unexpected exception: {ex.Message}", ex);
                break;
            }

            if (attempt < MaxRetries)
            {
                Thread.Sleep(RetryDelayMs);
            }
        }

        Logger.LogError("Exhausted all retries attempting to set clipboard text.");
        return false;
    }
}
