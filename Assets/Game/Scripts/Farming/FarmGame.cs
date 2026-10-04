using System;

/// <summary>Session-scoped business facade. No GameObjects, input, UI, or filesystem dependencies.</summary>
public sealed class FarmGame
{
    public GameSaveData Save { get; }
    public FarmContent Content { get; }
    public InventoryService Inventory { get; }
    public InventoryService Storage { get; }
    public FarmingService Farming { get; }
    public FarmEconomyService Economy { get; }
    public FarmProgressionService Progression { get; }
    public event Action Changed;
    public event Action<FarmResult> Feedback;
    public event Action DayEnded;
    public FarmGame(GameSaveData save, FarmContent content)
    {
        Save = save ?? throw new ArgumentNullException(nameof(save));
        Content = content ?? throw new ArgumentNullException(nameof(content));
        save.Normalize();
        Inventory = new InventoryService(save.inventory, content);
        Storage = new InventoryService(save.farm.storage, content);
        Progression = new FarmProgressionService(save, content);
        Economy = new FarmEconomyService(save, content, Inventory, Progression);
        Farming = new FarmingService(save, content, Inventory, Progression);
    }
    public FarmResult Execute(Func<FarmResult> command)
    {
        FarmResult result = command();
        if (result.Success) Changed?.Invoke();
        Feedback?.Invoke(result);
        return result;
    }
    public void NotifyChanged() => Changed?.Invoke();
    public void Tick(float seconds)
    {
        if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
        Save.farm.playSeconds += seconds;
        int whole = (int)Math.Min(int.MaxValue, Math.Floor(Save.farm.playSeconds));
        Save.metadata.playTimeSeconds += whole;
        Save.farm.playSeconds -= whole;
        Save.farm.clockSeconds += seconds;
        float step = Math.Max(0.1f, Content.secondsPerTenMinutes);
        if (Save.farm.clockSeconds < step) return;
        int steps = Math.Min(144, (int)(Save.farm.clockSeconds / step));
        Save.farm.clockSeconds -= steps * step;
        for (int i = 0; i < steps; i++)
        {
            GameCalendar.AdvanceMinutes(Save.world, 10);
            if (Save.world.hour >= Content.dayEndHour || Save.world.hour < 6) { Sleep(); return; }
        }
        Changed?.Invoke();
    }
    public FarmResult Sleep()
    {
        int minutesUntilMorning = (24 - Save.world.hour + 6) * 60 - Save.world.minute;
        if (Save.world.hour < 6) minutesUntilMorning = (6 - Save.world.hour) * 60 - Save.world.minute;
        GameCalendar.AdvanceMinutes(Save.world, minutesUntilMorning);
        Save.farm.daysPlayed++;
        // Deterministic weather is stable across saving/loading and reproducible in tests.
        bool rain = Save.world.season != 3 && (Save.farm.daysPlayed * 17 + 3) % 7 < 2;
        Save.world.weatherId = rain ? "rainy" : "sunny";
        Farming.EndDay(Save.world.season, rain);
        int income = Economy.SettleShipping();
        Save.farm.energy = 100;
        Save.farm.clockSeconds = 0;
        Progression.Record("sleep");
        Changed?.Invoke();
        FarmResult result = FarmResult.Ok("Good morning! Shipping income: " + income + "g. " + (rain ? "Rain waters the fields today." : "Remember to water your crops."));
        Feedback?.Invoke(result);
        DayEnded?.Invoke();
        return result;
    }
}
