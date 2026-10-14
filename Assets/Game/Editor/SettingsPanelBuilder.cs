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
        Text(card, "Title", "设置", 42f, 40f, 28f, 800f, 58f);
        Text(card, "Subtitle", "按自己的习惯调整", 21f, 42f, 86f, 850f, 34f);
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
            tabs[i] = Button(card, "Tab" + tabNames[i], new[] { "显示", "声音", "游戏", "按键" }[i], 40f, 170f + i * 83f, 220f, 64f);
            pages[i] = Rect(card, tabNames[i] + "Page", 290f, 162f, 748f, 475f).gameObject;
        }
        rows.Add(Row(pages[0].transform, "WindowMode", "窗口模式", "确认后应用", 0));
        rows.Add(Row(pages[0].transform, "Resolution", "分辨率", "默认使用桌面分辨率", 1));
        rows.Add(Row(pages[0].transform, "VSync", "垂直同步", "与显示器刷新率同步", 2));
        rows.Add(Row(pages[0].transform, "FrameRate", "帧率上限", "关闭垂直同步后可调整", 3));
        Text(pages[0].transform, "DisplayNote", "修改显示设置后需在 15 秒内确认。\n点击“还原”恢复原设置。", 19f, 12f, 372f, 720f, 80f);
        rows.Add(Row(pages[1].transform, "MasterVolume", "总音量", "调整所有游戏声音", 0, 0, 100));
        rows.Add(Row(pages[1].transform, "MuteUnfocused", "后台静音", "切换到其他程序时静音", 1));
        Text(pages[1].transform, "AudioNote", "音量修改会立即预览。\n点击“返回”取消修改。", 21f, 12f, 220f, 720f, 110f);
        rows.Add(Row(pages[2].transform, "UiScale", "界面大小", "调整时钟、按钮和快捷栏", 0, 80, 120));
        rows.Add(Row(pages[2].transform, "WorldZoom", "镜头缩放", "调整农场镜头的远近", 1, 75, 150));
        rows.Add(Row(pages[2].transform, "ClockFormat", "时间格式", "选择 12 或 24 小时制", 2));
        rows.Add(Row(pages[2].transform, "ControlHints", "操作提示", "在界面上显示快捷键", 3));
        rows.Add(Row(pages[2].transform, "PauseUnfocused", "后台暂停", "切换程序时暂停游戏", 4));
        string[] keyNames = { "向上移动", "向下移动", "向左移动", "向右移动", "背包" };
        for (int i = 0; i < keyNames.Length; i++)
            rows.Add(Row(pages[3].transform, "Binding" + i, keyNames[i], "点击后按字母键修改", i));
        Text(card, "NavigationHint", "Esc：返回\n方向键：选择\n回车：确认\n\n方向键和自定义按键\n都可以移动角色", 18f, 44f, 512f, 214f, 128f);

        TMP_Text status = Text(card, "Status", "", 20f, 42f, 647f, 1000f, 36f);
        Button defaults = Button(card, "Defaults", "恢复默认", 42f, 697f, 240f, 44f);
        Button back = Button(card, "Back", "返回", 570f, 697f, 200f, 44f);
        Button apply = Button(card, "Apply", "应用", 794f, 697f, 240f, 44f);
        apply.image.color = CozyUi.Leaf;

        GameObject confirmation = CozyUi.ModalRoot(root.transform, "DisplayConfirmation");
        RectTransform confirmCard = CozyUi.Rect(confirmation.transform, "ConfirmCard", Vector2.one * 0.5f,
            Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(720f, 310f));
        CozyUi.Panel(confirmCard, CozyUi.Cream);
        TMP_Text countdown = Text(confirmCard, "Countdown", "保留新的显示设置？", 30f, 38f, 38f, 644f, 125f);
        countdown.alignment = TextAlignmentOptions.Center;
        Button revert = Button(confirmCard, "RevertDisplay", "还原", 42f, 210f, 294f, 60f);
        Button keep = Button(confirmCard, "KeepDisplay", "保留", 378f, 210f, 294f, 60f);
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
