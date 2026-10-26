using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Buddy.Services;
namespace Buddy;
public partial class StatsWindow : Window
{
    public StatsWindow(Storage storage)
    {
        InitializeComponent();SourceInitialized+=(_,_)=>Buddy.Interop.NativeMethods.ApplyDarkTitleBar(new System.Windows.Interop.WindowInteropHelper(this).Handle);TodayText.Text=$"{storage.Glasses(DateTime.Today)} glasses today";
        var days=Enumerable.Range(0,7).Select(i=>DateTime.Today.AddDays(i-6)).ToArray();var max=Math.Max(1,days.Max(storage.Glasses));
        foreach(var day in days)
        { var count=storage.Glasses(day);var grid=new Grid { Margin=new Thickness(5,0,5,0) };grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); var stack=new StackPanel { VerticalAlignment=VerticalAlignment.Bottom };stack.Children.Add(new TextBlock { Text=count.ToString(),HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,5),Foreground=(Brush)FindResource("MutedText") });stack.Children.Add(new Rectangle { Height=Math.Max(3,110d*count/max),RadiusX=7,RadiusY=7,Fill=new SolidColorBrush(day==DateTime.Today?Color.FromRgb(255,208,90):Color.FromRgb(63,82,115)) });grid.Children.Add(stack);var label=new TextBlock { Text=day.ToString("ddd"),HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,10,0,0),FontSize=11 };Grid.SetRow(label,1);grid.Children.Add(label);Chart.Children.Add(grid); }
    }
}
