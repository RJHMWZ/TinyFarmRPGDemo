using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>Input and distance checks only; all commands are executed by domain services.</summary>
public sealed class FarmInteraction : MonoBehaviour
{
    private FarmRuntime runtime;
    private PlayerMovement movement;
    private PlayerAnimationController animationController;
    private InputModeService inputModes;
    private Vector2 facing = Vector2.down;
    private float nextAction;
    private readonly HashSet<string> mouseDragTargets = new HashSet<string>();
    public void Initialize(FarmRuntime owner)
    {
        runtime = owner;
        movement = GetComponent<PlayerMovement>();
        animationController = GetComponent<PlayerAnimationController>();
        inputModes = GameRoot.Instance.InputModes;
        inputModes.ModeChanged += OnModeChanged;
        runtime.Game.DayEnded += CancelAction;
    }
    private void CancelAction() { if (animationController != null) animationController.CancelAction(); }
    private void OnModeChanged(GameInputMode mode) { if (mode != GameInputMode.Gameplay) CancelAction(); }
    private void OnDisable() => CancelAction();
    private void OnDestroy()
    {
        CancelAction();
        if (runtime != null && runtime.Game != null) runtime.Game.DayEnded -= CancelAction;
        if (inputModes != null) inputModes.ModeChanged -= OnModeChanged;
    }
    private void Update()
    {
        var root = GameRoot.Instance;
        if (runtime == null || root == null) return;
        if (root.InputModes.CurrentMode != GameInputMode.Gameplay || root.Pause.IsPaused || root.UI.LastClosedFrame == Time.frameCount)
        { runtime.World.SetHighlight(null); return; }
        if (animationController != null && animationController.IsActing) return;
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
            case "plot": case "wood": case "stone": case "berry": TryUseTarget(actionTarget); break;
            default: root.UI.Open(FarmHubPanel.PanelId, new FarmPanelArgs { page = actionTarget.Kind, npcId = actionTarget.Id }); break;
        }
    }
    /// <summary>Starts one action. The target and selected stack are captured until its impact frame.</summary>
    public bool TryUseTarget(FarmWorldTarget target)
    {
        var root = GameRoot.Instance;
        if (runtime == null || target == null || root == null || root.Pause.IsPaused || root.InputModes.CurrentMode != GameInputMode.Gameplay ||
            !runtime.World.Targets.Contains(target) || Vector2.Distance(transform.position, target.Position) > 1.8f ||
            animationController == null || animationController.IsActing) return false;
        var game = runtime.Game;
        int slot = game.Save.inventory.selectedHotbarSlot;
        string held = game.Save.inventory.slots[slot].itemId;
        bool plot = target.Kind == "plot";
        if (!plot && target.Kind != "wood" && target.Kind != "stone" && target.Kind != "berry") return false;
        var crop = plot ? game.Content.Crop(game.Save.farm.plots[target.PlotIndex].seedId) : null;
        bool harvest = plot && game.Farming.IsReady(game.Save.farm.plots[target.PlotIndex]);
        PlayerAnimationType action = harvest || target.Kind == "berry" ? PlayerAnimationType.Harvest :
            held == "hoe" ? PlayerAnimationType.Hoe : held == "watering-can" ? PlayerAnimationType.Water :
            held == "axe" ? PlayerAnimationType.Axe : held == "pickaxe" ? PlayerAnimationType.Pickaxe : PlayerAnimationType.Plant;
        string reward = harvest ? crop.harvestId : plot ? null : target.Kind;
        int count = harvest ? crop.harvestCount + (game.Save.farm.plots[target.PlotIndex].fertilized ? 1 : 0) :
            plot ? 0 : target.Kind == "berry" ? 2 : 5;
        return animationController.PlayAction(action, target.Position - (Vector2)transform.position, () =>
        {
            if (runtime == null || Vector2.Distance(transform.position, target.Position) > 1.8f) return;
            FarmResult result = game.Execute(() =>
            {
                if (game.Save.inventory.slots[slot].itemId != held || plot && game.Farming.IsReady(game.Save.farm.plots[target.PlotIndex]) != harvest)
                    return FarmResult.Fail("The selected item or target changed. Try again.");
                return plot ? game.Farming.UsePlot(target.PlotIndex, slot) : game.Farming.Gather(target.Id, target.Kind, slot);
            });
            runtime.ActionFeedback.Play(action, target, result, reward, count);
        });
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
