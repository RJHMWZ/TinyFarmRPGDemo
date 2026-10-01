using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Coordinates frontend screens. Scene loading and save IO remain in services.</summary>
public sealed class MainMenuController : MonoBehaviour
{
    private enum SavePanelMode { Create, Load }

    [Header("Frontend screens")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject savePanel;
    [SerializeField] private CharacterCreationController characterCreation;

    private readonly UnityAction[] slotActions = new UnityAction[CharacterCreationSave.SlotCount];
    private MainMenuView menuView;
    private SaveSelectView saveView;
    private bool bindingsRegistered;
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
        if (!ResolveViews())
        {
            enabled = false;
            return;
        }

        Bind(menuView.CreateGameButton, CreateNewGame);
        Bind(menuView.LoadGameButton, OpenSavePanel);
        Bind(menuView.ExitButton, ExitGame);
        BindSavePanel();
        bindingsRegistered = true;

        if (characterCreation != null)
        {
            characterCreation.Configure(this);
            characterCreation.gameObject.SetActive(false);
        }
        ShowMainMenu();
    }

    private void OnDestroy()
    {
        if (!bindingsRegistered) return;

        if (menuView != null)
        {
            Unbind(menuView.CreateGameButton, CreateNewGame);
            Unbind(menuView.LoadGameButton, OpenSavePanel);
            Unbind(menuView.ExitButton, ExitGame);
        }

        if (saveView == null) return;
        Unbind(saveView.PrimaryActionButton, ConfirmSelectedSlot);
        Unbind(saveView.DeleteButton, DeleteSelectedSlot);
        Unbind(saveView.BackButton, CloseSavePanel);
        for (int i = 0; i < slotActions.Length; i++)
            Unbind(saveView.GetSlotButton(i), slotActions[i]);
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
                saveView.Description.text += "\nThis slot already has data. Click Create again to overwrite it.";
                return;
            }

            GameRoot.Instance.Flow.SelectNewGameSlot(selectedSlot);
            CloseSavePanel();
            menuPanel.SetActive(false);
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
            saveView.DeleteButtonLabel.text = "Confirm Delete";
            saveView.Description.text += "\nClick Confirm Delete to permanently remove this save.";
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

    private bool ResolveViews()
    {
        if (menuPanel == null || savePanel == null || characterCreation == null)
        {
            Debug.LogError("MainMenuController screen references are incomplete.", this);
            return false;
        }

        menuView = menuPanel.GetComponent<MainMenuView>();
        saveView = savePanel.GetComponent<SaveSelectView>();
        if (menuView == null || !menuView.IsConfigured)
        {
            Debug.LogError("MainMenuView is missing or has incomplete references.", menuPanel);
            return false;
        }
        if (saveView == null || !saveView.IsConfigured)
        {
            Debug.LogError("SaveSelectView is missing or has incomplete references.", savePanel);
            return false;
        }
        return true;
    }

    private void BindSavePanel()
    {
        for (int i = 0; i < slotActions.Length; i++)
        {
            int capturedSlot = i;
            slotActions[i] = () => SelectSlot(capturedSlot);
            Bind(saveView.GetSlotButton(i), slotActions[i]);
        }
        Bind(saveView.PrimaryActionButton, ConfirmSelectedSlot);
        Bind(saveView.DeleteButton, DeleteSelectedSlot);
        Bind(saveView.BackButton, CloseSavePanel);
        savePanel.SetActive(false);
    }

    private void OpenSavePanel(SavePanelMode mode)
    {
        panelMode = mode;
        selectedSlot = -1;
        pendingDeleteSlot = -1;
        RefreshSavePanel();
        savePanel.SetActive(true);
        savePanel.transform.SetAsLastSibling();
    }

    private void RefreshSavePanel()
    {
        bool loading = panelMode == SavePanelMode.Load;
        saveView.PanelTitle.text = loading ? "Choose a Save to Load" : "Choose a Slot for New Game";
        saveView.PrimaryActionLabel.text = loading ? "Load" : "Create";
        saveView.DeleteButtonLabel.text = "Delete";

        for (int i = 0; i < saveView.SlotCount; i++)
        {
            bool hasSave = CharacterCreationSave.TryLoadProfile(i, out CharacterCreationProfile slotProfile);
            saveView.GetSlotLabel(i).text = hasSave
                ? "Slot " + (i + 1) + "  -  " + DisplayName(slotProfile)
                : "Slot " + (i + 1) + "  -  Empty";

            Button button = saveView.GetSlotButton(i);
            if (button.image != null)
                button.image.color = i == selectedSlot ? new Color(1f, 0.82f, 0.42f, 1f) : Color.white;
        }

        bool selected = selectedSlot >= 0;
        CharacterCreationProfile profile = null;
        bool selectedHasSave = selected && CharacterCreationSave.TryLoadProfile(selectedSlot, out profile);
        saveView.Description.text = !selected
            ? "Select one of the four save slots.\nSelecting a slot will not start the game."
            : selectedHasSave
                ? "Slot " + (selectedSlot + 1) + "\nCharacter: " + DisplayName(profile) + "\nSave data is available."
                : "Slot " + (selectedSlot + 1) + "\nEmpty slot";
        saveView.PrimaryActionButton.interactable = selected && (!loading || selectedHasSave);
        saveView.DeleteButton.interactable = selectedHasSave;
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

    private static void Bind(Button button, UnityAction action)
    {
        if (button != null && action != null) button.onClick.AddListener(action);
    }

    private static void Unbind(Button button, UnityAction action)
    {
        if (button != null && action != null) button.onClick.RemoveListener(action);
    }
}
