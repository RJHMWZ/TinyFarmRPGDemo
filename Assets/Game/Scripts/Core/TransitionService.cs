using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Persistent full-screen transition layer shared by all scene changes.</summary>
public sealed class TransitionService : MonoBehaviour
{
    [Min(0.01f)] [SerializeField] private float defaultDuration = 0.45f;
    private CanvasGroup group;

    private void Awake()
    {
        BuildOverlay();
        SetAlpha(1f);
    }

    public IEnumerator FadeToBlack(float duration = -1f) => FadeTo(1f, duration);
    public IEnumerator FadeFromBlack(float duration = -1f) => FadeTo(0f, duration);

    public IEnumerator FadeTo(float target, float duration = -1f)
    {
        BuildOverlay();
        float start = group.alpha;
        float seconds = duration > 0f ? duration : defaultDuration;
        float elapsed = 0f;
        group.blocksRaycasts = true;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / seconds)));
            yield return null;
        }
        SetAlpha(target);
        group.blocksRaycasts = target > 0.001f;
    }

    private void BuildOverlay()
    {
        if (group != null) return;
        GameObject canvasObject = new GameObject("TransitionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;
        group = canvasObject.GetComponent<CanvasGroup>();

        GameObject imageObject = new GameObject("Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = (RectTransform)imageObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = imageObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
    }

    private void SetAlpha(float alpha)
    {
        group.alpha = Mathf.Clamp01(alpha);
        group.interactable = group.blocksRaycasts = group.alpha > 0.001f;
    }
}
