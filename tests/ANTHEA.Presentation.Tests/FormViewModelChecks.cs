using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Help;
using Anthea.Wiki;

namespace Anthea.Presentation.Tests;

/// <summary>
/// FormViewModel and FieldViewModel without WPF (proposal F5, sections 3.2 and 7.1): write rules, default choice, read-only,
/// validation, focus-loss commit, presentation of the numbers, layout and help. The expected writes and callbacks follow the
/// sequence of the historic InputForm (TextChanged, Set, Commit, focus); the FieldForm/InputForm parity check of the ui
/// stage compares the same sequences against InputForm itself.
/// </summary>
internal static class FormViewModelChecks
{
    internal static int Run()
    {
        var expect = new Expect();
        WriteRules(expect);
        DefaultChoice(expect);
        ReadOnly(expect);
        Validation(expect);
        FocusLossCommit(expect);
        Presentation(expect);
        SetSequence(expect);
        RowsAndGroups(expect);
        LayoutAndHelp(expect);
        return expect.Count;
    }

    static string Text(JsonObject values) => values.ToJsonString(J.Options);

    static void WriteRules(Expect expect)
    {
        var values = new JsonObject { ["raw"] = "1", ["num"] = "1", ["opt"] = 2, ["flag"] = false };
        var calls = new List<string>();
        var form = new FormViewModel(values, [new("raw", "Grezzo"), new("num", "Numero", Write: FieldWriteRule.NumberOrText),
            new("opt", "Opzionale", Write: FieldWriteRule.NumberOrTextRemoveEmpty), new("flag", "Opzione", Bool: true)], calls.Add);
        form["raw"].EditText = "2,50"; form["num"].EditText = "2,50"; form["opt"].EditText = ""; form["flag"].IsChecked = true;
        var expected = new JsonObject { ["raw"] = J.Node("2,50"), ["num"] = 2.5, ["flag"] = J.Node(true) };
        expect.That(Text(values) == Text(expected), "Ogni campo scrive con la sua regola: testo grezzo, numero o testo, chiave tolta se vuoto, booleano");
        expect.That(calls.SequenceEqual(["raw", "num", "opt", "flag"]), "Un richiamo per scrittura, con la chiave");
        form["opt"].EditText = "3";
        expect.That(Text(values) == Text(new JsonObject { ["raw"] = J.Node("2,50"), ["num"] = 2.5, ["flag"] = J.Node(true), ["opt"] = 3.0 }),
            "Il campo opzionale torna in coda quando si scrive di nuovo");
        expect.That(form.Get("raw") == "2,50" && form.Get("flag") == "" && form["num"].Text == "2,50", "Get: testo grezzo, vuoto per le caselle");
        expect.Throws<KeyNotFoundException>(() => form.Get("assente"), "Get di una chiave sconosciuta: errore, come InputForm");
        int before = calls.Count; form.Set("assente", "1"); form.Enable("assente", false); form.ShowField("assente", false); form.UpdateNote("assente");
        expect.That(calls.Count == before && !values.ContainsKey("assente"), "Set, Enable, ShowField su chiave sconosciuta: nessun effetto");
    }

