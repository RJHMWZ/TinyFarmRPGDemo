using System;

public sealed class FarmProgressionService
{
    private readonly GameSaveData save;
    private readonly FarmContent content;
    public int Level => Math.Min(10, 1 + save.farm.experience / 100);
    public FarmProgressionService(GameSaveData save, FarmContent content) { this.save = save; this.content = content; }
    public int Counter(string id) => save.farm.counters.Find(x => x.id == id)?.value ?? 0;
    public void Record(string id, int amount = 1)
    {
        if (amount <= 0) return;
        var counter = save.farm.counters.Find(x => x.id == id);
        if (counter == null) { counter = new FarmCounter { id = id }; save.farm.counters.Add(counter); }
        counter.value = (int)Math.Min(int.MaxValue, (long)counter.value + amount);
    }
    public void AddExperience(int amount) => save.farm.experience = Math.Min(1000000, save.farm.experience + Math.Max(0, amount));
    public bool Completed(string id) => Array.IndexOf(save.quests.completedQuestIds, id) >= 0;
    public bool Available(FarmQuest quest) => quest != null && !Completed(quest.id) &&
        (string.IsNullOrEmpty(quest.prerequisite) || Completed(quest.prerequisite));
    public FarmResult Claim(string id)
    {
        FarmQuest quest = content.Quest(id);
        if (!Available(quest)) return FarmResult.Fail("This quest is locked or already claimed.");
        if (Counter(quest.counter) < quest.target) return FarmResult.Fail("Complete the objective first.");
        if ((long)save.player.money + quest.goldReward > int.MaxValue) return FarmResult.Fail("Gold limit reached.");
        int length = save.quests.completedQuestIds.Length;
        Array.Resize(ref save.quests.completedQuestIds, length + 1);
        save.quests.completedQuestIds[length] = id;
        save.player.money += quest.goldReward;
        AddExperience(quest.experienceReward);
        return FarmResult.Ok(quest.title + " complete! +" + quest.goldReward + "g");
    }
    public FarmSocialState Social(string id)
    {
        var state = save.farm.villagers.Find(x => x.id == id);
        if (state == null) { state = new FarmSocialState { id = id }; save.farm.villagers.Add(state); }
        return state;
    }
    public FarmResult Talk(string id)
    {
        FarmNpc npc = content.Npc(id);
        if (npc == null) return FarmResult.Fail("Villager unavailable.");
        var state = Social(id);
        bool first = state.lastTalkDay != save.farm.daysPlayed;
        if (first)
        {
            state.lastTalkDay = save.farm.daysPlayed;
            state.friendship = Math.Min(1000, state.friendship + 20);
            Record("talk");
        }
        string line = npc.dialogue.Length > 0 ? npc.dialogue[save.farm.daysPlayed % npc.dialogue.Length] : "Welcome to the valley.";
        return FarmResult.Ok(npc.title + ": " + line + (first ? "\nFriendship +20" : "\nCome back tomorrow for more friendship."));
    }
    public FarmResult Gift(string id, int slotIndex, InventoryService inventory)
    {
        FarmNpc npc = content.Npc(id);
        if (npc == null || slotIndex < 0 || slotIndex >= inventory.Data.slots.Length) return FarmResult.Fail("Select an item first.");
        var state = Social(id);
        if (state.lastGiftDay == save.farm.daysPlayed) return FarmResult.Fail("One gift per villager each day.");
        var slot = inventory.Data.slots[slotIndex];
        FarmItem item = content.Item(slot.itemId);
        if (slot.IsEmpty || item == null || item.kind == FarmItemKind.Tool) return FarmResult.Fail("Choose a crop, food or material to give.");
        bool loved = item.id == npc.favoriteItem;
        if (!inventory.Remove(item.id, 1)) return FarmResult.Fail("Item unavailable.");
        state.lastGiftDay = save.farm.daysPlayed;
        state.friendship = Math.Min(1000, state.friendship + (loved ? 80 : 30));
        Record("gift");
        return FarmResult.Ok(loved ? npc.title + " loves this gift! +80 friendship" : npc.title + " appreciates your gift. +30 friendship");
    }
}
