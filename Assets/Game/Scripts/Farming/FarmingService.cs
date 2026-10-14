using System;

public sealed class FarmingService
{
    private readonly GameSaveData save;
    private readonly FarmContent content;
    private readonly InventoryService inventory;
    private readonly FarmProgressionService progression;
    public FarmingService(GameSaveData save, FarmContent content, InventoryService inventory, FarmProgressionService progression)
    { this.save = save; this.content = content; this.inventory = inventory; this.progression = progression; }
    public bool IsReady(FarmPlotData plot)
    {
        FarmCrop crop = content.Crop(plot.seedId);
        return crop != null && plot.growth >= crop.growthDays;
    }
    public FarmResult UsePlot(int index, int slotIndex)
    {
        if (index < 0 || index >= save.farm.plots.Length || slotIndex < 0 || slotIndex >= inventory.Data.slots.Length)
            return FarmResult.Fail("无法操作这块土地或物品格。");
        var plot = save.farm.plots[index];
        if (IsReady(plot)) return Harvest(plot);
        var slot = inventory.Data.slots[slotIndex];
        if (slot.IsEmpty) return FarmResult.Fail("请在快捷栏选择工具或种子。");
        if (save.farm.energy < 2) return FarmResult.Fail("体力不足，吃点东西或回家睡觉吧。");
        if (slot.itemId == "hoe")
        {
            if (plot.tilled) return FarmResult.Fail("土地已经耕好了，选择种子即可播种。");
            plot.tilled = true; plot.watered = save.world.weatherId == "rainy"; progression.Record("till");
        }
        else if (slot.itemId == "watering-can")
        {
            if (!plot.tilled) return FarmResult.Fail("请先用锄头耕地。");
            if (plot.watered) return FarmResult.Fail("今天已经浇过水了。");
            plot.watered = true; progression.Record("water");
        }
        else if (slot.itemId == "fertilizer")
        {
            if (!plot.tilled || !string.IsNullOrEmpty(plot.seedId) || plot.fertilized) return FarmResult.Fail("请在播种前给空耕地施肥。");
            if (!inventory.RemoveFromSlot(slotIndex, 1)) return FarmResult.Fail("物品不足或已不可用。");
            plot.fertilized = true;
        }
        else if (slot.itemId == "sprinkler")
        {
            if (!plot.tilled || plot.sprinkler) return FarmResult.Fail("只能在耕地上放置一个洒水器。");
            if (!inventory.RemoveFromSlot(slotIndex, 1)) return FarmResult.Fail("物品不足或已不可用。");
            plot.sprinkler = true; plot.watered = true;
        }
        else if (slot.itemId == "pickaxe")
        {
            if (!plot.tilled || !string.IsNullOrEmpty(plot.seedId) || plot.sprinkler)
                return FarmResult.Fail("只能清除没有作物和洒水器的空耕地。");
            plot.tilled = false; plot.watered = false; plot.fertilized = false;
        }
        else
        {
            FarmCrop crop = content.Crop(slot.itemId);
            if (crop == null) return FarmResult.Fail("请选择锄头、种子、水壶、肥料或洒水器。");
            if (!plot.tilled) return FarmResult.Fail("请先用锄头耕好这块土地。");
            if (!string.IsNullOrEmpty(plot.seedId)) return FarmResult.Fail("这里已经种了作物。");
            if (!crop.InSeason(save.world.season)) return FarmResult.Fail("这种作物不适合当前季节。");
            string seedId = slot.itemId;
            if (!inventory.RemoveFromSlot(slotIndex, 1)) return FarmResult.Fail("物品不足或已不可用。");
            plot.seedId = seedId; plot.growth = 0;
            progression.Record("plant");
        }
        save.farm.energy -= 2;
        return FarmResult.Ok("农田操作完成。");
    }
    private FarmResult Harvest(FarmPlotData plot)
    {
        FarmCrop crop = content.Crop(plot.seedId);
        int count = crop.harvestCount + (plot.fertilized ? 1 : 0);
        if (!inventory.Add(crop.harvestId, count)) return FarmResult.Fail("背包已满，作物仍保留在地里。");
        progression.Record("harvest", count);
        progression.AddExperience(20);
        if (crop.regrowDays > 0) plot.growth = Math.Max(0, crop.growthDays - crop.regrowDays);
        else { plot.seedId = null; plot.growth = 0; plot.fertilized = false; }
        return FarmResult.Ok("已收获 " + count + " " + content.Item(crop.harvestId).title + "!");
    }
    public void EndDay(int nextSeason, bool nextDayRain)
    {
        bool tutorialGrowth = save.farm.tutorialStep == (int)FirstDayTutorialStep.Sleep && !save.farm.tutorialFastGrowthUsed;
        int acceleratedCrops = 0;
        for (int i = 0; i < save.farm.plots.Length; i++)
        {
            var plot = save.farm.plots[i];
            FarmCrop crop = content.Crop(plot.seedId);
            if (crop != null)
            {
                if (!crop.InSeason(nextSeason)) { plot.seedId = null; plot.growth = 0; plot.fertilized = false; }
                else if (plot.watered)
                {
                    if (tutorialGrowth && acceleratedCrops < 3)
                    {
                        plot.growth = crop.growthDays;
                        acceleratedCrops++;
                    }
                    else plot.growth = Math.Min(crop.growthDays, plot.growth + 1);
                }
            }
            bool irrigated = plot.sprinkler;
            int x = i % FarmSaveData.Width, y = i / FarmSaveData.Width;
            if (x > 0) irrigated |= save.farm.plots[i - 1].sprinkler;
            if (x < FarmSaveData.Width - 1) irrigated |= save.farm.plots[i + 1].sprinkler;
            if (y > 0) irrigated |= save.farm.plots[i - FarmSaveData.Width].sprinkler;
            if (y < FarmSaveData.Height - 1) irrigated |= save.farm.plots[i + FarmSaveData.Width].sprinkler;
            plot.watered = plot.tilled && (nextDayRain || irrigated);
        }
        if (acceleratedCrops > 0) save.farm.tutorialFastGrowthUsed = true;
    }
    public bool NodeAvailable(string id) => (save.farm.resources.Find(x => x.id == id)?.availableDay ?? 0) <= save.farm.daysPlayed;
    public FarmResult Gather(string nodeId, string kind, int slotIndex)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || !NodeAvailable(nodeId)) return FarmResult.Fail("资源正在恢复，过几天再来吧。");
        if (slotIndex < 0 || slotIndex >= inventory.Data.slots.Length) return FarmResult.Fail("请先选择工具。");
        string required = kind == "wood" ? "axe" : kind == "stone" ? "pickaxe" : null;
        if (kind != "wood" && kind != "stone" && kind != "berry") return FarmResult.Fail("无法采集这个资源。");
        if (required != null && inventory.Data.slots[slotIndex].itemId != required) return FarmResult.Fail("请选择工具：" + content.Item(required)?.title + "。");
        if (save.farm.energy < 5) return FarmResult.Fail("体力不足，请吃东西或睡觉。");
        if (!inventory.Add(kind, kind == "berry" ? 2 : 5)) return FarmResult.Fail("背包已满。");
        save.farm.energy -= 5;
        var state = save.farm.resources.Find(x => x.id == nodeId);
        if (state == null) { state = new FarmNodeState { id = nodeId }; save.farm.resources.Add(state); }
        state.availableDay = save.farm.daysPlayed + 2;
        progression.Record("gather"); progression.AddExperience(10);
        return FarmResult.Ok("已采集 " + content.Item(kind)?.title + "，两天后恢复。");
    }
}
