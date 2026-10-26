using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Buddy.Models;

public partial class TaskItem : ObservableObject
{
    [ObservableProperty] private Guid id = Guid.NewGuid();
    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private string category = "Coding"; // Coding, Health, Work, Personal
    [ObservableProperty] private string priority = "Medium"; // High, Medium, Low
    [ObservableProperty] private bool isCompleted;
    [ObservableProperty] private DateTime createdAt = DateTime.Now;
    [ObservableProperty] private DateTime? completedAt;
    [ObservableProperty] private bool remindWithBuddy = true;
    [ObservableProperty] private DateTime? remindAt;

    partial void OnIsCompletedChanged(bool value)
    {
        CompletedAt = value ? DateTime.Now : null;
    }
}

