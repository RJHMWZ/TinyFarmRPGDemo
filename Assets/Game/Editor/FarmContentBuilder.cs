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
        AddItem(items, "hoe", "锄头", FarmItemKind.Tool, 0, 0, "用于耕开空地，为播种做准备。", Icon(data, "RPG icons/Weapons and Armor/1. Wood/Hoe.png"));
        AddItem(items, "watering-can", "水壶", FarmItemKind.Tool, 0, 0, "给耕地浇水，作物只有在浇水后才会生长。", Icon(data, "RPG icons/Weapons and Armor/1. Wood/Watering can.png"));
        AddItem(items, "axe", "斧头", FarmItemKind.Tool, 0, 0, "砍伐树木，收集木材。", Icon(data, "RPG icons/Weapons and Armor/1. Wood/Axe.png"));
        AddItem(items, "pickaxe", "镐", FarmItemKind.Tool, 0, 0, "采集石头，或清除空耕地。", Icon(data, "RPG icons/Weapons and Armor/1. Wood/Pickaxe.png"));
        AddItem(items, "wood", "木材", FarmItemKind.Material, 0, 3, "用于建设和制作物品。", Icon(data, "RPG icons/Extras/Wood.png"));
        AddItem(items, "stone", "石头", FarmItemKind.Material, 0, 2, "用于制作洒水器和工具。", Icon(data, "RPG icons/Extras/Stones.png"));
        AddItem(items, "berry", "野生浆果", FarmItemKind.Food, 25, 10, "食用恢复 15 体力，也可以送给村民。", Icon(data, "Food Icons/Blackberry.png"), 15);
        AddItem(items, "salad", "农场沙拉", FarmItemKind.Food, 90, 35, "新鲜的沙拉，食用恢复 45 体力。", Icon(data, "Food Icons/Salad.png"), 45);
        AddItem(items, "fertilizer", "肥料", FarmItemKind.Material, 35, 10, "播种前施肥，每次收获增加一个产物。", Icon(data, "RPG icons/Extras/Stones.png"));
        AddItem(items, "sprinkler", "洒水器", FarmItemKind.Furniture, 250, 80, "每天早上浇灌自身和上下左右的四格农田。", Icon(data, "RPG icons/Weapons and Armor/3. Iron/Watering Can.png"));
        data.crops = new[]
        {
            Crop(data, items, "parsnip", "防风草", "Spring", 4, 20, 45, 1),
            Crop(data, items, "potato", "土豆", "Spring", 5, 35, 75, 1),
            Crop(data, items, "tomato", "番茄", "Summer", 5, 40, 50, 2, 2),
            Crop(data, items, "pumpkin", "南瓜", "Fall", 7, 70, 150, 4),
            Crop(data, items, "carrot", "胡萝卜", "Spring", 4, 25, 45, 0)
        };
        data.items = items.ToArray();
        data.RebuildIndex();
        data.recipes = new[]
        {
            Recipe("fertilizer", "肥料", "fertilizer", 2, 1, new FarmIngredient("wood", 3), new FarmIngredient("stone", 2)),
            Recipe("salad", "农场沙拉", "salad", 1, 1, new FarmIngredient("parsnip", 1), new FarmIngredient("berry", 2)),
            Recipe("sprinkler", "洒水器", "sprinkler", 1, 2, new FarmIngredient("wood", 10), new FarmIngredient("stone", 10)),
            Recipe("seed-pouch", "胡萝卜种子包", "carrot-seed", 3, 2, new FarmIngredient("carrot", 1), new FarmIngredient("wood", 2))
        };
        data.quests = new[]
        {
            Quest("first-soil", "新的开始", "用锄头耕出 5 块土地。", "", "till", 5, 80, 20),
            Quest("first-seeds", "播下希望", "播下 5 颗种子。", "first-soil", "plant", 5, 100, 20),
            Quest("first-water", "悉心照料", "给 5 块农田浇水。", "first-seeds", "water", 5, 60, 20),
            Quest("neighbors", "认识邻居", "与村民聊天 3 次。", "", "talk", 3, 90, 30),
            Quest("gatherer", "收集基础材料", "采集资源 5 次。", "", "gather", 5, 100, 40),
            Quest("first-harvest", "第一次收获", "收获 5 个作物。", "first-water", "harvest", 5, 150, 50),
            Quest("first-shipping", "从农场到集市", "通过出货箱出售 5 个物品。", "first-harvest", "ship", 5, 120, 40),
            Quest("maker", "亲手制作", "制作物品 3 次。", "gatherer", "craft", 3, 150, 50),
            Quest("friend", "一份心意", "给村民送礼 3 次。", "neighbors", "gift", 3, 120, 40),
            Quest("prosperity", "农场成长", "通过出货累计赚取 1000 金币。", "first-shipping", "earn", 1000, 300, 100)
        };
        data.villagers = new[]
        {
            new FarmNpc { id = "willow", title = "薇洛", favoriteItem = "parsnip", dialogue = new[] { "欢迎！先种几块地并浇好水，小农场也会有大收获。", "收获前记得给背包留点空间。成熟的作物可以安心留在地里。", "雨水是农夫的好帮手，下雨时可以多去收集些材料。" } },
            new FarmNpc { id = "rowan", title = "罗恩", favoriteItem = "berry", dialogue = new[] { "附近的木材和石头两天后就会恢复，按需采集就好。", "洒水器每天早上都会浇灌自身和周围四格农田。", "礼物让人开心，友好的聊天也同样重要。" } }
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
        string artTitle = char.ToUpperInvariant(id[0]) + id.Substring(1);
        string path = "Crops/" + artSeason + "/" + artTitle + ".png";
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(Pack + path).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
        Sprite[] stages = frames.Take(Math.Max(1, frames.Length - 1)).Select(s => Centered(data, s)).ToArray();
        Sprite icon = Icon(data, "Food Icons/" + artTitle + ".png");
        if (icon == null && frames.Length > 0) icon = Centered(data, frames[frames.Length - 1]);
        AddItem(items, id + "-seed", title + "种子", FarmItemKind.Seed, buy, Math.Max(1, buy / 4), days + " 天浇水后成熟。" + (seasons == 0 ? "四季可种。" : GameCalendar.SeasonName(seasons == 1 ? 0 : seasons == 2 ? 1 : 2) + "作物。"), stages.FirstOrDefault());
        AddItem(items, id, title, FarmItemKind.Crop, 0, sell, "新鲜农产品，可以出售、制作食物或送礼。", icon, 8);
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
            Set(serialized, "title", Text(card, "Title", "农场任务", 36, 34, 22, 1000, 50));
            Set(serialized, "summary", Text(card, "Summary", "", 21, 34, 77, 1090, 35));
            Set(serialized, "close", Button(card, "Close", "X", 1080, 24, 64, 50, out _));
            string[] tabNames = { "背包", "制作", "任务", "指南" };
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
            Set(serialized, "previous", Button(card, "Previous", "上一页", 34, 648, 170, 32, out _));
            Set(serialized, "next", Button(card, "Next", "下一页", 584, 648, 170, 32, out _));
            Set(serialized, "status", Text(card, "Status", "", 20, 34, 750, 1110, 44));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            panel.ConfigurePresentation();
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
