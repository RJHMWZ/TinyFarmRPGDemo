using UnityEngine;

/// <summary>Applies the loaded session to scene-owned gameplay objects.</summary>
[DisallowMultipleComponent]
public sealed class GameplayEntryPoint : MonoBehaviour
{
    [SerializeField] private GameDataCatalog dataCatalog;
    [SerializeField] private PlayerAppearance playerAppearance;

    public bool IsConfigured => dataCatalog != null && playerAppearance != null && playerAppearance.IsConfigured;

    private void Start()
    {
        GameRoot root = GameRoot.Instance;
        if (root == null)
        {
            Debug.LogError("GameplayEntryPoint requires an active GameRoot.", this);
            return;
        }
        if (!IsConfigured)
        {
            Debug.LogError("GameplayEntryPoint references are incomplete.", this);
            return;
        }

        GameSaveData save = root.Session.CurrentSave;
        if (save == null && root.Saves.TryLoad(root.Session.ActiveSlot, out GameSaveData loaded))
        {
            save = loaded;
            root.Session.SetLoadedGame(root.Session.ActiveSlot, loaded);
        }
        if (save == null)
        {
            Debug.LogWarning("Gameplay started without a loaded save; player data was not applied.", this);
            return;
        }

        if (!CharacterAppearanceService.Apply(save.player.appearance, dataCatalog, playerAppearance))
            Debug.LogError("Saved character appearance could not be applied.", this);
        playerAppearance.transform.position = new Vector3(
            save.player.positionX,
            save.player.positionY,
            playerAppearance.transform.position.z);
    }
}
