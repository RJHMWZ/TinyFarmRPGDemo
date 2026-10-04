using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class FarmGameplayTests
{
    private FarmContent content;
    private FarmGame game;
    [SetUp]
    public void SetUp()
    {
        content = AssetDatabase.LoadAssetAtPath<FarmContent>("Assets/Game/Resources/FarmContent.asset");
        Assert.That(content, Is.Not.Null, "Run FarmContentBuilder.Build before tests.");
        game = new FarmGame(GameSaveData.CreateNew(new CharacterCreationProfile { playerName = "Farmer" }), content);
    }
    [Test] public void AuthoredContent_HasValidReferencesAndNoDuplicateIds() => Assert.That(content.ValidateContent().ToArray(), Is.Empty);
    [Test] public void Inventory_AddOverflow_DoesNotPartiallyFillStacks()
    {
        FillBackpack();
        game.Save.inventory.slots[5].Set("wood", "Wood", 98);
        Assert.That(game.Inventory.Add("wood", 2), Is.False);
        Assert.That(game.Inventory.Count("wood"), Is.EqualTo(98));
    }
    [Test] public void Inventory_RemoveAcrossStacks_AndRejectInsufficientAmounts()
    {
        Assert.That(game.Inventory.Add("wood", 120), Is.True);
        Assert.That(game.Inventory.Remove("wood", 121), Is.False);
        Assert.That(game.Inventory.Count("wood"), Is.EqualTo(120));
        Assert.That(game.Inventory.Remove("wood", 110), Is.True);
        Assert.That(game.Inventory.Count("wood"), Is.EqualTo(10));
    }
    [Test] public void Inventory_MoveMergesOrSwapsWithoutDuplicating()
    {
        game.Save.inventory.slots[6].Set("wood", "Wood", 80);
        game.Save.inventory.slots[7].Set("wood", "Wood", 40);
        Assert.That(game.Inventory.Move(7, 6), Is.True);
        Assert.That(game.Save.inventory.slots[6].count, Is.EqualTo(99));
        Assert.That(game.Save.inventory.slots[7].count, Is.EqualTo(21));
        Assert.That(game.Inventory.Move(7, 0), Is.True);
        Assert.That(game.Save.inventory.slots[7].itemId, Is.EqualTo("hoe"));
        Assert.That(game.Inventory.Count("wood"), Is.EqualTo(120));
    }
    [Test] public void Storage_TransferIsAtomicWhenTargetFull()
    {
        foreach (var slot in game.Save.farm.storage.slots) slot.Set("stone", "Stone", 99);
        Assert.That(game.Inventory.TransferTo(game.Storage, 4, 15), Is.False);
        Assert.That(game.Inventory.Count("parsnip-seed"), Is.EqualTo(15));
        game.Save.farm.storage.slots[0].Set(null, null, 0);
        Assert.That(game.Inventory.TransferTo(game.Storage, 4, 15), Is.True);
        Assert.That(game.Inventory.Count("parsnip-seed"), Is.Zero);
        Assert.That(game.Storage.Count("parsnip-seed"), Is.EqualTo(15));
    }
    [Test] public void Shop_FullBackpackAndInsufficientGoldNeverCharge()
    {
        FillBackpack();
        Assert.That(game.Economy.Buy("carrot-seed", 1).Success, Is.False);
        Assert.That(game.Save.player.money, Is.EqualTo(500));
        game.Save.player.money = 0;
        Assert.That(game.Economy.Buy("parsnip-seed", 1).Success, Is.False);
        Assert.That(game.Save.player.money, Is.Zero);
    }
    [TestCase(0)] [TestCase(-1)] [TestCase(int.MaxValue)]
    public void Shop_RejectsInvalidOrUnaffordableAmounts(int amount)
    {
        Assert.That(game.Economy.Buy("carrot-seed", amount).Success, Is.False);
        Assert.That(game.Save.player.money, Is.EqualTo(500));
    }
    [Test] public void Crop_RequiresWaterAndCannotBeHarvestedEarly()
    {
        Plant();
        game.Farming.EndDay(0, false);
        Assert.That(game.Save.farm.plots[0].growth, Is.Zero);
        Assert.That(game.Farming.UsePlot(0, 1).Success, Is.True);
        game.Farming.EndDay(0, false);
        Assert.That(game.Save.farm.plots[0].growth, Is.EqualTo(1));
        Assert.That(game.Farming.IsReady(game.Save.farm.plots[0]), Is.False);
        Assert.That(game.Save.farm.plots[0].watered, Is.False);
    }
    [Test] public void Harvest_FullBackpackPreservesRipeCrop()
    {
        Plant(); game.Save.farm.plots[0].growth = 4; FillBackpack();
        Assert.That(game.Farming.UsePlot(0, 0).Success, Is.False);
        Assert.That(game.Save.farm.plots[0].seedId, Is.EqualTo("parsnip-seed"));
        game.Save.inventory.slots[5].Set(null, null, 0);
        Assert.That(game.Farming.UsePlot(0, 0).Success, Is.True);
        Assert.That(game.Inventory.Count("parsnip"), Is.EqualTo(1));
        Assert.That(game.Save.farm.plots[0].seedId, Is.Null.Or.Empty);
    }
    [Test] public void SeasonChange_WithersIncompatibleCrops()
    {
        Plant(); game.Save.farm.plots[0].watered = true;
        game.Farming.EndDay(1, true);
        Assert.That(game.Save.farm.plots[0].seedId, Is.Null.Or.Empty);
        Assert.That(game.Save.farm.plots[0].watered, Is.True);
    }
    [Test] public void Sprinkler_WatersCrossWithoutWrappingRows()
    {
        foreach (var plot in game.Save.farm.plots) plot.tilled = true;
        game.Save.farm.plots[7].sprinkler = true;
        game.Farming.EndDay(0, false);
        Assert.That(game.Save.farm.plots[7].watered, Is.True);
        Assert.That(game.Save.farm.plots[6].watered, Is.True);
        Assert.That(game.Save.farm.plots[15].watered, Is.True);
        Assert.That(game.Save.farm.plots[8].watered, Is.False);
    }
    [Test] public void Fertilizer_AddsYieldAndIsConsumedWithSingleHarvestCrop()
    {
        Plant(); game.Save.farm.plots[0].growth = 4; game.Save.farm.plots[0].fertilized = true;
        Assert.That(game.Farming.UsePlot(0, 0).Success, Is.True);
        Assert.That(game.Inventory.Count("parsnip"), Is.EqualTo(2));
        Assert.That(game.Save.farm.plots[0].fertilized, Is.False);
    }
    [Test] public void Exhaustion_DoesNotConsumeSeedOrModifyPlot()
    {
        game.Save.farm.plots[0].tilled = true;
        game.Save.farm.energy = 0;
        Assert.That(game.Farming.UsePlot(0, 4).Success, Is.False);
        Assert.That(game.Inventory.Count("parsnip-seed"), Is.EqualTo(15));
        Assert.That(game.Save.farm.plots[0].seedId, Is.Null.Or.Empty);
    }
    [Test] public void Craft_MissingIngredientDoesNotConsumeOtherIngredients()
    {
        game.Inventory.Add("wood", 3);
        Assert.That(game.Economy.Craft("fertilizer").Success, Is.False);
        Assert.That(game.Inventory.Count("wood"), Is.EqualTo(3));
    }
    [Test] public void Craft_FullBackpackRollsBackMaterials()
    {
        FillBackpack();
        game.Save.inventory.slots[5].Set("wood", "Wood", 10);
        game.Save.inventory.slots[6].Set("stone", "Stone", 10);
        Assert.That(game.Economy.Craft("fertilizer").Success, Is.False);
        Assert.That(game.Inventory.Count("wood"), Is.EqualTo(10));
        Assert.That(game.Inventory.Count("stone"), Is.EqualTo(10));
    }
    [Test] public void Craft_CanUseSpaceFreedByIngredients()
    {
        FillBackpack();
        game.Save.inventory.slots[5].Set("wood", "Wood", 3);
        game.Save.inventory.slots[6].Set("stone", "Stone", 2);
        Assert.That(game.Economy.Craft("fertilizer").Success, Is.True);
        Assert.That(game.Inventory.Count("fertilizer"), Is.EqualTo(2));
    }
    [Test] public void Quest_PrerequisitesAndOneTimeRewardAreEnforced()
    {
        game.Progression.Record("plant", 5);
        Assert.That(game.Progression.Claim("first-seeds").Success, Is.False);
        game.Progression.Record("till", 5);
        Assert.That(game.Progression.Claim("first-soil").Success, Is.True);
        int balance = game.Save.player.money;
        Assert.That(game.Progression.Claim("first-soil").Success, Is.False);
        Assert.That(game.Save.player.money, Is.EqualTo(balance));
        Assert.That(game.Progression.Claim("first-seeds").Success, Is.True);
    }
    [Test] public void Social_TalkAndGiftsHaveDailyLimits()
    {
        game.Progression.Talk("willow"); game.Progression.Talk("willow");
        Assert.That(game.Progression.Social("willow").friendship, Is.EqualTo(20));
        game.Inventory.Add("parsnip", 2);
        int slot = Array.FindIndex(game.Save.inventory.slots, x => x.itemId == "parsnip");
        Assert.That(game.Progression.Gift("willow", slot, game.Inventory).Success, Is.True);
        Assert.That(game.Progression.Gift("willow", slot, game.Inventory).Success, Is.False);
        Assert.That(game.Inventory.Count("parsnip"), Is.EqualTo(1));
        Assert.That(game.Progression.Social("willow").friendship, Is.EqualTo(100));
    }
    [Test] public void Gather_RequiresToolAndRespawnsAfterTwoDays()
    {
        Assert.That(game.Farming.Gather("tree1", "wood", 0).Success, Is.False);
        Assert.That(game.Farming.Gather("tree1", "wood", 2).Success, Is.True);
        Assert.That(game.Farming.Gather("tree1", "wood", 2).Success, Is.False);
        game.Sleep(); Assert.That(game.Farming.NodeAvailable("tree1"), Is.False);
        game.Sleep(); Assert.That(game.Farming.NodeAvailable("tree1"), Is.True);
    }
    [Test] public void Sleep_SettlesOnceRestoresEnergyAndCrossesYearBoundary()
    {
        game.Inventory.Add("parsnip", 2);
        int slot = Array.FindIndex(game.Save.inventory.slots, x => x.itemId == "parsnip");
        game.Economy.Ship(slot, 2);
        Assert.That(game.Save.player.money, Is.EqualTo(500));
        game.Save.world.season = 3; game.Save.world.day = 28; game.Save.world.hour = 21;
        game.Save.farm.energy = 0;
        game.Sleep();
        Assert.That(game.Save.world.year, Is.EqualTo(2));
        Assert.That(game.Save.world.season, Is.Zero);
        Assert.That(game.Save.world.day, Is.EqualTo(1));
        Assert.That(game.Save.world.hour, Is.EqualTo(6));
        Assert.That(game.Save.farm.energy, Is.EqualTo(100));
        Assert.That(game.Save.player.money, Is.EqualTo(590));
        game.Sleep(); Assert.That(game.Save.player.money, Is.EqualTo(590));
    }
    [Test] public void Clock_ReachingBedtimeAdvancesExactlyOneDay()
    {
        game.Save.world.hour = 21; game.Save.world.minute = 50;
        game.Tick(content.secondsPerTenMinutes);
        Assert.That(game.Save.world.day, Is.EqualTo(2));
        Assert.That(game.Save.world.hour, Is.EqualTo(6));
        Assert.That(game.Save.farm.daysPlayed, Is.EqualTo(1));
    }
    [Test] public void FullLoop_PlantGrowHarvestShipSleepAndReload()
    {
        Plant();
        for (int day = 0; day < 4; day++) { game.Save.farm.plots[0].watered = true; game.Sleep(); }
        Assert.That(game.Farming.UsePlot(0, 0).Success, Is.True);
        int slot = Array.FindIndex(game.Save.inventory.slots, x => x.itemId == "parsnip");
        Assert.That(game.Economy.Ship(slot, 1).Success, Is.True);
        game.Sleep();
        string directory = Path.Combine(Path.GetTempPath(), "TinyFarmLoop", Guid.NewGuid().ToString("N"));
        try
        {
            var disk = new SaveService(directory);
            Assert.That(disk.Save(0, game.Save), Is.True);
            Assert.That(disk.TryLoad(0, out var loaded), Is.True);
            var restored = new FarmGame(loaded, content);
            Assert.That(restored.Save.version, Is.EqualTo(4));
            Assert.That(restored.Save.player.money, Is.EqualTo(545));
            Assert.That(restored.Progression.Counter("harvest"), Is.EqualTo(1));
            Assert.That(restored.Save.farm.daysPlayed, Is.EqualTo(5));
            Assert.That(restored.Economy.PendingIncome, Is.Zero);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Test] public void VersionThreeSave_MigratesWithoutChangingInventoryMoneyOrIdentity()
    {
        string directory = Path.Combine(Path.GetTempPath(), "TinyFarmMigration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "save-slot-1.json"), "{\"saveVersion\":3,\"data\":{\"version\":3,\"player\":{\"money\":777,\"appearance\":{\"playerName\":\"Willow\"}},\"inventory\":{\"slots\":[{\"itemId\":\"wood\",\"count\":42}]}}}");
            Assert.That(new SaveService(directory).TryLoad(0, out var loaded), Is.True);
            Assert.That(loaded.version, Is.EqualTo(4));
            Assert.That(loaded.player.money, Is.EqualTo(777));
            Assert.That(loaded.player.appearance.playerName, Is.EqualTo("Willow"));
            Assert.That(loaded.inventory.slots[0].count, Is.EqualTo(42));
            Assert.That(loaded.farm.plots.Length, Is.EqualTo(40));
            Assert.That(loaded.farm.energy, Is.EqualTo(100));
        }
        finally { Directory.Delete(directory, true); }
    }
    private void Plant()
    {
        Assert.That(game.Farming.UsePlot(0, 0).Success, Is.True);
        Assert.That(game.Farming.UsePlot(0, 4).Success, Is.True);
    }
    private void FillBackpack() { foreach (var slot in game.Save.inventory.slots) slot.Set("carrot", "Carrot", 99); }
}
