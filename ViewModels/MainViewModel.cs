using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;
using D2RBuffTracker.Models;
using D2RBuffTracker.Mvvm;
using D2RBuffTracker.Overlay;
using D2RBuffTracker.Services;

namespace D2RBuffTracker.ViewModels;

public enum AppSection
{
    Buffs,
    Options,
    About
}

/// <summary>
/// Top-level view model: owns the profile/buff data shown in the configuration
/// screens and coordinates the tracking engine and overlay window.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly TrackingEngine _engine;
    private readonly ForegroundWatcher _focusWatcher;
    private OverlayWindow? _overlay;
    private OverlayViewModel? _overlayVm;

    private TrackerProfile? _selectedProfile;
    private bool _isTracking;
    private bool _isPreview;
    private AppSection _selectedSection = AppSection.Buffs;

    public AppSettings Settings => App.Settings;

    public AppSection SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
                UpdatePreviewForSection();
        }
    }

    public RelayCommand NavigateCommand { get; }

    public ObservableCollection<TrackerProfile> Profiles => Settings.Profiles;

    public ObservableCollection<TrackedBuff> CurrentBuffs { get; } = new();

    public MainViewModel()
    {
        _engine = new TrackingEngine(Dispatcher.CurrentDispatcher);
        _engine.BuffActivated += OnBuffActivated;

        // Tracking follows the game: it starts when D2R is focused and pauses otherwise.
        _focusWatcher = new ForegroundWatcher();
        _focusWatcher.ForegroundChanged += OnForegroundChanged;

        NavigateCommand = new RelayCommand(p =>
        {
            if (p is AppSection s) SelectedSection = s;
            else if (p is string str && Enum.TryParse<AppSection>(str, out var parsed)) SelectedSection = parsed;
        });

        _selectedProfile = Profiles.FirstOrDefault(p => p.Id == Settings.LastSelectedProfileId)
                           ?? Profiles.FirstOrDefault();
        RefreshBuffs();

        _focusWatcher.Start();
    }

    /// <summary>
    /// Top-level window handle handed to the tracking engine so the gamepad
    /// poller can read controller input while the game has focus. Set once the
    /// main window's handle is available.
    /// </summary>
    public IntPtr InputWindowHandle
    {
        set
        {
            if (_engine.WindowHandle == value)
                return;
            _engine.WindowHandle = value;
            // Tracking can auto-start (game already focused) before the window
            // handle exists. Restart so the DirectInput fallback re-acquires with
            // the now-valid handle and its background cooperative level.
            if (IsTracking)
            {
                _engine.Stop();
                try
                {
                    _engine.Start(CurrentBuffs);
                }
                catch (Exception ex)
                {
                    // Restart failed (e.g. hook install) — don't leave IsTracking
                    // true, and tear down the overlay/timers so nothing lingers.
                    Services.Logger.Log(ex);
                    _engine.Stop();
                    IsTracking = false;
                    CloseOverlay();
                }
            }
        }
    }

    public TrackerProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                if (value != null)
                {
                    Settings.LastSelectedProfileId = value.Id;
                    Settings.Save();
                }
                RefreshBuffs();
            }
        }
    }

    public bool IsTracking
    {
        get => _isTracking;
        private set
        {
            SetProperty(ref _isTracking, value);
            OnPropertyChanged(nameof(IsNotTracking));
        }
    }

    public bool IsNotTracking => !_isTracking;

    public bool IsPreview
    {
        get => _isPreview;
        private set => SetProperty(ref _isPreview, value);
    }

    public bool HasBuffs => CurrentBuffs.Count > 0;

    #region Profiles

    public TrackerProfile AddProfile(string name, DiabloClass? cls = null)
    {
        var id = Profiles.Any() ? Profiles.Max(p => p.Id) + 1 : 0;
        var profile = new TrackerProfile { Id = id, Name = name, Class = cls };
        Profiles.Add(profile);
        SelectedProfile = profile;
        Settings.Save();
        return profile;
    }

    public void RenameProfile(TrackerProfile profile, string name)
    {
        profile.Name = name;
        Settings.Save();
    }

    public void DeleteProfile(TrackerProfile profile)
    {
        var related = Settings.Buffs.Where(b => b.ProfileId == profile.Id).ToList();
        foreach (var b in related)
            Settings.Buffs.Remove(b);

        Profiles.Remove(profile);
        if (Profiles.Count == 0)
            Profiles.Add(new TrackerProfile { Id = 0, Name = "Default" });

        SelectedProfile = Profiles.FirstOrDefault();
        Settings.Save();
    }

    #endregion

    #region Buffs

    private void RefreshBuffs()
    {
        CurrentBuffs.Clear();
        if (_selectedProfile != null)
        {
            foreach (var b in Settings.Buffs.Where(b => b.ProfileId == _selectedProfile.Id))
                CurrentBuffs.Add(b);
        }

        // Countdowns belong to the previous profile — clear them, and re-bind the
        // input engine to the new profile's buffs if tracking is active.
        _overlayVm?.Clear();
        if (IsTracking)
            _engine.Start(CurrentBuffs);

        OnPropertyChanged(nameof(HasBuffs));
    }

    public TrackedBuff AddBuffFromCatalog(BuffCatalogEntry entry)
    {
        var buff = new TrackedBuff
        {
            Id = NextBuffId(),
            ProfileId = _selectedProfile?.Id ?? 0,
            Name = entry.Name,
            Class = entry.Class,
            IconPath = entry.IconRelativePath,
            Duration = entry.DefaultDuration
        };
        AddBuff(buff);
        return buff;
    }

    public TrackedBuff AddCustomBuff()
    {
        var buff = new TrackedBuff
        {
            Id = NextBuffId(),
            ProfileId = _selectedProfile?.Id ?? 0,
            Name = "Custom Buff",
            Duration = 30
        };
        AddBuff(buff);
        return buff;
    }

    private void AddBuff(TrackedBuff buff)
    {
        Settings.Buffs.Add(buff);
        CurrentBuffs.Add(buff);
        OnPropertyChanged(nameof(HasBuffs));
        Settings.Save();
    }

    public void RemoveBuff(TrackedBuff buff)
    {
        Settings.Buffs.Remove(buff);
        CurrentBuffs.Remove(buff);
        OnPropertyChanged(nameof(HasBuffs));
        Settings.Save();
    }

    public TrackedBuff DuplicateBuff(TrackedBuff buff)
    {
        var copy = buff.Clone();
        copy.Id = NextBuffId();
        copy.Name = buff.Name + " Copy";
        AddBuff(copy);
        return copy;
    }

    public void SaveBuffs() => Settings.Save();

    private int NextBuffId() => Settings.Buffs.Any() ? Settings.Buffs.Max(b => b.Id) + 1 : 1;

    #endregion

    #region Tracking / Overlay

    public void StartTracking()
    {
        if (IsTracking)
            return;

        _engine.Start(CurrentBuffs);
        IsTracking = true;
        ReconcileOverlay();
    }

    public void StopTracking()
    {
        if (IsTracking)
        {
            _engine.Stop();
            IsTracking = false;
        }
        ReconcileOverlay();
    }

    private void OnForegroundChanged(ForegroundApp app)
    {
        if (app == ForegroundApp.Game)
            StartTracking();
        else
            StopTracking();
    }

    /// <summary>
    /// Reconcile the overlay window with the current state. Crucially, live buff
    /// countdowns are never destroyed by focus changes: when the game loses focus
    /// the overlay is merely hidden (its Stopwatch-based timers keep running in
    /// real time), so the cooldowns are still there when the player tabs back in.
    /// </summary>
    private void ReconcileOverlay()
    {
        var positioning = !IsTracking && _focusWatcher.Current == ForegroundApp.Self;

        if (IsTracking)
        {
            // In game: show the live countdowns, click-through, no samples.
            IsPreview = false;
            EnsureOverlay(preview: false);
            _overlayVm?.ClearPreviewSamples();
            _overlay?.SetPreview(false);
            ShowOverlayWindow();
        }
        else if (positioning)
        {
            // Positioning in our own app: draggable overlay. Position using the
            // live countdowns when present, otherwise show sample data.
            IsPreview = true;
            EnsureOverlay(preview: true);
            if (_overlayVm is { HasLiveTimers: false })
                _overlayVm.LoadPreviewSamples();
            _overlay?.SetPreview(true);
            ShowOverlayWindow();
        }
        else
        {
            // Neither in game nor positioning: preserve any live countdowns by
            // hiding (not closing) the overlay so they keep running; if nothing
            // is live, close the overlay entirely.
            IsPreview = false;
            _overlayVm?.ClearPreviewSamples();
            if (_overlayVm is { HasLiveTimers: true })
                _overlay?.Hide();
            else
                CloseOverlay();
        }
    }

    /// <summary>Re-show the overlay window if it was hidden while out of the game.</summary>
    private void ShowOverlayWindow()
    {
        if (_overlay is { IsVisible: false })
            _overlay.Show();
    }

    /// <summary>
    /// Show the draggable positioning overlay the whole time our own window (or
    /// its overlay) is the active app — not merely when the Options page is
    /// open, and not merely whenever the game lacks focus.
    /// </summary>
    private void UpdatePreviewForSection() => ReconcileOverlay();

    public void SetPreviewMode(bool on) => ReconcileOverlay();

    public void UpdateOverlayScale(double scale)
    {
        Settings.OverlayScale = scale;
        _overlay?.ApplyScale(scale);
        Settings.Save();
    }

    public void RefreshOverlayLayout()
    {
        if (_overlayVm != null)
            _overlayVm.Orientation = Settings.IsOverlayVertical
                ? System.Windows.Controls.Orientation.Vertical
                : System.Windows.Controls.Orientation.Horizontal;
    }

    public void RefreshOverlayDigits()
    {
        if (_overlayVm != null)
        {
            _overlayVm.ShowDigits = Settings.ShowDigits;
            _overlayVm.DigitFontSize = Settings.DigitFontSize;
        }
    }

    public void RefreshOverlaySwipe()
    {
        if (_overlayVm != null)
            _overlayVm.ShowSwipe = Settings.ShowCooldownSwipe;
    }

    private void EnsureOverlay(bool preview)
    {
        if (_overlay == null)
        {
            _overlayVm = new OverlayViewModel { IsPreview = preview };
            _overlay = new OverlayWindow(_overlayVm);
            _overlay.Show();
            _overlay.SetPreview(preview);
        }
    }

    private void CloseOverlay()
    {
        _overlayVm?.Clear();
        _overlay?.Close();
        _overlay = null;
        _overlayVm = null;
        IsPreview = false;
    }

    private void OnBuffActivated(TrackedBuff buff)
    {
        _overlayVm?.Activate(buff);
    }

    public void Shutdown()
    {
        _focusWatcher.Dispose();
        _engine.Dispose();
        CloseOverlay();
        Settings.Save();
    }

    #endregion
}
