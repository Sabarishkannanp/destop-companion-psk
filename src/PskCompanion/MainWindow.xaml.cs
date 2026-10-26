using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PskCompanion.Models;
using PskCompanion.Services;
using Path = System.IO.Path;

namespace PskCompanion;

public partial class MainWindow : Window
{
    private readonly CompanionStorage storage;
    private readonly CompanionSpeechService speech;
    private readonly Stopwatch animationClock = Stopwatch.StartNew();
    private DesktopCompanionWindow? floatingBuddy;
    private BitmapImage? userPhoto;
    private BitmapImage? pixarSprite;
    private readonly System.Windows.Threading.DispatcherTimer reminderTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool awaitingResponse;
    private WorkTask? currentReminderTask;
    private Buddy.OverlayWindow? walkingBuddy;
    private readonly Buddy.Models.BuddySettings walkingSettings = new();
    private string walkingMessage = "Hey, PSK!";
    private bool ClassicWalking => storage.Config.AvatarMode == "Pixar" && storage.Config.VehicleMode == "Walking";

    public MainWindow() : this(new CompanionStorage()) { }
    public MainWindow(CompanionStorage initialStorage)
    {
        storage = initialStorage;
        InitializeComponent();

        speech = new CompanionSpeechService();
        DataContext = storage.Config;

        LoadAvatarAssets();
        ApplyAvatarCustomization();
        RefreshTasks();
        UpdateMetrics();
        VoiceToggleCheckBox.IsChecked = storage.Config.SpeechVoiceEnabled;
        storage.Save();
        HeaderGreetingText.Text = $"Hello, {storage.Config.UserName}!";
        reminderTimer.Tick += (_, _) => CheckScheduledReminders();
        reminderTimer.Start();

        CompositionTarget.Rendering += OnRenderAnimation;
        Closed += (_, _) =>
        {
            CompositionTarget.Rendering -= OnRenderAnimation;
            floatingBuddy?.Close();
            walkingBuddy?.Close();
            reminderTimer.Stop(); storage.Save();
            speech.Dispose();
        };

        // Greet on startup if voice is enabled
        Loaded += async (_, _) =>
        {
            if (storage.Config.AutoShowCompanion) FloatDesktop_Click(this, new RoutedEventArgs());
            if (storage.Config.SpeechVoiceEnabled)
            {
                await speech.GreetGoodMorning(storage.Config.UserName);
            }
        };
    }

