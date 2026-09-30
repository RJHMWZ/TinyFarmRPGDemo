using UnityEngine;

/// <summary>Random-name selection logic kept separate from the configurable name data.</summary>
public static class CharacterNameGenerator
{
    public static string GetRandomName(CharacterNameDatabase database, string localeCode)
    {
        if (database == null) return string.Empty;
        CharacterNameDatabase.NamePool pool = database.FindPool(localeCode);
        return pool != null ? Pick(pool.Names) : string.Empty;
    }

    public static string GetRandomNameFromAllPools(CharacterNameDatabase database)
    {
        if (database == null) return string.Empty;
        int total = 0;
        for (int i = 0; i < database.Pools.Count; i++)
            if (database.Pools[i] != null) total += CountValid(database.Pools[i].Names);
        if (total == 0) return string.Empty;

        int index = Random.Range(0, total);
        for (int i = 0; i < database.Pools.Count; i++)
        {
            CharacterNameDatabase.NamePool pool = database.Pools[i];
            if (pool == null) continue;
            int count = CountValid(pool.Names);
            if (index < count) return GetValidAt(pool.Names, index);
            index -= count;
        }
        return string.Empty;
    }

    private static string Pick(System.Collections.Generic.IReadOnlyList<string> names)
    {
        int count = CountValid(names);
        return count > 0 ? GetValidAt(names, Random.Range(0, count)) : string.Empty;
    }

    private static int CountValid(System.Collections.Generic.IReadOnlyList<string> names)
    {
        if (names == null) return 0;
        int count = 0;
        for (int i = 0; i < names.Count; i++)
            if (!string.IsNullOrWhiteSpace(names[i])) count++;
        return count;
    }

    private static string GetValidAt(System.Collections.Generic.IReadOnlyList<string> names, int validIndex)
    {
        for (int i = 0; i < names.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(names[i])) continue;
            if (validIndex-- == 0) return names[i].Trim();
        }
        return string.Empty;
    }
}
