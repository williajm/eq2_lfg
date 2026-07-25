namespace Eq2Lfg.Core.Models;

/// <summary>
/// Static knowledge of EQ2 adventure classes: canonical names, chat abbreviations,
/// and the role each class fills in a group.
/// </summary>
public static class ClassCatalog
{
    private sealed record ClassInfo(string Name, Role Role, string[] Aliases);

    private static readonly ClassInfo[] Classes =
    [
        new("Guardian", Role.Tank, ["guard", "guardian"]),
        new("Berserker", Role.Tank, ["zerker", "zerk", "berserker", "berz"]),
        new("Monk", Role.Tank, ["monk"]),
        new("Bruiser", Role.Tank, ["bruiser", "bruis"]),
        new("Shadowknight", Role.Tank, ["sk", "shadowknight", "shadow knight"]),
        new("Paladin", Role.Tank, ["pally", "pali", "paladin", "pal"]),
        new("Templar", Role.Healer, ["templar", "temp"]),
        new("Inquisitor", Role.Healer, ["inq", "inqui", "inquis", "inquisitor"]),
        new("Warden", Role.Healer, ["warden", "ward"]),
        new("Fury", Role.Healer, ["fury"]),
        new("Mystic", Role.Healer, ["mystic", "myst"]),
        new("Defiler", Role.Healer, ["defiler", "def"]),
        new("Channeler", Role.Healer, ["channeler", "chan"]),
        new("Wizard", Role.Dps, ["wiz", "wizard", "wizzy"]),
        new("Warlock", Role.Dps, ["lock", "warlock"]),
        new("Conjuror", Role.Dps, ["conj", "conji", "conjy", "conjuror", "conjurer"]),
        new("Necromancer", Role.Dps, ["nec", "necro", "necromancer"]),
        new("Swashbuckler", Role.Dps, ["swash", "swashy", "swashbuckler"]),
        new("Brigand", Role.Dps, ["brig", "brigand"]),
        new("Ranger", Role.Dps, ["ranger", "rng"]),
        new("Assassin", Role.Dps, ["sin", "assassin", "assa", "asn"]),
        new("Beastlord", Role.Dps, ["beastlord", "bl"]),
        new("Illusionist", Role.Support, ["ilu", "ill", "illu", "illy", "illus", "illusionist"]),
        new("Coercer", Role.Support, ["coercer", "coerc"]),
        new("Dirge", Role.Support, ["dirge"]),
        new("Troubador", Role.Support, ["troub", "troubador", "troubadour", "trub"]),
    ];

    // EQ2's class tree pairs subclasses under archetype names players still use in
    // chat ("sorc for wizard or warlock, cleric for templar or inquisitor").
    private static readonly (string[] Aliases, string[] Members)[] Groups =
    [
        (["warrior"], ["Guardian", "Berserker"]),
        (["brawler"], ["Monk", "Bruiser"]),
        (["crusader"], ["Paladin", "Shadowknight"]),
        (["cleric"], ["Templar", "Inquisitor"]),
        (["druid"], ["Fury", "Warden"]),
        (["shaman", "shammy"], ["Mystic", "Defiler"]),
        (["sorcerer", "sorceror", "sorc"], ["Wizard", "Warlock"]),
        (["enchanter", "chanter", "ench"], ["Illusionist", "Coercer"]),
        (["summoner"], ["Conjuror", "Necromancer"]),
        (["bard"], ["Dirge", "Troubador"]),
        (["predator", "pred"], ["Assassin", "Ranger"]),
        (["rogue"], ["Swashbuckler", "Brigand"]),
        // Top-tier archetypes that don't collapse to a single role: mages span
        // Dps and Support (enchanters), scouts span Dps and Support (bards).
        (["mage", "caster"],
            ["Wizard", "Warlock", "Conjuror", "Necromancer", "Illusionist", "Coercer"]),
        (["scout"],
            ["Swashbuckler", "Brigand", "Assassin", "Ranger", "Dirge", "Troubador"]),
    ];

    private static readonly Dictionary<string, string> AliasToClass = BuildAliasMap();
    private static readonly Dictionary<string, string[]> AliasToGroup = BuildGroupMap();
    private static readonly Dictionary<string, Role> ClassToRole =
        Classes.ToDictionary(c => c.Name, c => c.Role, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, string> BuildAliasMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in Classes)
        {
            map[c.Name] = c.Name;
            foreach (var alias in c.Aliases)
            {
                map[alias] = c.Name;
            }
        }

        return map;
    }

    private static Dictionary<string, string[]> BuildGroupMap()
    {
        var map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var (aliases, members) in Groups)
        {
            foreach (var alias in aliases)
            {
                map[alias] = members;
            }
        }

        return map;
    }

    public static IReadOnlyList<string> AllClassNames { get; } = Classes.Select(c => c.Name).ToList();

    /// <summary>Resolve a chat token ("wiz", "sk", "Warden") to a canonical class name, or null.</summary>
    public static string? ResolveClass(string token) =>
        AliasToClass.TryGetValue(token.Trim(), out var name) ? name : null;

    /// <summary>Resolve an archetype token ("sorc", "bard", "cleric") to its member classes, or null.</summary>
    public static IReadOnlyList<string>? ResolveClassGroup(string token) =>
        AliasToGroup.TryGetValue(token.Trim(), out var members) ? members : null;

    /// <summary>The group role a class fills. Throws for unknown class names.</summary>
    public static Role RoleOf(string className) => ClassToRole[className];

    public static bool TryRoleOf(string className, out Role role) =>
        ClassToRole.TryGetValue(className, out role);
}
