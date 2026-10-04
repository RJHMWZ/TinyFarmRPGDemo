using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Creates missing assets without touching open scenes or overwriting authored balance changes.</summary>
public static class FarmContentBuilder
{
    public const string ContentPath = "Assets/Game/Resources/FarmContent.asset";
    public const string PanelPath = "Assets/Game/Prefabs/UI/FarmHubPanel.prefab";
    private const string Pack = "Assets/ThirdParty/FarmRPGTinyAssetPack/";
    [MenuItem("Tools/Tiny Farm/Build Farm Content")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<FarmContent>(ContentPath) == null) BuildContent();
        if (AssetDatabase.LoadAssetAtPath<FarmHubPanel>(PanelPath) == null) BuildPanel();
        RegisterPanel();
        AssetDatabase.SaveAssets();
        ValidateOrThrow();
        Debug.Log("Farm content and panel are ready. Existing authored assets were preserved.");
    }
    [MenuItem("Tools/Tiny Farm/Validate Farm Content")]
    public static void ValidateOrThrow()
    {
        var content = AssetDatabase.LoadAssetAtPath<FarmContent>(ContentPath);
        if (content == null) throw new InvalidOperationException("Farm content is missing.");
        content.RebuildIndex();
        var errors = content.ValidateContent().ToArray();
        if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors));
        var catalog = AssetDatabase.LoadAssetAtPath<UIPanelCatalog>("Assets/Game/Data/UI/UIPanelCatalog.asset");
        if (catalog.Find(FarmHubPanel.PanelId)?.prefab == null) throw new InvalidOperationException("Farm hub is not registered.");
    }
    private static void BuildContent()
    {
        var data = ScriptableObject.CreateInstance<FarmContent>();
        AssetDatabase.CreateAsset(data, ContentPath);
        var items = new List<FarmItem>();
        AddItem(items, "hoe", "Hoe", FarmItemKind.Tool, 0, 0, "Till empty farm plots.", Icon(data, "RPG icons/Weapons and Armor/1. Wood/Hoe.png"));
        AddItem(items, "watering-can", "Watering Can", FarmItemKind.Tool, 0, 0, "Water tilled soil. Crops grow only after watered days.", Icon(data, "RPG icons/Weapons and Armor/1. Wood/Watering can.png"));
        AddItem(items, "axe", "Axe", FarmItemKind.Tool, 0, 0, "Gather wood from trees.", Icon(data, "RPG icons/Weapons and Armor/1. Wood/Axe.png"));
        AddItem(items, "pickaxe", "Pickaxe", FarmItemKind.Tool, 0, 0, "Gather stone or clear empty tilled plots.", Icon(data, "RPG icons/Weapons and Armor/1. Wood/Pickaxe.png"));
        AddItem(items, "wood", "Wood", FarmItemKind.Material, 0, 3, "Construction and crafting material.", Icon(data, "RPG icons/Extras/Wood.png"));
        AddItem(items, "stone", "Stone", FarmItemKind.Material, 0, 2, "Useful for sprinklers and tools.", Icon(data, "RPG icons/Extras/Stones.png"));
        AddItem(items, "berry", "Wild Berries", FarmItemKind.Food, 25, 10, "Eat to restore 15 energy or give as a gift.", Icon(data, "Food Icons/Blackberry.png"), 15);
        AddItem(items, "salad", "Farm Salad", FarmItemKind.Food, 90, 35, "A fresh meal. Restores 45 energy.", Icon(data, "Food Icons/Salad.png"), 45);
        AddItem(items, "fertilizer", "Fertilizer", FarmItemKind.Material, 35, 10, "Apply before planting for one extra crop per harvest.", Icon(data, "RPG icons/Extras/Stones.png"));
        AddItem(items, "sprinkler", "Sprinkler", FarmItemKind.Furniture, 250, 80, "Waters its plot and four adjacent plots every morning.", Icon(data, "RPG icons/Weapons and Armor/3. Iron/Watering Can.png"));
        data.crops = new[]
        {
            Crop(data, items, "parsnip", "Parsnip", "Spring", 4, 20, 45, 1),
            Crop(data, items, "potato", "Potato", "Spring", 5, 35, 75, 1),
            Crop(data, items, "tomato", "Tomato", "Summer", 5, 40, 50, 2, 2),
            Crop(data, items, "pumpkin", "Pumpkin", "Fall", 7, 70, 150, 4),
            Crop(data, items, "carrot", "Carrot", "Spring", 4, 25, 45, 0)
        };
        data.items = items.ToArray();
        data.RebuildIndex();
        data.recipes = new[]
        {
            Recipe("fertilizer", "Fertilizer", "fertilizer", 2, 1, new FarmIngredient("wood", 3), new FarmIngredient("stone", 2)),
            Recipe("salad", "Farm Salad", "salad", 1, 1, new FarmIngredient("parsnip", 1), new FarmIngredient("berry", 2)),
            Recipe("sprinkler", "Sprinkler", "sprinkler", 1, 2, new FarmIngredient("wood", 10), new FarmIngredient("stone", 10)),
            Recipe("seed-pouch", "Carrot Seed Pouch", "carrot-seed", 3, 2, new FarmIngredient("carrot", 1), new FarmIngredient("wood", 2))
        };
        data.quests = new[]
        {
            Quest("first-soil", "A Fresh Start", "Till five plots with your hoe.", "", "till", 5, 80, 20),
            Quest("first-seeds", "Seeds of Tomorrow", "Plant five seeds.", "first-soil", "plant", 5, 100, 20),
            Quest("first-water", "A Little Care", "Water five plots.", "first-seeds", "water", 5, 60, 20),
            Quest("neighbors", "Meet the Neighbors", "Talk to villagers on three occasions.", "", "talk", 3, 90, 30),
            Quest("gatherer", "Gather the Basics", "Gather from five resource nodes.", "", "gather", 5, 100, 40),
            Quest("first-harvest", "First Harvest", "Harvest five crops.", "first-water", "harvest", 5, 150, 50),
            Quest("first-shipping", "From Farm to Market", "Ship five items using the shipping bin.", "first-harvest", "ship", 5, 120, 40),
            Quest("maker", "Made by Hand", "Craft three items.", "gatherer", "craft", 3, 150, 50),
            Quest("friend", "A Thoughtful Gift", "Give three gifts to your neighbors.", "neighbors", "gift", 3, 120, 40),
            Quest("prosperity", "Growing Together", "Earn 1000 gold from shipping.", "first-shipping", "earn", 1000, 300, 100)
        };
        data.villagers = new[]
        {
            new FarmNpc { id = "willow", title = "Willow", favoriteItem = "parsnip", dialogue = new[] { "Welcome! Start with a few watered plots. A small farm can do great things.", "Leave room in your backpack before harvesting. Your crops can wait safely.", "Rain is a farmer's best helper. Use the extra time to gather materials." } },
            new FarmNpc { id = "rowan", title = "Rowan", favoriteItem = "berry", dialogue = new[] { "Wood and stone regrow in two days around here. Take only what you need.", "A sprinkler waters its own plot and the four plots around it each morning.", "Gifts are lovely, but a friendly conversation matters just as much." } }
        };
        data.grass = Slice(data, "Tileset/Tileset Grass Spring.png", new Rect(144, 592, 16, 16), "FarmGrass");
        data.soil = Slice(data, "Tileset/Tilled Soil and wet soil.png", new Rect(144, 80, 16, 16), "FarmSoil");
        data.tree = Slice(data, "Objects/Tree/Common/No Shadow/Maple Tree.png", new Rect(0, 96, 32, 48), "FarmTree");
        data.rock = data.Item("stone").icon;
        data.workbench = First(data, "Objects/Work Benches/Workbench.png");
        data.chest = First(data, "Objects/Exterior/chest.png");
        data.bed = First(data, "Objects/Interior/Beds.png");
        var appearances = AssetDatabase.LoadAssetAtPath<GameDataCatalog>("Assets/Game/Data/Catalogs/GameDataCatalog.asset").CharacterAppearances;
        for (int i = 0; i < data.villagers.Length; i++)
        {
            var layers = new List<Sprite>();
            var options = new List<CharacterAppearanceDatabase.Entry>();
            foreach (var category in new[] { PlayerAppearanceCategory.Skin, PlayerAppearanceCategory.Clothes, PlayerAppearanceCategory.Eyes, PlayerAppearanceCategory.Hair })
            {
                appearances.GetEntries(category, i == 0 ? PlayerGender.Female : PlayerGender.Male, options);
                var entry = options.ElementAtOrDefault(Math.Min(i, options.Count - 1));
                Sprite sprite = entry?.AnimationSet.GetSprite(PlayerAnimationType.Idle, PlayerDirection.Down, 0);
                if (sprite != null) layers.Add(Centered(data, sprite));
            }
            data.villagers[i].layers = layers.ToArray();
        }
        data.RebuildIndex();
        EditorUtility.SetDirty(data);
    }
    private static FarmCrop Crop(FarmContent data, List<FarmItem> items, string id, string title, string artSeason, int days, int buy, int sell, int seasons, int regrow = 0)
    {
        string path = "Crops/" + artSeason + "/" + title + ".png";
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(Pack + path).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
        Sprite[] stages = frames.Take(Math.Max(1, frames.Length - 1)).Select(s => Centered(data, s)).ToArray();
        Sprite icon = Icon(data, "Food Icons/" + title + ".png");
        if (icon == null && frames.Length > 0) icon = Centered(data, frames[frames.Length - 1]);
        AddItem(items, id + "-seed", title + " Seeds", FarmItemKind.Seed, buy, Math.Max(1, buy / 4), days + " watered days. " + (seasons == 0 ? "All seasons." : artSeason + " crop."), stages.FirstOrDefault());
        AddItem(items, id, title, FarmItemKind.Crop, 0, sell, "Fresh produce. Ship for gold, cook, eat or give as a gift.", icon, 8);
        return new FarmCrop { seedId = id + "-seed", harvestId = id, growthDays = days, seasonMask = seasons, regrowDays = regrow, stages = stages };
    }
    private static void AddItem(List<FarmItem> items, string id, string title, FarmItemKind kind, int buy, int sell, string description, Sprite icon, int energy = 0) =>
        items.Add(new FarmItem { id = id, title = title, kind = kind, buyPrice = buy, sellPrice = sell, maxStack = kind == FarmItemKind.Tool ? 1 : 99, description = description, icon = icon, energy = energy });
    private static FarmRecipe Recipe(string id, string title, string output, int count, int level, params FarmIngredient[] ingredients) =>
        new FarmRecipe { id = id, title = title, outputId = output, outputCount = count, requiredLevel = level, ingredients = ingredients };
    private static FarmQuest Quest(string id, string title, string description, string prerequisite, string counter, int target, int gold, int xp) =>
        new FarmQuest { id = id, title = title, description = description, prerequisite = prerequisite, counter = counter, target = target, goldReward = gold, experienceReward = xp };
    private static Sprite Icon(FarmContent owner, string path) => First(owner, "Icons/" + path);
    private static Sprite First(FarmContent owner, string path)
    {
        var source = AssetDatabase.LoadAllAssetsAtPath(Pack + path).OfType<Sprite>().OrderByDescending(s => s.rect.y).ThenBy(s => s.rect.x).FirstOrDefault();
        if (source != null) return Centered(owner, source);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Pack + path);
        if (texture == null) return null;
        return Slice(owner, path, new Rect(0, 0, Mathf.Min(texture.width, 32), Mathf.Min(texture.height, 32)), Path.GetFileNameWithoutExtension(path));
    }
    private static Sprite Centered(FarmContent owner, Sprite source)
    {
        var sprite = Sprite.Create(source.texture, source.rect, Vector2.one * 0.5f, 16, 0, SpriteMeshType.FullRect);
        sprite.name = source.name + "_Centered";
        AssetDatabase.AddObjectToAsset(sprite, owner);
        return sprite;
    }
    private static Sprite Slice(FarmContent owner, string path, Rect rect, string name)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Pack + path);
        if (texture == null) return null;
        var sprite = Sprite.Create(texture, rect, Vector2.one * 0.5f, 16, 0, SpriteMeshType.FullRect);
        sprite.name = name; AssetDatabase.AddObjectToAsset(sprite, owner); return sprite;
    }
    private static void BuildPanel()
    {
        GameObject root = CozyUi.ModalRoot(null, "FarmHubPanel");
        try
        {
            var panel = root.AddComponent<FarmHubPanel>();
            var card = CozyUi.Rect(root.transform, "Card", Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(1180, 810));
            CozyUi.Panel(card, CozyUi.Cream);
            var serialized = new SerializedObject(panel);
            Set(serialized, "card", card);
            Set(serialized, "title", Text(card, "Title", "FARM JOURNAL", 36, 34, 22, 1000, 50));
            Set(serialized, "summary", Text(card, "Summary", "", 21, 34, 77, 1090, 35));
            Set(serialized, "close", Button(card, "Close", "X", 1080, 24, 64, 50, out _));
            string[] tabNames = { "BACKPACK", "CRAFTING", "JOURNAL", "GUIDE" };
            var tabs = new List<Button>();
            for (int i = 0; i < 4; i++) tabs.Add(Button(card, "Tab" + i, tabNames[i], 34 + i * 280, 126, 263, 50, out _));
            SetArray(serialized, "tabs", tabs);
            var slots = new List<Button>(); var icons = new List<Image>(); var labels = new List<TMP_Text>();
            for (int i = 0; i < 24; i++)
            {
                var button = Button(card, "Slot" + i, "", 34 + i % 6 * 122, 200 + i / 6 * 112, 110, 100, out var label);
                label.fontSize = 16;
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = new Vector2(1, 0.5f);
                label.rectTransform.offsetMin = new Vector2(4, 2); label.rectTransform.offsetMax = new Vector2(-4, 0);
                var iconRect = CozyUi.Rect(button.transform, "Icon", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -5), new Vector2(38, 38));
                var icon = iconRect.gameObject.AddComponent<Image>(); icon.preserveAspect = true; icon.raycastTarget = false;
                slots.Add(button); icons.Add(icon); labels.Add(label);
            }
            SetArray(serialized, "slots", slots); SetArray(serialized, "icons", icons); SetArray(serialized, "slotLabels", labels);
            var rows = new List<Button>(); var rowLabels = new List<TMP_Text>();
            for (int i = 0; i < 6; i++)
            {
                rows.Add(Button(card, "Row" + i, "", 34, 198 + i * 75, 720, 65, out var label));
                label.fontSize = 19; label.alignment = TextAlignmentOptions.MidlineLeft; rowLabels.Add(label);
            }
            SetArray(serialized, "rows", rows); SetArray(serialized, "rowLabels", rowLabels);
            Set(serialized, "detail", Text(card, "Detail", "", 22, 790, 205, 350, 450));
            var actions = new List<Button>(); var actionLabels = new List<TMP_Text>();
            for (int i = 0; i < 3; i++)
            {
                actions.Add(Button(card, "Action" + i, "", 34 + i * 372, 687, 354, 48, out var label));
                label.fontSize = 21; actionLabels.Add(label);
            }
            SetArray(serialized, "actions", actions); SetArray(serialized, "actionLabels", actionLabels);
            Set(serialized, "previous", Button(card, "Previous", "PREVIOUS", 34, 648, 170, 32, out _));
            Set(serialized, "next", Button(card, "Next", "NEXT", 584, 648, 170, 32, out _));
            Set(serialized, "status", Text(card, "Status", "", 20, 34, 750, 1110, 44));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    private static TMP_Text Text(Transform parent, string name, string value, float size, float x, float y, float width, float height) =>
        CozyUi.Text(parent, name, value, size, TextAlignmentOptions.TopLeft, CozyUi.Ink, Vector2.up, Vector2.up, Vector2.up, new Vector2(x, -y), new Vector2(width, height));
    private static Button Button(Transform parent, string name, string value, float x, float y, float width, float height, out TextMeshProUGUI label) =>
        CozyUi.Button(parent, name, value, Vector2.up, Vector2.up, Vector2.up, new Vector2(x, -y), new Vector2(width, height), null, out label);
    private static void Set(SerializedObject target, string field, UnityEngine.Object value) => target.FindProperty(field).objectReferenceValue = value;
    private static void SetArray<T>(SerializedObject target, string field, IList<T> values) where T : UnityEngine.Object
    {
        var array = target.FindProperty(field); array.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
    private static void RegisterPanel()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<UIPanelCatalog>("Assets/Game/Data/UI/UIPanelCatalog.asset");
        var serialized = new SerializedObject(catalog);
        var entries = serialized.FindProperty("entries");
        int index = -1;
        for (int i = 0; i < entries.arraySize; i++) if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == FarmHubPanel.PanelId) index = i;
        if (index < 0) { index = entries.arraySize; entries.InsertArrayElementAtIndex(index); }
        var entry = entries.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("id").stringValue = FarmHubPanel.PanelId;
        entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FarmHubPanel>(PanelPath);
        entry.FindPropertyRelative("layer").enumValueIndex = (int)UILayer.Window;
        entry.FindPropertyRelative("lifetime").enumValueIndex = (int)UIPanelLifetime.Cached;
        entry.FindPropertyRelative("hideHud").boolValue = false;
        entry.FindPropertyRelative("pauseGameplay").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(catalog);
    }
}
