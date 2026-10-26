using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Buddy.Models;

public partial class BuddySettings : ObservableObject
{
    [ObservableProperty] private string name = "PSK";
    [ObservableProperty] private string personaTitle = "Desktop Companion & Focus Guardian";
    [ObservableProperty] private string mood = "Energetic ⚡";
    [ObservableProperty] private double avatarSize = 220;
    [ObservableProperty] private double walkSpeed = 120;
    [ObservableProperty] private bool soundEnabled = true;
    [ObservableProperty] private int dailyWaterGoal = 8;
    [ObservableProperty] private double bubbleDuration = 30;
    [ObservableProperty] private bool bobEnabled = true;
    [ObservableProperty] private bool breathingEnabled = true;
    [ObservableProperty] private DateTime pausedUntil;

    public ObservableCollection<Reminder> Reminders { get; set; } = new()
    {
        new() { Message = "Hey, {name}!\nDid you drink water?", Kind="water", Pose="drink_water.png", IntervalMinutes=45 },
        new() { Message = "Time to stretch, {name}!", IntervalMinutes = 60, NextDue=DateTime.Now.AddMinutes(60), Pose="wave.png", Kind="stretch" },
        new() { Message = "Rest your eyes, {name}!\nLook 20 feet away for 20 seconds.", IntervalMinutes=20, NextDue=DateTime.Now.AddMinutes(20), Enabled=false, Pose="wave.png", Kind="eyes" }
    };

    public ObservableCollection<TaskItem> Tasks { get; set; } = new()
    {
        new() { Title = "Review code and test PSK Charm companion 🚀", Category = "Coding", Priority = "High" },
        new() { Title = "Drink 500ml water and stay hydrated 💧", Category = "Health", Priority = "High" },
        new() { Title = "Take a short screen break & stretch 🧘", Category = "Health", Priority = "Medium" }
    };
}
