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
    public FarmActionFeedback ActionFeedback { get; private set; }
    public FarmSceneLayout Layout { get; private set; }
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
        Layout = GetComponent<FarmSceneLayout>();
        if (Layout != null && !Layout.IsConfigured)
        {
            Debug.LogError("FarmSceneLayout requires spawn, home, plot origin and playable bounds.", this);
            return false;
        }
        Active = this;
        Player = player;
        Game = new FarmGame(root.Session.CurrentSave, content);
        Game.DayEnded += OnDayEnded;
        World = gameObject.AddComponent<FarmWorldView>();
        World.Initialize(this);
        ActionFeedback = gameObject.AddComponent<FarmActionFeedback>();
        ActionFeedback.Initialize(this);
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
        Game.Save.player.needsSceneSpawn = false;
        Game.Save.world.currentScene = SceneManager.GetActiveScene().name;
        bool saved = root.Saves.Save(root.Session.ActiveSlot, Game.Save);
        if (feedback || !saved) Game.Execute(() => saved ? FarmResult.Ok("游戏已保存。") : FarmResult.Fail("保存失败，当前游戏仍保留，请重试。"), FarmChange.None);
        return saved;
    }
    private void OnDayEnded()
    {
        Vector2 home = Layout != null && Layout.IsConfigured ? (Vector2)Layout.homePoint.position : new Vector2(-4f, 3f);
        Player.position = new Vector3(home.x, home.y, Player.position.z);
        Rigidbody2D body = Player.GetComponent<Rigidbody2D>();
        if (body != null) { body.position = home; body.velocity = Vector2.zero; }
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
