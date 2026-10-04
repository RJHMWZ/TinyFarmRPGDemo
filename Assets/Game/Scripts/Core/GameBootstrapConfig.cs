using UnityEngine;

/// <summary>Authored root used when a developer starts directly in a non-Boot scene.</summary>
public sealed class GameBootstrapConfig : ScriptableObject
{
    [SerializeField] private GameRoot rootPrefab;
    public GameRoot RootPrefab => rootPrefab;
}
