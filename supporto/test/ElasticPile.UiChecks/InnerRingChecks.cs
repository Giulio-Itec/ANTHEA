using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Anthea.Calculations;
using X.Core;

static partial class Program
{
    /// <summary>
    /// --inner-ring-only [--probe]: second ring of longitudinal bars (keys second_inner_*, «Secondo anello interno» of the c.a.
    /// section) in the horizontal pile: main section form and drawing, reinforcement segments, round trip through the c.a.
    /// verifier of a segment, elastic verifications. With --probe every finding is written to anello-interno.txt without
    /// stopping (capture before a correction); without it every finding is a check. Images in the same folder.
    /// </summary>
    static int InnerRingChecks(string dir, bool probe)
    {
        capture = true;
        var lines = new List<string>();
        void Fact(bool ok, string text) { lines.Add((ok ? "PASS " : "FAIL ") + text); if (!probe) Check(ok, text); else Console.WriteLine((ok ? "PASS " : "FAIL ") + text); }
        static void Ring(JsonObject target, bool enabled, int count = 8, double diameter = 16, double gap = 30)
        { target["second_inner_enabled"] = enabled; target["second_inner_count"] = count; target["second_inner_diameter"] = diameter; target["second_inner_gap"] = gap; }
        try
        {
            var app = new TestApp(); app.LoadStyles(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var assembly = typeof(X.Desktop.MainWindow).Assembly;

            // Main section of the pile: form and drawing.
            var root = Input(); root["elastico"]!["dettagli"]!["azioni_progetto"] = true; root["elastico"]!["dettagli"]!["taglio_confermato"] = true;
            int outer = (int)root["sezione"].D("longitudinal_bar_count", 16);
            var ringed = (JsonObject)root.DeepClone(); Ring(ringed["sezione"]!.AsObject(), true);
            var hostType = assembly.GetType("X.Desktop.HorizontalWorkspace")!;
            var host = (FrameworkElement)Activator.CreateInstance(hostType, flags, null, [ringed], null)!;
            var form = (FrameworkElement)hostType.GetField("sectionFields", flags)!.GetValue(host)!;
            var editors = (System.Collections.IDictionary)form.GetType().GetField("Editors", flags)!.GetValue(form)!;
            Fact(new[] { "second_inner_enabled", "second_inner_count", "second_inner_diameter", "second_inner_gap" }.All(editors.Contains), "main pile section offers the inner ring fields");
            Snapshot(form, Path.Combine(dir, "sezione-principale-campi.png"), 700, 1300);
            var drawing = (FrameworkElement)hostType.GetField("sectionDrawing", flags)!.GetValue(host)!;
            var bars = (System.Collections.ICollection)(drawing.GetType().GetProperty("Bars", flags)?.GetValue(drawing) ?? drawing.GetType().GetField("Bars", flags)!.GetValue(drawing)!);
            Fact(bars.Count == outer + 8, $"main section drawing shows both rings ({bars.Count} bars, expected {outer + 8})");
            Snapshot(drawing, Path.Combine(dir, "sezione-principale-disegno.png"), 400, 400);
            ((IDisposable)host).Dispose();

            // Segment T2 with its own reinforcement: inner ring added in the c.a. verifier.
            var trial = (JsonObject)root.DeepClone();
            var two = new JsonArray(ElasticHorizontalPile.NewSegment("T1", 5), ElasticHorizontalPile.NewSegment("T2", null)); trial["elastico"]!["tratti"] = two;
            two[1]!["collegato"] = false; two[1]!["longitudinal_bar_count"] = 12;
            string plain = ElasticHorizontalPile.SegmentSection(trial, two[1]!.AsObject()).ToJsonString();
            Fact(!plain.Contains("second_inner"), "a pile without inner ring gives the segment section unchanged (no inner ring keys)");
            var result = ElasticHorizontalPile.CalculateShared(trial);
            var sheet = ElasticHorizontalPile.CreateConcreteSheet(trial, result, 1);
            // Same diameter as the first ring: the bill of the library does not accept mixed diameters (checked below).
            double phi = sheet["input"].D("longitudinal_bar_diameter_mm");
            Ring(sheet["input"]!.AsObject(), true, 8, phi);
            try { ElasticHorizontalPile.ApplyConcreteSheetReinforcement(trial, sheet, 1); } catch (ArgumentException ex) { lines.Add("NOTE apply: " + ex.Message); }
            two[1]!["foglio_cls"] = sheet.DeepClone();
            Fact(two[1].B("second_inner_enabled") && two[1].D("second_inner_count") == 8 && two[1].D("second_inner_diameter") == phi && two[1].D("second_inner_gap") == 30,
                "applying the c.a. verifier keeps the inner ring on the segment");
            var again = ElasticHorizontalPile.CreateConcreteSheet(trial, result, 1);
            Fact(again["input"].B("second_inner_enabled") && again["input"].D("second_inner_count") == 8, "reopening the c.a. verifier of the segment keeps the inner ring");
            var caType = assembly.GetType("X.Desktop.ConcreteWorkspace")!; var ca = (FrameworkElement)Activator.CreateInstance(caType, flags, null, [again], null)!;
            Snapshot(ca, Path.Combine(dir, "verificatore-tratto-riaperto.png"), 1400, 950); ((IDisposable)ca).Dispose();
            var checkedRing = ElasticHorizontalPile.CalculateShared(trial);
            int t1 = checkedRing["armature"]!.Array("tratti")[0]!.Array("barre_sezione").Count, t2 = checkedRing["armature"]!.Array("tratti")[1]!.Array("barre_sezione").Count;
            Fact(t1 == outer && t2 == 12 + 8, $"elastic verifications use the inner ring only where it is assigned (T1 {t1}, T2 {t2} bars)");
            var mixed = (JsonObject)trial.DeepClone(); mixed["elastico"]!["tratti"]![1]!["second_inner_diameter"] = phi == 16 ? 20 : 16;
            string refusal = ""; try { ElasticHorizontalPile.CalculateShared(mixed); } catch (ArgumentException ex) { refusal = ex.Message; }
            Fact(refusal.Contains("diametri misti"), $"library limit: a second ring of another diameter stops the bill of the pile with an explicit message ({refusal})");

            // Segment cards.
            var editorType = assembly.GetType("X.Desktop.PileReinforcementEditor")!;
            var editor = (FrameworkElement)Activator.CreateInstance(editorType, flags, null, [trial, (Action)(() => { }), (Func<JsonObject?>)(() => checkedRing), null], null)!;
            editor.Measure(new Size(1500, 1100)); editor.Arrange(new Rect(0, 0, 1500, 1100)); editor.UpdateLayout();
            Fact(Descendants<CheckBox>(editor).Count(c => c.Content?.ToString()?.Contains("anello interno") == true) == 2, "every segment card shows the inner ring choice");
            Snapshot(editor, Path.Combine(dir, "tratti.png"), 1500, 1100);

            // Main section through the verifier of the first segment.
            var main = (JsonObject)root.DeepClone(); main["elastico"]!["tratti"] = new JsonArray(ElasticHorizontalPile.NewSegment("T1", null));
            var mainResult = ElasticHorizontalPile.CalculateShared(main);
            var first = ElasticHorizontalPile.CreateConcreteSheet(main, mainResult, 0); Ring(first["input"]!.AsObject(), true);
            try { ElasticHorizontalPile.ApplyConcreteSheetReinforcement(main, first, 0); } catch (ArgumentException ex) { lines.Add("NOTE apply main: " + ex.Message); }
            Fact(main["sezione"].B("second_inner_enabled") && main["sezione"].D("second_inner_count") == 8, "applying the verifier of the first segment sets the inner ring of the main section");
            Ring(first["input"]!.AsObject(), true, 3);
            bool refused = false; try { ElasticHorizontalPile.ApplyConcreteSheetReinforcement(main, first, 0); } catch (ArgumentException) { refused = true; }
            Fact(refused && main["sezione"].D("second_inner_count") == 8, "an inner ring with fewer than four bars is refused, as in the c.a. section, and nothing changes");
            File.WriteAllText(Path.Combine(dir, "input-anello.json"), trial.ToJsonString(J.Options));
            return lines.Any(l => l.StartsWith("FAIL")) && !probe ? 1 : 0;
        }
        finally { File.WriteAllLines(Path.Combine(dir, "anello-interno.txt"), lines); }
    }
}
