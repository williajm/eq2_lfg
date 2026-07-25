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
        new("Illusionist", Role.Support, ["ilu", "illu", "illy", "illus", "illusionist"]),
        new("Coercer", Role.Support, ["coercer", "coerc"]),
        new("Dirge", Role.Support, ["dirge"]),
        new("Troubador", Role.Support, ["troub", "troubador", "troubadour", "trub"]),
    ];

    private static readonly Dictionary<string, string> AliasToClass;
    private static readonly Dictionary<string, Role> ClassToRole;

    static ClassCatalog()
    {
        AliasToClass = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ClassToRole = new Dictionary<string, Role>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in Classes)
        {
            ClassToRole[c.Name] = c.Role;
            AliasToClass[c.Name] = c.Name;
            foreach (var alias in c.Aliases)
            {
                AliasToClass[alias] = c.Name;
            }
        }
    }

    public static IReadOnlyList<string> AllClassNames { get; } = Classes.Select(c => c.Name).ToList();

    /// <summary>Resolve a chat token ("wiz", "sk", "Warden") to a canonical class name, or null.</summary>
    public static string? ResolveClass(string token) =>
        AliasToClass.TryGetValue(token.Trim(), out var name) ? name : null;

    /// <summary>The group role a class fills. Throws for unknown class names.</summary>
    public static Role RoleOf(string className) => ClassToRole[className];

    public static bool TryRoleOf(string className, out Role role) =>
        ClassToRole.TryGetValue(className, out role);
}
