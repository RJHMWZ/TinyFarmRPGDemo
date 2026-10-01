using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordinates pause requests so one system cannot accidentally resume gameplay
/// while another system still requires it to remain paused.
/// </summary>
public sealed class PauseService : MonoBehaviour
{
    private readonly HashSet<object> owners = new HashSet<object>();
    private float resumeTimeScale = 1f;

    public bool IsPaused => owners.Count > 0;
    public event Action<bool> PauseChanged;

    public void SetPaused(object owner, bool paused)
    {
        if (owner == null)
        {
            Debug.LogError("A pause request requires a non-null owner.", this);
            return;
        }

        bool wasPaused = IsPaused;
        bool changed;
        if (paused)
        {
            if (!wasPaused && Time.timeScale > 0f) resumeTimeScale = Time.timeScale;
            changed = owners.Add(owner);
        }
        else
        {
            changed = owners.Remove(owner);
        }

        if (!changed) return;

        bool isPaused = IsPaused;
        Time.timeScale = isPaused ? 0f : resumeTimeScale;
        if (wasPaused != isPaused) PauseChanged?.Invoke(isPaused);
    }

    private void OnDisable()
    {
        if (owners.Count > 0) Time.timeScale = resumeTimeScale;
        owners.Clear();
    }
}
