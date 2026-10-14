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
    public void RevertRecord(string id, int amount)
    {
        var counter = save.farm.counters.Find(x => x.id == id);
        if (counter != null && amount > 0) counter.value = Math.Max(0, counter.value - amount);
    }
    public bool Completed(string id) => Array.IndexOf(save.quests.completedQuestIds, id) >= 0;
    public bool Available(FarmQuest quest) => quest != null && !Completed(quest.id) &&
        (string.IsNullOrEmpty(quest.prerequisite) || Completed(quest.prerequisite));
    public FarmResult Claim(string id)
    {
        FarmQuest quest = content.Quest(id);
        if (!Available(quest)) return FarmResult.Fail("任务尚未解锁或奖励已领取。");
        if (Counter(quest.counter) < quest.target) return FarmResult.Fail("请先完成任务目标。");
        if ((long)save.player.money + quest.goldReward > int.MaxValue) return FarmResult.Fail("金币已达到上限。");
        int length = save.quests.completedQuestIds.Length;
        Array.Resize(ref save.quests.completedQuestIds, length + 1);
        save.quests.completedQuestIds[length] = id;
        save.player.money += quest.goldReward;
        AddExperience(quest.experienceReward);
        return FarmResult.Ok(quest.title + "已完成！获得 " + quest.goldReward + " 金币");
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
        if (npc == null) return FarmResult.Fail("无法与这位村民互动。");
        var state = Social(id);
        bool first = state.lastTalkDay != save.farm.daysPlayed;
        if (first)
        {
            state.lastTalkDay = save.farm.daysPlayed;
            state.friendship = Math.Min(1000, state.friendship + 20);
            Record("talk");
        }
        string line = npc.dialogue.Length > 0 ? npc.dialogue[save.farm.daysPlayed % npc.dialogue.Length] : "欢迎来到这里。";
        return FarmResult.Ok(npc.title + ": " + line + (first ? "\n好感度 +20" : "\n明天再来聊天可以继续增加好感。"));
    }
    public FarmResult Gift(string id, int slotIndex, InventoryService inventory)
    {
        FarmNpc npc = content.Npc(id);
        if (npc == null || slotIndex < 0 || slotIndex >= inventory.Data.slots.Length) return FarmResult.Fail("请先选择物品。");
        var state = Social(id);
        if (state.lastGiftDay == save.farm.daysPlayed) return FarmResult.Fail("每位村民每天只能收一份礼物。");
        var slot = inventory.Data.slots[slotIndex];
        FarmItem item = content.Item(slot.itemId);
        if (slot.IsEmpty || item == null || item.kind == FarmItemKind.Tool) return FarmResult.Fail("请选择作物、食物或材料作为礼物。");
        bool loved = item.id == npc.favoriteItem;
        if (!inventory.RemoveFromSlot(slotIndex, 1)) return FarmResult.Fail("物品不足或已不可用。");
        state.lastGiftDay = save.farm.daysPlayed;
        state.friendship = Math.Min(1000, state.friendship + (loved ? 80 : 30));
        Record("gift");
        return FarmResult.Ok(loved ? npc.title + "很喜欢这份礼物！好感度 +80" : npc.title + "感谢你的礼物。好感度 +30");
    }
}
