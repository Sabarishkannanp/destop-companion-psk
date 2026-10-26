
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Buddy.Models;
using Buddy.Services;
using Path = System.IO.Path;

namespace Buddy;

public partial class DashboardWindow : Window
{
    private readonly Storage storage;
    private readonly ReminderScheduler scheduler;
    private readonly OverlayWindow overlay;
    private readonly MediaPlayer testSound = new();
    private readonly Dictionary<string, BitmapImage> sprites = new();
    private string currentPose = "idle_front.png";
    private string taskFilter = "All";

    public event Action? SettingsChanged;
    private void SaveAppearanceTimes_Click(object sender, RoutedEventArgs e)
    {
        if (!ScheduleGrid.CommitEdit(DataGridEditingUnit.Cell,true) || !ScheduleGrid.CommitEdit(DataGridEditingUnit.Row,true) ||
            !TaskScheduleGrid.CommitEdit(DataGridEditingUnit.Cell,true) || !TaskScheduleGrid.CommitEdit(DataGridEditingUnit.Row,true))
        { MessageBox.Show(this,"Correct the highlighted date, time, or interval first.","Appearance timing"); return; }
        storage.SaveSettings(); RefreshRemindersList(); scheduler.Check();
        AppearanceStatus.Text = "Saved. Times use your computer clock. Pending reminders wait for your response.";
    }

