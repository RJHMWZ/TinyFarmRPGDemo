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
                    return new TutorialObjective("SELECT YOUR HOE", "Press 1 or click the Hoe in your hotbar.", save.farm.tutorialHoeSelected ? 1 : 0, 1);
                case FirstDayTutorialStep.TillSoil:
                    return new TutorialObjective("PREPARE THE FIELD", "Use the Hoe on 3 empty farm plots.", progression.Counter("till"), 3);
                case FirstDayTutorialStep.PlantSeeds:
                    return new TutorialObjective("PLANT YOUR SEEDS", "Select Parsnip Seeds and plant 3 tilled plots.", progression.Counter("plant"), 3);
                case FirstDayTutorialStep.WaterCrops:
                    return new TutorialObjective("WATER YOUR CROPS", "Select the Watering Can and water 3 planted plots.", progression.Counter("water"), 3);
                case FirstDayTutorialStep.Sleep:
                    return new TutorialObjective("END THE DAY", "Go to HOME / SLEEP and choose SLEEP & SAVE.", progression.Counter("sleep"), 1);
                case FirstDayTutorialStep.Harvest:
                    return new TutorialObjective("FIRST HARVEST", "Your first crops grew overnight. Harvest one ripe crop.", progression.Counter("harvest"), 1);
                case FirstDayTutorialStep.Ship:
                    return new TutorialObjective("SHIP YOUR CROP", "Take a harvested crop to SHIPPING and ship at least one.", progression.Counter("ship"), 1);
                default:
                    return new TutorialObjective("FIRST DAY COMPLETE", "Your farm is ready. Choose your own next goal!", 1, 1);
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
        return "First day complete! +" + RewardGold + "g and +" + RewardExperience + " Farming XP.";
    }
}