    static void DefaultChoice(Expect expect)
    {
        string[] modes = ["Esclusa", "Inclusa"];
        var values = new JsonObject { ["altro"] = "x" };
        var calls = new List<string>();
        var form = new FormViewModel(values, [new("modo", "Modo", Choices: modes, Default: modes[0])], calls.Add);
        var field = form["modo"];
        expect.That(field.SelectedChoice == "Esclusa" && form.Get("modo") == "Esclusa" && !values.ContainsKey("modo") && calls.Count == 0,
            "Scelta predefinita mostrata e non scritta");
        field.SelectedChoice = "Esclusa";
        expect.That(!values.ContainsKey("modo") && calls.Count == 0, "Riscegliere la scelta mostrata non scrive (nessun cambio di selezione)");
        form.Set("modo", "Inclusa", display: true);
        expect.That(field.SelectedChoice == "Inclusa" && !values.ContainsKey("modo") && calls.Count == 0, "Set in sola presentazione non scrive");
        field.SelectedChoice = "Esclusa";
        expect.That(values["modo"]!.GetValue<string>() == "Esclusa" && calls.SequenceEqual(["modo"]), "La scelta dell'utente scrive, anche se è la predefinita");
        field.SelectedChoice = "Non prevista";
        expect.That(field.SelectedChoice == "Esclusa" && calls.Count == 1, "Valore fuori elenco ignorato, come la selezione della casella combinata");

        var saved = new JsonObject { ["modo"] = "Storica" };
        var old = new FormViewModel(saved, [new("modo", "Modo", Choices: ["", "Esclusa", "Inclusa", "Esclusa"], Default: "Esclusa")]);
        expect.That(old["modo"].Choices.SequenceEqual(["Esclusa", "Inclusa", "Storica"]) && old["modo"].SelectedChoice == "Storica",
            "Valore salvato fuori elenco aggiunto in coda; vuoti e ripetizioni tolti; la predefinita vale solo senza chiave");
        var empty = new FormViewModel(new JsonObject(), [new("modo", "Modo", Choices: modes)]);
        expect.That(empty["modo"].SelectedChoice is null && empty.Get("modo") == "", "Senza valore e senza predefinita nessuna selezione");
    }

    static void ReadOnly(Expect expect)
    {
        var values = new JsonObject { ["calc"] = "1.5" };
        var calls = new List<string>();
        var form = new FormViewModel(values, [new("calc", "Calcolato", "kN", ReadOnly: true)], calls.Add) { CommitOnFocusLoss = true };
        var field = form["calc"];
        field.IsFocused = true; field.EditText = "9"; field.IsFocused = false; form.Commit();
        expect.That(field.IsReadOnly && values["calc"]!.ToString() == "1.5" && calls.Count == 0 && !form.HasDrafts, "Sola lettura: la digitazione non scrive mai");
        form.Set("calc", "2.5");
        expect.That(values["calc"]!.ToString() == "2.5" && calls.SequenceEqual(["calc"]) && form.Get("calc") == "2.5", "Sola lettura: il modulo può impostare il valore calcolato");
        form.Set("calc", "3.5", display: true);
        expect.That(values["calc"]!.ToString() == "2.5" && form.Get("calc") == "3.5" && calls.Count == 1, "Sola lettura: valore solo mostrato");
        expect.That(field.IsInputReadOnly && field.CanChange && !form.IsReadOnly, "Campo calcolato in un form modificabile");
        ReadOnlyForm(expect);
    }

    /// <summary>A read-only form (a previous revision, D4 of F5.4): the user's typing, choices and ticks are refused; the module still sets values.</summary>
    static void ReadOnlyForm(Expect expect)
    {
        var values = new JsonObject { ["testo"] = "1", ["scelta"] = "A", ["spunta"] = false };
        var calls = new List<string>();
        var form = new FormViewModel(values, [new("testo", "Testo", Write: FieldWriteRule.NumberOrText), new("scelta", "Scelta", Choices: ["A", "B"]),
            new("spunta", "Spunta", Bool: true)], calls.Add) { CommitOnFocusLoss = true };
        var notices = new List<string?>();
        form["testo"].PropertyChanged += (_, e) => notices.Add(e.PropertyName);
        form.IsReadOnly = true;
        expect.That(form.Fields.All(f => f.IsInputReadOnly && !f.CanChange && !f.IsReadOnly)
            && notices.Contains(nameof(FieldViewModel.IsInputReadOnly)) && notices.Contains(nameof(FieldViewModel.CanChange)), "Form in sola lettura: stato dei campi notificato");
        string before = Text(values);
        notices.Clear();
        form["testo"].IsFocused = true; form["testo"].EditText = "2"; form["testo"].IsFocused = false; form.Commit();
        form["scelta"].SelectedChoice = "B"; form["spunta"].IsChecked = true;
        expect.That(Text(values) == before && calls.Count == 0 && form.Get("testo") == "1" && form.Get("scelta") == "A" && !form["spunta"].IsChecked && !form.HasDrafts,
            "Form in sola lettura: digitazione, scelta e spunta rifiutate");
        expect.That(notices.Contains(nameof(FieldViewModel.EditText)), "Form in sola lettura: l'editor torna al testo del campo");
        form.Set("testo", "3");
        expect.That(values["testo"]!.GetValue<double>() == 3 && calls.SequenceEqual(["testo"]), "Form in sola lettura: il modulo imposta ancora i valori");
        form.IsReadOnly = false;
        form["testo"].EditText = "4"; form["scelta"].SelectedChoice = "B"; form["spunta"].IsChecked = true;
        expect.That(form.Fields.All(f => !f.IsInputReadOnly && f.CanChange) && values["testo"]!.GetValue<double>() == 4 && values["scelta"]!.ToString() == "B"
            && values["spunta"]!.GetValue<bool>() && calls.SequenceEqual(["testo", "testo", "scelta", "spunta"]), "Ritorno alla modifica: il form accetta di nuovo");
    }

