using DeskTip.Core;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using DeskTip.Services;
namespace DeskTip;
public partial class PetWindow : Window
{
    private readonly DispatcherTimer dismiss = new() { Interval = TimeSpan.FromSeconds(45) };
    public event Action? SnoozeRequested;
    public PetWindow(ReminderRule rule, bool preview)
    {
        InitializeComponent(); TitleText.Text = rule.Title; MessageText.Text = rule.Message;
        PetImage.Source = AssetService.Pet;
        if (preview) { LabelText.Text = "提醒预览 · 不改变正式计时"; SnoozeButton.IsEnabled = false; }
        else { var service = ((App)Application.Current).Reminders; SnoozeButton.IsEnabled = ScheduleEngine.SamePeriod(service.Settings, service.Now, service.Now.AddMinutes(5)); }
        dismiss.Tick += (_, _) => Close();
        Loaded += (_, _) => { var area = SystemParameters.WorkArea; Left = Math.Max(area.Left + 8, area.Right - ActualWidth - 22); Top = Math.Max(area.Top + 8, area.Bottom - ActualHeight - 16); dismiss.Start(); };
        Closed += (_, _) => dismiss.Stop();
    }
    private void Dismiss_Click(object sender, RoutedEventArgs e) => Close();
    private void Snooze_Click(object sender, RoutedEventArgs e) { SnoozeRequested?.Invoke(); Close(); }
    private void Drag_MouseDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
}
