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
        if (item == null || item.buyPrice <= 0 || count <= 0) return FarmResult.Fail("这个物品无法购买。");
        FarmCrop crop = content.Crop(id);
        if (crop != null && !crop.InSeason(save.world.season)) return FarmResult.Fail("这些种子不适合当前季节。");
        long price = (long)item.buyPrice * count;
        if (price > save.player.money) return FarmResult.Fail("金币不足。");
        if (!inventory.Add(id, count)) return FarmResult.Fail("背包已满，请先腾出空间。");
        save.player.money -= (int)price;
        progression.Record("buy", count);
        return FarmResult.Ok("已购买 " + count + " " + item.title + ".");
    }
    public FarmResult Ship(int slotIndex, int count)
    {
        if (slotIndex < 0 || slotIndex >= inventory.Data.slots.Length || count <= 0) return FarmResult.Fail("请先选择物品。");
        var slot = inventory.Data.slots[slotIndex];
        FarmItem item = content.Item(slot.itemId);
        if (slot.IsEmpty || slot.count < count || item == null || item.sellPrice <= 0 || item.kind == FarmItemKind.Tool)
            return FarmResult.Fail("这个物品无法出售。");
        long value = PendingIncome + (long)item.sellPrice * count + save.player.money;
        if (value > int.MaxValue) return FarmResult.Fail("出货金额超过金币上限。");
        if (!inventory.RemoveFromSlot(slotIndex, count)) return FarmResult.Fail("物品不足或已不可用。");
        save.farm.shipments.Add(new FarmShipment { itemId = item.id, count = count, unitPrice = item.sellPrice });
        save.farm.lastShipmentRecoverable = true;
        progression.Record("ship", count);
        return FarmResult.Ok("已出货 " + count + " " + item.title + "，睡觉后结算金币。");
    }
    public long PendingIncome
    {
        get { long total = 0; foreach (var line in save.farm.shipments) total += (long)line.count * Math.Max(0, line.unitPrice); return total; }
    }
    public FarmShipment RecoverableShipment => save.farm.lastShipmentRecoverable && save.farm.shipments.Count > 0
        ? save.farm.shipments[save.farm.shipments.Count - 1] : null;
    public FarmResult UndoLastShipment()
    {
        var shipment = RecoverableShipment;
        if (shipment == null) return FarmResult.Fail("没有可以取回的出货。");
        if (!inventory.Add(shipment.itemId, shipment.count)) return FarmResult.Fail("背包已满，腾出空间后才能取回出货。");
        save.farm.shipments.RemoveAt(save.farm.shipments.Count - 1);
        save.farm.lastShipmentRecoverable = false;
        progression.RevertRecord("ship", shipment.count);
        return FarmResult.Ok("已取回 " + shipment.count + " " + (content.Item(shipment.itemId)?.title ?? shipment.itemId) + ".");
    }
    public int SettleShipping()
    {
        int income = (int)Math.Min(int.MaxValue - (long)save.player.money, PendingIncome);
        save.player.money += income;
        save.farm.lastShippingIncome = income;
        save.farm.totalEarnings = (int)Math.Min(int.MaxValue, (long)save.farm.totalEarnings + income);
        save.farm.shipments.Clear();
        save.farm.lastShipmentRecoverable = false;
        progression.Record("earn", income);
        return income;
    }
    public FarmResult Craft(string id)
    {
        FarmRecipe recipe = content.Recipe(id);
        if (recipe == null) return FarmResult.Fail("找不到这个配方。");
        if (progression.Level < recipe.requiredLevel) return FarmResult.Fail("需要农场等级 " + recipe.requiredLevel + ".");
        var draft = new InventoryService(inventory.Copy(), content);
        foreach (var part in recipe.ingredients)
            if (!draft.Remove(part.itemId, part.count)) return FarmResult.Fail("缺少材料：" + (content.Item(part.itemId)?.title ?? part.itemId) + ".");
        if (!draft.Add(recipe.outputId, recipe.outputCount)) return FarmResult.Fail("背包已满，制作已取消。");
        inventory.Replace(draft.Data);
        progression.Record("craft");
        progression.AddExperience(10);
        return FarmResult.Ok("已制作：" + recipe.title + ".");
    }
    public FarmResult Eat(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= inventory.Data.slots.Length) return FarmResult.Fail("请先选择食物。");
        var slot = inventory.Data.slots[slotIndex];
        FarmItem item = content.Item(slot.itemId);
        if (slot.IsEmpty || item == null || item.energy <= 0) return FarmResult.Fail("这个物品不能食用。");
        if (save.farm.energy >= 100) return FarmResult.Fail("体力已经恢复满了。");
        if (!inventory.RemoveFromSlot(slotIndex, 1)) return FarmResult.Fail("物品不足或已不可用。");
        save.farm.energy = Math.Min(100, save.farm.energy + item.energy);
        return FarmResult.Ok("已食用 " + item.title + "，体力已恢复。");
    }
}
