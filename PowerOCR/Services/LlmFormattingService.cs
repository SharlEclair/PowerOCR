using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PowerOCR.Settings;

namespace PowerOCR.Services;

public sealed class LlmFormattingService
{
    private static readonly HttpClient HttpClient = new();

    private const string SystemPrompt =
        "You are an expert OCR post-processing and text reconstruction engine. " +
        "Your task is to repair OCR artifacts and accurately format the extracted text.\n\n" +
        "Strict rules:\n" +
        "1. Repair OCR Noise: Fix obvious character misrecognitions (e.g., '0' vs 'O', '1' vs 'l' vs '|', broken accents, punctuation).\n" +
        "2. Fix Broken Line Wraps: Join hyphenated or broken mid-sentence line wraps into fluid prose, while strictly preserving natural paragraphs and list structures.\n" +
        "3. Code & Commands: If the input is code, scripts, or terminal commands, restore exact syntax, indentation, and structure. Do NOT modify variable names, parameters, or functions.\n" +
        "4. Tables: If the input represents columnar or tabular data, reconstruct it as a pristine Markdown table with aligned headers. Preserve all numeric values, prices, and percentages verbatim.\n" +
        "5. NO Conversational Filler: Do NOT include preamble, acknowledgments, greetings, or explanations (e.g., 'Here is the cleaned text:').\n" +
        "6. NO Outer Code Blocks: Do NOT wrap the entire output in triple backticks (```) unless the original input was explicitly and entirely a code block. Output ONLY the reconstructed text itself.";

    public async Task<string> CleanAndFormatOcrTextAsync(string rawText, LlmConfig config, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return rawText;
        }

        // Strict 5-second timeout enforced via linked CancellationTokenSource
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            string endpoint = ResolveEndpoint(config);
            string model = string.IsNullOrWhiteSpace(config.Model)
                ? (config.Provider.Equals("ollama", StringComparison.OrdinalIgnoreCase) ? "llama3.2" : "gpt-4o-mini")
                : config.Model;

            object requestBody;
            if (endpoint.EndsWith("/api/generate", StringComparison.OrdinalIgnoreCase))
            {
                // Native Ollama /api/generate endpoint schema
                requestBody = new
                {
                    model = model,
                    prompt = rawText,
                    system = SystemPrompt,
                    stream = false
                };
            }
            else
            {
                // OpenAI-compatible /v1/chat/completions endpoint schema (OpenAI, Gemini, Ollama v1)
                requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "system", content = SystemPrompt },
                        new { role = "user", content = rawText }
                    },
                    temperature = 0.1
                };
            }

            string jsonPayload = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            if (!string.IsNullOrWhiteSpace(config.ApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
            }

            using HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return rawText;
            }

            string responseJson = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(responseJson);

            string? extractedContent = null;
            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0 &&
                choices[0].TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var contentProp))
            {
                extractedContent = contentProp.GetString();
            }
            else if (doc.RootElement.TryGetProperty("response", out var directResp))
            {
                extractedContent = directResp.GetString();
            }

            if (string.IsNullOrWhiteSpace(extractedContent))
            {
                return rawText;
            }

            return SanitizeOutput(extractedContent, rawText);
        }
        catch
        {
            // Silently return rawText unchanged on any network, cancellation, or parsing error
            return rawText;
        }
    }

    private static string ResolveEndpoint(LlmConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.Endpoint))
        {
            return config.Endpoint.Trim();
        }

        return config.Provider.ToLowerInvariant() switch
        {
            "gemini" => "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
            "ollama" => "http://localhost:11434/api/generate",
            _ => "https://api.openai.com/v1/chat/completions"
        };
    }

    private static string SanitizeOutput(string output, string rawText)
    {
        string trimmed = output.Trim();

        // If the original text did NOT have code fences, strip any unwanted outer code fences emitted by the LLM
        if (!rawText.Contains("```") && trimmed.StartsWith("```") && trimmed.EndsWith("```"))
        {
            int firstNewline = trimmed.IndexOf('\n');
            if (firstNewline != -1 && firstNewline < trimmed.Length - 3)
            {
                trimmed = trimmed.Substring(firstNewline + 1, trimmed.Length - firstNewline - 4).Trim();
            }
        }

        return string.IsNullOrWhiteSpace(trimmed) ? rawText : trimmed;
    }
}
