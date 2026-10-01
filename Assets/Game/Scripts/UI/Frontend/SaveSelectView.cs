using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Serialized references owned by the save-selection prefab.</summary>
[DisallowMultipleComponent]
public sealed class SaveSelectView : MonoBehaviour
{
    [SerializeField] private TMP_Text panelTitle;
    [SerializeField] private TMP_Text description;
    [SerializeField] private Button primaryActionButton;
    [SerializeField] private TMP_Text primaryActionLabel;
    [SerializeField] private Button deleteButton;
    [SerializeField] private TMP_Text deleteButtonLabel;
    [SerializeField] private Button backButton;
    [SerializeField] private Button[] slotButtons;
    [SerializeField] private TMP_Text[] slotLabels;

    public TMP_Text PanelTitle => panelTitle;
    public TMP_Text Description => description;
    public Button PrimaryActionButton => primaryActionButton;
    public TMP_Text PrimaryActionLabel => primaryActionLabel;
    public Button DeleteButton => deleteButton;
    public TMP_Text DeleteButtonLabel => deleteButtonLabel;
    public Button BackButton => backButton;
    public int SlotCount => slotButtons != null ? slotButtons.Length : 0;

    public Button GetSlotButton(int index) => slotButtons[index];
    public TMP_Text GetSlotLabel(int index) => slotLabels[index];

    public bool IsConfigured
    {
        get
        {
            if (panelTitle == null || description == null || primaryActionButton == null ||
                primaryActionLabel == null || deleteButton == null || deleteButtonLabel == null ||
                backButton == null || slotButtons == null || slotLabels == null ||
                slotButtons.Length != CharacterCreationSave.SlotCount ||
                slotLabels.Length != CharacterCreationSave.SlotCount)
                return false;

            for (int i = 0; i < slotButtons.Length; i++)
                if (slotButtons[i] == null || slotLabels[i] == null) return false;
            return true;
        }
    }
}
