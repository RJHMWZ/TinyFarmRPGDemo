using UnityEngine;

/// <summary>
/// 管理角色的分层外观，并把同一动画帧设置到各层 SpriteRenderer。
///
/// 角色不是一张完整图片，而是由皮肤、衣服、眼睛、头发、饰品五张透明图片
/// 按相同位置和顺序叠加而成。换装只需替换其中一层的 PlayerPartAnimationSet。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAppearance : MonoBehaviour
{
    // 缓存最近显示的动画状态，换装后可以立即重绘，而不必等待动画控制器下一帧。
    private PlayerAnimationType currentAnimation = PlayerAnimationType.Idle;
    private PlayerDirection currentDirection = PlayerDirection.Down;
    private int currentFrameIndex;
    [Header("SpriteRenderer")]

    [Tooltip("身体/肤色图层的 SpriteRenderer。")]
    [SerializeField]
    private SpriteRenderer skinRenderer;

    [SerializeField]
    private SpriteRenderer clothesRenderer;

    [SerializeField]
    private SpriteRenderer eyesRenderer;

    [SerializeField]
    private SpriteRenderer hairRenderer;

    [SerializeField]
    private SpriteRenderer accessoryRenderer;


    [Header("当前角色外观（每层各自的动画帧资源）")]
    [SerializeField]
    private PlayerPartAnimationSet skin;

    [SerializeField]
    private PlayerPartAnimationSet clothes;

    [SerializeField]
    private PlayerPartAnimationSet eyes;

    [SerializeField]
    private PlayerPartAnimationSet hair;

    [SerializeField]
    private PlayerPartAnimationSet accessory;

    public bool IsConfigured => skinRenderer != null && clothesRenderer != null && eyesRenderer != null &&
                                hairRenderer != null && accessoryRenderer != null;


    /// <summary>
    /// 刷新整个角色的当前帧。
    /// 所有外观层必须使用相同的动画类型、朝向和帧序号，叠加时才不会错位。
    /// </summary>
    public void SetFrame(PlayerAnimationType animationType, PlayerDirection direction, int frameIndex)
    {
        currentAnimation = animationType;
        currentDirection = direction;
        currentFrameIndex = frameIndex;
        SetPart(skinRenderer, skin, animationType, direction, frameIndex);
        SetPart(clothesRenderer, clothes, animationType, direction, frameIndex);
        SetPart(eyesRenderer, eyes, animationType, direction, frameIndex);
        SetPart(hairRenderer, hair, animationType, direction, frameIndex);
        SetPart(accessoryRenderer, accessory, animationType, direction, frameIndex);
    }


    /// <summary>刷新一个外观层；未装备该层时清空其图片。</summary>
    private void SetPart(SpriteRenderer renderer, PlayerPartAnimationSet animationSet, PlayerAnimationType animationType, PlayerDirection direction, int frameIndex)
    {
        // 某些角色可以没有饰品等可选图层，因此允许 Renderer 没有配置。
        if (renderer == null) return;

        if (animationSet == null)
        {
            // 换装时传入 null 代表卸下该部件，必须清空旧 Sprite。
            renderer.sprite = null;
            return;
        }

        renderer.sprite = animationSet.GetSprite(animationType, direction, frameIndex);
    }


    /// <summary>
    /// 获取动画帧数量。
    /// 当前以 skin 为基准，其他外观部件应与 skin 使用相同的每方向帧数。
    /// </summary>
    public int GetFrameCount(PlayerAnimationType animationType)
    {
        if (skin == null)
            return 1;

        return skin.GetFrameCount(animationType);
    }


    // =========================
    // 换装接口：只替换资源引用；下一次 SetFrame 时新部件就会显示。
    // 如果希望点击换装后立刻可见，调用方可随后要求动画控制器刷新当前帧。
    // =========================
    public void SetSkin(PlayerPartAnimationSet newSkin)
    {
        skin = newSkin;
    }

    public void SetClothes(PlayerPartAnimationSet newClothes)
    {
        clothes = newClothes;
    }

    public void SetHair(PlayerPartAnimationSet newHair)
    {
        hair = newHair;
    }

    public void SetEyes(PlayerPartAnimationSet newEyes)
    {
        eyes = newEyes;
    }

    public void SetAccessory(PlayerPartAnimationSet newAccessory)
    {
        accessory = newAccessory;
    }

    /// <summary>换装完成后，使用缓存状态立即刷新所有图层。</summary>
    public void RefreshCurrentFrame()
    {
        SetFrame(currentAnimation, currentDirection, currentFrameIndex);
    }
}
