using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UIPanel : MonoBehaviour
{
    public virtual void OnCreate() { }
    public virtual void OnOpen(object args) { }
    public virtual void OnFocus() { }
    public virtual void OnBlur() { }
    public virtual void OnClose() { }

    public void SetVisible(bool visible)
    {
        CanvasGroup group = GetComponent<CanvasGroup>();
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
        gameObject.SetActive(visible);
    }
}
