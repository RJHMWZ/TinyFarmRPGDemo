using System;

public sealed class FarmEconomyService
{
    private readonly GameSaveData save;
    private readonly FarmContent content;
    private readonly InventoryService inventory;
    private readonly FarmProgressionService progression;
    public FarmEconomyService(GameSaveData save, FarmContent content, InventoryService inventory, FarmProgressionService progression)
    { this.save = save; this.content = content; this.inventory = inventory; this.progression = progression; }

    public FarmResult Buy(string id, int count)
    {
        FarmItem item = content.Item(id);
        if (item == null || item.buyPrice <= 0 || count <= 0) return FarmResult.Fail("This item is not for sale.");
        FarmCrop crop = content.Crop(id);
        if (crop != null && !crop.InSeason(save.world.season)) return FarmResult.Fail("These seeds are out of season.");
        long price = (long)item.buyPrice * count;
        if (price > save.player.money) return FarmResult.Fail("Not enough gold.");
        if (!inventory.Add(id, count)) return FarmResult.Fail("Backpack full. Make room first.");
        save.player.money -= (int)price;
        progression.Record("buy", count);
        return FarmResult.Ok("Bought " + count + " " + item.title + ".");
    }
    public FarmResult Ship(int slotIndex, int count)
    {
        if (slotIndex < 0 || slotIndex >= inventory.Data.slots.Length || count <= 0) return FarmResult.Fail("Select an item first.");
        var slot = inventory.Data.slots[slotIndex];
        FarmItem item = content.Item(slot.itemId);
        if (slot.IsEmpty || slot.count < count || item == null || item.sellPrice <= 0 || item.kind == FarmItemKind.Tool)
            return FarmResult.Fail("This item cannot be shipped.");
        long value = PendingIncome + (long)item.sellPrice * count + save.player.money;
        if (value > int.MaxValue) return FarmResult.Fail("Shipping value exceeds the gold limit.");
        if (!inventory.Remove(item.id, count)) return FarmResult.Fail("Item unavailable.");
        save.farm.shipments.Add(new FarmShipment { itemId = item.id, count = count, unitPrice = item.sellPrice });
        progression.Record("ship", count);
        return FarmResult.Ok("Shipped " + count + " " + item.title + ". Paid after sleep.");
    }
    public long PendingIncome
    {
        get { long total = 0; foreach (var line in save.farm.shipments) total += (long)line.count * Math.Max(0, line.unitPrice); return total; }
    }
    public int SettleShipping()
    {
        int income = (int)Math.Min(int.MaxValue - (long)save.player.money, PendingIncome);
        save.player.money += income;
        save.farm.lastShippingIncome = income;
        save.farm.totalEarnings = (int)Math.Min(int.MaxValue, (long)save.farm.totalEarnings + income);
        save.farm.shipments.Clear();
        progression.Record("earn", income);
        return income;
    }
    public FarmResult Craft(string id)
    {
        FarmRecipe recipe = content.Recipe(id);
        if (recipe == null) return FarmResult.Fail("Unknown recipe.");
        if (progression.Level < recipe.requiredLevel) return FarmResult.Fail("Requires farming level " + recipe.requiredLevel + ".");
        var draft = new InventoryService(inventory.Copy(), content);
        foreach (var part in recipe.ingredients)
            if (!draft.Remove(part.itemId, part.count)) return FarmResult.Fail("Missing " + (content.Item(part.itemId)?.title ?? part.itemId) + ".");
        if (!draft.Add(recipe.outputId, recipe.outputCount)) return FarmResult.Fail("Backpack full. Crafting cancelled.");
        inventory.Replace(draft.Data);
        progression.Record("craft");
        progression.AddExperience(10);
        return FarmResult.Ok("Crafted " + recipe.title + ".");
    }
    public FarmResult Eat(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= inventory.Data.slots.Length) return FarmResult.Fail("Select food first.");
        var slot = inventory.Data.slots[slotIndex];
        FarmItem item = content.Item(slot.itemId);
        if (slot.IsEmpty || item == null || item.energy <= 0) return FarmResult.Fail("This item cannot be eaten.");
        if (save.farm.energy >= 100) return FarmResult.Fail("Energy is already full.");
        inventory.Remove(item.id, 1);
        save.farm.energy = Math.Min(100, save.farm.energy + item.energy);
        return FarmResult.Ok("Ate " + item.title + ". Energy restored.");
    }
}
