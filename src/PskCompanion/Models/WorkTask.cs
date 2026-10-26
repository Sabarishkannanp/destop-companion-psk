using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PskCompanion.Models;

public partial class WorkTask : ObservableObject
{
    [ObservableProperty] private Guid id = Guid.NewGuid();
    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private string category = "Work 💼";
    [ObservableProperty] private string priority = "High 🔥";
    [ObservableProperty] private bool isCompleted;
    [ObservableProperty] private DateTime createdAt = DateTime.Now;
    [ObservableProperty] private string dueDate = "Today";
    [ObservableProperty] private DateTime? completedAt;
    [ObservableProperty] private DateTime? remindAt;

    partial void OnIsCompletedChanged(bool value)
    {
        CompletedAt = value ? DateTime.Now : null;
    }
}

