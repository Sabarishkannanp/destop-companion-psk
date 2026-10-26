using System.Windows;

namespace PskCompanion;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--smoke-test"))
        {
            StartupUri = null;
            var storage = new Services.CompanionStorage(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"PskCompanion-smoke-"+System.Guid.NewGuid()));
            storage.Config.SpeechVoiceEnabled = false; storage.Config.RemindersEnabled = false; storage.Config.AutoShowCompanion = false;
            MainWindow = new MainWindow(storage); MainWindow.Show();
        }
    }
}

