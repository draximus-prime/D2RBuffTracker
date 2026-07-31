namespace D2RBuffTracker.Models;

/// <summary>
/// A predefined buff that belongs to a specific class. Used to populate the
/// class-filtered gallery in the add-buff flow. Selecting one pre-fills the
/// icon and a sensible default duration.
/// </summary>
public sealed class BuffCatalogEntry
{
    public string Name { get; }
    public DiabloClass Class { get; }
    public double DefaultDuration { get; }

    /// <summary>Relative icon path, e.g. "Buffs/Barbarian/Battle Orders.png".</summary>
    public string IconRelativePath { get; }

    public BuffCatalogEntry(string name, DiabloClass cls, double defaultDuration)
    {
        Name = name;
        Class = cls;
        DefaultDuration = defaultDuration;
        IconRelativePath = $"Buffs/{cls}/{name}.png";
    }
}

/// <summary>
/// The built-in catalog of class buffs. Buffs are grouped by their owning class
/// even though many can be used cross-class through items.
/// </summary>
public static class BuffCatalog
{
    public static IReadOnlyList<BuffCatalogEntry> All { get; } = Build();

    public static IEnumerable<BuffCatalogEntry> ForClass(DiabloClass cls)
        => All.Where(b => b.Class == cls);

    private static BuffCatalogEntry[] Build()
    {
        return new[]
        {
            // Amazon
            new BuffCatalogEntry("Inner Sight", DiabloClass.Amazon, 8),
            new BuffCatalogEntry("Slow Missiles", DiabloClass.Amazon, 20),
            new BuffCatalogEntry("Decoy", DiabloClass.Amazon, 20),
            new BuffCatalogEntry("Valkyrie", DiabloClass.Amazon, 30),

            // Assassin
            new BuffCatalogEntry("Burst of Speed", DiabloClass.Assassin, 120),
            new BuffCatalogEntry("Fade", DiabloClass.Assassin, 120),
            new BuffCatalogEntry("Cloak of Shadows", DiabloClass.Assassin, 12),
            new BuffCatalogEntry("Blade Shield", DiabloClass.Assassin, 33),
            new BuffCatalogEntry("Blade Sentinel", DiabloClass.Assassin, 12),
            new BuffCatalogEntry("Venom", DiabloClass.Assassin, 12),
            new BuffCatalogEntry("Shadow Warrior", DiabloClass.Assassin, 120),
            new BuffCatalogEntry("Shadow Master", DiabloClass.Assassin, 120),

            // Barbarian
            new BuffCatalogEntry("Battle Cry", DiabloClass.Barbarian, 8),
            new BuffCatalogEntry("Battle Orders", DiabloClass.Barbarian, 30),
            new BuffCatalogEntry("Battle Command", DiabloClass.Barbarian, 30),
            new BuffCatalogEntry("Shout", DiabloClass.Barbarian, 30),
            new BuffCatalogEntry("War Cry", DiabloClass.Barbarian, 4),
            new BuffCatalogEntry("Frenzy", DiabloClass.Barbarian, 6),
            new BuffCatalogEntry("Grim Ward", DiabloClass.Barbarian, 20),
            new BuffCatalogEntry("Taunt", DiabloClass.Barbarian, 6),

            // Druid
            new BuffCatalogEntry("Werewolf", DiabloClass.Druid, 40),
            new BuffCatalogEntry("Werebear", DiabloClass.Druid, 40),
            new BuffCatalogEntry("Hurricane", DiabloClass.Druid, 25),
            new BuffCatalogEntry("Armageddon", DiabloClass.Druid, 30),
            new BuffCatalogEntry("Cyclone Armor", DiabloClass.Druid, 60),
            new BuffCatalogEntry("Oak Sage", DiabloClass.Druid, 30),
            new BuffCatalogEntry("Heart of Wolverine", DiabloClass.Druid, 30),
            new BuffCatalogEntry("Spirit of Barbs", DiabloClass.Druid, 30),

            // Necromancer
            new BuffCatalogEntry("Bone Armor", DiabloClass.Necromancer, 20),
            new BuffCatalogEntry("Bone Wall", DiabloClass.Necromancer, 24),
            new BuffCatalogEntry("Bone Prison", DiabloClass.Necromancer, 24),
            new BuffCatalogEntry("Clay Golem", DiabloClass.Necromancer, 60),
            new BuffCatalogEntry("Blood Golem", DiabloClass.Necromancer, 60),
            new BuffCatalogEntry("Iron Golem", DiabloClass.Necromancer, 60),
            new BuffCatalogEntry("Fire Golem", DiabloClass.Necromancer, 60),
            new BuffCatalogEntry("Revive", DiabloClass.Necromancer, 180),

            // Paladin
            new BuffCatalogEntry("Holy Shield", DiabloClass.Paladin, 40),
            new BuffCatalogEntry("Conversion", DiabloClass.Paladin, 8),
            new BuffCatalogEntry("Vigor", DiabloClass.Paladin, 10),

            // Sorceress
            new BuffCatalogEntry("Frozen Armor", DiabloClass.Sorceress, 120),
            new BuffCatalogEntry("Shiver Armor", DiabloClass.Sorceress, 144),
            new BuffCatalogEntry("Chilling Armor", DiabloClass.Sorceress, 168),
            new BuffCatalogEntry("Energy Shield", DiabloClass.Sorceress, 120),
            new BuffCatalogEntry("Enchant", DiabloClass.Sorceress, 150),
            new BuffCatalogEntry("Hydra", DiabloClass.Sorceress, 12),
            new BuffCatalogEntry("Thunder Storm", DiabloClass.Sorceress, 60),

            // Warlock (Reign of the Warlock)
            new BuffCatalogEntry("Hex Bane", DiabloClass.Warlock, 30),
            new BuffCatalogEntry("Hex Purge", DiabloClass.Warlock, 30),
            new BuffCatalogEntry("Hex Siphon", DiabloClass.Warlock, 30),
            new BuffCatalogEntry("Sigil Rancor", DiabloClass.Warlock, 12),
            new BuffCatalogEntry("Sigil Lethargy", DiabloClass.Warlock, 12),
            new BuffCatalogEntry("Sigil Death", DiabloClass.Warlock, 12),
            new BuffCatalogEntry("Bind Demon", DiabloClass.Warlock, 20),
            new BuffCatalogEntry("Consume", DiabloClass.Warlock, 30),
        };
    }
}
