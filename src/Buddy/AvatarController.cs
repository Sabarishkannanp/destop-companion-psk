using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Buddy.Models;
namespace Buddy;
public enum AvatarState { Offscreen,WalkIn,Idle,Remind,Happy,Sad,WalkOut }
public sealed class AvatarController : IDisposable
{
    private readonly OverlayWindow window;
    private readonly BuddySettings settings;
    private readonly Dictionary<string,BitmapImage> sprites=new();
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private double previous,x,target,direction=1,stateUntil,walkDistance;
    private double exitStarted,exitFrom,exitTarget;
    private readonly int walkFrameCount;
    private int frame=-1;
    private string pose="idle_front.png";
    public AvatarState State { get; private set; }=AvatarState.Offscreen;
    public bool Dragging { get; set; }
    public event Action? Arrived;
    public event Action? Departed;
    public AvatarController(OverlayWindow window,BuddySettings settings)
    {
        this.window=window; this.settings=settings;
        var dir=Path.Combine(Buddy.Services.AssetPaths.Root,"Avatar");
        if(Directory.Exists(dir)) foreach(var path in Directory.EnumerateFiles(dir,"*.png"))
        { try { var b=new BitmapImage(); b.BeginInit(); b.CacheOption=BitmapCacheOption.OnLoad; b.UriSource=new Uri(path); b.EndInit(); b.Freeze(); sprites[Path.GetFileName(path)]=b; } catch(IOException){} catch(NotSupportedException){} }
        walkFrameCount=sprites.ContainsKey("walk_right_8.png")?8:4;
        CompositionTarget.Rendering+=Render; ApplySize(); window.AvatarHost.Visibility=Visibility.Collapsed; window.Shadow.Visibility=Visibility.Collapsed;
    }
    public void ApplySize() { window.AvatarHost.Height=settings.AvatarSize; window.AvatarHost.Width=settings.AvatarSize*2/3; SetPosition(x); }
    public void Enter()
    {
        if(State!=AvatarState.Offscreen)return;
        direction=Random.Shared.Next(2)==0?1:-1; x=direction>0?-window.AvatarHost.Width:window.Width;
        target=window.Width*(0.2+Random.Shared.NextDouble()*0.5); target=Math.Clamp(target,0,Math.Max(0,window.Width-window.AvatarHost.Width));
        State=AvatarState.WalkIn; frame=-1; walkDistance=0; window.AvatarHost.Visibility=window.Shadow.Visibility=Visibility.Visible; StopBreathing();
    }
    public void Respond(bool yes,string responsePose)
    { EndDrag(); State=yes?AvatarState.Happy:AvatarState.Sad; SetPose(responsePose); stateUntil=clock.Elapsed.TotalSeconds+2; StopBreathing(); }
    public void Idle() { State=AvatarState.Idle; SetPose("idle_front.png"); Breathe(); }
    public void Remind(string reminderPose) { State=AvatarState.Remind; pose=reminderPose; SetPose(pose); Breathe(); }
    public void MoveTo(double newX) { x=Math.Clamp(newX,0,Math.Max(0,window.Width-window.AvatarHost.Width)); SetPosition(x); }
    public double X=>x;
    public void Leave()
    {
        if(State is AvatarState.Offscreen or AvatarState.WalkOut)return;
        EndDrag(); State=AvatarState.WalkOut; direction=x<window.Width/2?-1:1;
        exitStarted=clock.Elapsed.TotalSeconds; exitFrom=x;
        exitTarget=direction>0?window.Width+1:-window.AvatarHost.Width-1;
        frame=-1; StopBreathing(); window.Bubble.Visibility=Visibility.Collapsed;
    }
    private void EndDrag() { Dragging=false; window.AvatarHost.ReleaseMouseCapture(); }
    private void Render(object? sender,EventArgs e)
    {
        var now=clock.Elapsed.TotalSeconds; var dt=Math.Min(0.05,now-previous); previous=now;
        if(State==AvatarState.Offscreen)return;
        if(State is AvatarState.WalkIn or AvatarState.WalkOut && !Dragging)
        {
            // Match the cycle to distance traveled: changing speed no longer makes the feet skate.
            var speed=settings.WalkSpeed;
            if(State==AvatarState.WalkIn)speed*=Math.Clamp(Math.Abs(x-target)/65,0.35,1);
            var step=speed*dt;
            // Finish dismissal in three seconds, even at slow walking speeds or low frame rates.
            if(State==AvatarState.WalkOut)
            {
                var progress=Math.Clamp((now-exitStarted)/3,0,1);
                step=Math.Abs(exitFrom+(exitTarget-exitFrom)*progress-x);
            }
            if(State==AvatarState.WalkIn&&Math.Abs(x-target)<=step) { x=target; State=AvatarState.Remind; SetPose(pose); window.Bob.Y=0; Breathe(); Arrived?.Invoke(); }
            else
            {
                x+=direction*step; walkDistance+=step; window.Facing.ScaleX=direction;
                var cycle=walkDistance/(settings.AvatarSize*0.6);
                var f=(int)(cycle*walkFrameCount)%walkFrameCount; if(f!=frame){SetPose($"walk_right_{f+1}.png",false);frame=f;}
                window.Bob.Y = settings.BobEnabled ? -Math.Pow(Math.Sin(cycle * Math.PI * 2), 2) * 1.8 : 0;
                if(State==AvatarState.WalkOut&&(x>window.Width||x< -window.AvatarHost.Width))
                { State=AvatarState.Offscreen; window.AvatarHost.Visibility=window.Shadow.Visibility=Visibility.Collapsed; Departed?.Invoke(); }
            }
        }
        if(State is AvatarState.Happy or AvatarState.Sad && now>=stateUntil)Leave();
        if(State==AvatarState.Idle)SetPose((int)now%18==0?"wave.png":"idle_front.png");
        SetPosition(x);
    }
    public void TestPose(string name)
    {
        pose = name;
        if (State == AvatarState.Offscreen) Enter();
        else { SetPose(name); Breathe(); }
    }
    private void SetPosition(double left)
    {
        Canvas.SetLeft(window.AvatarHost,left); Canvas.SetTop(window.AvatarHost,window.Height-window.AvatarHost.Height-6);
        Canvas.SetLeft(window.Shadow,left+(window.AvatarHost.Width-window.Shadow.Width)/2); Canvas.SetTop(window.Shadow,window.Height-14);
        Canvas.SetLeft(window.Bubble,Math.Clamp(left+(window.AvatarHost.Width-window.Bubble.Width)/2,6,Math.Max(6,window.Width-window.Bubble.Width-6)));
        Canvas.SetTop(window.Bubble,Math.Max(4,window.Height-window.AvatarHost.Height-window.Bubble.ActualHeight-8));
    }
    private void SetPose(string name,bool front=true)
    {
        if(front)window.Facing.ScaleX=1;
        var ok=sprites.TryGetValue(name,out var image); window.AvatarImage.Source=image; window.Placeholder.Visibility=window.PlaceholderLabel.Visibility=ok?Visibility.Collapsed:Visibility.Visible;
    }
    private void Breathe()
    {
        if (!settings.BreathingEnabled) { StopBreathing(); return; }
        window.Breathing.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(1,1.015,TimeSpan.FromSeconds(1.4)){ AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever });
    }
    private void StopBreathing() { window.Breathing.BeginAnimation(ScaleTransform.ScaleYProperty,null); window.Breathing.ScaleY=1; }
    public void Dispose() { CompositionTarget.Rendering-=Render; }
}
