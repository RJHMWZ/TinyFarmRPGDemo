using UnityEngine;

public class PlayerAppearance : MonoBehaviour
{
    [Header("SpriteRenderer")]

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


    [Header("当前角色外观")]
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


    /// <summary>
    /// 刷新角色当前帧
    /// </summary>
    public void SetFrame(PlayerAnimationType animationType,PlayerDirection direction,int frameIndex)
    {
        SetPart(skinRenderer,skin,animationType,direction,frameIndex);

        SetPart(clothesRenderer,clothes,animationType,direction,frameIndex);

        SetPart(eyesRenderer,eyes,animationType,direction,frameIndex);

        SetPart(hairRenderer,hair,animationType,direction,frameIndex);

        SetPart(accessoryRenderer,accessory,animationType,direction,frameIndex);
    }


    private void SetPart(SpriteRenderer renderer,PlayerPartAnimationSet animationSet,PlayerAnimationType animationType,PlayerDirection direction,int frameIndex)
    {
        if (renderer == null)return;

        if (animationSet == null)
        {
            renderer.sprite = null;
            return;
        }
        renderer.sprite =animationSet.GetSprite(animationType,direction,frameIndex);
    }


    /// <summary>
    /// 获取动画帧数量
    /// </summary>
    public int GetFrameCount(PlayerAnimationType animationType)
    {
        if (skin == null)
            return 1;

        return skin.GetFrameCount(animationType);
    }


    // =========================
    // 换装接口
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
}