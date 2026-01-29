using HiHi2.AtlasTools;
using UnityEngine;

public static class AtlasTextureUtility
{
    public static Texture2D CreateReadableTexture(Texture2D source, int targetWidth, int targetHeight)
    {
        if (source == null || targetWidth <= 0 || targetHeight <= 0)
        {
            AtlasLogger.LogError("Invalid parameters for CreateReadableTexture");
            return null;
        }

        RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Graphics.Blit(source, rt);
        GL.sRGBWrite = true;

        Texture2D readable = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false, false);
        readable.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        readable.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        return readable;
    }

    public static void CleanupCache()
    {
    }

    public static void ApplyPaddingFillOptimized(Texture2D atlas, Rect spriteRect, Texture2D source, int padding)
    {
        if (padding == 0 || atlas == null || source == null)
            return;

        int x = (int)spriteRect.x;
        int y = (int)spriteRect.y;
        int w = (int)spriteRect.width;
        int h = (int)spriteRect.height;

        for (int px = 0; px < w; px++)
        {
            Color edgeColor = source.GetPixel(px, h - 1);
            for (int p = 1; p <= padding; p++)
            {
                atlas.SetPixel(x + px, y + h + p - 1, edgeColor);
            }
        }

        for (int px = 0; px < w; px++)
        {
            Color edgeColor = source.GetPixel(px, 0);
            for (int p = 1; p <= padding; p++)
            {
                atlas.SetPixel(x + px, y - p, edgeColor);
            }
        }

        for (int py = 0; py < h; py++)
        {
            Color edgeColor = source.GetPixel(0, py);
            for (int p = 1; p <= padding; p++)
            {
                atlas.SetPixel(x - p, y + py, edgeColor);
            }
        }

        for (int py = 0; py < h; py++)
        {
            Color edgeColor = source.GetPixel(w - 1, py);
            for (int p = 1; p <= padding; p++)
            {
                atlas.SetPixel(x + w + p - 1, y + py, edgeColor);
            }
        }

        Color topLeftColor = source.GetPixel(0, h - 1);
        Color topRightColor = source.GetPixel(w - 1, h - 1);
        Color bottomLeftColor = source.GetPixel(0, 0);
        Color bottomRightColor = source.GetPixel(w - 1, 0);

        for (int py = 0; py < padding; py++)
        {
            for (int px = 0; px < padding; px++)
            {
                atlas.SetPixel(x - padding + px, y + h + py, topLeftColor);
                atlas.SetPixel(x + w + px, y + h + py, topRightColor);
                atlas.SetPixel(x - padding + px, y - padding + py, bottomLeftColor);
                atlas.SetPixel(x + w + px, y - padding + py, bottomRightColor);
            }
        }
    }
}