using System;

/// <summary>
/// 可序列化的玩家角色创建结果。
/// 只保存资源稳定 ID，不直接序列化 ScriptableObject，方便跨场景和版本持久化。
/// </summary>
[Serializable]
public sealed class CharacterCreationProfile
{
    // 存档结构版本，未来字段迁移时可据此执行兼容处理。
    public int version = 1;
    public string playerName;
    public PlayerGender gender;
    public string skinId;
    public string clothesId;
    public string eyesId;
    public string hairId;
    public string accessoryId;

    /// <summary>按外观分类读取对应资源 ID。</summary>
    public string GetPartId(PlayerAppearanceCategory category)
    {
        switch (category)
        {
            case PlayerAppearanceCategory.Skin: return skinId;
            case PlayerAppearanceCategory.Clothes: return clothesId;
            case PlayerAppearanceCategory.Eyes: return eyesId;
            case PlayerAppearanceCategory.Hair: return hairId;
            case PlayerAppearanceCategory.Accessory: return accessoryId;
            default: return string.Empty;
        }
    }

    /// <summary>按外观分类写入对应资源 ID。</summary>
    public void SetPartId(PlayerAppearanceCategory category, string id)
    {
        switch (category)
        {
            case PlayerAppearanceCategory.Skin: skinId = id; break;
            case PlayerAppearanceCategory.Clothes: clothesId = id; break;
            case PlayerAppearanceCategory.Eyes: eyesId = id; break;
            case PlayerAppearanceCategory.Hair: hairId = id; break;
            case PlayerAppearanceCategory.Accessory: accessoryId = id; break;
        }
    }
}
