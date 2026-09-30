using UnityEngine;

/// <summary>
/// Single runtime entry point for authored game data.
/// Scenes reference this catalog instead of depending on folders or loading APIs.
/// </summary>
[CreateAssetMenu(fileName = "GameDataCatalog", menuName = "Tiny Farm/Data/Game Data Catalog")]
public sealed class GameDataCatalog : ScriptableObject
{
    [Header("Character Creation")]
    [SerializeField] private CharacterAppearanceDatabase characterAppearances;
    [SerializeField] private CharacterNameDatabase characterNames;

    public CharacterAppearanceDatabase CharacterAppearances => characterAppearances;
    public CharacterNameDatabase CharacterNames => characterNames;
}
