using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class FarmLoopPlayModeTests
{
    [UnityTest]
    public IEnumerator ChineseUi_FrontendAndSettingsRenderChinese()
    {
        var issues = new List<string>();
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(GameSceneNames.MainMenu);
        yield return null;
        Capture("chinese-title-1280.png", 1280, 720);
        var menu = Object.FindObjectOfType<MainMenuView>();
        menu.CreateGameButton.onClick.Invoke();
        yield return null;
        var saveView = Object.FindObjectOfType<SaveSelectView>();
        saveView.GetSlotButton(0).onClick.Invoke();
        Capture("chinese-save-select-1280.png", 1280, 720);
        saveView.PrimaryActionButton.onClick.Invoke();
        yield return null;
        var creation = Object.FindObjectOfType<CharacterCreationView>();
        creation.RandomNameButton.onClick.Invoke();
        Assert.That(Regex.IsMatch(creation.NameInput.text, "[\\u4e00-\\u9fff]"), Is.True);
        Capture("chinese-character-1280.png", 1280, 720);
        foreach (TMP_Text text in creation.GetComponentsInChildren<TMP_Text>())
        {
            text.ForceMeshUpdate();
            if (!text.font.HasCharacters(text.text.Replace("\n", ""), out uint[] missing, true, true))
                issues.Add(text.name + " missing glyphs: " + string.Join(",", missing ?? new uint[0]));
            if (text.isTextOverflowing) issues.Add(text.name + " text is overflowing.");
        }
        creation.ReturnButton.onClick.Invoke();
        var panel = (SettingsPanel)root.UI.Open(SettingsPanel.PanelId);
        yield return null;
        foreach (string tab in new[] { "DISPLAY", "AUDIO", "GAMEPLAY", "CONTROLS" })
        {
            panel.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(button => button.name == "Tab" + tab).onClick.Invoke();
            Capture("chinese-settings-" + tab.ToLowerInvariant() + "-1280.png", 1280, 720);
            foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                if (!text.font.HasCharacters(text.text.Replace("\n", ""), out uint[] missing, true, true))
                    issues.Add(text.name + " missing glyphs: " + string.Join(",", missing ?? new uint[0]));
                if (text.isTextOverflowing) issues.Add(tab + "/" + text.name + " text is overflowing.");
            }
        }
        root.UI.Close(SettingsPanel.PanelId);
        Assert.That(issues, Is.Empty);
    }

    [UnityTest]
    public IEnumerator ChineseUi_PagesRenderReadableTextWithCompleteGlyphs()
    {
        var issues = new List<string>();
        FarmGame game = FarmRuntime.Active.Game;
        game.Save.inventory.slots[13].Set("wood", "木材", 35);
        game.Save.inventory.slots[14].Set("berry", "野生浆果", 8);
        string[] pages = { "backpack", "shop", "shipping", "storage", "crafting", "journal", "villager", "sleep", "guide" };
        foreach (string page in pages)
        {
            var panel = (FarmHubPanel)root.UI.Open(FarmHubPanel.PanelId, new FarmPanelArgs { page = page, npcId = "willow" });
            yield return null;
            if (page == "backpack") Click(panel, "Slot4");
            if (page == "shipping") Click(panel, "Slot13");
            if (page == "storage") Click(panel, "Slot14");
            Capture("chinese-" + page + "-1280.png", 1280, 720);
            if (page == "backpack") Capture("chinese-backpack-1024.png", 1024, 768);
            foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                string visible = Regex.Replace(text.text, "<[^>]+>", "");
                if (!text.font.HasCharacters(visible.Replace("\n", ""), out uint[] missing, true, true))
                    issues.Add(page + "/" + text.name + " missing glyphs: " + string.Join(",", missing ?? new uint[0]));
                if (text.isTextOverflowing) issues.Add(page + "/" + text.name + " text is overflowing: " + text.text);
            }
            root.UI.Close(FarmHubPanel.PanelId);
        }
        Assert.That(game.Content.items.All(item => Regex.IsMatch(item.title, "[\\u4e00-\\u9fff]")), Is.True);
        Assert.That(issues, Is.Empty);
    }
}
