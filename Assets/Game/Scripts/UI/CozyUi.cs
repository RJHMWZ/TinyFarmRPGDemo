using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Small code-authored UI kit used by the prototype HUD and settings screens.</summary>
public static class CozyUi
{
    public static readonly Color Cream = new Color32(255, 241, 196, 255);
    public static readonly Color Ink = new Color32(72, 47, 32, 255);
    public static readonly Color Wood = new Color32(126, 76, 42, 255);
    public static readonly Color WoodLight = new Color32(185, 119, 60, 255);
    public static readonly Color Leaf = new Color32(76, 122, 67, 255);
    public static readonly Color Gold = new Color32(245, 190, 75, 255);
    public static readonly Color ModalDim = new Color(0.035f, 0.055f, 0.075f, 0.72f);

    public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static Image Panel(RectTransform rect, Color color, bool outline = true)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        if (outline)
        {
            Outline border = rect.gameObject.AddComponent<Outline>();
            border.effectColor = new Color32(60, 38, 28, 255);
            border.effectDistance = new Vector2(4f, -4f);
        }
        return image;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, string value, float size,
        TextAlignmentOptions alignment, Color color, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 position, Vector2 dimensions)
    {
        RectTransform rect = Rect(parent, name, anchorMin, anchorMax, pivot, position, dimensions);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    public static Button Button(Transform parent, string name, string label, Vector2 anchorMin,
        Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, Action clicked,
        out TextMeshProUGUI labelText)
    {
        RectTransform rect = Rect(parent, name, anchorMin, anchorMax, pivot, position, size);
        Image image = Panel(rect, WoodLight);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.9f, 0.62f, 1f);
        colors.pressedColor = new Color(0.78f, 0.64f, 0.42f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        if (clicked != null) button.onClick.AddListener(() => clicked());
        labelText = Text(rect, "Label", label, 27f, TextAlignmentOptions.Center, Cream,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-18f, -10f));
        return button;
    }

    public static GameObject ModalRoot(Transform parent, string name)
    {
        RectTransform root = Rect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        Image dim = root.gameObject.AddComponent<Image>();
        dim.color = ModalDim;
        return root.gameObject;
    }

    /// <summary>Keep keyboard/controller selection inside the active modal.</summary>
    public static void TrapNavigation(Transform root)
    {
        Selectable[] controls = root.GetComponentsInChildren<Selectable>();
        Canvas.ForceUpdateCanvases();
        foreach (Selectable source in controls)
        {
            var navigation = new Navigation { mode = Navigation.Mode.Explicit };
            Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
            Selectable[] neighbors = new Selectable[4];
            Vector3 origin = source.transform.TransformPoint(((RectTransform)source.transform).rect.center);
            for (int i = 0; i < 4; i++)
            {
                float best = 0;
                foreach (Selectable candidate in controls)
                {
                    if (candidate == source || !candidate.IsInteractable()) continue;
                    Vector2 offset = candidate.transform.TransformPoint(((RectTransform)candidate.transform).rect.center) - origin;
                    float dot = Vector2.Dot(directions[i], offset);
                    if (dot <= 0.01f) continue;
                    float score = dot / offset.sqrMagnitude;
                    if (score > best) { best = score; neighbors[i] = candidate; }
                }
            }
            navigation.selectOnUp = neighbors[0]; navigation.selectOnDown = neighbors[1];
            navigation.selectOnLeft = neighbors[2]; navigation.selectOnRight = neighbors[3];
            source.navigation = navigation;
        }
    }
}
