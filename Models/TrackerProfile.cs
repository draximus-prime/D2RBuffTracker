using D2RBuffTracker.Mvvm;

namespace D2RBuffTracker.Models;

/// <summary>
/// A named collection of tracked buffs (e.g. one per character build).
/// </summary>
public sealed class TrackerProfile : ObservableObject
{
    private string _name = "New Profile";

    public int Id { get; set; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>Optional class this profile is themed around.</summary>
    public DiabloClass? Class { get; set; }
}
