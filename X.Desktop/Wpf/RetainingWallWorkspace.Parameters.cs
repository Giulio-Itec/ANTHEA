using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace
{
    internal Window CalculationParametersWindow()
    {
        Commit(); var snapshot = (JsonObject)Data.DeepClone(); int openedRevision = revision;
        var window = new Window { Owner = Window.GetWindow(this), Title = "Muro · valori utilizzati nel calcolo", Width = 1150, Height = 780, MinWidth = 780, MinHeight = 530, Background = Appearance.Surface, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var rows = new JsonGrid([new("group", "Gruppo", ReadOnly: true), new("meaning", "Parametro", ReadOnly: true), new("value", "Valore assegnato"), new("unit", "Unità", ReadOnly: true)]) { Height = 230 };
        rows.Columns[0].Width = 270; rows.Columns[1].Width = 340; rows.Columns[2].Width = 190;
        var bindings = new List<(JsonObject Row, JsonObject Owner, string Key, JsonNode? Original)>();
        var known = RetainingWall.GeometryFields.Concat(RetainingWall.FoundationFields).Concat(RetainingWall.MaterialFields).Concat(RetainingWall.SeismicFields).GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.First());
        var labels = new Dictionary<string, string> { ["thickness"] = "Spessore strato", ["phi"] = "Angolo di attrito terreno", ["free_height"] = "Altezza libera dalla sommità a valle", ["mobilization"] = "Frazione passiva mobilitata 0–1", ["wall_delta"] = "Attrito muro assegnato", ["wall_phi_cv"] = "φcv,k contatto muro", ["base_phi_cv"] = "φcv,k contatto fondazione", ["wall_mode"] = "Attrito muro: " + string.Join(" / ", RetainingWall.FrictionModes), ["base_mode"] = "Attrito base: " + string.Join(" / ", RetainingWall.FrictionModes), ["height_mode"] = "Interamente libero / Assegnato", ["mphi"] = "γM sulla tangente di φ", ["rslide"] = "γR scorrimento", ["rover"] = "γR ribaltamento", ["rbearing"] = "γR portanza" };
        string GroupName(string path)
        {
            var parts = path.Split('/'); string root = parts[0] switch { "geometry" => "Geometria", "layers" => "Strati monte", "valley" => "Terreno valle", "interfaces" => "Attriti", "foundation" => "Terreno di posa", "materials" => "Materiali", "water" => "Falda", "seismic" => "Sisma", "reinforcement" => "Armature", "actions" => "Azioni", "combinations" => "Combinazioni", "global_stability" => "Stabilità globale", _ => parts[0] };
            return root + (parts.Length > 1 ? " · " + string.Join(" · ", parts.Skip(1).Select(p => int.TryParse(p, out int n) ? (n+1).ToString() : p)) : "");
        }
        foreach (var item in new[] { ("name", "Descrizione"), ("linked", "Modifica insieme le colonne (true/false)"), ("passive", "Usa passiva (true/false)"), ("enabled", "Attivo (true/false)"), ("visible", "Disegna (true/false)"), ("gamma_sat", "Peso specifico saturo"), ("soil", "Fattore del peso terreno monte"), ("valley_soil", "Fattore del peso terreno valle"), ("wall", "Fattore del peso muro"), ("water", "Fattore delle pressioni idrauliche"), ("bottom", "Quota del fondo strato"), ("soil_mode", "Profilo unico / Due colonne"), ("soil_split_x", "Ascissa del confine valle / monte"), ("profile_confirmed", "Profilo confermato (true/false)"), ("front_head", "Battente a valle dal piano di posa"), ("depth", "Profondità della falda da monte"), ("source", "Origine dei coefficienti sismici"), ("method", "Metodo delle spinte sismiche"), ("value", "Intensità dell’azione"), ("type", "Tipo di azione"), ("category", "Categoria dell’azione"), ("state", "Stato limite"), ("approach", "Approccio di verifica"), ("condition", "Condizione drenata / non drenata"), ("two_zones", "Due zone armature (true/false)"), ("lower_height", "Altezza armatura inferiore"), ("diameter", "Diametro barre"), ("count", "Barre per metro e per faccia") }) labels[item.Item1] = item.Item2;
        void Walk(JsonNode? node, string path)
        {
            if (node is JsonArray array) { for (int i = 0; i < array.Count; i++) Walk(array[i], path + "/" + i); return; }
            if (node is not JsonObject obj) return;
            foreach (var pair in obj)
            {
                if (pair.Key.StartsWith("__") || pair.Key is "id" or "combination_signature") continue;
                string key = path + "/" + pair.Key;
                if (pair.Value is JsonObject or JsonArray) { Walk(pair.Value, key); continue; }
                var p = known.GetValueOrDefault(pair.Key); var row = J.Obj(("path", key), ("group", GroupName(path)), ("meaning", labels.GetValueOrDefault(pair.Key, p?.Label ?? pair.Key)), ("value", pair.Value?.ToString() ?? ""), ("unit", p?.Unit ?? ""));
                rows.Rows.Add(new JsonRow(row)); bindings.Add((row, obj, pair.Key, pair.Value?.DeepClone()));
            }
        }
        foreach (string group in new[] { "geometry", "layers", "valley", "interfaces", "foundation", "materials", "water", "seismic", "reinforcement", "actions", "combinations", "global_stability" }) Walk(snapshot[group], group);
        var combo = Ui.Choice(Calculation?.Cases.Select(c => c.Name) ?? [], Combination.SelectedItem as string ?? Calculation?.Cases.FirstOrDefault()?.Name ?? "");
        var derived = new ContentControl();
        void Display()
        {
            if (Calculation?.Cases.FirstOrDefault(c => c.Name == combo.SelectedItem as string) is not { } c) { derived.Content = Ui.Text("Ricalcolare per interrogare i valori derivati.", 12); return; }
            var values = new List<string[]>();
            void Add(string key, string value, string source) => values.Add([key, value, source]);
            foreach (var p in c.SoilAudit) { var meaning = RetainingWall.AuditMeaning(p.Key); Add(meaning.Label, (J.Number(p.Value) is double n ? n.ToString("0.#####", It) : p.Value?.ToString() ?? "") + " " + meaning.Unit, meaning.Origin); }
            foreach (var p in c.Factors ?? new()) if (p.Value is JsonValue) Add(p.Key, p.Value.ToString(), "Coefficiente della combinazione selezionata");
            foreach (var p in c.PressureDetails) Add($"Monte z={F(p.Z0)}–{F(p.Z1)} m", $"φd={F(p.PhiDesign)}°; K={p.K:0.00000}; Ke={p.Ke:0.00000}; σ′={F(p.Sigma0)}–{F(p.Sigma1)}; p={F(p.Total0)}–{F(p.Total1)} kPa", "Piano esterno di equilibrio; acqua e incremento inclusi");
            foreach (var p in c.StemPressureDetails) Add($"Coefficienti fusto z={F(p.Z0)}–{F(p.Z1)} m", $"φd={F(p.PhiDesign)}°; K={p.K:0.00000}; Ke={p.Ke:0.00000}; Δp={F(p.Dynamic)} kPa", "Coulomb/MO con δ muro, oppure K₀ Wood");
            foreach (var p in c.StemPressures) Add($"Fusto z={F(p.Z0)}–{F(p.Z1)} m", $"p={F(p.P0)}–{F(p.P1)} kPa", "Coulomb/MO al paramento + acqua + azioni − passiva");
            foreach (var p in c.ValleyPressures) Add($"Valle z={F(p.Z0)}–{F(p.Z1)} m", $"p={F(p.P0)}–{F(p.P1)} kPa", "Passiva effettivamente utilizzata · z dalla sommità muro");
            Add("Equilibrio", $"H={F(c.Horizontal)}; V′={F(c.Vertical)}; U={F(c.Uplift)} kN/m; Mstab={F(c.Stabilizing)}; Mrib={F(c.Overturning)} kNm/m", "Somma forze e momenti");
            Add("Fondazione", $"e={F(c.Eccentricity)} m; B′={F(c.EffectiveWidth)} m; Rsc={F(c.SlidingResistance)} kN/m; Rport={c.BearingResistance?.ToString("0.###") ?? "non disponibile"} kN/m", "Contatto e resistenze");
            foreach (var p in c.Sections) Add($"{p.Name} z/l={F(p.Position)} m", $"N={F(p.N)} kN/m; M={F(p.M)} kNm/m; V={F(p.V)} kN/m", "Equilibrio della sezione · modificare le cause negli input");
            var table = Table(["Valore", "Risultato utilizzato", "Origine / significato"], values); table.Height = 230; derived.Content = table;
        }
        combo.SelectionChanged += (_, _) => Display(); Display();
        var message = Ui.Text("Modificare gli input nella colonna Valore. Per cambiare un coefficiente derivato selezionare la relativa modalità Assegnato. Le forze risultanti sono consultabili; si aggiornano ricalcolando le loro cause.", 11);
        var apply = Ui.Button("Applica e ricalcola", async () =>
        {
            rows.Commit();
            try
            {
                if (revision != openedRevision) throw new ArgumentException("Il foglio è cambiato: chiudere e riaprire il riepilogo.");
                bool comboEdit = false, globalEdit = false, rearEdit = false, frontEdit = false;
                foreach (var b in bindings)
                {
                    string value = b.Row.S("value"); if (value == (b.Original?.ToString() ?? "")) { b.Owner[b.Key] = b.Original?.DeepClone(); continue; }
                    rearEdit |= b.Row.S("path").StartsWith("layers/"); frontEdit |= b.Row.S("path").StartsWith("valley/layers/");
                    if (b.Original is JsonValue flag && flag.TryGetValue<bool>(out _)) b.Owner[b.Key] = bool.TryParse(value, out bool enabled) ? enabled : throw new ArgumentException(b.Row.S("path") + ": true o false.");
                    else if (double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) { if (!double.IsFinite(number)) throw new ArgumentException("Valore non finito."); b.Owner[b.Key] = number; }
                    else b.Owner[b.Key] = value;
                    comboEdit |= b.Row.S("path").StartsWith("combinations/"); globalEdit |= b.Row.S("path").StartsWith("global_stability/combinations/");
                }
                if (snapshot["valley"].B("linked")) { if (frontEdit && rearEdit) throw new ArgumentException("Colonne collegate: modificare una sola colonna, oppure disattivare il collegamento."); RetainingWall.CopySoilColumn(snapshot, frontEdit); }
                if (comboEdit) { snapshot["combination_mode"] = "Personalizzate"; snapshot["combination_signature"] = RetainingWall.CombinationSignature(snapshot); }
                if (globalEdit) { snapshot["global_stability"]!["combination_mode"] = "Personalizzate"; snapshot["global_stability"]!["combination_signature"] = RetainingWall.GlobalSignature(snapshot); }
                window.IsEnabled = false; message.Text = "Controllo dati e ricalcolo…";
                var checkedResult = await Task.Run(() => RetainingWall.Calculate(snapshot));
                if (checkedResult.GlobalError is not null) throw new ArgumentException(checkedResult.GlobalError);
                Data.Clear(); foreach (var p in snapshot) Data[p.Key] = p.Value?.DeepClone(); BuildInputs(); BuildMatrix(); UpdateFields(); Changed(); window.Close(); await CalculateAsync();
            }
            catch (Exception ex) { message.Text = ex.Message; message.Foreground = Brushes.Firebrick; }
            finally { window.IsEnabled = true; }
        });
        var body = Ui.Stack(Ui.Text("Input modificabili e valori effettivamente utilizzati", 18, true), message, rows, Ui.Bar(Ui.Text("Combinazione", 12), combo), derived, Ui.Bar(apply, Ui.Button("Chiudi", window.Close)));
        body.Background = Appearance.Surface; body.Margin = new Thickness(15); window.Content = new Border { Background = Appearance.Surface, Child = Scroll(body) }; return window;
    }
    private async void ShowCalculationParameters() { await CalculateAsync(); if (!disposed) CalculationParametersWindow().ShowDialog(); }
}
