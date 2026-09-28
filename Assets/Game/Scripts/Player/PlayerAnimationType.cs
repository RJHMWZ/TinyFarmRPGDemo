/// <summary>角色当前播放的动画种类，用于选择对应的 Sprite 数组。</summary>
public enum PlayerAnimationType
{
    /// <summary>原地待机。</summary>
    Idle,

    /// <summary>行走。</summary>
    Walk
}

/// <summary>四方向角色动画的朝向。</summary>
public enum PlayerDirection
{
    Down,
    Up,
    Left,
    Right
}
