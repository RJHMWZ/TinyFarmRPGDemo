using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>Input and distance checks only; all commands are executed by domain services.</summary>
public sealed class FarmInteraction : MonoBehaviour
{
    private FarmRuntime runtime;
    private PlayerMovement movement;
    private Vector2 facing = Vector2.down;
    private float nextAction;
    private readonly HashSet<string> mouseDragTargets = new HashSet<string>();
    public void Initialize(FarmRuntime owner) { runtime = owner; movement = GetComponent<PlayerMovement>(); }
    private void Update()
    {
        var root = GameRoot.Instance;
        if (runtime == null || root == null) return;
        if (root.InputModes.CurrentMode != GameInputMode.Gameplay || root.Pause.IsPaused || root.UI.LastClosedFrame == Time.frameCount)
        { runtime.World.SetHighlight(null); return; }
        if (movement != null && movement.MoveInput.sqrMagnitude > 0.01f) facing = movement.MoveInput.normalized;
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        int selected = runtime.Game.Save.inventory.selectedHotbarSlot;
        bool selectionInput = false;
        if (keyboard != null)
        {
            Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0, Key.Minus, Key.Equals };
            for (int i = 0; i < keys.Length; i++) if (keyboard[keys[i]].wasPressedThisFrame) { selected = i; selectionInput = true; }
            if (keyboard.tabKey.wasPressedThisFrame) { root.UI.Open(FarmHubPanel.PanelId, new FarmPanelArgs { page = "journal" }); return; }
        }
        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (mouse != null && Mathf.Abs(mouse.scroll.ReadValue().y) > 0.1f)
        { selected = (selected + (mouse.scroll.ReadValue().y > 0 ? 11 : 1)) % 12; selectionInput = true; }
        if (selectionInput) runtime.Game.SelectHotbarSlot(selected);
        FarmWorldTarget facingTarget = FindTarget((Vector2)transform.position + facing * 0.85f, false);
        FarmWorldTarget pointerTarget = null;
        if (mouse != null && !overUi && Camera.main != null)
        {
            Vector2 point = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
            pointerTarget = FindTarget(point, true);
        }
        FarmWorldTarget target = pointerTarget ?? facingTarget;
        runtime.World.SetHighlight(target);
        runtime.InteractionHint = target == null ? "Move close to a target. Left click: use   Right click / Space: interact" : Describe(target);

        bool keyboardUse = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        bool gamepadUse = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
        bool mouseHeld = mouse != null && !overUi && mouse.leftButton.isPressed;
        if (!mouseHeld) mouseDragTargets.Clear();
        bool mouseUse = mouseHeld && pointerTarget != null && !mouseDragTargets.Contains(pointerTarget.Id);
        bool mouseInteract = mouse != null && !overUi && mouse.rightButton.wasPressedThisFrame;
        FarmWorldTarget actionTarget = mouseUse || mouseInteract ? pointerTarget : facingTarget;

        if (mouseInteract && actionTarget == null)
        {
            FarmItem selectedItem = runtime.Game.Content.Item(runtime.Game.Save.inventory.slots[selected].itemId);
            if (selectedItem != null && selectedItem.kind == FarmItemKind.Food && Time.unscaledTime >= nextAction)
            {
                nextAction = Time.unscaledTime + 0.22f;
                runtime.Game.Execute(() => runtime.Game.Economy.Eat(selected));
            }
            return;
        }

        if (!(keyboardUse || gamepadUse || mouseUse || mouseInteract) || actionTarget == null || Time.unscaledTime < nextAction) return;
        nextAction = Time.unscaledTime + 0.12f;
        if (mouseUse) mouseDragTargets.Add(actionTarget.Id);
        var game = runtime.Game;
        switch (actionTarget.Kind)
        {
            case "plot": game.Execute(() => game.Farming.UsePlot(actionTarget.PlotIndex, selected)); break;
            case "wood": case "stone": case "berry": game.Execute(() => game.Farming.Gather(actionTarget.Id, actionTarget.Kind, selected)); break;
            default: root.UI.Open(FarmHubPanel.PanelId, new FarmPanelArgs { page = actionTarget.Kind, npcId = actionTarget.Id }); break;
        }
    }
    private FarmWorldTarget FindTarget(Vector2 point, bool pointer)
    {
        FarmWorldTarget best = null;
        float distance = pointer ? 0.8f : 1.3f;
        foreach (var target in runtime.World.Targets)
        {
            if (Vector2.Distance(transform.position, target.Position) > 1.8f) continue;
            float d = Vector2.Distance(point, target.Position);
            if (d < distance) { best = target; distance = d; }
        }
        return best;
    }
    private string Describe(FarmWorldTarget target)
    {
        if (target.Kind != "plot") return "Right click / Space: " + target.Title;
        var plot = runtime.Game.Save.farm.plots[target.PlotIndex];
        if (runtime.Game.Farming.IsReady(plot)) return "Left click / Space: Harvest ripe crop";
        FarmCrop crop = runtime.Game.Content.Crop(plot.seedId);
        if (crop != null) return runtime.Game.Content.Item(crop.harvestId).title + "  " + plot.growth + "/" + crop.growthDays + " days | " + (plot.watered ? "Watered" : "Needs water");
        return plot.tilled ? "Tilled plot | Select seeds to plant" : "Empty plot | Select hoe to till";
    }
}
