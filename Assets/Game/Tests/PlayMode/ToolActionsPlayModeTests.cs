using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class FarmLoopPlayModeTests
{
    private FarmWorldTarget Plot(int index) => FarmRuntime.Active.World.Targets.First(x => x.Kind == "plot" && x.PlotIndex == index);
    private void StandNear(FarmWorldTarget target, Vector2 offset)
    {
        var player = FarmRuntime.Active.Player;
        player.position = target.Position + offset;
        player.GetComponent<Rigidbody2D>().position = player.position;
    }
    private IEnumerator FinishToolAction()
    {
        var animation = FarmRuntime.Active.Player.GetComponent<PlayerAnimationController>();
        float deadline = Time.realtimeSinceStartup + 3;
        while (animation.IsActing && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(animation.IsActing, Is.False, "The one-shot action must finish and unlock movement.");
        Assert.That(FarmRuntime.Active.Player.GetComponent<PlayerMovement>().ActionLocked, Is.False);
    }

    [UnityTest]
    public IEnumerator Tools_RenderAllFourDirectionsWithMatchedOverlays()
    {
        var farm = FarmRuntime.Active;
        var interaction = farm.Player.GetComponent<FarmInteraction>();
        var animation = farm.Player.GetComponent<PlayerAnimationController>();
        var offsets = new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        var directions = new[] { PlayerDirection.Down, PlayerDirection.Up, PlayerDirection.Right, PlayerDirection.Left };
        for (int i = 0; i < offsets.Length; i++)
        {
            StandNear(Plot(0), offsets[i] * 0.8f);
            foreach (int slot in new[] { 0, 1, 2, 3 })
            {
                farm.Game.SelectHotbarSlot(slot);
                Assert.That(interaction.TryUseTarget(Plot(0)), Is.True);
                typeof(PlayerAnimationController).GetMethod("AdvanceAction", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(animation, new object[] { 0.19f });
                Assert.That(animation.CurrentDirection, Is.EqualTo(directions[i]));
                var tool = farm.Player.GetComponentsInChildren<SpriteRenderer>().First(x => x.name == "AnimatedTool");
                Assert.That(tool.sprite, Is.SameAs(Resources.Load<FarmActionAssets>("FarmActionAssets").GetTool(animation.CurrentAnimation, directions[i], animation.CurrentFrame)));
                Capture("tool-" + animation.CurrentAnimation + "-" + directions[i] + ".png", 1600, 900);
                animation.CancelAction();
            }
        }
        Assert.That(farm.Game.Save.farm.plots[0].tilled, Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Tools_CommitAtImpactOnceAndReturnToIdle()
    {
        var farm = FarmRuntime.Active;
        var interaction = farm.Player.GetComponent<FarmInteraction>();
        var animation = farm.Player.GetComponent<PlayerAnimationController>();
        var movement = farm.Player.GetComponent<PlayerMovement>();
        StandNear(Plot(0), Vector2.down * 0.8f);
        farm.Game.SelectHotbarSlot(0);
        int energy = farm.Game.Save.farm.energy;
        Assert.That(interaction.TryUseTarget(Plot(0)), Is.True);
        Assert.That(animation.CurrentAnimation, Is.EqualTo(PlayerAnimationType.Hoe));
        Assert.That(animation.CurrentDirection, Is.EqualTo(PlayerDirection.Up));
        Assert.That(movement.ActionLocked, Is.True);
        Assert.That(farm.Game.Save.farm.plots[0].tilled, Is.False, "Starting the animation must not apply the tool.");
        Assert.That(interaction.TryUseTarget(Plot(1)), Is.False, "No overlapping actions.");
        yield return null;
        Assert.That(farm.Player.Find("Skin/AnimatedTool") != null || farm.Player.GetComponentsInChildren<SpriteRenderer>().Any(x => x.name == "AnimatedTool" && x.sprite != null), Is.True);
        yield return FinishToolAction();
        Assert.That(farm.Game.Save.farm.plots[0].tilled, Is.True);
        Assert.That(farm.Game.Save.farm.energy, Is.EqualTo(energy - 2));
        Assert.That(farm.Game.Progression.Counter("till"), Is.EqualTo(1));
        Assert.That(animation.CurrentAnimation, Is.EqualTo(PlayerAnimationType.Idle));
        Assert.That(farm.Player.GetComponentsInChildren<SpriteRenderer>().First(x => x.name == "AnimatedTool").enabled, Is.False);
        farm.Game.SelectHotbarSlot(1);
        Assert.That(interaction.TryUseTarget(Plot(0)), Is.True);
        typeof(PlayerAnimationController).GetMethod("AdvanceAction", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(animation, new object[] { 0.21f });
        Assert.That(farm.ActionFeedback.ActiveParticleCount, Is.GreaterThan(0));
        yield return new WaitForSeconds(0.07f);
        Capture("tool-Water-impact.png", 1600, 900);
        yield return FinishToolAction();
        Assert.That(farm.Game.Save.farm.plots[0].watered, Is.True);
        Assert.That(farm.Game.Save.farm.energy, Is.EqualTo(energy - 4));
    }

    [UnityTest]
    public IEnumerator Tools_PlantHarvestChopAndMineUseExistingRules()
    {
        var farm = FarmRuntime.Active;
        var interaction = farm.Player.GetComponent<FarmInteraction>();
        StandNear(Plot(0), Vector2.down * 0.8f);
        farm.Game.Execute(() => farm.Game.Farming.UsePlot(0, 0));
        farm.Game.SelectHotbarSlot(4);
        Assert.That(interaction.TryUseTarget(Plot(0)), Is.True);
        Assert.That(farm.Game.Inventory.Count("parsnip-seed"), Is.EqualTo(15));
        yield return FinishToolAction();
        Assert.That(farm.Game.Inventory.Count("parsnip-seed"), Is.EqualTo(14));
        farm.Game.Save.farm.plots[0].growth = farm.Game.Content.Crop("parsnip-seed").growthDays;
        int energy = farm.Game.Save.farm.energy;
        Assert.That(interaction.TryUseTarget(Plot(0)), Is.True);
        Assert.That(farm.Game.Inventory.Count("parsnip"), Is.Zero);
        typeof(PlayerAnimationController).GetMethod("AdvanceAction", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(farm.Player.GetComponent<PlayerAnimationController>(), new object[] { 0.18f });
        Assert.That(farm.ActionFeedback.ActiveParticleCount, Is.GreaterThan(0));
        yield return new WaitForSeconds(0.07f);
        Capture("tool-Harvest-impact.png", 1600, 900);
        yield return FinishToolAction();
        Assert.That(farm.Game.Inventory.Count("parsnip"), Is.EqualTo(1));
        Assert.That(farm.Game.Save.farm.energy, Is.EqualTo(energy), "Harvest keeps the existing zero-energy rule.");
        foreach (string kind in new[] { "wood", "stone" })
        {
            var target = farm.World.Targets.First(x => x.Kind == kind);
            StandNear(target, Vector2.down * 0.85f);
            farm.Game.SelectHotbarSlot(kind == "wood" ? 2 : 3);
            Assert.That(interaction.TryUseTarget(target), Is.True);
            Assert.That(farm.Game.Inventory.Count(kind), Is.Zero);
            yield return FinishToolAction();
            Assert.That(farm.Game.Inventory.Count(kind), Is.EqualTo(5));
            Assert.That(farm.Game.Farming.NodeAvailable(target.Id), Is.False);
        }
        Assert.That(farm.Game.Save.farm.energy, Is.EqualTo(energy - 10));
    }

    [UnityTest]
    public IEnumerator Tools_MenuCancelsPendingHitAndPauseFreezesAction()
    {
        var farm = FarmRuntime.Active;
        var animation = farm.Player.GetComponent<PlayerAnimationController>();
        var interaction = farm.Player.GetComponent<FarmInteraction>();
        StandNear(Plot(0), Vector2.down * 0.8f);
        farm.Game.SelectHotbarSlot(0);
        Assert.That(interaction.TryUseTarget(Plot(0)), Is.True);
        root.UI.Open(FarmHubPanel.PanelId, new FarmPanelArgs { page = "backpack" });
        Assert.That(animation.IsActing, Is.False, "Opening UI cancels immediately, even when closed in the same frame.");
        root.UI.Close(FarmHubPanel.PanelId);
        yield return new WaitForSeconds(0.6f);
        Assert.That(farm.Game.Save.farm.plots[0].tilled, Is.False);
        Assert.That(farm.Game.Save.farm.energy, Is.EqualTo(100));
        Assert.That(interaction.TryUseTarget(Plot(0)), Is.True);
        var pauseOwner = new object();
        root.Pause.SetPaused(pauseOwner, true);
        yield return new WaitForSecondsRealtime(0.35f);
        Assert.That(animation.IsActing, Is.True);
        Assert.That(farm.Game.Save.farm.plots[0].tilled, Is.False);
        root.Pause.SetPaused(pauseOwner, false);
        yield return FinishToolAction();
        Assert.That(farm.Game.Save.farm.plots[0].tilled, Is.True);
    }

    [UnityTest]
    public IEnumerator Tools_FullBackpackAndSkippedFramesNeverDuplicateRewards()
    {
        var farm = FarmRuntime.Active;
        var interaction = farm.Player.GetComponent<FarmInteraction>();
        var animation = farm.Player.GetComponent<PlayerAnimationController>();
        StandNear(Plot(0), Vector2.down * 0.8f);
        farm.Game.Save.farm.plots[0].seedId = "parsnip-seed";
        farm.Game.Save.farm.plots[0].growth = farm.Game.Content.Crop("parsnip-seed").growthDays;
        foreach (var slot in farm.Game.Save.inventory.slots) slot.Set("wood", "Wood", 99);
        Assert.That(interaction.TryUseTarget(Plot(0)), Is.True);
        yield return FinishToolAction();
        Assert.That(farm.Game.Farming.IsReady(farm.Game.Save.farm.plots[0]), Is.True);
        Assert.That(farm.Game.Progression.Counter("harvest"), Is.Zero);
        Assert.That(farm.ActionFeedback.ActiveParticleCount, Is.Zero, "Failed harvest must not emit reward effects.");
        farm.Game.Save.inventory.slots[0].Set(null, null, 0);
        Assert.That(interaction.TryUseTarget(Plot(0)), Is.True);
        typeof(PlayerAnimationController).GetMethod("AdvanceAction", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(animation, new object[] { 1f });
        Assert.That(farm.Game.Inventory.Count("parsnip"), Is.EqualTo(1));
        Assert.That(animation.IsActing, Is.False);
        yield return new WaitForSeconds(0.6f);
        Assert.That(farm.Game.Inventory.Count("parsnip"), Is.EqualTo(1));
    }
}
