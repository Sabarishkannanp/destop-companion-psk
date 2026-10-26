using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Buddy.Models;
using Buddy.Services;
namespace Buddy;
public partial class SettingsWindow : Window
{
    private readonly Storage storage;
    private readonly BuddySettings draft;
    public event Action? Saved;
    public SettingsWindow(Storage storage)
    { InitializeComponent();SourceInitialized+=(_,_)=>Buddy.Interop.NativeMethods.ApplyDarkTitleBar(new System.Windows.Interop.WindowInteropHelper(this).Handle);this.storage=storage;draft=JsonSerializer.Deserialize<BuddySettings>(JsonSerializer.Serialize(storage.Settings))!;DataContext=draft; }
    private void AddReminder(object sender,RoutedEventArgs e)=>draft.Reminders.Add(new Reminder { Message="Time for a break, {name}!",Kind="break",Pose="wave.png" });
    private void DeleteReminder(object sender,RoutedEventArgs e) { if(ReminderGrid.SelectedItem is Reminder r)draft.Reminders.Remove(r); }
    private void Save(object sender,RoutedEventArgs e)
    {
        if(!ReminderGrid.CommitEdit(DataGridEditingUnit.Cell,true)||!ReminderGrid.CommitEdit(DataGridEditingUnit.Row,true)) { MessageBox.Show(this,"Correct the highlighted reminder values first.","Buddy");return; }
        Storage.Validate(draft);var s=storage.Settings;s.Name=draft.Name;s.AvatarSize=draft.AvatarSize;s.WalkSpeed=draft.WalkSpeed;s.SoundEnabled=draft.SoundEnabled;
        foreach(var r in draft.Reminders) { var old=s.Reminders.FirstOrDefault(o=>o.Id==r.Id);if(old==null||old.IntervalMinutes!=r.IntervalMinutes||old.Enabled!=r.Enabled)r.NextDue=DateTime.Now.AddMinutes(r.IntervalMinutes); }
        s.Reminders=draft.Reminders;storage.SaveSettings();Saved?.Invoke();Close();
    }
    private void Cancel(object sender,RoutedEventArgs e)=>Close();
    private void OpenFolder(object sender,RoutedEventArgs e) { var dir=Path.Combine(Buddy.Services.AssetPaths.Root,"Avatar");Directory.CreateDirectory(dir);Process.Start(new ProcessStartInfo("explorer.exe",$"\"{dir}\"") { UseShellExecute=true }); }
}
