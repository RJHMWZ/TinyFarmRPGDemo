using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class ToolActionAssetTests
{
    [Test]
    public void EveryOutfit_HasAlignedActionsOrAnExplicitOptionalAccessoryFallback()
    {
        string[] guids = AssetDatabase.FindAssets("t:PlayerPartAnimationSet", new[] { "Assets/Game/Data/CharacterAppearance" });
        Assert.That(guids.Length, Is.GreaterThan(0));
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var set = AssetDatabase.LoadAssetAtPath<PlayerPartAnimationSet>(path);
            foreach (var action in new[] { PlayerAnimationType.Hoe, PlayerAnimationType.Pickaxe, PlayerAnimationType.Axe, PlayerAnimationType.Water, PlayerAnimationType.Harvest, PlayerAnimationType.Plant })
            {
                if (!set.HasAction(action))
                {
                    Assert.That(path, Does.EndWith("/Accessory/Accessory_01.asset"), "Only the pack's missing Beret watering layer may use a fallback.");
                    Assert.That(action, Is.EqualTo(PlayerAnimationType.Water));
                    foreach (PlayerDirection direction in System.Enum.GetValues(typeof(PlayerDirection)))
                        Assert.That(set.GetSprite(action, direction, 0), Is.SameAs(set.GetSprite(PlayerAnimationType.Idle, direction, 0)));
                    continue;
                }
                Assert.That(set.GetFrameCount(action), Is.EqualTo(FarmActionAssets.FrameCount(action)));
                foreach (PlayerDirection direction in System.Enum.GetValues(typeof(PlayerDirection)))
                    for (int frame = 0; frame < set.GetFrameCount(action); frame++)
                    {
                        Sprite sprite = set.GetSprite(action, direction, frame);
                        Assert.That(sprite, Is.Not.Null, set.name + " / " + action + " / " + direction + " / " + frame);
                        Assert.That(sprite.pivot / sprite.rect.size, Is.EqualTo(set.IdleSprite.pivot / set.IdleSprite.rect.size));
                        Assert.That(sprite.pixelsPerUnit, Is.EqualTo(set.IdleSprite.pixelsPerUnit));
                    }
            }
        }
    }

    [Test]
    public void ToolOverlays_FollowPackDirectionOrderAndMatchBodyFrameCount()
    {
        var assets = Resources.Load<FarmActionAssets>("FarmActionAssets");
        Assert.That(assets, Is.Not.Null);
        foreach (var action in new[] { PlayerAnimationType.Hoe, PlayerAnimationType.Pickaxe, PlayerAnimationType.Axe, PlayerAnimationType.Water })
        {
            var clip = assets.Find(action);
            int count = FarmActionAssets.FrameCount(action);
            Assert.That(clip.frames.Length, Is.EqualTo(count * 4));
            Assert.That(clip.frames.All(x => x != null), Is.True);
            Assert.That(clip.frames.All(x => x.pivot / x.rect.size == Vector2.one * 0.5f), Is.True, "Tool overlays must use the same centered anchor as the character.");
            Assert.That(assets.GetTool(action, PlayerDirection.Down, 0), Is.SameAs(clip.frames[0]));
            Assert.That(assets.GetTool(action, PlayerDirection.Up, 0), Is.SameAs(clip.frames[count]));
            Assert.That(assets.GetTool(action, PlayerDirection.Right, 0), Is.SameAs(clip.frames[count * 2]));
            Assert.That(assets.GetTool(action, PlayerDirection.Left, 0), Is.SameAs(clip.frames[count * 3]));
        }
    }
}
