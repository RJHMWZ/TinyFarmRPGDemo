using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Controls the title menu and builds a lightweight save-selection panel from existing UI assets.</summary>
public sealed class MainMenuController : MonoBehaviour
{
    private enum SavePanelMode { Create, Load }

    private Button createGameButton;
    private Button readSaveButton;
    private Button exitButton;
    private CharacterCreationController characterCreation;
    private PlayerAppearance playerAppearance;
    private PlayerMovement playerMovement;
    private bool playerMovementWasEnabled;
    private GameObject savePanel;
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

    private void Awake()
    {
        createGameButton = FindComponent<Button>(transform, "CreateGameBtn");
        readSaveButton = FindComponent<Button>(transform, "ReadarchivesBtn");
        exitButton = FindComponent<Button>(transform, "ExitBtn");

        Transform creationTransform = transform.parent != null
            ? transform.parent.Find("CharacterCreation")
            : null;
        if (creationTransform != null)
        {
            characterCreation = creationTransform.GetComponent<CharacterCreationController>();
            creationTransform.gameObject.SetActive(false);
        }

        playerAppearance = FindObjectOfType<PlayerAppearance>();
        if (playerAppearance != null) playerMovement = playerAppearance.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovementWasEnabled = playerMovement.enabled;
            playerMovement.enabled = false;
        }

        Bind(createGameButton, CreateNewGame);
        Bind(readSaveButton, OpenSavePanel);
        Bind(exitButton, ExitGame);
    }

    private void OnDestroy()
    {
        if (createGameButton != null) createGameButton.onClick.RemoveListener(CreateNewGame);
        if (readSaveButton != null) readSaveButton.onClick.RemoveListener(OpenSavePanel);
        if (exitButton != null) exitButton.onClick.RemoveListener(ExitGame);
    }

    public void CreateNewGame()
    {
        if (characterCreation == null)
        {
            Debug.LogError("CharacterCreation panel/controller was not found.", this);
            return;
        }

        OpenSavePanel(SavePanelMode.Create);
    }

    public void ShowMainMenu()
    {
        gameObject.SetActive(true);
        CloseSavePanel();
        if (playerMovement != null) playerMovement.enabled = false;
    }

    public void OpenSavePanel()
    {
        OpenSavePanel(SavePanelMode.Load);
    }

    private void OpenSavePanel(SavePanelMode mode)
    {
        if (savePanel == null) CacheSceneSavePanel();
        if (savePanel == null)
        {
            Debug.LogError("SavePanel is missing from the Canvas. Run Tools/Main Menu/Rebuild Save Panel.", this);
            return;
        }
        panelMode = mode;
        selectedSlot = -1;
        pendingDeleteSlot = -1;
        RefreshSavePanel();
        savePanel.SetActive(true);
        savePanel.transform.SetAsLastSibling();
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
                saveDescription.text += "\nThis slot already has data. Click Create again to overwrite it.";
                return;
            }

            CharacterCreationSave.SetActiveSlot(selectedSlot);
            RestorePlayerMovementState();
            CloseSavePanel();
            gameObject.SetActive(false);
            characterCreation.BeginNewGame();
            return;
        }

        LoadSelectedGame();
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

    private void LoadSelectedGame()
    {
        if (!CharacterCreationSave.TryLoadProfile(selectedSlot, out CharacterCreationProfile profile))
        {
            RefreshSavePanel();
            return;
        }

        CharacterCreationSave.SetActiveSlot(selectedSlot);

        GameDataCatalog catalog = characterCreation != null ? characterCreation.DataCatalog : null;
        if (!CharacterAppearanceService.Apply(profile, catalog, playerAppearance))
        {
            Debug.LogError("The saved character could not be applied. Check GameDataCatalog references.", this);
            return;
        }

        RestorePlayerMovementState();
        if (savePanel != null) Destroy(savePanel);
        Destroy(gameObject);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void CacheSceneSavePanel()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Transform panelTransform = FindDescendant(canvas.transform, "SavePanel");
        if (panelTransform == null) return;

        savePanel = panelTransform.gameObject;
        panelTitle = FindComponent<TMP_Text>(panelTransform, "PanelTitle");
        saveDescription = FindComponent<TMP_Text>(panelTransform, "SaveDescription");

        for (int i = 0; i < slotButtons.Length; i++)
        {
            int capturedSlot = i;
            slotButtons[i] = FindComponent<Button>(panelTransform, "SaveSlot" + (i + 1));
            if (slotButtons[i] != null) slotButtons[i].onClick.AddListener(() => SelectSlot(capturedSlot));
        }

        actionButton = FindComponent<Button>(panelTransform, "PrimaryActionBtn");
        deleteButton = FindComponent<Button>(panelTransform, "DeleteSaveBtn");
        Button backButton = FindComponent<Button>(panelTransform, "BackBtn");
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
            if (label != null)
                label.text = hasSave
                    ? "Slot " + (i + 1) + "  -  " + DisplayName(slotProfile)
                    : "Slot " + (i + 1) + "  -  Empty";
            if (slotButtons[i] != null)
                slotButtons[i].image.color = i == selectedSlot ? new Color(1f, 0.82f, 0.42f, 1f) : Color.white;
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

    private static string DisplayName(CharacterCreationProfile profile)
    {
        return profile == null || string.IsNullOrWhiteSpace(profile.playerName) ? "Unnamed" : profile.playerName;
    }

    private void RestorePlayerMovementState()
    {
        if (playerMovement != null) playerMovement.enabled = playerMovementWasEnabled;
    }

    private static void Bind(Button button, UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }

    private static T FindComponent<T>(Transform root, string objectName) where T : Component
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            if (children[i].name == objectName) return children[i].GetComponent<T>();
        return null;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            if (children[i].name == objectName) return children[i];
        return null;
    }
}
