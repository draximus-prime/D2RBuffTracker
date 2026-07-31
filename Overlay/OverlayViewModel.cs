using System.Collections.ObjectModel;
using System.Windows.Controls;
using D2RBuffTracker.Models;
using D2RBuffTracker.Mvvm;

namespace D2RBuffTracker.Overlay;

/// <summary>
/// Backs the overlay window: keeps the collection of active buff countdowns and
/// exposes layout options derived from settings.
/// </summary>
public sealed class OverlayViewModel : ObservableObject
{
    private Orientation _orientation;
    private bool _isPreview;
    private bool _showDigits = true;
    private double _digitFontSize = 20.0;
    private bool _showSwipe = true;

    public ObservableCollection<BuffTimerViewModel> Items { get; } = new();

    public Orientation Orientation
    {
        get => _orientation;
        set => SetProperty(ref _orientation, value);
    }

    public bool ShowDigits
    {
        get => _showDigits;
        set => SetProperty(ref _showDigits, value);
    }

    public double DigitFontSize
    {
        get => _digitFontSize;
        set => SetProperty(ref _digitFontSize, value);
    }

    public bool ShowSwipe
    {
        get => _showSwipe;
        set => SetProperty(ref _showSwipe, value);
    }

    public bool IsPreview
    {
        get => _isPreview;
        set { SetProperty(ref _isPreview, value); OnPropertyChanged(nameof(IsNotPreview)); }
    }

    public bool IsNotPreview => !_isPreview;

    public OverlayViewModel()
    {
        Orientation = App.Settings.IsOverlayVertical ? Orientation.Vertical : Orientation.Horizontal;
        ShowDigits = App.Settings.ShowDigits;
        DigitFontSize = App.Settings.DigitFontSize;
        ShowSwipe = App.Settings.ShowCooldownSwipe;
    }

    /// <summary>Add a new countdown, or reset an existing one for the same buff.</summary>
    public void Activate(TrackedBuff buff)
    {
        var existing = Items.FirstOrDefault(i => i.Buff.Id == buff.Id);
        if (existing != null)
        {
            existing.Reset();
            return;
        }

        var item = new BuffTimerViewModel(buff);
        item.Completed += OnCompleted;

        if (App.Settings.InsertNewToStart)
            Items.Insert(0, item);
        else
            Items.Add(item);
    }

    private void OnCompleted(BuffTimerViewModel item)
    {
        // Keep the expired buff on the overlay (red, no number) as a recast
        // reminder. It will be reset if the buff is cast again.
    }

    /// <summary>True while at least one real (non-sample) countdown is active.</summary>
    public bool HasLiveTimers => Items.Any(i => !i.IsStatic);

    /// <summary>Show sample data so the user can position/size the overlay.</summary>
    public void LoadPreviewSamples()
    {
        ClearPreviewSamples();
        var samples = BuffCatalog.ForClass(DiabloClass.Barbarian).Take(3).ToList();
        // Use Shiver Armor's icon for the first preview slot instead of Battle Cry.
        samples[0] = BuffCatalog.All.First(b => b.Name == "Shiver Armor");
        // Full duration vs. shown remaining so the cooldown sweep is partway round.
        var durations = new double[] { 60, 40, 30 };
        var remaining = new double[] { 45, 20, 6 };
        var id = 1;
        for (var i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            var buff = new TrackedBuff
            {
                Id = id++,
                Name = s.Name,
                Class = s.Class,
                IconPath = s.IconRelativePath,
                Duration = i < durations.Length ? durations[i] : 30,
                AmberWarningSeconds = 10,
                RedWarningSeconds = 5
            };
            var rem = i < remaining.Length ? remaining[i] : buff.Duration;
            // Static timers: they must not count down or expire while positioning.
            Items.Add(new BuffTimerViewModel(buff, isStatic: true, staticRemaining: rem));
        }
    }

    public void Clear()
    {
        foreach (var i in Items)
            i.Stop();
        Items.Clear();
    }

    /// <summary>Remove only the positioning samples, leaving live countdowns intact.</summary>
    public void ClearPreviewSamples()
    {
        foreach (var i in Items.Where(i => i.IsStatic).ToList())
        {
            i.Stop();
            Items.Remove(i);
        }
    }
}
