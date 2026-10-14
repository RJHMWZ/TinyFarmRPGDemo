using System;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Scene-authored locations for the shared farming simulation.</summary>
[DisallowMultipleComponent]
public sealed class FarmSceneLayout : MonoBehaviour
{
    [Serializable]
    public sealed class Station
    {
        public string id;
        public string kind;
        public string title;
        public Transform marker;
        public SpriteRenderer sceneRenderer;
    }

    [Header("Player entry and return")]
    public Transform spawnPoint;
    public Transform homePoint;
    [Header("Farm and playable bounds")]
    [Tooltip("Upper-left plot. The field extends right and down from this marker.")]
    public Transform plotOrigin;
    public Rect worldBounds = new Rect(-55, -18, 135, 43);
    [Header("Interaction locations")]
    public Station[] stations = Array.Empty<Station>();
    [Header("Authored scenery and water collision")]
    public Transform[] sceneryRoots = Array.Empty<Transform>();
    public Tilemap[] blockedTilemaps = Array.Empty<Tilemap>();

    public bool IsConfigured => spawnPoint != null && homePoint != null && plotOrigin != null &&
                                worldBounds.width > 0 && worldBounds.height > 0;
    public Vector2 PlotPosition(int index) => (Vector2)plotOrigin.position +
        new Vector2(index % FarmSaveData.Width, -index / FarmSaveData.Width);

    public Vector2 ResolvePlayerPosition(Vector2 saved, bool needsSpawn)
    {
        return needsSpawn || !worldBounds.Contains(saved) ||
               float.IsNaN(saved.x) || float.IsNaN(saved.y) ? (Vector2)spawnPoint.position : saved;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(worldBounds.center, new Vector3(worldBounds.width, worldBounds.height, 0));
        if (plotOrigin != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(PlotPosition(0) + new Vector2((FarmSaveData.Width - 1) * 0.5f, -(FarmSaveData.Height - 1) * 0.5f),
                new Vector3(FarmSaveData.Width, FarmSaveData.Height, 0));
        }
        foreach (Station station in stations)
            if (station != null && station.marker != null) Gizmos.DrawWireSphere(station.marker.position, 0.65f);
    }
}
