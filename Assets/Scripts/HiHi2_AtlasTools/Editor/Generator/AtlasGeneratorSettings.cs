using UnityEngine;

[CreateAssetMenu(fileName = "AtlasGeneratorSettings", menuName = "Atlas/Generator Settings")]
public class AtlasGeneratorSettings : ScriptableObject
{
    [Header("图集设置")]
    [Range(0, 16)]
    [Tooltip("图片间距（必须为偶数）")]
    public int padding = 2;

    [Range(0, 50)]
    [Tooltip("允许的最大空白区域百分比")]
    public float maxWastagePercent = 25f;

    [Header("尺寸限制")]
    [Tooltip("最小图集尺寸")]
    public int minAtlasSize = 32;

    [Tooltip("最大图集尺寸")]
    public int maxAtlasSize = 4096;

    [Header("其他选项")]
    [Tooltip("是否允许生成多张图集")]
    public bool allowMultipleAtlases = true;

    [Tooltip("图集命名前缀")]
    public string atlasNamePrefix = "Atlas";

    private void OnValidate()
    {
        // 确保padding是偶数
        if (padding % 2 != 0)
        {
            padding = Mathf.Max(0, padding - 1);
            Debug.LogWarning("Padding必须为偶数，已自动调整为：" + padding);
        }

        // 确保尺寸是2的幂次方
        minAtlasSize = Mathf.ClosestPowerOfTwo(Mathf.Clamp(minAtlasSize, 32, 4096));
        maxAtlasSize = Mathf.ClosestPowerOfTwo(Mathf.Clamp(maxAtlasSize, 32, 4096));

        if (minAtlasSize > maxAtlasSize)
        {
            minAtlasSize = maxAtlasSize;
        }
    }
}