using System.Diagnostics;
using System.Media;
using System.Windows.Threading;
using D2RBuffTracker.Models;
using D2RBuffTracker.Mvvm;
using D2RBuffTracker.Services;

namespace D2RBuffTracker.Overlay;

/// <summary>
/// A single active buff countdown shown in the overlay. The remaining time is
/// derived from a <see cref="Stopwatch"/> and sampled frequently so the radial
/// cooldown sweep animates smoothly, while the digit and threshold sounds still
/// react on whole-second boundaries.
/// </summary>
public sealed class BuffTimerViewModel : ObservableObject
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private double _remaining;
    private bool _isWarning;
    private bool _isAmber;
    private bool _isLow;
    private bool _isExpired;

    public TrackedBuff Buff { get; }

    /// <summary>True for non-live sample entries used only to position the overlay.</summary>
    public bool IsStatic { get; }

    public string? IconPath => Buff.IconPath;

    public event Action<BuffTimerViewModel>? Completed;

    public BuffTimerViewModel(TrackedBuff buff, bool isStatic = false, double? staticRemaining = null)
    {
        Buff = buff;
        IsStatic = isStatic;
        _remaining = staticRemaining ?? buff.Duration;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += OnTick;
        if (!isStatic)
        {
            _clock.Start();
            _timer.Start();
        }
        UpdateFlags();
    }

    public double Remaining
    {
        get => _remaining;
        private set { SetProperty(ref _remaining, value); OnPropertyChanged(nameof(DisplaySeconds)); OnPropertyChanged(nameof(Progress)); }
    }

    public int DisplaySeconds => (int)Math.Ceiling(_remaining);

    public double Progress => Buff.Duration <= 0 ? 0 : Math.Clamp(_remaining / Buff.Duration, 0, 1);

    public bool IsWarning
    {
        get => _isWarning;
        private set => SetProperty(ref _isWarning, value);
    }

    public bool IsAmber
    {
        get => _isAmber;
        private set => SetProperty(ref _isAmber, value);
    }

    public bool IsLow
    {
        get => _isLow;
        private set => SetProperty(ref _isLow, value);
    }

    public bool IsExpired
    {
        get => _isExpired;
        private set => SetProperty(ref _isExpired, value);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var wasWarning = _isWarning;
        var wasAmber = _isAmber;

        Remaining = Math.Max(0, Buff.Duration - _clock.Elapsed.TotalSeconds);
        UpdateFlags();

        // Edge-triggered: play the threshold sound only as we cross into it.
        if (_isWarning && !wasWarning)
            PlaySound(Buff.RedSoundEnabled, Buff.RedSoundName);
        else if (_isAmber && !wasAmber)
            PlaySound(Buff.AmberSoundEnabled, Buff.AmberSoundName);

        if (_remaining <= 0)
            Expire();
    }

    private void UpdateFlags()
    {
        var red = Buff.IsRedWarningEnabled && _remaining <= Buff.RedWarningSeconds;
        // Amber sits above red and never overlaps it.
        var amber = !red && Buff.IsAmberWarningEnabled && _remaining <= Buff.AmberWarningSeconds;
        IsWarning = red;
        IsAmber = amber;
        IsLow = (amber || red) && _remaining > 0;
    }

    /// <summary>Restart the countdown (buff re-cast before expiry).</summary>
    public void Reset()
    {
        _clock.Restart();
        Remaining = Buff.Duration;
        IsExpired = false;
        UpdateFlags();
        if (!_timer.IsEnabled)
            _timer.Start();
    }

    private void Expire()
    {
        _timer.Stop();
        _clock.Stop();
        IsExpired = true;
        IsLow = false;
        IsWarning = true;
        IsAmber = false;
        Remaining = 0;
        PlayExpireSound();
        Completed?.Invoke(this);
    }

    private void PlayExpireSound()
    {
        if (!App.Settings.SoundsEnabled || !Buff.ExpireSoundEnabled)
            return;
        try
        {
            var path = AssetPaths.Sound(Buff.ExpireSoundName);
            if (System.IO.File.Exists(path))
            {
                using var player = new SoundPlayer(path);
                player.Play();
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    private static void PlaySound(bool enabled, string? name)
    {
        if (!enabled || !App.Settings.SoundsEnabled)
            return;
        try
        {
            var path = AssetPaths.Sound(name);
            if (System.IO.File.Exists(path))
            {
                using var player = new SoundPlayer(path);
                player.Play();
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    public void Stop()
    {
        _timer.Stop();
        _clock.Stop();
        _timer.Tick -= OnTick;
    }
}
