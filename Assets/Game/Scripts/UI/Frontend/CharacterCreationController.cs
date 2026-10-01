using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Coordinates character-creation data, view state, and confirmation flow.</summary>
public sealed class CharacterCreationController : MonoBehaviour
{
    private sealed class Row
    {
        public PlayerAppearanceCategory Category;
        public TMP_Text Number;
        public Button Previous;
        public Button Next;
        public UnityAction PreviousAction;
        public UnityAction NextAction;
        public readonly List<CharacterAppearanceDatabase.Entry> Options =
            new List<CharacterAppearanceDatabase.Entry>();
        public int Index;
    }

    [Header("Data and flow")]
    [SerializeField] private GameDataCatalog dataCatalog;
    [SerializeField] private MainMenuController frontEnd;
    [SerializeField] private CharacterCreationView view;

    [Header("Presentation")]
    [SerializeField] private Color selectedGenderColor = Color.white;
    [SerializeField] private Color normalGenderColor = new Color(0.72f, 0.72f, 0.72f, 1f);
    [SerializeField] private UnityEvent onConfirmed;

    private readonly Dictionary<PlayerAppearanceCategory, Row> rows =
        new Dictionary<PlayerAppearanceCategory, Row>();
    private readonly Dictionary<PlayerAppearanceCategory, Image> previews =
        new Dictionary<PlayerAppearanceCategory, Image>();

    private CharacterCreationProfile profile;
    private CharacterAppearanceDatabase database;
    private CharacterNameDatabase nameDatabase;
    private UnityAction<string> nameChangedAction;
    private bool initialized;
    private bool eventsBound;
    private bool isConfirming;
    private bool startWithNewProfile;

    public CharacterCreationProfile CurrentProfile => profile;
    public GameDataCatalog DataCatalog => dataCatalog;

    public void Configure(MainMenuController owner) => frontEnd = owner;

    private void Awake() => Initialize();

    private void OnDestroy()
    {
        if (!eventsBound || view == null) return;

        view.MaleButton.onClick.RemoveListener(SetMale);
        view.FemaleButton.onClick.RemoveListener(SetFemale);
        view.RandomNameButton.onClick.RemoveListener(RandomizeName);
        view.StartGameButton.onClick.RemoveListener(Confirm);
        view.ReturnButton.onClick.RemoveListener(ReturnToMainMenu);
        if (nameChangedAction != null) view.NameInput.onValueChanged.RemoveListener(nameChangedAction);

        foreach (Row row in rows.Values)
        {
            if (row.PreviousAction != null) row.Previous.onClick.RemoveListener(row.PreviousAction);
            if (row.NextAction != null) row.Next.onClick.RemoveListener(row.NextAction);
        }
    }

    /// <summary>Opens character creation with clean data without deleting an existing save first.</summary>
    public void BeginNewGame()
    {
        bool wasInitialized = initialized;
        startWithNewProfile = true;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (wasInitialized) ResetForNewGame();
    }

    /// <summary>Returns to the title menu without modifying save data.</summary>
    public void ReturnToMainMenu()
    {
        if (isConfirming) return;
        if (frontEnd == null)
        {
            Debug.LogError("MainMenuController reference is missing.", this);
            return;
        }

        frontEnd.ShowMainMenu();
        gameObject.SetActive(false);
    }

    public void Initialize()
    {
        if (initialized) return;
        if (view == null || !view.IsConfigured)
        {
            Debug.LogError("CharacterCreationView is missing or has incomplete references.", this);
            enabled = false;
            return;
        }

        database = dataCatalog != null ? dataCatalog.CharacterAppearances : null;
        nameDatabase = dataCatalog != null ? dataCatalog.CharacterNames : null;
        if (database == null)
        {
            Debug.LogError("GameDataCatalog or its CharacterAppearanceDatabase is missing.", this);
            enabled = false;
            return;
        }
        if (nameDatabase == null)
            Debug.LogWarning("CharacterNameDatabase is missing. English fallback name 'Player' will be used.", this);

        profile = startWithNewProfile ? new CharacterCreationProfile() : CharacterCreationSave.Load();
        if (string.IsNullOrWhiteSpace(profile.playerName)) profile.playerName = GenerateRandomName();
        view.NameInput.text = profile.playerName;

        BuildRows();
        BindEvents();
        SetGender(profile.gender);
        initialized = true;
    }

    private void BindEvents()
    {
        view.MaleButton.onClick.AddListener(SetMale);
        view.FemaleButton.onClick.AddListener(SetFemale);
        view.RandomNameButton.onClick.AddListener(RandomizeName);
        view.StartGameButton.onClick.AddListener(Confirm);
        view.ReturnButton.onClick.AddListener(ReturnToMainMenu);
        nameChangedAction = value => profile.playerName = value.Trim();
        view.NameInput.onValueChanged.AddListener(nameChangedAction);

        foreach (Row row in rows.Values)
        {
            row.PreviousAction = () => Select(row, -1);
            row.NextAction = () => Select(row, 1);
            row.Previous.onClick.AddListener(row.PreviousAction);
            row.Next.onClick.AddListener(row.NextAction);
        }
        eventsBound = true;
    }

