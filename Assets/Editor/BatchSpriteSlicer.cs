using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public class BatchSpriteSlicer : EditorWindow
{
    private int cellWidth = 64;
    private int cellHeight = 64;

    private int offsetX = 0;
    private int offsetY = 0;

    private int paddingX = 0;
    private int paddingY = 0;

    [MenuItem("Tools/Sprite/批量切割 Sprite")]
    private static void OpenWindow()
    {
        GetWindow<BatchSpriteSlicer>("批量切割 Sprite");
    }

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

        Texture2D[] textures =
            Selection.GetFiltered<Texture2D>(SelectionMode.Assets);

        EditorGUILayout.LabelField(
            $"当前选中图片：{textures.Length} 张"
        );

        EditorGUILayout.Space();

        if (GUILayout.Button("批量设置 Multiple 并切割", GUILayout.Height(40)))
        {
            SliceSelectedTextures(textures);
        }
    }

    private void SliceSelectedTextures(Texture2D[] textures)
    {
        if (textures == null || textures.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "提示",
                "请先在 Project 窗口选择图片。",
                "确定"
            );

            return;
        }

        if (cellWidth <= 0 || cellHeight <= 0)
        {
            EditorUtility.DisplayDialog(
                "错误",
                "切割宽高必须大于 0。",
                "确定"
            );

            return;
        }

        SpriteDataProviderFactories factory =
            new SpriteDataProviderFactories();

        factory.Init();

        int successCount = 0;

        foreach (Texture2D texture in textures)
        {
            string path = AssetDatabase.GetAssetPath(texture);

            TextureImporter importer =
                AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
                continue;

            // ===================================
            // 1. 设置为 Sprite + Multiple
            // ===================================

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;

            importer.SaveAndReimport();

            // 重新获取 Texture，避免 Reimport 后引用问题
            Texture2D currentTexture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (currentTexture == null)
                continue;

            // ===================================
            // 2. 获取 Sprite Editor 数据
            // ===================================

            ISpriteEditorDataProvider dataProvider =
                factory.GetSpriteEditorDataProviderFromObject(
                    currentTexture
                );

            if (dataProvider == null)
                continue;

            dataProvider.InitSpriteEditorDataProvider();

            List<SpriteRect> spriteRects =
                new List<SpriteRect>();

            int row = 0;

            // Sprite Rect 坐标原点在左下角
            for (
                int y = currentTexture.height - offsetY - cellHeight;
                y >= 0;
                y -= cellHeight + paddingY
            )
            {
                int column = 0;

                for (
                    int x = offsetX;
                    x + cellWidth <= currentTexture.width;
                    x += cellWidth + paddingX
                )
                {
                    SpriteRect spriteRect = new SpriteRect
                    {
                        name =
                            $"{currentTexture.name}_{row}_{column}",

                        rect = new Rect(
                            x,
                            y,
                            cellWidth,
                            cellHeight
                        ),

                        alignment = SpriteAlignment.Center,

                        pivot = new Vector2(
                            0.5f,
                            0.5f
                        ),

                        spriteID = GUID.Generate()
                    };

                    spriteRects.Add(spriteRect);

                    column++;
                }

                row++;
            }

            // ===================================
            // 3. 写入 Sprite Rect
            // ===================================

            dataProvider.SetSpriteRects(
                spriteRects.ToArray()
            );

            // Unity 2021.2+ 需要同步 Sprite Name 和 ID
            ISpriteNameFileIdDataProvider nameProvider =
                dataProvider
                    .GetDataProvider<ISpriteNameFileIdDataProvider>();

            if (nameProvider != null)
            {
                var namePairs =
                    spriteRects.Select(
                        sprite =>
                            new SpriteNameFileIdPair(
                                sprite.name,
                                sprite.spriteID
                            )
                    );

                nameProvider.SetNameFileIdPairs(namePairs);
            }

            dataProvider.Apply();

            AssetImporter spriteImporter =
                dataProvider.targetObject as AssetImporter;

            if (spriteImporter != null)
            {
                spriteImporter.SaveAndReimport();
            }

            successCount++;
        }

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "完成",
            $"成功处理 {successCount} 张图片。",
            "确定"
        );
    }
}