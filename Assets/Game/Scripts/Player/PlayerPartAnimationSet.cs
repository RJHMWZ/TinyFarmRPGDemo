using UnityEngine;

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


    [Header("方向在Sprite Sheet中的块顺序")]
    [SerializeField]
    private int downBlock = 0;

    [SerializeField]
    private int upBlock = 1;

    [SerializeField]
    private int leftBlock = 2;

    [SerializeField]
    private int rightBlock = 3;


    private const int IdleFramesPerDirection = 4;
    private const int WalkFramesPerDirection = 6;


    /// <summary>
    /// 获取指定动画的Sprite
    /// </summary>
    public Sprite GetSprite(PlayerAnimationType animationType,PlayerDirection direction,int frameIndex)
    {
        Sprite[] sprites;
        int framesPerDirection;
        switch (animationType)
        {
            case PlayerAnimationType.Walk:
                sprites = walkFrames;
                framesPerDirection =WalkFramesPerDirection;
                break;
            case PlayerAnimationType.Idle:
                sprites = idleFrames;
                framesPerDirection =IdleFramesPerDirection;
                break;
            default:
                sprites = idleFrames;
                framesPerDirection =IdleFramesPerDirection;
                break;
        }

        if (sprites == null || sprites.Length == 0)return null;
        int directionBlock =GetDirectionBlock(direction);
        int frame =frameIndex % framesPerDirection;
        int spriteIndex =directionBlock * framesPerDirection+ frame;
        if (spriteIndex < 0||spriteIndex >= sprites.Length)return null;
        return sprites[spriteIndex];
    }

    /// <summary>
    /// 获取动画帧数
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
    /// 获取方向对应的Sprite块
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