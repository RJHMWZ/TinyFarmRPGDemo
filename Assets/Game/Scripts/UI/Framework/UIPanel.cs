using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
[DisallowMultipleComponent]
public class UIPanel : MonoBehaviour
{
    private CanvasGroup canvasGroup;

    public virtual void OnCreate() { }
    public virtual void OnOpen(object args) { }
    public virtual void OnFocus() { }
    public virtual void OnBlur() { }
    public virtual void OnClose() { }

    public void SetVisible(bool visible)
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
        gameObject.SetActive(visible);
    }
}