    sealed class NonNegative(params string[] keys) : IFieldValidator
    {
        public IEnumerable<FieldError> Validate(JsonObject values)
        {
            foreach (string key in keys)
                if (J.Number(values[key]) is double v && v < 0) yield return new(key, key + " non può essere negativo");
                else if (values[key] is not null && J.Number(values[key]) is null) yield return new(key, key + " non è un numero");
        }
    }

    static void Validation(Expect expect)
    {
        var values = new JsonObject { ["a"] = "1", ["b"] = "-1" };
        var form = new FormViewModel(values, [new("a", "A"), new("b", "B"), new("c", "C")], validator: new NonNegative("a", "b"));
        var changes = new List<string?>();
        form.ErrorsChanged += (_, e) => changes.Add(e.PropertyName);
        expect.That(form.HasErrors && form["b"].HasError && form["b"].Error == "b non può essere negativo" && !form["a"].HasError,
            "Errori iniziali dal validatore del modulo");
        form["a"].EditText = "x";
        expect.That(values["a"]!.ToString() == "x" && form["a"].Error == "a non è un numero" && changes.SequenceEqual(["a"]),
            "Testo non valido scritto come oggi, errore sul campo e notifica per chiave");
        expect.That(form.GetErrors("a").Cast<string>().SequenceEqual(["a non è un numero"]) && form.GetErrors(null).Cast<string>().Count() == 2
            && !form.GetErrors("c").Cast<string>().Any(), "INotifyDataErrorInfo: errori per chiave e di tutto il form");
        form["a"].EditText = "2"; form["b"].EditText = "3";
        expect.That(!form.HasErrors && !form["a"].HasError && form["b"].Error is null && changes.SequenceEqual(["a", "a", "b"]), "Errori tolti quando i valori tornano validi");
        values["b"] = "-5"; form.Validate();
        expect.That(form["b"].HasError && changes.Count == 4, "Validate dopo una modifica fuori dal form");
        var plain = new FormViewModel(new JsonObject { ["a"] = "-1" }, [new("a", "A")]);
        expect.That(!plain.HasErrors && !plain["a"].HasError, "Senza validatore nessuna regola nuova");
    }

