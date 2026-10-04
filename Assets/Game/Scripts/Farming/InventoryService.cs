using System;

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
        slot.count -= count;
        if (slot.count == 0) slot.Set(null, null, 0);
        return true;
    }
}
