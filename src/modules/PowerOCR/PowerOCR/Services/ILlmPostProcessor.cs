// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace PowerOCR.Services;

/// <summary>
/// Defines the contract for an LLM post-processing service that cleans and formats OCR text.
/// </summary>
public interface ILlmPostProcessor
{
    /// <summary>
    /// Asynchronously processes raw OCR text to clean errors, fix line breaks, restore code syntax, and format markdown tables.
    /// </summary>
    /// <param name="rawText">The raw OCR text extracted from the screen.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation or enforce timeouts.</param>
    /// <returns>The cleaned and formatted text, or null if the operation failed or was cancelled.</returns>
    Task<string?> ProcessTextAsync(string rawText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously performs multimodal Vision OCR on cropped screenshot image bytes.
    /// </summary>
    /// <param name="imageBytes">The raw PNG or JPEG bytes of the cropped screenshot.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation or enforce timeouts.</param>
    /// <returns>The vision-extracted and formatted text, or null if the operation failed or was cancelled.</returns>
    Task<string?> ProcessImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
}
