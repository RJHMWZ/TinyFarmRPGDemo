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
            quests = new QuestSaveData { activeQuestIds = null, completedQuestIds = null },
            relationships = new RelationshipSaveData
            {
                npcIds = new[] { "npc-a", "npc-b" },
                friendshipValues = new[] { 10 }
            },
            unlocks = new UnlockSaveData { unlockedIds = null }
        };

        data.Normalize();

        Assert.That(data.metadata, Is.Not.Null);
        Assert.That(data.player, Is.Not.Null);
        Assert.That(data.player.appearance, Is.Not.Null);
        Assert.That(data.world, Is.Not.Null);
        Assert.That(data.quests, Is.Not.Null);
        Assert.That(data.quests.activeQuestIds, Is.Not.Null);
        Assert.That(data.quests.completedQuestIds, Is.Not.Null);
        Assert.That(data.relationships, Is.Not.Null);
        Assert.That(data.relationships.npcIds, Is.Not.Null);
        Assert.That(data.relationships.friendshipValues, Is.Not.Null);
        Assert.That(data.relationships.friendshipValues, Has.Length.EqualTo(2));
        Assert.That(data.relationships.friendshipValues[0], Is.EqualTo(10));
        Assert.That(data.unlocks, Is.Not.Null);
        Assert.That(data.unlocks.unlockedIds, Is.Not.Null);
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