    public DashboardWindow(Storage storage, ReminderScheduler scheduler, OverlayWindow overlay)
    {
        this.storage = storage;
        this.scheduler = scheduler;
        this.overlay = overlay;

        InitializeComponent();
        SourceInitialized += (_, _) => Buddy.Interop.NativeMethods.ApplyDarkTitleBar(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        DataContext = storage.Settings;
        NewTaskDate.SelectedDate = DateTime.Today;
        NewTaskTime.Text = DateTime.Now.AddMinutes(30).ToString("HH:mm");

        LoadSprites();
        LoadCompanionMood();
        UpdateAllMetrics();
        RefreshTasksList();
        RefreshRemindersList();
        RenderStatsChart();

        StartupCheckBox.IsChecked = StartupService.Enabled;
        SoundCheckBox.IsChecked = storage.Settings.SoundEnabled;

        // Set initial active tab style
        HighlightNavButton(NavOverviewBtn);
    }

    private void LoadSprites()
    {
        var dir = Path.Combine(Buddy.Services.AssetPaths.Root, "Avatar");
        if (Directory.Exists(dir))
        {
            foreach (var path in Directory.EnumerateFiles(dir, "*.png"))
            {
                try
                {
                    var b = new BitmapImage();
                    b.BeginInit();
                    b.CacheOption = BitmapCacheOption.OnLoad;
                    b.UriSource = new Uri(path);
                    b.EndInit();
                    b.Freeze();
                    sprites[Path.GetFileName(path)] = b;
                }
                catch (Exception) { }
            }
        }
        UpdateAvatarPreview("idle_front.png");
    }

    private void UpdateAvatarPreview(string pose)
    {
        currentPose = pose;
        if (sprites.TryGetValue(pose, out var img))
        {
            OverviewAvatarImage.Source = img;
            StudioAvatarImage.Source = img;
        }
    }

    private void LoadCompanionMood()
    {
        var mood = storage.Settings.Mood;
        foreach (ComboBoxItem item in CompanionMoodCombo.Items)
        {
            if (item.Content?.ToString() == mood)
            {
                CompanionMoodCombo.SelectedItem = item;
                break;
            }
        }
        if (CompanionMoodCombo.SelectedItem == null && CompanionMoodCombo.Items.Count > 0)
        {
            CompanionMoodCombo.SelectedIndex = 0;
        }
    }

    public void UpdateAllMetrics()
    {
        var name = storage.Settings.Name;
        SidebarAvatarName.Text = name;

        // Greeting
        var hour = DateTime.Now.Hour;
        var timeGreeting = hour < 12 ? "Good morning" : (hour < 17 ? "Good afternoon" : "Good evening");
        GreetingText.Text = $"{timeGreeting}, {name}! ✨";

        // Water metrics
        var glasses = storage.Glasses(DateTime.Today);
        var goal = storage.Settings.DailyWaterGoal > 0 ? storage.Settings.DailyWaterGoal : 8;
        MetricWaterText.Text = $"{glasses} / {goal} Glasses";
        MetricWaterBar.Maximum = goal;
        MetricWaterBar.Value = Math.Min(glasses, goal);

        StatsGlassesCount.Text = glasses.ToString();
        var percent = Math.Min(100, (int)Math.Round((double)glasses / goal * 100));
        StatsGoalPercentageText.Text = $"{percent}% of daily hydration goal achieved ({glasses}/{goal} glasses)";
        StatsProgressBar.Maximum = goal;
        StatsProgressBar.Value = Math.Min(glasses, goal);

        // Tasks metrics
        var activeTasks = storage.Settings.Tasks.Count(t => !t.IsCompleted);
        var completedTasks = storage.Settings.Tasks.Count(t => t.IsCompleted);
        MetricTasksText.Text = $"{activeTasks} Pending";
        MetricTasksSubtext.Text = $"{completedTasks} completed today";
        TaskBadgeText.Text = activeTasks.ToString();

        // Reminders metrics
        var activeReminders = storage.Settings.Reminders.Count(r => r.Enabled);
        MetricRemindersText.Text = $"{activeReminders} Active";
        ReminderBadgeText.Text = activeReminders.ToString();

        // Companion metric
        MetricMoodText.Text = storage.Settings.Mood;
        MetricSpeedSizeText.Text = $"{storage.Settings.AvatarSize:0} DIP · {storage.Settings.WalkSpeed:0} DIP/s";
        AvatarHeightLabel.Text = $"{storage.Settings.AvatarSize:0} DIP";
        WalkSpeedLabel.Text = $"{storage.Settings.WalkSpeed:0} DIP/s";

        // Overview tasks list
        var topPending = storage.Settings.Tasks.Where(t => !t.IsCompleted).Take(4).ToList();
        OverviewTaskItemsControl.ItemsSource = null;
        OverviewTaskItemsControl.ItemsSource = topPending;
        OverviewNoTasksText.Visibility = topPending.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ==================== NAVIGATION ====================

    private void NavOverview_Click(object sender, RoutedEventArgs e)
    {
        SwitchTab(TabOverview, NavOverviewBtn, "Dashboard Overview", "Live companion metrics, focus tasks, and daily hydration");
    }

    private void NavCompanion_Click(object sender, RoutedEventArgs e)
    {
        SwitchTab(TabCompanion, NavCompanionBtn, "My Companion Studio", "Customize avatar appearance, dynamics, and view live poses");
    }

    private void NavTasks_Click(object sender, RoutedEventArgs e)
    {
        SwitchTab(TabTasks, NavTasksBtn, "Task Planner & Goals", "Manage your daily tasks and have Buddy remind you on screen");
    }

    private void NavReminders_Click(object sender, RoutedEventArgs e)
    {
        SwitchTab(TabReminders, NavRemindersBtn, "Reminder Scheduler", "Configure active hydration, stretch, and wellness alerts");
    }

    private void NavStats_Click(object sender, RoutedEventArgs e)
    {
        SwitchTab(TabStats, NavStatsBtn, "Hydration & Progress", "7-day hydration history, daily goals, and streaks");
        RenderStatsChart();
    }

    private void NavSettings_Click(object sender, RoutedEventArgs e)
    {
        SwitchTab(TabSettings, NavSettingsBtn, "Settings & System", "Windows startup, gentle sounds, and pause controls");
    }

    public void SwitchToTab(string tabName)
    {
        switch (tabName)
        {
            case "Companion":
                SwitchTab(TabCompanion, NavCompanionBtn, "My Companion Studio", "Customize avatar appearance, dynamics, and view live poses");
                break;
            case "Tasks":
                SwitchTab(TabTasks, NavTasksBtn, "Task Planner & Goals", "Manage your daily tasks and have Buddy remind you on screen");
                break;
            case "Reminders":
                SwitchTab(TabReminders, NavRemindersBtn, "Reminder Scheduler", "Configure active hydration, stretch, and wellness alerts");
                break;
            case "Stats":
                SwitchTab(TabStats, NavStatsBtn, "Hydration & Progress", "7-day hydration history, daily goals, and streaks");
                RenderStatsChart();
                break;
            case "Settings":
                SwitchTab(TabSettings, NavSettingsBtn, "Settings & System", "Windows startup, gentle sounds, and pause controls");
                break;
            default:
                SwitchTab(TabOverview, NavOverviewBtn, "Dashboard Overview", "Live companion metrics, focus tasks, and daily hydration");
                break;
        }
    }

    private void SwitchTab(FrameworkElement targetTab, Button activeBtn, string title, string subtitle)
    {
        TabOverview.Visibility = Visibility.Collapsed;
        TabCompanion.Visibility = Visibility.Collapsed;
        TabTasks.Visibility = Visibility.Collapsed;
        TabReminders.Visibility = Visibility.Collapsed;
        TabStats.Visibility = Visibility.Collapsed;
        TabSettings.Visibility = Visibility.Collapsed;

        targetTab.Visibility = Visibility.Visible;
        SectionTitle.Text = title;
        SectionSubtitle.Text = subtitle;

        HighlightNavButton(activeBtn);
    }

    private void HighlightNavButton(Button active)
    {
        var navButtons = new[] { NavOverviewBtn, NavCompanionBtn, NavTasksBtn, NavRemindersBtn, NavStatsBtn, NavSettingsBtn };
        foreach (var b in navButtons)
        {
            if (b == active)
            {
                b.Background = (Brush)FindResource("CardHoverBackground");
                b.BorderBrush = (Brush)FindResource("Accent");
            }
            else
            {
                b.Background = Brushes.Transparent;
                b.BorderBrush = Brushes.Transparent;
            }
        }
    }

    // ==================== COMPANION ACTIONS ====================

    private void SummonCompanion_Click(object sender, RoutedEventArgs e)
    {
        scheduler.ShowNow();
    }

    private void DismissCompanion_Click(object sender, RoutedEventArgs e)
    {
        scheduler.Complete(false);
        overlay.Avatar.Leave();
    }

    private void SayFunLine_Click(object sender, RoutedEventArgs e)
    {
        overlay.RequestFun();
    }

    private void PoseSelect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string pose)
        {
            UpdateAvatarPreview(pose);
            overlay.Avatar.TestPose(pose);
        }
    }

