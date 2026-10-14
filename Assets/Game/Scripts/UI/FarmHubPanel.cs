using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    private TMP_Text quickbarHeading;
    private TMP_Text backpackHeading;
    private TMP_Text controlsHint;
    private readonly List<Action> rowCommands = new List<Action>();
    private readonly List<string> rowTexts = new List<string>();
    private readonly List<Action> actionCommands = new List<Action>();
    private readonly string[] tabIds = { "backpack", "crafting", "journal", "guide" };
    public override void OnCreate()
    {
        ConfigurePresentation();
        close.onClick.AddListener(Close);
        previous.onClick.AddListener(() => { pageNumber = Mathf.Max(0, pageNumber - 1); Refresh(); });
        next.onClick.AddListener(() => { pageNumber++; Refresh(); });
        for (int i = 0; i < tabs.Length; i++)
        { int index = i; tabs[i].onClick.AddListener(() => SelectPage(tabIds[index])); }
        for (int i = 0; i < slots.Length; i++)
        {
            int index = i;
            slots[i].onClick.AddListener(() => SelectSlot(index));
            FarmSlotPointerRelay relay = slots[i].GetComponent<FarmSlotPointerRelay>();
            if (relay == null) relay = slots[i].gameObject.AddComponent<FarmSlotPointerRelay>();
            relay.Bind(index, OnSlotPointerClick);
        }
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
        status.text = "点击物品查看详情，按 Esc 关闭。";
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
        card.localScale = Vector3.one * Mathf.Max(0.1f, Mathf.Min(1f, Mathf.Min((parent.rect.width - 24) / 1280f, (parent.rect.height - 24) / 900f)));
        if (Time.frameCount != openedFrame && (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame || Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)) Close();
    }
    private void Close() => GameRoot.Instance.UI.Close(PanelId);
    private void ShowFeedback(FarmResult result) { status.text = result.Message; status.color = result.Success ? CozyUi.Leaf : new Color(0.65f, 0.2f, 0.12f); }
    private void SelectPage(string id)
    {
        page = id; selection = -1; moveFrom = -1; pageNumber = 0; withdrawing = false;
        status.text = "点击物品或条目查看，按 Esc 关闭。"; status.color = CozyUi.Leaf;
        Refresh();
    }
    private void SelectSlot(int index)
    {
        if (Keyboard.current != null && Keyboard.current.shiftKey.isPressed)
        {
            selection = index;
            moveFrom = -1;
            if (page == "backpack") { game.EquipSlot(index); return; }
            if (page == "storage") { TransferSelectedStack(); return; }
        }
        if (page == "backpack")
        {
            if (moveFrom >= 0)
            {
                if (moveFrom == index)
                {
                    moveFrom = -1;
                    status.text = "已取消移动。";
                }
                else
                {
                    int source = moveFrom;
                    moveFrom = -1;
                    Execute(() => game.InventoryActions.Move(source, index));
                }
                selection = index;
                Refresh();
                return;
            }

            if (!game.Inventory.Data.slots[index].IsEmpty)
            {
                moveFrom = index;
                selection = index;
                status.text = "已拿起物品：点击另一格放下，再点原格取消。";
                Refresh();
                return;
            }
        }
        selection = index;
        Refresh();
    }
    private void OnSlotPointerClick(int index, PointerEventData.InputButton button)
    {
        if (!opened || button != PointerEventData.InputButton.Right) return;
        selection = index;
        moveFrom = -1;
        switch (page)
        {
            case "backpack":
                InventorySlotSaveData slot = game.Inventory.Data.slots[index];
                FarmItem item = game.Content.Item(slot.itemId);
                if (slot.IsEmpty) { status.text = "这是一个空格。"; break; }
                if (item != null && item.kind == FarmItemKind.Food)
                    Execute(() => game.Economy.Eat(index));
                else
                    game.EquipSlot(index);
                break;
            case "shipping":
                Execute(() => game.Economy.Ship(index, 1));
                break;
            case "storage":
                TransferSelectedStack(Keyboard.current != null && Keyboard.current.shiftKey.isPressed);
                break;
            default:
                Refresh();
                break;
        }
    }
    private void TransferSelectedStack() => TransferSelectedStack(false);
    private void TransferSelectedStack(bool halfStack)
    {
        Execute(() => game.InventoryActions.Transfer(withdrawing, selection, halfStack));
    }
    private FarmResult Execute(Func<FarmResult> command) => game.Execute(command, FarmChange.Inventory | FarmChange.Progression);
    private InventoryService CurrentInventory => page == "storage" && withdrawing ? game.Storage : game.Inventory;
    private void Refresh()
    {
        if (!opened || game == null) return;
        summary.text = "金币 " + game.Save.player.money + "　·　体力 " + game.Save.farm.energy + "/100　·　农场等级 " + game.Progression.Level + "　·　经验 " + game.Save.farm.experience;
        foreach (var button in actions) button.gameObject.SetActive(false);
        actionCommands.Clear(); rowCommands.Clear(); rowTexts.Clear();
        bool grid = page == "backpack" || page == "shipping" || page == "storage" || page == "villager";
        quickbarHeading.gameObject.SetActive(grid);
        backpackHeading.gameObject.SetActive(grid);
        quickbarHeading.text = page == "storage" && withdrawing ? "仓库　1–12 格" : "快捷栏　数字键 1–0、-、=";
        backpackHeading.text = page == "storage" && withdrawing ? "仓库　13–24 格" : "背包收纳　13–24 格";
        controlsHint.text = grid ? "左键：选择 / 移动物品\n右键：快捷操作\nShift + 右键：仓库转移半组" : "点击左侧条目进行操作，按 Esc 返回。";
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].gameObject.SetActive(grid);
            if (!grid) continue;
            var slot = CurrentInventory.Data.slots[i];
            var item = game.Content.Item(slot.itemId);
            slotLabels[i].text = slot.IsEmpty ? "" : item?.icon == null
                ? (item?.title ?? "未知物品") + " ×" + slot.count : slot.count > 1 ? "×" + slot.count : "";
            icons[i].sprite = item?.icon; icons[i].enabled = !slot.IsEmpty && icons[i].sprite != null;
            slots[i].image.color = i == moveFrom ? CozyUi.Leaf : i == selection ? CozyUi.Gold :
                i < 12 ? new Color32(249, 233, 194, 255) : new Color32(237, 219, 178, 255);
            slotLabels[i].color = i == moveFrom ? CozyUi.Cream : CozyUi.Ink;
        }
        for (int i = 0; i < tabs.Length; i++) tabs[i].image.color = page == tabIds[i] ? CozyUi.Leaf : CozyUi.WoodLight;
        detail.text = "";
        if (grid) DescribeSelection();
        switch (page)
        {
            case "backpack":
                title.text = "背包";
                controlsHint.text = "左键：拿起 / 放下\n右键：装备 / 食用\nShift + 左键：放入快捷栏\n整理会保留快捷栏顺序。";
                ActionButton("食用", () => { moveFrom = -1; Execute(() => game.Economy.Eat(selection)); });
                ActionButton("放入快捷栏", () =>
                {
                    moveFrom = -1;
                    game.EquipSlot(selection);
                });
                ActionButton("整理物品", () => { moveFrom = -1; selection = -1; Execute(game.InventoryActions.OrganizeBackpack); });
                FarmItem selectedItem = selection >= 0 ? game.Content.Item(game.Inventory.Data.slots[selection].itemId) : null;
                actions[0].interactable = selectedItem != null && selectedItem.energy > 0 && game.Save.farm.energy < 100;
                actions[1].interactable = selectedItem != null;
                break;
            case "shop":
                title.text = "种子商店";
                detail.text = "点击商品购买一个。\n\n每天浇水才能生长。\n不适宜当前季节的作物会枯萎。\n\n出货箱出售，睡觉后结算。";
                foreach (var item in game.Content.items)
                {
                    if (item.buyPrice <= 0) continue;
                    var crop = game.Content.Crop(item.id);
                    if (crop != null && !crop.InSeason(game.Save.world.season)) continue;
                    string id = item.id;
                    Row(item.title + "   |　·　" + item.buyPrice + " 金币\n" + item.description, () => Execute(() => game.Economy.Buy(id, 1)));
                }
                break;
            case "shipping":
                title.text = "出货箱";
                detail.text += "\n\n待结算：" + game.Economy.PendingIncome + " 金币";
                controlsHint.text = "右键：出售一个\n睡觉后结算收入。\n只可取回最近一次出货。";
                var recent = game.Economy.RecoverableShipment;
                if (recent != null) detail.text += "\n可取回：" + (game.Content.Item(recent.itemId)?.title ?? recent.itemId) + " x" + recent.count;
                ActionButton("出售一个", () => Execute(() => game.Economy.Ship(selection, 1)));
                ActionButton("出售整组", () => { int count = selection >= 0 ? game.Inventory.Data.slots[selection].count : 0; Execute(() => game.Economy.Ship(selection, count)); });
                ActionButton("取回上次出货", () => Execute(game.Economy.UndoLastShipment));
                FarmItem shipmentItem = selection >= 0 ? game.Content.Item(game.Inventory.Data.slots[selection].itemId) : null;
                actions[0].interactable = actions[1].interactable = shipmentItem != null && shipmentItem.sellPrice > 0 && shipmentItem.kind != FarmItemKind.Tool;
                actions[actionCommands.Count - 1].interactable = recent != null;
                break;
            case "storage":
                title.text = withdrawing ? "仓库 · 取出物品" : "仓库 · 存入物品";
                controlsHint.text = "右键 / Shift + 左键：转移整组\nShift + 右键：转移半组\n同类归堆：存入仓库已有物品。";
                ActionButton(withdrawing ? "切换到存入" : "切换到取出", () => { withdrawing = !withdrawing; selection = -1; Refresh(); });
                ActionButton("转移整组", TransferSelectedStack);
                ActionButton(withdrawing ? "整理物品" : "同类归堆", () =>
                {
                    selection = -1;
                    Execute(withdrawing ? (Func<FarmResult>)game.InventoryActions.OrganizeStorage : game.InventoryActions.QuickStack);
                });
                actions[1].interactable = selection >= 0 && !CurrentInventory.Data.slots[selection].IsEmpty;
                break;
            case "crafting":
                title.text = "制作";
                detail.text = "点击配方制作。\n材料齐全、背包有空间时才消耗材料。\n\n肥料：每次多收获一个。\n洒水器：浇灌自身和周围四格。\n食物：恢复体力。\n\n采集、收获和任务可获得经验。";
                foreach (var recipe in game.Content.recipes)
                {
                    string id = recipe.id;
                    var text = new StringBuilder(recipe.title + "　等级 " + recipe.requiredLevel + "\n");
                    foreach (var part in recipe.ingredients) text.Append(game.Content.Item(part.itemId)?.title).Append(' ').Append(game.Inventory.Count(part.itemId)).Append('/').Append(part.count).Append("   |　·　");
                    Row(text.ToString(), () => Execute(() => game.Economy.Craft(id)));
                }
                break;
            case "journal":
                title.text = "农场任务";
                detail.text = "完成目标后点击任务领赏。\n领取后解锁后续任务。\n\n之前的操作也计入进度。\n每项奖励只能领取一次。\n\n累计收入：" + game.Save.farm.totalEarnings + " 金币\n经营天数：" + game.Save.farm.daysPlayed;
                foreach (var quest in game.Content.quests)
                {
                    string id = quest.id;
                    string state = game.Progression.Completed(id) ? "已领取" : !game.Progression.Available(quest) ? "未解锁" : Mathf.Min(game.Progression.Counter(quest.counter), quest.target) + "/" + quest.target;
                    Row(quest.title + "  [" + state + "]   +" + quest.goldReward + " 金币\n" + quest.description, () => Execute(() => game.Progression.Claim(id)));
                }
                break;
            case "villager":
                var npc = game.Content.Npc(npcId);
                title.text = npc != null ? npc.title.ToUpperInvariant() : "村民";
                if (npc != null) detail.text = "好感度：" + game.Progression.Social(npcId).friendship + "/1000\n喜欢的礼物：" + game.Content.Item(npc.favoriteItem)?.title + "\n\n赠送物品：" +
                    (selection >= 0 ? game.Content.Item(game.Inventory.Data.slots[selection].itemId)?.title ?? "空格" : "请在左侧选择");
                controlsHint.text = "每天聊天可以增加好感度。\n每位村民每天可收一份礼物。";
                ActionButton("聊天", () => Execute(() => game.Progression.Talk(npcId)));
                ActionButton("赠送所选物品", () => Execute(() => game.Progression.Gift(npcId, selection, game.Inventory)));
                actions[1].interactable = selection >= 0 && !game.Inventory.Data.slots[selection].IsEmpty && game.Content.Item(game.Inventory.Data.slots[selection].itemId)?.kind != FarmItemKind.Tool;
                break;
            case "sleep":
                title.text = "结束今天？";
                detail.text = "明早 06:00 起床。\n\n浇过水的作物生长一天。\n体力恢复，出货结算。\n本次收入：" + game.Economy.PendingIncome + " 金币\n自动保存农场进度。\n\n换季时不适宜的作物会枯萎。\n每天 22:00 自动回家。";
                Row("睡觉并保存\n迎接农场的新一天。", () => { Close(); game.Sleep(); });
                break;
            default:
                title.text = "农场指南";
                detail.text = "村庄地图\n\n东侧住宅：睡觉\n西侧商店：买种子\n东侧铁匠铺：制作\n农田北侧：出货箱、仓库\n出生点南侧：农田\n西南：木材、石头\n东侧：浆果";
                Row("1. 耕地\n按 1 选锄头，靠近农田后点击或按空格。", null);
                Row("2. 播种与浇水\n按 5 选种子播种，按 2 选水壶，每天浇水。", null);
                Row("3. 收获与出售\n点击成熟作物收获，到出货箱出售。", null);
                Row("4. 制作与采集\n用斧头砍树、镐采石，在工作台制作物品。", null);
                Row("5. 认识邻居\n每天聊天和送礼，在任务页领取奖励。", null);
                Row("常用快捷键\nWASD 移动　Tab 任务　I 背包　Esc 菜单", null);
                break;
        }
        int maxPage = Mathf.Max(0, (rowTexts.Count - 1) / rows.Length);
        pageNumber = Mathf.Clamp(pageNumber, 0, maxPage);
        for (int i = 0; i < rows.Length; i++)
        {
            int index = pageNumber * rows.Length + i;
            rows[i].gameObject.SetActive(!grid && index < rowTexts.Count);
            if (index < rowTexts.Count) rowLabels[i].text = rowTexts[index];
            rows[i].interactable = index < rowCommands.Count && rowCommands[index] != null;
        }
        previous.gameObject.SetActive(!grid && maxPage > 0); next.gameObject.SetActive(!grid && maxPage > 0);
        previous.interactable = pageNumber > 0; next.interactable = pageNumber < maxPage;
        CozyUi.TrapNavigation(card);
    }
    private void DescribeSelection()
    {
        if (selection < 0) { detail.text = "请选择物品\n\n点击左侧物品，在这里查看用途和售价。"; return; }
        var slot = CurrentInventory.Data.slots[selection];
        var item = game.Content.Item(slot.itemId);
        detail.text = slot.IsEmpty ? "空格\n\n可以把物品放到这里。" : "<size=34><b>" + (item?.title ?? "未知物品") + "</b></size>\n数量：" + slot.count + "\n\n" + item?.description +
            (item != null && item.sellPrice > 0 ? "\n\n出售单价：" + item.sellPrice + " 金币" : "\n\n此物品不能出售。");
    }

    /// <summary>Shared by prefab authoring and runtime, keeping the two layouts identical.</summary>
    public void ConfigurePresentation()
    {
        card.sizeDelta = new Vector2(1280, 900);
        Place(title.rectTransform, 36, 24, 1080, 64); title.fontSize = 42;
        Place(summary.rectTransform, 38, 91, 1198, 42); summary.fontSize = 24;
        Place((RectTransform)close.transform, 1176, 26, 68, 54);
        close.GetComponentInChildren<TMP_Text>().text = "×";
        string[] labels = { "背包", "制作", "任务", "指南" };
        for (int i = 0; i < tabs.Length; i++)
        {
            Place((RectTransform)tabs[i].transform, 36 + i * 306, 148, 294, 52);
            tabs[i].GetComponentInChildren<TMP_Text>().text = labels[i];
        }
        for (int i = 0; i < slots.Length; i++)
        {
            int row = i / 6;
            Place((RectTransform)slots[i].transform, 36 + i % 6 * 120, 251 + row * 111 + (row >= 2 ? 48 : 0), 108, 100);
            RectTransform icon = icons[i].rectTransform;
            icon.anchorMin = icon.anchorMax = icon.pivot = Vector2.one * 0.5f;
            icon.anchoredPosition = Vector2.zero; icon.sizeDelta = new Vector2(62, 62);
            TMP_Text label = slotLabels[i];
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(7, 5); label.rectTransform.offsetMax = new Vector2(-7, -5);
            label.fontSize = 23; label.alignment = TextAlignmentOptions.BottomRight;
            label.enableWordWrapping = false; label.color = CozyUi.Ink; label.fontStyle = FontStyles.Bold;
        }
        for (int i = 0; i < rows.Length; i++)
        {
            Place((RectTransform)rows[i].transform, 36, 245 + i * 78, 712, 70);
            rowLabels[i].fontSize = 24; rowLabels[i].margin = new Vector4(12, 0, 12, 0);
            rowLabels[i].rectTransform.sizeDelta = new Vector2(-18, -4);
        }
        Place(detail.rectTransform, 800, 258, 410, 380); detail.fontSize = 26; detail.lineSpacing = 2;
        for (int i = 0; i < actions.Length; i++)
        {
            Place((RectTransform)actions[i].transform, 36 + i * 408, 777, 394, 58);
            actionLabels[i].fontSize = 27;
        }
        Place((RectTransform)previous.transform, 36, 725, 180, 38);
        Place((RectTransform)next.transform, 568, 725, 180, 38);
        previous.GetComponentInChildren<TMP_Text>().text = "上一页";
        next.GetComponentInChildren<TMP_Text>().text = "下一页";
        foreach (Button button in new[] { previous, next })
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            label.fontSize = 22;
            label.rectTransform.sizeDelta = new Vector2(-18, -2);
        }
        Place(status.rectTransform, 38, 851, 1200, 38); status.fontSize = 24;
        quickbarHeading = Heading("QuickbarHeading", "快捷栏　数字键 1–0、-、=", 36, 212, 712, 38);
        backpackHeading = Heading("BackpackHeading", "背包收纳　13–24 格", 36, 475, 712, 38);
        Heading("DetailHeading", "物品详情 / 使用说明", 800, 216, 410, 40);
        controlsHint = Heading("ControlsHint", "", 800, 645, 410, 120);
        controlsHint.fontSize = 22; controlsHint.lineSpacing = 1;
        if (card.Find("DetailBackdrop") == null)
        {
            var rect = CozyUi.Rect(card, "DetailBackdrop", Vector2.up, Vector2.up, Vector2.up,
                new Vector2(774, -209), new Vector2(470, 552));
            CozyUi.Panel(rect, new Color32(246, 227, 180, 255), false).raycastTarget = false;
            rect.SetAsFirstSibling();
        }
        foreach (TMP_Text text in card.GetComponentsInChildren<TMP_Text>(true)) text.font = ChineseUiFont.Font;
    }

    private TMP_Text Heading(string name, string value, float x, float y, float width, float height)
    {
        Transform existing = card.Find(name);
        TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : CozyUi.Text(card, name, value, 25,
            TextAlignmentOptions.TopLeft, CozyUi.Ink, Vector2.up, Vector2.up, Vector2.up,
            new Vector2(x, -y), new Vector2(width, height));
        Place(text.rectTransform, x, y, width, height);
        return text;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }
    private void Row(string text, Action command) { rowTexts.Add(text); rowCommands.Add(command); }
    private void ActionButton(string label, Action command)
    {
        int index = actionCommands.Count;
        if (index >= actions.Length) return;
        actionCommands.Add(command); actions[index].gameObject.SetActive(true); actions[index].interactable = true; actionLabels[index].text = label;
    }
}
