using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public sealed class GameDataTests
{
    [Test]
    public void Normalize_RestoresRequiredSaveSections()
    {
        var data = new GameSaveData
        {
            metadata = null,
            player = null,
            world = null,
            quests = null,
            relationships = null,
            unlocks = null
        };

        data.Normalize();

        Assert.That(data.metadata, Is.Not.Null);
        Assert.That(data.player, Is.Not.Null);
        Assert.That(data.player.appearance, Is.Not.Null);
        Assert.That(data.world, Is.Not.Null);
        Assert.That(data.quests, Is.Not.Null);
        Assert.That(data.relationships, Is.Not.Null);
        Assert.That(data.unlocks, Is.Not.Null);
    }

    [Test]
    public void CharacterNameGenerator_UsesOnlyConfiguredEnglishNames()
    {
        CharacterNameDatabase database = AssetDatabase.LoadAssetAtPath<CharacterNameDatabase>(
            "Assets/Game/Data/Catalogs/CharacterNameDatabase.asset");
        Assert.That(database, Is.Not.Null);
        Assert.That(database.DefaultLocaleCode, Is.EqualTo("en").IgnoreCase);

        CharacterNameDatabase.NamePool pool = database.FindPool("en");
        Assert.That(pool, Is.Not.Null);
        var expected = new HashSet<string>(pool.Names);
        for (int i = 0; i < 32; i++)
            Assert.That(expected.Contains(CharacterNameGenerator.GetRandomName(database, "en")), Is.True);
    }
}
