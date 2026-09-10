---
name: llm-ocr-prompt-tuning
description: System prompt definitions, post-processing rules, schemas, and evaluation benchmarks for LLM text formatting in PowerOCR.
---

# LLM OCR Post-Processing & Prompt Tuning Guide

## 1. Objectives
PowerOCR uses a high-speed, dual-stage pipeline:
1. **Stage 1 (Immediate Local OCR)**: Instantly extracts text using the local WinRT `OcrEngine`, writes it to the Windows Clipboard, and closes the capture overlay.
2. **Stage 2 (Background LLM Formatting)**: Asynchronously dispatches the raw text to a configured local or cloud LLM to repair OCR artifacts, fix broken line wraps, format tables into Markdown, and safely update the clipboard.

---

## 2. Production System Prompt

Defined in `PowerOCR.Services.LlmFormattingService`:

```text
You are an expert OCR post-processing and text reconstruction engine. Your task is to repair OCR artifacts and accurately format the extracted text.

Strict rules:
1. Repair OCR Noise: Fix obvious character misrecognitions (e.g., '0' vs 'O', '1' vs 'l' vs '|', broken accents, punctuation).
2. Fix Broken Line Wraps: Join hyphenated or broken mid-sentence line wraps into fluid prose, while strictly preserving natural paragraphs and list structures.
3. Code & Commands: If the input is code, scripts, or terminal commands, restore exact syntax, indentation, and structure. Do NOT modify variable names, parameters, or functions.
4. Tables: If the input represents columnar or tabular data, reconstruct it as a pristine Markdown table with aligned headers. Preserve all numeric values, prices, and percentages verbatim.
5. NO Conversational Filler: Do NOT include preamble, acknowledgments, greetings, or explanations (e.g., 'Here is the cleaned text:').
6. NO Outer Code Blocks: Do NOT wrap the entire output in triple backticks (```) unless the original input was explicitly and entirely a code block. Output ONLY the reconstructed text itself.
```

---

## 3. Strict 5-Second Timeout & Fault Tolerance
- **Timeout Policy**: Strict 5.0-second timeout enforced via linked `CancellationTokenSource`.
- **Silent Fallback**: If an HTTP request times out, encounters a network error, or the LLM returns an error status code, the exception is caught silently and `rawText` is returned unaltered.
- **No Clipboard Degradation**: The user always has the raw OCR text in their clipboard from Stage 1 even if the LLM is completely offline.

---

## 4. API Request Schemas & Provider Auto-Detection

`LlmFormattingService` dynamically constructs the HTTP payload based on the endpoint:

### A. Native Ollama Schema (`/api/generate`)
Used when the endpoint ends with `/api/generate` (e.g., `http://localhost:11434/api/generate`):
```json
{
  "model": "llama3.2",
  "prompt": "<raw-ocr-text>",
  "system": "<system-prompt>",
  "stream": false
}
```

### B. OpenAI-Compatible Schema (`/v1/chat/completions`)
Used for OpenAI, Google Gemini, Groq, and Ollama v1 endpoints:
```json
{
  "model": "gpt-4o-mini",
  "messages": [
    { "role": "system", "content": "<system-prompt>" },
    { "role": "user", "content": "<raw-ocr-text>" }
  ],
  "temperature": 0.1
}
```

---

## 5. Output Sanitization (`SanitizeOutput`)
Some LLMs spontaneously wrap text in markdown code blocks (` ``` `) even when instructed not to. `LlmFormattingService.SanitizeOutput()` enforces:
1. If `rawText` does NOT contain triple backticks (` ``` `):
2. Any outer markdown code block wrappers added by the LLM are stripped, extracting only the inner text.
3. If the sanitized text is whitespace or empty, `rawText` is returned.

---

## 6. Concurrency & Race Condition Safeguards
- **Inflight Cancellation**: If the user triggers a new snip while a prior LLM call is running, the prior `CancellationTokenSource` is immediately cancelled so old requests cannot overwrite the clipboard out of order.
- **External Modification Check**: Before overwriting the clipboard with LLM-enhanced text, `OcrPipelineManager` reads `Clipboard.GetContent()`. If the user copied something else in the interim, the overwrite is aborted.
- **Interactive Undo**: When the clipboard is updated by the LLM, an enhanced Windows Toast notification is displayed with an interactive **Undo** button allowing the user to restore the raw OCR text with a single click.