    static void FocusLossCommit(Expect expect)
    {
        var values = new JsonObject { ["x"] = "1" };
        var calls = new List<string>();
        var form = new FormViewModel(values, [new("x", "X"), new("y", "Y")], calls.Add) { CommitOnFocusLoss = true };
        var x = form["x"];
        x.IsFocused = true; x.EditText = "12"; x.EditText = "123";
        expect.That(values["x"]!.ToString() == "1" && calls.Count == 0 && form.HasDrafts && form.Get("x") == "123", "Con il fuoco: bozza non scritta, Get restituisce il testo digitato");
        x.IsFocused = false;
        expect.That(values["x"]!.ToString() == "123" && calls.SequenceEqual(["x"]) && !form.HasDrafts, "Perdita del fuoco: una sola scrittura");
        x.IsFocused = true; x.EditText = "7"; form.Commit();
        expect.That(values["x"]!.ToString() == "7" && calls.Count == 2 && !form.HasDrafts, "Commit scrive le bozze");
        x.IsFocused = false;
        expect.That(calls.Count == 2, "Dopo Commit la perdita del fuoco non riscrive");
        x.IsFocused = true; x.EditText = "8"; form.Set("x", "9");
        expect.That(values["x"]!.ToString() == "9" && calls.Count == 3 && form.HasDrafts && x.Text == "9", "Set con il fuoco: scrive e la digitazione resta una bozza, come InputForm");
        x.IsFocused = false;
        expect.That(calls.Count == 4 && values["x"]!.ToString() == "9", "Alla perdita del fuoco la bozza riscrive lo stesso valore");
        form.CommitOnFocusLoss = false;
        form["y"].IsFocused = true; form["y"].EditText = "5";
        expect.That(values["y"]!.ToString() == "5" && calls.Count == 5, "Senza conferma alla perdita del fuoco ogni modifica scrive");
    }

    static void Presentation(Expect expect)
    {
        var values = new JsonObject { ["x"] = "123.456789", ["nome"] = "001", ["k"] = "0.123456" };
        var calls = new List<string>();
        var form = new FormViewModel(values, [new("x", "X"), new("nome", "Nome"), new("k", "k")], calls.Add);
        string Rounded(double n) => n.ToString("0.##", CultureInfo.CurrentCulture);
        expect.That(form["x"].EditText == "123.456789", "Numeri non formattati finché la vista non lo chiede");
        var notices = new List<string?>(); form["x"].PropertyChanged += (_, e) => notices.Add(e.PropertyName);
        form.FormatNumbers = true;
        expect.That(form["x"].EditText == Rounded(123.46) && form["x"].Text == "123.456789" && form["nome"].EditText == "001" && notices.Contains(nameof(FieldViewModel.EditText)),
            "Senza fuoco il testo è presentato con due decimali; il valore resta intero");
        form["x"].IsFocused = true;
        expect.That(form["x"].EditText == "123.456789", "Con il fuoco il testo grezzo");
        form["x"].IsFocused = false;
        expect.That(form["x"].EditText == Rounded(123.46) && calls.Count == 0 && values["x"]!.ToString() == "123.456789", "Cambio di fuoco: nessuna scrittura");
        form["x"].EditText = Rounded(123.46);
        expect.That(calls.Count == 0, "Assegnare il testo già mostrato non è una modifica");
        form["x"].EditText = "124.56789";
        expect.That(values["x"]!.ToString() == "124.56789" && form["x"].EditText == Rounded(124.57) && calls.SequenceEqual(["x"]), "Modifica: valore completo scritto, due decimali mostrati");
        form["k"].NumberFormatting = false;
        expect.That(form["k"].EditText == "0.123456" && form["k"].FormatsNumbers is false, "Presentazione disattivata per un solo campo");
    }

    static void SetSequence(Expect expect)
    {
        var values = new JsonObject { ["x"] = "123.456" };
        var calls = new List<string>();
        var form = new FormViewModel(values, [new("x", "X")], calls.Add) { FormatNumbers = true };
        form.Set("x", "123.456");
        expect.That(calls.Count == 1 && values["x"]!.ToString() == "123.456", "Set dello stesso valore con testo mostrato diverso: un richiamo, come il TextChanged di InputForm");
        form.FormatNumbers = false; calls.Clear();
        form.Set("x", "123.456");
        expect.That(calls.Count == 0, "Set dello stesso valore già mostrato: nessun richiamo");
        values["x"] = "5"; form.Set("x", "123.456");
        expect.That(calls.SequenceEqual(["x"]) && values["x"]!.ToString() == "123.456", "Set con documento cambiato fuori: riscrive il valore");
        form.Set("x", "7", display: true);
        expect.That(form.Get("x") == "7" && values["x"]!.ToString() == "123.456" && calls.Count == 1, "Set in sola presentazione");
    }

