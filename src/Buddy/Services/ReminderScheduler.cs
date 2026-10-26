using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using Buddy.Interop;
using Buddy.Models;
using Microsoft.Win32;

namespace Buddy.Services;

public sealed class ReminderScheduler : IDisposable
{
    private readonly Storage storage;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly Queue<Guid> queue = new();
    private bool locked;
    private DateTime quietUntil;
    public Reminder? Current { get; private set; }
    public event Action<Reminder>? Due;
    public event Action<Reminder>? ToastRequested;
    public Func<bool>? CanPresent { get; set; }

    public ReminderScheduler(Storage storage, Dispatcher dispatcher)
    {
        this.storage = storage;
        this.dispatcher = dispatcher;
        timer.Tick += (_, _) => Check();
        SystemEvents.SessionSwitch += SessionChanged;
        timer.Start();
    }

    public static bool InHours(Reminder r, DateTime now)
    {
        var t = now.TimeOfDay;
        return r.StartTime == r.EndTime || (r.StartTime < r.EndTime ? t >= r.StartTime && t < r.EndTime : t >= r.StartTime || t < r.EndTime);
    }

    private bool Quiet(DateTime now) => locked || now < quietUntil || now < storage.Settings.PausedUntil || NativeMethods.IdleTime() > TimeSpan.FromMinutes(5);

    public void Check()
    {
        var now = DateTime.Now;
        if (Quiet(now)) return;

        foreach (var r in storage.Settings.Reminders.Where(r => r.Enabled && r.NextDue <= now && InHours(r, now)))
            if (Current?.Id != r.Id && !queue.Contains(r.Id)) queue.Enqueue(r.Id);
        foreach (var task in DueTasks(storage.Settings,now))
            if (Current?.TaskId != task.Id && !queue.Contains(task.Id)) queue.Enqueue(task.Id);

        if (Current != null || CanPresent?.Invoke() == false) return;

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            var r = storage.Settings.Reminders.FirstOrDefault(r => r.Id == id && r.Enabled);
            if (r == null)
            {
                var task = storage.Settings.Tasks.FirstOrDefault(t => t.Id == id && !t.IsCompleted && t.RemindWithBuddy && t.RemindAt <= now);
                if (task != null) r = TaskReminder(task);
            }
            if (r == null || r.NextDue > now || (r.TaskId == null && !InHours(r, now))) continue;

            if (NativeMethods.IsFullscreen())
            {
                r.NextDue = now.AddMinutes(5);
                if (r.TaskId is Guid taskId) storage.Settings.Tasks.First(t => t.Id == taskId).RemindAt = r.NextDue;
                r.FullscreenDeferrals++;
                if (r.FullscreenDeferrals >= 2)
                {
                    ToastRequested?.Invoke(r);
                    r.FullscreenDeferrals = 0;
                }
                storage.SaveSettings();
                continue;
            }

            Current = r;
            Due?.Invoke(r);
            return;
        }
    }
    public static IEnumerable<TaskItem> DueTasks(BuddySettings settings,DateTime now) =>
        settings.Tasks.Where(t => !t.IsCompleted && t.RemindWithBuddy && t.RemindAt <= now);

    public void ShowNow(Reminder? specific = null)
    {
        if (Current != null || CanPresent?.Invoke() == false) return;
        Current = specific ?? storage.Settings.Reminders.FirstOrDefault(r => r.Kind == "water") ?? new Reminder();
        Due?.Invoke(Current);
    }

    public void ShowTaskReminder(TaskItem task)
    {
        if (Current != null || CanPresent?.Invoke() == false) return;
        var r = TaskReminder(task);
        Current = r;
        Due?.Invoke(Current);
    }

    public void Complete(bool yes)
    {
        if (Current == null) return;
        Schedule(Current, yes);
        Current = null;
    }

    public void Schedule(Reminder r, bool yes)
    {
        if (r.TaskId is Guid id)
        {
            var task = storage.Settings.Tasks.FirstOrDefault(t => t.Id == id);
            if (task != null) { task.IsCompleted = yes; task.RemindAt = yes ? null : DateTime.Now.AddMinutes(10); }
        }
        if (yes && r.Kind == "water") storage.AddGlass();
        r.NextDue = DateTime.Now.AddMinutes(yes ? r.IntervalMinutes : 10);
        r.FullscreenDeferrals = 0;
        storage.SaveSettings();
    }
    private static Reminder TaskReminder(TaskItem task) => new()
    {
        Id = task.Id, TaskId = task.Id, Kind = "task", Pose = "wave.png",
        NextDue = task.RemindAt ?? DateTime.Now,
        Message = $"Hey, {{name}}!\nTime to work on:\n{task.Title}"
    };

    public void Pause(TimeSpan duration)
    {
        storage.Settings.PausedUntil = DateTime.Now + duration;
        storage.SaveSettings();
        queue.Clear();
    }

    public void Resume()
    {
        storage.Settings.PausedUntil = DateTime.MinValue;
        storage.SaveSettings();
        Check();
    }

    private void SessionChanged(object sender, SessionSwitchEventArgs e)
    {
        dispatcher.BeginInvoke(() =>
        {
            if (e.Reason == SessionSwitchReason.SessionLock) locked = true;
            if (e.Reason == SessionSwitchReason.SessionUnlock) { locked = false; quietUntil = DateTime.Now.AddMinutes(2); }
        });
    }

    public void Dispose()
    {
        timer.Stop();
        SystemEvents.SessionSwitch -= SessionChanged;
    }
}