    private void StudioPose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string pose)
        {
            UpdateAvatarPreview(pose);
            overlay.Avatar.TestPose(pose);
        }
    }

    private void AvatarHeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (AvatarHeightLabel != null)
        {
            AvatarHeightLabel.Text = $"{e.NewValue:0} DIP";
            storage.Settings.AvatarSize = e.NewValue;
            overlay.Avatar.ApplySize();
            storage.SaveSettings();
        }
    }

    private void WalkSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (WalkSpeedLabel != null)
        {
            WalkSpeedLabel.Text = $"{e.NewValue:0} DIP/s";
            storage.Settings.WalkSpeed = e.NewValue;
            storage.SaveSettings();
        }
    }

    private void SaveCompanionSettings_Click(object sender, RoutedEventArgs e)
    {
        if (CompanionMoodCombo.SelectedItem is ComboBoxItem item)
        {
            storage.Settings.Mood = item.Content?.ToString() ?? "Energetic ⚡";
        }
        storage.SaveSettings();
        overlay.Avatar.ApplySize();
        UpdateAllMetrics();
        SettingsChanged?.Invoke();
        MessageBox.Show("Companion profile updated and saved successfully! ✨", "PSK Charm", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ==================== TASK PLANNER ACTIONS ====================

    private void RefreshTasksList()
    {
        IEnumerable<TaskItem> tasks = storage.Settings.Tasks;
        if (taskFilter == "Active") tasks = tasks.Where(t => !t.IsCompleted);
        else if (taskFilter == "Completed") tasks = tasks.Where(t => t.IsCompleted);

        var list = tasks.OrderBy(t => t.IsCompleted).ThenByDescending(t => t.CreatedAt).ToList();
        TasksListItemsControl.ItemsSource = null;
        TasksListItemsControl.ItemsSource = list;

        NoTasksFilteredNotice.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateAllMetrics();
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        PerformAddTask();
    }

    private void NewTaskTitleBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PerformAddTask();
        }
    }

    private void PerformAddTask()
    {
        var title = NewTaskTitleBox.Text.Trim();
        if (string.IsNullOrEmpty(title))
        {
            MessageBox.Show("Please enter a task title.", "PSK Charm", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DateTime? remindAt = null;
        if (ScheduleNewTask.IsChecked == true)
        {
            if (NewTaskDate.SelectedDate is not DateTime date ||
                !TimeSpan.TryParseExact(NewTaskTime.Text.Trim(), @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out var time) ||
                date.Date + time <= DateTime.Now)
            { TaskEntryStatus.Text = "Choose a future date and a time in HH:mm format (24-hour clock)."; return; }
            remindAt = date.Date + time;
        }
        var category = "Coding";
        if (NewTaskCategoryCombo.SelectedItem is ComboBoxItem cItem && cItem.Content is string cStr)
        {
            category = cStr.Split(' ')[0];
        }

        var priority = "Medium";
        if (NewTaskPriorityCombo.SelectedItem is ComboBoxItem pItem && pItem.Content is string pStr)
        {
            priority = pStr.Split(' ')[0];
        }

        var task = new TaskItem
        {
            Title = title,
            Category = category,
            Priority = priority,
            IsCompleted = false,
            CreatedAt = DateTime.Now,
            RemindAt = remindAt
        };

        storage.Settings.Tasks.Insert(0, task);
        storage.SaveSettings();
        NewTaskTitleBox.Clear();
        TaskEntryStatus.Text = remindAt.HasValue ? $"Task saved. Character will appear at {remindAt:dd MMM yyyy, HH:mm}." : "Task saved without a scheduled reminder.";
        RefreshTasksList();
    }

    private void TaskFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            taskFilter = tag;
            TaskFilterAllBtn.Background = tag == "All" ? (Brush)FindResource("CardHoverBackground") : (Brush)FindResource("PanelBackground");
            TaskFilterActiveBtn.Background = tag == "Active" ? (Brush)FindResource("CardHoverBackground") : (Brush)FindResource("PanelBackground");
            TaskFilterCompletedBtn.Background = tag == "Completed" ? (Brush)FindResource("CardHoverBackground") : (Brush)FindResource("PanelBackground");
            RefreshTasksList();
        }
    }

    private void TaskItemToggle_Click(object sender, RoutedEventArgs e)
    {
        storage.SaveSettings();
        RefreshTasksList();
    }

    private void OverviewTaskToggle_Click(object sender, RoutedEventArgs e)
    {
        storage.SaveSettings();
        UpdateAllMetrics();
    }

    private void RemindTaskWithBuddy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid id)
        {
            var task = storage.Settings.Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                scheduler.ShowTaskReminder(task);
            }
        }
    }

    private void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid id)
        {
            var task = storage.Settings.Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                storage.Settings.Tasks.Remove(task);
                storage.SaveSettings();
                RefreshTasksList();
            }
        }
    }

    private void ClearCompletedTasks_Click(object sender, RoutedEventArgs e)
    {
        var completed = storage.Settings.Tasks.Where(t => t.IsCompleted).ToList();
        if (completed.Count == 0) return;

        foreach (var t in completed)
        {
            storage.Settings.Tasks.Remove(t);
        }
        storage.SaveSettings();
        RefreshTasksList();
    }

    // ==================== REMINDERS ACTIONS ====================

    private void RefreshRemindersList()
    {
        RemindersItemsControl.ItemsSource = null;
        RemindersItemsControl.ItemsSource = storage.Settings.Reminders;
        UpdateAllMetrics();
    }

    private void ReminderToggle_Click(object sender, RoutedEventArgs e)
    {
        storage.SaveSettings();
        UpdateAllMetrics();
    }

    private void TestSpecificReminder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid id)
        {
            var reminder = storage.Settings.Reminders.FirstOrDefault(r => r.Id == id);
            if (reminder != null)
            {
                scheduler.ShowNow(reminder);
            }
        }
    }

    private void TestWaterReminder_Click(object sender, RoutedEventArgs e)
    {
        var water = storage.Settings.Reminders.FirstOrDefault(r => r.Kind == "water");
        scheduler.ShowNow(water);
    }

    private void DeleteReminder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid id)
        {
            var reminder = storage.Settings.Reminders.FirstOrDefault(r => r.Id == id);
            if (reminder != null)
            {
                storage.Settings.Reminders.Remove(reminder);
                storage.SaveSettings();
                RefreshRemindersList();
            }
        }
    }

    private void AddReminder_Click(object sender, RoutedEventArgs e)
    {
        var msg = NewReminderMessageBox.Text.Trim();
        if (string.IsNullOrEmpty(msg))
        {
            msg = "Time for a healthy reset, {name}!";
        }

        if (!int.TryParse(NewReminderIntervalBox.Text.Trim(), out var interval) || interval < 1)
        {
            interval = 45;
        }

        var pose = "drink_water.png";
        if (NewReminderPoseCombo.SelectedItem is ComboBoxItem pItem && pItem.Content is string pStr)
        {
            pose = pStr;
        }

        var kind = "custom";
        if (NewReminderKindCombo.SelectedItem is ComboBoxItem kItem && kItem.Content is string kStr)
        {
            kind = kStr;
        }

        var r = new Reminder
        {
            Message = msg,
            IntervalMinutes = interval,
            Pose = pose,
            Kind = kind,
            Enabled = true,
            NextDue = DateTime.Now.AddMinutes(interval)
        };

        storage.Settings.Reminders.Add(r);
        storage.SaveSettings();
        NewReminderMessageBox.Clear();
        RefreshRemindersList();
    }

    private void PresetWater_Click(object sender, RoutedEventArgs e)
    {
        NewReminderMessageBox.Text = "Hey, {name}!\nDid you drink water?";
        NewReminderIntervalBox.Text = "45";
        SetComboText(NewReminderPoseCombo, "drink_water.png");
        SetComboText(NewReminderKindCombo, "water");
    }

    private void PresetStretch_Click(object sender, RoutedEventArgs e)
    {
        NewReminderMessageBox.Text = "Time to stretch, {name}!\nStand up and move your body.";
        NewReminderIntervalBox.Text = "60";
        SetComboText(NewReminderPoseCombo, "wave.png");
        SetComboText(NewReminderKindCombo, "stretch");
    }

    private void PresetEyes_Click(object sender, RoutedEventArgs e)
    {
        NewReminderMessageBox.Text = "Rest your eyes, {name}!\nLook 20 feet away for 20 seconds.";
        NewReminderIntervalBox.Text = "20";
        SetComboText(NewReminderPoseCombo, "wave.png");
        SetComboText(NewReminderKindCombo, "eyes");
    }

    private void PresetBreath_Click(object sender, RoutedEventArgs e)
    {
        NewReminderMessageBox.Text = "Take 3 deep breaths, {name}!\nReset your mind and refocus.";
        NewReminderIntervalBox.Text = "30";
        SetComboText(NewReminderPoseCombo, "idle_front.png");
        SetComboText(NewReminderKindCombo, "custom");
    }

    private static void SetComboText(ComboBox combo, string text)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (item.Content?.ToString() == text)
            {
                combo.SelectedItem = item;
                break;
            }
        }
    }

    // ==================== STATS & HYDRATION ====================

    private void QuickAddGlass_Click(object sender, RoutedEventArgs e)
    {
        storage.AddGlass();
        UpdateAllMetrics();
        RenderStatsChart();
    }

    private void UndoGlass_Click(object sender, RoutedEventArgs e)
    {
        storage.RemoveGlass();
        UpdateAllMetrics();
        RenderStatsChart();
    }

    private void RenderStatsChart()
    {
        ChartGrid.Children.Clear();
        ChartGrid.ColumnDefinitions.Clear();

        var days = Enumerable.Range(0, 7)
            .Select(i => DateTime.Today.AddDays(i - 6))
            .ToList();

        for (int i = 0; i < days.Count; i++)
        {
            ChartGrid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        var maxGlasses = Math.Max(8, days.Max(d => storage.Glasses(d)));

        for (int i = 0; i < days.Count; i++)
        {
            var day = days[i];
            var count = storage.Glasses(day);
            var isToday = day == DateTime.Today;

            var colStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Value text
            var valText = new TextBlock
            {
                Text = count.ToString(),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = isToday ? (Brush)FindResource("Accent") : (Brush)FindResource("Cyan"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };
            colStack.Children.Add(valText);

            // Bar Border
            var barHeight = Math.Max(8, (double)count / maxGlasses * 130);
            var bar = new Border
            {
                Width = 28,
                Height = barHeight,
                CornerRadius = new CornerRadius(6),
                Background = isToday ? (Brush)FindResource("Accent") : (Brush)FindResource("Cyan"),
                Opacity = count > 0 ? (isToday ? 1.0 : 0.8) : 0.25
            };
            colStack.Children.Add(bar);

            // Date label
            var dateLabel = new TextBlock
            {
                Text = isToday ? "Today" : day.ToString("ddd"),
                FontSize = 11,
                Foreground = isToday ? (Brush)FindResource("Ink") : (Brush)FindResource("MutedText"),
                FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };
            colStack.Children.Add(dateLabel);

            Grid.SetColumn(colStack, i);
            ChartGrid.Children.Add(colStack);
        }
    }

    // ==================== SETTINGS & SYSTEM ====================

    private void StartupCheckBox_Click(object sender, RoutedEventArgs e)
    {
        StartupService.SetEnabled(StartupCheckBox.IsChecked == true);
    }

    private void TestSound_Click(object sender, RoutedEventArgs e)
    {
        var file = Path.Combine(Buddy.Services.AssetPaths.Root, "pop.wav");
        if (File.Exists(file))
        {
            testSound.Open(new Uri(file));
            testSound.Volume = 0.5;
            testSound.Play();
        }
        else
        {
            System.Media.SystemSounds.Asterisk.Play();
        }
    }

    private void Pause30Min_Click(object sender, RoutedEventArgs e)
    {
        scheduler.Complete(false);
        scheduler.Pause(TimeSpan.FromMinutes(30));
        overlay.Avatar.Leave();
        MessageBox.Show("Companion paused for 30 minutes.", "PSK Charm", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Pause1Hour_Click(object sender, RoutedEventArgs e)
    {
        scheduler.Complete(false);
        scheduler.Pause(TimeSpan.FromHours(1));
        overlay.Avatar.Leave();
        MessageBox.Show("Companion paused for 1 hour.", "PSK Charm", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PauseTomorrow_Click(object sender, RoutedEventArgs e)
    {
        var duration = DateTime.Today.AddDays(1).AddHours(9) - DateTime.Now;
        scheduler.Complete(false);
        scheduler.Pause(duration);
        overlay.Avatar.Leave();
        MessageBox.Show("Companion paused until tomorrow morning 9:00 AM.", "PSK Charm", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ResumeCompanion_Click(object sender, RoutedEventArgs e)
    {
        scheduler.Resume();
        MessageBox.Show("Companion resumed! Reminders are now active.", "PSK Charm", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenAppDataFolder_Click(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(storage.Folder))
        {
            Process.Start("explorer.exe", storage.Folder);
        }
    }

    private void OpenAvatarFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.Combine(Buddy.Services.AssetPaths.Root, "Avatar");
        if (Directory.Exists(dir))
        {
            Process.Start("explorer.exe", dir);
        }
    }

    private void OpenDiagLog_Click(object sender, RoutedEventArgs e)
    {
        var log = Path.Combine(storage.Folder, "diagnostics.log");
        if (File.Exists(log))
        {
            Process.Start(new ProcessStartInfo("notepad.exe", log) { UseShellExecute = true });
        }
        else
        {
            MessageBox.Show("No diagnostic errors logged. System running smoothly! ✨", "PSK Charm", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

