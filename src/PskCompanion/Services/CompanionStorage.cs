using System;
using System.IO;
using System.Text.Json;
using PskCompanion.Models;

namespace PskCompanion.Services;

public sealed class CompanionStorage
{
    public string Folder { get; }
    public CompanionConfig Config { get; }
    private readonly string filePath;
    private readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

    public CompanionStorage(string? folder = null)
    {
        Folder = folder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PskCompanion");
        Directory.CreateDirectory(Folder);
        filePath = Path.Combine(Folder, "config.json");

        Config = Load();
        ValidateAndResetDaily();
    }

    private CompanionConfig Load()
    {
        try
        {
            if (File.Exists(filePath))
            {
                var text = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<CompanionConfig>(text, jsonOptions) ?? new();
            }
        }
        catch (Exception) { }
        return new();
    }

    private void ValidateAndResetDaily()
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        if (Config.LastActiveDate != today)
        {
            Config.LastActiveDate = today;
            Config.GlassesDrankToday = 0;
            Save();
        }

        Config.UserName = string.IsNullOrWhiteSpace(Config.UserName) || Config.UserName.Trim().Equals("Selva", StringComparison.OrdinalIgnoreCase) || Config.UserName.Trim().Equals("PDK", StringComparison.OrdinalIgnoreCase) ? "PSK" : Config.UserName.Trim();
        Config.CompanionTitle = string.IsNullOrWhiteSpace(Config.CompanionTitle) ? "Work & Focus Partner" : Config.CompanionTitle.Trim();
        Config.DailyWaterGoal = Math.Max(1, Config.DailyWaterGoal);
        Config.WorkReminderMinutes = Math.Clamp(Config.WorkReminderMinutes, 1, 1440);
        Config.DisplaySeconds = Math.Clamp(Config.DisplaySeconds, 5, 120);
        Config.Tasks ??= new();
        if (!Config.ClassicDesktopApplied)
        { Config.AvatarMode = "Pixar"; Config.VehicleMode = "Walking"; Config.Accessory = "None"; Config.ClassicDesktopApplied = true; }
        Config.AvatarHeight = double.IsFinite(Config.AvatarHeight) ? Math.Clamp(Config.AvatarHeight,150,260) : 220;
        if (Config.ReminderStart < TimeSpan.Zero || Config.ReminderStart >= TimeSpan.FromDays(1)) Config.ReminderStart = TimeSpan.FromHours(9);
        if (Config.ReminderEnd < TimeSpan.Zero || Config.ReminderEnd >= TimeSpan.FromDays(1)) Config.ReminderEnd = TimeSpan.FromHours(19);
    }

    public void Save()
    {
        try
        {
            var tmp = filePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Config, jsonOptions));
            File.Move(tmp, filePath, true);
        }
        catch (Exception) { }
    }
}

