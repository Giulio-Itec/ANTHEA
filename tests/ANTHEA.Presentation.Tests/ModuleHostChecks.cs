using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Wiki;

namespace Anthea.Presentation.Tests;

/// <summary>
/// ModuleHost contract (F5.2, proposal 5.1) with the test-only probe module (tests/Shared/ProbeSheetModule.cs): registry over
/// ModuleDirectory, CreateSheet, Commit, Reload, publication of the current revision only, cancellation and closing during a
/// calculation, read-only, host commands. One "UI" thread (SingleThreadContext), engine held by barriers.
/// </summary>
internal static class ModuleHostChecks
{
    internal static int Run() => SingleThreadContext.Run(RunAsync);

    private static readonly ModuleInfo ProbeInfo = new("prova", "Prove", "Modulo di prova", "Contratto del ModuleHost", "Prova", "Prova", "Solo prove");

    private static async Task<int> RunAsync(SingleThreadContext ui)
    {
        var expect = new Expect();
        Registry(expect);

        // CreateSheet: the context gives the owned copy, the module the identity; the calculation service must be of the module.
        var service = new BarrierModuleService();
        var host = new ScriptedHost();
        var messages = new ScriptedMessages();
        var limiter = new CalculationLimiter(1);
        var module = new ProbeSheetModule(ProbeInfo);
        SheetContext Context(JsonObject data, bool readOnly = false) => new(data, readOnly, host, Scripted.Services(messages), service, limiter);
        var data = new JsonObject { ["valore"] = 1.0, ["etichetta"] = "A" };
        using var sheet = (ProbeSheetViewModel)module.CreateSheet(Context(data));
        expect.That(module.Created.Single() == sheet && sheet.ModuleId == "prova" && ReferenceEquals(sheet.Data, data), "CreateSheet: modulo o copia di lavoro");
        expect.That(sheet.Revision == 0 && !sheet.IsBusy && !sheet.HasResults && sheet.Result is null && !sheet.IsReadOnly && sheet.CanEdit
            && sheet.CalculationState == CalculationState.Idle, "CreateSheet: stato iniziale");
        expect.Throws<ArgumentException>(() => new ProbeSheetModule(ProbeInfo with { Id = "altro" }).CreateSheet(Context(new JsonObject())),
            "Servizio di calcolo di un altro modulo accettato");
        expect.Throws<ArgumentNullException>(() => _ = new SheetContext(null!, false, host, Scripted.Services(), service, limiter), "Contesto senza dati");
        expect.Throws<ArgumentNullException>(() => _ = new SheetContext(new JsonObject(), false, null!, Scripted.Services(), service, limiter), "Contesto senza ospite");

        // Edit: written at once, new revision, Modified once, results cleared (SheetEditor.Changed).
        int modified = 0;
        sheet.Modified += (_, _) => modified++;
        var changed = new List<string?>();
        sheet.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        expect.That(await Expect.Within(sheet.CalculateAsync()) && sheet.HasResults && Echo(sheet) == 1, "Calcolo iniziale");
        changed.Clear();
        sheet.Label = "B";
        expect.That(data["etichetta"]!.ToString() == "B" && sheet.Revision == 1 && modified == 1, "Modifica non scritta o Modified mancante");
        expect.That(!sheet.HasResults && sheet.Result is null && sheet.CalculationState == CalculationState.Idle, "Risultati non invalidati dalla modifica");
        expect.That(changed.Contains(nameof(sheet.Revision)) && changed.Contains(nameof(sheet.Result)) && changed.Contains(nameof(sheet.HasResults)), "Notifiche della modifica");

        // Commit: the draft reaches the data only at Commit, once.
        sheet.Note = "bozza";
        expect.That(data["nota"] is null && modified == 1, "Bozza scritta prima della conferma");
        sheet.Commit();
        expect.That(data["nota"]!.ToString() == "bozza" && sheet.Note is null && sheet.Commits == 1 && modified == 2 && sheet.Revision == 2, "Commit della bozza");
        sheet.Commit();
        expect.That(sheet.Commits == 1 && modified == 2 && sheet.Revision == 2, "Commit senza bozze ha modificato il foglio");

        // Reload: same working copy, contents replaced by a copy, drafts discarded, results invalidated, no Modified.
        await Expect.Within(sheet.CalculateAsync());
        sheet.Note = "da scartare";
        var replacement = new JsonObject { ["valore"] = 7.0, ["etichetta"] = "R", ["annidato"] = new JsonObject { ["x"] = 1 } };
        sheet.Reload(replacement);
        expect.That(ReferenceEquals(sheet.Data, data) && JsonNode.DeepEquals(data, replacement) && !ReferenceEquals(data["annidato"], replacement["annidato"]),
            "Reload: copia di lavoro sostituita o condivisa con l'origine");
        expect.That(sheet.Note is null && !sheet.HasResults && sheet.Revision == 3 && modified == 2 && sheet.Label == "R", "Reload: bozze, risultati o revisione");
        replacement["etichetta"] = "cambiata dopo";
        expect.That(sheet.Label == "R", "Reload: il foglio legge ancora l'oggetto sostituito");
        sheet.Reload(sheet.Data);
        expect.That(JsonNode.DeepEquals(sheet.Data, new JsonObject { ["valore"] = 7.0, ["etichetta"] = "R", ["annidato"] = new JsonObject { ["x"] = 1 } }), "Reload dei propri dati");

        // Only the current revision is published: a calculation started before an edit is never shown.
        service.Hold = true;
        var stale = sheet.CalculateAsync();
        await service.EnteredAsync();
        expect.That(sheet.IsBusy && sheet.CalculationState == CalculationState.Running, "Stato durante il calcolo");
        sheet.Value = 8;
        expect.That(!await Expect.Within(stale) && !sheet.HasResults && !sheet.IsBusy, "Pubblicato il calcolo di una revisione superata");
        var first = sheet.CalculateAsync();
        await service.EnteredAsync();
        var second = sheet.CalculateAsync();
        await service.EnteredAsync();
        service.Release();
        expect.That(!await Expect.Within(first) && await Expect.Within(second) && Echo(sheet) == 8, "Calcolo superato pubblicato");

        // Cancellation by the caller: nothing published, state cancelled.
        sheet.Value = 9;
        using (var cancel = new CancellationTokenSource())
        {
            var cancelled = sheet.CalculateAsync(cancel.Token);
            await service.EnteredAsync();
            cancel.Cancel();
            expect.That(!await Expect.Within(cancelled) && !sheet.HasResults && sheet.CalculationState == CalculationState.Cancelled, "Annullamento del calcolo");
        }

        // Read-only (a previous revision): edits and drafts refused, calculation allowed (the «Calcola» of the inspection).
        service.Hold = false;
        int revision = (int)sheet.Revision, before = modified;
        var snapshot = sheet.Data.DeepClone();
        sheet.IsReadOnly = true;
        sheet.Label = "vietata"; sheet.Value = 99; sheet.Note = "vietata"; sheet.Commit();
        expect.That(JsonNode.DeepEquals(sheet.Data, snapshot) && sheet.Revision == revision && modified == before && sheet.Note is null && !sheet.CanEdit,
            "Sola lettura: modifica accettata");
        expect.That(!sheet.ReplaceSoilCommand.CanExecute(null), "Sola lettura: sostituzione del terreno disponibile");
        expect.That(await Expect.Within(sheet.CalculateAsync()) && Echo(sheet) == 9, "Sola lettura: calcolo non disponibile");
        using (var inspected = (ProbeSheetViewModel)module.CreateSheet(Context(new JsonObject { ["valore"] = 2.0 }, readOnly: true)))
        {
            inspected.Value = 5;
            expect.That(inspected.IsReadOnly && inspected.Value == 2 && inspected.Revision == 0, "Sola lettura dal contesto");
        }
        sheet.IsReadOnly = false;

        // Host commands go to the host; a failure of the host is shown through the guard (Q19), not thrown.
        host.Sources.Add(new SoilSource("Palo · Palo verticale", "geo_palo_verticale", new JsonObject(), "id1"));
        sheet.CopyCommand.Execute(null); sheet.ReplaceSoilCommand.Execute(null); sheet.ReportCommand.Execute(null); sheet.SourcesCommand.Execute(null);
        expect.That(host.Calls.SequenceEqual(["copia prova Copia di prova", "terreno", "report", "sorgenti"]) && sheet.Sources.Single().Id == "id1",
            "Comandi verso l'ospite: " + string.Join(", ", host.Calls));
        expect.That(JsonNode.DeepEquals(host.Copied!, sheet.Data) && !ReferenceEquals(host.Copied, sheet.Data), "Copia: dati del foglio non copiati");
        host.Fail = new InvalidOperationException("Ospite non disponibile");
        sheet.ReportCommand.Execute(null);
        expect.That(messages.Failures.Single().Error == host.Fail && messages.Failures.Single().Title == "Operazione non completata", "Errore dell'ospite non mostrato");
        host.Fail = null;
        expect.That(Encoding.UTF8.GetString(sheet.Report.BuildReport("Titolo", new HashSet<string> { "b", "a" }, true)) == "Titolo|a,b|True|R", "Report del foglio");

        // Automatic recalculation (delay of the module): published after the edit.
        using (var automatic = (ProbeSheetViewModel)new ProbeSheetModule(ProbeInfo, TimeSpan.Zero).CreateSheet(Context(new JsonObject { ["valore"] = 3.0 })))
        {
            automatic.Value = 4;
            expect.That(await Expect.Within(automatic.PendingCalculation) && Echo(automatic) == 4 && automatic.AutomaticDelay == TimeSpan.Zero, "Ricalcolo automatico");
        }

        // Closed during a calculation: nothing published, no error, edits and calculations refused.
        service.Hold = true;
        var closing = (ProbeSheetViewModel)module.CreateSheet(Context(new JsonObject { ["valore"] = 10.0 }));
        int closingModified = 0;
        closing.Modified += (_, _) => closingModified++;
        var running = closing.CalculateAsync();
        await service.EnteredAsync();
        closing.Dispose();
        closing.Dispose();
        expect.That(!await Expect.Within(running) && !closing.HasResults && closing.IsDisposed && !closing.CanEdit, "Chiusura durante il calcolo");
        closing.Value = 11;
        expect.That(closing.Value == 10 && closingModified == 0, "Modifica dopo la chiusura");
        expect.Throws<ObjectDisposedException>(() => _ = closing.CalculateAsync(), "Calcolo dopo la chiusura");
        expect.Throws<ObjectDisposedException>(() => closing.Reload(new JsonObject()), "Reload dopo la chiusura");
        expect.Throws<ObjectDisposedException>(closing.Commit, "Commit dopo la chiusura");
        service.Hold = false;
        expect.That(messages.Failures.Count == 1, "Errori inattesi mostrati: " + messages.Failures.Count);
        return expect.Count;
    }

