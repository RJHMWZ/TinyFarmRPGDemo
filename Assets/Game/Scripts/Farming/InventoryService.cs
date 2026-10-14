using System;
using System.Collections.Generic;

/// <summary>Inventory mutations use a private draft. Failed operations never consume partial stacks.</summary>
public sealed class InventoryService
{
    private readonly InventorySaveData data;
    private readonly FarmContent content;
    public InventorySaveData Data => data;
    public InventoryService(InventorySaveData data, FarmContent content) { this.data = data; this.content = content; data.Normalize(); }
    public int Count(string id)
    {
        long result = 0;
        foreach (var slot in data.slots) if (!slot.IsEmpty && slot.itemId == id) result += slot.count;
        return (int)Math.Min(int.MaxValue, result);
    }
    public InventorySaveData Copy()
    {
        var copy = new InventorySaveData { selectedHotbarSlot = data.selectedHotbarSlot };
        copy.Normalize();
        for (int i = 0; i < data.slots.Length; i++) copy.slots[i].Set(data.slots[i].itemId, data.slots[i].displayName, data.slots[i].count);
        return copy;
    }
    public void Replace(InventorySaveData draft)
    {
        for (int i = 0; i < data.slots.Length; i++) data.slots[i].Set(draft.slots[i].itemId, draft.slots[i].displayName, draft.slots[i].count);
    }
    public bool Add(string id, int count)
    {
        FarmItem item = content.Item(id);
        if (item == null || count <= 0) return false;
        InventorySaveData draft = Copy();
        int remaining = count;
        // Existing stacks first; then empty slots.
        for (int pass = 0; pass < 2; pass++)
            foreach (var slot in draft.slots)
            {
                bool eligible = pass == 0 ? !slot.IsEmpty && slot.itemId == id : slot.IsEmpty;
                if (!eligible) continue;
                int held = slot.IsEmpty ? 0 : slot.count;
                int amount = Math.Min(remaining, Math.Max(0, item.maxStack - held));
                if (amount > 0) slot.Set(id, item.title, held + amount);
                remaining -= amount;
                if (remaining == 0) { Replace(draft); return true; }
            }
        return false;
    }
    public bool Remove(string id, int count)
    {
        if (count <= 0 || Count(id) < count) return false;
        foreach (var slot in data.slots)
        {
            if (slot.IsEmpty || slot.itemId != id) continue;
            int amount = Math.Min(slot.count, count);
            slot.count -= amount;
            count -= amount;
            if (slot.count == 0) slot.Set(null, null, 0);
            if (count == 0) break;
        }
        return true;
    }
    public bool RemoveFromSlot(int slotIndex, int count)
    {
        if (slotIndex < 0 || slotIndex >= data.slots.Length || count <= 0) return false;
        var slot = data.slots[slotIndex];
        if (slot.IsEmpty || slot.count < count) return false;
        slot.count -= count;
        if (slot.count == 0) slot.Set(null, null, 0);
        return true;
    }
    public int CapacityFor(string id)
    {
        var item = content.Item(id);
        if (item == null) return 0;
        long capacity = 0;
        foreach (var slot in data.slots)
            if (slot.IsEmpty || slot.itemId == id)
                capacity += Math.Max(0, item.maxStack - (slot.IsEmpty ? 0 : slot.count));
        return (int)Math.Min(int.MaxValue, capacity);
    }
    /// <summary>Merge and sort only the requested range; the hotbar can stay untouched.</summary>
    public bool Organize(int firstSlot)
    {
        if (firstSlot < 0 || firstSlot >= data.slots.Length) return false;
        var draft = Copy();
        var stacks = new List<InventorySlotSaveData>();
        for (int i = firstSlot; i < draft.slots.Length; i++)
        {
            var source = draft.slots[i];
            if (source.IsEmpty) continue;
            int maxStack = content.Item(source.itemId)?.maxStack ?? 99;
            foreach (var target in stacks)
            {
                if (target.itemId != source.itemId) continue;
                int amount = Math.Min(source.count, Math.Max(0, maxStack - target.count));
                target.count += amount;
                source.count -= amount;
                if (source.count == 0) break;
            }
            if (source.count > 0) stacks.Add(source);
        }
        stacks.Sort((a, b) =>
        {
            int kind = (content.Item(a.itemId)?.kind ?? (FarmItemKind)int.MaxValue)
                .CompareTo(content.Item(b.itemId)?.kind ?? (FarmItemKind)int.MaxValue);
            if (kind != 0) return kind;
            int id = StringComparer.Ordinal.Compare(a.itemId, b.itemId);
            return id != 0 ? id : b.count.CompareTo(a.count);
        });
        // The list references the draft slots, so copy into fresh slots before clearing.
        var result = new InventorySaveData();
        result.Normalize();
        for (int i = 0; i < firstSlot; i++) result.slots[i].Set(data.slots[i].itemId, data.slots[i].displayName, data.slots[i].count);
        for (int i = 0; i < stacks.Count; i++) result.slots[firstSlot + i].Set(stacks[i].itemId, stacks[i].displayName, stacks[i].count);
        Replace(result);
        return true;
    }
    public bool Move(int from, int to)
    {
        if (from < 0 || to < 0 || from >= data.slots.Length || to >= data.slots.Length || from == to) return false;
        var source = data.slots[from];
        var target = data.slots[to];
        if (source.IsEmpty) return false;
        if (!target.IsEmpty && target.itemId == source.itemId)
        {
            int amount = Math.Min(source.count, Math.Max(0, (content.Item(source.itemId)?.maxStack ?? 99) - target.count));
            if (amount == 0) return false;
            target.count += amount; source.count -= amount;
            if (source.count == 0) source.Set(null, null, 0);
        }
        else { data.slots[from] = target; data.slots[to] = source; }
        return true;
    }
    public bool TransferTo(InventoryService target, int slotIndex, int count)
    {
        if (target == null || ReferenceEquals(data, target.data) || slotIndex < 0 || slotIndex >= data.slots.Length || count <= 0) return false;
        var slot = data.slots[slotIndex];
        if (slot.IsEmpty || slot.count < count || !target.Add(slot.itemId, count)) return false;
        RemoveFromSlot(slotIndex, count);
        return true;
    }
}
