using UnityEngine;

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

    private PlayerAnimationType currentAnimation =PlayerAnimationType.Idle;
    private PlayerDirection currentDirection =PlayerDirection.Down;

    private int frameIndex;

    private float frameTimer;

    private void Start()
    {
        RefreshFrame();
    }

    private void Update()
    {
        if (movement == null ||appearance == null)
        {
            return;
        }

        UpdateState();

        UpdateDirection();

        UpdateFrame();
    }


    /// <summary>
    /// Idle / Walk
    /// </summary>
    private void UpdateState()
    {
        PlayerAnimationType newAnimation =movement.IsMoving? PlayerAnimationType.Walk: PlayerAnimationType.Idle;
        if (newAnimation ==currentAnimation)
        {
            return;
        }
        currentAnimation = newAnimation;
        ResetAnimation();
    }

    /// <summary>
    /// 根据输入决定朝向
    /// </summary>
    private void UpdateDirection()
    {
        // Idle时保持最后一次方向
        if (!movement.IsMoving)
            return;
        Vector2 input =movement.MoveInput;
        PlayerDirection newDirection;
        // 判断哪个轴输入更大
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
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

        if (newDirection ==currentDirection)
        {
            return;
        }

        currentDirection =newDirection;
        ResetAnimation();
    }


    /// <summary>
    /// 播放当前动画
    /// </summary>
    private void UpdateFrame()
    {
        float fps =currentAnimation ==PlayerAnimationType.Walk? walkFPS: idleFPS;
        float frameDuration =1f / fps;
        frameTimer +=Time.deltaTime;
        if (frameTimer <frameDuration)
        {
            return;
        }
        frameTimer -=frameDuration;
        int frameCount =appearance.GetFrameCount(currentAnimation);
        frameIndex++;

        if (frameIndex >= frameCount)
        {
            frameIndex = 0;
        }
        RefreshFrame();
    }


    private void ResetAnimation()
    {
        frameIndex = 0;
        frameTimer = 0f;
        RefreshFrame();
    }

    private void RefreshFrame()
    {
        appearance.SetFrame(currentAnimation,currentDirection,frameIndex);
    }
}