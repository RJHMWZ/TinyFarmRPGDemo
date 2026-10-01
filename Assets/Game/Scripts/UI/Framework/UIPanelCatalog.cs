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
    public IReadOnlyList<Entry> Entries => entries;

    public Entry Find(string id)
    {
        for (int i = 0; i < entries.Count; i++)
            if (string.Equals(entries[i].id, id, StringComparison.Ordinal)) return entries[i];
        return null;
    }
}
