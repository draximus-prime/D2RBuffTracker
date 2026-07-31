using System.Collections.ObjectModel;
using System.IO;
using D2RBuffTracker.Mvvm;
using Newtonsoft.Json;

namespace D2RBuffTracker.Models;

public enum AppTheme
{
    System,
    Dark,
    Light
}

/// <summary>
/// All persisted application state, serialized to JSON in the user's
/// local application data folder.
/// </summary>
public sealed class AppSettings : ObservableObject
{
    [JsonIgnore] private string? _filePath;

    private bool _isOverlayVertical;
    private bool _insertNewToStart;
    private bool _showDigits = true;
    private double _digitFontSize = 17.0;
    private bool _showCooldownSwipe = true;
    private bool _soundsEnabled = true;
    private bool _minimizeToTrayOnClose = true;
    private double _overlayScale = 1.0;
    private AppTheme _theme = AppTheme.Dark;

    public ObservableCollection<TrackerProfile> Profiles { get; set; } = new();
    public ObservableCollection<TrackedBuff> Buffs { get; set; } = new();

    public int LastSelectedProfileId { get; set; }

    public int OverlayX { get; set; } = 200;
    public int OverlayY { get; set; } = 200;

    public double OverlayScale
    {
        get => _overlayScale;
        set => SetProperty(ref _overlayScale, value);
    }

    public bool IsOverlayVertical
    {
        get => _isOverlayVertical;
        set => SetProperty(ref _isOverlayVertical, value);
    }

    public bool InsertNewToStart
    {
        get => _insertNewToStart;
        set => SetProperty(ref _insertNewToStart, value);
    }

    public bool ShowDigits
    {
        get => _showDigits;
        set => SetProperty(ref _showDigits, value);
    }

    /// <summary>Font size of the countdown number shown under each overlay icon.</summary>
    public double DigitFontSize
    {
        get => _digitFontSize;
        set => SetProperty(ref _digitFontSize, value);
    }

    /// <summary>Show the radial cooldown sweep over each overlay icon.</summary>
    public bool ShowCooldownSwipe
    {
        get => _showCooldownSwipe;
        set => SetProperty(ref _showCooldownSwipe, value);
    }

    /// <summary>Master switch for all automatic overlay sounds.</summary>
    public bool SoundsEnabled
    {
        get => _soundsEnabled;
        set => SetProperty(ref _soundsEnabled, value);
    }

    /// <summary>When true, closing the window hides it to the tray instead of exiting.</summary>
    public bool MinimizeToTrayOnClose
    {
        get => _minimizeToTrayOnClose;
        set => SetProperty(ref _minimizeToTrayOnClose, value);
    }

    /// <summary>Whether the "still running in the tray" notice has already been shown once.</summary>
    public bool TrayHintShown { get; set; }

    public AppTheme Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    public void UpdatePath(string path) => _filePath = path;

    /// <summary>
    /// Restore the overlay appearance and behaviour options to their defaults.
    /// Does not touch profiles, tracked buffs, or the saved overlay position.
    /// </summary>
    public void ResetDisplayDefaults()
    {
        OverlayScale = 1.0;
        IsOverlayVertical = false;
        InsertNewToStart = false;
        ShowDigits = true;
        DigitFontSize = 17.0;
        ShowCooldownSwipe = true;
        SoundsEnabled = true;
        MinimizeToTrayOnClose = true;
        Theme = AppTheme.Dark;
    }

    public void Save()
    {
        if (string.IsNullOrEmpty(_filePath))
            return;
        try
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            // Write to a temporary file and swap it into place so a crash mid-write
            // can never leave a truncated / corrupt settings file behind.
            var tmp = _filePath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(_filePath))
                File.Replace(tmp, _filePath, null);
            else
                File.Move(tmp, _filePath);
        }
        catch (Exception ex)
        {
            Services.Logger.Log(ex);
        }
    }

    public static AppSettings Load(string filePath)
    {
        AppSettings result;
        try
        {
            result = File.Exists(filePath)
                ? JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(filePath)) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception ex)
        {
            Services.Logger.Log(ex);
            result = new AppSettings();
        }

        // Valid JSON can still contain explicit nulls for collections; normalise
        // them so the rest of the app never has to null-check.
        result.Profiles ??= new ObservableCollection<TrackerProfile>();
        result.Buffs ??= new ObservableCollection<TrackedBuff>();

        if (result.Profiles.Count == 0)
            result.Profiles.Add(new TrackerProfile { Id = 0, Name = "Default" });

        result.UpdatePath(filePath);
        return result;
    }
}
