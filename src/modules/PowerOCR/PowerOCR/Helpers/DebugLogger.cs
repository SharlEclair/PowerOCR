// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using ManagedCommon;

namespace PowerOCR.Helpers;

internal static class DebugLogger
{
    private static readonly string DebugLogFilePath = Path.Combine(
        Path.GetTempPath(),
        "PowerToys_TextExtractor_Debug.log");

    private static readonly object FileLock = new();

    // Rotate log at 2MB to prevent unbounded disk growth during long sessions
    private const long MaxLogFileSizeBytes = 2 * 1024 * 1024;

    public static void LogInfo(string message) => Log("INFO", message, ex: null);

    public static void LogWarning(string message) => Log("WARN", message, ex: null);

    public static void LogError(string message, Exception? ex = null) => Log("ERROR", message, ex);

    private static void Log(string level, string message, Exception? ex)
    {
        string exceptionSuffix = ex != null ? $"\nException: {ex}" : string.Empty;
        string formatted = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}{exceptionSuffix}";

        try
        {
            if (level == "ERROR")
            {
                Console.Error.WriteLine(formatted);
            }
            else
            {
                Console.WriteLine(formatted);
            }
        }
        catch
        {
        }

        try
        {
            switch (level)
            {
                case "WARN":
                    Logger.LogWarning(message);
                    break;
                case "ERROR" when ex != null:
                    Logger.LogError(message, ex);
                    break;
                case "ERROR":
                    Logger.LogError(message);
                    break;
                default:
                    Logger.LogInfo(message);
                    break;
            }
        }
        catch
        {
        }

        WriteToDebugFile(formatted);
    }

    private static void WriteToDebugFile(string logLine)
    {
        try
        {
            lock (FileLock)
            {
                RotateLogIfNeeded();
                using var writer = new StreamWriter(DebugLogFilePath, append: true);
                writer.WriteLine(logLine);
            }
        }
        catch
        {
        }
    }

    private static void RotateLogIfNeeded()
    {
        try
        {
            if (File.Exists(DebugLogFilePath) && new FileInfo(DebugLogFilePath).Length > MaxLogFileSizeBytes)
            {
                string archivePath = DebugLogFilePath + ".old";
                File.Move(DebugLogFilePath, archivePath, overwrite: true);
            }
        }
        catch
        {
        }
    }
}
