using System;
using System.Collections.Generic;
using UnityEngine;

public enum FarmItemKind { Material, Tool, Seed, Crop, Food, Furniture }

[Serializable]
public sealed class FarmItem
{
    public string id;
    public string title;
    [TextArea] public string description;
    public FarmItemKind kind;
    [Min(1)] public int maxStack = 99;
    [Min(0)] public int buyPrice;
    [Min(0)] public int sellPrice;
    [Min(0)] public int energy;
    public Sprite icon;
}

[Serializable]
public sealed class FarmCrop
{
    public string seedId;
    public string harvestId;
    [Min(1)] public int growthDays = 4;
    [Min(1)] public int harvestCount = 1;
    [Tooltip("0 means any season; otherwise a bit mask: Spring=1, Summer=2, Fall=4, Winter=8.")]
    public int seasonMask;
    [Min(0)] public int regrowDays;
    public Sprite[] stages = Array.Empty<Sprite>();
    public bool InSeason(int season) => seasonMask == 0 || (seasonMask & (1 << season)) != 0;
}

[Serializable]
public sealed class FarmIngredient
{
    public string itemId;
    public int count = 1;
    public FarmIngredient() { }
    public FarmIngredient(string id, int amount) { itemId = id; count = amount; }
}

[Serializable]
public sealed class FarmRecipe
{
    public string id;
    public string title;
    public string outputId;
    public int outputCount = 1;
    public int requiredLevel = 1;
    public FarmIngredient[] ingredients = Array.Empty<FarmIngredient>();
}

[Serializable]
public sealed class FarmQuest
{
    public string id;
    public string title;
    [TextArea] public string description;
    public string prerequisite;
    public string counter;
    public int target = 1;
    public int goldReward;
    public int experienceReward;
}

[Serializable]
public sealed class FarmNpc
{
    public string id;
    public string title;
    public string favoriteItem;
    [TextArea] public string[] dialogue = Array.Empty<string>();
    public Sprite portrait;
    public Sprite[] layers = Array.Empty<Sprite>();
}

/// <summary>Authored definitions only. Stable IDs are the save contract; sprites can be replaced freely.</summary>
[CreateAssetMenu(menuName = "Tiny Farm/Data/Farm Content")]
public sealed class FarmContent : ScriptableObject
{
    public FarmItem[] items = Array.Empty<FarmItem>();
    public FarmCrop[] crops = Array.Empty<FarmCrop>();
    public FarmRecipe[] recipes = Array.Empty<FarmRecipe>();
    public FarmQuest[] quests = Array.Empty<FarmQuest>();
    public FarmNpc[] villagers = Array.Empty<FarmNpc>();
    [Min(0.1f)] public float secondsPerTenMinutes = 7f;
    [Min(1)] public int dayEndHour = 22;
    public Sprite grass;
    public Sprite soil;
    public Sprite tree;
    public Sprite rock;
    public Sprite workbench;
    public Sprite chest;
    public Sprite bed;
    private Dictionary<string, FarmItem> byId;

    public FarmItem Item(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (byId == null) RebuildIndex();
        byId.TryGetValue(id, out FarmItem value);
        return value;
    }
    public FarmCrop Crop(string seedId) => Array.Find(crops, x => x != null && x.seedId == seedId);
    public FarmRecipe Recipe(string id) => Array.Find(recipes, x => x != null && x.id == id);
    public FarmQuest Quest(string id) => Array.Find(quests, x => x != null && x.id == id);
    public FarmNpc Npc(string id) => Array.Find(villagers, x => x != null && x.id == id);
    public void RebuildIndex()
    {
        byId = new Dictionary<string, FarmItem>(StringComparer.Ordinal);
        foreach (FarmItem item in items)
            if (item != null && !string.IsNullOrEmpty(item.id) && !byId.ContainsKey(item.id)) byId.Add(item.id, item);
    }
    private void OnEnable() => RebuildIndex();
    private void OnValidate() => RebuildIndex();

    public IEnumerable<string> ValidateContent()
    {
        var ids = new HashSet<string>();
        foreach (FarmItem item in items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id) || !ids.Add(item.id))
            { yield return "Missing or duplicate item ID."; continue; }
            if (string.IsNullOrWhiteSpace(item.title) || item.maxStack < 1 || item.buyPrice < 0 || item.sellPrice < 0)
                yield return "Invalid item: " + item.id;
            if (item.buyPrice > 0 && item.sellPrice > item.buyPrice) yield return "Buy/sell arbitrage: " + item.id;
        }
        ids.Clear();
        foreach (FarmCrop crop in crops)
            if (crop == null || !ids.Add(crop.seedId) || Item(crop.seedId)?.kind != FarmItemKind.Seed ||
                Item(crop.harvestId) == null || crop.growthDays < 1 || crop.harvestCount < 1 || crop.regrowDays < 0)
                yield return "Invalid crop definition.";
        ids.Clear();
        foreach (FarmRecipe recipe in recipes)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.id) || !ids.Add(recipe.id) || Item(recipe.outputId) == null || recipe.outputCount < 1)
            { yield return "Invalid recipe."; continue; }
            if (recipe.ingredients == null || recipe.ingredients.Length == 0) { yield return "Recipe without ingredients: " + recipe.id; continue; }
            var ingredients = new HashSet<string>();
            foreach (FarmIngredient part in recipe.ingredients)
                if (part == null || Item(part.itemId) == null || part.count < 1 || !ingredients.Add(part.itemId))
                    yield return "Invalid/duplicate ingredient: " + recipe.id;
        }
        ids.Clear();
        foreach (FarmQuest quest in quests)
        {
            if (quest == null || string.IsNullOrEmpty(quest.id) || !ids.Add(quest.id) || quest.target < 1 || quest.goldReward < 0 || quest.experienceReward < 0 || string.IsNullOrEmpty(quest.counter))
            { yield return "Invalid quest."; continue; }
            var visited = new HashSet<string> { quest.id };
            FarmQuest current = quest;
            while (!string.IsNullOrEmpty(current.prerequisite))
            {
                current = Quest(current.prerequisite);
                if (current == null || !visited.Add(current.id)) { yield return "Missing/cyclic prerequisite: " + quest.id; break; }
            }
        }
        ids.Clear();
        foreach (FarmNpc npc in villagers)
            if (npc == null || string.IsNullOrEmpty(npc.id) || !ids.Add(npc.id) || Item(npc.favoriteItem) == null || npc.dialogue == null || npc.dialogue.Length == 0)
                yield return "Invalid villager.";
        if (secondsPerTenMinutes <= 0 || dayEndHour <= 6 || dayEndHour > 23) yield return "Invalid clock settings.";
    }
}
