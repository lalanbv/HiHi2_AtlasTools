using UnityEngine;
using System.Collections.Generic;

namespace HiHi2.AtlasTools
{
[System.Serializable]
public class SpriteInfoData
{
    public string spriteName;
    public Rect uvRect;

    // 原始尺寸（应用 MaxSize 后的尺寸，无任何 padding 缩减）
    public Vector2Int originalSize;

    // 压缩后尺寸 / 内容尺寸：在打 padding 前，为了腾出 padding 而缩小后的尺寸
    // 也是 packed.rect.width / height 对应的尺寸（即 UV 对应的真实内容区域大小）
    public Vector2Int resizedSize;

    // 含 Padding 的占用尺寸：resizedSize + padding * 2
    public Vector2Int paddedSize;

    // 当前图片使用的 padding（通常与图集 padding 一致）
    public int padding;

    public string sourceTexturePath;
}

[CreateAssetMenu(fileName = "AtlasConfig", menuName = "HiHi2/Atlas/Atlas Config")]
public class AtlasConfig : ScriptableObject
{
    public Texture2D atlasTexture;
    public List<SpriteInfoData> spriteInfos = new List<SpriteInfoData>();

    [Header("图集信息")]
    public int atlasWidth;
    public int atlasHeight;
    public int spriteCount;
    // 新增：生成该图集时使用的 padding（像素）
    public int padding;
    public float wastagePercent;

    private Dictionary<string, SpriteInfoData> _lookupCache;

    private void OnValidate()
    {
        _lookupCache = null;
    }

    public SpriteInfoData GetSpriteInfo(string name)
    {
        if (_lookupCache == null)
        {
            _lookupCache = new Dictionary<string, SpriteInfoData>();
            foreach (var info in spriteInfos)
            {
                if (!string.IsNullOrEmpty(info?.spriteName))
                {
                    _lookupCache[info.spriteName] = info;
                }
            }
        }

        return _lookupCache.TryGetValue(name, out var result) ? result : null;
    }
    }
}