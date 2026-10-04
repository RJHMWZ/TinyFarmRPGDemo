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
        Assert.That(data.inventory, Is.Not.Null);
        Assert.That(data.inventory.slots, Has.Length.EqualTo(InventorySaveData.SlotCount));
        Assert.That(data.inventory.slots, Has.All.Not.Null);
    }

    [Test]
    public void StarterInventory_ContainsCoreFarmTools()
    {
        InventorySaveData inventory = InventorySaveData.CreateStarterInventory();

        Assert.That(inventory.slots[0].itemId, Is.EqualTo("hoe"));
        Assert.That(inventory.slots[1].itemId, Is.EqualTo("watering-can"));
        Assert.That(inventory.slots[4].count, Is.EqualTo(15));
    }

    [Test]
    public void Calendar_AdvancesAcrossSeasonAndYearBoundary()
    {
        var world = new WorldSaveData { year = 1, season = 3, day = 28, hour = 23, minute = 50 };

        bool beganNewDay = GameCalendar.AdvanceMinutes(world, 10);

        Assert.That(beganNewDay, Is.True);
        Assert.That(world.year, Is.EqualTo(2));
        Assert.That(world.season, Is.Zero);
        Assert.That(world.day, Is.EqualTo(1));
        Assert.That(world.hour, Is.Zero);
        Assert.That(world.minute, Is.Zero);
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
