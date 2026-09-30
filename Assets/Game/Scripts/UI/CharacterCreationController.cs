using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 角色创建面板的总控制器。
/// 数据流：CharacterAppearanceDatabase -> 可选外观列表 -> UI 预览/场景角色 -> CharacterCreationProfile 存档。
/// 面板控件通过现有层级名称自动查找，因此增加资源时不需要修改场景引用或选项数量。
/// </summary>
public sealed class CharacterCreationController : MonoBehaviour
{
    /// <summary>缓存一行外观选择控件及其当前可用数据。</summary>
    private sealed class Row
    {
        public PlayerAppearanceCategory Category;
        public TMP_Text Number;
        public Button Previous;
        public Button Next;
        public readonly List<CharacterAppearanceDatabase.Entry> Options =
            new List<CharacterAppearanceDatabase.Entry>();
        public int Index;
    }

    [Header("数据与应用目标")]
    [Tooltip("游戏静态数据的统一入口。")]
    [SerializeField] private GameDataCatalog dataCatalog;
    [Tooltip("确认或切换选项时接收外观的场景角色。")]
    [SerializeField] private PlayerAppearance targetAppearance;

    [Header("面板控件")]
    [SerializeField] private TMP_InputField nameInput;
    [Tooltip("预览区域的占位 Image；运行时会在其下创建五个叠加图层。")]
    [SerializeField] private Image previewTemplate;
    [SerializeField] private Image maleBackground;
    [SerializeField] private Image femaleBackground;
    [SerializeField] private Color selectedGenderColor = Color.white;
    [SerializeField] private Color normalGenderColor = new Color(0.72f, 0.72f, 0.72f, 1f);
    [Header("Start Game Transition")]
    [Tooltip("Seconds used for both the fade to black and the fade back to the game.")]
    [Min(0.01f)]
    [SerializeField] private float fadeDuration = 0.6f;
    [Tooltip("角色数据保存并应用完成后触发，可在 Inspector 中连接场景切换等后续流程。")]
    [SerializeField] private UnityEvent onConfirmed;

    // 每种外观对应一行选择器；预览字典则保存运行时生成的五层 UI Image。
    private readonly Dictionary<PlayerAppearanceCategory, Row> rows =
        new Dictionary<PlayerAppearanceCategory, Row>();
    private readonly Dictionary<PlayerAppearanceCategory, Image> previews =
        new Dictionary<PlayerAppearanceCategory, Image>();
    private CharacterCreationProfile profile;
    private bool initialized;
    private bool isConfirming;
    private Button startGameButton;
    private Button returnButton;
    private PlayerMovement playerMovement;
    private bool playerMovementWasEnabled;
    private CharacterNameLanguage nameLanguage;
    private CharacterAppearanceDatabase database;
    private CharacterNameDatabase nameDatabase;
    private bool startWithNewProfile;

    /// <summary>供后续创建存档、进入游戏等系统读取尚未或已经确认的角色资料。</summary>
    public CharacterCreationProfile CurrentProfile => profile;
    public GameDataCatalog DataCatalog => dataCatalog;

    /// <summary>Opens character creation with clean data without deleting the existing save first.</summary>
    public void BeginNewGame()
    {
        bool wasInitialized = initialized;
        startWithNewProfile = true;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (wasInitialized)
        {
            ResetForNewGame();
            LockPlayerMovement();
        }
    }

    /// <summary>Abandons the unfinished character and returns to the title menu without touching the save.</summary>
    public void ReturnToMainMenu()
    {
        if (isConfirming) return;

        if (CharacterCreationSave.TryLoadProfile(out CharacterCreationProfile savedProfile))
            CharacterAppearanceService.Apply(savedProfile, dataCatalog, targetAppearance);

        Transform menuTransform = transform.parent != null ? transform.parent.Find("MenuPanel") : null;
        MainMenuController menu = menuTransform != null ? menuTransform.GetComponent<MainMenuController>() : null;
        if (menu == null)
        {
            Debug.LogError("MenuPanel/MainMenuController was not found.", this);
            return;
        }

        menu.ShowMainMenu();
        gameObject.SetActive(false);
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        // 这些监听由本组件在运行时添加，销毁时清理以避免重复绑定。
        foreach (Row row in rows.Values)
        {
            if (row.Previous != null) row.Previous.onClick.RemoveAllListeners();
            if (row.Next != null) row.Next.onClick.RemoveAllListeners();
        }
    }

