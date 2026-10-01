using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Batch-slices selected textures while preserving existing Sprite IDs whenever a generated name
/// still matches. Stable IDs prevent re-slicing from breaking prefab and animation references.
/// </summary>
public sealed class BatchSpriteSlicer : EditorWindow
{
    private int cellWidth = 64;
    private int cellHeight = 64;
    private int offsetX;
    private int offsetY;
    private int paddingX;
    private int paddingY;

    [MenuItem("Tools/Sprite/批量切割 Sprite")]
    private static void OpenWindow() => GetWindow<BatchSpriteSlicer>("批量切割 Sprite");

    private void OnGUI()
    {
        GUILayout.Label("批量 Sprite 切割", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        cellWidth = EditorGUILayout.IntField("单格宽度", cellWidth);
        cellHeight = EditorGUILayout.IntField("单格高度", cellHeight);
        EditorGUILayout.Space();

        offsetX = EditorGUILayout.IntField("Offset X", offsetX);
        offsetY = EditorGUILayout.IntField("Offset Y", offsetY);
        paddingX = EditorGUILayout.IntField("Padding X", paddingX);
        paddingY = EditorGUILayout.IntField("Padding Y", paddingY);
        EditorGUILayout.Space();

        Texture2D[] textures = Selection.GetFiltered<Texture2D>(SelectionMode.Assets);
        EditorGUILayout.LabelField($"当前选中图片：{textures.Length} 张");
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(textures.Length == 0))
        {
            if (GUILayout.Button("批量设置 Multiple 并切割", GUILayout.Height(40)))
                SliceSelectedTextures(textures);
        }
    }

    private void SliceSelectedTextures(Texture2D[] textures)
    {
        if (!ValidateSettings(out string validationError))
        {
            EditorUtility.DisplayDialog("切割设置无效", validationError, "确定");
            return;
        }

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        int successCount = 0;

        for (int i = 0; i < textures.Length; i++)
        {
            Texture2D texture = textures[i];
            string path = AssetDatabase.GetAssetPath(texture);
            try
            {
                if (SliceTexture(factory, path)) successCount++;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to slice texture '{path}': {exception.Message}", texture);
            }
        }

        EditorUtility.DisplayDialog("完成", $"成功处理 {successCount}/{textures.Length} 张图片。", "确定");
    }

    private bool SliceTexture(SpriteDataProviderFactories factory, string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return false;

        if (importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Multiple)
        {
            Undo.RegisterCompleteObjectUndo(importer, "Configure Sprite Importer");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.SaveAndReimport();
        }

        Texture2D currentTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(currentTexture);
        if (provider == null) return false;
        provider.InitSpriteEditorDataProvider();

        var existingIds = new Dictionary<string, GUID>(StringComparer.Ordinal);
        SpriteRect[] existingRects = provider.GetSpriteRects();
        for (int i = 0; i < existingRects.Length; i++)
        {
            SpriteRect existing = existingRects[i];
            if (!string.IsNullOrEmpty(existing.name) && !existingIds.ContainsKey(existing.name))
                existingIds.Add(existing.name, existing.spriteID);
        }

        List<SpriteRect> spriteRects = BuildSpriteRects(currentTexture, existingIds);
        if (spriteRects.Count == 0)
        {
            Debug.LogWarning("The slicing settings produced no sprites for " + path, currentTexture);
            return false;
        }

        provider.SetSpriteRects(spriteRects.ToArray());
        ISpriteNameFileIdDataProvider nameProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameProvider != null)
        {
            var pairs = new List<SpriteNameFileIdPair>(spriteRects.Count);
            for (int i = 0; i < spriteRects.Count; i++)
                pairs.Add(new SpriteNameFileIdPair(spriteRects[i].name, spriteRects[i].spriteID));
            nameProvider.SetNameFileIdPairs(pairs);
        }

        provider.Apply();
        importer.SaveAndReimport();
        return true;
    }

    private List<SpriteRect> BuildSpriteRects(Texture2D texture, Dictionary<string, GUID> existingIds)
    {
        var result = new List<SpriteRect>();
        int row = 0;
        for (int y = texture.height - offsetY - cellHeight; y >= 0; y -= cellHeight + paddingY)
        {
            int column = 0;
            for (int x = offsetX; x + cellWidth <= texture.width; x += cellWidth + paddingX)
            {
                string spriteName = $"{texture.name}_{row}_{column}";
                result.Add(new SpriteRect
                {
                    name = spriteName,
                    rect = new Rect(x, y, cellWidth, cellHeight),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = existingIds.TryGetValue(spriteName, out GUID id) ? id : GUID.Generate()
                });
                column++;
            }
            row++;
        }
        return result;
    }

    private bool ValidateSettings(out string error)
    {
        if (cellWidth <= 0 || cellHeight <= 0)
            error = "切割宽高必须大于 0。";
        else if (offsetX < 0 || offsetY < 0)
            error = "Offset 不能为负数。";
        else if (cellWidth + paddingX <= 0 || cellHeight + paddingY <= 0)
            error = "单格尺寸与 Padding 的和必须大于 0，避免无效循环。";
        else
            error = string.Empty;
        return string.IsNullOrEmpty(error);
    }
}
