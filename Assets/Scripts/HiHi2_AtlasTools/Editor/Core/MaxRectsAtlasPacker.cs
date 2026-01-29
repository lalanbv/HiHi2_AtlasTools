using UnityEngine;
using System.Collections.Generic;
using HiHi2.AtlasTools;

public class MaxRectsAtlasPacker
{
    private readonly List<Rect> freeRects = new List<Rect>(32);
    private readonly List<PackedSprite> packedSprites = new List<PackedSprite>(32);
    private int currentPadding;

    public class PackedSprite
    {
        public TextureInfo textureInfo;
        public Rect rect;
    }

    public bool TryPack(List<TextureInfo> textures, int atlasWidth, int atlasHeight, int padding, out List<PackedSprite> result)
    {
        if (textures == null || textures.Count == 0)
        {
            result = null;
            return false;
        }

        freeRects.Clear();
        packedSprites.Clear();
        freeRects.Add(new Rect(0, 0, atlasWidth, atlasHeight));
        currentPadding = padding;

        foreach (var texInfo in textures)
        {
            if (texInfo == null || !texInfo.IsValid)
            {
                AtlasLogger.LogWarning($"Skipping invalid texture info");
                continue;
            }

            int width = texInfo.width + padding * 2;
            int height = texInfo.height + padding * 2;

            Rect? bestRect = FindBestRect(width, height);

            if (!bestRect.HasValue)
            {
                result = null;
                return false;
            }

            Rect placed = bestRect.Value;
            packedSprites.Add(new PackedSprite
            {
                textureInfo = texInfo,
                rect = new Rect(placed.x + padding, placed.y + padding, texInfo.width, texInfo.height)
            });

            SplitFreeRects(new Rect(placed.x, placed.y, width, height));
        }

        result = packedSprites;
        return true;
    }

    private Rect? FindBestRect(int width, int height)
    {
        Rect? bestRect = null;
        int bestShortSideFit = int.MaxValue;
        int bestLongSideFit = int.MaxValue;

        int freeRectCount = freeRects.Count;
        for (int i = 0; i < freeRectCount; i++)
        {
            Rect rect = freeRects[i];
            if (rect.width >= width && rect.height >= height)
            {
                int leftoverHoriz = (int)rect.width - width;
                int leftoverVert = (int)rect.height - height;
                int shortSideFit = Mathf.Min(leftoverHoriz, leftoverVert);
                int longSideFit = Mathf.Max(leftoverHoriz, leftoverVert);

                if (shortSideFit < bestShortSideFit ||
                    (shortSideFit == bestShortSideFit && longSideFit < bestLongSideFit))
                {
                    bestRect = new Rect(rect.x, rect.y, width, height);
                    bestShortSideFit = shortSideFit;
                    bestLongSideFit = longSideFit;
                }
            }
        }

        return bestRect;
    }

    private void SplitFreeRects(Rect usedRect)
    {
        List<Rect> newRects = new List<Rect>(8);

        for (int i = freeRects.Count - 1; i >= 0; i--)
        {
            if (SplitRect(freeRects[i], usedRect, out List<Rect> splits))
            {
                freeRects.RemoveAt(i);
                newRects.AddRange(splits);
            }
        }

        freeRects.AddRange(newRects);
        PruneFreeRects();
    }

    private bool SplitRect(Rect freeRect, Rect usedRect, out List<Rect> splits)
    {
        splits = new List<Rect>(4);

        if (!freeRect.Overlaps(usedRect))
            return false;

        if (usedRect.x > freeRect.x && usedRect.x < freeRect.xMax)
        {
            splits.Add(new Rect(freeRect.x, freeRect.y, usedRect.x - freeRect.x, freeRect.height));
        }

        if (usedRect.xMax < freeRect.xMax)
        {
            splits.Add(new Rect(usedRect.xMax, freeRect.y, freeRect.xMax - usedRect.xMax, freeRect.height));
        }

        if (usedRect.y > freeRect.y && usedRect.y < freeRect.yMax)
        {
            splits.Add(new Rect(freeRect.x, freeRect.y, freeRect.width, usedRect.y - freeRect.y));
        }

        if (usedRect.yMax < freeRect.yMax)
        {
            splits.Add(new Rect(freeRect.x, usedRect.yMax, freeRect.width, freeRect.yMax - usedRect.yMax));
        }

        return true;
    }

    private void PruneFreeRects()
    {
        for (int i = 0; i < freeRects.Count; i++)
        {
            for (int j = i + 1; j < freeRects.Count; j++)
            {
                if (IsContainedIn(freeRects[i], freeRects[j]))
                {
                    freeRects.RemoveAt(i);
                    i--;
                    break;
                }

                if (IsContainedIn(freeRects[j], freeRects[i]))
                {
                    freeRects.RemoveAt(j);
                    j--;
                }
            }
        }
    }

    private bool IsContainedIn(Rect a, Rect b)
    {
        return a.x >= b.x && a.y >= b.y && a.xMax <= b.xMax && a.yMax <= b.yMax;
    }

    public float CalculateWastage(int atlasWidth, int atlasHeight)
    {
        if (atlasWidth <= 0 || atlasHeight <= 0)
            return 100f;

        long usedArea = 0;
        foreach (var packed in packedSprites)
        {
            int rectWidthWithPadding = (int)packed.rect.width + currentPadding * 2;
            int rectHeightWithPadding = (int)packed.rect.height + currentPadding * 2;
            usedArea += rectWidthWithPadding * rectHeightWithPadding;
        }

        long totalArea = (long)atlasWidth * atlasHeight;
        return ((float)(totalArea - usedArea) / totalArea) * 100f;
    }

    public void Clear()
    {
        freeRects.Clear();
        packedSprites.Clear();
        currentPadding = 0;
    }
}