    /// <summary>
    /// 加载数据库和上次存档，并根据当前场景层级建立所有按钮监听。
    /// 使用 initialized 防止组件被外部系统重复初始化。
    /// </summary>
    public void Initialize()
    {
        if (initialized) return;
        database = dataCatalog != null ? dataCatalog.CharacterAppearances : null;
        nameDatabase = dataCatalog != null ? dataCatalog.CharacterNames : null;
        if (database == null)
        {
            Debug.LogError("GameDataCatalog or its CharacterAppearanceDatabase is missing.", this);
            return;
        }
        if (nameDatabase == null)
            Debug.LogWarning("CharacterNameDatabase is missing. Assign the asset from Assets/Game/Data.", this);

        AutoWire();
        nameLanguage = LocaleToLanguage(nameDatabase != null ? nameDatabase.DefaultLocaleCode : "zh-CN");
        profile = startWithNewProfile ? new CharacterCreationProfile() : CharacterCreationSave.Load();
        if (string.IsNullOrWhiteSpace(profile.playerName)) profile.playerName = GenerateRandomName();
        if (nameInput != null)
        {
            nameInput.text = profile.playerName;
            nameInput.onValueChanged.AddListener(value => profile.playerName = value.Trim());
        }

        BuildRows();
        SetGender(profile.gender);
        LockPlayerMovement();
        initialized = true;
    }

    /// <summary>切换为男性，并重新过滤带有性别限制的外观资源。</summary>
    public void SetMale() => SetGender(PlayerGender.Male);
    /// <summary>切换为女性，并重新过滤带有性别限制的外观资源。</summary>
    public void SetFemale() => SetGender(PlayerGender.Female);

    /// <summary>从候选名字中随机一个名字，并同步输入框。</summary>
    public void RandomizeName()
    {
        string value = GenerateRandomName();
        if (string.IsNullOrEmpty(value)) return;
        profile.playerName = value;
        if (nameInput != null) nameInput.text = value;
    }

    private void ResetForNewGame()
    {
        profile = new CharacterCreationProfile();
        profile.playerName = GenerateRandomName();
        if (nameInput != null) nameInput.text = profile.playerName;
        SetGender(profile.gender);
        isConfirming = false;
        if (startGameButton != null) startGameButton.interactable = true;

        CanvasGroup panelGroup = GetComponent<CanvasGroup>();
        if (panelGroup != null)
        {
            panelGroup.alpha = 1f;
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;
        }
    }

    public void UseChineseNames() => nameLanguage = CharacterNameLanguage.Chinese;
    public void UseEnglishNames() => nameLanguage = CharacterNameLanguage.English;
    public void UseMixedNames() => nameLanguage = CharacterNameLanguage.Mixed;

    public void SetNameLanguage(CharacterNameLanguage language)
    {
        nameLanguage = language;
    }

    private string GenerateRandomName()
    {
        string generatedName = nameLanguage == CharacterNameLanguage.Mixed
            ? CharacterNameGenerator.GetRandomNameFromAllPools(nameDatabase)
            : CharacterNameGenerator.GetRandomName(nameDatabase, LanguageToLocale(nameLanguage));
        return string.IsNullOrEmpty(generatedName) ? "Player" : generatedName;
    }

    private static string LanguageToLocale(CharacterNameLanguage language)
    {
        return language == CharacterNameLanguage.English ? "en" : "zh-CN";
    }

    private static CharacterNameLanguage LocaleToLanguage(string localeCode)
    {
        return !string.IsNullOrEmpty(localeCode) && localeCode.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? CharacterNameLanguage.English
            : CharacterNameLanguage.Chinese;
    }

    /// <summary>在每个已有选择行中随机选择一项外观。</summary>
    public void RandomizeAppearance()
    {
        foreach (Row row in rows.Values)
        {
            if (row.Options.Count == 0) continue;
            row.Index = UnityEngine.Random.Range(0, row.Options.Count);
            Select(row, 0);
        }
    }

    /// <summary>
    /// 校验名字、持久化当前资料、应用到角色，最后通知后续游戏流程。
    /// 此方法可以直接绑定到面板的“确认/开始”按钮。
    /// </summary>
    public void Confirm()
    {
        if (isConfirming || profile == null) return;
        isConfirming = true;
        if (startGameButton != null) startGameButton.interactable = false;

        if (nameInput != null) profile.playerName = nameInput.text.Trim();
        if (string.IsNullOrWhiteSpace(profile.playerName))
        {
            RandomizeName();
        }
        CharacterCreationSave.Save(profile);
        StartCoroutine(CompleteCharacterCreation());
    }

