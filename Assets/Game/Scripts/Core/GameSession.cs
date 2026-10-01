using UnityEngine;

/// <summary>Runtime-only state for the selected save slot and loaded game.</summary>
public sealed class GameSession : MonoBehaviour
{
    public int ActiveSlot { get; private set; }
    public GameSaveData CurrentSave { get; private set; }
    public bool HasLoadedGame => CurrentSave != null;

    public void SelectSlot(int slotIndex)
    {
        ActiveSlot = Mathf.Clamp(slotIndex, 0, SaveService.SlotCount - 1);
    }

    public void SetLoadedGame(int slotIndex, GameSaveData data)
    {
        SelectSlot(slotIndex);
        CurrentSave = data;
        CurrentSave?.Normalize();
    }

    public void ClearLoadedGame()
    {
        CurrentSave = null;
    }
}
