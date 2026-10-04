using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Rebuilds the authored settings prefab and wires its shared frontend/gameplay registration.</summary>
public static class SettingsPanelBuilder
{
    public const string PrefabPath = "Assets/Game/Prefabs/UI/SettingsPanel.prefab";
    private const string CatalogPath = "Assets/Game/Data/UI/UIPanelCatalog.asset";
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

    [MenuItem("Tools/Tiny Farm/UI/Rebuild Settings Panel")]
    public static void Build()
    {
        BuildPrefab();
        RegisterPanel();
        ConfigureRootPrefab();
        ConfigureFrontend();
        AssetDatabase.SaveAssets();
        Debug.Log("Shared settings prefab, catalog and frontend wiring are ready.");
    }

    private static void BuildPrefab()
    {
        GameObject root = CozyUi.ModalRoot(null, "SettingsPanel");
        SettingsPanel panel = root.AddComponent<SettingsPanel>();
        RectTransform card = CozyUi.Rect(root.transform, "Card", Vector2.one * 0.5f, Vector2.one * 0.5f,
            Vector2.one * 0.5f, Vector2.zero, new Vector2(1080f, 760f));
        CozyUi.Panel(card, CozyUi.Cream);
        Text(card, "Title", "SETTINGS", 42f, 40f, 28f, 800f, 58f);
        Text(card, "Subtitle", "Make yourself at home", 21f, 42f, 86f, 850f, 34f);
        Sprite decor = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/FarmRPGTinyAssetPack/UI/Inventory/Decor.png")
            .OfType<Sprite>().FirstOrDefault();
        if (decor != null)
        {
            RectTransform trim = Rect(card, "FarmTrim", 40f, 128f, 1000f, 10f);
            Image image = trim.gameObject.AddComponent<Image>();
            image.sprite = decor;
            image.type = Image.Type.Tiled;
            image.raycastTarget = false;
        }

        string[] tabNames = { "DISPLAY", "AUDIO", "GAMEPLAY", "CONTROLS" };
        var tabs = new Button[4];
        var pages = new GameObject[4];
        var rows = new List<SettingsOptionRow>();
        for (int i = 0; i < 4; i++)
        {
            tabs[i] = Button(card, "Tab" + tabNames[i], tabNames[i], 40f, 170f + i * 83f, 220f, 64f);
            pages[i] = Rect(card, tabNames[i] + "Page", 290f, 162f, 748f, 475f).gameObject;
        }
        rows.Add(Row(pages[0].transform, "WindowMode", "Window mode", "Applied after confirmation", 0));
        rows.Add(Row(pages[0].transform, "Resolution", "Resolution", "Desktop uses your monitor size", 1));
        rows.Add(Row(pages[0].transform, "VSync", "Vertical sync", "Synchronize frames with the display", 2));
        rows.Add(Row(pages[0].transform, "FrameRate", "Frame limit", "Available when VSync is off", 3));
        Text(pages[0].transform, "DisplayNote", "Display changes revert after 15 seconds unless kept.\nChoose Revert to restore the previous display.", 19f, 12f, 372f, 720f, 80f);
        rows.Add(Row(pages[1].transform, "MasterVolume", "Master volume", "All game audio", 0, 0, 100));
        rows.Add(Row(pages[1].transform, "MuteUnfocused", "Mute in background", "Silence audio while another app is active", 1));
        Text(pages[1].transform, "AudioNote", "Audio changes are previewed immediately.\nBack restores your previous volume.", 21f, 12f, 220f, 720f, 110f);
        rows.Add(Row(pages[2].transform, "UiScale", "HUD scale", "Clock, backpack button and hotbar", 0, 80, 120));
        rows.Add(Row(pages[2].transform, "WorldZoom", "World zoom", "Camera zoom while on the farm", 1, 75, 150));
        rows.Add(Row(pages[2].transform, "ClockFormat", "Clock format", "Choose a 12 or 24 hour clock", 2));
        rows.Add(Row(pages[2].transform, "ControlHints", "Control hints", "Show shortcut labels on the HUD", 3));
        rows.Add(Row(pages[2].transform, "PauseUnfocused", "Pause in background", "Pause the farm when switching apps", 4));
        string[] keyNames = { "Move up", "Move down", "Move left", "Move right", "Backpack" };
        for (int i = 0; i < keyNames.Length; i++)
            rows.Add(Row(pages[3].transform, "Binding" + i, keyNames[i], "Choose a letter key", i));
        Text(card, "NavigationHint", "ESC  Back\nArrow keys  Navigate\nEnter  Select\n\nMove with arrows\nor your chosen keys", 18f, 44f, 512f, 214f, 128f);

        TMP_Text status = Text(card, "Status", "", 20f, 42f, 647f, 1000f, 36f);
        Button defaults = Button(card, "Defaults", "DEFAULTS", 42f, 697f, 240f, 44f);
        Button back = Button(card, "Back", "BACK", 570f, 697f, 200f, 44f);
        Button apply = Button(card, "Apply", "APPLY", 794f, 697f, 240f, 44f);
        apply.image.color = CozyUi.Leaf;

        GameObject confirmation = CozyUi.ModalRoot(root.transform, "DisplayConfirmation");
        RectTransform confirmCard = CozyUi.Rect(confirmation.transform, "ConfirmCard", Vector2.one * 0.5f,
            Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(720f, 310f));
        CozyUi.Panel(confirmCard, CozyUi.Cream);
        TMP_Text countdown = Text(confirmCard, "Countdown", "Keep these display settings?", 30f, 38f, 38f, 644f, 125f);
        countdown.alignment = TextAlignmentOptions.Center;
        Button revert = Button(confirmCard, "RevertDisplay", "REVERT", 42f, 210f, 294f, 60f);
        Button keep = Button(confirmCard, "KeepDisplay", "KEEP", 378f, 210f, 294f, 60f);
        keep.image.color = CozyUi.Leaf;
        confirmation.SetActive(false);
        var serialized = new SerializedObject(panel);
        Set(serialized, "card", card);
        Set(serialized, "status", status);
        Set(serialized, "applyButton", apply);
        Set(serialized, "backButton", back);
        Set(serialized, "defaultsButton", defaults);
        Set(serialized, "displayConfirmation", confirmation);
        Set(serialized, "countdown", countdown);
        Set(serialized, "keepDisplayButton", keep);
        Set(serialized, "revertDisplayButton", revert);
        SetArray(serialized, "tabs", tabs);
        SetArray(serialized, "pages", pages);
        SetArray(serialized, "rows", rows.ToArray());
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
    }