    private IEnumerator CompleteCharacterCreation()
    {
        Image fadeOverlay = CreateFadeOverlay();
        if (fadeOverlay == null)
        {
            FinishCharacterCreation();
            yield break;
        }

        yield return Fade(fadeOverlay, 0f, 1f);

        ApplyTo(targetAppearance);

        CanvasGroup panelGroup = GetComponent<CanvasGroup>();
        if (panelGroup == null) panelGroup = gameObject.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;

        yield return Fade(fadeOverlay, 1f, 0f);

        UnlockPlayerMovement();
        Destroy(fadeOverlay.gameObject);
        onConfirmed?.Invoke();
        Destroy(gameObject);
    }

    private IEnumerator Fade(Image overlay, float from, float to)
    {
        float duration = Mathf.Max(0.01f, fadeDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetImageAlpha(overlay, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        SetImageAlpha(overlay, to);
    }

    private Image CreateFadeOverlay()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return null;

        GameObject overlayObject = new GameObject(
            "CharacterCreationFade",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        overlayObject.layer = canvas.gameObject.layer;

        RectTransform rect = overlayObject.GetComponent<RectTransform>();
        rect.SetParent(canvas.rootCanvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsLastSibling();

        Image overlay = overlayObject.GetComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0f);
        overlay.raycastTarget = true;
        return overlay;
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private void FinishCharacterCreation()
    {
        ApplyTo(targetAppearance);
        onConfirmed?.Invoke();
        UnlockPlayerMovement();
        Destroy(gameObject);
    }

    /// <summary>把资料中的五个稳定资源 ID 解析为动画资源并应用到指定角色。</summary>
    public void ApplyTo(PlayerAppearance appearance)
    {
        if (appearance == null || database == null) return;
        appearance.SetSkin(GetAnimation(PlayerAppearanceCategory.Skin));
        appearance.SetClothes(GetAnimation(PlayerAppearanceCategory.Clothes));
        appearance.SetEyes(GetAnimation(PlayerAppearanceCategory.Eyes));
        appearance.SetHair(GetAnimation(PlayerAppearanceCategory.Hair));
        appearance.SetAccessory(GetAnimation(PlayerAppearanceCategory.Accessory));
        appearance.RefreshCurrentFrame();
    }

    /// <summary>按照当前资料保存的 ID 从数据库取得具体动画资源。</summary>
    private PlayerPartAnimationSet GetAnimation(PlayerAppearanceCategory category)
    {
        CharacterAppearanceDatabase.Entry entry = database.Find(profile.GetPartId(category));
        return entry != null ? entry.AnimationSet : null;
    }

    /// <summary>把现有面板的各个选择区域转换为运行时 Row。</summary>
    private void BuildRows()
    {
        rows.Clear();
        AddRow(PlayerAppearanceCategory.Skin, "SkinChooseBg");
        AddRow(PlayerAppearanceCategory.Clothes, "ClothesChooseBg");
        AddRow(PlayerAppearanceCategory.Eyes, "EyesChooseBg");
        AddRow(PlayerAppearanceCategory.Hair, "HairsChooseBg");
        AddRow(PlayerAppearanceCategory.Accessory, "AccessoryChooseBg", false);
    }

    /// <summary>
    /// 查找一行中的左右按钮和数量文本，并注册循环切换事件。
    /// 左右按钮即使同名，也能根据本地 X 坐标稳定区分。
    /// </summary>
    private void AddRow(PlayerAppearanceCategory category, string objectName, bool required = true)
    {
        Transform root = FindDescendant(transform, objectName);
        if (root == null)
        {
            if (required) Debug.LogWarning("Character creation row not found: " + objectName, this);
            return;
        }

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        Array.Sort(buttons, (a, b) => a.transform.localPosition.x.CompareTo(b.transform.localPosition.x));
        Row row = new Row
        {
            Category = category,
            Number = FindComponent<TMP_Text>(root, "NumTMP"),
            Previous = buttons.Length > 0 ? buttons[0] : null,
            Next = buttons.Length > 1 ? buttons[buttons.Length - 1] : null
        };
        if (row.Previous != null) row.Previous.onClick.AddListener(() => Select(row, -1));
        if (row.Next != null) row.Next.onClick.AddListener(() => Select(row, 1));
        rows.Add(category, row);
    }

    /// <summary>更新性别，并保持仍然有效的原选择；无效时回退到该分类第一项。</summary>
    private void SetGender(PlayerGender gender)
    {
        profile.gender = gender;
        if (maleBackground != null) maleBackground.color = gender == PlayerGender.Male ? selectedGenderColor : normalGenderColor;
        if (femaleBackground != null) femaleBackground.color = gender == PlayerGender.Female ? selectedGenderColor : normalGenderColor;

        foreach (Row row in rows.Values)
        {
            string selectedId = profile.GetPartId(row.Category);
            database.GetEntries(row.Category, gender, row.Options);
            row.Index = FindIndex(row.Options, selectedId);
            if (row.Index < 0) row.Index = 0;
            Select(row, 0);
        }
    }

    /// <summary>
    /// 在一行的候选项中循环移动。取模计算可让第一项向左回到末项，反之亦然。
    /// </summary>
    private void Select(Row row, int delta)
    {
        if (row.Options.Count == 0)
        {
            profile.SetPartId(row.Category, string.Empty);
            if (row.Number != null) row.Number.text = "0";
            UpdatePreview(row.Category, null);
            return;
        }

        row.Index = (row.Index + delta + row.Options.Count) % row.Options.Count;
        CharacterAppearanceDatabase.Entry entry = row.Options[row.Index];
        profile.SetPartId(row.Category, entry.Id);
        if (row.Number != null) row.Number.text = (row.Index + 1).ToString();
        UpdatePreview(row.Category, entry.AnimationSet);
        ApplyTo(targetAppearance);
    }

    /// <summary>
    /// 使用各部件朝下待机的第一帧合成静态预览。
    /// siblingIndex 与枚举顺序一致，确保皮肤在底层、饰品在最上层。
    /// </summary>
    private void UpdatePreview(PlayerAppearanceCategory category, PlayerPartAnimationSet set)
    {
        if (previewTemplate == null) return;
        if (!previews.TryGetValue(category, out Image image))
        {
            GameObject layer = new GameObject("Preview_" + category, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = (RectTransform)layer.transform;
            rect.SetParent(previewTemplate.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetSiblingIndex((int)category);
            image = layer.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            previews.Add(category, image);
        }
        image.sprite = set != null ? set.GetSprite(PlayerAnimationType.Idle, PlayerDirection.Down, 0) : null;
        image.enabled = image.sprite != null;
        previewTemplate.enabled = false;
    }

    /// <summary>按约定的对象名称自动寻找控件，使场景无需逐项拖拽引用。</summary>
    private void AutoWire()
    {
        targetAppearance = targetAppearance != null ? targetAppearance : FindObjectOfType<PlayerAppearance>();
        nameInput = nameInput != null ? nameInput : FindComponent<TMP_InputField>(transform, "NameInputField");
        previewTemplate = previewTemplate != null ? previewTemplate : FindComponent<Image>(transform, "PlayerPreview");
        maleBackground = maleBackground != null ? maleBackground : FindComponent<Image>(transform, "MaleBg");
        femaleBackground = femaleBackground != null ? femaleBackground : FindComponent<Image>(transform, "FemaleBg");

        BindButton("MaleBg", SetMale);
        BindButton("FemaleBg", SetFemale);
        BindButton("RandomNameBtn", RandomizeName);
        startGameButton = BindButton("StartGameBtn", Confirm);
        returnButton = BindButton("ReturnBtn", ReturnToMainMenu);
    }

    /// <summary>为现有图片对象补充或取得 Button，并绑定指定事件。</summary>
    private Button BindButton(string objectName, UnityAction action)
    {
        Transform target = FindDescendant(transform, objectName);
        if (target == null) return null;
        Button button = target.GetComponent<Button>();
        if (button == null) button = target.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        return button;
    }

    private void LockPlayerMovement()
    {
        if (targetAppearance == null) return;
        playerMovement = targetAppearance.GetComponent<PlayerMovement>();
        if (playerMovement == null) return;
        playerMovementWasEnabled = playerMovement.enabled;
        playerMovement.enabled = false;
    }

    private void UnlockPlayerMovement()
    {
        if (playerMovement != null) playerMovement.enabled = playerMovementWasEnabled;
    }

    /// <summary>根据稳定 ID 查找候选项下标；旧资源不存在时返回 -1。</summary>
    private static int FindIndex(List<CharacterAppearanceDatabase.Entry> options, string id)
    {
        for (int i = 0; i < options.Count; i++)
            if (options[i].Id == id) return i;
        return -1;
    }

    /// <summary>在指定层级中按对象名称查找组件。</summary>
    private static T FindComponent<T>(Transform root, string objectName) where T : Component
    {
        Transform target = FindDescendant(root, objectName);
        return target != null ? target.GetComponent<T>() : null;
    }

    /// <summary>包括非激活对象在内，递归查找第一个同名子节点。</summary>
    private static Transform FindDescendant(Transform root, string objectName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            if (children[i].name == objectName) return children[i];
        return null;
    }
}
