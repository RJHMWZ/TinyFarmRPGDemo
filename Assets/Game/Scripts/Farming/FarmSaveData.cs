using System;
using System.Collections.Generic;

[Serializable]
public sealed class FarmSaveData
{
    public const int Width = 8;
    public const int Height = 5;
    public FarmPlotData[] plots = new FarmPlotData[Width * Height];
    public InventorySaveData storage = new InventorySaveData();
    public List<FarmShipment> shipments = new List<FarmShipment>();
    public List<FarmCounter> counters = new List<FarmCounter>();
    public List<FarmNodeState> resources = new List<FarmNodeState>();
    public List<FarmSocialState> villagers = new List<FarmSocialState>();
    public int energy = 100;
    public int experience;
    public int lastShippingIncome;
    public int totalEarnings;
    public int daysPlayed;
    public float clockSeconds;
    public double playSeconds;
    public int tutorialStep;
    public bool tutorialHoeSelected;
    public bool tutorialFastGrowthUsed;
    public bool tutorialRewardClaimed;
    public void Normalize()
    {
        if (plots == null) plots = new FarmPlotData[Width * Height];
        if (plots.Length != Width * Height) Array.Resize(ref plots, Width * Height);
        for (int i = 0; i < plots.Length; i++)
        {
            if (plots[i] == null) plots[i] = new FarmPlotData();
            plots[i].growth = Math.Max(0, plots[i].growth);
        }
        if (storage == null) storage = new InventorySaveData();
        storage.Normalize();
        if (shipments == null) shipments = new List<FarmShipment>();
        if (counters == null) counters = new List<FarmCounter>();
        if (resources == null) resources = new List<FarmNodeState>();
        if (villagers == null) villagers = new List<FarmSocialState>();
        shipments.RemoveAll(x => x == null || string.IsNullOrEmpty(x.itemId) || x.count <= 0);
        counters.RemoveAll(x => x == null || string.IsNullOrEmpty(x.id));
        resources.RemoveAll(x => x == null || string.IsNullOrEmpty(x.id));
        villagers.RemoveAll(x => x == null || string.IsNullOrEmpty(x.id));
        energy = Math.Max(0, Math.Min(100, energy));
        experience = Math.Max(0, Math.Min(1000000, experience));
        daysPlayed = Math.Max(0, daysPlayed);
        tutorialStep = Math.Max(0, Math.Min((int)FirstDayTutorialStep.Complete, tutorialStep));
        if (tutorialRewardClaimed) tutorialStep = (int)FirstDayTutorialStep.Complete;
        if (float.IsNaN(clockSeconds) || float.IsInfinity(clockSeconds) || clockSeconds < 0) clockSeconds = 0;
        if (double.IsNaN(playSeconds) || double.IsInfinity(playSeconds) || playSeconds < 0) playSeconds = 0;
    }
}

[Serializable] public sealed class FarmPlotData
{
    public bool tilled;
    public bool watered;
    public string seedId;
    public int growth;
    public bool fertilized;
    public bool sprinkler;
}
[Serializable] public sealed class FarmShipment { public string itemId; public int count; public int unitPrice; }
[Serializable] public sealed class FarmCounter { public string id; public int value; }
[Serializable] public sealed class FarmNodeState { public string id; public int availableDay; }
[Serializable] public sealed class FarmSocialState
{
    public string id;
    public int friendship;
    public int lastTalkDay = -1;
    public int lastGiftDay = -1;
}

public readonly struct FarmResult
{
    public readonly bool Success;
    public readonly string Message;
    private FarmResult(bool success, string message) { Success = success; Message = message; }
    public static FarmResult Ok(string text) => new FarmResult(true, text);
    public static FarmResult Fail(string text) => new FarmResult(false, text);
}
