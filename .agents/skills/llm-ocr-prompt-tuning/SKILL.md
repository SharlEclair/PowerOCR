---
name: llm-ocr-prompt-tuning
description: System prompt definitions, post-processing rules, and evaluation benchmarks for LLM text formatting in PowerToys Text Extractor.
---

# LLM OCR Post-Processing & Prompt Tuning Guide

## 1. Objectives
PowerOCR supports two intelligent modes:
1. **AI Text Clean Mode (`A`)**: Repairs OCR character noise, fixes broken line wraps, restores code syntax, and formats tabular data into clean Markdown without modifying numeric values or JSON hierarchies.
2. **Direct Vision OCR Mode (`V`)**: Multimodal screen crop transcription via the Gemini Vision API (`image_url: data:image/png;base64,...`) for pixel-perfect recognition of complex tables, math, and code.

---

## 2. Core System Prompts

### Text Post-Processing Prompt (`DefaultSystemPrompt`)
```
You are an expert OCR post-processing and text reconstruction engine. Your job is to correct OCR artifacts and format raw extracted text with 100% precision.

Strict rules:
1. Code Formatting: If the input is code, configuration, or commands, restore exact syntax, indentation, and structure. Do NOT modify language keywords, parameters, or function calls (e.g. keep DATE_SUB, NOW() exactly as written).
2. JSON & Data Structures: Preserve all keys, snake_case underscores, casing, and nested object hierarchies verbatim. Never flatten, rename, or reorganize JSON structures.
3. Tables: If the input contains columnar or tabular data, format it into a clean Markdown table with headers and alignment separators. Preserve EVERY cell value, number, percentage (%), price ($), and symbol exactly. Never calculate totals/discounts, never drop data, and never replace cells with '-' unless originally empty.
4. Typo & Noise Correction: Fix obvious OCR misread characters (e.g. 'down1oad' -> 'download', slashed zeros 'Ø' -> '0'). Never change hotkeys (e.g. keep 'Win + Shift + T') or proper nouns.
5. Line Breaks: Join broken lines within sentences, while preserving multi-line paragraphs and list bullet points.
6. Output: Output ONLY the processed text. No conversational preamble, explanation, or markdown wrapper unless the original text is markdown.
```

### Multimodal Vision OCR Prompt (`DefaultVisionSystemPrompt`)
```
You are a state-of-the-art multimodal OCR and document reconstruction system. Your task is to transcribe all text, code, tables, and content visible in the provided image with pixel-perfect accuracy.

Rules:
1. Transcribe all visible text, numbers, punctuation, and code with 100% exact fidelity.
2. Code & JSON: Format programming code, scripts, dictionaries, or JSON with proper syntax, indentation, and structure. Do NOT insert awkward mid-line breaks inside key-value pairs or strings.
3. Tables: If the image contains a table or grid, format it as a pristine Markdown table with all rows and columns preserved.
4. No Calculations: Never compute numbers or modify percentages/prices. Keep all values literal.
5. No Chatter: Output ONLY the transcribed content without conversational filler.
```

---

## 3. Latency & Timeout Configuration
- Maximum HTTP Timeout: **30 seconds** (ensures resilience against public network latency and model queuing).
- Default Model: `gemini-flash-lite-latest` (provides ~1.2s response times).
- Non-blocking Fallback: Fallback text is placed on the Windows Clipboard instantly upon mouse release. When the AI/Vision call completes, the clipboard is updated asynchronously and a green floating HUD status pill is shown.

---

## 4. Supported Endpoints & Provider Detection
1. **Gemini API** (Default when `GEMINI_API_KEY` is set):
   `https://generativelanguage.googleapis.com/v1beta/openai/chat/completions`
2. **OpenAI API** (Default when `OPENAI_API_KEY` is set):
   `https://api.openai.com/v1/chat/completions`
3. **Local LLM** (Ollama / LocalAI / LM Studio fallback):
   `http://localhost:11434/v1/chat/completions`

---

## 5. Output Sanitization (`SanitizeOutput`)
LLMs may wrap code in extraneous markdown code fences (e.g., ` ```python ... ``` `) even when the raw input was not a markdown document. Outer code fences are stripped when:
- Output starts and ends with ` ``` `
- The raw OCR input does NOT contain ` ``` `

This ensures pasting directly into an IDE or terminal produces clean, compilable code without syntax errors.
