using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using Materiali;

/// <summary>
/// Sezione 11 delle prove dell'adattatore: durabilità, copriferri, scheda Materiali e validazione di progetto (refactoring F2.9,
/// docs/refactoring/piano.md; progetto in supporto/artefatti/refactoring/f27-f28-progetto/F29-progetto.md del checkout principale).
/// Ogni messaggio comincia con il numero della prova (11a…11i), così una prova negativa dice quale prova fallisce.
/// 11g. Per riflessione: nessun campo o proprietà statica scrivibile e nessuna collezione statica modificabile nelle facciate, nel
///      nucleo legacy e negli array dei copriferri dei muri. Eccezione dichiarata: la tabella privata int[,] del prospetto 4.4N del
///      nucleo legacy (ex Durability.cs:33), che scade in F2.11.
/// </summary>
internal static class DurabilityChecks
{
    public static JsonObject Run(string root, Action<bool, string> check)
    {
        int staticMembers = StaticState(check);
        Console.WriteLine($"durabilità (sezione 11): 11g {staticMembers} membri statici controllati");
        return new JsonObject { ["membri_statici_controllati"] = staticMembers };
    }

    // ---------------------------------------------------------------- 11g. stato statico
    /// <summary>Eccezioni dichiarate della 11g: nome completo del campo → motivo.</summary>
    static readonly ImmutableDictionary<string, string> DeclaredStatic = ImmutableDictionary.CreateRange(new Dictionary<string, string>
    {
        ["Materiali.DurabilityLegacy+Durability.Covers"] = "tabella privata int[,] del prospetto 4.4N del nucleo legacy, scade in F2.11"
    });

    static int StaticState(Action<bool, string> check)
    {
        var types = new List<Type> { typeof(Materiali.Durability), typeof(NtcCover), typeof(MinimumConcrete), typeof(AtecapMix), typeof(MaterialCover), typeof(DurabilityLegacy) };
        types.AddRange(typeof(DurabilityLegacy).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Where(t => !t.IsDefined(typeof(CompilerGeneratedAttribute))));
        int checkedMembers = 0; var declaredSeen = new HashSet<string>();
        foreach (var type in types)
        {
            foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Where(f => !f.Name.Contains('<')))
            {
                string id = type.FullName + "." + field.Name;
                checkedMembers++;
                if (DeclaredStatic.ContainsKey(id)) { declaredSeen.Add(id); continue; }
                check(field.IsLiteral || field.IsInitOnly, "11g: campo statico scrivibile " + id);
                check(!Mutable(field.FieldType, field.IsLiteral ? null : field.GetValue(null)), $"11g: collezione statica modificabile {id} ({field.FieldType})");
            }
            foreach (var property in type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                string id = type.FullName + "." + property.Name;
                checkedMembers++;
                check(property.SetMethod is null, "11g: proprietà statica scrivibile " + id);
                check(!Mutable(property.PropertyType, property.GetValue(null)), $"11g: proprietà statica con collezione modificabile {id} ({property.PropertyType})");
            }
        }
        // Array dei copriferri dei muri (RetainingWall.Materials.cs), letti da CompleteMaterialInput, CoverMaterialState e ProjectWallFields.
        foreach (string name in new[] { "CoverChoiceDefaults", "CoverFlags", "CoverSheetPaths" })
        {
            var field = typeof(RetainingWall).GetField(name, BindingFlags.Static | BindingFlags.Public);
            check(field is not null, "11g: campo dei copriferri dei muri non trovato: " + name);
            check(field!.IsInitOnly && !Mutable(field.FieldType, field.GetValue(null)), $"11g: RetainingWall.{name} scrivibile o modificabile ({field.FieldType})");
            checkedMembers++;
        }
        check(declaredSeen.SetEquals(DeclaredStatic.Keys), "11g: eccezioni dichiarate non trovate: " + string.Join(", ", DeclaredStatic.Keys.Except(declaredSeen)));
        return checkedMembers;
    }

    /// <summary>Collezione modificabile: array, collezioni generiche o non generiche che non siano immutabili (System.Collections.Immutable,
    /// System.Collections.Frozen), anche quando il tipo dichiarato è un'interfaccia di sola lettura costruita su un array.</summary>
    static bool Mutable(Type declared, object? value)
    {
        static bool Immutable(Type t) => t.Namespace is "System.Collections.Immutable" or "System.Collections.Frozen";
        static bool Collection(Type t) => t != typeof(string) && typeof(IEnumerable).IsAssignableFrom(t);
        if (declared.IsArray) return true;
        if (Collection(declared) && !Immutable(declared) && !(declared.IsInterface && value is not null && Immutable(value.GetType()))) return true;
        return value is not null && value.GetType().IsArray;
    }
}
