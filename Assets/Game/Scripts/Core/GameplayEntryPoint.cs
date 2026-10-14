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
        FarmSceneLayout layout = GetComponent<FarmSceneLayout>();
        if (layout != null && layout.IsConfigured)
        {
            Vector2 position = layout.ResolvePlayerPosition(new Vector2(save.player.positionX, save.player.positionY), save.player.needsSceneSpawn);
            save.player.positionX = position.x;
            save.player.positionY = position.y;
        }
        save.player.needsSceneSpawn = false;
        save.world.currentScene = gameObject.scene.name;
        playerAppearance.transform.position = new Vector3(
            save.player.positionX,
            save.player.positionY,
            playerAppearance.transform.position.z);
        Rigidbody2D body = playerAppearance.GetComponent<Rigidbody2D>();
        if (body != null) body.position = playerAppearance.transform.position;

        FarmRuntime farm = GetComponent<FarmRuntime>();
        if (farm == null) farm = gameObject.AddComponent<FarmRuntime>();
        if (!farm.Initialize(playerAppearance.transform)) return;

        GameplayHudController hud = GetComponent<GameplayHudController>();
        if (hud == null) hud = gameObject.AddComponent<GameplayHudController>();
        hud.Initialize(playerAppearance.transform);
    }
}
