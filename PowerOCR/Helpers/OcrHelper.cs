using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace PowerOCR.Helpers;

public static class OcrHelper
{
    public static async Task<string> ExtractTextAsync(Bitmap gdiBitmap)
    {
        if (gdiBitmap == null)
        {
            throw new ArgumentNullException(nameof(gdiBitmap));
        }

        // 1. Save GDI bitmap to standard MemoryStream as PNG
        using var memoryStream = new MemoryStream();
        gdiBitmap.Save(memoryStream, ImageFormat.Png);
        memoryStream.Position = 0;

        // 2. Convert to WinRT IRandomAccessStream using WindowsRuntimeStreamExtensions
        using IRandomAccessStream randomAccessStream = WindowsRuntimeStreamExtensions.AsRandomAccessStream(memoryStream);

        // 3. Decode into a SoftwareBitmap
        var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
        using SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

        SoftwareBitmap bitmapToProcess = softwareBitmap;
        SoftwareBitmap? convertedBitmap = null;

        try
        {
            // Ensure SoftwareBitmap is in a pixel format and alpha mode supported by OcrEngine
            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                softwareBitmap.BitmapAlphaMode == BitmapAlphaMode.Straight)
            {
                convertedBitmap = SoftwareBitmap.Convert(
                    softwareBitmap,
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);
                bitmapToProcess = convertedBitmap;
            }

            // 4. Create WinRT OcrEngine from user profile languages (with fallback if needed)
            OcrEngine? ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (ocrEngine == null)
            {
                if (OcrEngine.AvailableRecognizerLanguages.Count > 0)
                {
                    ocrEngine = OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0]);
                }

                if (ocrEngine == null)
                {
                    throw new InvalidOperationException("No supported OCR language packs are installed or available on this system.");
                }
            }

            // 5. Run OCR recognition
            OcrResult ocrResult = await ocrEngine.RecognizeAsync(bitmapToProcess);
            return ocrResult.Text ?? string.Empty;
        }
        finally
        {
            convertedBitmap?.Dispose();
        }
    }
}
