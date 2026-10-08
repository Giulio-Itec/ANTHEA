using System.Collections;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

/// <summary>
/// Prova 5l del refactoring F2.7 (stato statico, progetto F2.7 §6.2 e §9): per riflessione sul tipo e sui suoi tipi annidati, esclusi
/// quelli generati dal compilatore (cache dei delegati, chiusure), nessun campo o proprietà statica scrivibile, nessuna collezione
/// statica modificabile e nessun oggetto statico con membri pubblici scrivibili (per esempio un AsyncLocal). I const sono ammessi
/// (letterali); un vettore vuoto non è modificabile; una proprietà statica di sola lettura conta solo se due letture danno la stessa
/// istanza (stato condiviso), non se crea un oggetto nuovo a ogni lettura.
/// </summary>
static class StaticState
{
    const BindingFlags DeclaredStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>Membri statici esaminati e violazioni ("Tipo.Membro", motivo) del tipo e dei tipi annidati.</summary>
    public static (int Members, List<(string Member, string Reason)> Violations) Inspect(Type type)
    {
        var violations = new List<(string, string)>();
        int members = Collect(type, violations);
        return (members, violations);
    }

    static int Collect(Type type, List<(string, string)> violations)
    {
        int members = 0;
        bool open = type.ContainsGenericParameters; // valori non leggibili: si controlla solo la scrivibilità
        foreach (var field in type.GetFields(DeclaredStatic))
        {
            if (field.IsLiteral) continue;
            members++;
            string member = Name(type) + "." + field.Name;
            if (!field.IsInitOnly) violations.Add((member, "campo statico scrivibile"));
            else if (!open && Mutable(field.GetValue(null)) is string reason) violations.Add((member, reason));
        }
        foreach (var property in type.GetProperties(DeclaredStatic))
        {
            members++;
            string member = Name(type) + "." + property.Name;
            if (property.SetMethod is not null) { violations.Add((member, "proprietà statica scrivibile")); continue; }
            if (open || property.GetMethod is null || property.GetIndexParameters().Length > 0) continue;
            object? first = property.GetValue(null);
            if (first is not null && !first.GetType().IsValueType && ReferenceEquals(first, property.GetValue(null)) && Mutable(first) is string reason)
                violations.Add((member, reason));
        }
        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            if (!nested.IsDefined(typeof(CompilerGeneratedAttribute), false)) members += Collect(nested, violations);
        return members;
    }

    static string Name(Type type) => type.DeclaringType is null ? type.Name : Name(type.DeclaringType) + "." + type.Name;

    /// <summary>Motivo per cui il valore di un membro statico è uno stato modificabile, oppure null.</summary>
    static string? Mutable(object? value)
    {
        switch (value)
        {
            case null or string: return null;
            case Array array: return array.Length > 0 ? "collezione statica modificabile" : null;
            case IList list: return list.IsReadOnly ? null : "collezione statica modificabile";
            case IDictionary dictionary: return dictionary.IsReadOnly ? null : "collezione statica modificabile";
        }
        var type = value.GetType();
        foreach (var contract in type.GetInterfaces())
            if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICollection<>))
                return contract.GetProperty(nameof(ICollection<int>.IsReadOnly))!.GetValue(value) is true ? null : "collezione statica modificabile";
        if (type.IsValueType) return null;
        bool writable = type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Any(p => p.GetIndexParameters().Length == 0 && p.SetMethod is { IsPublic: true } set
                && !set.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
            || type.GetFields(BindingFlags.Instance | BindingFlags.Public).Any(f => !f.IsInitOnly && !f.IsLiteral);
        return writable ? "oggetto statico con membri pubblici scrivibili" : null;
    }
}

/// <summary>Campione della prova 5l: ogni forma di stato statico che la prova deve riconoscere e quelle ammesse.</summary>
static class StaticStateSample
{
    public const int Constant = 1;                                                     // ammesso: letterale
    public static int Writable = 1;                                                    // campo scrivibile
    public static string Property { get; set; } = "";                                  // proprietà scrivibile e campo d'appoggio scrivibile
    public static readonly int[] Values = [1];                                         // vettore modificabile
    public static readonly int[] Empty = [];                                           // ammesso: vettore vuoto
    public static readonly Dictionary<string, string> Map = new();                     // dizionario modificabile
    public static readonly FrozenDictionary<string, string> Frozen = new Dictionary<string, string> { ["a"] = "b" }.ToFrozenDictionary();
    public static readonly ImmutableArray<int> Immutable = [1];                        // ammesso
    public static readonly IReadOnlyList<string> Wrapped = Array.AsReadOnly(new[] { "a" }); // ammesso: ReadOnlyCollection
    public static IReadOnlyList<int> Shared => Values;                                  // stessa istanza modificabile a ogni lettura
    public static IReadOnlyList<int> Fresh => new[] { 1 };                             // ammesso: istanza nuova a ogni lettura
    public static readonly Regex Pattern = new("a", RegexOptions.CultureInvariant);   // ammesso: immutabile
    public static readonly AsyncLocal<string?> Local = new();                          // oggetto con membro pubblico scrivibile (Value)

    /// <summary>Violazioni attese del campione (membro).</summary>
    public static readonly ImmutableArray<string> Expected =
    [
        "StaticStateSample.Writable", "StaticStateSample.<Property>k__BackingField", "StaticStateSample.Property", "StaticStateSample.Values",
        "StaticStateSample.Map", "StaticStateSample.Shared", "StaticStateSample.Local"
    ];
}
