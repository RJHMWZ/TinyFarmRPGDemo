using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class FarmPanelArgs { public string page = "backpack"; public string npcId; }

/// <summary>Serialized, cached view. Buttons dispatch commands; no economy or inventory rules live here.</summary>
public sealed class FarmHubPanel : UIPanel
{
    public const string PanelId = "farm-hub";
    [SerializeField] private RectTransform card;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text summary;
    [SerializeField] private TMP_Text detail;
    [SerializeField] private TMP_Text status;
    [SerializeField] private Button close;
    [SerializeField] private Button previous;
    [SerializeField] private Button next;
    [SerializeField] private Button[] tabs;
    [SerializeField] private Button[] slots;
    [SerializeField] private Image[] icons;
    [SerializeField] private TMP_Text[] slotLabels;
    [SerializeField] private Button[] rows;
    [SerializeField] private TMP_Text[] rowLabels;
    [SerializeField] private Button[] actions;
    [SerializeField] private TMP_Text[] actionLabels;
    private FarmGame game;
    private string page;
    private string npcId;
    private int selection = -1;
    private int moveFrom = -1;
    private int pageNumber;
    private int openedFrame;
    private bool opened;
    private bool withdrawing;
    private readonly List<Action> rowCommands = new List<Action>();
    private readonly List<string> rowTexts = new List<string>();
    private readonly List<Action> actionCommands = new List<Action>();
    private readonly string[] tabIds = { "backpack", "crafting", "journal", "guide" };
    public override void OnCreate()
    {
        close.onClick.AddListener(Close);
        previous.onClick.AddListener(() => { pageNumber = Mathf.Max(0, pageNumber - 1); Refresh(); });
        next.onClick.AddListener(() => { pageNumber++; Refresh(); });
        for (int i = 0; i < tabs.Length; i++)
        { int index = i; tabs[i].onClick.AddListener(() => SelectPage(tabIds[index])); }
        for (int i = 0; i < slots.Length; i++)
        { int index = i; slots[i].onClick.AddListener(() => SelectSlot(index)); }
        for (int i = 0; i < rows.Length; i++)
        { int index = i; rows[i].onClick.AddListener(() => { int item = pageNumber * rows.Length + index; if (item < rowCommands.Count) rowCommands[item]?.Invoke(); }); }
        for (int i = 0; i < actions.Length; i++)
        { int index = i; actions[i].onClick.AddListener(() => { if (index < actionCommands.Count) actionCommands[index]?.Invoke(); }); }
    }
    public override void OnOpen(object args)
    {
        if (opened) OnClose();
        game = FarmRuntime.Active != null ? FarmRuntime.Active.Game : null;
        if (game == null) return;
        var context = args as FarmPanelArgs ?? new FarmPanelArgs();
        npcId = context.npcId;
        opened = true;
        openedFrame = Time.frameCount;
        game.Changed += Refresh;
        game.Feedback += ShowFeedback;
        status.text = "Select an item or action. Escape closes this window.";
        SelectPage(context.page);
        close.Select();
    }
    public override void OnClose()
    {
        if (!opened) return;
        opened = false;
        game.Changed -= Refresh;
        game.Feedback -= ShowFeedback;
    }
    private void OnDisable() => OnClose();
    private void Update()
    {
        if (!opened) return;
        var parent = transform as RectTransform;
        card.localScale = Vector3.one * Mathf.Max(0.1f, Mathf.Min(1f, Mathf.Min((parent.rect.width - 24) / 1180f, (parent.rect.height - 24) / 810f)));
        if (Time.frameCount != openedFrame && (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame || Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)) Close();
    }
    private void Close() => GameRoot.Instance.UI.Close(PanelId);
    private void ShowFeedback(FarmResult result) { status.text = result.Message; status.color = result.Success ? CozyUi.Leaf : new Color(0.65f, 0.2f, 0.12f); }
    private void SelectPage(string id) { page = id; selection = -1; moveFrom = -1; pageNumber = 0; withdrawing = false; Refresh(); }
    private void SelectSlot(int index)
    {
        if (moveFrom >= 0 && page == "backpack")
        {
            bool moved = game.Inventory.Move(moveFrom, index);
            moveFrom = -1;
            if (moved) game.NotifyChanged();
        }
        selection = index;
        Refresh();
    }
    private InventoryService CurrentInventory => page == "storage" && withdrawing ? game.Storage : game.Inventory;
    private void Refresh()
    {
        if (!opened || game == null) return;
        summary.text = game.Save.player.money + "g   |   Energy " + game.Save.farm.energy + "/100   |   Farming Lv." + game.Progression.Level + "   |   " + game.Save.farm.experience + " XP";
        title.text = page.ToUpperInvariant();
        foreach (var button in actions) button.gameObject.SetActive(false);
        actionCommands.Clear(); rowCommands.Clear(); rowTexts.Clear();
        bool grid = page == "backpack" || page == "shipping" || page == "storage" || page == "villager";
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].gameObject.SetActive(grid);
            if (!grid) continue;
            var slot = CurrentInventory.Data.slots[i];
            var item = game.Content.Item(slot.itemId);
            slotLabels[i].text = slot.IsEmpty ? (i + 1).ToString() : (item?.title ?? slot.displayName) + "\nx" + slot.count;
            icons[i].sprite = item?.icon; icons[i].enabled = !slot.IsEmpty && icons[i].sprite != null;
            slots[i].image.color = i == selection ? CozyUi.Gold : i < 12 ? CozyUi.WoodLight : CozyUi.Wood;
        }
        for (int i = 0; i < tabs.Length; i++) tabs[i].image.color = page == tabIds[i] ? CozyUi.Leaf : CozyUi.WoodLight;
        detail.text = "";
        if (grid) DescribeSelection();
        switch (page)
        {
            case "backpack":
                title.text = "BACKPACK";
                detail.text += "\n\nFirst 12 slots are your hotbar. Select an item, choose Move, then its destination.";
                ActionButton("MOVE / SWAP", () => { if (selection >= 0 && !game.Inventory.Data.slots[selection].IsEmpty) { moveFrom = selection; status.text = "Select a destination slot."; } });
                ActionButton("EAT", () => game.Execute(() => game.Economy.Eat(selection)));
                ActionButton("EQUIP", () =>
                {
                    if (selection < 0) return;
                    if (selection >= 12)
                    {
                        int hotbar = game.Save.inventory.selectedHotbarSlot;
                        if (game.Inventory.Move(selection, hotbar)) game.SelectHotbarSlot(hotbar);
                    }
                    else game.SelectHotbarSlot(selection);
                });
                break;
            case "shop":
                title.text = "WILLOW SEED SHOP";
                detail.text = "Buy one item per click. Seeds follow the seasons; crops outside their season wither overnight.\n\nWater every growing day. Rain and sprinklers help.\n\nSell crops at the shipping bin; payment arrives after sleep.";
                foreach (var item in game.Content.items)
                {
                    if (item.buyPrice <= 0) continue;
                    var crop = game.Content.Crop(item.id);
                    if (crop != null && !crop.InSeason(game.Save.world.season)) continue;
                    string id = item.id;
                    Row(item.title + "   " + item.buyPrice + "g\n" + item.description, () => game.Execute(() => game.Economy.Buy(id, 1)));
                }
                break;
            case "shipping":
                title.text = "SHIPPING BIN";
                detail.text += "\n\nPending payment: " + game.Economy.PendingIncome + "g\nShipped items are sold when you sleep. Confirm with the buttons below.";
                ActionButton("SHIP 1", () => game.Execute(() => game.Economy.Ship(selection, 1)));
                ActionButton("SHIP STACK", () => { int count = selection >= 0 ? game.Inventory.Data.slots[selection].count : 0; game.Execute(() => game.Economy.Ship(selection, count)); });
                break;
            case "storage":
                title.text = withdrawing ? "STORAGE > BACKPACK" : "BACKPACK > STORAGE";
                detail.text += "\n\nStore items safely in the farm chest. Both inventories are saved with your farm.";
                ActionButton(withdrawing ? "DEPOSIT VIEW" : "WITHDRAW VIEW", () => { withdrawing = !withdrawing; selection = -1; Refresh(); });
                ActionButton("TRANSFER STACK", () => game.Execute(() =>
                {
                    if (selection < 0) return FarmResult.Fail("Select a stack.");
                    var source = CurrentInventory;
                    return source.TransferTo(withdrawing ? game.Inventory : game.Storage, selection, source.Data.slots[selection].count) ? FarmResult.Ok("Stack transferred.") : FarmResult.Fail("Transfer failed. Check available space.");
                }));
                break;
            case "crafting":
                title.text = "CRAFTING";
                detail.text = "Crafting consumes all required materials only when the result fits.\n\nFertilizer: +1 yield for the planted crop.\nSprinkler: waters its plot and four neighbors each morning.\nFood: restores energy from the backpack.\n\nEarn XP by gathering, harvesting and completing quests.";
                foreach (var recipe in game.Content.recipes)
                {
                    string id = recipe.id;
                    var text = new StringBuilder(recipe.title + "   Lv." + recipe.requiredLevel + "\n");
                    foreach (var part in recipe.ingredients) text.Append(game.Content.Item(part.itemId)?.title).Append(' ').Append(game.Inventory.Count(part.itemId)).Append('/').Append(part.count).Append("   ");
                    Row(text.ToString(), () => game.Execute(() => game.Economy.Craft(id)));
                }
                break;
            case "journal":
                title.text = "FARM JOURNAL";
                detail.text = "Goals count your lifetime actions, including work done before a quest unlocks.\n\nClick a completed objective to claim its reward once. Claiming unlocks the next chapter.\n\nTotal earned: " + game.Save.farm.totalEarnings + "g\nDays on farm: " + game.Save.farm.daysPlayed;
                foreach (var quest in game.Content.quests)
                {
                    string id = quest.id;
                    string state = game.Progression.Completed(id) ? "CLAIMED" : !game.Progression.Available(quest) ? "LOCKED" : Mathf.Min(game.Progression.Counter(quest.counter), quest.target) + "/" + quest.target;
                    Row(quest.title + "  [" + state + "]   +" + quest.goldReward + "g\n" + quest.description, () => game.Execute(() => game.Progression.Claim(id)));
                }
                break;
            case "villager":
                var npc = game.Content.Npc(npcId);
                title.text = npc != null ? npc.title.ToUpperInvariant() : "VILLAGER";
                if (npc != null) detail.text += "\n\nFriendship: " + game.Progression.Social(npcId).friendship + "/1000\nTalk and give one gift each day.\nFavorite: " + game.Content.Item(npc.favoriteItem)?.title;
                ActionButton("TALK", () => game.Execute(() => game.Progression.Talk(npcId)));
                ActionButton("GIVE SELECTED", () => game.Execute(() => game.Progression.Gift(npcId, selection, game.Inventory)));
                break;
            case "sleep":
                title.text = "END THE DAY?";
                detail.text = "Sleep until 06:00 tomorrow.\n\nWatered crops grow one day. Energy refills. Shipping pays " + game.Economy.PendingIncome + "g. The farm is saved automatically.\n\nOut-of-season crops wither.\nAt 22:00, you automatically return home.";
                Row("SLEEP & SAVE\nStart a fresh day on your farm.", () => { Close(); game.Sleep(); });
                break;
            default:
                title.text = "FIELD GUIDE";
                detail.text = "FARM MAP\n\nNorthwest: home and pond\nNorth: seed shop, shipping, storage, workbench\nCenter-east: 40 farm plots\nWest: villagers\nSouthwest: wood and stone\nEast: berry bush\n\nAutosave: every 2 active minutes and after sleep. Manual save is in the farm menu.";
                Row("1. PREPARE\nMove with WASD/arrows. Select hoe with 1. Stand near a plot and click it or press Space.", null);
                Row("2. PLANT & WATER\nSelect seeds with 5; plant in tilled soil. Select watering can with 2; water every day.", null);
                Row("3. HARVEST & TRADE\nInteract with ripe crops using any item. Take crops to the northern shipping bin.", null);
                Row("4. BUILD YOUR FARM\nUse axe on trees and pickaxe on rocks. Craft fertilizer, food and sprinklers.", null);
                Row("5. MEET THE NEIGHBORS\nTalk and gift once a day. Claim journal rewards to unlock the next goal.", null);
                Row("CONTROLS\nSpace / click: use   1-0, -, = / wheel: hotbar   Tab: journal   I: backpack   Esc: menu", null);
                break;
        }
        int maxPage = Mathf.Max(0, (rowTexts.Count - 1) / rows.Length);
        pageNumber = Mathf.Clamp(pageNumber, 0, maxPage);
        for (int i = 0; i < rows.Length; i++)
        {
            int index = pageNumber * rows.Length + i;
            rows[i].gameObject.SetActive(!grid && index < rowTexts.Count);
            if (index < rowTexts.Count) rowLabels[i].text = rowTexts[index];
        }
        previous.gameObject.SetActive(!grid && maxPage > 0); next.gameObject.SetActive(!grid && maxPage > 0);
        previous.interactable = pageNumber > 0; next.interactable = pageNumber < maxPage;
        CozyUi.TrapNavigation(card);
    }
    private void DescribeSelection()
    {
        if (selection < 0) { detail.text = "Select an item to inspect it."; return; }
        var slot = CurrentInventory.Data.slots[selection];
        var item = game.Content.Item(slot.itemId);
        detail.text = slot.IsEmpty ? "Empty slot" : (item?.title ?? slot.displayName) + " x" + slot.count + "\n\n" + item?.description + "\n\nShipping: " + (item?.sellPrice ?? 0) + "g each";
    }
    private void Row(string text, Action command) { rowTexts.Add(text); rowCommands.Add(command); }
    private void ActionButton(string label, Action command)
    {
        int index = actionCommands.Count;
        if (index >= actions.Length) return;
        actionCommands.Add(command); actions[index].gameObject.SetActive(true); actionLabels[index].text = label;
    }
}
