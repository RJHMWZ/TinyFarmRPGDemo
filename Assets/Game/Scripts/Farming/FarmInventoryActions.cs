using System;
using System.Collections.Generic;

/// <summary>Session inventory commands shared by the UI and input layer.</summary>
public sealed class FarmInventoryActions
{
    private readonly InventoryService backpack;
    private readonly InventoryService storage;
    private readonly FarmContent content;
    public FarmInventoryActions(InventoryService backpack, InventoryService storage, FarmContent content)
    { this.backpack = backpack; this.storage = storage; this.content = content; }

    public FarmResult Move(int from, int to) => backpack.Move(from, to)
        ? FarmResult.Ok("Item placed.") : FarmResult.Fail("That item cannot be moved there.");

    public FarmResult Equip(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= backpack.Data.slots.Length || backpack.Data.slots[slotIndex].IsEmpty)
            return FarmResult.Fail("Select an item first.");
        if (slotIndex < 12) { backpack.Data.selectedHotbarSlot = slotIndex; return FarmResult.Ok("Hotbar slot selected."); }
        int hotbar = backpack.Data.selectedHotbarSlot;
        return backpack.Move(slotIndex, hotbar)
            ? FarmResult.Ok("Equipped to the active hotbar slot.") : FarmResult.Fail("Could not equip that item.");
    }
    public FarmResult OrganizeBackpack() => backpack.Organize(12)
        ? FarmResult.Ok("Backpack organized. Hotbar preserved.") : FarmResult.Fail("Could not organize backpack.");
    public FarmResult OrganizeStorage() => storage.Organize(0)
        ? FarmResult.Ok("Storage organized.") : FarmResult.Fail("Could not organize storage.");

    public FarmResult Transfer(bool withdrawing, int slotIndex, bool halfStack = false)
    {
        var source = withdrawing ? storage : backpack;
        var target = withdrawing ? backpack : storage;
        if (slotIndex < 0 || slotIndex >= source.Data.slots.Length || source.Data.slots[slotIndex].IsEmpty)
            return FarmResult.Fail("Select an item first.");
        int count = source.Data.slots[slotIndex].count;
        if (halfStack) count = count / 2 + count % 2;
        return source.TransferTo(target, slotIndex, count)
            ? FarmResult.Ok(halfStack ? "Half stack transferred." : "Stack transferred.")
            : FarmResult.Fail("Transfer failed. Check available space.");
    }
    public FarmResult QuickStack()
    {
        // Match only items already in storage. Tools stay in the backpack.
        var existing = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in storage.Data.slots) if (!slot.IsEmpty) existing.Add(slot.itemId);
        var source = new InventoryService(backpack.Copy(), content);
        var target = new InventoryService(storage.Copy(), content);
        long moved = 0;
        for (int i = 0; i < source.Data.slots.Length; i++)
        {
            var slot = source.Data.slots[i];
            if (slot.IsEmpty || !existing.Contains(slot.itemId) || content.Item(slot.itemId)?.kind == FarmItemKind.Tool) continue;
            int count = Math.Min(slot.count, target.CapacityFor(slot.itemId));
            if (count > 0 && source.TransferTo(target, i, count)) moved += count;
        }
        if (moved == 0) return FarmResult.Fail("No matching items fit in storage.");
        backpack.Replace(source.Data);
        storage.Replace(target.Data);
        return FarmResult.Ok("Stacked " + moved + " items into storage.");
    }
}
