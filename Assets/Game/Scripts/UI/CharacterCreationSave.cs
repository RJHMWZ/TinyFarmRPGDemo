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
    public const int SlotCount = 4;
    private const string LegacySaveFileName = "character-profile.json";
    private const string SaveFilePattern = "character-profile-slot-{0}.json";
    private static int activeSlot;

    private static string SaveDirectory => Path.Combine(Application.persistentDataPath, SaveFolderName);
    private static string LegacySavePath => Path.Combine(SaveDirectory, LegacySaveFileName);
    private static string SavePath => GetSavePath(activeSlot);
    private static string BackupPath => GetBackupPath(activeSlot);
    private static string TemporaryPath => GetTemporaryPath(activeSlot);

    public static int ActiveSlot => activeSlot;

    public static void SetActiveSlot(int slotIndex)
    {
        activeSlot = Mathf.Clamp(slotIndex, 0, SlotCount - 1);
    }

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
        return TryLoadProfile(activeSlot, out CharacterCreationProfile profile)
            ? profile
            : new CharacterCreationProfile();
    }

    public static bool TryLoadProfile(out CharacterCreationProfile profile)
    {
        return TryLoadProfile(activeSlot, out profile);
    }

    public static bool TryLoadProfile(int slotIndex, out CharacterCreationProfile profile)
    {
        if (!IsValidSlot(slotIndex))
        {
            profile = null;
            return false;
        }

        profile = TryLoad(GetSavePath(slotIndex)) ?? TryLoad(GetBackupPath(slotIndex));
        if (profile == null && slotIndex == 0)
            profile = TryLoad(LegacySavePath) ?? TryLoad(LegacySavePath + ".bak");
        return profile != null;
    }

    public static bool HasSave(int slotIndex)
    {
        return TryLoadProfile(slotIndex, out _);
    }

    public static bool DeleteSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return false;
        try
        {
            DeleteIfExists(GetSavePath(slotIndex));
            DeleteIfExists(GetBackupPath(slotIndex));
            DeleteIfExists(GetTemporaryPath(slotIndex));
            if (slotIndex == 0)
            {
                DeleteIfExists(LegacySavePath);
                DeleteIfExists(LegacySavePath + ".bak");
            }
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("Failed to delete save slot " + (slotIndex + 1) + ": " + exception.Message);
            return false;
        }
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

    private static bool IsValidSlot(int slotIndex) => slotIndex >= 0 && slotIndex < SlotCount;

    private static string GetSavePath(int slotIndex)
    {
        return Path.Combine(SaveDirectory, string.Format(SaveFilePattern, slotIndex + 1));
    }

    private static string GetBackupPath(int slotIndex) => GetSavePath(slotIndex) + ".bak";
    private static string GetTemporaryPath(int slotIndex) => GetSavePath(slotIndex) + ".tmp";

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
