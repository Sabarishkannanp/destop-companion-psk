using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using Buddy.Interop;
using Buddy.Models;
namespace Buddy;
public partial class OverlayWindow : Window
{
    private nint handle;
    private bool clickThrough=true;
    private readonly DispatcherTimer pointerTimer=new() { Interval=TimeSpan.FromMilliseconds(30) };
    private readonly DispatcherTimer topTimer=new() { Interval=TimeSpan.FromSeconds(5) };
    private double dragOffset;
    public AvatarController Avatar { get; }
    public Controls.SpeechBubble Speech => Bubble;
    public event Action? FunRequested;
    public void RequestFun() => FunRequested?.Invoke();
    public OverlayWindow(BuddySettings settings)
    {
        InitializeComponent(); Reposition(); Avatar=new(this,settings);
        AvatarHost.LostMouseCapture+=(_,_)=>Avatar.Dragging=false;
        SourceInitialized+=InitializeNative;
        pointerTimer.Tick+=PointerTick;
        topTimer.Tick+=(_,_)=>NativeMethods.SetWindowPos(handle,new nint(-1),0,0,0,0,NativeMethods.NoMove|NativeMethods.NoSize|NativeMethods.NoActivation);
        SystemEvents.DisplaySettingsChanged+=DisplayChanged; SystemParameters.StaticPropertyChanged+=WorkAreaChanged;
        Closed+=(_,_)=> { pointerTimer.Stop(); topTimer.Stop(); Avatar.Dispose(); SystemEvents.DisplaySettingsChanged-=DisplayChanged; SystemParameters.StaticPropertyChanged-=WorkAreaChanged; };
    }
    private void InitializeNative(object? sender,EventArgs e)
    {
        handle=new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowLongPtr(handle,NativeMethods.GwlExStyle,NativeMethods.GetWindowLongPtr(handle,NativeMethods.GwlExStyle)|NativeMethods.ToolWindow|NativeMethods.NoActivate|NativeMethods.Layered|NativeMethods.Transparent);
        HwndSource.FromHwnd(handle)?.AddHook(WindowHook); pointerTimer.Start(); topTimer.Start();
    }
    private nint WindowHook(nint hwnd,int msg,nint wp,nint lp,ref bool handled)
    { if(msg==0x02E0)Dispatcher.BeginInvoke(Reposition,DispatcherPriority.Loaded); if(msg==0x0021){handled=true;return new nint(3);} return 0; }
    private void Reposition() { var area=SystemParameters.WorkArea; Left=area.Left; Top=area.Bottom-420; Width=area.Width; Height=420; }
    private void DisplayChanged(object? sender,EventArgs e)=>Dispatcher.BeginInvoke(Reposition);
    private void WorkAreaChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e) { if(e.PropertyName==nameof(SystemParameters.WorkArea))Dispatcher.BeginInvoke(Reposition); }
    private void PointerTick(object? sender,EventArgs e)
    {
        if(!NativeMethods.GetCursorPos(out var p))return;
        var local=PointFromScreen(new Point(p.X,p.Y));
        bool Contains(FrameworkElement el) { if(el.Visibility!=Visibility.Visible)return false; var origin=el.TranslatePoint(new Point(),this); return new Rect(origin,new Size(el.ActualWidth,el.ActualHeight)).Contains(local); }
        var shouldPass=!(Avatar.Dragging||Contains(AvatarHost)||Contains(Bubble)); if(shouldPass==clickThrough)return;
        var style=NativeMethods.GetWindowLongPtr(handle,NativeMethods.GwlExStyle);
        NativeMethods.SetWindowLongPtr(handle,NativeMethods.GwlExStyle,shouldPass?style|NativeMethods.Transparent:style&~NativeMethods.Transparent); clickThrough=shouldPass;
    }
    private void AvatarDown(object sender,MouseButtonEventArgs e)
    { if(Avatar.State is AvatarState.Happy or AvatarState.Sad or AvatarState.WalkOut)return; if(e.ClickCount==2){FunRequested?.Invoke();return;} Avatar.Dragging=true; dragOffset=e.GetPosition(Stage).X-Avatar.X; AvatarHost.CaptureMouse(); e.Handled=true; }
    private void AvatarMove(object sender,MouseEventArgs e) { if(Avatar.Dragging)Avatar.MoveTo(e.GetPosition(Stage).X-dragOffset); }
    private void AvatarUp(object sender,MouseButtonEventArgs e) { Avatar.Dragging=false; AvatarHost.ReleaseMouseCapture(); }
}
