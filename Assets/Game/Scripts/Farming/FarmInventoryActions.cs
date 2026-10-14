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
        ? FarmResult.Ok("物品已放下。") : FarmResult.Fail("无法把物品放到这里。");

    public FarmResult Equip(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= backpack.Data.slots.Length || backpack.Data.slots[slotIndex].IsEmpty)
            return FarmResult.Fail("请先选择物品。");
        if (slotIndex < 12) { backpack.Data.selectedHotbarSlot = slotIndex; return FarmResult.Ok("已选中快捷栏物品。"); }
        int hotbar = backpack.Data.selectedHotbarSlot;
        return backpack.Move(slotIndex, hotbar)
            ? FarmResult.Ok("已放入当前快捷栏格子。") : FarmResult.Fail("无法装备这个物品。");
    }
    public FarmResult OrganizeBackpack() => backpack.Organize(12)
        ? FarmResult.Ok("背包已整理，快捷栏顺序保持不变。") : FarmResult.Fail("背包整理失败。");
    public FarmResult OrganizeStorage() => storage.Organize(0)
        ? FarmResult.Ok("仓库已整理。") : FarmResult.Fail("仓库整理失败。");

    public FarmResult Transfer(bool withdrawing, int slotIndex, bool halfStack = false)
    {
        var source = withdrawing ? storage : backpack;
        var target = withdrawing ? backpack : storage;
        if (slotIndex < 0 || slotIndex >= source.Data.slots.Length || source.Data.slots[slotIndex].IsEmpty)
            return FarmResult.Fail("请先选择物品。");
        int count = source.Data.slots[slotIndex].count;
        if (halfStack) count = count / 2 + count % 2;
        return source.TransferTo(target, slotIndex, count)
            ? FarmResult.Ok(halfStack ? "已转移半组物品。" : "已转移整组物品。")
            : FarmResult.Fail("转移失败，请检查剩余空间。");
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
        if (moved == 0) return FarmResult.Fail("没有可以归堆的物品，或仓库空间不足。");
        backpack.Replace(source.Data);
        storage.Replace(target.Data);
        return FarmResult.Ok("已向仓库归堆 " + moved + " 个物品。");
    }
}
