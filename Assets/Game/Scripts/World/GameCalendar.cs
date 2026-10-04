using System;

/// <summary>Calendar arithmetic kept separate from presentation so it remains deterministic.</summary>
public static class GameCalendar
{
    public const int DaysPerSeason = 28;
    public static readonly string[] SeasonNames = { "Spring", "Summer", "Fall", "Winter" };

    public static bool AdvanceMinutes(WorldSaveData world, int minutes)
    {
        if (world == null || minutes <= 0) return false;
        int previousDay = world.day;
        world.minute += minutes;
        while (world.minute >= 60)
        {
            world.minute -= 60;
            world.hour++;
        }
        while (world.hour >= 24)
        {
            world.hour -= 24;
            world.day++;
        }
        while (world.day > DaysPerSeason)
        {
            world.day -= DaysPerSeason;
            world.season++;
        }
        while (world.season >= SeasonNames.Length)
        {
            world.season -= SeasonNames.Length;
            world.year++;
        }
        return world.day != previousDay;
    }

    public static string SeasonName(int season) => SeasonNames[Math.Max(0, Math.Min(SeasonNames.Length - 1, season))];

    public static string FormatTime(WorldSaveData world)
    {
        if (world == null) return "--:--";
        return world.hour.ToString("00") + ":" + world.minute.ToString("00");
    }
}
