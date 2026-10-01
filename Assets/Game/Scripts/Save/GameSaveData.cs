using System;

[Serializable]
public sealed class GameSaveData
{
    public int version = 1;
    public SaveMetadata metadata = new SaveMetadata();
    public PlayerSaveData player = new PlayerSaveData();
    public WorldSaveData world = new WorldSaveData();
    public QuestSaveData quests = new QuestSaveData();
    public RelationshipSaveData relationships = new RelationshipSaveData();
    public UnlockSaveData unlocks = new UnlockSaveData();

    public static GameSaveData CreateNew(CharacterCreationProfile appearance)
    {
        GameSaveData data = new GameSaveData();
        data.player.appearance = appearance ?? new CharacterCreationProfile();
        data.metadata.playerName = data.player.appearance.playerName;
        data.metadata.lastSavedUtcTicks = DateTime.UtcNow.Ticks;
        return data;
    }

    public void Normalize()
    {
        if (metadata == null) metadata = new SaveMetadata();
        if (player == null) player = new PlayerSaveData();
        if (player.appearance == null) player.appearance = new CharacterCreationProfile();
        if (world == null) world = new WorldSaveData();
        if (quests == null) quests = new QuestSaveData();
        if (relationships == null) relationships = new RelationshipSaveData();
        if (unlocks == null) unlocks = new UnlockSaveData();
        if (string.IsNullOrWhiteSpace(metadata.playerName)) metadata.playerName = player.appearance.playerName;
    }
}

[Serializable]
public sealed class SaveMetadata
{
    public string playerName;
    public string farmName;
    public long playTimeSeconds;
    public long lastSavedUtcTicks;
}

[Serializable]
public sealed class PlayerSaveData
{
    public CharacterCreationProfile appearance = new CharacterCreationProfile();
    public float positionX;
    public float positionY;
    public int money = 500;
}

[Serializable]
public sealed class WorldSaveData
{
    public string currentScene = GameSceneNames.Gameplay;
    public int year = 1;
    public int season;
    public int day = 1;
    public int hour = 6;
    public int minute;
    public string weatherId = "sunny";
}

[Serializable]
public sealed class QuestSaveData
{
    public string[] activeQuestIds = Array.Empty<string>();
    public string[] completedQuestIds = Array.Empty<string>();
}

[Serializable]
public sealed class RelationshipSaveData
{
    public string[] npcIds = Array.Empty<string>();
    public int[] friendshipValues = Array.Empty<int>();
}

[Serializable]
public sealed class UnlockSaveData
{
    public string[] unlockedIds = Array.Empty<string>();
}
