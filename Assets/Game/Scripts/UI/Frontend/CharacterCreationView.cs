using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Serialized references owned by the character-creation prefab.</summary>
[DisallowMultipleComponent]
public sealed class CharacterCreationView : MonoBehaviour
{
    [Serializable]
    public sealed class AppearanceRow
    {
        [SerializeField] private PlayerAppearanceCategory category;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text numberLabel;

        public PlayerAppearanceCategory Category => category;
        public Button PreviousButton => previousButton;
        public Button NextButton => nextButton;
        public TMP_Text NumberLabel => numberLabel;
        public bool IsConfigured => previousButton != null && nextButton != null && numberLabel != null;
    }

    [Header("Identity")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button randomNameButton;

    [Header("Gender")]
    [SerializeField] private Image maleBackground;
    [SerializeField] private Image femaleBackground;
    [SerializeField] private Button maleButton;
    [SerializeField] private Button femaleButton;

    [Header("Appearance")]
    [SerializeField] private Image previewTemplate;
    [SerializeField] private Image[] previewLayers;
    [SerializeField] private AppearanceRow[] appearanceRows;

    [Header("Navigation")]
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button returnButton;

    public TMP_InputField NameInput => nameInput;
    public Button RandomNameButton => randomNameButton;
    public Image MaleBackground => maleBackground;
    public Image FemaleBackground => femaleBackground;
    public Button MaleButton => maleButton;
    public Button FemaleButton => femaleButton;
    public Image PreviewTemplate => previewTemplate;
    public Image GetPreviewLayer(PlayerAppearanceCategory category)
    {
        int index = (int)category;
        return previewLayers != null && index >= 0 && index < previewLayers.Length
            ? previewLayers[index]
            : null;
    }
    public IReadOnlyList<AppearanceRow> AppearanceRows => appearanceRows;
    public Button StartGameButton => startGameButton;
    public Button ReturnButton => returnButton;

    public bool IsConfigured
    {
        get
        {
            if (nameInput == null || randomNameButton == null || maleBackground == null ||
                femaleBackground == null || maleButton == null || femaleButton == null ||
                previewTemplate == null || previewLayers == null || previewLayers.Length < 4 ||
                startGameButton == null || returnButton == null ||
                appearanceRows == null)
                return false;

            for (int i = 0; i < 4; i++)
                if (previewLayers[i] == null) return false;

            var categories = new HashSet<PlayerAppearanceCategory>();
            for (int i = 0; i < appearanceRows.Length; i++)
            {
                AppearanceRow row = appearanceRows[i];
                if (row == null || !row.IsConfigured || !categories.Add(row.Category)) return false;
            }

            return categories.Contains(PlayerAppearanceCategory.Skin) &&
                   categories.Contains(PlayerAppearanceCategory.Clothes) &&
                   categories.Contains(PlayerAppearanceCategory.Eyes) &&
                   categories.Contains(PlayerAppearanceCategory.Hair);
        }
    }
}
