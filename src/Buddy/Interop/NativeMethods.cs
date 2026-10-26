using System.Runtime.InteropServices;
using System.Text;
namespace Buddy.Interop;
internal static class NativeMethods
{
    internal const int GwlExStyle=-20, ToolWindow=0x80, NoActivate=0x08000000, Layered=0x80000, Transparent=0x20;
    internal const uint NoSize=1, NoMove=2, NoActivation=0x10;
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] internal struct LastInputInfo { public uint Size,Time; }
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] private static extern nint GetWindowLongPtr64(nint hwnd,int index);
    [DllImport("user32.dll", EntryPoint="GetWindowLongW")] private static extern int GetWindowLong32(nint hwnd,int index);
    [DllImport("user32.dll", EntryPoint="SetWindowLongPtrW")] private static extern nint SetWindowLongPtr64(nint hwnd,int index,nint value);
    [DllImport("user32.dll", EntryPoint="SetWindowLongW")] private static extern int SetWindowLong32(nint hwnd,int index,int value);
    internal static nint GetWindowLongPtr(nint hwnd,int index) => nint.Size==8?GetWindowLongPtr64(hwnd,index):GetWindowLong32(hwnd,index);
    internal static void SetWindowLongPtr(nint hwnd,int index,nint value) { if(nint.Size==8) SetWindowLongPtr64(hwnd,index,value); else SetWindowLong32(hwnd,index,(int)value); }
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int cx,int cy,uint flags);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd,out Rect rect);
    [DllImport("user32.dll")] internal static extern nint MonitorFromWindow(nint hwnd,uint flags);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetClassName(nint hwnd,StringBuilder name,int count);
    [DllImport("user32.dll")] internal static extern bool GetLastInputInfo(ref LastInputInfo info);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd,int attribute,ref int value,int size);
    internal static void ApplyDarkTitleBar(nint hwnd)
    { var enabled=1;DwmSetWindowAttribute(hwnd,20,ref enabled,sizeof(int));var rounded=2;DwmSetWindowAttribute(hwnd,33,ref rounded,sizeof(int)); }
    internal static TimeSpan IdleTime()
    { var input=new LastInputInfo { Size=(uint)Marshal.SizeOf<LastInputInfo>() }; return GetLastInputInfo(ref input)?TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount-input.Time)):TimeSpan.Zero; }
    internal static bool IsFullscreen()
    {
        var hwnd=GetForegroundWindow(); if(hwnd==0)return false;
        var name=new StringBuilder(256); GetClassName(hwnd,name,256); if(name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd")return false;
        var info=new MonitorInfo { Size=Marshal.SizeOf<MonitorInfo>() };
        if(!GetWindowRect(hwnd,out var r)||!GetMonitorInfo(MonitorFromWindow(hwnd,2),ref info))return false;
        var m=info.Monitor; return r.Left<=m.Left&&r.Top<=m.Top&&r.Right>=m.Right&&r.Bottom>=m.Bottom;
    }
}
