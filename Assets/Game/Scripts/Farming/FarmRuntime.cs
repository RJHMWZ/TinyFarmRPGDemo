using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene lifetime adapter: simulation clock, checkpoint capture, autosave and presentation.</summary>
[DisallowMultipleComponent]
public sealed class FarmRuntime : MonoBehaviour
{
    public static FarmRuntime Active { get; private set; }
    public FarmGame Game { get; private set; }
    public Transform Player { get; private set; }
    public FarmWorldView World { get; private set; }
    public string InteractionHint { get; internal set; }
    private float autosaveSeconds;
    public bool Initialize(Transform player)
    {
        if (Game != null) return true;
        var root = GameRoot.Instance;
        FarmContent content = Resources.Load<FarmContent>("FarmContent");
        if (root == null || root.Session.CurrentSave == null || content == null)
        {
            Debug.LogError("FarmRuntime requires a loaded save and Resources/FarmContent. Run Tools > Tiny Farm > Build Farm Content.");
            return false;
        }
        Active = this;
        Player = player;
        Game = new FarmGame(root.Session.CurrentSave, content);
        Game.DayEnded += OnDayEnded;
        World = gameObject.AddComponent<FarmWorldView>();
        World.Initialize(this);
        var interaction = player.gameObject.AddComponent<FarmInteraction>();
        interaction.Initialize(this);
        return true;
    }
    private void Update()
    {
        var root = GameRoot.Instance;
        if (Game == null || root == null || root.Pause.IsPaused || root.InputModes.CurrentMode != GameInputMode.Gameplay) return;
        Game.Tick(Time.deltaTime);
        autosaveSeconds += Time.deltaTime;
        if (autosaveSeconds >= 120f) { autosaveSeconds = 0; SaveNow(false); }
    }
    public bool SaveNow(bool feedback = true)
    {
        var root = GameRoot.Instance;
        if (Game == null || root == null || root.Session.CurrentSave != Game.Save) return false;
        Game.Save.player.positionX = Player.position.x;
        Game.Save.player.positionY = Player.position.y;
        Game.Save.world.currentScene = SceneManager.GetActiveScene().name;
        bool saved = root.Saves.Save(root.Session.ActiveSlot, Game.Save);
        if (feedback || !saved) Game.Execute(() => saved ? FarmResult.Ok("Game saved.") : FarmResult.Fail("Save failed. Your session is still open; please retry."));
        return saved;
    }
    private void OnDayEnded()
    {
        Player.position = new Vector3(-4f, 3f, Player.position.z);
        SaveNow(false);
    }
    private void OnApplicationPause(bool paused) { if (paused && Game != null) SaveNow(false); }
    private void OnApplicationQuit() { if (Game != null) SaveNow(false); }
    private void OnDestroy()
    {
        if (Game != null) Game.DayEnded -= OnDayEnded;
        if (Active == this) Active = null;
    }
}
