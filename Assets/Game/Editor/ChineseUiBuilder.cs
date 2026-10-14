using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>Authors a portable Chinese font and applies it to every game-owned UI prefab.</summary>
public static class ChineseUiBuilder
{
    public const string FontPath = "Assets/Game/Resources/Fonts/ChineseUI.asset";

    [MenuItem("Tools/Tiny Farm/UI/Apply Chinese Font and Layout")]
    public static void Build()
    {
        TMP_FontAsset font = BuildFont();
        var settings = new SerializedObject(TMP_Settings.instance);
        settings.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(TMP_Settings.instance);
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
                if (texts.Length == 0) continue;
                foreach (TMP_Text text in texts)
                {
                    text.font = font;
                    text.fontSharedMaterial = font.material;
                    // Frontend fonts used to carry Latin pixel-font spacing and outlines.
                    text.characterSpacing = 0;
                    text.wordSpacing = 0;
                    if (path.Contains("/Frontend/") || path == SettingsPanelBuilder.PrefabPath)
                    {
                        text.enableAutoSizing = true;
                        text.fontSizeMin = 16;
                        text.fontSizeMax = Mathf.Max(16, text.fontSize);
                    }
                }
                FarmHubPanel panel = root.GetComponent<FarmHubPanel>();
                if (panel != null) panel.ConfigurePresentation();
                foreach (TMP_InputField input in root.GetComponentsInChildren<TMP_InputField>(true))
                    input.fontAsset = font;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        ProjectHealthValidator.ValidateOrThrow();
        Debug.Log("中文字体、界面和背包布局已生成并通过工程检查。");
    }

    private static TMP_FontAsset BuildFont()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null && (font.atlasTextures.Length == 0 || font.atlasTextures[0] == null ||
            AssetDatabase.GetAssetPath(font.sourceFontFile) != "Assets/Game/Art/Fonts/ChineseText.otf"))
        {
            AssetDatabase.DeleteAsset(FontPath);
            font = null;
        }
        if (font == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FontPath));
            AssetDatabase.Refresh();
            Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/Art/Fonts/ChineseText.otf");
            if (source == null) throw new InvalidOperationException("项目中文正文字体 ChineseText.otf 缺失。");
            font = TMP_FontAsset.CreateFontAsset(source, 48, 6, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            font.name = "ChineseUI";
            font.atlasTexture.name = "ChineseUI Atlas";
            font.material.name = "ChineseUI Material";
            AssetDatabase.CreateAsset(font, FontPath);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (Texture2D atlas in font.atlasTextures)
                if (atlas != null && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
        }
        // Preload actual Chinese UI glyphs; dynamic atlases also support player-entered names.
        var characters = new StringBuilder();
        for (int c = 32; c < 127; c++) characters.Append((char)c);
        characters.Append("×–…·　：，。！？（）【】“”、；");
        foreach (string path in Directory.GetFiles("Assets/Game", "*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(path);
            if (extension != ".cs" && extension != ".prefab" && extension != ".asset") continue;
            foreach (Match match in Regex.Matches(File.ReadAllText(path), "[\\u4e00-\\u9fff]"))
                characters.Append(match.Value);
        }
        string requested = new string(characters.ToString().Distinct().ToArray());
        // TMP 3.0.7 uses this cached property when naming a second persistent atlas.
        Texture2D primaryAtlas = font.atlasTexture;
        font.TryAddCharacters(requested, out string missing);
        TMP_FontAsset latin = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (font.fallbackFontAssetTable == null) font.fallbackFontAssetTable = new List<TMP_FontAsset>();
        if (latin != null && !font.fallbackFontAssetTable.Contains(latin)) font.fallbackFontAssetTable.Add(latin);
        var face = font.faceInfo;
        face.lineHeight = face.pointSize * 1.25f;
        font.faceInfo = face;
        foreach (Texture2D atlas in font.atlasTextures)
            if (atlas != null && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
        EditorUtility.SetDirty(font);
        foreach (Texture2D atlas in font.atlasTextures) if (atlas != null) EditorUtility.SetDirty(atlas);
        Directory.CreateDirectory("Artifacts/ChineseUI");
        File.WriteAllText("Artifacts/ChineseUI/font-coverage.txt", "Requested glyphs: " + requested.Length +
            "\nMissing glyphs (Latin fallback handles symbols): " + missing);
        AssetDatabase.SaveAssets();
        return font;
    }
}
