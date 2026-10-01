using UnityEngine;

/// <summary>Applies the loaded session to scene-owned gameplay objects.</summary>
public sealed class GameplayEntryPoint : MonoBehaviour
{
    [SerializeField] private GameDataCatalog dataCatalog;
    [SerializeField] private PlayerAppearance playerAppearance;

    public void Configure(GameDataCatalog catalog, PlayerAppearance appearance)
    {
        dataCatalog = catalog;
        playerAppearance = appearance;
    }

    private void Start()
    {
        GameRoot root = GameRoot.Instance;
        GameSaveData save = root != null ? root.Session.CurrentSave : null;
        if (save == null && root != null && root.Saves.TryLoad(root.Session.ActiveSlot, out GameSaveData loaded))
        {
            save = loaded;
            root.Session.SetLoadedGame(root.Session.ActiveSlot, loaded);
        }
        if (save == null) return;

        CharacterAppearanceService.Apply(save.player.appearance, dataCatalog, playerAppearance);
        if (playerAppearance != null)
            playerAppearance.transform.position = new Vector3(save.player.positionX, save.player.positionY, playerAppearance.transform.position.z);
    }
}
