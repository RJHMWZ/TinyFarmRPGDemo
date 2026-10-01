using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Authored character-name pools grouped by locale code.</summary>
[CreateAssetMenu(fileName = "CharacterNameDatabase", menuName = "Tiny Farm/Character Name Database")]
public sealed class CharacterNameDatabase : ScriptableObject
{
    [Serializable]
    public sealed class NamePool
    {
        [Tooltip("BCP-47 style locale code, for example en or zh-CN.")]
        [SerializeField] private string localeCode;
        [SerializeField] private List<string> names = new List<string>();

        public string LocaleCode => localeCode;
        public IReadOnlyList<string> Names => names;
    }

    [Tooltip("Locale used when the character creation panel opens.")]
    [SerializeField] private string defaultLocaleCode = "en";
    [SerializeField] private List<NamePool> pools = new List<NamePool>();
    private Dictionary<string, NamePool> poolsByLocale;

    public string DefaultLocaleCode => defaultLocaleCode;
    public IReadOnlyList<NamePool> Pools => pools;

    private void OnEnable() => RebuildIndex();

    public NamePool FindPool(string localeCode)
    {
        if (string.IsNullOrWhiteSpace(localeCode)) return null;
        if (poolsByLocale == null) RebuildIndex();
        poolsByLocale.TryGetValue(localeCode, out NamePool pool);
        return pool;
    }

    private void RebuildIndex()
    {
        poolsByLocale = new Dictionary<string, NamePool>(StringComparer.OrdinalIgnoreCase);
        if (pools == null) return;

        for (int i = 0; i < pools.Count; i++)
        {
            NamePool pool = pools[i];
            if (pool == null || string.IsNullOrWhiteSpace(pool.LocaleCode) ||
                poolsByLocale.ContainsKey(pool.LocaleCode))
                continue;
            poolsByLocale.Add(pool.LocaleCode, pool);
        }
    }
}
