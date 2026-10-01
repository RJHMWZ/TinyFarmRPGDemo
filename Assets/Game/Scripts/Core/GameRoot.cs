using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameRoot : MonoBehaviour
{
    public static GameRoot Instance { get; private set; }

    public SaveService Saves { get; private set; }
    public GameSession Session { get; private set; }
    public SceneLoader Scenes { get; private set; }
    public TransitionService Transitions { get; private set; }
    public InputModeService InputModes { get; private set; }
    public PauseService Pause { get; private set; }
    public UIService UI { get; private set; }
    public GameFlowController Flow { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Saves = new SaveService();
        Session = GetOrAdd<GameSession>();
        Scenes = GetOrAdd<SceneLoader>();
        Transitions = GetOrAdd<TransitionService>();
        InputModes = GetOrAdd<InputModeService>();
        Pause = GetOrAdd<PauseService>();
        UI = GetOrAdd<UIService>();
        Flow = GetOrAdd<GameFlowController>();
    }

    private T GetOrAdd<T>() where T : Component
    {
        T component = GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}

public static class GameRootBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRoot()
    {
        if (GameRoot.Instance == null) new GameObject("GameRoot").AddComponent<GameRoot>();
    }
}
