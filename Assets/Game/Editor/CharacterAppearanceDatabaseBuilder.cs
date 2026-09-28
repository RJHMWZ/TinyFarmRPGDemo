using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 编辑器外观数据库生成器。
/// 将 Data 目录中的 PlayerPartAnimationSet 转换为运行时可加载的 Resources 数据库。
/// </summary>
[InitializeOnLoad]
public static class CharacterAppearanceDatabaseBuilder
{
    private const string DataRoot = "Assets/Game/Data";
    private const string DatabasePath = "Assets/Game/Resources/CharacterAppearanceDatabase.asset";

    static CharacterAppearanceDatabaseBuilder()
    {
        // 工程首次加入本系统但数据库尚不存在时，在脚本编译完成后自动创建。
        EditorApplication.delayCall += RebuildIfNeeded;
    }

    /// <summary>扫描全部外观资源，建立稳定、自然排序的运行时索引。</summary>
    [MenuItem("Tools/Character Creation/Rebuild Database")]
    public static void Rebuild()
    {
        EnsureFolder("Assets/Game/Resources");
        CharacterAppearanceDatabase database = AssetDatabase.LoadAssetAtPath<CharacterAppearanceDatabase>(DatabasePath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<CharacterAppearanceDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        // GUID 既用于加载资源，也作为玩家存档中的长期稳定标识。
        string[] guids = AssetDatabase.FindAssets("t:PlayerPartAnimationSet", new[] { DataRoot });
        var entries = new List<CharacterAppearanceDatabase.Entry>(guids.Length);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            if (!TryGetCategory(path, out PlayerAppearanceCategory category)) continue;
            PlayerPartAnimationSet set = AssetDatabase.LoadAssetAtPath<PlayerPartAnimationSet>(path);
            if (set == null) continue;
            entries.Add(new CharacterAppearanceDatabase.Entry(guid, BuildDisplayName(path), category, GetGender(path), set));
        }

        // 固定分类、性别和自然名称顺序，避免不同机器扫描顺序导致 UI 编号变化。
        entries.Sort((a, b) =>
        {
            int category = a.Category.CompareTo(b.Category);
            if (category != 0) return category;
            int gender = a.Gender.CompareTo(b.Gender);
            return gender != 0 ? gender : EditorUtility.NaturalCompare(a.DisplayName, b.DisplayName);
        });
        database.ReplaceEntries(entries);
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log("Character appearance database rebuilt: " + entries.Count + " items.", database);
    }

    /// <summary>仅在数据库缺失时补建，避免每次 Domain Reload 都产生无效导入。</summary>
    private static void RebuildIfNeeded()
    {
        if (AssetDatabase.LoadAssetAtPath<CharacterAppearanceDatabase>(DatabasePath) == null)
            Rebuild();
    }

    /// <summary>从 Data 下的第一级目录名解析 Skin、Clothes 等分类。</summary>
    private static bool TryGetCategory(string path, out PlayerAppearanceCategory category)
    {
        string relative = path.Substring(DataRoot.Length).TrimStart('/');
        string folder = relative.Split('/')[0];
        return System.Enum.TryParse(folder, true, out category);
    }

    /// <summary>路径含 Male/Female 文件夹时设为专属，否则视为通用资源。</summary>
    private static AppearanceGender GetGender(string path)
    {
        string normalized = "/" + path.ToLowerInvariant() + "/";
        if (normalized.Contains("/male/")) return AppearanceGender.Male;
        if (normalized.Contains("/female/")) return AppearanceGender.Female;
        return AppearanceGender.Any;
    }

    /// <summary>把相对路径转为可调试、可展示的资源名称。</summary>
    private static string BuildDisplayName(string path)
    {
        string relative = path.Substring(DataRoot.Length).TrimStart('/');
        return Path.ChangeExtension(relative, null).Replace('/', ' ');
    }

    /// <summary>逐级创建目录，兼容 Resources 首次不存在的工程。</summary>
    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}

/// <summary>每次正式构建前强制刷新，保证安装包不会漏掉刚加入的资源。</summary>
public sealed class CharacterAppearanceBuildProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;
    public void OnPreprocessBuild(BuildReport report) => CharacterAppearanceDatabaseBuilder.Rebuild();
}

/// <summary>监听 Data 目录的导入、删除和移动，并延迟重建数据库。</summary>
public sealed class CharacterAppearanceDataPostprocessor : AssetPostprocessor
{
    private static bool rebuilding;

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (rebuilding || !TouchesData(imported) && !TouchesData(deleted) && !TouchesData(moved) && !TouchesData(movedFrom)) return;
        // delayCall 避免在 AssetPostprocessor 回调内部再次刷新 AssetDatabase。
        rebuilding = true;
        EditorApplication.delayCall += () =>
        {
            try { CharacterAppearanceDatabaseBuilder.Rebuild(); }
            finally { rebuilding = false; }
        };
    }

    private static bool TouchesData(string[] paths)
    {
        for (int i = 0; i < paths.Length; i++)
            if (paths[i].Replace('\\', '/').StartsWith("Assets/Game/Data/")) return true;
        return false;
    }
}