    static void RowsAndGroups(Expect expect)
    {
        var values = new JsonObject { ["cls_diagramma"] = "Stress block", ["a"] = "1" };
        var form = new FormViewModel(values, [new("cls_diagramma", "Diagramma", Choices: ["Parabola-rettangolo", "Stress block"], Note: FieldNotes.StressBlock),
            new("a", "A"), new("b", "B"), new("c", "C", Note: v => v.S("a") == "2" ? "a vale 2" : null)]);
        var note = form["cls_diagramma"];
        expect.That(note.Note is not null && note.IsNoteVisible, "Nota calcolata all'apertura");
        note.SelectedChoice = "Parabola-rettangolo";
        expect.That(note.Note is null && !note.IsNoteVisible, "Nota aggiornata dopo la scrittura del campo");
        form["a"].EditText = "2";
        expect.That(form["c"].Note is null, "La nota di un altro campo cambia solo con UpdateNote, come InputForm");
        form.UpdateNote("c");
        expect.That(form["c"].Note == "a vale 2" && form["c"].IsNoteVisible, "UpdateNote ricalcola la nota");
        form.ShowField("c", false);
        expect.That(!form["c"].IsVisible && !form["c"].IsNoteVisible, "Campo nascosto: nota nascosta");
        form.ShowField("c", true);
        expect.That(form["c"].IsNoteVisible, "Campo di nuovo visibile: nota visibile");
        form.Enable("b", false);
        expect.That(!form["b"].IsEnabled && !form["b"].IsDimmed, "Disattivato senza attenuazione");
        form.Enable("b", false, dim: true);
        expect.That(form["b"].IsDimmed, "Disattivato e attenuato");
        form.Enable("b", true);
        expect.That(form["b"].IsEnabled && form["b"].IsDimmed, "Riattivato senza dim: l'attenuazione resta, come l'opacità di InputForm");
        form.Enable("b", true, dim: true);
        expect.That(!form["b"].IsDimmed, "Riattivato con dim: piena opacità");
        var group = form.GroupFields("Gruppo", ["c", "assente", "a"], expanded: true);
        expect.That(group.Fields.Select(f => f.Key).SequenceEqual(["c", "a"]) && group.IsExpanded && form.TableFields.Select(f => f.Key).SequenceEqual(["cls_diagramma", "b"])
            && form.Groups.Single() == group && form.Fields.Count == 4, "Gruppo: campi nell'ordine dato, chiavi sconosciute saltate, tabella senza i campi raggruppati");
        expect.Throws<InvalidOperationException>(() => form.GroupFields("Altro", ["a"]), "Un campo in due gruppi è un errore");
    }

