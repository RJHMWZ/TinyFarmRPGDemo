using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 角色资料的磁盘持久化入口。使用版本化 JSON、临时文件和备份文件，
/// 避免把正式存档放进只适合设置项的 PlayerPrefs。
/// </summary>
public static class CharacterCreationSave
{
    [Serializable]
    private sealed class SaveEnvelope
    {
        public int saveVersion = CurrentSaveVersion;
        public CharacterCreationProfile profile;
    }

    private const int CurrentSaveVersion = 1;
    private const string SaveFolderName = "Saves";
    private const string SaveFileName = "character-profile.json";

    private static string SaveDirectory => Path.Combine(Application.persistentDataPath, SaveFolderName);
    private static string SavePath => Path.Combine(SaveDirectory, SaveFileName);
    private static string BackupPath => SavePath + ".bak";
    private static string TemporaryPath => SavePath + ".tmp";

    /// <summary>将完整角色资料序列化并立即写入磁盘。</summary>
    public static void Save(CharacterCreationProfile profile)
    {
        if (profile == null) return;

        try
        {
            Directory.CreateDirectory(SaveDirectory);
            string json = JsonUtility.ToJson(new SaveEnvelope { profile = profile }, true);
            File.WriteAllText(TemporaryPath, json);

            if (File.Exists(SavePath))
            {
                File.Copy(SavePath, BackupPath, true);
                File.Delete(SavePath);
            }
            File.Move(TemporaryPath, SavePath);
        }
        catch (Exception exception)
        {
            Debug.LogError("Failed to save character profile: " + exception.Message);
            TryDeleteTemporaryFile();
        }
    }

    /// <summary>读取角色资料；没有存档或内容损坏时返回安全的空资料。</summary>
    public static CharacterCreationProfile Load()
    {
        CharacterCreationProfile profile = TryLoad(SavePath);
        if (profile != null) return profile;

        profile = TryLoad(BackupPath);
        return profile ?? new CharacterCreationProfile();
    }

    private static CharacterCreationProfile TryLoad(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            SaveEnvelope envelope = JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path));
            if (envelope == null || envelope.profile == null || envelope.saveVersion > CurrentSaveVersion)
                return null;
            return Migrate(envelope.profile, envelope.saveVersion);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Failed to load character profile from " + path + ": " + exception.Message);
            return null;
        }
    }

    private static CharacterCreationProfile Migrate(CharacterCreationProfile profile, int sourceVersion)
    {
        // Add sequential migration steps here when the save schema changes.
        profile.version = Mathf.Max(profile.version, sourceVersion);
        return profile;
    }

    private static void TryDeleteTemporaryFile()
    {
        try
        {
            if (File.Exists(TemporaryPath)) File.Delete(TemporaryPath);
        }
        catch
        {
            // The original save remains intact; cleanup can be retried next time.
        }
    }
}
