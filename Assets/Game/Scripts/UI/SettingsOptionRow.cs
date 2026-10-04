using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Serialized view for a slider or a pair of step buttons.</summary>
public sealed class SettingsOptionRow : MonoBehaviour
{
    [SerializeField] private TMP_Text valueLabel;
    [SerializeField] private Button previous;
    [SerializeField] private Button next;
    [SerializeField] private Slider slider;

    public void Bind(Action<int> step, Action<float> slide)
    {
        previous.onClick.RemoveAllListeners();
        next.onClick.RemoveAllListeners();
        slider.onValueChanged.RemoveAllListeners();
        if (step != null)
        {
            previous.onClick.AddListener(() => step(-1));
            next.onClick.AddListener(() => step(1));
        }
        if (slide != null) slider.onValueChanged.AddListener(value => slide(value));
    }

    public void Refresh(string text, float value = 0f, bool interactable = true)
    {
        valueLabel.text = text;
        slider.SetValueWithoutNotify(value);
        previous.interactable = next.interactable = slider.interactable = interactable;
    }
}
