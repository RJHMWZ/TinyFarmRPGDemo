/// <summary>
/// Compatibility facade for the original character-only save API.
/// New code should use GameRoot.Instance.Saves and GameSession directly.
/// </summary>
public static class CharacterCreationSave
{
    private static readonly SaveService Fallback = new SaveService();
    private static int fallbackSlot;

    public const int SlotCount = SaveService.SlotCount;
    public static int ActiveSlot => GameRoot.Instance != null ? GameRoot.Instance.Session.ActiveSlot : fallbackSlot;
    private static SaveService Saves => GameRoot.Instance != null ? GameRoot.Instance.Saves : Fallback;

    public static void SetActiveSlot(int slotIndex)
    {
        fallbackSlot = UnityEngine.Mathf.Clamp(slotIndex, 0, SlotCount - 1);
        if (GameRoot.Instance != null) GameRoot.Instance.Session.SelectSlot(fallbackSlot);
    }

    public static void Save(CharacterCreationProfile profile)
    {
        GameSaveData data;
        if (!Saves.TryLoad(ActiveSlot, out data)) data = GameSaveData.CreateNew(profile);
        data.player.appearance = profile ?? new CharacterCreationProfile();
        Saves.Save(ActiveSlot, data);
    }

    public static CharacterCreationProfile Load()
    {
        return TryLoadProfile(ActiveSlot, out CharacterCreationProfile profile)
            ? profile
            : new CharacterCreationProfile();
    }

    public static bool TryLoadProfile(out CharacterCreationProfile profile) => TryLoadProfile(ActiveSlot, out profile);

    public static bool TryLoadProfile(int slotIndex, out CharacterCreationProfile profile)
    {
        if (Saves.TryLoad(slotIndex, out GameSaveData data))
        {
            profile = data.player.appearance;
            return profile != null;
        }
        profile = null;
        return false;
    }

    public static bool HasSave(int slotIndex) => Saves.HasSave(slotIndex);
    public static bool DeleteSlot(int slotIndex) => Saves.Delete(slotIndex);
}
