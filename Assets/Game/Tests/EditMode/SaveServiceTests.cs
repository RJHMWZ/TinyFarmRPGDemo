using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class SaveServiceTests
{
    private string directory;
    private SaveService saves;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "TinyFarmSaveTests", Guid.NewGuid().ToString("N"));
        saves = new SaveService(directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [Test]
    public void VersionFourGameSceneSave_MigratesToVillageWithoutLosingProgress()
    {
        var data = GameSaveData.CreateNew(new CharacterCreationProfile { playerName = "Village Farmer" });
        data.version = 4;
        data.world.currentScene = GameSceneNames.LegacyGameplay;
        data.player.needsSceneSpawn = false;
        data.player.positionX = -4;
        data.player.positionY = 3;
        data.player.money = 725;
        data.farm.Normalize();
        data.farm.plots[0].seedId = "parsnip-seed";
        data.farm.plots[0].growth = 2;
        data.quests.completedQuestIds = new[] { "first-harvest" };
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "save-slot-1.json"),
            "{\"saveVersion\":4,\"data\":" + JsonUtility.ToJson(data) + "}");

        Assert.That(saves.TryLoad(0, out GameSaveData loaded), Is.True);
        Assert.That(loaded.version, Is.EqualTo(5));
        Assert.That(loaded.world.currentScene, Is.EqualTo(GameSceneNames.Gameplay));
        Assert.That(loaded.player.needsSceneSpawn, Is.True);
        Assert.That(loaded.player.money, Is.EqualTo(725));
        Assert.That(loaded.inventory.slots[4].count, Is.EqualTo(15));
        Assert.That(loaded.farm.plots[0].growth, Is.EqualTo(2));
        Assert.That(loaded.quests.completedQuestIds, Is.EqualTo(new[] { "first-harvest" }));
    }

    [Test]
    public void VillageCheckpoint_RetainsPositionWhenReloaded()
    {
        var data = GameSaveData.CreateNew(new CharacterCreationProfile { playerName = "Village Farmer" });
        data.player.needsSceneSpawn = false;
        data.player.positionX = 45;
        data.player.positionY = 0;
        Assert.That(saves.Save(0, data), Is.True);
        Assert.That(saves.TryLoad(0, out GameSaveData loaded), Is.True);
        Assert.That(loaded.world.currentScene, Is.EqualTo("Village"));
        Assert.That(loaded.player.needsSceneSpawn, Is.False);
        Assert.That(loaded.player.positionX, Is.EqualTo(45));
    }

    [Test]
    public void SaveAndLoad_RoundTripsGameplayData()
    {
        var profile = new CharacterCreationProfile { playerName = "Robin" };
        GameSaveData source = GameSaveData.CreateNew(profile);
        source.player.money = 725;
        source.world.day = 12;

        Assert.That(saves.Save(0, source), Is.True);
        Assert.That(saves.TryLoad(0, out GameSaveData loaded), Is.True);
        Assert.That(loaded.player.appearance.playerName, Is.EqualTo("Robin"));
        Assert.That(loaded.metadata.playerName, Is.EqualTo("Robin"));
        Assert.That(loaded.player.money, Is.EqualTo(725));
        Assert.That(loaded.world.day, Is.EqualTo(12));
    }

    [Test]
    public void TryLoad_WhenCurrentFileIsDamaged_UsesBackup()
    {
        Assert.That(saves.Save(0, GameSaveData.CreateNew(new CharacterCreationProfile { playerName = "First" })), Is.True);
        Assert.That(saves.Save(0, GameSaveData.CreateNew(new CharacterCreationProfile { playerName = "Second" })), Is.True);
        File.WriteAllText(Path.Combine(directory, "save-slot-1.json"), "{ damaged json");

        Assert.That(saves.TryLoad(0, out GameSaveData recovered), Is.True);
        Assert.That(recovered.player.appearance.playerName, Is.EqualTo("First"));
    }

    [Test]
    public void TryLoad_WhenCurrentSaveIsFromNewerVersion_DoesNotLoadStaleBackup()
    {
        Assert.That(saves.Save(0, GameSaveData.CreateNew(new CharacterCreationProfile { playerName = "Backup" })), Is.True);
        Assert.That(saves.Save(0, GameSaveData.CreateNew(new CharacterCreationProfile { playerName = "Current" })), Is.True);
        File.WriteAllText(Path.Combine(directory, "save-slot-1.json"),
            "{\"saveVersion\":999,\"data\":{\"version\":999}}");

        LogAssert.Expect(LogType.Error,
            "Save file was created by a newer game version and cannot be loaded: " +
            Path.Combine(directory, "save-slot-1.json"));
        Assert.That(saves.TryLoad(0, out _), Is.False);
        Assert.That(saves.IsSlotOccupied(0), Is.True);
    }

    [Test]
    public void Save_WhenBackupAlreadyExists_ReplacesItWithPreviousSave()
    {
        Assert.That(saves.Save(0, GameSaveData.CreateNew(new CharacterCreationProfile { playerName = "First" })), Is.True);
        Assert.That(saves.Save(0, GameSaveData.CreateNew(new CharacterCreationProfile { playerName = "Second" })), Is.True);
        Assert.That(saves.Save(0, GameSaveData.CreateNew(new CharacterCreationProfile { playerName = "Third" })), Is.True);
        File.WriteAllText(Path.Combine(directory, "save-slot-1.json"), "{ damaged json");

        Assert.That(saves.TryLoad(0, out GameSaveData recovered), Is.True);
        Assert.That(recovered.player.appearance.playerName, Is.EqualTo("Second"));
    }

    [Test]
    public void Delete_RemovesCurrentBackupAndTemporaryFiles()
    {
        Assert.That(saves.Save(0, GameSaveData.CreateNew(new CharacterCreationProfile { playerName = "Robin" })), Is.True);
        File.WriteAllText(Path.Combine(directory, "save-slot-1.json.tmp"), "temporary");

        Assert.That(saves.Delete(0), Is.True);
        Assert.That(saves.HasSave(0), Is.False);
        Assert.That(saves.IsSlotOccupied(0), Is.False);
        Assert.That(Directory.GetFiles(directory, "save-slot-1.json*"), Is.Empty);
    }
}