    private void BuildRows()
    {
        rows.Clear();
        IReadOnlyList<CharacterCreationView.AppearanceRow> configuredRows = view.AppearanceRows;
        for (int i = 0; i < configuredRows.Count; i++)
        {
            CharacterCreationView.AppearanceRow configured = configuredRows[i];
            rows.Add(configured.Category, new Row
            {
                Category = configured.Category,
                Number = configured.NumberLabel,
                Previous = configured.PreviousButton,
                Next = configured.NextButton
            });
        }
    }

    public void SetMale() => SetGender(PlayerGender.Male);
    public void SetFemale() => SetGender(PlayerGender.Female);

    public void RandomizeName()
    {
        string value = GenerateRandomName();
        if (string.IsNullOrEmpty(value)) return;
        profile.playerName = value;
        view.NameInput.text = value;
    }

    public void RandomizeAppearance()
    {
        foreach (Row row in rows.Values)
        {
            if (row.Options.Count == 0) continue;
            row.Index = UnityEngine.Random.Range(0, row.Options.Count);
            Select(row, 0);
        }
    }

    private void ResetForNewGame()
    {
        profile = new CharacterCreationProfile { playerName = GenerateRandomName() };
        view.NameInput.text = profile.playerName;
        SetGender(profile.gender);
        isConfirming = false;
        view.StartGameButton.interactable = true;

        CanvasGroup panelGroup = GetComponent<CanvasGroup>();
        if (panelGroup == null) return;
        panelGroup.alpha = 1f;
        panelGroup.interactable = true;
        panelGroup.blocksRaycasts = true;
    }

    private string GenerateRandomName()
    {
        string generatedName = CharacterNameGenerator.GetRandomName(nameDatabase, "en");
        return string.IsNullOrEmpty(generatedName) ? "Player" : generatedName;
    }

    public void Confirm()
    {
        if (isConfirming || profile == null) return;
        isConfirming = true;
        view.StartGameButton.interactable = false;

        profile.playerName = view.NameInput.text.Trim();
        if (string.IsNullOrWhiteSpace(profile.playerName)) RandomizeName();
        StartCoroutine(CompleteCharacterCreation());
    }

    private IEnumerator CompleteCharacterCreation()
    {
        onConfirmed?.Invoke();
        yield return null;
        if (GameRoot.Instance == null)
        {
            Debug.LogError("GameRoot is missing; cannot start a new game.", this);
            isConfirming = false;
            view.StartGameButton.interactable = true;
            yield break;
        }
        GameRoot.Instance.Flow.StartNewGame(profile);
    }

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

    private PlayerPartAnimationSet GetAnimation(PlayerAppearanceCategory category)
    {
        CharacterAppearanceDatabase.Entry entry = database.Find(profile.GetPartId(category));
        return entry != null ? entry.AnimationSet : null;
    }

    private void SetGender(PlayerGender gender)
    {
        profile.gender = gender;
        view.MaleBackground.color = gender == PlayerGender.Male ? selectedGenderColor : normalGenderColor;
        view.FemaleBackground.color = gender == PlayerGender.Female ? selectedGenderColor : normalGenderColor;

        foreach (Row row in rows.Values)
        {
            string selectedId = profile.GetPartId(row.Category);
            database.GetEntries(row.Category, gender, row.Options);
            row.Index = FindIndex(row.Options, selectedId);
            if (row.Index < 0) row.Index = 0;
            Select(row, 0);
        }
    }

    private void Select(Row row, int delta)
    {
        if (row.Options.Count == 0)
        {
            profile.SetPartId(row.Category, string.Empty);
            row.Number.text = "0";
            UpdatePreview(row.Category, null);
            return;
        }

        row.Index = (row.Index + delta + row.Options.Count) % row.Options.Count;
        CharacterAppearanceDatabase.Entry entry = row.Options[row.Index];
        profile.SetPartId(row.Category, entry.Id);
        row.Number.text = (row.Index + 1).ToString();
        UpdatePreview(row.Category, entry.AnimationSet);
    }

    private void UpdatePreview(PlayerAppearanceCategory category, PlayerPartAnimationSet set)
    {
        Image previewTemplate = view.PreviewTemplate;
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

    private static int FindIndex(List<CharacterAppearanceDatabase.Entry> options, string id)
    {
        for (int i = 0; i < options.Count; i++)
            if (options[i].Id == id) return i;
        return -1;
    }
}
