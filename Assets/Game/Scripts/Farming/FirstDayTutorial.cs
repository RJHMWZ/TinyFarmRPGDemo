using System;

public enum FirstDayTutorialStep
{
    SelectHoe,
    TillSoil,
    PlantSeeds,
    WaterCrops,
    Sleep,
    Harvest,
    Ship,
    Complete
}

public readonly struct TutorialObjective
{
    public readonly string Title;
    public readonly string Instruction;
    public readonly int Progress;
    public readonly int Target;

    public TutorialObjective(string title, string instruction, int progress, int target)
    {
        Title = title;
        Instruction = instruction;
        Progress = progress;
        Target = target;
    }
}

/// <summary>
/// Save-backed first-day guidance. It observes real farm counters, so the HUD can never
/// disagree with the actions that actually changed the farm.
/// </summary>
public sealed class FirstDayTutorial
{
    public const int RewardGold = 200;
    public const int RewardExperience = 50;
    public const int ObjectiveCount = 7;

    private readonly GameSaveData save;
    private readonly FarmProgressionService progression;

    public FirstDayTutorial(GameSaveData save, FarmProgressionService progression)
    {
        this.save = save ?? throw new ArgumentNullException(nameof(save));
        this.progression = progression ?? throw new ArgumentNullException(nameof(progression));
        Refresh(false);
    }

    public FirstDayTutorialStep Step => (FirstDayTutorialStep)save.farm.tutorialStep;
    public bool IsComplete => Step == FirstDayTutorialStep.Complete;
    public int DisplayStep => Math.Min(ObjectiveCount, (int)Step + 1);

    public TutorialObjective Objective
    {
        get
        {
            switch (Step)
            {
                case FirstDayTutorialStep.SelectHoe:
                    return new TutorialObjective("选择锄头", "按 1 或点击快捷栏中的锄头。", save.farm.tutorialHoeSelected ? 1 : 0, 1);
                case FirstDayTutorialStep.TillSoil:
                    return new TutorialObjective("准备农田", "靠近农田，用锄头耕出 3 块土地。", progression.Counter("till"), 3);
                case FirstDayTutorialStep.PlantSeeds:
                    return new TutorialObjective("播下种子", "选择防风草种子，在 3 块耕地上播种。", progression.Counter("plant"), 3);
                case FirstDayTutorialStep.WaterCrops:
                    return new TutorialObjective("给作物浇水", "选择水壶，给种下的 3 块农田浇水。", progression.Counter("water"), 3);
                case FirstDayTutorialStep.Sleep:
                    return new TutorialObjective("结束今天", "前往住宅，选择“睡觉并保存”。", progression.Counter("sleep"), 1);
                case FirstDayTutorialStep.Harvest:
                    return new TutorialObjective("第一次收获", "第一批作物已成熟，收获一个试试。", progression.Counter("harvest"), 1);
                case FirstDayTutorialStep.Ship:
                    return new TutorialObjective("出售作物", "把收获的作物带到出货箱，出售至少一个。", progression.Counter("ship"), 1);
                default:
                    return new TutorialObjective("新手引导完成", "现在开始经营自己的农场吧！", 1, 1);
            }
        }
    }

    public string SelectHotbarItem(string itemId)
    {
        if (Step == FirstDayTutorialStep.SelectHoe && itemId == "hoe")
            save.farm.tutorialHoeSelected = true;
        return Refresh();
    }

    public string Refresh(bool grantReward = true)
    {
        bool advanced;
        do
        {
            advanced = false;
            switch (Step)
            {
                case FirstDayTutorialStep.SelectHoe:
                    advanced = save.farm.tutorialHoeSelected;
                    break;
                case FirstDayTutorialStep.TillSoil:
                    advanced = progression.Counter("till") >= 3;
                    break;
                case FirstDayTutorialStep.PlantSeeds:
                    advanced = progression.Counter("plant") >= 3;
                    break;
                case FirstDayTutorialStep.WaterCrops:
                    advanced = progression.Counter("water") >= 3;
                    break;
                case FirstDayTutorialStep.Sleep:
                    advanced = progression.Counter("sleep") >= 1;
                    break;
                case FirstDayTutorialStep.Harvest:
                    advanced = progression.Counter("harvest") >= 1;
                    break;
                case FirstDayTutorialStep.Ship:
                    advanced = progression.Counter("ship") >= 1;
                    break;
            }

            if (advanced) save.farm.tutorialStep++;
        }
        while (advanced && !IsComplete);

        if (!IsComplete || save.farm.tutorialRewardClaimed || !grantReward) return null;
        if ((long)save.player.money + RewardGold > int.MaxValue) return null;

        save.player.money += RewardGold;
        progression.AddExperience(RewardExperience);
        save.farm.tutorialRewardClaimed = true;
        return "新手引导完成！获得 " + RewardGold + " 金币和 " + RewardExperience + " 农场经验。";
    }
}
