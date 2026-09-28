using UnityEngine;

/// <summary>
/// 不使用 Animator 的逐帧动画播放器。
/// 它根据 PlayerMovement 决定 Idle/Walk 和朝向，使用计时器推进 frameIndex，
/// 再让 PlayerAppearance 把对应 Sprite 同步设置到所有外观图层。
/// </summary>
public class PlayerAnimationController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField]
    private PlayerMovement movement;

    [SerializeField]
    private PlayerAppearance appearance;

    [Header("动画速度")]
    [SerializeField]
    private float idleFPS = 4f;

    [SerializeField]
    private float walkFPS = 8f;

    // 当前动画状态和朝向。它们共同决定从哪一组 Sprite 中取帧。
    private PlayerAnimationType currentAnimation = PlayerAnimationType.Idle;
    private PlayerDirection currentDirection = PlayerDirection.Down;

    private int frameIndex;   // 当前动画播放到第几帧，从 0 开始。

    private float frameTimer; // 当前帧已经显示了多少秒。

    private void Start()
    {
        RefreshFrame();
    }

    private void Update()
    {
        if (movement == null || appearance == null)
        {
            return;
        }

        UpdateState();

        UpdateDirection();

        UpdateFrame();
    }


    /// <summary>
    /// 根据 PlayerMovement 的 IsMoving 决定当前动画是 Idle 还是 Walk。
    /// </summary>
    private void UpdateState()
    {
        PlayerAnimationType newAnimation = movement.IsMoving
            ? PlayerAnimationType.Walk
            : PlayerAnimationType.Idle;

        if (newAnimation == currentAnimation)
        {
            return;
        }
        currentAnimation = newAnimation;
        ResetAnimation();
    }

    /// <summary>
    /// 根据 PlayerMovement 的 MoveInput 决定当前朝向。
    /// </summary>
    private void UpdateDirection()
    {
        // Idle 时不重新计算方向，因此角色停止后会保持最后一次移动的朝向。
        if (!movement.IsMoving)
            return;

        Vector2 input = movement.MoveInput;
        PlayerDirection newDirection;

        // 斜向输入时只显示一个四方向动画：哪个轴的绝对值更大，就采用哪个轴。
        // 两轴相等时进入纵向分支，所以 W+D 会显示 Up，S+D 会显示 Down。
        if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
        {
            if (input.x > 0f)
            {
                newDirection =PlayerDirection.Right;
            }
            else
            {
                newDirection =PlayerDirection.Left;
            }
        }
        else
        {
            if (input.y > 0f)
            {
                newDirection =PlayerDirection.Up;
            }
            else
            {
                newDirection =PlayerDirection.Down;
            }
        }

        if (newDirection == currentDirection)
        {
            return;
        }

        currentDirection =newDirection;
        ResetAnimation();
    }


    /// <summary>
    /// 按设定的 FPS 推进当前动画。
    /// 这是 Animator 在本项目中的替代实现：自己累计时间、切换帧并循环。
    /// </summary>
    private void UpdateFrame()
    {
        float fps = currentAnimation == PlayerAnimationType.Walk ? walkFPS : idleFPS;
        float frameDuration = 1f / fps; // 例如 8 FPS 表示每帧显示 1/8 秒。

        frameTimer += Time.deltaTime;
        if (frameTimer < frameDuration)
        {
            return;
        }
        // 使用减法而不是直接清零，可以保留超出一帧时长的零头，减少节奏漂移。
        frameTimer -= frameDuration;

        int frameCount = appearance.GetFrameCount(currentAnimation);
        frameIndex++;

        if (frameIndex >= frameCount)
        {
            frameIndex = 0;
        }
        RefreshFrame();
    }

    /// <summary>
    /// 动画类型或朝向变化后从第 0 帧重新播放，避免跳到新动画的中间帧。
    /// </summary>
    private void ResetAnimation()
    {
        frameIndex = 0;
        frameTimer = 0f;
        RefreshFrame();
    }

    /// <summary>
    /// 让 PlayerAppearance 把当前动画类型、朝向和帧序号对应的 Sprite 同步设置到所有外观图层。
    /// </summary>
    private void RefreshFrame()
    {
        appearance.SetFrame(currentAnimation, currentDirection, frameIndex);
    }
}
