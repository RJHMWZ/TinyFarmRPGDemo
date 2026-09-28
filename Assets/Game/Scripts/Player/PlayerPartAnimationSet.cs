using UnityEngine;

/// <summary>
/// 一个角色部件（皮肤、衣服、头发等）的全部逐帧图片资源。
/// ScriptableObject 让这些数据保存为 Project 中的 .asset，可被多个角色或换装槽复用。
///
/// 数组采用“方向块 + 块内帧”的一维排列。例如 Idle 每方向 4 帧：
/// Down[0..3]、Up[0..3]、Left[0..3]、Right[0..3]，总计 16 张。
/// 实际方向块顺序由下面四个 Block 字段配置。
/// </summary>
[CreateAssetMenu(
    fileName = "PlayerPartAnimation",
    menuName = "Game/Player/Part Animation Set"
)]
public class PlayerPartAnimationSet : ScriptableObject
{
    [Header("Idle - 16帧")]
    [Tooltip("4方向 × 每方向4帧")]
    [SerializeField]
    private Sprite[] idleFrames;

    [Header("Walk - 24帧")]
    [Tooltip("4方向 × 每方向6帧")]
    [SerializeField]
    private Sprite[] walkFrames;


    [Header("方向在 Sprite 数组中的块顺序")]
    [Tooltip("Down 位于第几个方向块；从 0 开始。")]
    [SerializeField]
    private int downBlock = 0;

    [SerializeField]
    private int upBlock = 1;

    [SerializeField]
    private int leftBlock = 2;

    [SerializeField]
    private int rightBlock = 3;


    // 当前资源格式的固定约定：待机每方向 4 帧，行走每方向 6 帧。
    private const int IdleFramesPerDirection = 4;
    private const int WalkFramesPerDirection = 6;


    /// <summary>
    /// 根据动画、朝向和帧序号，从一维 Sprite 数组中取得目标图片。
    /// </summary>
    public Sprite GetSprite(PlayerAnimationType animationType, PlayerDirection direction, int frameIndex)
    {
        Sprite[] sprites;
        int framesPerDirection;
        switch (animationType)
        {
            case PlayerAnimationType.Walk:
                sprites = walkFrames;
                framesPerDirection = WalkFramesPerDirection;
                break;
            case PlayerAnimationType.Idle:
                sprites = idleFrames;
                framesPerDirection = IdleFramesPerDirection;
                break;
            default:
                sprites = idleFrames;
                framesPerDirection = IdleFramesPerDirection;
                break;
        }

        if (sprites == null || sprites.Length == 0) return null;

        int directionBlock = GetDirectionBlock(direction);

        // 取模使超出末帧的序号回到开头，从而形成循环动画。
        int frame = frameIndex % framesPerDirection;

        // 一维数组下标 = 第几个方向块 * 每块帧数 + 块内第几帧。
        // 例如 Walk/Left，leftBlock=2、frameIndex=3，则下标为 2*6+3=15。
        int spriteIndex = directionBlock * framesPerDirection + frame;
        if (spriteIndex < 0 || spriteIndex >= sprites.Length) return null;

        return sprites[spriteIndex];
    }

    /// <summary>
    /// 返回一个方向所包含的帧数，而不是四个方向的总图片数。
    /// </summary>
    public int GetFrameCount(PlayerAnimationType animationType)
    {
        switch (animationType)
        {
            case PlayerAnimationType.Walk:
                return WalkFramesPerDirection;
            case PlayerAnimationType.Idle:
                return IdleFramesPerDirection;
            default:
                return IdleFramesPerDirection;
        }
    }

    /// <summary>
    /// 把逻辑朝向转换成该朝向在 Sprite 数组中的块编号。
    /// </summary>
    private int GetDirectionBlock(PlayerDirection direction)
    {
        switch (direction)
        {
            case PlayerDirection.Up:
                return upBlock;
            case PlayerDirection.Left:
                return leftBlock;
            case PlayerDirection.Right:
                return rightBlock;
            default:
                return downBlock;
        }
    }
}
