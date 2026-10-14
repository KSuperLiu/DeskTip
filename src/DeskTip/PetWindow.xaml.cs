using DeskTip.Core;
using DeskTip.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;

namespace DeskTip;

public sealed class NotificationCard : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public void RefreshTheme() => PropertyChanged?.Invoke(this, new(nameof(Accent)));
    public ReminderOccurrence Occurrence { get; }
    public string Name => Occurrence.Reminder.Name;
    public string Message => Occurrence.Message;
    public Geometry Icon => IconArt.Get(Occurrence.Reminder.IconKey);
    public Brush Accent => ThemeService.Accent(Occurrence.Reminder.Accent);
    public bool CanSnooze { get; }
    public string SnoozeLabel => "稍后 " + Occurrence.Reminder.SnoozeMinutes + " 分钟";
    public NotificationCard(ReminderOccurrence occurrence, bool preview) { Occurrence = occurrence; CanSnooze = !preview && ((App)Application.Current).Reminders.CanSnooze(occurrence.Reminder); }
}
public partial class PetWindow : Window
{
    private readonly ObservableCollection<NotificationCard> cards = new();
    private readonly DispatcherTimer dismiss = new() { Interval = TimeSpan.FromSeconds(45) };
    private readonly bool preview;
    public event Action<string>? SnoozeRequested;
    public PetWindow(IReadOnlyList<ReminderOccurrence> occurrences, bool preview)
    {
        this.preview = preview; InitializeComponent(); NotificationItems.ItemsSource = cards; Add(occurrences);
        dismiss.Tick += (_, _) => Close(); Loaded += (_, _) => { Position(); dismiss.Start(); }; Closed += (_, _) => dismiss.Stop();
        SizeChanged += (_, _) => { if (IsLoaded) Position(); };
        ThemeService.Changed += RefreshTheme; Closed += (_, _) => ThemeService.Changed -= RefreshTheme;
    }
    private void RefreshTheme() { foreach (var card in cards) card.RefreshTheme(); }
    public void Add(IReadOnlyList<ReminderOccurrence> occurrences)
    {
        foreach (var occurrence in occurrences)
        {
            var old = cards.FirstOrDefault(c => c.Occurrence.Reminder.Id == occurrence.Reminder.Id); if (old != null) cards.Remove(old);
            cards.Add(new(occurrence, preview));
        }
        UpdateHeader(); if (IsLoaded) { dismiss.Stop(); dismiss.Start(); }
    }
    private void UpdateHeader()
    {
        LabelText.Text = preview ? "DESKTIP · 提醒预览，不改变计时" : cards.Count > 1 ? $"DESKTIP · {cards.Count} 条提醒到时间了" : "DESKTIP · 到时间了";
        CompleteAll.Visibility = cards.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        bool companion = cards.Any(c => c.Occurrence.Reminder.ShowCompanion);
        CompanionRow.Height = new GridLength(companion ? 175 : 0); CompanionImage.Visibility = companion ? Visibility.Visible : Visibility.Collapsed;
        if (companion) CompanionImage.Source = AssetService.Pet;
    }
    private void Position() { var area = SystemParameters.WorkArea; Left = Math.Max(area.Left + 8, area.Right - ActualWidth - 22); Top = Math.Max(area.Top + 8, area.Bottom - ActualHeight - 16); }
    private void Remove(NotificationCard card) { cards.Remove(card); if (cards.Count == 0) Close(); else UpdateHeader(); }
    public void Prune(Func<ReminderOccurrence, bool> valid) { foreach (var card in cards.Where(c => !valid(c.Occurrence)).ToArray()) Remove(card); }
    private void Complete_Click(object sender, RoutedEventArgs e) => Remove((NotificationCard)((Button)sender).Tag);
    private void Snooze_Click(object sender, RoutedEventArgs e) { var card = (NotificationCard)((Button)sender).Tag; if (!((App)Application.Current).Reminders.CanSnooze(card.Occurrence.Reminder)) return; SnoozeRequested?.Invoke(card.Occurrence.Reminder.Id); Remove(card); }
    private void Dismiss_Click(object sender, RoutedEventArgs e) => Close();
    private void Drag_MouseDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
}
