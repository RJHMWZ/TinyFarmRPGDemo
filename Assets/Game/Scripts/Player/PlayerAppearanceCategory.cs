/// <summary>
/// 角色外观叠加层分类。枚举顺序同时决定 UI 预览从底到顶的绘制顺序。
/// </summary>
public enum PlayerAppearanceCategory
{
    Skin,
    Clothes,
    Eyes,
    Hair,
    Accessory
}

/// <summary>玩家在角色创建面板中选择的性别。</summary>
public enum PlayerGender
{
    Male,
    Female
}

/// <summary>单项外观资源允许使用的性别；Any 表示两者通用。</summary>
public enum AppearanceGender
{
    Any,
    Male,
    Female
}
