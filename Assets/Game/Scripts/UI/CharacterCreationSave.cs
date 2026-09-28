using UnityEngine;

/// <summary>
/// 角色创建资料的轻量持久化入口。
/// 当前使用 PlayerPrefs 保存 JSON；以后替换为正式存档系统时，面板无需跟随修改。
/// </summary>
public static class CharacterCreationSave
{
    private const string PlayerPrefsKey = "character.profile.v1";

    /// <summary>将完整角色资料序列化并立即写入磁盘。</summary>
    public static void Save(CharacterCreationProfile profile)
    {
        if (profile == null) return;
        PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(profile));
        PlayerPrefs.Save();
    }

    /// <summary>读取角色资料；没有存档或内容损坏时返回安全的空资料。</summary>
    public static CharacterCreationProfile Load()
    {
        string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json)) return new CharacterCreationProfile();

        try
        {
            return JsonUtility.FromJson<CharacterCreationProfile>(json) ?? new CharacterCreationProfile();
        }
        catch
        {
            return new CharacterCreationProfile();
        }
    }
}
