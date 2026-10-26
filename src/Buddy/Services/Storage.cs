using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Buddy.Models;

namespace Buddy.Services;

public sealed class Storage
{
    public string Folder { get; }
    public BuddySettings Settings { get; }
    public Dictionary<string, int> Stats { get; }
    private readonly JsonSerializerOptions options = new() { WriteIndented = true };

    public Storage(string? folder = null)
    {
        Folder = folder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Buddy");
        Directory.CreateDirectory(Folder);
        Settings = Read<BuddySettings>("settings.json") ?? new();
        Stats = Read<Dictionary<string, int>>("stats.json") ?? new();
        Validate(Settings);
    }

    private T? Read<T>(string file)
    {
        try
        {
            var p = Path.Combine(Folder, file);
            if (!File.Exists(p)) return default;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(p));
        }
        catch (FileNotFoundException)
        {
            return default;
        }
        catch (JsonException)
        {
            var p = Path.Combine(Folder, file);
            if (File.Exists(p))
                File.Copy(p, p + ".invalid-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true);
            return default;
        }
    }

    public static void Validate(BuddySettings s)
    {
        s.Name = string.IsNullOrWhiteSpace(s.Name) || s.Name.Trim().Equals("Selva", StringComparison.OrdinalIgnoreCase) || s.Name.Trim().Equals("PDK", StringComparison.OrdinalIgnoreCase) ? "PSK" : s.Name.Trim();
        s.PersonaTitle = string.IsNullOrWhiteSpace(s.PersonaTitle) ? "Desktop Companion & Focus Guardian" : s.PersonaTitle.Trim();
        s.Mood = string.IsNullOrWhiteSpace(s.Mood) ? "Energetic ⚡" : s.Mood.Trim();
        s.AvatarSize = double.IsFinite(s.AvatarSize) ? Math.Clamp(s.AvatarSize, 140, 260) : 220;
        s.WalkSpeed = double.IsFinite(s.WalkSpeed) ? Math.Clamp(s.WalkSpeed, 40, 240) : 120;
        s.DailyWaterGoal = Math.Clamp(s.DailyWaterGoal > 0 ? s.DailyWaterGoal : 8, 1, 30);
        s.BubbleDuration = double.IsFinite(s.BubbleDuration) ? Math.Clamp(s.BubbleDuration, 5, 120) : 30;

        s.Reminders ??= new();
        foreach (var r in s.Reminders)
        {
            r.IntervalMinutes = Math.Clamp(r.IntervalMinutes, 1, 1440);
            r.Message ??= "Time for a break, {name}!";
            r.Pose = Path.GetFileName(r.Pose ?? "wave.png");
            if (r.StartTime < TimeSpan.Zero || r.StartTime >= TimeSpan.FromDays(1)) r.StartTime = TimeSpan.FromHours(9);
            if (r.EndTime < TimeSpan.Zero || r.EndTime >= TimeSpan.FromDays(1)) r.EndTime = TimeSpan.FromHours(19);
        }

        s.Tasks ??= new();
        foreach (var t in s.Tasks)
        {
            if (string.IsNullOrWhiteSpace(t.Title)) t.Title = "Untitled Task";
            if (string.IsNullOrWhiteSpace(t.Category)) t.Category = "Coding";
            if (string.IsNullOrWhiteSpace(t.Priority)) t.Priority = "Medium";
        }
    }

    public void SaveSettings()
    {
        Validate(Settings);
        Write("settings.json", Settings);
    }

    public int Glasses(DateTime day) => Stats.GetValueOrDefault(day.ToString("yyyy-MM-dd"));

    public void AddGlass()
    {
        var key = DateTime.Today.ToString("yyyy-MM-dd");
        Stats[key] = Stats.GetValueOrDefault(key) + 1;
        Write("stats.json", Stats);
    }

    public void RemoveGlass()
    {
        var key = DateTime.Today.ToString("yyyy-MM-dd");
        var current = Stats.GetValueOrDefault(key);
        if (current > 0)
        {
            Stats[key] = current - 1;
            Write("stats.json", Stats);
        }
    }

    private void Write<T>(string file, T value)
    {
        var p = Path.Combine(Folder, file);
        File.WriteAllText(p + ".tmp", JsonSerializer.Serialize(value, options));
        File.Move(p + ".tmp", p, true);
    }
}
