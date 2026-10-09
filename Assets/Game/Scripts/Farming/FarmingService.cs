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
            return FarmResult.Fail("Invalid plot or slot.");
        var plot = save.farm.plots[index];
        if (IsReady(plot)) return Harvest(plot);
        var slot = inventory.Data.slots[slotIndex];
        if (slot.IsEmpty) return FarmResult.Fail("Select a tool or seeds in your hotbar.");
        if (save.farm.energy < 2) return FarmResult.Fail("Too tired. Eat food or sleep at home.");
        if (slot.itemId == "hoe")
        {
            if (plot.tilled) return FarmResult.Fail("Already tilled. Select seeds to plant.");
            plot.tilled = true; plot.watered = save.world.weatherId == "rainy"; progression.Record("till");
        }
        else if (slot.itemId == "watering-can")
        {
            if (!plot.tilled) return FarmResult.Fail("Till the soil first.");
            if (plot.watered) return FarmResult.Fail("Already watered today.");
            plot.watered = true; progression.Record("water");
        }
        else if (slot.itemId == "fertilizer")
        {
            if (!plot.tilled || !string.IsNullOrEmpty(plot.seedId) || plot.fertilized) return FarmResult.Fail("Fertilize an empty tilled plot before planting.");
            inventory.Remove(slot.itemId, 1); plot.fertilized = true;
        }
        else if (slot.itemId == "sprinkler")
        {
            if (!plot.tilled || plot.sprinkler) return FarmResult.Fail("Place one sprinkler on a tilled plot.");
            inventory.Remove(slot.itemId, 1); plot.sprinkler = true; plot.watered = true;
        }
        else if (slot.itemId == "pickaxe")
        {
            if (!plot.tilled || !string.IsNullOrEmpty(plot.seedId) || plot.sprinkler)
                return FarmResult.Fail("Only empty plots without sprinklers can be cleared.");
            plot.tilled = false; plot.watered = false; plot.fertilized = false;
        }
        else
        {
            FarmCrop crop = content.Crop(slot.itemId);
            if (crop == null) return FarmResult.Fail("Use a hoe, seeds, watering can, fertilizer or sprinkler.");
            if (!plot.tilled) return FarmResult.Fail("Use the hoe to till this plot first.");
            if (!string.IsNullOrEmpty(plot.seedId)) return FarmResult.Fail("A crop is already growing here.");
            if (!crop.InSeason(save.world.season)) return FarmResult.Fail("This crop cannot grow in the current season.");
            string seedId = slot.itemId;
            inventory.Remove(seedId, 1); plot.seedId = seedId; plot.growth = 0;
            progression.Record("plant");
        }
        save.farm.energy -= 2;
        return FarmResult.Ok("Farm updated.");
    }
    private FarmResult Harvest(FarmPlotData plot)
    {
        FarmCrop crop = content.Crop(plot.seedId);
        int count = crop.harvestCount + (plot.fertilized ? 1 : 0);
        if (!inventory.Add(crop.harvestId, count)) return FarmResult.Fail("Backpack full. Your crop is safe in the field.");
        progression.Record("harvest", count);
        progression.AddExperience(20);
        if (crop.regrowDays > 0) plot.growth = Math.Max(0, crop.growthDays - crop.regrowDays);
        else { plot.seedId = null; plot.growth = 0; plot.fertilized = false; }
        return FarmResult.Ok("Harvested " + count + " " + content.Item(crop.harvestId).title + "!");
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
        if (string.IsNullOrWhiteSpace(nodeId) || !NodeAvailable(nodeId)) return FarmResult.Fail("This resource is recovering. Check again in a few days.");
        if (slotIndex < 0 || slotIndex >= inventory.Data.slots.Length) return FarmResult.Fail("Select a tool.");
        string required = kind == "wood" ? "axe" : kind == "stone" ? "pickaxe" : null;
        if (kind != "wood" && kind != "stone" && kind != "berry") return FarmResult.Fail("Unknown resource.");
        if (required != null && inventory.Data.slots[slotIndex].itemId != required) return FarmResult.Fail("Select your " + required + ".");
        if (save.farm.energy < 5) return FarmResult.Fail("Too tired. Eat food or sleep.");
        if (!inventory.Add(kind, kind == "berry" ? 2 : 5)) return FarmResult.Fail("Backpack full.");
        save.farm.energy -= 5;
        var state = save.farm.resources.Find(x => x.id == nodeId);
        if (state == null) { state = new FarmNodeState { id = nodeId }; save.farm.resources.Add(state); }
        state.availableDay = save.farm.daysPlayed + 2;
        progression.Record("gather"); progression.AddExperience(10);
        return FarmResult.Ok("Collected " + kind + ". Regrows in two days.");
    }
}
