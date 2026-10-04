using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>Owns versioned, atomic save-file persistence. It has no scene or UI dependencies.</summary>
public sealed class SaveService
{
    private enum LoadStatus
    {
        Missing,
        Loaded,
        Invalid,
        UnsupportedVersion
    }

    [Serializable]
    private sealed class SaveEnvelope
    {
        public int saveVersion = CurrentSaveVersion;
        public GameSaveData data;
    }

    [Serializable]
    private sealed class LegacyEnvelope
    {
        // Explicit defaults document the legacy JSON contract and keep standalone C# builds warning-free.
        public int saveVersion = 0;
        public CharacterCreationProfile profile = null;
    }

    private const int CurrentSaveVersion = 3;
    private const string SaveFolderName = "Saves";
    private const string SaveFilePattern = "save-slot-{0}.json";
    private const string LegacyFilePattern = "character-profile-slot-{0}.json";
    private const string LegacySingleFile = "character-profile.json";
    public const int SlotCount = 4;
    private readonly string saveDirectory;

    public SaveService() : this(Path.Combine(Application.persistentDataPath, SaveFolderName)) { }

    /// <summary>Allows tests and platform adapters to provide an isolated storage directory.</summary>
    public SaveService(string saveDirectory)
    {
        if (string.IsNullOrWhiteSpace(saveDirectory))
            throw new ArgumentException("Save directory cannot be empty.", nameof(saveDirectory));
        this.saveDirectory = Path.GetFullPath(saveDirectory);
    }

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
            Directory.CreateDirectory(saveDirectory);
            WriteDurableText(temporaryPath, JsonUtility.ToJson(new SaveEnvelope { data = data }, true));
            if (File.Exists(path))
            {
                ReplaceWithBackup(temporaryPath, path, backupPath);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
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
        string path = GetSavePath(slotIndex);
        LoadStatus currentStatus = TryLoadCurrent(path, out data);
        if (currentStatus == LoadStatus.UnsupportedVersion) return false;

        if (data == null)
        {
            LoadStatus backupStatus = TryLoadCurrent(path + ".bak", out data);
            if (backupStatus == LoadStatus.UnsupportedVersion) return false;
        }
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

    /// <summary>
    /// Returns true when any current, backup, temporary, or legacy file occupies the slot, even if
    /// this game version cannot read it. UI uses this to prevent accidental overwrite of newer or
    /// damaged saves that must not be presented as empty slots.
    /// </summary>
    public bool IsSlotOccupied(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return false;
        if (SaveFamilyExists(GetSavePath(slotIndex)) || SaveFamilyExists(GetLegacyPath(slotIndex))) return true;
        return slotIndex == 0 && SaveFamilyExists(Path.Combine(saveDirectory, LegacySingleFile));
    }

    public bool Delete(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return false;
        try
        {
            DeleteSaveFamily(GetSavePath(slotIndex));
            DeleteSaveFamily(GetLegacyPath(slotIndex));
            if (slotIndex == 0) DeleteSaveFamily(Path.Combine(saveDirectory, LegacySingleFile));
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("Failed to delete slot " + (slotIndex + 1) + ": " + exception.Message);
            return false;
        }
    }

    private static LoadStatus TryLoadCurrent(string path, out GameSaveData data)
    {
        data = null;
        if (!File.Exists(path)) return LoadStatus.Missing;
        try
        {
            SaveEnvelope envelope = JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path));
            if (envelope == null || envelope.data == null) return LoadStatus.Invalid;
            if (envelope.saveVersion > CurrentSaveVersion)
            {
                Debug.LogError("Save file was created by a newer game version and cannot be loaded: " + path);
                return LoadStatus.UnsupportedVersion;
            }

            data = Migrate(envelope.data, envelope.saveVersion);
            return LoadStatus.Loaded;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Failed to read save file " + path + ": " + exception.Message);
            return LoadStatus.Invalid;
        }
    }

    private GameSaveData TryLoadLegacy(int slotIndex)
    {
        string legacyPath = GetLegacyPath(slotIndex);
        CharacterCreationProfile profile = ReadLegacyProfile(legacyPath) ?? ReadLegacyProfile(legacyPath + ".bak");
        if (profile == null && slotIndex == 0)
        {
            string singlePath = Path.Combine(saveDirectory, LegacySingleFile);
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
        data.version = CurrentSaveVersion;
        return data;
    }

    private static void ReplaceWithBackup(string temporaryPath, string path, string backupPath)
    {
        try
        {
            // File.Replace performs the swap and backup as one filesystem operation where supported.
            File.Replace(temporaryPath, path, backupPath);
        }
        catch (PlatformNotSupportedException)
        {
            ReplaceWithPortableFallback(temporaryPath, path, backupPath);
        }
        catch (IOException)
        {
            // Some Unity target filesystems do not implement File.Replace. The backup-first fallback
            // still guarantees that at least one complete copy survives an interrupted write.
            ReplaceWithPortableFallback(temporaryPath, path, backupPath);
        }
    }

    private static void WriteDurableText(string path, string content)
    {
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.Write(content);
            writer.Flush();
            // Flush managed and operating-system buffers before the atomic swap.
            stream.Flush(true);
        }
    }

    private static void ReplaceWithPortableFallback(string temporaryPath, string path, string backupPath)
    {
        File.Copy(path, backupPath, true);
        File.Delete(path);
        File.Move(temporaryPath, path);
    }

    private static bool IsValidSlot(int slotIndex) => slotIndex >= 0 && slotIndex < SlotCount;
    private string GetSavePath(int slotIndex) => Path.Combine(saveDirectory, string.Format(SaveFilePattern, slotIndex + 1));
    private string GetLegacyPath(int slotIndex) => Path.Combine(saveDirectory, string.Format(LegacyFilePattern, slotIndex + 1));

    private static void DeleteSaveFamily(string path)
    {
        TryDelete(path);
        TryDelete(path + ".bak");
        TryDelete(path + ".tmp");
    }

    private static bool SaveFamilyExists(string path) =>
        File.Exists(path) || File.Exists(path + ".bak") || File.Exists(path + ".tmp");

    private static void TryDelete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
