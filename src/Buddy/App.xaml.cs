using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Buddy.Models;
using Buddy.Services;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Buddy;

public partial class App : Application
{
    private Mutex? mutex;
    private bool ownsMutex;
    private Storage storage = null!;
    private ReminderScheduler scheduler = null!;
    private OverlayWindow overlay = null!;
    private TaskbarIcon? tray;
    private System.Drawing.Icon? trayIcon;
    private DashboardWindow? dashboardWindow;
    private readonly MediaPlayer sound = new();
    private bool responseHandled;
    private Reminder? displayReminder;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        mutex = new Mutex(true, @"Local\Buddy.PSK.DesktopCompanion", out ownsMutex);
        if (!ownsMutex)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            File.AppendAllText(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buddy-error.log"),
                DateTime.Now + " " + args.Exception + Environment.NewLine);
            MessageBox.Show("Buddy encountered an error. Details are in %LocalAppData%\\Buddy-error.log.", "Buddy");
            args.Handled = true;
            Shutdown(1);
        };

        storage = new Storage();
        overlay = new OverlayWindow(storage.Settings);
        MainWindow = overlay;
        overlay.Show();

        scheduler = new ReminderScheduler(storage, Dispatcher)
        {
            CanPresent = () => overlay.Avatar.State == AvatarState.Offscreen
        };
        scheduler.Due += Present;
        scheduler.ToastRequested += Toast;

        overlay.Avatar.Arrived += Arrived;
        overlay.Avatar.Departed += () =>
        {
            displayReminder = null;
            scheduler.Check();
        };

        overlay.Bubble.Confirmed += () => Respond(true);
        overlay.Bubble.Snoozed += () => Respond(false);
        overlay.FunRequested += () =>
        {
            if (overlay.Avatar.State is AvatarState.Remind or AvatarState.Idle)
            {
                overlay.Bubble.Present(FunLine(), !responseHandled, displayReminder?.Kind == "water");
            }
        };


        CreateTray();

        try
        {
            ToastNotificationManagerCompat.OnActivated += args => Dispatcher.BeginInvoke(() => ToastActivated(args.Argument));
        }
        catch (Exception ex)
        {
            Log(ex);
        }

        storage.SaveSettings();
        if (!e.Args.Contains("--quiet"))
        {
            OpenDashboard(e.Args.Contains("--settings") ? "Reminders" : null);
        }
    }

    private void Present(Reminder r)
    {
        if (overlay.Avatar.State != AvatarState.Offscreen) return;
        displayReminder = r;
        responseHandled = false;
        overlay.Bubble.Visibility = Visibility.Collapsed;
        overlay.Avatar.Enter();
    }

    private void Arrived()
    {
        if (displayReminder == null) return;
        overlay.Avatar.Remind(displayReminder.Pose);
        overlay.Bubble.Present(displayReminder.Message.Replace("{name}", storage.Settings.Name), true, displayReminder.Kind == "water");


        if (storage.Settings.SoundEnabled)
        {
            var file = Path.Combine(Buddy.Services.AssetPaths.Root, "pop.wav");
            if (File.Exists(file))
            {
                sound.Open(new Uri(file));
                sound.Volume = 0.35;
                sound.Play();
            }
        }
    }

    private void Respond(bool yes)
    {
        if (responseHandled || displayReminder == null) return;
        responseHandled = true;

        var kind = displayReminder.Kind;
        scheduler.Complete(yes);

        if (kind == "water")
        {
            overlay.Bubble.Present(yes ? "Nice! Stay hydrated 💧" : "Okay… I'll be back in 10 min", false);
            overlay.Avatar.Respond(yes, yes ? "drink_water.png" : "sad.png");
        }
        else if (kind == "task")
        {
            overlay.Bubble.Present(yes ? "Awesome job completing the task! 🎯✨" : "Okay, PSK. I'll remind you in 10 minutes.", false);
            overlay.Avatar.Respond(yes, yes ? "wave.png" : "sad.png");
        }
        else
        {
            overlay.Bubble.Present(yes ? "Nice! A little reset goes a long way. ✨" : "Okay… I'll be back in 10 min", false);
            overlay.Avatar.Respond(yes, yes ? "wave.png" : "sad.png");
        }

        dashboardWindow?.UpdateAllMetrics();
    }

    private string FunLine() => new[]
    {
        $"Coding again, {storage.Settings.Name}? 😄",
        "Tiny break. Big ideas. ✨",
        "Your next breakthrough is one glass of water away! 💧",
        "High focus mode activated! Keep crushing it. 🚀",
        $"Proud of your progress today, {storage.Settings.Name}!"
    }[Random.Shared.Next(5)];

    private void CreateTray()
    {
        var menu = new ContextMenu();
        MenuItem Item(string title, Action action, bool bold = false)
        {
            var m = new MenuItem { Header = title };
            if (bold) m.FontWeight = FontWeights.Bold;
            m.Click += (_, _) => action();
            return m;
        }

        menu.Items.Add(Item("✨ PSK Charm Dashboard", () => OpenDashboard(), true));
        menu.Items.Add(Item("🚶 Summon Buddy Now", ShowNow));

        var pause = new MenuItem { Header = "Pause Companion" };
        pause.Items.Add(Item("30 minutes", () => Pause(TimeSpan.FromMinutes(30))));
        pause.Items.Add(Item("1 hour", () => Pause(TimeSpan.FromHours(1))));
        pause.Items.Add(Item("Until tomorrow", () => Pause(DateTime.Today.AddDays(1).AddHours(9) - DateTime.Now)));
        pause.Items.Add(Item("Resume Now", () => { scheduler.Resume(); }));
        menu.Items.Add(pause);

        menu.Items.Add(new Separator());
        menu.Items.Add(Item("📋 My Tasks", () => OpenDashboard("Tasks")));
        menu.Items.Add(Item("⏰ Reminders", () => OpenDashboard("Reminders")));
        menu.Items.Add(Item("📊 Hydration & Stats", () => OpenDashboard("Stats")));
        menu.Items.Add(Item("🤖 My Companion Studio", () => OpenDashboard("Companion")));

        var startup = new MenuItem { Header = "Start with Windows", IsCheckable = true, IsChecked = StartupService.Enabled };
        startup.Click += (_, _) => StartupService.SetEnabled(startup.IsChecked);
        menu.Items.Add(startup);

        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Quit", () => Shutdown()));

        var path = Path.Combine(Buddy.Services.AssetPaths.Root, "app.ico");
        trayIcon = File.Exists(path) ? new System.Drawing.Icon(path) : (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();

        tray = new TaskbarIcon
        {
            ToolTipText = "PSK Charm · Companion",
            Icon = trayIcon,
            ContextMenu = menu,
            LeftClickCommand = new RelayCommand(() => OpenDashboard())
        };
        tray.ForceCreate();
    }

    public void ShowNow()
    {
        if (overlay.Avatar.State == AvatarState.Offscreen)
            scheduler.ShowNow();
    }

    private void Pause(TimeSpan duration)
    {
        scheduler.Pause(duration);
        if (displayReminder != null && !responseHandled) Respond(false);
        overlay.Avatar.Leave();
    }

    public void OpenDashboard(string? tab = null)
    {
        if (dashboardWindow != null)
        {
            dashboardWindow.Activate();
            dashboardWindow.WindowState = WindowState.Normal;
        }
        else
        {
            dashboardWindow = new DashboardWindow(storage, scheduler, overlay);
            dashboardWindow.SettingsChanged += () => overlay.Avatar.ApplySize();
            dashboardWindow.Closed += (_, _) => dashboardWindow = null;
            dashboardWindow.Show();
        }

        if (tab != null) dashboardWindow.SwitchToTab(tab);
    }

    private readonly HashSet<Guid> toastPending = new();

    private void Toast(Reminder r)
    {
        try
        {
            toastPending.Add(r.Id);
            new ToastContentBuilder().AddText("Buddy")
                .AddText(r.Message.Replace("{name}", storage.Settings.Name))
                .AddArgument("reminder", r.Id.ToString())
                .AddButton(new ToastButton().SetContent(r.Kind == "water" ? "Yes" : "Done").AddArgument("action", "yes").SetBackgroundActivation())
                .AddButton(new ToastButton().SetContent("Later").AddArgument("action", "later").SetBackgroundActivation())
                .Show();
        }
        catch (Exception ex)
        {
            toastPending.Remove(r.Id);
            Log(ex);
        }
    }

    private void ToastActivated(string argument)
    {
        var args = ToastArguments.Parse(argument);
        if (!args.Contains("reminder") || !Guid.TryParse(args["reminder"], out var id) || !toastPending.Remove(id)) return;
        var r = storage.Settings.Reminders.FirstOrDefault(rem => rem.Id == id);
        if (r == null) return;
        var yes = args.Contains("action") && args["action"] == "yes";
        if (displayReminder?.Id == id && !responseHandled) Respond(yes);
        else scheduler.Schedule(r, yes);
    }

    private void Log(Exception ex)
    {
        File.AppendAllText(Path.Combine(storage.Folder, "diagnostics.log"), $"{DateTime.Now:O} {ex.Message}{Environment.NewLine}");
    }
    protected override void OnExit(ExitEventArgs e)
    {
        sound.Close(); scheduler?.Dispose(); tray?.Dispose(); trayIcon?.Dispose();
        storage?.SaveSettings(); overlay?.Close();
        if (ownsMutex) mutex?.ReleaseMutex(); mutex?.Dispose(); base.OnExit(e);
    }
}
