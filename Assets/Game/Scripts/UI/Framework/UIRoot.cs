using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UIRoot : MonoBehaviour
{
    private static readonly List<UIRoot> ActiveRoots = new List<UIRoot>();

    public static UIRoot Active { get; private set; }
    public static event Action<UIRoot> ActiveChanged;

    [SerializeField] private RectTransform hudLayer;
    [SerializeField] private RectTransform screenLayer;
    [SerializeField] private RectTransform windowLayer;
    [SerializeField] private RectTransform popupLayer;
    [SerializeField] private RectTransform toastLayer;
    [SerializeField] private RectTransform transitionLayer;

    public bool IsConfigured => hudLayer != null && screenLayer != null && windowLayer != null &&
                                popupLayer != null && toastLayer != null && transitionLayer != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ActiveRoots.Clear();
        Active = null;
        ActiveChanged = null;
    }

    private void OnEnable()
    {
        if (Active != null && Active != this)
        {
            Debug.LogError("More than one active UIRoot exists.", this);
        }
        ActiveRoots.Remove(this);
        ActiveRoots.Add(this);
        SetActive(this);
    }

    private void OnDisable()
    {
        ActiveRoots.Remove(this);
        if (Active == this)
            SetActive(ActiveRoots.Count > 0 ? ActiveRoots[ActiveRoots.Count - 1] : null);
    }

    private static void SetActive(UIRoot value)
    {
        if (Active == value) return;
        Active = value;
        ActiveChanged?.Invoke(value);
    }

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

}