    static void LayoutAndHelp(Expect expect)
    {
        FieldSpec[] fields = [new("diametro", "Diametro palo D", "m"), new("tipo_palo", "Tipo", Choices: ["Trivellato"]), new("presenza_falda", "Falda", Bool: true),
            new("profilo_chs", "Profilo", "mm", Choices: ["A"]), new("metodo", "Metodo", Choices: ["X"]), new("motivo", "Motivo", Wide: true), new("n", "n")];
        var symbols = new FormViewModel(new JsonObject(), fields, symbolColumns: true, compact: true);
        expect.That(symbols["diametro"].Label == "Diametro palo" && symbols["diametro"].Symbol == "D" && symbols["n"].Symbol == "", "Colonne dei simboli: tabella di FieldSymbols");
        expect.That(symbols.Fields.Select(f => f.Layout).SequenceEqual([FieldLayout.Standard, FieldLayout.EditorSpansUnit, FieldLayout.Standard, FieldLayout.Standard,
            FieldLayout.EditorSpansUnit, FieldLayout.Standard, FieldLayout.Standard]), "Disposizione con le colonne dei simboli");
        var plain = new FormViewModel(new JsonObject(), fields);
        expect.That(plain["diametro"].Label == "Diametro palo D" && plain["diametro"].Symbol == "", "Senza colonne dei simboli etichette invariate");
        expect.That(plain.Fields.Select(f => f.Layout).SequenceEqual([FieldLayout.Standard, FieldLayout.EditorSpansUnit, FieldLayout.EditorOnly, FieldLayout.Standard,
            FieldLayout.EditorOnly, FieldLayout.LabelAbove, FieldLayout.Standard]), "Disposizione senza simboli");
        var compact = new FormViewModel(new JsonObject(), fields, compact: true, wideChoices: true);
        expect.That(compact["tipo_palo"].Layout == FieldLayout.LabelAbove && compact["profilo_chs"].Layout == FieldLayout.LabelAbove
            && new FormViewModel(new JsonObject(), fields, compact: true)["tipo_palo"].Layout == FieldLayout.Standard, "Scelte larghe e forma compatta");
        expect.That(plain.Fields.Select(f => f.Kind).SequenceEqual([FieldKind.Text, FieldKind.Choice, FieldKind.Bool, FieldKind.Choice, FieldKind.Choice, FieldKind.Text, FieldKind.Text]), "Tipo di editor");

        var help = new FormViewModel(new JsonObject(), [new("cover_mm", "Copriferro", "mm"), new("modello", "Analisi", Choices: ["Lineare"], HelpTopic: HelpTopics.StressAnalysis.Uri), new("ignoto", "Ignoto", "kN")]);
        expect.That(!help["cover_mm"].HasHelp && help["cover_mm"].HelpUri is null && help["modello"].HasHelp, "Senza modulo solo l'aiuto fisso");
        expect.That(help["modello"].EditorToolTip == HelpTopics.StressAnalysis.Title && help["modello"].HelpUri == HelpTopics.StressAnalysis.Uri
            && help["modello"].HelpToolTip == HelpTopics.StressAnalysis.Title + "\nApri questa sezione della Wiki", "Aiuto fisso per identificativo");
        var notices = new List<string?>(); help["cover_mm"].PropertyChanged += (_, e) => notices.Add(e.PropertyName);
        help.InheritedWikiModule = "str_palo";
        var topic = HelpTopics.ForField("cover_mm", "str_palo")!;
        expect.That(help["cover_mm"].HelpTopic == topic && help["cover_mm"].LabelToolTip == topic.Title + "\nApri questa sezione della Wiki"
            && help["cover_mm"].HelpUri == topic.Uri && notices.Contains(nameof(FieldViewModel.HasHelp)), "Modulo della vista: argomento, suggerimento e F1");
        expect.That(help["cover_mm"].EditorToolTip == (CalculationHelp.Field("cover_mm") ?? "Copriferro [mm]"), "Suggerimento dell'editor solo col modulo dato al form, come InputForm");
        expect.That(help["ignoto"].HelpUri == WikiCatalog.ForModule("str_palo")?.Id && help["ignoto"].LabelToolTip == (CalculationHelp.Field("ignoto") ?? "Ignoto"),
            "F1 senza argomento: guida del modulo");
        var given = new FormViewModel(new JsonObject(), [new("cover_mm", "Copriferro", "mm")], wikiModule: "str_palo") { InheritedWikiModule = "geo_palo_verticale" };
        expect.That(given.EffectiveWikiModule == "str_palo" && given["cover_mm"].EditorToolTip == topic.Title, "Il modulo dato al form prevale su quello della vista");

        // «?» and F1 through the navigator of the sheet (F5.4): the topic of the field, otherwise the guide of the module.
        expect.That(!help["cover_mm"].OpenHelp() && !help["cover_mm"].OpenContextHelp(), "Senza navigatore la vista apre la Wiki da sé");
        var wiki = new RecordingWiki();
        help.WikiNavigator = wiki;
        given.WikiNavigator = wiki;
        expect.That(help["cover_mm"].OpenHelp() && help["cover_mm"].OpenContextHelp() && !help["ignoto"].OpenHelp() && help["ignoto"].OpenContextHelp() && help["modello"].OpenHelp(),
            "Aiuto dei campi dal navigatore del form");
        expect.That(wiki.Shown.SequenceEqual([topic.Uri, topic.Uri, WikiCatalog.ForModule("str_palo")?.Id, HelpTopics.StressAnalysis.Uri]), "Sezioni aperte: argomento, F1, guida del modulo, aiuto fisso");
    }
}