    private static SettingsOptionRow Row(Transform parent, string name, string caption, string hint,
        int index, int min = 0, int max = 0)
    {
        RectTransform root = Rect(parent, name, 0f, index * 94f, 748f, 84f);
        CozyUi.Panel(root, new Color32(242, 222, 175, 255), false);
        Text(root, "Caption", caption, 25f, 14f, 8f, 370f, 36f);
        Text(root, "Hint", hint, 16f, 14f, 46f, 370f, 28f);
        TMP_Text value = Text(root, "Value", "", 23f, 438f, 6f, 232f, 36f);
        value.alignment = TextAlignmentOptions.Center;
        Button previous = Button(root, "Previous", "<", 398f, 17f, 44f, 48f);
        Button next = Button(root, "Next", ">", 688f, 17f, 44f, 48f);
        RectTransform sliderRect = Rect(root, "Slider", 415f, 46f, 294f, 30f);
        Image background = CozyUi.Panel(sliderRect, CozyUi.Wood, false);
        Slider slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max > min ? max : 100;
        slider.wholeNumbers = true;
        RectTransform handleArea = CozyUi.Rect(sliderRect, "HandleArea", Vector2.zero, Vector2.one,
            Vector2.one * 0.5f, Vector2.zero, new Vector2(-22f, 0f));
        RectTransform handle = CozyUi.Rect(handleArea, "Handle", Vector2.zero, Vector2.one,
            Vector2.one * 0.5f, Vector2.zero, new Vector2(22f, 0f));
        Image handleImage = CozyUi.Panel(handle, CozyUi.Gold);
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        sliderRect.gameObject.SetActive(max > min);
        previous.gameObject.SetActive(max == min);
        next.gameObject.SetActive(max == min);
        if (max == min) ((RectTransform)value.transform).anchoredPosition = new Vector2(438f, -23f);
        SettingsOptionRow row = root.gameObject.AddComponent<SettingsOptionRow>();
        var serialized = new SerializedObject(row);
        Set(serialized, "valueLabel", value);
        Set(serialized, "previous", previous);
        Set(serialized, "next", next);
        Set(serialized, "slider", slider);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return row;
    }

