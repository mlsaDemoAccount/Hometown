using System.Collections.Generic;
using UnityEngine;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

/// QR code drawing + reading with the ZXing core library (no Unity-specific ZXing parts needed).
public static class QrCodeUtil
{
    /// Draws text as a black-on-white QR code texture. The caller owns (and should Destroy) it.
    public static Texture2D Encode(string text, int size = 512)
    {
        var hints = new Dictionary<EncodeHintType, object> { { EncodeHintType.MARGIN, 2 } };
        BitMatrix matrix = new QRCodeWriter().encode(text, BarcodeFormat.QR_CODE, size, size, hints);

        var pixels = new Color32[size * size];
        var black = new Color32(0, 0, 0, 255);
        var white = new Color32(255, 255, 255, 255);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = matrix[x, size - 1 - y] ? black : white; // texture rows run bottom-up

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static readonly BarcodeReaderGeneric reader = new BarcodeReaderGeneric
    {
        AutoRotate = true,
        Options = new DecodingOptions
        {
            PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
            TryHarder = true
        }
    };

    private static byte[] rgbBuffer;

    /// Looks for a QR code in a camera frame. Returns its text, or null if none was readable.
    public static string Decode(Color32[] pixels, int width, int height)
    {
        int needed = width * height * 3;
        if (rgbBuffer == null || rgbBuffer.Length != needed) rgbBuffer = new byte[needed];

        // Camera rows run bottom-up; flip so the code isn't read mirrored.
        int i = 0;
        for (int y = height - 1; y >= 0; y--)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                Color32 c = pixels[row + x];
                rgbBuffer[i++] = c.r;
                rgbBuffer[i++] = c.g;
                rgbBuffer[i++] = c.b;
            }
        }

        try
        {
            var result = reader.Decode(rgbBuffer, width, height, RGBLuminanceSource.BitmapFormat.RGB24);
            return result != null ? result.Text : null;
        }
        catch (System.Exception)
        {
            return null;
        }
    }
}
