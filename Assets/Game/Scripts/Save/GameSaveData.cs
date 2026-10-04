using System;

[Serializable]
public sealed class GameSaveData
{
    public int version = 3;
    public SaveMetadata metadata = new SaveMetadata();
    public PlayerSaveData player = new PlayerSaveData();
    public WorldSaveData world = new WorldSaveData();
    public QuestSaveData quests = new QuestSaveData();
    public RelationshipSaveData relationships = new RelationshipSaveData();
    public UnlockSaveData unlocks = new UnlockSaveData();
    public InventorySaveData inventory = InventorySaveData.CreateStarterInventory();

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
        if (inventory == null) inventory = InventorySaveData.CreateStarterInventory();
        inventory.Normalize();
        if (quests.activeQuestIds == null) quests.activeQuestIds = Array.Empty<string>();
        if (quests.completedQuestIds == null) quests.completedQuestIds = Array.Empty<string>();
        if (relationships.npcIds == null) relationships.npcIds = Array.Empty<string>();
        if (relationships.friendshipValues == null) relationships.friendshipValues = Array.Empty<int>();
        if (relationships.friendshipValues.Length != relationships.npcIds.Length)
            Array.Resize(ref relationships.friendshipValues, relationships.npcIds.Length);
        if (unlocks.unlockedIds == null) unlocks.unlockedIds = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(world.currentScene)) world.currentScene = GameSceneNames.Gameplay;
        world.year = Math.Max(1, world.year);
        world.day = Math.Max(1, world.day);
        world.hour = Math.Max(0, Math.Min(23, world.hour));
        world.minute = Math.Max(0, Math.Min(59, world.minute));
        if (string.IsNullOrWhiteSpace(metadata.playerName)) metadata.playerName = player.appearance.playerName;
    }
}

[Serializable]
public sealed class InventorySaveData
{
    public const int SlotCount = 24;
    public InventorySlotSaveData[] slots = new InventorySlotSaveData[SlotCount];
    public int selectedHotbarSlot;

    public static InventorySaveData CreateStarterInventory()
    {
        var inventory = new InventorySaveData();
        inventory.Normalize();
        inventory.slots[0].Set("hoe", "Hoe", 1);
        inventory.slots[1].Set("watering-can", "Watering Can", 1);
        inventory.slots[2].Set("axe", "Axe", 1);
        inventory.slots[3].Set("pickaxe", "Pickaxe", 1);
        inventory.slots[4].Set("parsnip-seed", "Parsnip Seeds", 15);
        return inventory;
    }

    public void Normalize()
    {
        if (slots == null) slots = new InventorySlotSaveData[SlotCount];
        if (slots.Length != SlotCount) Array.Resize(ref slots, SlotCount);
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == null) slots[i] = new InventorySlotSaveData();
        selectedHotbarSlot = Math.Max(0, Math.Min(11, selectedHotbarSlot));
    }
}

[Serializable]
public sealed class InventorySlotSaveData
{
    public string itemId;
    public string displayName;
    public int count;

    public bool IsEmpty => string.IsNullOrWhiteSpace(itemId) || count <= 0;

    public void Set(string id, string name, int amount)
    {
        itemId = id;
        displayName = name;
        count = Math.Max(0, amount);
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
