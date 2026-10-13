using System;
using UnityEngine;

/// <summary>
/// 不使用 Animator 的逐帧动画播放器。
/// 它根据 PlayerMovement 决定 Idle/Walk 和朝向，使用计时器推进 frameIndex，
/// 再让 PlayerAppearance 把对应 Sprite 同步设置到所有外观图层。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAnimationController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField]
    private PlayerMovement movement;

    [SerializeField]
    private PlayerAppearance appearance;

    [Header("动画速度")]
    [Min(0.01f)]
    [SerializeField]
    private float idleFPS = 4f;

    [Min(0.01f)]
    [SerializeField]
    private float walkFPS = 8f;

    // 当前动画状态和朝向。它们共同决定从哪一组 Sprite 中取帧。
    private PlayerAnimationType currentAnimation = PlayerAnimationType.Idle;
    private PlayerDirection currentDirection = PlayerDirection.Down;

    private int frameIndex;   // 当前动画播放到第几帧，从 0 开始。

    private float frameTimer; // 当前帧已经显示了多少秒。
    private Action impact;
    private float actionSeconds;
    private FarmActionAssets actionAssets;
    private SpriteRenderer toolRenderer;
    public bool IsActing { get; private set; }
    public PlayerAnimationType CurrentAnimation => currentAnimation;
    public PlayerDirection CurrentDirection => currentDirection;
    public int CurrentFrame => frameIndex;

    public bool PlayAction(PlayerAnimationType action, Vector2 toward, Action onImpact)
    {
        if (!isActiveAndEnabled || IsActing || appearance == null || action == PlayerAnimationType.Idle || action == PlayerAnimationType.Walk) return false;
        if (toolRenderer == null) toolRenderer = appearance.CreateToolRenderer();
        if (actionAssets == null) actionAssets = Resources.Load<FarmActionAssets>("FarmActionAssets");
        if (toward.sqrMagnitude > 0.001f)
            currentDirection = Mathf.Abs(toward.x) > Mathf.Abs(toward.y)
                ? toward.x > 0 ? PlayerDirection.Right : PlayerDirection.Left
                : toward.y > 0 ? PlayerDirection.Up : PlayerDirection.Down;
        currentAnimation = action;
        impact = onImpact;
        actionSeconds = 0;
        IsActing = true;
        movement.SetActionLocked(true);
        ResetAnimation();
        return true;
    }

    public void CancelAction()
    {
        impact = null;
        IsActing = false;
        if (movement != null) movement.SetActionLocked(false);
        if (toolRenderer != null) toolRenderer.enabled = false;
        currentAnimation = PlayerAnimationType.Idle;
        if (appearance != null) ResetAnimation();
    }

    private void OnDisable() => CancelAction();

    private void AdvanceAction(float seconds)
    {
        actionSeconds += seconds;
        float fps = FarmActionAssets.FramesPerSecond(currentAnimation);
        int count = appearance.HasAction(currentAnimation) ? appearance.GetFrameCount(currentAnimation) : FarmActionAssets.FrameCount(currentAnimation);
        frameIndex = Mathf.Min(count - 1, Mathf.FloorToInt(actionSeconds * fps));
        RefreshFrame();
        if (actionSeconds * fps >= Mathf.Min(count - 1, FarmActionAssets.ImpactFrame(currentAnimation)) && impact != null)
        {
            Action callback = impact;
            impact = null; // Clear before invoking; scene changes and skipped frames cannot hit twice.
            try { callback(); }
            catch { CancelAction(); throw; }
        }
        if (IsActing && actionSeconds * fps >= count) CancelAction();
    }

    private void Awake()
    {
        if (movement != null && appearance != null) return;
        Debug.LogError("PlayerAnimationController requires PlayerMovement and PlayerAppearance references.", this);
        enabled = false;
    }

    private void Start()
    {
        RefreshFrame();
    }

    private void Update()
    {
        var root = GameRoot.Instance;
        if (root != null)
        {
            if (root.InputModes.CurrentMode != GameInputMode.Gameplay)
            {
                if (IsActing) CancelAction();
                return;
            }
            if (root.Pause.IsPaused) return;
        }
        if (IsActing) { AdvanceAction(Time.deltaTime); return; }
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

        // 斜向输入只显示一个四方向动画；绝对值相等时优先采用横向。
        if (Mathf.Abs(input.x) >= Mathf.Abs(input.y))
        {
            if (input.x > 0f)
            {
                newDirection = PlayerDirection.Right;
            }
            else
            {
                newDirection = PlayerDirection.Left;
            }
        }
        else
        {
            if (input.y > 0f)
            {
                newDirection = PlayerDirection.Up;
            }
            else
            {
                newDirection = PlayerDirection.Down;
            }
        }

        if (newDirection == currentDirection)
        {
            return;
        }

        currentDirection = newDirection;
        ResetAnimation();
    }


    /// <summary>
    /// 按设定的 FPS 推进当前动画。
    /// 这是 Animator 在本项目中的替代实现：自己累计时间、切换帧并循环。
    /// </summary>
    private void UpdateFrame()
    {
        float fps = currentAnimation == PlayerAnimationType.Walk ? walkFPS : idleFPS;
        if (fps <= 0f) return;
        float frameDuration = 1f / fps; // 例如 8 FPS 表示每帧显示 1/8 秒。

        frameTimer += Time.deltaTime;
        int elapsedFrames = Mathf.FloorToInt(frameTimer / frameDuration);
        if (elapsedFrames <= 0) return;
        // 保留不足一帧的余量，并在掉帧时一次追上多帧，避免动画实际速度随帧率降低。
        frameTimer -= elapsedFrames * frameDuration;

        int frameCount = Mathf.Max(1, appearance.GetFrameCount(currentAnimation));
        frameIndex = (frameIndex + elapsedFrames) % frameCount;
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
        if (toolRenderer != null)
        {
            toolRenderer.sprite = IsActing && actionAssets != null ? actionAssets.GetTool(currentAnimation, currentDirection, frameIndex) : null;
            toolRenderer.enabled = toolRenderer.sprite != null;
        }
    }
}
