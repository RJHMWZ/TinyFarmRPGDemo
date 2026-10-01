using UnityEngine;

[DisallowMultipleComponent]
public sealed class UIRoot : MonoBehaviour
{
    [SerializeField] private RectTransform hudLayer;
    [SerializeField] private RectTransform screenLayer;
    [SerializeField] private RectTransform windowLayer;
    [SerializeField] private RectTransform popupLayer;
    [SerializeField] private RectTransform toastLayer;
    [SerializeField] private RectTransform transitionLayer;

    public RectTransform GetLayer(UILayer layer)
    {
        switch (layer)
        {
            case UILayer.Hud: return hudLayer;
            case UILayer.Screen: return screenLayer;
            case UILayer.Window: return windowLayer;
            case UILayer.Popup: return popupLayer;
            case UILayer.Toast: return toastLayer;
            case UILayer.Transition: return transitionLayer;
            default: return screenLayer;
        }
    }

    public void Configure(RectTransform hud, RectTransform screen, RectTransform window, RectTransform popup, RectTransform toast, RectTransform transition)
    {
        hudLayer = hud;
        screenLayer = screen;
        windowLayer = window;
        popupLayer = popup;
        toastLayer = toast;
        transitionLayer = transition;
    }
}