    private void LoadAvatarAssets()
    {
        // 1. User photo (me.jpg)
        var photoPath = File.Exists(storage.Config.CustomImagePath) ? storage.Config.CustomImagePath! : Path.Combine(PskCompanion.Services.AssetPaths.Root, "me.jpg");
        if (File.Exists(photoPath))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(photoPath);
                bmp.EndInit();
                bmp.Freeze();
                userPhoto = bmp;
                PhotoAvatarImage.Source = bmp;
            }
            catch (Exception) { }
        }

        // 2. 3D Pixar sprite
        var pixarPath = Path.Combine(PskCompanion.Services.AssetPaths.Root, "Avatar", "idle_front.png");
        if (File.Exists(pixarPath))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(pixarPath);
                bmp.EndInit();
                bmp.Freeze();
                pixarSprite = bmp;
                PixarAvatarImage.Source = bmp;
            }
            catch (Exception) { }
        }
    }

    private void OnRenderAnimation(object? sender, EventArgs e)
    {
        var t = animationClock.Elapsed.TotalSeconds;
        var mode = storage.Config.VehicleMode;

        if (mode == "Airplane")
        {
            // Airplane banking and swaying flight
            AvatarTilt.Angle = Math.Sin(t * 2.2) * 9;
            AvatarBob.Y = Math.Sin(t * 3.5) * 8 - 15;
            AvatarBob.X = Math.Cos(t * 1.5) * 6;
        }
        else if (mode == "Walking")
        {
            // Natural walking step bob
            AvatarTilt.Angle = Math.Sin(t * 4.0) * 3;
            AvatarBob.Y = -Math.Abs(Math.Sin(t * 5.0)) * 7;
            AvatarBob.X = 0;
        }
        else if (mode == "Hover")
        {
            // Floating meditation hover
            AvatarTilt.Angle = Math.Sin(t * 1.2) * 2;
            AvatarBob.Y = Math.Sin(t * 2.0) * 9;
            AvatarBob.X = 0;
        }
        else // Desk
        {
            AvatarTilt.Angle = 0;
            AvatarBob.Y = Math.Sin(t * 1.5) * 2;
            AvatarBob.X = 0;
        }
    }

    private void ApplyAvatarCustomization()
    {
        var cfg = storage.Config;
        PhotoAvatarImage.Source = userPhoto ?? pixarSprite;

        // Avatar Style: Photo / Pixar / Airplane / Jetpack
        if (cfg.AvatarMode == "Pixar")
        {
            PhotoAvatarBorder.Visibility = Visibility.Collapsed;
            PixarAvatarImage.Visibility = Visibility.Visible;
            ActiveModeBadgeText.Text = "🎨 3D Pixar Avatar";
        }
        else if (cfg.AvatarMode == "Airplane")
        {
            PhotoAvatarBorder.Visibility = Visibility.Visible;
            PixarAvatarImage.Visibility = Visibility.Collapsed;
            ActiveModeBadgeText.Text = "✈️ Airplane Pilot PSK";
            cfg.VehicleMode = "Airplane";
            cfg.Accessory = "AviatorGoggles";
        }
        else if (cfg.AvatarMode == "Jetpack")
        {
            PhotoAvatarBorder.Visibility = Visibility.Visible;
            PixarAvatarImage.Visibility = Visibility.Collapsed;
            ActiveModeBadgeText.Text = "🚀 Jetpack Pilot";
            cfg.VehicleMode = "Hover";
            cfg.Accessory = "Wings";
        }
        else // Photo
        {
            PhotoAvatarBorder.Visibility = Visibility.Visible;
            PixarAvatarImage.Visibility = Visibility.Collapsed;
            ActiveModeBadgeText.Text = "👤 My Real Photo";
        }

        // Vehicle Mode Effects
        bool isAirplane = cfg.VehicleMode == "Airplane";
        SkyCanvas.Visibility = isAirplane ? Visibility.Visible : Visibility.Collapsed;
        AirplaneWingsGrid.Visibility = isAirplane ? Visibility.Visible : Visibility.Collapsed;

        // Vehicle buttons highlight
        VehicleAirplaneBtn.Background = isAirplane ? (Brush)FindResource("Accent") : (Brush)FindResource("PanelBackground");
        VehicleWalkingBtn.Background = cfg.VehicleMode == "Walking" ? (Brush)FindResource("Accent") : (Brush)FindResource("PanelBackground");
        VehicleHoverBtn.Background = cfg.VehicleMode == "Hover" ? (Brush)FindResource("Accent") : (Brush)FindResource("PanelBackground");
        VehicleDeskBtn.Background = cfg.VehicleMode == "Desk" ? (Brush)FindResource("Accent") : (Brush)FindResource("PanelBackground");

        // Accessories
        GogglesAccessory.Visibility = cfg.Accessory == "AviatorGoggles" ? Visibility.Visible : Visibility.Collapsed;
        HatAccessory.Visibility = cfg.Accessory == "TopHat" ? Visibility.Visible : Visibility.Collapsed;
        WingsAccessory.Visibility = cfg.Accessory == "Wings" ? Visibility.Visible : Visibility.Collapsed;
        CoffeeAccessory.Visibility = cfg.Accessory == "CoffeeMug" ? Visibility.Visible : Visibility.Collapsed;
        LaptopAccessory.Visibility = cfg.Accessory == "Laptop" ? Visibility.Visible : Visibility.Collapsed;
    }

    // ==================== AVATAR CUSTOMIZER CLICKS ====================

    private void SetAvatarMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            storage.Config.AvatarMode = tag;
            if (tag == "Airplane")
            {
                storage.Config.VehicleMode = "Airplane";
                storage.Config.Accessory = "AviatorGoggles";
                SetSpeechBubble("All clear for takeoff, Captain PSK! ✈️ Flying high above our work tasks!");
                if (storage.Config.SpeechVoiceEnabled) _ = speech.SpeakAsync("All clear for takeoff, Captain PSK! Time to fly high and conquer our work!");
            }
            else if (tag == "Pixar")
            {
                SetSpeechBubble("3D Pixar avatar active! Ready to partner with you, PSK! ✨");
            }
            else
            {
                SetSpeechBubble($"Real photo companion activated! Looking sharp today, {storage.Config.UserName}! 🚀");
            }

            storage.Save();
            ApplyAvatarCustomization();
            floatingBuddy?.UpdateAppearance();
        }
    }

    private void SetVehicleMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string mode)
        {
            storage.Config.VehicleMode = mode;
            storage.Save();
            ApplyAvatarCustomization();
            floatingBuddy?.UpdateAppearance();

            if (mode == "Airplane")
            {
                SetSpeechBubble("Airplane mode engaged! Propellers spinning, soaring through work! ✈️💨");
                if (storage.Config.SpeechVoiceEnabled) _ = speech.SpeakAsync("Airplane mode engaged! Let's soar through our work tasks!");
            }
        }
    }

    private void SetAccessory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string acc)
        {
            storage.Config.Accessory = acc;
            storage.Save();
            ApplyAvatarCustomization();
            floatingBuddy?.UpdateAppearance();
        }
    }

    private void AvatarSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (AvatarScaleLabel != null && AvatarHost != null)
        {
            AvatarScaleLabel.Text = $"{e.NewValue:0} DIP";
            AvatarHost.Height = e.NewValue;
            AvatarHost.Width = e.NewValue * 0.8;
            storage.Config.AvatarHeight = e.NewValue;
            walkingSettings.AvatarSize = e.NewValue; walkingBuddy?.Avatar.ApplySize();
            floatingBuddy?.UpdateAppearance();
        }
    }

    private void UploadCustomPhoto_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Choose Your Photo for PSK Companion",
            Filter = "Images (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(dlg.FileName);
                bmp.EndInit();
                bmp.Freeze();

                userPhoto = bmp;
                PhotoAvatarImage.Source = bmp;
                storage.Config.CustomImagePath = dlg.FileName;
                storage.Config.AvatarMode = "Photo";
                storage.Save();
                ApplyAvatarCustomization();
                floatingBuddy?.UpdateAppearance();
                SetSpeechBubble($"Awesome! New custom photo loaded for {storage.Config.UserName}! ✨");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load image: {ex.Message}", "PSK Companion", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ==================== SPEECH & GREETINGS ====================

    private void SetSpeechBubble(string text)
    {
        SpeechBubbleText.Text = text;
    }

    private async void GreetMorning_Click(object sender, RoutedEventArgs e)
    {
        var hour = DateTime.Now.Hour;
        string greeting = hour < 12 ? "Good morning" : (hour < 17 ? "Good afternoon" : "Good evening");
        var msg = $"{greeting}, {storage.Config.UserName}! ☀️ Time to work! You're ready to crush all your goals today!";
        SetSpeechBubble(msg);
        if (storage.Config.SpeechVoiceEnabled)
        {
            await speech.GreetGoodMorning(storage.Config.UserName);
        }
    }

    private async void TellToWork_Click(object sender, RoutedEventArgs e)
    {
        var pendingTask = storage.Config.Tasks.FirstOrDefault(t => !t.IsCompleted);
        string msg;
        if (pendingTask != null)
        {
            msg = $"Time to work, {storage.Config.UserName}! Focus on: \"{pendingTask.Title}\"! Let's get it done! 🚀";
        }
        else
        {
            msg = $"Time to work, {storage.Config.UserName}! All tasks are cleared or add a new goal to tackle! 🔥";
        }

        SetSpeechBubble(msg);
        if (storage.Config.SpeechVoiceEnabled)
        {
            await speech.TellToWork(storage.Config.UserName, pendingTask?.Title);
        }
    }

    private void RepeatSpeech_Click(object sender, RoutedEventArgs e)
    {
        _ = speech.SpeakAsync(SpeechBubbleText.Text);
    }

    private void DrinkWater_Click(object sender, RoutedEventArgs e)
    {
        storage.Config.GlassesDrankToday++;
        storage.Save();
        UpdateMetrics();
        var msg = $"Great job staying hydrated, {storage.Config.UserName}! That's {storage.Config.GlassesDrankToday} glasses today! 💧";
        SetSpeechBubble(msg);
        speech.PlayPop();
        if (storage.Config.SpeechVoiceEnabled) _ = speech.SpeakAsync(msg);
    }

    private void Stretch_Click(object sender, RoutedEventArgs e)
    {
        var msg = $"Stand up, stretch your back, and relax your shoulders, {storage.Config.UserName}! Reset your energy! 🧘";
        SetSpeechBubble(msg);
        if (storage.Config.SpeechVoiceEnabled) _ = speech.SpeakAsync(msg);
    }

    private void MotivateMe_Click(object sender, RoutedEventArgs e)
    {
        var quotes = new[]
        {
            $"You are unstoppable today, {storage.Config.UserName}! Focus and conquer! ⚡",
            $"Small steps lead to massive victories. Keep building, {storage.Config.UserName}! 🚀",
            $"Focus is your superpower. Knock out that next task! 🎯",
            $"Captain PSK in the cockpit: all tasks will be completed today! ✈️💨"
        };
        var q = quotes[Random.Shared.Next(quotes.Length)];
        SetSpeechBubble(q);
        if (storage.Config.SpeechVoiceEnabled) _ = speech.SpeakAsync(q);
    }

    private void VoiceToggle_Click(object sender, RoutedEventArgs e)
    {
        storage.Config.SpeechVoiceEnabled = VoiceToggleCheckBox.IsChecked == true;
        storage.Save();
    }

    // ==================== TASK MANAGEMENT ====================

    private void RefreshTasks()
    {
        TasksItemsControl.ItemsSource = null;
        TasksItemsControl.ItemsSource = storage.Config.Tasks.OrderBy(t => t.IsCompleted).ThenByDescending(t => t.CreatedAt).ToList();
        UpdateMetrics();
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        PerformAddTask();
    }

    private void TaskInputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PerformAddTask();
        }
    }

    private void PerformAddTask()
    {
        var title = TaskInputBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            MessageBox.Show("Please enter a task name.", "PSK Companion", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var cat = "Work 💼";
        if (TaskCategoryCombo.SelectedItem is ComboBoxItem cItem && cItem.Content is string cStr)
        {
            cat = cStr;
        }

        var priority = "High 🔥";
        if (TaskPriorityCombo.SelectedItem is ComboBoxItem pItem && pItem.Content is string pStr)
        {
            priority = pStr;
        }

        var task = new WorkTask
        {
            Title = title,
            Category = cat,
            Priority = priority,
            IsCompleted = false,
            CreatedAt = DateTime.Now
        };

        storage.Config.Tasks.Insert(0, task);
        storage.Save();
        TaskInputBox.Clear();
        RefreshTasks();

        var msg = $"New task added: \"{title}\". Time to work, {storage.Config.UserName}! 🎯";
        SetSpeechBubble(msg);
        if (storage.Config.SpeechVoiceEnabled) _ = speech.SpeakAsync($"Task added: {title}. Let's get to work!");
    }

    private async void TaskToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is Guid id)
        {
            var task = storage.Config.Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                storage.Save();
                RefreshTasks();
                if (task.IsCompleted)
                {
                    var msg = $"Awesome victory, {storage.Config.UserName}! Task completed: \"{task.Title}\"! 🎉";
                    SetSpeechBubble(msg);
                    if (storage.Config.SpeechVoiceEnabled)
                    {
                        await speech.CelebrateTask(storage.Config.UserName, task.Title);
                    }
                }
            }
        }
    }

    private async void TellToWorkTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid id)
        {
            var task = storage.Config.Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                var msg = $"PSK, time to work on: \"{task.Title}\"! Let's finish this! 💻🔥";
                SetSpeechBubble(msg);
                if (storage.Config.SpeechVoiceEnabled)
                {
                    await speech.TellToWork(storage.Config.UserName, task.Title);
                }
            }
        }
    }

    private void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid id)
        {
            var task = storage.Config.Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                storage.Config.Tasks.Remove(task);
                storage.Save();
                RefreshTasks();
            }
        }
    }

    private void ClearCompletedTasks_Click(object sender, RoutedEventArgs e)
    {
        var completed = storage.Config.Tasks.Where(t => t.IsCompleted).ToList();
        foreach (var t in completed) storage.Config.Tasks.Remove(t);
        storage.Save();
        RefreshTasks();
    }

    // ==================== METRICS & WATER ====================

    private void UpdateMetrics()
    {
        var pending = storage.Config.Tasks.Count(t => !t.IsCompleted);
        var total = storage.Config.Tasks.Count;
        TaskCountSummaryText.Text = $"{pending} tasks pending today ({total - pending} completed)";

        var glasses = storage.Config.GlassesDrankToday;
        var goal = storage.Config.DailyWaterGoal;
        WaterCountLabel.Text = $"{glasses} / {goal} Glasses";
        WaterProgressBar.Maximum = goal;
        WaterProgressBar.Value = Math.Min(glasses, goal);
    }

    private void AddGlass_Click(object sender, RoutedEventArgs e)
    {
        storage.Config.GlassesDrankToday++;
        storage.Save();
        UpdateMetrics();
        speech.PlayPop();
    }

    private void UndoGlass_Click(object sender, RoutedEventArgs e)
    {
        if (storage.Config.GlassesDrankToday > 0)
        {
            storage.Config.GlassesDrankToday--;
            storage.Save();
            UpdateMetrics();
        }
    }

    // ==================== FLOATING DESKTOP BUDDY ====================

    private void FloatDesktop_Click(object sender, RoutedEventArgs e)
    {
        if (ClassicWalking)
        { EnsureWalkingBuddy(); walkingMessage = SpeechBubbleText.Text; if (walkingBuddy!.Avatar.State == Buddy.AvatarState.Offscreen) walkingBuddy.Avatar.Enter(); return; }
        if (floatingBuddy == null || !floatingBuddy.IsLoaded)
        {
            floatingBuddy = new DesktopCompanionWindow(storage, speech, this);
            floatingBuddy.Closed += (_, _) => floatingBuddy = null;
            floatingBuddy.Show();
            SetSpeechBubble("Floating desktop companion launched! I'm now on your screen beside all your windows! 📌");
        }
        else
        {
            floatingBuddy.Show();
            floatingBuddy.Activate();
        }
    }

    public ImageSource? GetCurrentAvatarImage()
    {
        if (storage.Config.AvatarMode == "Pixar") return pixarSprite;
        return userPhoto ?? pixarSprite;
    }
    private void SaveTiming_Click(object sender, RoutedEventArgs e)
    {
        // Read the visible values before any focus or binding updates can replace edits.
        if (!int.TryParse(IntervalMinutesBox.Text, out var minutes) || minutes < 1 || minutes > 1440 ||
            !TimeSpan.TryParse(ActiveFromBox.Text, out var start) || start < TimeSpan.Zero || start >= TimeSpan.FromDays(1) ||
            !TimeSpan.TryParse(ActiveUntilBox.Text, out var end) || end < TimeSpan.Zero || end >= TimeSpan.FromDays(1) ||
            !DateTime.TryParse(NextAppearanceBox.Text, out var next))
        { MessageBox.Show(this,"Use 1–1440 minutes, valid HH:mm hours, a valid date/time, and 5–120 display seconds.","Check timing"); return; }
        var cfg = storage.Config;
        cfg.WorkReminderMinutes = minutes; cfg.ReminderStart = start; cfg.ReminderEnd = end;
        cfg.NextAppearance = next;
        cfg.RemindersEnabled = AutomaticRemindersCheck.IsChecked == true;
        cfg.AutoShowCompanion = AutoShowCheck.IsChecked == true;
        cfg.UserName = string.IsNullOrWhiteSpace(ProfileNameBox.Text) ? "PSK" : ProfileNameBox.Text.Trim();
        storage.Save(); HeaderGreetingText.Text = $"Hello, {cfg.UserName}!";
        TimingStatus.Text = $"Saved. Next repeat: {cfg.NextAppearance:dd MMM, HH:mm}. Task times are checked every 5 seconds.";
    }
    private void SaveTaskTimes_Click(object sender, RoutedEventArgs e)
    {
        if (!TaskTimingGrid.CommitEdit(DataGridEditingUnit.Cell,true) || !TaskTimingGrid.CommitEdit(DataGridEditingUnit.Row,true))
        { MessageBox.Show(this,"Correct the task date/time before saving.","Check task timing"); return; }
        storage.Save(); TimingStatus.Text = "Task reminder times saved.";
    }
    private void CheckScheduledReminders()
    {
        var cfg = storage.Config; var now = DateTime.Now;
        if (!cfg.RemindersEnabled || awaitingResponse || (walkingBuddy != null && walkingBuddy.Avatar.State != Buddy.AvatarState.Offscreen)) return;
        var due = CompanionSchedule.DueTask(cfg,now);
        if (due != null)
        { ShowTaskMessage(due); storage.Save(); return; }
        if (cfg.NextAppearance > now || !CompanionSchedule.InHours(cfg,now)) return;
        cfg.NextAppearance = now.AddMinutes(cfg.WorkReminderMinutes);
        ShowTaskMessage(cfg.Tasks.FirstOrDefault(t => !t.IsCompleted && (!t.RemindAt.HasValue || t.RemindAt <= now))); storage.Save();
    }
    public void ShowNextTaskNow() => ShowTaskMessage(storage.Config.Tasks.FirstOrDefault(t => !t.IsCompleted));
    private void ShowTaskMessage(WorkTask? task)
    {
        if (awaitingResponse) return;
        awaitingResponse = true;
        currentReminderTask = task;
        var message = task == null ? $"Hey, {storage.Config.UserName}! Take a break and drink some water." :
            $"Hey, {storage.Config.UserName}! Time to work on: {task.Title}";
        SetSpeechBubble(message);
        if (ClassicWalking)
        {
            EnsureWalkingBuddy(); walkingMessage = message;
            if (walkingBuddy!.Avatar.State == Buddy.AvatarState.Offscreen) walkingBuddy.Avatar.Enter();
            else { walkingBuddy.Avatar.Remind("wave.png"); walkingBuddy.Speech.Present(message,true,false); }
            if (storage.Config.SpeechVoiceEnabled) _ = speech.SpeakAsync(message);
            return;
        }
        if (floatingBuddy == null || !floatingBuddy.IsLoaded)
        { floatingBuddy = new DesktopCompanionWindow(storage,speech,this); floatingBuddy.Closed += (_,_) => floatingBuddy = null; }
        floatingBuddy.Present(message);
        if (storage.Config.SpeechVoiceEnabled) _ = speech.SpeakAsync(message);
    }
    public void RespondToTask(bool done)
    {
        if (!awaitingResponse) return;
        awaitingResponse = false;
        var water = currentReminderTask == null;
        if (currentReminderTask != null)
        { if (done) { currentReminderTask.IsCompleted = true; currentReminderTask.RemindAt = null; }
          else currentReminderTask.RemindAt = DateTime.Now.AddMinutes(10); }
        if (!done) storage.Config.NextAppearance = DateTime.Now.AddMinutes(10);
        currentReminderTask = null; storage.Save(); RefreshTasks(); floatingBuddy?.Hide();
        if (walkingBuddy != null && walkingBuddy.Avatar.State != Buddy.AvatarState.Offscreen)
        { walkingBuddy.Speech.Present(done ? "Nice, PSK! 💙" : "Okay, PSK. Back in 10 min.",false);
          walkingBuddy.Avatar.Respond(done,done ? (water ? "drink_water.png" : "wave.png") : "sad.png"); }
    }
    private void EnsureWalkingBuddy()
    {
        walkingSettings.Name = storage.Config.UserName; walkingSettings.AvatarSize = storage.Config.AvatarHeight;
        if (walkingBuddy != null) { walkingBuddy.Avatar.ApplySize(); return; }
        walkingBuddy = new Buddy.OverlayWindow(walkingSettings);
        walkingBuddy.Avatar.Arrived += () =>
        { walkingBuddy.Avatar.Remind(currentReminderTask == null ? "drink_water.png" : "wave.png");
          walkingBuddy.Speech.Present(walkingMessage,true,currentReminderTask == null); };
        walkingBuddy.Speech.Confirmed += () => RespondToTask(true);
        walkingBuddy.Speech.Snoozed += () => RespondToTask(false);
        walkingBuddy.Show();
    }
}