    private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height) =>
        CozyUi.Rect(parent, name, TopLeft, TopLeft, TopLeft, new Vector2(x, -y), new Vector2(width, height));

    private static TMP_Text Text(Transform parent, string name, string text, float fontSize, float x, float y, float width, float height) =>
        CozyUi.Text(parent, name, text, fontSize, TextAlignmentOptions.Left, CozyUi.Ink,
            TopLeft, TopLeft, TopLeft, new Vector2(x, -y), new Vector2(width, height));

    private static Button Button(Transform parent, string name, string label, float x, float y, float width, float height)
    {
        Button button = CozyUi.Button(parent, name, label, TopLeft, TopLeft, TopLeft,
            new Vector2(x, -y), new Vector2(width, height), null, out TextMeshProUGUI text);
        text.fontSize = 23f;
        return button;
    }

    private static void Set(SerializedObject target, string name, Object value) => target.FindProperty(name).objectReferenceValue = value;
    private static void SetArray<T>(SerializedObject target, string name, T[] values) where T : Object
    {
        SerializedProperty property = target.FindProperty(name);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void RegisterPanel()
    {
        UIPanelCatalog catalog = AssetDatabase.LoadAssetAtPath<UIPanelCatalog>(CatalogPath);
        var serialized = new SerializedObject(catalog);
        SerializedProperty entries = serialized.FindProperty("entries");
        int index = -1;
        for (int i = 0; i < entries.arraySize; i++)
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == SettingsPanel.PanelId) index = i;
        if (index < 0) { index = entries.arraySize; entries.arraySize++; }
        SerializedProperty entry = entries.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("id").stringValue = SettingsPanel.PanelId;
        entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SettingsPanel>(PrefabPath);
        entry.FindPropertyRelative("layer").enumValueIndex = (int)UILayer.Popup;
        entry.FindPropertyRelative("lifetime").enumValueIndex = (int)UIPanelLifetime.Cached;
        entry.FindPropertyRelative("hideHud").boolValue = false;
        entry.FindPropertyRelative("pauseGameplay").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureRootPrefab()
    {
        const string path = "Assets/Game/Prefabs/Core/GameRoot.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root.GetComponent<SettingsService>() == null) root.AddComponent<SettingsService>();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        const string configPath = "Assets/Game/Resources/GameBootstrap.asset";
        GameBootstrapConfig config = AssetDatabase.LoadAssetAtPath<GameBootstrapConfig>(configPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<GameBootstrapConfig>();
            AssetDatabase.CreateAsset(config, configPath);
        }
        var serialized = new SerializedObject(config);
        Set(serialized, "rootPrefab", AssetDatabase.LoadAssetAtPath<GameRoot>(path));
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureFrontend()
    {
        const string path = "Assets/Game/Scenes/Frontend/MainMenu.unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        bool wasOpen = scene.isLoaded;
        if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        Canvas canvas = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Canvas>(true)).First();
        if (canvas.GetComponent<UIRoot>() == null)
        {
            UIRoot root = canvas.gameObject.AddComponent<UIRoot>();
            var serialized = new SerializedObject(root);
            string[] names = { "hudLayer", "screenLayer", "windowLayer", "popupLayer", "toastLayer", "transitionLayer" };
            foreach (string name in names)
            {
                RectTransform layer = CozyUi.Rect(canvas.transform, name, Vector2.zero, Vector2.one,
                    Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
                Set(serialized, name, layer);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        if (!scene.GetRootGameObjects().Any(go => go.GetComponent<GameRoot>() != null))
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Core/GameRoot.prefab"), scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
    }
}
