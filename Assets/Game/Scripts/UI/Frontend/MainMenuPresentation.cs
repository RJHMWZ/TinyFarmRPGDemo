using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Adds the authored pixel-art backdrop and cozy menu presentation at runtime.</summary>
[DisallowMultipleComponent]
public sealed class MainMenuPresentation : MonoBehaviour
{
    private Button settingsButton;
    private Canvas canvas;
    private bool initialized;

    public void Initialize(MainMenuView view)
    {
        if (initialized || view == null) return;
        canvas = view.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("Main menu presentation requires a parent Canvas.", this);
            return;
        }

        initialized = true;
        BuildBackground();
        StyleAndPlaceButtons(view);
        settingsButton = view.SettingsButton;
        settingsButton.interactable = true;
        settingsButton.onClick.AddListener(OpenSettings);
    }

    private void BuildBackground()
    {
        RectTransform backdrop = CozyUi.Rect(canvas.transform, "MainMenuBackdrop", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        RawImage image = backdrop.gameObject.AddComponent<RawImage>();
        image.texture = Resources.Load<Texture2D>("UI/MainMenuBackground");
        image.color = Color.white;
        image.raycastTarget = false;
        backdrop.SetAsFirstSibling();
        if (image.texture == null) Debug.LogWarning("Main menu background texture was not found in Resources/UI.", this);

        RectTransform shade = CozyUi.Rect(canvas.transform, "MenuShade", new Vector2(0.68f, 0f), Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image shadeImage = shade.gameObject.AddComponent<Image>();
        shadeImage.color = new Color(0.02f, 0.07f, 0.09f, 0.48f);
        shadeImage.raycastTarget = false;
        shade.SetSiblingIndex(1);

        RectTransform titlePlate = CozyUi.Rect(canvas.transform, "TitlePlate", new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-210f, -56f), new Vector2(880f, 170f));
        Image plateImage = titlePlate.gameObject.AddComponent<Image>();
        plateImage.color = new Color(0.12f, 0.22f, 0.16f, 0.64f);
        plateImage.raycastTarget = false;
        Outline plateOutline = titlePlate.gameObject.AddComponent<Outline>();
        plateOutline.effectColor = new Color32(246, 205, 105, 220);
        plateOutline.effectDistance = new Vector2(3f, -3f);
        titlePlate.SetSiblingIndex(2);
        TMP_Text title = CozyUi.Text(titlePlate, "Title", "TINY FARM", 74f, TextAlignmentOptions.Center,
            CozyUi.Cream, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 15f),
            new Vector2(-30f, -35f));
        title.fontStyle = FontStyles.Bold;
        CozyUi.Text(titlePlate, "Subtitle", "A COZY FARMING ADVENTURE", 22f, TextAlignmentOptions.Center,
            new Color32(245, 203, 107, 255), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 19f), new Vector2(700f, 38f));

        TMP_Text version = CozyUi.Text(canvas.transform, "Version", "Prototype 0.2", 18f,
            TextAlignmentOptions.BottomLeft, new Color(1f, 1f, 1f, 0.72f), Vector2.zero, Vector2.zero,
            Vector2.zero, new Vector2(20f, 16f), new Vector2(240f, 32f));
        version.transform.SetSiblingIndex(3);
    }

    private static void StyleAndPlaceButtons(MainMenuView view)
    {
        Button[] buttons =
        {
            view.CreateGameButton, view.LoadGameButton, view.SettingsButton, view.ExitButton
        };
        string[] labels = { "NEW GAME", "LOAD GAME", "SETTINGS", "EXIT GAME" };
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-92f, 155f - i * 105f);
            rect.sizeDelta = new Vector2(450f, 78f);
            if (button.image != null) button.image.color = CozyUi.WoodLight;
            Outline outline = button.GetComponent<Outline>();
            if (outline == null) outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(60, 38, 28, 255);
            outline.effectDistance = new Vector2(5f, -5f);
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = labels[i];
                text.fontSize = 31f;
                text.color = CozyUi.Cream;
                text.fontStyle = FontStyles.Bold;
            }
        }
    }

    private void OpenSettings()
    {
        GameRoot root = GameRoot.Instance;
        if (root != null && !root.UI.IsOpen(SettingsPanel.PanelId)) root.UI.Open(SettingsPanel.PanelId);
    }

    private void OnDestroy()
    {
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OpenSettings);
    }
}
