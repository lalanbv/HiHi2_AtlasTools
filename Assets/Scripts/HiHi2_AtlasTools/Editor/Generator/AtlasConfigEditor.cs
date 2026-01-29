using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using HiHi2.AtlasTools;

[CustomEditor(typeof(AtlasConfig))]
public class AtlasConfigEditor : Editor
{
    private bool showSpriteList = true;
    private Dictionary<string, bool> spriteFoldouts = new Dictionary<string, bool>();

    public override void OnInspectorGUI()
    {
        AtlasConfig config = (AtlasConfig)target;

        EditorGUILayout.LabelField("图集信息", EditorStyles.boldLabel);
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField("图集纹理", config.atlasTexture, typeof(Texture2D), false);
        EditorGUILayout.IntField("宽度", config.atlasWidth);
        EditorGUILayout.IntField("高度", config.atlasHeight);
        EditorGUILayout.IntField("图片数量", config.spriteCount);
        EditorGUILayout.FloatField("空白率 %", config.wastagePercent);
        EditorGUILayout.IntField("Padding", config.padding);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();

        showSpriteList = EditorGUILayout.Foldout(
            showSpriteList,
            $"包含的图片列表 ({config.spriteInfos.Count})",
            true,
            EditorStyles.foldoutHeader
        );

        if (showSpriteList && config.spriteInfos != null)
        {
            EditorGUI.indentLevel++;

            for (int i = 0; i < config.spriteInfos.Count; i++)
            {
                var spriteInfo = config.spriteInfos[i];
                if (spriteInfo == null) continue;

                if (!spriteFoldouts.ContainsKey(spriteInfo.spriteName))
                {
                    spriteFoldouts[spriteInfo.spriteName] = false;
                }

                EditorGUILayout.BeginVertical("box");

                spriteFoldouts[spriteInfo.spriteName] = EditorGUILayout.Foldout(
                    spriteFoldouts[spriteInfo.spriteName],
                    $"{i}. {spriteInfo.spriteName}",
                    true
                );

                if (spriteFoldouts[spriteInfo.spriteName])
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginDisabledGroup(true);

                    Texture2D sourceTexture = null;
                    if (!string.IsNullOrEmpty(spriteInfo.sourceTexturePath))
                    {
                        sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(spriteInfo.sourceTexturePath);
                    }

                    EditorGUILayout.ObjectField("源图片", sourceTexture, typeof(Texture2D), false);
                    EditorGUILayout.TextField("路径", spriteInfo.sourceTexturePath);

                    EditorGUILayout.Vector2IntField("原始尺寸", spriteInfo.originalSize);
                    EditorGUILayout.Vector2IntField("压缩后尺寸(不含Padding)", spriteInfo.resizedSize);
                    EditorGUILayout.Vector2IntField("含Padding尺寸", spriteInfo.paddedSize);
                    EditorGUILayout.IntField("单图Padding", spriteInfo.padding);

                    EditorGUILayout.RectField("UV矩形(内容区域)", spriteInfo.uvRect);

                    EditorGUI.EndDisabledGroup();
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("刷新显示"))
        {
            Repaint();
        }
    }
}