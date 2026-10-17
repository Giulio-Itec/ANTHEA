using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace Anthea.Application.Concrete;

/// <summary>
/// Derivations of the tables of the actions of the RC section sheet (str_palo), moved without changes from the historic view
/// (ConcreteWorkspace.cs, ConcreteExchange.cs and ConcreteShear.cs, preparation of F5.10): the values of a new row and its name, the new
/// rows of the shear, the columns of the paste and of the copy, and the plan of a paste (validated before any row changes). The historic
/// view calls them; the ViewModel of the sub-step «azioni» will call them too. The modal editor of the actions is already a ViewModel
/// (SectionActionsViewModel).
/// </summary>
public static class ConcreteActionPresentation
{
    /// <summary>The values of a new row of the N–Mx–My actions, with the outputs not yet calculated; a new identifier when none is given.</summary>
    public static JsonObject NewRow(string name, string n, string mx, string my, string? id = null, bool visible = true) =>
        J.Obj(("id", string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N") : id), ("nome", name), ("N", n), ("Mx", mx), ("My", my), ("visible", visible), ("eta", "—"), ("esito", "Da calcolare"));

    /// <summary>The fields of a row of the unified actions copied into the row of a table.</summary>
    public static ImmutableArray<string> UnifiedFields { get; } = ["elemento", "stato", "Vx", "Vy", "T"];

    /// <summary>The first free name «Combo n» of a family.</summary>
    public static string NextName(IEnumerable<string> names)
    {
        var used = names.ToHashSet(StringComparer.Ordinal);
        int n = 1; while (used.Contains("Combo " + n)) n++;
        return "Combo " + n;
    }

    /// <summary>A row of the shear (historic format) completed with T, Mx and My at zero when they are missing.</summary>
    public static JsonObject ShearRow(JsonObject values)
    {
        foreach (string key in new[] { "T", "Mx", "My" }) if (!values.ContainsKey(key)) values[key] = "0";
        return values;
    }

    /// <summary>The values of a new row of the shear (historic format, before <see cref="ShearRow"/>), with a new identifier.</summary>
    public static JsonObject NewShearRow(string name) =>
        J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("N", "0"), ("Vx", "0"), ("Vy", "0"));

    /// <summary>The name of the row added by «+ Combinazione» of the shear after <paramref name="count"/> rows.</summary>
    public static string ShearName(int count) => "Taglio " + (count + 1);

    /// <summary>The columns of the paste and of the copy: name and N–Mx–My, or name, N–Mx–My and Vx–Vy–T for the shear.</summary>
    public static string[] Columns(string family) => family == "Taglio" ? ["nome", "N", "Mx", "My", "Vx", "Vy", "T"] : ["nome", "N", "Mx", "My"];

    /// <summary>The text of a copy: one line per row, the columns separated by tabs.</summary>
    public static string CopyText(IEnumerable<JsonObject> rows, string family)
    {
        var keys = Columns(family);
        return string.Join(Environment.NewLine, rows.Select(row => string.Join('\t', keys.Select(k => row.S(k)))));
    }

    public static string CopyStatus(int rows) => $"Copiate {rows} righe: nome e sollecitazioni. Ctrl+clic / Maiusc+clic per selezionare più righe.";

    public static string PasteStatus(int rows) => $"{rows} righe incollate · aggiornamento automatico in attesa…";

    /// <summary>A paste ready to apply: the columns written, from which one, and the cells of every row.</summary>
    public sealed record PastePlan(IReadOnlyList<string> Keys, int StartColumn, int Width, IReadOnlyList<string[]> Rows);

    /// <summary>
    /// Reads a paste of <paramref name="family"/> from the text of the clipboard and validates it all, before any row changes: rows separated
    /// by new lines, cells by tabs; 4 columns name–N–Mx–My (7 for the shear, 6 without the name; the historic 3–5 columns of the shear are
    /// name–N–Vx–Vy[–T]); fewer columns start at the cell <paramref name="currentColumn"/>; a first row of headers is skipped. Refuses, with
    /// the message of the sheet, an empty text, more than 10 000 rows, a wrong number of columns, a missing name and a value that is not a
    /// finite number.
    /// </summary>
    public static PastePlan Plan(string family, string text, string currentColumn)
    {
        string[] keys = Columns(family);
        var matrix = text.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Split('\t')).ToList();
        if (matrix.Count == 0) throw new ArgumentException("Gli appunti non contengono righe.");
        if (matrix.Count > 10000) throw new ArgumentException("Massimo 10.000 righe per incolla.");
        int width = matrix[0].Length;
        // Preserve the existing clipboard format; moments are included only in the new full-width format.
        if (family == "Taglio" && width is >= 3 and <= 5) keys = ["nome", "N", "Vx", "Vy", "T"];
        if (width < 1 || width > keys.Length || matrix.Any(row => row.Length != width)) throw new ArgumentException("Numero di colonne non valido per la famiglia selezionata.");
        if (width < 3 && !keys.Contains(currentColumn)) throw new ArgumentException("Selezionare prima una cella di input (nome o sollecitazione). Per aggiungere righe complete usare 3 o 4 colonne.");
        int startColumn = width == 6 && family == "Taglio" ? 1 : width >= 4 ? 0 : width == 3 ? 1 : Math.Max(0, Array.IndexOf(keys, currentColumn));
        if (startColumn + width > keys.Length) throw new ArgumentException("La selezione supera le colonne di input. Selezionare la cella iniziale corretta.");
        bool Header(string cell, int i) => cell.Trim().Equals(keys[i], StringComparison.OrdinalIgnoreCase) || cell.Trim().StartsWith(keys[i] + " [", StringComparison.OrdinalIgnoreCase) || i == 0 && cell.Trim() == "Combinazione";
        if (matrix[0].Select((cell, i) => Header(cell, startColumn + i)).All(v => v)) matrix.RemoveAt(0);
        if (matrix.Count == 0) throw new ArgumentException("Sono presenti solo le intestazioni.");
        // Validate the entire paste before mutating a single existing row.
        for (int r = 0; r < matrix.Count; r++)
            for (int c = 0; c < width; c++)
            {
                if (startColumn + c == 0 && string.IsNullOrWhiteSpace(matrix[r][c])) throw new ArgumentException($"Riga {r + 1}: nome mancante.");
                if (startColumn + c > 0) ConcreteEditor.Number(matrix[r][c], $"Riga {r + 1}, {keys[startColumn + c]}");
            }
        return new(keys, startColumn, width, matrix);
    }

    /// <summary>The name of a row added by a paste beyond the rows shown.</summary>
    public static string PastedName(int count) => "Combo " + (count + 1);
}
