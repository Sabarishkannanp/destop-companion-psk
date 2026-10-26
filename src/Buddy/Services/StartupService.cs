using Microsoft.Win32;
namespace Buddy.Services;
public static class StartupService
{
    private const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool Enabled { get { using var key=Registry.CurrentUser.OpenSubKey(RunKey);return key?.GetValue("Buddy")!=null; } }
    public static void SetEnabled(bool enabled)
    { using var key=Registry.CurrentUser.CreateSubKey(RunKey);if(enabled)key.SetValue("Buddy",$"\"{Environment.ProcessPath}\"");else key.DeleteValue("Buddy",false); }
}
