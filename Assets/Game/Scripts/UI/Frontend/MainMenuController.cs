using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Coordinates front-end screens. Scene loading and save IO remain in services.</summary>
public sealed class MainMenuController : MonoBehaviour
{
    private enum SavePanelMode { Create, Load }

    [Header("Front-end screens")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject savePanel;
    [SerializeField] private CharacterCreationController characterCreation;

    private Button createGameButton;
    private Button readSaveButton;
    private Button exitButton;
    private TMP_Text panelTitle;
    private TMP_Text saveDescription;
    private TMP_Text actionButtonLabel;
    private TMP_Text deleteButtonLabel;
    private Button actionButton;
    private Button deleteButton;
    private readonly Button[] slotButtons = new Button[CharacterCreationSave.SlotCount];
    private SavePanelMode panelMode;
    private int selectedSlot = -1;
    private int pendingDeleteSlot = -1;

    public void Configure(GameObject menu, GameObject saves, CharacterCreationController creation)
    {
        menuPanel = menu;
        savePanel = saves;
        characterCreation = creation;
        if (characterCreation != null) characterCreation.Configure(this);
    }

    private void Awake()
    {
        if (menuPanel == null) menuPanel = gameObject;
        if (savePanel == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform found = canvas != null ? FindDescendant(canvas.transform, "SavePanel") : null;
            if (found != null) savePanel = found.gameObject;
        }
        if (characterCreation == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform found = canvas != null ? FindDescendant(canvas.transform, "CharacterCreation") : null;
            if (found != null) characterCreation = found.GetComponent<CharacterCreationController>();
        }

        createGameButton = FindComponent<Button>(menuPanel.transform, "CreateGameBtn");
        readSaveButton = FindComponent<Button>(menuPanel.transform, "ReadarchivesBtn");
        exitButton = FindComponent<Button>(menuPanel.transform, "ExitBtn") ?? FindComponent<Button>(menuPanel.transform, "EixtBtn");
        Bind(createGameButton, CreateNewGame);
        Bind(readSaveButton, OpenSavePanel);
        Bind(exitButton, ExitGame);
        CacheSavePanel();
        if (characterCreation != null)
        {
            characterCreation.Configure(this);
            characterCreation.gameObject.SetActive(false);
        }
        ShowMainMenu();
    }

    private void OnDestroy()
    {
        if (createGameButton != null) createGameButton.onClick.RemoveListener(CreateNewGame);
        if (readSaveButton != null) readSaveButton.onClick.RemoveListener(OpenSavePanel);
        if (exitButton != null) exitButton.onClick.RemoveListener(ExitGame);
    }

    public void CreateNewGame() => OpenSavePanel(SavePanelMode.Create);
    public void OpenSavePanel() => OpenSavePanel(SavePanelMode.Load);

    public void ShowMainMenu()
    {
        if (menuPanel != null) menuPanel.SetActive(true);
        if (characterCreation != null) characterCreation.gameObject.SetActive(false);
        CloseSavePanel();
    }

    public void CloseSavePanel()
    {
        if (savePanel != null) savePanel.SetActive(false);
    }

    public void ConfirmSelectedSlot()
    {
        if (selectedSlot < 0) return;
        if (panelMode == SavePanelMode.Create)
        {
            if (CharacterCreationSave.HasSave(selectedSlot) && pendingDeleteSlot != -2)
            {
                pendingDeleteSlot = -2;
                if (saveDescription != null) saveDescription.text += "\nThis slot already has data. Click Create again to overwrite it.";
                return;
            }
            GameRoot.Instance.Flow.SelectNewGameSlot(selectedSlot);
            CloseSavePanel();
            if (menuPanel != null) menuPanel.SetActive(false);
            if (characterCreation != null) characterCreation.BeginNewGame();
            return;
        }
        GameRoot.Instance.Flow.LoadGame(selectedSlot);
    }

    public void DeleteSelectedSlot()
    {
        if (selectedSlot < 0 || !CharacterCreationSave.HasSave(selectedSlot)) return;
        if (pendingDeleteSlot != selectedSlot)
        {
            pendingDeleteSlot = selectedSlot;
            if (deleteButtonLabel != null) deleteButtonLabel.text = "Confirm Delete";
            if (saveDescription != null) saveDescription.text += "\nClick Confirm Delete to permanently remove this save.";
            return;
        }
        CharacterCreationSave.DeleteSlot(selectedSlot);
        pendingDeleteSlot = -1;
        RefreshSavePanel();
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OpenSavePanel(SavePanelMode mode)
    {
        if (savePanel == null)
        {
            Debug.LogError("SavePanel reference is missing.", this);
            return;
        }
        panelMode = mode;
        selectedSlot = -1;
        pendingDeleteSlot = -1;
        RefreshSavePanel();
        savePanel.SetActive(true);
        savePanel.transform.SetAsLastSibling();
    }

    private void CacheSavePanel()
    {
        if (savePanel == null) return;
        panelTitle = FindComponent<TMP_Text>(savePanel.transform, "PanelTitle");
        saveDescription = FindComponent<TMP_Text>(savePanel.transform, "SaveDescription");
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int capturedSlot = i;
            slotButtons[i] = FindComponent<Button>(savePanel.transform, "SaveSlot" + (i + 1));
            if (slotButtons[i] != null) slotButtons[i].onClick.AddListener(() => SelectSlot(capturedSlot));
        }
        actionButton = FindComponent<Button>(savePanel.transform, "PrimaryActionBtn");
        deleteButton = FindComponent<Button>(savePanel.transform, "DeleteSaveBtn");
        Button backButton = FindComponent<Button>(savePanel.transform, "BackBtn");
        if (actionButton != null) actionButton.onClick.AddListener(ConfirmSelectedSlot);
        if (deleteButton != null) deleteButton.onClick.AddListener(DeleteSelectedSlot);
        if (backButton != null) backButton.onClick.AddListener(CloseSavePanel);
        actionButtonLabel = actionButton != null ? actionButton.GetComponentInChildren<TMP_Text>(true) : null;
        deleteButtonLabel = deleteButton != null ? deleteButton.GetComponentInChildren<TMP_Text>(true) : null;
        savePanel.SetActive(false);
    }

    private void RefreshSavePanel()
    {
        if (panelTitle != null) panelTitle.text = panelMode == SavePanelMode.Load ? "Choose a Save to Load" : "Choose a Slot for New Game";
        if (actionButtonLabel != null) actionButtonLabel.text = panelMode == SavePanelMode.Load ? "Load" : "Create";
        if (deleteButtonLabel != null) deleteButtonLabel.text = "Delete";
        for (int i = 0; i < slotButtons.Length; i++)
        {
            bool hasSave = CharacterCreationSave.TryLoadProfile(i, out CharacterCreationProfile slotProfile);
            TMP_Text label = slotButtons[i] != null ? slotButtons[i].GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null) label.text = hasSave ? "Slot " + (i + 1) + "  -  " + DisplayName(slotProfile) : "Slot " + (i + 1) + "  -  Empty";
            if (slotButtons[i] != null) slotButtons[i].image.color = i == selectedSlot ? new Color(1f, 0.82f, 0.42f, 1f) : Color.white;
        }
        bool selected = selectedSlot >= 0;
        CharacterCreationProfile profile = null;
        bool selectedHasSave = selected && CharacterCreationSave.TryLoadProfile(selectedSlot, out profile);
        if (saveDescription != null)
            saveDescription.text = !selected
                ? "Select one of the four save slots.\nSelecting a slot will not start the game."
                : selectedHasSave
                    ? "Slot " + (selectedSlot + 1) + "\nCharacter: " + DisplayName(profile) + "\nSave data is available."
                    : "Slot " + (selectedSlot + 1) + "\nEmpty slot";
        if (actionButton != null) actionButton.interactable = selected && (panelMode == SavePanelMode.Create || selectedHasSave);
        if (deleteButton != null) deleteButton.interactable = selectedHasSave;
    }

    private void SelectSlot(int slotIndex)
    {
        selectedSlot = slotIndex;
        pendingDeleteSlot = -1;
        RefreshSavePanel();
    }

    private static string DisplayName(CharacterCreationProfile profile) => profile == null || string.IsNullOrWhiteSpace(profile.playerName) ? "Unnamed" : profile.playerName;

    private static void Bind(Button button, UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }

    private static T FindComponent<T>(Transform root, string objectName) where T : Component
    {
        Transform target = FindDescendant(root, objectName);
        return target != null ? target.GetComponent<T>() : null;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null) return null;
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++) if (children[i].name == objectName) return children[i];
        return null;
    }
}
