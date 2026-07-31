using D2RBuffTracker.Mvvm;
using Newtonsoft.Json;

namespace D2RBuffTracker.Models;

/// <summary>
/// A buff the user has chosen to track within a profile. Holds its display
/// data plus a small state machine that recognises the optional
/// "select key then use key" activation sequence used in game.
/// </summary>
public sealed class TrackedBuff : ObservableObject
{
    private string _name = "New Buff";
    private string? _iconPath;
    private double _duration = 30;
    private bool _isEnabled = true;
    private InputBinding? _selectKey;
    private InputBinding? _useKey;
    private int _redWarningSeconds = 5;
    private int _amberWarningSeconds = 10;
    private bool _redSoundEnabled;
    private string _redSoundName = "warning";
    private bool _amberSoundEnabled;
    private string _amberSoundName = "beep";
    private bool _expireSoundEnabled = true;
    private string _expireSoundName = "expire";

    /// <summary>0 = idle, 1 = select key pressed (awaiting use key).</summary>
    [JsonIgnore] private volatile int _state;

    public int Id { get; set; }
    public int ProfileId { get; set; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>Owning class for organizing/filtering. Null for custom buffs.</summary>
    public DiabloClass? Class { get; set; }

    /// <summary>Icon path, stored relative to the app base directory when possible.</summary>
    public string? IconPath
    {
        get => _iconPath;
        set => SetProperty(ref _iconPath, value);
    }

    public double Duration
    {
        get => _duration;
        set => SetProperty(ref _duration, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public InputBinding? SelectKey
    {
        get => _selectKey;
        set => SetProperty(ref _selectKey, value);
    }

    public InputBinding? UseKey
    {
        get => _useKey;
        set => SetProperty(ref _useKey, value);
    }

    /// <summary>Seconds remaining at which this buff's icon turns red. 0 disables it.</summary>
    public int RedWarningSeconds
    {
        get => _redWarningSeconds;
        set
        {
            SetProperty(ref _redWarningSeconds, value);
            OnPropertyChanged(nameof(IsRedWarningEnabled));
            // Keep amber strictly above red so the two scales never clash.
            if (_amberWarningSeconds > 0 && _amberWarningSeconds <= value)
                AmberWarningSeconds = value + 1;
        }
    }

    [JsonIgnore]
    public bool IsRedWarningEnabled => _redWarningSeconds > 0;

    /// <summary>Seconds remaining at which this buff's icon turns amber. 0 disables it.</summary>
    public int AmberWarningSeconds
    {
        get => _amberWarningSeconds;
        set
        {
            // Amber must trigger before red; clamp so it never overlaps.
            if (value > 0 && value <= _redWarningSeconds)
                value = _redWarningSeconds + 1;
            SetProperty(ref _amberWarningSeconds, value);
            OnPropertyChanged(nameof(IsAmberWarningEnabled));
        }
    }

    [JsonIgnore]
    public bool IsAmberWarningEnabled => _amberWarningSeconds > 0;

    public bool RedSoundEnabled
    {
        get => _redSoundEnabled;
        set => SetProperty(ref _redSoundEnabled, value);
    }

    public string RedSoundName
    {
        get => _redSoundName;
        set => SetProperty(ref _redSoundName, value);
    }

    public bool AmberSoundEnabled
    {
        get => _amberSoundEnabled;
        set => SetProperty(ref _amberSoundEnabled, value);
    }

    public string AmberSoundName
    {
        get => _amberSoundName;
        set => SetProperty(ref _amberSoundName, value);
    }

    /// <summary>Play a sound when this buff expires (reaches 0).</summary>
    public bool ExpireSoundEnabled
    {
        get => _expireSoundEnabled;
        set => SetProperty(ref _expireSoundEnabled, value);
    }

    public string ExpireSoundName
    {
        get => _expireSoundName;
        set => SetProperty(ref _expireSoundName, value);
    }

    /// <summary>
    /// Process a press of the buff's use key. Returns true when the buff should
    /// fire (either no select key is required, or the select key was pressed first).
    /// </summary>
    public bool OnUseKeyPressed()
    {
        if (_state == 0 && SelectKey == null)
            return true;

        if (_state == 1)
        {
            _state = 0;
            return true;
        }

        return false;
    }

    /// <summary>Process a press of the buff's select key.</summary>
    public void OnSelectKeyPressed()
    {
        if (_state == 0)
            _state = 1;
    }

    /// <summary>Reset the activation sequence.</summary>
    public void ResetSequence() => _state = 0;

    public TrackedBuff Clone() => new()
    {
        Id = Id,
        ProfileId = ProfileId,
        Name = Name,
        Class = Class,
        IconPath = IconPath,
        Duration = Duration,
        IsEnabled = IsEnabled,
        SelectKey = SelectKey?.Clone(),
        UseKey = UseKey?.Clone(),
        RedWarningSeconds = RedWarningSeconds,
        AmberWarningSeconds = AmberWarningSeconds,
        RedSoundEnabled = RedSoundEnabled,
        RedSoundName = RedSoundName,
        AmberSoundEnabled = AmberSoundEnabled,
        AmberSoundName = AmberSoundName,
        ExpireSoundEnabled = ExpireSoundEnabled,
        ExpireSoundName = ExpireSoundName
    };
}
