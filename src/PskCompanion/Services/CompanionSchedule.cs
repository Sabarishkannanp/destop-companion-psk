using PskCompanion.Models;
namespace PskCompanion.Services;
public static class CompanionSchedule
{
    public static bool InHours(CompanionConfig config, DateTime now)
    {
        var time = now.TimeOfDay;
        return config.ReminderStart == config.ReminderEnd ||
            (config.ReminderStart < config.ReminderEnd ? time >= config.ReminderStart && time < config.ReminderEnd : time >= config.ReminderStart || time < config.ReminderEnd);
    }
    public static WorkTask? DueTask(CompanionConfig config, DateTime now) =>
        config.Tasks.Where(t => !t.IsCompleted && t.RemindAt <= now).OrderBy(t => t.RemindAt).FirstOrDefault();
}
