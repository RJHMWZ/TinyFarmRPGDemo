using System;
using System.IO;
using UnityEngine;

/// <summary>Owns versioned, atomic save-file persistence. It has no scene or UI dependencies.</summary>
public sealed class SaveService
{
    [Serializable]
    private sealed class SaveEnvelope
    {
        public int saveVersion = CurrentSaveVersion;
        public GameSaveData data;
    }

    [Serializable]
    private sealed class LegacyEnvelope
    {
        public int saveVersion;
        public CharacterCreationProfile profile;
    }

    private const int CurrentSaveVersion = 2;
    private const string SaveFolderName = "Saves";
    private const string SaveFilePattern = "save-slot-{0}.json";
    private const string LegacyFilePattern = "character-profile-slot-{0}.json";
    private const string LegacySingleFile = "character-profile.json";
    public const int SlotCount = 4;

    private static string SaveDirectory => Path.Combine(Application.persistentDataPath, SaveFolderName);

    public bool Save(int slotIndex, GameSaveData data)
    {
        if (!IsValidSlot(slotIndex) || data == null) return false;
        data.Normalize();
        data.version = CurrentSaveVersion;
        data.metadata.playerName = data.player.appearance.playerName;
        data.metadata.lastSavedUtcTicks = DateTime.UtcNow.Ticks;

        string path = GetSavePath(slotIndex);
        string temporaryPath = path + ".tmp";
        string backupPath = path + ".bak";
        try
        {
            Directory.CreateDirectory(SaveDirectory);
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(new SaveEnvelope { data = data }, true));
            if (File.Exists(path))
            {
                File.Copy(path, backupPath, true);
                File.Delete(path);
            }
            File.Move(temporaryPath, path);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("Failed to save slot " + (slotIndex + 1) + ": " + exception.Message);
            TryDelete(temporaryPath);
            return false;
        }
    }

    public bool TryLoad(int slotIndex, out GameSaveData data)
    {
        data = null;
        if (!IsValidSlot(slotIndex)) return false;
        data = TryLoadCurrent(GetSavePath(slotIndex)) ?? TryLoadCurrent(GetSavePath(slotIndex) + ".bak");
        if (data == null) data = TryLoadLegacy(slotIndex);
        if (data == null) return false;
        data.Normalize();
        return true;
    }

    public bool TryReadMetadata(int slotIndex, out SaveMetadata metadata)
    {
        if (TryLoad(slotIndex, out GameSaveData data))
        {
            metadata = data.metadata;
            return true;
        }
        metadata = null;
        return false;
    }

    public bool HasSave(int slotIndex) => TryLoad(slotIndex, out _);

    public bool Delete(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return false;
        try
        {
            DeleteSaveFamily(GetSavePath(slotIndex));
            DeleteSaveFamily(GetLegacyPath(slotIndex));
            if (slotIndex == 0) DeleteSaveFamily(Path.Combine(SaveDirectory, LegacySingleFile));
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("Failed to delete slot " + (slotIndex + 1) + ": " + exception.Message);
            return false;
        }
    }

    private static GameSaveData TryLoadCurrent(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            SaveEnvelope envelope = JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path));
            if (envelope == null || envelope.data == null || envelope.saveVersion > CurrentSaveVersion) return null;
            return Migrate(envelope.data, envelope.saveVersion);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Failed to read save file " + path + ": " + exception.Message);
            return null;
        }
    }

    private static GameSaveData TryLoadLegacy(int slotIndex)
    {
        string legacyPath = GetLegacyPath(slotIndex);
        CharacterCreationProfile profile = ReadLegacyProfile(legacyPath) ?? ReadLegacyProfile(legacyPath + ".bak");
        if (profile == null && slotIndex == 0)
        {
            string singlePath = Path.Combine(SaveDirectory, LegacySingleFile);
            profile = ReadLegacyProfile(singlePath) ?? ReadLegacyProfile(singlePath + ".bak");
        }
        return profile == null ? null : GameSaveData.CreateNew(profile);
    }

    private static CharacterCreationProfile ReadLegacyProfile(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            LegacyEnvelope envelope = JsonUtility.FromJson<LegacyEnvelope>(File.ReadAllText(path));
            return envelope != null ? envelope.profile : null;
        }
        catch { return null; }
    }

    private static GameSaveData Migrate(GameSaveData data, int sourceVersion)
    {
        // Add sequential schema migrations here as saveVersion increases.
        data.version = Mathf.Max(data.version, sourceVersion);
        return data;
    }

    private static bool IsValidSlot(int slotIndex) => slotIndex >= 0 && slotIndex < SlotCount;
    private static string GetSavePath(int slotIndex) => Path.Combine(SaveDirectory, string.Format(SaveFilePattern, slotIndex + 1));
    private static string GetLegacyPath(int slotIndex) => Path.Combine(SaveDirectory, string.Format(LegacyFilePattern, slotIndex + 1));

    private static void DeleteSaveFamily(string path)
    {
        TryDelete(path);
        TryDelete(path + ".bak");
        TryDelete(path + ".tmp");
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
