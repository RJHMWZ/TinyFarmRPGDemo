using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "UIPanelCatalog", menuName = "Tiny Farm/UI/Panel Catalog")]
public sealed class UIPanelCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public string id;
        public UIPanel prefab;
        public UILayer layer = UILayer.Screen;
        public UIPanelLifetime lifetime = UIPanelLifetime.Cached;
        public bool hideHud;
        public bool pauseGameplay;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();
    private Dictionary<string, Entry> entriesById;

    public IReadOnlyList<Entry> Entries => entries;

    private void OnEnable() => RebuildIndex();

#if UNITY_EDITOR
    private void OnValidate() => RebuildIndex();
#endif

    public Entry Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (entriesById == null) RebuildIndex();
        entriesById.TryGetValue(id, out Entry entry);
        return entry;
    }

    private void RebuildIndex()
    {
        entriesById = new Dictionary<string, Entry>(StringComparer.Ordinal);
        if (entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.id) || entriesById.ContainsKey(entry.id)) continue;
            entriesById.Add(entry.id, entry);
        }
    }
}
