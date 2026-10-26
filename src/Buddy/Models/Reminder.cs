using CommunityToolkit.Mvvm.ComponentModel;
namespace Buddy.Models;
public partial class Reminder : ObservableObject
{
    [ObservableProperty] private Guid id = Guid.NewGuid();
    [ObservableProperty] private string message = "Did you drink water, {name}?";
    [ObservableProperty] private int intervalMinutes = 45;
    [ObservableProperty] private TimeSpan startTime = TimeSpan.FromHours(9);
    [ObservableProperty] private TimeSpan endTime = TimeSpan.FromHours(19);
    [ObservableProperty] private string pose = "drink_water.png";
    [ObservableProperty] private bool enabled = true;
    [ObservableProperty] private DateTime nextDue = DateTime.Now.AddMinutes(45);
    [ObservableProperty] private string kind = "water";
    public System.Guid? TaskId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public int FullscreenDeferrals { get; set; }
}
