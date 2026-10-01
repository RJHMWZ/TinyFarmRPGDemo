using System;
using System.Collections.Generic;
using UnityEngine;

public enum CharacterNameLanguage
{
    Chinese,
    English,
    Mixed
}

/// <summary>
/// 可在运行时读取的角色名称总表。
/// </summary>
[CreateAssetMenu(fileName = "CharacterNameDatabase", menuName = "Tiny Farm/Character Name Database")]
public sealed class CharacterNameDatabase : ScriptableObject
{
    [Serializable]
    public sealed class NamePool
    {
        [Tooltip("BCP-47 style locale code, for example zh-CN or en.")]
        [SerializeField] private string localeCode;
        [SerializeField] private List<string> names = new List<string>();

        public string LocaleCode => localeCode;
        public IReadOnlyList<string> Names => names;
    }

    [Tooltip("Locale used when the character creation panel opens.")]
    [SerializeField] private string defaultLocaleCode = "zh-CN";
    [SerializeField] private List<NamePool> pools = new List<NamePool>();

    public string DefaultLocaleCode => defaultLocaleCode;
    public IReadOnlyList<NamePool> Pools => pools;

    public NamePool FindPool(string localeCode)
    {
        if (string.IsNullOrWhiteSpace(localeCode)) return null;
        return pools.Find(pool => pool != null &&
            string.Equals(pool.LocaleCode, localeCode, StringComparison.OrdinalIgnoreCase));
    }
}
