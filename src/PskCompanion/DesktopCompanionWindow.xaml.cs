using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PskCompanion.Services;

namespace PskCompanion;

public partial class DesktopCompanionWindow : Window
{
    private readonly CompanionStorage storage;
    private readonly CompanionSpeechService speech;
    private readonly MainWindow mainWindow;
    private readonly Stopwatch animationClock = Stopwatch.StartNew();
    private readonly DispatcherTimer nudgeTimer = new();
    private BitmapImage? userPhoto;
    private BitmapImage? pixarSprite;

    public DesktopCompanionWindow(CompanionStorage storage, CompanionSpeechService speech, MainWindow mainWindow)
    {
        InitializeComponent();

        this.storage = storage;
        this.speech = speech;
        this.mainWindow = mainWindow;

        // Position in bottom-right corner of primary screen
        var work = SystemParameters.WorkArea;
        Left = work.Right - Width - 30;
        Top = work.Bottom - Height - 20;

        LoadImages();
        UpdateAppearance();

        CompositionTarget.Rendering += OnRender;
        // The dashboard owns the schedule; one timer prevents duplicate reminders.

        Closed += (_, _) =>
        {
            CompositionTarget.Rendering -= OnRender;
            nudgeTimer.Stop();
        };
    }

    private void LoadImages()
    {
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
                FloatingPhotoImage.Source = bmp;
            }
            catch (Exception) { }
        }

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
                FloatingPixarImage.Source = bmp;
            }
            catch (Exception) { }
        }
    }

    private void OnRender(object? sender, EventArgs e)
    {
        var t = animationClock.Elapsed.TotalSeconds;
        var mode = storage.Config.VehicleMode;

        if (mode == "Airplane")
        {
            FloatTilt.Angle = Math.Sin(t * 2.5) * 8;
            FloatBob.Y = Math.Sin(t * 3.2) * 7;
            FloatBob.X = Math.Cos(t * 1.8) * 5;
        }
        else if (mode == "Walking")
        {
            FloatTilt.Angle = Math.Sin(t * 3.5) * 2;
            FloatBob.Y = -Math.Abs(Math.Sin(t * 4.5)) * 6;
            FloatBob.X = 0;
        }
        else
        {
            FloatTilt.Angle = 0;
            FloatBob.Y = Math.Sin(t * 1.8) * 4;
            FloatBob.X = 0;
        }
    }

    public void UpdateAppearance()
    {
        var cfg = storage.Config;
        FloatingPhotoImage.Source = mainWindow.GetCurrentAvatarImage() ?? userPhoto ?? pixarSprite;

        if (cfg.AvatarMode == "Pixar")
        {
            FloatingPhotoBorder.Visibility = Visibility.Collapsed;
            FloatingPixarImage.Visibility = Visibility.Visible;
        }
        else
        {
            FloatingPhotoBorder.Visibility = Visibility.Visible;
            FloatingPixarImage.Visibility = Visibility.Collapsed;
        }

        bool isAirplane = cfg.VehicleMode == "Airplane";
        FloatingWings.Visibility = isAirplane ? Visibility.Visible : Visibility.Collapsed;
        FloatingWingsIcon.Visibility = isAirplane ? Visibility.Visible : Visibility.Collapsed;
        FloatingGoggles.Visibility = cfg.Accessory == "AviatorGoggles" ? Visibility.Visible : Visibility.Collapsed;
    }
    public void Present(string message) { BubbleText.Text = message; UpdateAppearance(); Show(); }
    private void DoneTask_Click(object sender, RoutedEventArgs e) => mainWindow.RespondToTask(true);
    private void LaterTask_Click(object sender, RoutedEventArgs e) => mainWindow.RespondToTask(false);

    private void PeriodicNudge()
    {
        var pendingTask = storage.Config.Tasks.FirstOrDefault(t => !t.IsCompleted);
        if (pendingTask != null)
        {
            BubbleText.Text = $"Time to work, {storage.Config.UserName}! Let's focus on: \"{pendingTask.Title}\" 🚀";
            if (storage.Config.SpeechVoiceEnabled)
            {
                speech.TellToWork(storage.Config.UserName, pendingTask.Title);
            }
        }
        else
        {
            BubbleText.Text = $"Great focus, {storage.Config.UserName}! Remember to drink some water! 💧";
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void WorkPrompt_Click(object sender, RoutedEventArgs e)
    {
        mainWindow.ShowNextTaskNow();
    }

    private void Glass_Click(object sender, RoutedEventArgs e)
    {
        storage.Config.GlassesDrankToday++;
        storage.Save();
        speech.PlayPop();
        BubbleText.Text = $"Nice! +1 glass logged ({storage.Config.GlassesDrankToday} total today) 💧";
    }

    private void OpenApp_Click(object sender, RoutedEventArgs e)
    {
        mainWindow.WindowState = WindowState.Normal;
        mainWindow.Activate();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

