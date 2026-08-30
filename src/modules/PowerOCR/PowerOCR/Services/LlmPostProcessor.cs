// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using PowerOCR.Helpers;

namespace PowerOCR.Services;

/// <summary>
/// Service that sends raw OCR text to an LLM endpoint for post-processing cleanup and formatting.
/// </summary>
public class LlmPostProcessor : ILlmPostProcessor
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly HttpClient SharedHttpClient = CreateConfiguredHttpClient();

    private static readonly Lazy<LlmPostProcessor> LazyInstance = new(() => new LlmPostProcessor());

    public static LlmPostProcessor Instance => LazyInstance.Value;

    private readonly HttpClient _httpClient;
    private readonly string _endpointUrl;
    private readonly string _modelName;
    private readonly string? _apiKey;

    private const string DefaultSystemPrompt =
        "You are an expert OCR post-processing and text reconstruction engine. Your job is to correct OCR artifacts and format raw extracted text with 100% precision.\n\n" +
        "Strict rules:\n" +
        "1. Code Formatting: If the input is code, configuration, or commands, restore exact syntax, indentation, and structure. Do NOT modify language keywords, parameters, or function calls (e.g. keep DATE_SUB, NOW() exactly as written).\n" +
        "2. JSON & Data Structures: Preserve all keys, snake_case underscores, casing, and nested object hierarchies verbatim. Never flatten, rename, or reorganize JSON structures.\n" +
        "3. Tables: If the input contains columnar or tabular data, format it into a clean Markdown table with headers and alignment separators. Preserve EVERY cell value, number, percentage (%), price ($), and symbol exactly. Never calculate totals/discounts, never drop data, and never replace cells with '-' unless originally empty.\n" +
        "4. Typo & Noise Correction: Fix obvious OCR misread characters (e.g. 'down1oad' -> 'download', slashed zeros 'Ø' -> '0'). Never change hotkeys (e.g. keep 'Win + Shift + T') or proper nouns.\n" +
        "5. Line Breaks: Join broken lines within sentences, while preserving multi-line paragraphs and list bullet points.\n" +
        "6. Output: Output ONLY the processed text. No conversational preamble, explanation, or markdown wrapper unless the original text is markdown.";

    private const string DefaultVisionSystemPrompt =
        "You are a state-of-the-art multimodal OCR and document reconstruction system. Your task is to transcribe all text, code, tables, and content visible in the provided image with pixel-perfect accuracy.\n\n" +
        "Rules:\n" +
        "1. Transcribe all visible text, numbers, punctuation, and code with 100% exact fidelity.\n" +
        "2. Code & JSON: Format programming code, scripts, dictionaries, or JSON with proper syntax, indentation, and structure. Do NOT insert awkward mid-line breaks inside key-value pairs or strings.\n" +
        "3. Tables: If the image contains a table or grid, format it as a pristine Markdown table with all rows and columns preserved.\n" +
        "4. No Calculations: Never compute numbers or modify percentages/prices. Keep all values literal.\n" +
        "5. No Chatter: Output ONLY the transcribed content without conversational filler.";

    public LlmPostProcessor(HttpClient? httpClient = null, string? endpointUrl = null, string? modelName = null, string? apiKey = null)
    {
        _httpClient = httpClient ?? SharedHttpClient;

        string? geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        string? envKey = Environment.GetEnvironmentVariable("TEXT_EXTRACTOR_LLM_API_KEY")
            ?? geminiKey
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        _apiKey = apiKey ?? envKey;

        string? envEndpoint = Environment.GetEnvironmentVariable("TEXT_EXTRACTOR_LLM_ENDPOINT")
            ?? Environment.GetEnvironmentVariable("POWERTOYS_LLM_ENDPOINT")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_BASE");

        if (!string.IsNullOrWhiteSpace(endpointUrl))
        {
            _endpointUrl = endpointUrl;
        }
        else if (!string.IsNullOrWhiteSpace(envEndpoint))
        {
            _endpointUrl = NormalizeEndpoint(envEndpoint);
        }
        else if (!string.IsNullOrWhiteSpace(geminiKey))
        {
            _endpointUrl = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
        }
        else if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _endpointUrl = "https://api.openai.com/v1/chat/completions";
        }
        else
        {
            // Default to local OpenAI-compatible endpoint (Ollama / LocalAI / LM Studio)
            _endpointUrl = "http://localhost:11434/v1/chat/completions";
        }

        string? envModel = Environment.GetEnvironmentVariable("TEXT_EXTRACTOR_LLM_MODEL")
            ?? Environment.GetEnvironmentVariable("POWERTOYS_LLM_MODEL");

        if (!string.IsNullOrWhiteSpace(modelName))
        {
            _modelName = modelName;
        }
        else if (!string.IsNullOrWhiteSpace(envModel))
        {
            _modelName = envModel;
        }
        else if (!string.IsNullOrWhiteSpace(geminiKey))
        {
            _modelName = "gemini-flash-lite-latest";
        }
        else if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _modelName = "gpt-4o-mini";
        }
        else
        {
            _modelName = "llama3";
        }

        // Normalize model aliases if an invalid/retired/slow model name is passed
        if (_modelName.StartsWith("gemini-2.5", StringComparison.OrdinalIgnoreCase) ||
            _modelName.StartsWith("gemini-3.6", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(_modelName, "gemini-flash-lite", StringComparison.OrdinalIgnoreCase))
        {
            _modelName = "gemini-flash-lite-latest";
        }
    }

    private static HttpClient CreateConfiguredHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            ConnectTimeout = DefaultTimeout,
        };

        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan, // Let per-request CTS handle strict timeout
        };
    }

    private static string NormalizeEndpoint(string endpoint)
    {
        string trimmed = endpoint.Trim().TrimEnd('/');
        if (!trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                return $"{trimmed}/chat/completions";
            }

            return $"{trimmed}/v1/chat/completions";
        }

        return trimmed;
    }

    /// <inheritdoc/>
    public async Task<string?> ProcessTextAsync(string rawText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return rawText;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(DefaultTimeout);

        try
        {
            var requestBody = new
            {
                model = _modelName,
                messages = new object[]
                {
                    new { role = "system", content = DefaultSystemPrompt },
                    new { role = "user", content = rawText },
                },
                temperature = 0.1,
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpointUrl)
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json"),
            };

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey.Trim());
            }

            DebugLogger.LogInfo($"Sending OCR text to LLM endpoint: {_endpointUrl} (Model: {_modelName}, Raw text length: {rawText.Length} chars)");

            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                linkedCts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);
                DebugLogger.LogWarning($"LLM post-processing returned non-success HTTP status code {(int)response.StatusCode}: {errorContent}");
                return null;
            }

            string responseJson = await response.Content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);
            string? result = ParseCompletionResponse(responseJson, rawText);
            DebugLogger.LogInfo($"LLM post-processing successfully parsed response (Cleaned length: {result?.Length ?? 0} chars).");
            return result;
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            DebugLogger.LogWarning($"LLM post-processing timed out after {DefaultTimeout.TotalSeconds} seconds.");
            return null;
        }
        catch (OperationCanceledException)
        {
            DebugLogger.LogInfo("LLM post-processing operation was cancelled.");
            return null;
        }
        catch (HttpRequestException httpEx)
        {
            DebugLogger.LogWarning($"LLM post-processing HTTP request failed: {httpEx.Message}");
            return null;
        }
        catch (Exception ex)
        {
            DebugLogger.LogError($"Unexpected exception in LLM post-processing: {ex.Message}", ex);
            return null;
        }
    }

    public async Task<string?> ProcessImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            return null;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(DefaultTimeout);

        try
        {
            string base64Image = Convert.ToBase64String(imageBytes);
            string dataUrl = $"data:image/png;base64,{base64Image}";

            var requestBody = new
            {
                model = _modelName,
                messages = new object[]
                {
                    new { role = "system", content = DefaultVisionSystemPrompt },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = "Extract and format all text, code, tables, and content visible in this screenshot with exact fidelity. Output ONLY the extracted text." },
                            new
                            {
                                type = "image_url",
                                image_url = new { url = dataUrl },
                            },
                        },
                    },
                },
                temperature = 0.1,
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpointUrl)
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json"),
            };

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey.Trim());
            }

            DebugLogger.LogInfo($"Sending crop image ({imageBytes.Length / 1024} KB) to Gemini Vision endpoint: {_endpointUrl} (Model: {_modelName})");

            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                linkedCts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);
                DebugLogger.LogWarning($"Gemini Vision returned non-success HTTP status code {(int)response.StatusCode}: {errorContent}");
                return null;
            }

            string responseJson = await response.Content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);
            string? result = ParseCompletionResponse(responseJson, rawText: string.Empty);
            DebugLogger.LogInfo($"Gemini Vision successfully transcribed image (Extracted length: {result?.Length ?? 0} chars).");
            return result;
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            DebugLogger.LogWarning($"Gemini Vision timed out after {DefaultTimeout.TotalSeconds} seconds.");
            return null;
        }
        catch (OperationCanceledException)
        {
            DebugLogger.LogInfo("Gemini Vision operation was cancelled.");
            return null;
        }
        catch (HttpRequestException httpEx)
        {
            DebugLogger.LogWarning($"Gemini Vision HTTP request failed: {httpEx.Message}");
            return null;
        }
        catch (Exception ex)
        {
            DebugLogger.LogError($"Unexpected exception in Gemini Vision: {ex.Message}", ex);
            return null;
        }
    }

    private static string? ParseCompletionResponse(string jsonResponse, string rawText)
    {
        if (string.IsNullOrWhiteSpace(jsonResponse))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("choices", out JsonElement choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                JsonElement firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out JsonElement message) &&
                    message.TryGetProperty("content", out JsonElement content))
                {
                    string? result = content.GetString();
                    if (string.IsNullOrWhiteSpace(result))
                    {
                        return null;
                    }

                    return SanitizeOutput(result.Trim(), rawText);
                }
            }

            Logger.LogWarning("LLM response did not contain expected choices/message/content structure.");
            return null;
        }
        catch (JsonException jEx)
        {
            Logger.LogError($"Failed to parse JSON response from LLM: {jEx.Message}", jEx);
            return null;
        }
    }

    private static string SanitizeOutput(string output, string rawText)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return output;
        }

        string trimmed = output.Trim();

        // If the model wrapped the response in a single markdown code block and raw text did not have one
        if (trimmed.StartsWith("```", StringComparison.Ordinal) && trimmed.EndsWith("```", StringComparison.Ordinal) && !rawText.Contains("```", StringComparison.Ordinal))
        {
            int firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 3 && trimmed.Length > firstNewline + 3)
            {
                string inner = trimmed.Substring(firstNewline + 1, trimmed.Length - firstNewline - 4).Trim();
                if (!string.IsNullOrEmpty(inner))
                {
                    return inner;
                }
            }
        }

        return trimmed;
    }
}
