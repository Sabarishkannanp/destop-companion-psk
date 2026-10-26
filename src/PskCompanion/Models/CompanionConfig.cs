using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PskCompanion.Models;

public partial class CompanionConfig : ObservableObject
{
    [ObservableProperty] private string userName = "PSK";
    [ObservableProperty] private string companionTitle = "Work & Focus Partner";
    [ObservableProperty] private string avatarMode = "Photo"; // "Photo", "Pixar", "Airplane", "Jetpack", "Custom"
    [ObservableProperty] private string? customImagePath;
    [ObservableProperty] private string vehicleMode = "Airplane"; // "Airplane", "Walking", "Hover", "Desk"
    [ObservableProperty] private string accessory = "AviatorGoggles"; // "None", "AviatorGoggles", "Wings", "TopHat", "CoffeeMug", "Laptop"
    [ObservableProperty] private bool speechVoiceEnabled = true;
    [ObservableProperty] private string greetingStyle = "Motivational 🔥";
    [ObservableProperty] private int workReminderMinutes = 30;
    [ObservableProperty] private bool remindersEnabled = true;
    [ObservableProperty] private bool autoShowCompanion = true;
    [ObservableProperty] private TimeSpan reminderStart = TimeSpan.FromHours(9);
    [ObservableProperty] private TimeSpan reminderEnd = TimeSpan.FromHours(19);
    [ObservableProperty] private DateTime nextAppearance = DateTime.Now.AddMinutes(30);
    [ObservableProperty] private int displaySeconds = 30;
    [ObservableProperty] private bool classicDesktopApplied;
    [ObservableProperty] private double avatarHeight = 220;
    [ObservableProperty] private int dailyWaterGoal = 8;
    [ObservableProperty] private int glassesDrankToday = 2;
    [ObservableProperty] private string lastActiveDate = DateTime.Today.ToString("yyyy-MM-dd");

    public ObservableCollection<WorkTask> Tasks { get; set; } = new()
    {
        new() { Title = "Build and test client features 💻", Category = "Coding 💻", Priority = "High 🔥" },
        new() { Title = "Review task backlog and project deadlines 📋", Category = "Work 💼", Priority = "High 🔥" },
        new() { Title = "Drink 500ml water and stay hydrated 💧", Category = "Health 💧", Priority = "Medium ⚡" }
    };
}

