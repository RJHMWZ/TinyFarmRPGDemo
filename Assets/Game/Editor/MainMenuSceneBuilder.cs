using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates an editable, scene-serialized save panel from the current main-menu visual style.</summary>
public static class MainMenuSceneBuilder
{
    [MenuItem("Tools/Main Menu/Rebuild Save Panel")]
    public static void RebuildSavePanel()
    {
        if (Application.isBatchMode && SceneManager.GetActiveScene().name != "GameScene")
            EditorSceneManager.OpenScene("Assets/Game/Scene/GameScene.unity", OpenSceneMode.Single);

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        GameObject menu = GameObject.Find("MenuPanel");
        Button template = menu != null ? Find<Button>(menu.transform, "ReadarchivesBtn") : null;
        if (canvas == null || template == null)
        {
            Debug.LogError("Canvas, MenuPanel, or ReadarchivesBtn was not found.");
            return;
        }

        Transform existing = FindTransform(canvas.transform, "SavePanel");
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

        GameObject panel = CreateImage("SavePanel", canvas.transform, new Color(0f, 0f, 0f, 0.78f));
        Stretch((RectTransform)panel.transform);

        GameObject window = CreateImage("SaveWindow", panel.transform, new Color(0.20f, 0.12f, 0.07f, 0.98f));
        RectTransform windowRect = (RectTransform)window.transform;
        windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(1180f, 700f);
        Image templateImage = template.GetComponent<Image>();
        if (templateImage != null)
        {
            Image windowImage = window.GetComponent<Image>();
            windowImage.sprite = templateImage.sprite;
            windowImage.type = Image.Type.Sliced;
        }

        TMP_Text textTemplate = template.GetComponentInChildren<TMP_Text>(true);
        CreateText("PanelTitle", window.transform, textTemplate, "Choose a Save",
            new Vector2(0f, 275f), new Vector2(850f, 80f), 42f);

        for (int i = 0; i < CharacterCreationSave.SlotCount; i++)
        {
            Button slot = CloneButton(template, window.transform, "SaveSlot" + (i + 1),
                "Slot " + (i + 1) + "  -  Empty", new Vector2(-350f, 155f - i * 115f));
            ((RectTransform)slot.transform).sizeDelta = new Vector2(380f, 92f);
        }

        CreateText("SaveDescription", window.transform, textTemplate,
            "Select one of the four save slots.", new Vector2(255f, 90f), new Vector2(480f, 260f), 27f);
        CloneButton(template, window.transform, "BackBtn", "Back", new Vector2(-245f, -225f));
        CloneButton(template, window.transform, "PrimaryActionBtn", "Load", new Vector2(40f, -225f));
        CloneButton(template, window.transform, "DeleteSaveBtn", "Delete", new Vector2(325f, -225f));

        panel.SetActive(false);
        Undo.RegisterCreatedObjectUndo(panel, "Create Save Panel");
        Selection.activeGameObject = panel;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (Application.isBatchMode) EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Editable SavePanel created under Canvas. Save the scene to keep it.", panel);
    }

    private static GameObject CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        result.layer = parent.gameObject.layer;
        result.transform.SetParent(parent, false);
        result.GetComponent<Image>().color = color;
        return result;
    }

    private static TMP_Text CreateText(string objectName, Transform parent, TMP_Text template, string value,
        Vector2 position, Vector2 size, float fontSize)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)textObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        if (template != null)
        {
            text.font = template.font;
            text.fontSharedMaterial = template.fontSharedMaterial;
            text.color = template.color;
        }
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        return text;
    }

    private static Button CloneButton(Button template, Transform parent, string objectName, string label, Vector2 position)
    {
        Button button = Object.Instantiate(template, parent, false);
        button.name = objectName;
        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(260f, 90f);
        button.onClick.RemoveAllListeners();
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;
        return button;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static T Find<T>(Transform root, string objectName) where T : Component
    {
        Transform target = FindTransform(root, objectName);
        return target != null ? target.GetComponent<T>() : null;
    }

    private static Transform FindTransform(Transform root, string objectName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            if (children[i].name == objectName) return children[i];
        return null;
    }
}