    // The registry covers exactly ModuleDirectory; a sheet module must be one of its modules, with the same metadata.
    private static void Registry(Expect expect)
    {
        var historic = new ModuleRegistry([]);
        expect.That(historic.All.Select(d => d.Info).SequenceEqual(ModuleDirectory.All)
            && historic.All.Count(d => d.Info.Id != Anthea.Application.EmbeddedWalls.EmbeddedWallDocument.Module) == 13,
            "Il registro deve conservare i 13 moduli precedenti e includere il nuovo modulo paratie");
        expect.That(historic.SheetModuleIds.Count == 0 && historic.All.All(d => d is not ISheetModule), "Moduli MVVM nel registro storico");
        expect.That(historic.All.All(d => d.Help.GuideId == WikiCatalog.ForModule(d.Info.Id)?.Id && d.Persistence == ModulePersistence.Undeclared
            && d.Reports == ModuleReportCapabilities.Undeclared), "Guida, persistenza o report dei moduli storici");
        expect.That(historic.All.Count(d => d.Help.GuideId is not null) > 0, "Nessuna guida dei moduli letta dalla Wiki");
        expect.That(historic.Get("str_palo").Info == ModuleDirectory.Get("str_palo") && !historic.TryGetSheetModule("str_palo", out _), "Descrittore storico");
        expect.Throws<ArgumentException>(() => historic.Get("inesistente"), "Modulo sconosciuto accettato");
        expect.Throws<InvalidOperationException>(() => historic.SheetModule("str_palo"), "Modulo storico restituito come MVVM");

        var info = ModuleDirectory.Get("mat_acciaio_armatura");
        var probe = new ProbeSheetModule(info);
        var migrated = new ModuleRegistry([probe]);
        expect.That(migrated.All.Select(d => d.Info).SequenceEqual(ModuleDirectory.All) && migrated.SheetModuleIds.SequenceEqual([info.Id]), "Registro con un modulo MVVM");
        expect.That(ReferenceEquals(migrated.Get(info.Id), probe) && ReferenceEquals(migrated.SheetModule(info.Id), probe)
            && migrated.TryGetSheetModule(info.Id, out var found) && ReferenceEquals(found, probe), "Modulo MVVM non restituito");
        expect.Throws<ArgumentException>(() => _ = new ModuleRegistry([new ProbeSheetModule(ProbeInfo)]), "Secondo catalogo: modulo fuori da ModuleDirectory accettato");
        expect.Throws<ArgumentException>(() => _ = new ModuleRegistry([new ProbeSheetModule(info with { Name = "Altro nome" })]), "Metadati diversi da ModuleDirectory accettati");
        expect.Throws<ArgumentException>(() => _ = new ModuleRegistry([probe, new ProbeSheetModule(info)]), "Modulo MVVM registrato due volte");
        expect.Throws<ArgumentException>(() => _ = new ModuleRegistry([null!]), "Modulo nullo accettato");
    }

    private static double Echo(SheetViewModelBase sheet) => sheet.Result!["eco"]!.GetValue<double>();

    /// <summary>Records the requests of the sheet; <see cref="Fail"/> makes every request throw.</summary>
    private sealed class ScriptedHost : ISheetHost
    {
        internal List<string> Calls { get; } = [];
        internal List<SoilSource> Sources { get; } = [];
        internal JsonObject? Copied { get; private set; }
        internal Exception? Fail { get; set; }

        private void Record(string call) { if (Fail is not null) throw Fail; Calls.Add(call); }

        public void OpenModuleCopy(string module, JsonObject data, string name) { Record("copia " + module + " " + name); Copied = data; }
        public void ReplaceSoil(JsonObject data) => Record("terreno");
        public IReadOnlyList<SoilSource> SoilSources() { Record("sorgenti"); return Sources; }
        public void RequestReport() => Record("report");
    }
}
