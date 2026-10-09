using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Forwards non-primary slot clicks without placing inventory rules in the view helper.</summary>
public sealed class FarmSlotPointerRelay : MonoBehaviour, IPointerClickHandler
{
    private int index;
    private Action<int, PointerEventData.InputButton> clicked;

    public void Bind(int slotIndex, Action<int, PointerEventData.InputButton> callback)
    {
        index = slotIndex;
        clicked = callback;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            clicked?.Invoke(index, eventData.button);
    }
}
