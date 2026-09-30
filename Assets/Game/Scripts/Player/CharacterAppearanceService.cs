/// <summary>Applies saved, ID-based appearance data to a scene character.</summary>
public static class CharacterAppearanceService
{
    public static bool Apply(
        CharacterCreationProfile profile,
        GameDataCatalog catalog,
        PlayerAppearance appearance)
    {
        if (profile == null || catalog == null || catalog.CharacterAppearances == null || appearance == null)
            return false;

        CharacterAppearanceDatabase database = catalog.CharacterAppearances;
        appearance.SetSkin(GetAnimation(database, profile.skinId));
        appearance.SetClothes(GetAnimation(database, profile.clothesId));
        appearance.SetEyes(GetAnimation(database, profile.eyesId));
        appearance.SetHair(GetAnimation(database, profile.hairId));
        appearance.SetAccessory(GetAnimation(database, profile.accessoryId));
        appearance.RefreshCurrentFrame();
        return true;
    }

    private static PlayerPartAnimationSet GetAnimation(CharacterAppearanceDatabase database, string id)
    {
        CharacterAppearanceDatabase.Entry entry = database.Find(id);
        return entry != null ? entry.AnimationSet : null;
    }
}
