using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace
{
    internal readonly TextBlock globalReadiness = Ui.Text("", 12);
    internal readonly TextBlock globalSearchSummary = Ui.Text("", 11, color: Ui.Muted);
    private static readonly string[] GlobalSearchLimits = ["exit_min", "exit_max", "entry_min", "entry_max", "depth_min", "depth_max"];
    internal const string GlobalResultExplanation = "Leggere prima l’Esito di ogni combinazione. F è il fattore trovato con i parametri di progetto; il tasso è η=γR/F. La verifica è soddisfatta nel dominio esplorato quando F≥γR (η≤1) e la ricerca è completa e convergente. «Minimo sul bordo», «ricerca incompleta» o «discretizzazione non convergente» richiedono di correggere/ampliare la ricerca, anche con η<1. Il cerchio disegnato è la superficie critica; le linee verticali sono i conci di terreno.";

    internal void ShowGlobalSetup()
    {
        var g = Data["global_stability"]!;
        if (g.Array("valley").Count == 0 && g.Array("uphill").Count == 0 && g.Array("layers").Count == 0 && g.Array("valley_layers").Count == 0) PrepareGlobal();
        Pages.SelectedIndex = 0; Cards["Terreno"].IsExpanded = true; Cards["Stabilità globale"].IsExpanded = true;
        ScrollGlobalSetup();
    }

    private void ScrollGlobalSetup()
    {
        UpdateLayout(); var card = Cards["Stabilità globale"];
        DependencyObject? parent = card;
        while ((parent = VisualTreeHelper.GetParent(parent)) is not null)
            if (parent is ScrollViewer viewer)
            {
                double offset = card.TransformToAncestor(viewer).Transform(new Point()).Y + viewer.VerticalOffset;
                viewer.ScrollToVerticalOffset(Math.Max(0, offset)); break;
            }
    }

    private void InvalidateGlobalConfirmation()
    {
        Data["global_stability"]!["profile_confirmed"] = false;
        if (Forms.TryGetValue("global", out var form) && form.Editors["profile_confirmed"] is CheckBox box) box.IsChecked = false;
    }
    private void RefreshGlobalSearch()
    {
        var g = Data["global_stability"]!; bool automatic = g.S("search_mode") == "Automatica";
        if (automatic) RetainingWall.ProposeGlobalSearch(Data);
        if (Forms.TryGetValue("global_search", out var form)) foreach (string key in GlobalSearchLimits)
        {
            form.Enable(key, !automatic, true);
            if (automatic) form.Set(key, g.S(key), display: true);
        }
        globalSearchSummary.Text = J.Number(g["depth_max"]) is double depth
            ? $"Ricerca da {g.D("depth_min"):0.##} a {depth:0.##} m sotto la fondazione · {g.D("slices"):0} conci. Limiti visibili nel disegno."
            : "Aggiungi gli strati sotto la fondazione: la profondità di ricerca si aggiornerà automaticamente.";
    }

    private void RefreshGlobalGuidance()
    {
        var g = Data["global_stability"]!;
        var items = new List<string>();
        if (!g.B("enabled")) items.Add("Attiva la verifica globale oppure premi «Prepara dal muro».");
        if (g.Array("valley").Count < 2 || g.Array("uphill").Count < 2) items.Add("Precompila il profilo, poi correggi le quote secondo il rilievo.");
        else if (g.Array("valley").Concat(g.Array("uphill")).Any(p => new[] { "x", "y" }.Any(k => J.Number(p?[k]) is not double n || !double.IsFinite(n)))) items.Add("Completa le coordinate x e y di tutti i punti del rilievo.");
        var columns = g.S("soil_mode") == "Due colonne" ? new[] { ("monte", "layers"), ("valle", "valley_layers") } : new[] { ("profilo unico", "layers") };
        foreach (var (name, key) in columns)
        {
            var layers = g.Array(key);
            if (layers.Count == 0) { items.Add($"Inserisci gli strati profondi: {name}."); continue; }
            var bottom = J.Number(layers[^1]?["bottom"]);
            if (bottom is null || !double.IsFinite(bottom.Value) || bottom >= 0) items.Add($"La colonna {name} deve raggiungere il terreno sotto la fondazione (fondo y negativo).");
            else if (J.Number(g["depth_max"]) is double depth && depth > -bottom) items.Add($"{name}: strati fino a y={bottom:0.##} m; la ricerca a {depth:0.##} m è più profonda dei dati disponibili.");
            string[] required = g.S("condition") == "Non drenata" ? ["gamma", "gamma_sat"] : ["gamma", "gamma_sat", "phi", "c"];
            if (layers.Any(l => required.Any(k => J.Number(l?[k]) is not double n || !double.IsFinite(n)))) items.Add($"{name}: seleziona gli strati e completa le proprietà del terreno.");
            if (g.S("condition") == "Non drenata" && layers.Any(l => J.Number(l?["cu"]) is not double cu || cu <= 0)) items.Add($"{name}: inserisci cu,k > 0 per tutti gli strati non drenati.");
        }
        if (J.Number(g["depth_max"]) is not double maximum || !double.IsFinite(maximum) || maximum <= g.D("depth_min")) items.Add("Imposta una profondità massima di ricerca maggiore della minima, entro gli strati noti.");
        if (g.B("water_enabled") && g.Array("water").Count < 2) items.Add("Completa la linea di falda con almeno due punti (x;y).");
        if (!g.B("profile_confirmed")) items.Add("Controlla rilievo, terreni e falda, poi spunta «Ho controllato profilo, strati e falda del sito».");
        var error = independentGlobalError ?? Calculation?.GlobalError;
        if (!string.IsNullOrWhiteSpace(error)) items.Add("Ultimo calcolo: " + error);
        globalReadiness.Text = items.Count == 0 ? "Pronto per il calcolo · premi «Calcola globale». L’esito si legge nelle Verifiche." : "Da completare · " + items[0] + (items.Count > 1 ? $"\nAltri {items.Count - 1} controlli da completare: vedi il suggerimento su questo messaggio." : "");
        globalReadiness.ToolTip = string.Join("\n", items.Select((item, i) => $"{i + 1}. {item}"));
        ToolTipService.SetShowDuration(globalReadiness, 30000);
        globalReadiness.Foreground = Appearance.Foreground(items.Count == 0 ? Ui.Navy : Brushes.DarkGoldenrod);
        GlobalSetupButton.Content = !g.B("enabled") ? "Stabilità globale: non attiva · imposta" : GlobalResult is null ? "Stabilità globale: dati / calcolo" : "Stabilità globale: calcolata · imposta";
        GlobalSetupButton.ToolTip = "Apri il percorso guidato: profilo del terreno, strati profondi, falda, ricerca e verifica.";
    }
}
