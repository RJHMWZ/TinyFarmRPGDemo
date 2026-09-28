using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可在运行时读取的角色外观总表。
/// Editor 构建器会扫描 Data 目录生成此表，游戏逻辑不直接访问 AssetDatabase。
/// </summary>
[CreateAssetMenu(fileName = "CharacterAppearanceDatabase", menuName = "Game/Player/Appearance Database")]
public sealed class CharacterAppearanceDatabase : ScriptableObject
{
    /// <summary>一项可选外观及其分类、性别限制和具体逐帧资源。</summary>
    [Serializable]
    public sealed class Entry
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private PlayerAppearanceCategory category;
        [SerializeField] private AppearanceGender gender = AppearanceGender.Any;
        [SerializeField] private PlayerPartAnimationSet animationSet;

        // id 使用 Unity GUID。资源移动或改名后 GUID 不变，旧存档仍可正确恢复。
        public string Id => id;
        public string DisplayName => displayName;
        public PlayerAppearanceCategory Category => category;
        public AppearanceGender Gender => gender;
        public PlayerPartAnimationSet AnimationSet => animationSet;

#if UNITY_EDITOR
        public Entry(string id, string displayName, PlayerAppearanceCategory category,
            AppearanceGender gender, PlayerPartAnimationSet animationSet)
        {
            this.id = id;
            this.displayName = displayName;
            this.category = category;
            this.gender = gender;
            this.animationSet = animationSet;
        }
#endif
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();
    public IReadOnlyList<Entry> Entries => entries;

    /// <summary>
    /// 将指定分类和性别可用的项目写入调用方提供的列表。
    /// 复用列表可以避免玩家连续点击时产生临时 GC 分配。
    /// </summary>
    public void GetEntries(PlayerAppearanceCategory category, PlayerGender gender, List<Entry> result)
    {
        result.Clear();
        AppearanceGender requested = gender == PlayerGender.Male
            ? AppearanceGender.Male
            : AppearanceGender.Female;

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry != null && entry.AnimationSet != null && entry.Category == category &&
                (entry.Gender == AppearanceGender.Any || entry.Gender == requested))
            {
                result.Add(entry);
            }
        }
    }

    /// <summary>使用存档中的稳定 ID 查找资源；资源已删除时返回 null。</summary>
    public Entry Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return entries.Find(entry => entry != null && entry.Id == id);
    }

#if UNITY_EDITOR
    /// <summary>仅供编辑器构建器整体替换自动生成的数据。</summary>
    public void ReplaceEntries(List<Entry> newEntries)
    {
        entries = newEntries ?? new List<Entry>();
    }
#endif
}
