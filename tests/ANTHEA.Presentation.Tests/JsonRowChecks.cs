using System.Collections.Specialized;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Forms;

namespace Anthea.Presentation.Tests;

/// <summary>Rows and row collections of the tables (former JsonRow/JsonRows, F5.2): values, notifications and deferral.</summary>
internal static class JsonRowChecks
{
    internal static int Run()
    {
        var expect = new Expect();
        Rows(expect);
        Collections(expect);
        return expect.Count;
    }

    static void Rows(Expect expect)
    {
        var values = new JsonObject { ["flag"] = true, ["n"] = 1.5, ["t"] = "abc" };
        var changes = new List<string>(); var notices = new List<string?>();
        var row = new JsonRowViewModel(values, changes.Add);
        row.PropertyChanged += (_, e) => notices.Add(e.PropertyName);
        expect.That(row["flag"] is true && row["n"] as string == "1.5" && row["t"] as string == "abc" && row["assente"] as string == "",
            "Lettura: booleano come bool, il resto come testo, chiave assente vuota");
        row["n"] = "2,50";
        expect.That(values["n"]!.ToJsonString() == J.Node("2,50")!.ToJsonString() && changes.SequenceEqual(["n"]) && notices.SequenceEqual(["Item[]"]),
            "Scrittura: testo grezzo con J.Node, una notifica Item[] e il richiamo con la chiave");
        row.Output("calcolato", 3.0); row.Output("calcolato", 3.0);
        expect.That(values["calcolato"]!.ToJsonString() == "3" && notices.Count == 2 && changes.Count == 1, "Valore calcolato: notifica solo se cambia, nessun richiamo");

        var other = new JsonRowViewModel(new JsonObject());
        int otherNotices = 0; other.PropertyChanged += (_, _) => otherNotices++;
        notices.Clear();
        var scope = JsonRowViewModel.DeferNotifications([row, row, other]);
        var inner = JsonRowViewModel.DeferNotifications([row]);
        row["t"] = "x"; row["t"] = "y"; row.Refresh();
        inner.Dispose();
        expect.That(notices.Count == 0, "Notifiche trattenute finché resta aperto un ambito");
        scope.Dispose(); scope.Dispose();
        expect.That(notices.SequenceEqual(["Item[]"]) && otherNotices == 0 && values["t"]!.ToString() == "y", "Una notifica per riga cambiata alla chiusura, nessuna per le righe ferme");
        row.Refresh();
        expect.That(notices.Count == 2, "Dopo l'ambito le notifiche tornano immediate");
    }

    static void Collections(Expect expect)
    {
        var rows = new JsonRowCollection();
        var events = new List<string>();
        rows.CollectionChanged += (_, e) => events.Add(e.Action.ToString());
        ((System.ComponentModel.INotifyPropertyChanged)rows).PropertyChanged += (_, e) => events.Add(e.PropertyName!);
        rows.Add(new JsonRowViewModel(new JsonObject()));
        expect.That(events.SequenceEqual(["Count", "Item[]", nameof(NotifyCollectionChangedAction.Add)]), "Senza ambito le notifiche sono quelle di ObservableCollection");
        events.Clear();
        using (rows.DeferRefresh())
        {
            using (rows.DeferRefresh()) { rows.Add(new JsonRowViewModel(new JsonObject())); rows.RemoveAt(0); }
            rows.Add(new JsonRowViewModel(new JsonObject()));
            expect.That(events.Count == 0 && rows.Count == 2, "Nessuna notifica dentro gli ambiti annidati");
        }
        expect.That(events.SequenceEqual(["Count", "Item[]", nameof(NotifyCollectionChangedAction.Reset)]), "Alla chiusura: Count, Item[] e un solo Reset");
        events.Clear();
        using (rows.DeferRefresh()) { }
        expect.That(events.Count == 0, "Ambito senza modifiche: nessuna notifica");
    }
}
