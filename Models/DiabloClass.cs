namespace D2RBuffTracker.Models;

/// <summary>
/// The playable Diablo II: Resurrected character classes, including the Warlock
/// introduced in the Reign of the Warlock expansion. A buff's class is
/// organizing metadata only — buffs can still be used cross-class via
/// items, but they always "belong" to their originating class.
/// </summary>
public enum DiabloClass
{
    Amazon,
    Assassin,
    Barbarian,
    Druid,
    Necromancer,
    Paladin,
    Sorceress,
    Warlock
}

public static class DiabloClassInfo
{
    /// <summary>Accent colour (hex) used to theme each class in the UI.</summary>
    public static string AccentHex(DiabloClass c) => c switch
    {
        DiabloClass.Amazon => "#66BB6A",
        DiabloClass.Assassin => "#AB47BC",
        DiabloClass.Barbarian => "#EF5350",
        DiabloClass.Druid => "#8D6E63",
        DiabloClass.Necromancer => "#78909C",
        DiabloClass.Paladin => "#FFCA28",
        DiabloClass.Sorceress => "#42A5F5",
        DiabloClass.Warlock => "#7C4DFF",
        _ => "#8A8D93"
    };

    public static IReadOnlyList<DiabloClass> All { get; } =
        Enum.GetValues<DiabloClass>().ToArray();
}
