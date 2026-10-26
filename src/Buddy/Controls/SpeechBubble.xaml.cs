using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
namespace Buddy.Controls;
public partial class SpeechBubble : UserControl
{
    public event Action? Confirmed;
    public event Action? Snoozed;
    public SpeechBubble() { InitializeComponent(); }
    public void Present(string message,bool actions=true,bool water=true)
    {
        MessageText.Text=message; Actions.Visibility=actions?Visibility.Visible:Visibility.Collapsed; YesButton.Content=water?"YES":"DONE"; Visibility=Visibility.Visible;
        PopScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,new DoubleAnimation(0.8,1,TimeSpan.FromMilliseconds(320)) { EasingFunction=new BackEase { EasingMode=EasingMode.EaseOut,Amplitude=0.25 } });
        PopScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty,new DoubleAnimation(0.8,1,TimeSpan.FromMilliseconds(320)) { EasingFunction=new BackEase { EasingMode=EasingMode.EaseOut,Amplitude=0.25 } });
        BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(200)));
    }
    private void YesClick(object sender,RoutedEventArgs e) => Confirmed?.Invoke();
    private void LaterClick(object sender,RoutedEventArgs e) => Snoozed?.Invoke();
}
