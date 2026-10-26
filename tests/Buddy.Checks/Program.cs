using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Buddy;
using Buddy.Controls;
using Buddy.Models;
using Buddy.Services;

internal static class Program
{
    private static int assertions;
    private static OverlayWindow? overlay;
    [STAThread]
    public static int Main()
    {
        Console.WriteLine("Starting Buddy checks.");
        var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri("/Buddy;component/Themes/Psk.xaml",UriKind.Relative) });
        Console.WriteLine("Validation application initialized.");
        Dispatcher.CurrentDispatcher.BeginInvoke(new Action(async ()=>
        {
            try { await Run();Console.WriteLine($"PASS: {assertions} checks"); }
            catch(Exception ex){ Console.Error.WriteLine(ex);Environment.ExitCode=1; }
            finally { overlay?.Close(); Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }));
        Dispatcher.Run();return Environment.ExitCode;
    }
    private static void Check(bool condition,string label)
    { if(!condition)throw new Exception("FAIL: "+label);assertions++;Console.WriteLine("PASS: "+label); }
    private static async Task Run()
    {
        var folder=Path.Combine(Path.GetTempPath(),"Buddy-checks-"+Guid.NewGuid());
        var store=new Storage(folder);store.Settings.Name="Test";store.Settings.AvatarSize=double.NaN;store.Settings.WalkSpeed=999;store.Settings.Reminders[0].IntervalMinutes=0;store.SaveSettings();
        var loaded=new Storage(folder);Check(loaded.Settings.Name=="Test","settings round trip");Check(loaded.Settings.AvatarSize==220&&loaded.Settings.WalkSpeed==240&&loaded.Settings.Reminders[0].IntervalMinutes==1,"unsafe settings clamped");
        loaded.AddGlass();loaded.AddGlass();Check(new Storage(folder).Glasses(DateTime.Today)==2,"hydration persists");Check(loaded.Glasses(DateTime.Today.AddDays(-1))==0,"date isolation");
        File.WriteAllText(Path.Combine(folder,"settings.json"),"{broken");Check(new Storage(folder).Settings.Name=="PSK","corrupt JSON recovery");Check(Directory.EnumerateFiles(folder,"*.invalid-*").Any(),"corrupt JSON backup");
        var migrated = new BuddySettings { Name="Selva" };Storage.Validate(migrated);Check(migrated.Name=="PSK","saved Selva name migrates to PSK");
        Check(File.Exists(Buddy.Services.AssetPaths.Root+"/Avatar/idle_front.png"),"embedded avatar extraction");
        using(var icon=new System.Drawing.Icon(Path.Combine(Buddy.Services.AssetPaths.Root,"app.ico")))
        using(var tray=new H.NotifyIcon.TaskbarIcon { Icon=icon, ToolTipText="Buddy validation" })
        { tray.ForceCreate();await Task.Delay(150);Check(tray.Icon!=null,"tray accepts native ICO without image conversion"); }
        var timedTask=new TaskItem { Title="Test scheduled work",RemindAt=DateTime.Now.AddMinutes(-1) };
        loaded.Settings.Tasks.Add(timedTask);loaded.SaveSettings();Check(new Storage(folder).Settings.Tasks.Any(t=>t.Id==timedTask.Id&&t.RemindAt!=null),"task appearance time persists");
        Check(ReminderScheduler.DueTasks(loaded.Settings,DateTime.Now).Any(t=>t.Id==timedTask.Id),"due task selected");timedTask.RemindWithBuddy=false;
        Check(!ReminderScheduler.DueTasks(loaded.Settings,DateTime.Now).Any(t=>t.Id==timedTask.Id),"disabled task reminder skipped");timedTask.RemindWithBuddy=true;
        using(var scheduler=new ReminderScheduler(loaded,Dispatcher.CurrentDispatcher))
        { scheduler.ShowTaskReminder(timedTask);Check(scheduler.Current?.Message.Contains(timedTask.Title)==true,"task title delivered to avatar");scheduler.Complete(false);Check(timedTask.RemindAt>DateTime.Now.AddMinutes(9),"task snooze persists ten minutes");scheduler.ShowTaskReminder(timedTask);scheduler.Complete(true);Check(timedTask.IsCompleted&&timedTask.RemindAt==null,"done completes scheduled task"); }
        var companionStore=new PskCompanion.Services.CompanionStorage(Path.Combine(folder,"Psk"));
        companionStore.Config.Tasks.Add(new PskCompanion.Models.WorkTask {Title="Scheduled Psk work",RemindAt=DateTime.Now.AddMinutes(-1)});
        Check(PskCompanion.Services.CompanionSchedule.DueTask(companionStore.Config,DateTime.Now)?.Title=="Scheduled Psk work","PSK exact task scheduling");
        companionStore.Config.ReminderStart=TimeSpan.FromHours(22);companionStore.Config.ReminderEnd=TimeSpan.FromHours(6);
        Check(PskCompanion.Services.CompanionSchedule.InHours(companionStore.Config,DateTime.Today.AddHours(23))&&!PskCompanion.Services.CompanionSchedule.InHours(companionStore.Config,DateTime.Today.AddHours(12)),"PSK overnight appearance hours");
        companionStore.Save();Check(new PskCompanion.Services.CompanionStorage(companionStore.Folder).Config.Tasks.Last().RemindAt!=null,"PSK task date persists");
        var reminder=new Reminder { StartTime=TimeSpan.FromHours(22),EndTime=TimeSpan.FromHours(6) };
        Check(ReminderScheduler.InHours(reminder,DateTime.Today.AddHours(23))&&ReminderScheduler.InHours(reminder,DateTime.Today.AddHours(2))&&!ReminderScheduler.InHours(reminder,DateTime.Today.AddHours(12)),"overnight schedule");
        reminder.StartTime=reminder.EndTime;Check(ReminderScheduler.InHours(reminder,DateTime.Today.AddHours(12)),"all-day schedule");
        using(var scheduler=new ReminderScheduler(loaded,Dispatcher.CurrentDispatcher))
        {
            scheduler.CanPresent=()=>false;scheduler.ShowNow();Check(scheduler.Current==null,"busy avatar does not consume reminder");scheduler.CanPresent=()=>true;scheduler.ShowNow();Check(scheduler.Current!=null,"manual delivery");
            scheduler.Complete(false);Check(scheduler.Current==null&&loaded.Settings.Reminders[0].NextDue>DateTime.Now.AddMinutes(9),"ten-minute snooze");
            var before=loaded.Glasses(DateTime.Today);scheduler.ShowNow();scheduler.Complete(true);Check(loaded.Glasses(DateTime.Today)==before+1,"water confirmation increments once");scheduler.Complete(true);Check(loaded.Glasses(DateTime.Today)==before+1,"duplicate confirmation ignored");
            scheduler.Pause(TimeSpan.FromMinutes(30));Check(loaded.Settings.PausedUntil>DateTime.Now.AddMinutes(29),"pause persisted");
        }
        var names=new[]{"idle_front","walk_right_1","walk_right_2","walk_right_3","walk_right_4","walk_right_5","walk_right_6","walk_right_7","walk_right_8","drink_water","wave","sad"};
        foreach(var name in names)
        {
            var p=Path.Combine(AppContext.BaseDirectory,"Assets","Avatar",name+".png");Check(File.Exists(p),name+" exists");
            var b=new BitmapImage(new Uri(p));Check(b.PixelWidth==400&&b.PixelHeight==600,name+" dimensions");
            var converted=new FormatConvertedBitmap(b,PixelFormats.Bgra32,null,0);var pixels=new byte[400*600*4];converted.CopyPixels(pixels,400*4,0);
            Check(pixels[3]==0&&pixels[(400*600-1)*4+3]==0,name+" transparent corners");Check(Enumerable.Range(598*400,800).Any(i=>pixels[i*4+3]>8),name+" feet aligned within two pixels");
        }
        Check(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Assets","pop.wav")).Length==17684,"valid sound length");
        var settings=new BuddySettings { WalkSpeed=240 };overlay=new OverlayWindow(settings);overlay.Show();await Task.Delay(150);
        Check(overlay.Topmost&&!overlay.ShowInTaskbar&&!overlay.ShowActivated,"overlay flags");Check(Math.Abs(overlay.Top+overlay.Height-SystemParameters.WorkArea.Bottom)<1,"taskbar alignment");
        var native=typeof(OverlayWindow).Assembly.GetType("Buddy.Interop.NativeMethods")!;var style=(nint)native.GetMethod("GetWindowLongPtr",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{new WindowInteropHelper(overlay).Handle,-20})!;
        Check(((long)style&0x08080080)==0x08080080,"native no-activate, layered, tool-window styles");
        Check(((long)style&0x20)==0x20,"empty overlay is click-through");
        var bubble=(SpeechBubble)overlay.FindName("Bubble");bubble.Present("Hello, Test!");await Task.Delay(100);Check(bubble.Visibility==Visibility.Visible&&bubble.ActualHeight>50,"bubble layout and animation");
        var settingsWindow=new SettingsWindow(loaded);settingsWindow.Show();await Task.Delay(100);Check(settingsWindow.IsLoaded,"settings window loads");Check(settingsWindow.Foreground is SolidColorBrush ink&&ink.Color.R>200,"settings text contrast");Capture((FrameworkElement)settingsWindow.Content,"psk-settings.png");settingsWindow.Close();
        var statsWindow=new StatsWindow(loaded);statsWindow.Show();await Task.Delay(100);Check(statsWindow.IsLoaded,"stats chart loads");Check(statsWindow.Foreground is SolidColorBrush statsInk&&statsInk.Color.R>200,"stats text contrast");Capture((FrameworkElement)statsWindow.Content,"psk-stats.png");statsWindow.Close();
        using(var sched=new ReminderScheduler(loaded,Dispatcher.CurrentDispatcher))
        {
            var dash=new DashboardWindow(loaded,sched,overlay);dash.Show();await Task.Delay(150);
            Check(dash.IsLoaded,"dashboard window loads");Capture((FrameworkElement)dash.Content,"psk-dashboard.png");
            dash.SwitchToTab("Companion");await Task.Delay(100);Capture((FrameworkElement)dash.Content,"psk-dashboard-companion.png");
            dash.SwitchToTab("Tasks");
            ((System.Windows.Controls.TextBox)dash.FindName("NewTaskTitleBox")).Text="My scheduled task";
            ((System.Windows.Controls.CheckBox)dash.FindName("ScheduleNewTask")).IsChecked=true;
            var planned=DateTime.Today.AddDays(1).AddHours(14).AddMinutes(30);
            ((System.Windows.Controls.DatePicker)dash.FindName("NewTaskDate")).SelectedDate=planned.Date;
            ((System.Windows.Controls.TextBox)dash.FindName("NewTaskTime")).Text="14:30";
            typeof(DashboardWindow).GetMethod("PerformAddTask",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(dash,null);
            Check(new Storage(folder).Settings.Tasks.Any(t=>t.Title=="My scheduled task"&&t.RemindAt==planned),"task form saves selected date and time");
            Check(!ReminderScheduler.DueTasks(loaded.Settings,planned.AddSeconds(-1)).Any(t=>t.Title=="My scheduled task")&&ReminderScheduler.DueTasks(loaded.Settings,planned).Any(t=>t.Title=="My scheduled task"),"task becomes due at selected time");
            ((System.Windows.Controls.TextBox)dash.FindName("NewTaskTitleBox")).Text="Invalid time task";
            ((System.Windows.Controls.TextBox)dash.FindName("NewTaskTime")).Text="25:00";
            typeof(DashboardWindow).GetMethod("PerformAddTask",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(dash,null);
            Check(!loaded.Settings.Tasks.Any(t=>t.Title=="Invalid time task"),"invalid task time rejected");
            ((System.Windows.Controls.TextBox)dash.FindName("NewTaskTitleBox")).Clear();
            ((System.Windows.Controls.TextBox)dash.FindName("NewTaskTime")).Text="14:30";
            await Task.Delay(100);Capture((FrameworkElement)dash.Content,"psk-dashboard-tasks.png");
            dash.SwitchToTab("Reminders");await Task.Delay(100);Capture((FrameworkElement)dash.Content,"psk-dashboard-reminders.png");
            dash.SwitchToTab("Stats");await Task.Delay(100);Capture((FrameworkElement)dash.Content,"psk-dashboard-stats.png");
            dash.Close();
        }
        companionStore.Config.SpeechVoiceEnabled=false;companionStore.Config.AutoShowCompanion=false;companionStore.Config.RemindersEnabled=false;
        var pskTheme=new ResourceDictionary { Source=new Uri("/PskCompanion;component/Themes/PskTheme.xaml",UriKind.Relative) };Application.Current.Resources.MergedDictionaries.Add(pskTheme);
        var pskApp = new PskCompanion.MainWindow(companionStore);
        pskApp.Show();
        await Task.Delay(200);
        Check(pskApp.IsLoaded, "psk companion main window loads");
        Check(pskApp.FindName("NextAppearanceBox") is System.Windows.Controls.TextBox,"appearance timing editor loads");
        pskApp.ShowNextTaskNow();pskApp.RespondToTask(false);Check(companionStore.Config.Tasks[0].RemindAt>DateTime.Now.AddMinutes(9),"PSK floating task snooze");
        pskApp.ShowNextTaskNow();pskApp.RespondToTask(true);Check(companionStore.Config.Tasks[0].IsCompleted,"PSK floating Done completes task");
        var sharedWalker=(Buddy.OverlayWindow)typeof(PskCompanion.MainWindow).GetField("walkingBuddy",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pskApp)!;
        for(var i=0;i<300&&sharedWalker.Avatar.State!=Buddy.AvatarState.Offscreen;i++)await Task.Delay(50);
        Check(sharedWalker.Avatar.State==Buddy.AvatarState.Offscreen,"classic PSK avatar walks out after response");
        companionStore.Config.DisplaySeconds=5;
        companionStore.Config.RemindersEnabled=true;
        typeof(PskCompanion.MainWindow).GetMethod("CheckScheduledReminders",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pskApp,null);
        for(var i=0;i<300&&sharedWalker.Avatar.State!=Buddy.AvatarState.Remind;i++)await Task.Delay(50);
        Check(((System.Windows.Controls.TextBlock)sharedWalker.Speech.FindName("MessageText")).Text.Contains("Scheduled Psk work"),"timed task actually appears with its title");
        var pendingTime=companionStore.Config.Tasks.First(t=>t.Title=="Scheduled Psk work").RemindAt;
        await Task.Delay(6200);
        Check(sharedWalker.Avatar.State==AvatarState.Remind&&sharedWalker.Speech.Visibility==Visibility.Visible,"character waits beyond old timeout for user choice");
        pskApp.ShowNextTaskNow();
        Check(((System.Windows.Controls.TextBlock)sharedWalker.Speech.FindName("MessageText")).Text.Contains("Scheduled Psk work"),"pending reminder cannot be replaced");
        Check(companionStore.Config.Tasks.First(t=>t.Title=="Scheduled Psk work").RemindAt==pendingTime,"waiting does not snooze a task");
        pskApp.RespondToTask(false);companionStore.Config.RemindersEnabled=false;
        var timeBox=(System.Windows.Controls.TextBox)pskApp.FindName("IntervalMinutesBox");timeBox.Text="7";
        Console.WriteLine("Timing editor values: "+string.Join(" | ",new[]{"IntervalMinutesBox","ActiveFromBox","ActiveUntilBox","NextAppearanceBox"}.Select(n=>n+"="+((System.Windows.Controls.TextBox)pskApp.FindName(n)).Text)));
        typeof(PskCompanion.MainWindow).GetMethod("SaveTiming_Click",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pskApp,new object[]{pskApp,new RoutedEventArgs()});
        Check(new PskCompanion.Services.CompanionStorage(companionStore.Folder).Config.WorkReminderMinutes==7,"timing editor save persists interval");
        Capture((FrameworkElement)pskApp.Content, "psk-companion-main.png");
        using var testSpeech = new PskCompanion.Services.CompanionSpeechService();
        var floating = new PskCompanion.DesktopCompanionWindow(companionStore, testSpeech, pskApp);
        floating.Show();
        await Task.Delay(200);
        Check(floating.IsLoaded, "psk companion floating window loads");
        Capture((FrameworkElement)floating.Content, "psk-companion-floating.png");
        floating.Close();
        pskApp.Close();
        Application.Current.Resources.MergedDictionaries.Remove(pskTheme);
        bool arrived=false,departed=false;overlay.Avatar.Arrived+=()=>arrived=true;overlay.Avatar.Departed+=()=>departed=true;overlay.Avatar.Enter();
        for(var i=0;i<250&&!arrived;i++)await Task.Delay(50);Check(arrived&&overlay.Avatar.State==AvatarState.Remind,"walking arrival");
        Check(((System.Windows.Controls.Image)overlay.FindName("AvatarImage")).Source is BitmapSource,"avatar image loaded in overlay");
        Check(((FrameworkElement)overlay.FindName("Placeholder")).Visibility==Visibility.Collapsed,"avatar placeholder hidden");
        bubble.Present("Hey, PSK!\nDid you drink water?");await Task.Delay(400);Capture((FrameworkElement)overlay.Content,"psk-overlay.png");
        settings.WalkSpeed=40; overlay.Avatar.Dragging=true;
        overlay.Avatar.Respond(true,"drink_water.png");
        Check(!overlay.Avatar.Dragging,"Done releases interrupted drag");Check(overlay.Avatar.State==AvatarState.Happy,"happy response pose");
        for(var i=0;i<140&&!departed;i++)await Task.Delay(50);Check(departed&&overlay.Avatar.State==AvatarState.Offscreen,"Done exits within seven seconds at minimum speed");
        overlay.Avatar.Enter();overlay.Avatar.MoveTo(overlay.Width/2);
        overlay.Avatar.Dragging=true;departed=false;
        overlay.Avatar.Respond(false,"sad.png");
        Check(!overlay.Avatar.Dragging,"Later releases interrupted drag");
        for(var i=0;i<140&&!departed;i++)await Task.Delay(50);
        Check(departed&&overlay.Avatar.State==AvatarState.Offscreen,"Later exits within seven seconds at minimum speed");
        Console.WriteLine("Test data: "+folder);
    }
    private static void Capture(FrameworkElement visual,string name)
    {
        var width=(int)Math.Ceiling(visual.ActualWidth);var height=(int)Math.Ceiling(visual.ActualHeight);
        var drawing=new DrawingVisual();using(var context=drawing.RenderOpen())context.DrawRectangle(new VisualBrush(visual),null,new Rect(0,0,width,height));
        var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(drawing);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));Directory.CreateDirectory("artifacts");using var stream=File.Create(Path.Combine("artifacts",name));encoder.Save(stream);
    }
}
