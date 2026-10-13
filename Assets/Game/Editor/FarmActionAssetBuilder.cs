using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Maps the pack's named action folders to all existing outfit GUIDs without editing source textures.</summary>
[InitializeOnLoad]
public static class FarmActionAssetBuilder
{
    private const string Root = "Assets/ThirdParty/FarmRPGTinyAssetPack/Character/Character/PNG/";
    private const string Output = "Assets/Game/Resources/FarmActionAssets.asset";
    private static readonly PlayerAnimationType[] Actions = { PlayerAnimationType.Hoe, PlayerAnimationType.Axe, PlayerAnimationType.Water, PlayerAnimationType.Harvest };
    private static readonly string[] Folders = { "4. Pickaxe, Hoe and Catching insects", "5. Axe and Sickle", "7. Watering", "13.3 Carrying - Pick Up" };

    static FarmActionAssetBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            var assets = AssetDatabase.LoadAssetAtPath<FarmActionAssets>(Output);
            if (!EditorApplication.isPlayingOrWillChangePlaymode && (assets == null || assets.clips.Length < 6 ||
                assets.clips.Any(x => x.frames.Any(sprite => sprite != null && sprite.pivot / sprite.rect.size != Vector2.one * 0.5f)))) Build();
        };
    }

    [MenuItem("Tools/Tiny Farm/Build Tool Actions")]
    public static void Build()
    {
        var missing = new List<string>();
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:PlayerPartAnimationSet", new[] { "Assets/Game/Data/CharacterAppearance" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var set = AssetDatabase.LoadAssetAtPath<PlayerPartAnimationSet>(path);
            Sprite idle = set.IdleSprite;
            string source = AssetDatabase.GetAssetPath(idle).Replace('\\', '/');
            string prefix = Root + "1. Idle/";
            if (idle == null || !source.StartsWith(prefix, StringComparison.Ordinal)) { missing.Add(path + ": cannot map idle source"); continue; }
            string relative = source.Substring(prefix.Length);
            var clips = new PlayerPartAnimationSet.ActionFrames[Actions.Length];
            Vector2 pivot = idle.pivot / idle.rect.size;
            for (int i = 0; i < Actions.Length; i++)
            {
                string sheet = ResolveSheet(Root + Folders[i] + "/" + relative);
                Sprite[] frames = LoadFrames(sheet, path, Actions[i].ToString(), pivot, idle.pixelsPerUnit);
                if (frames.Length == 0) missing.Add(path + ": " + sheet);
                clips[i] = new PlayerPartAnimationSet.ActionFrames { action = Actions[i], frames = frames };
            }
            if (!Matches(set.Actions, clips))
            {
                set.SetActions(clips);
                EditorUtility.SetDirty(set);
                AssetDatabase.SaveAssetIfDirty(set);
            }
            count++;
        }

        var assets = AssetDatabase.LoadAssetAtPath<FarmActionAssets>(Output);
        if (assets == null) { assets = ScriptableObject.CreateInstance<FarmActionAssets>(); AssetDatabase.CreateAsset(assets, Output); }
        var tools = new List<FarmActionAssets.ToolClip>();
        AddTool(tools, assets, PlayerAnimationType.Hoe, Folders[0] + "/Weapons/Hoe/1.png");
        AddTool(tools, assets, PlayerAnimationType.Pickaxe, Folders[0] + "/Weapons/Pickaxe/1.png");
        AddTool(tools, assets, PlayerAnimationType.Axe, Folders[1] + "/Weapons/Axe/1.png");
        AddTool(tools, assets, PlayerAnimationType.Water, Folders[2] + "/Weapons/Watering/1.png");
        foreach (var action in new[] { PlayerAnimationType.Harvest, PlayerAnimationType.Plant })
            tools.Add(new FarmActionAssets.ToolClip { action = action, sound = assets.Find(action)?.sound });
        assets.clips = tools.ToArray();
        EditorUtility.SetDirty(assets);
        AssetDatabase.SaveAssetIfDirty(assets);
        Directory.CreateDirectory("Artifacts/ToolActions");
        File.WriteAllText("Artifacts/ToolActions/asset-report.txt", "Mapped outfits: " + count + "\nMissing layers: " + missing.Count + "\n" + string.Join("\n", missing));
        if (missing.Count > 0) Debug.LogWarning("Tool action layers missing:\n" + string.Join("\n", missing));
        else Debug.Log("Tool actions built for " + count + " outfits; no missing layers.");
    }

    private static bool Matches(PlayerPartAnimationSet.ActionFrames[] a, PlayerPartAnimationSet.ActionFrames[] b)
    {
        return a != null && a.Length == b.Length && a.Select((x, i) => x != null && x.action == b[i].action && x.frames.SequenceEqual(b[i].frames)).All(x => x);
    }

    private static string ResolveSheet(string path)
    {
        if (File.Exists(path)) return path;
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory)) return path;
        // The pack spells "pirate eye patch" as "Pirate eyepatch" in two action folders.
        string key = string.Concat(Path.GetFileNameWithoutExtension(path).Where(char.IsLetterOrDigit)).ToLowerInvariant();
        return Directory.GetFiles(directory, "*.png").FirstOrDefault(x =>
            string.Concat(Path.GetFileNameWithoutExtension(x).Where(char.IsLetterOrDigit)).ToLowerInvariant() == key)?.Replace('\\', '/') ?? path;
    }

    private static void AddTool(List<FarmActionAssets.ToolClip> result, FarmActionAssets assets, PlayerAnimationType action, string relative)
    {
        Sprite[] frames = LoadFrames(Root + relative, Output, action.ToString(), Vector2.one * 0.5f, 16);
        if (frames.Length == 0) throw new BuildFailedException("Missing animated tool: " + relative);
        result.Add(new FarmActionAssets.ToolClip { action = action, frames = frames, sound = assets.Find(action)?.sound });
    }

    private static Sprite[] LoadFrames(string source, string destination, string name, Vector2 pivot, float pixelsPerUnit)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(source);
        int expected = name == "Harvest" ? 16 : name == "Water" ? 32 : 24;
        if (texture == null || texture.width != expected * 32 || texture.height != 32) return Array.Empty<Sprite>();
        var imported = AssetDatabase.LoadAllAssetsAtPath(source).OfType<Sprite>().ToArray();
        var sprites = new Sprite[expected];
        var existing = AssetDatabase.LoadAllAssetsAtPath(destination).OfType<Sprite>().ToDictionary(x => x.name);
        for (int i = 0; i < sprites.Length; i++)
        {
            var rect = new Rect(i * 32, 0, 32, 32);
            var sprite = imported.FirstOrDefault(x => x.rect == rect);
            // Some tool sheets omit empty cells; include every cell to preserve body/tool synchronization.
            if (sprite != null && (sprite.pivot / sprite.rect.size - pivot).sqrMagnitude < 0.0001f && Mathf.Approximately(sprite.pixelsPerUnit, pixelsPerUnit))
            { sprites[i] = sprite; continue; }
            string id = "Action_" + name + "_" + i;
            existing.TryGetValue(id, out Sprite clone);
            if (clone != null && (clone.pivot / clone.rect.size - pivot).sqrMagnitude >= 0.0001f)
            {
                id += "_Anchor" + Mathf.RoundToInt(pivot.x * 100) + "_" + Mathf.RoundToInt(pivot.y * 100);
                existing.TryGetValue(id, out clone);
            }
            if (clone == null)
            {
                clone = Sprite.Create(texture, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                clone.name = id;
                AssetDatabase.AddObjectToAsset(clone, destination);
            }
            sprites[i] = clone;
        }
        return sprites;
    }

    [MenuItem("Tools/Tiny Farm/Validate Tool Actions")]
    public static void ValidateOrThrow()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:PlayerPartAnimationSet", new[] { "Assets/Game/Data/CharacterAppearance" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var set = AssetDatabase.LoadAssetAtPath<PlayerPartAnimationSet>(path);
            foreach (var action in Actions)
            {
                if (!set.HasAction(action) && path.Contains("/Accessory/")) continue;
                if (!set.HasAction(action) || set.GetFrameCount(action) != FarmActionAssets.FrameCount(action) || set.Actions.Any(x => x.frames.Any(sprite => sprite == null)))
                    throw new BuildFailedException("Missing action " + action + " on " + set.name);
            }
        }
        var assets = AssetDatabase.LoadAssetAtPath<FarmActionAssets>(Output);
        foreach (var action in new[] { PlayerAnimationType.Hoe, PlayerAnimationType.Pickaxe, PlayerAnimationType.Axe, PlayerAnimationType.Water })
            if (assets == null || assets.Find(action)?.frames.Length != FarmActionAssets.FrameCount(action) * 4 || assets.Find(action).frames.Any(x => x == null))
                throw new BuildFailedException("Missing tool frames: " + action);
    }
}

public sealed class FarmActionBuildProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder => -1100;
    public void OnPreprocessBuild(BuildReport report) { FarmActionAssetBuilder.Build(); FarmActionAssetBuilder.ValidateOrThrow(); }
}
