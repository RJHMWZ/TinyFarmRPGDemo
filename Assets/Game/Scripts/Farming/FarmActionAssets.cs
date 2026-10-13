using System;
using UnityEngine;

/// <summary>Authored tool overlays and optional recorded sounds. Body frames live with each outfit.</summary>
[CreateAssetMenu(menuName = "Tiny Farm/Data/Action Assets")]
public sealed class FarmActionAssets : ScriptableObject
{
    [Serializable]
    public sealed class ToolClip
    {
        public PlayerAnimationType action;
        public Sprite[] frames = Array.Empty<Sprite>();
        public AudioClip sound;
    }
    public ToolClip[] clips = Array.Empty<ToolClip>();
    public ToolClip Find(PlayerAnimationType action) => Array.Find(clips, x => x != null && x.action == action);
    public Sprite GetTool(PlayerAnimationType action, PlayerDirection direction, int frame)
    {
        Sprite[] frames = Find(action)?.frames;
        if (frames == null || frames.Length < 4) return null;
        int count = frames.Length / 4;
        int block = direction == PlayerDirection.Down ? 0 : direction == PlayerDirection.Up ? 1 : direction == PlayerDirection.Right ? 2 : 3;
        return frames[block * count + Mathf.Clamp(frame, 0, count - 1)];
    }
    public static int FrameCount(PlayerAnimationType action) => action == PlayerAnimationType.Water ? 8 : action == PlayerAnimationType.Harvest || action == PlayerAnimationType.Plant ? 4 : 6;
    public static float FramesPerSecond(PlayerAnimationType action) => action == PlayerAnimationType.Water ? 16f : 12f;
    public static int ImpactFrame(PlayerAnimationType action) => action == PlayerAnimationType.Hoe || action == PlayerAnimationType.Pickaxe ? 4 :
        action == PlayerAnimationType.Harvest || action == PlayerAnimationType.Plant ? 2 : 3;
}
