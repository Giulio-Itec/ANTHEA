using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private JsonObject? sharedBaseline;
    private Button confirmShared = null!;
    private Func<bool>? sharedChoiceForTest;

    private void ConfirmSharedChanges(JsonObject proposed)
    {
        if (document.S("tipo") != "progetti" || currentSheet?.Parent?.Parent is not JsonObject section || sharedBaseline is null) return;
        var preview = ProjectSharedData.PreviewSection(section, currentSheet, proposed["dati"]!.AsObject());
        var keys = ProjectSharedData.ReferenceKeys(preview.Sheet, ProjectSharedData.ChangedKeys(sharedBaseline, proposed));
        var groups = ProjectSharedData.Fields(proposed).Values.Where(f => keys.Contains(f.Key)).Select(f => f.Group).ToHashSet();
        if (groups.Count == 0) return;
        bool controlsDescendants = ProjectSharedData.Sections(section).Skip(1).Any(s => s.Array("fogli").Count > 0);
        var targets = ProjectSharedData.SubtreeSheets(section).Where(s => !ReferenceEquals(s, currentSheet) &&
            ProjectSharedData.Common(proposed, s).Any(p => keys.Contains(p.Source.Key) && !ProjectSharedData.Equal(p.Source.Value, p.Target.Value))).ToArray();
        if (targets.Length == 0) return;
        bool update;
        if (controlsDescendants) update = true;
        else if (sharedChoiceForTest is not null) update = sharedChoiceForTest();
        else if (testing) update = false;
        else
        {
            var content = Ui.Stack(Ui.Text("Hai modificato: " + string.Join(", ", groups), 17, true),
                Ui.Text("Sezione: " + section.S("nome") + "\nAggiorna i parametri compatibili nei seguenti fogli, oppure mantieni la modifica solo qui. Carichi e combinazioni restano specifici di ciascun foglio.", 14),
                Ui.Text(string.Join("\n\n", targets.Select(s => s.S("nome", ModuleName(s.S("modulo_id"))) + "\n" + string.Join("\n", ProjectSharedData.Common(proposed, s)
                    .Where(p => keys.Contains(p.Source.Key) && !ProjectSharedData.Equal(p.Source.Value, p.Target.Value))
                    .Select(p => "• " + SharedFieldLabel(p.Source.Key) + ": " + ProjectSharedData.Text(p.Target.Value) + " → " + ProjectSharedData.Text(p.Source.Value))))), 14));
            content.Margin = new Thickness(18);
            var dialog = Ui.Dialog(this, "Dati condivisi della sezione", new ScrollViewer { Background = Ui.Bg, Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 670, 440);
            var all = Ui.Button("Aggiorna tutti i fogli collegati", () => dialog.DialogResult = true, true);
            var local = Ui.Button("Solo questo foglio", () => dialog.DialogResult = false); local.IsCancel = true;
            content.Children.Add(Ui.Bar(all, local));
            update = dialog.ShowDialog() == true;
        }
        if (!update) return;
        var before = currentSheet["dati"]?.DeepClone();
        currentSheet["dati"] = proposed["dati"]!.DeepClone();
        try { ProjectSharedData.ApplyHierarchy(currentSheet, groups, keys); MarkDirty(); }
        catch { currentSheet["dati"] = before; throw; }
    }

    private static void InheritSectionData(JsonObject sheet, JsonObject section)
    {
        ProjectSharedData.InheritHierarchy(sheet, section);
    }

    private void AddCoherenceBadge(StackPanel header, JsonObject section)
    {
        if (!ProjectSharedData.SubtreeSheets(section).Any()) return;
        var differences = ProjectSharedData.Differences(section);
        bool comparable = ProjectSharedData.ComparisonPairs(section).Any(p => ProjectSharedData.ComparableFields(p.First, p.Second).Any());
        var warnings = ProjectSharedData.Limitations(section).Concat(CoverChecks(section).Where(c => c.Passed != true).Select(c => c.Text)).Distinct().ToArray();
        if (!comparable && warnings.Length == 0) return;
        bool hasConflicts = differences.Count > 0 || ProjectSharedData.MissingSoilLayers(section).Count > 0;
        string title = hasConflicts ? "⚠ Differenze tra fogli" : comparable ? "✓ Dati comuni coerenti" : "";
        if (warnings.Length > 0) title += (title.Length > 0 ? " · " : "") + $"⚠ {warnings.Length} {(warnings.Length == 1 ? "avviso" : "avvisi")}";
        var badge = Ui.Button(title, () => Safe(() => ShowCoherence(section)));
        badge.FontSize = 11;
        badge.Foreground = !hasConflicts && warnings.Length == 0 ? System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.DarkOrange;
        badge.ToolTip = warnings.Length == 0 ? "Apri il confronto dei dati comuni della sezione." :
            "Apri il confronto e gli avvisi della sezione.\n\n" + string.Join("\n\n", warnings);
        header.Children.Add(badge);
    }
    private static string SharedFieldLabel(string key) => key.StartsWith("Strato · ") ? ProjectSharedData.SoilFieldLabel(key) : key switch
    {
        "esposizione" => "Classe di esposizione", "shape" => "Forma della sezione", "diameter_mm" => "Diametro [mm]", "width_mm" => "Larghezza [mm]",
        "height_mm" => "Altezza [mm]", "cover_mm" => "Copriferro netto [mm]", "lunghezza" => "Lunghezza [m]",
        "longitudinal_bar_count" => "Numero barre longitudinali", "longitudinal_bar_diameter_mm" => "Diametro barre longitudinali [mm]",
        "transverse_bar_diameter_mm" => "Diametro staffe [mm]", "transverse_spacing_mm" => "Passo staffe [mm]",
        "barre_manuali" => "Disposizione manuale delle barre", "trefoli" => "Trefoli",
        "fyk_mpa" => "Resistenza caratteristica acciaio [MPa]", "gamma_c" => "Coefficiente gamma c",
        "gamma_s" => "Coefficiente gamma s", "alpha_cc" => "Coefficiente alfa cc",
        "classe_acciaio" => "Classe dell'acciaio", "materiale_acciaio_nome" => "Nome dell'acciaio",
        "steel_modulus_mpa" => "Modulo elastico acciaio [MPa]", "steel_fu_mpa" => "Resistenza a rottura acciaio [MPa]",
        "steel_eps_u" => "Deformazione ultima acciaio [‰]", "steel_diagramma" => "Diagramma dell'acciaio",
        _ => key.Replace("_", " ")
    };
    private sealed record SharedSheet(JsonObject Sheet)
    {
        public override string ToString() => Sheet.S("nome", ModuleName(Sheet.S("modulo_id")));
    }
    private void ShowCoherence(JsonObject section)
    {
        Commit();
        var panel = new StackPanel { Margin = new Thickness(22) };
        var dialog = Ui.Dialog(this, "Confronto · " + section.S("nome"), new ScrollViewer { Background = Ui.Bg, Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 900, 650);
        void Render()
        {
        panel.Children.Clear();
        panel.Children.Add(Ui.Text("Confronto tra fogli", 23, true));
        panel.Children.Add(Ui.Text("I fogli dei livelli superiori guidano i dati comuni delle sottosezioni. Uniforma usando un riferimento superiore; per i dati locali scegli il foglio da mantenere. Il percorso indica la sezione di appartenenza.", 13));
        var differences = ProjectSharedData.Differences(section);
        if (differences.Count == 0 && ProjectSharedData.MissingSoilLayers(section).Count == 0) panel.Children.Add(Ui.Text("Nessun conflitto tra i dati comuni dei fogli.", 13));
        int conflictNumber = 0;
        foreach (var conflict in differences.GroupBy(d => d.Key))
        {
            var participants = conflict.SelectMany(d => new[] { d.First, d.Second }).ToHashSet();
            foreach (var participant in participants.ToArray())
                participants.UnionWith(ProjectSharedData.AncestorReferences(participant, conflict.Key));
            var block = new StackPanel();
            Grid.SetIsSharedSizeScope(block, true);
            string label = SharedFieldLabel(conflict.Key);
            var title = Ui.Text($"Conflitto {++conflictNumber}: {label}", 16, true);
            block.Children.Add(new Border { Background = Ui.Brush("#EDF3F9"), Padding = new Thickness(16, 12, 16, 12),
                BorderBrush = Ui.Brush("#D8E0EB"), BorderThickness = new Thickness(0, 0, 0, 1), Child = title });
            // Keep project order and show each participating sheet once, even when several pairs disagree.
            foreach (var sheet in ProjectSharedData.ContextSheets(section).Where(participants.Contains))
            {
                var entry = new StackPanel { Margin = new Thickness(12, 10, 0, 2) };
                entry.Children.Add(Ui.Text(sheet.S("nome", ModuleName(sheet.S("modulo_id"))), 14, true));
                entry.Children.Add(Ui.Text(ProjectSharedData.Location(sheet), 11, color: Ui.Muted));
                var references = ProjectSharedData.AncestorReferences(sheet, conflict.Key);
                if (references.Length > 0) entry.Children.Add(Ui.Text("Riferimento superiore: " + string.Join(", ", references.Select(s => s.S("nome"))), 11, true, Ui.Blue));
                entry.Children.Add(Ui.Text(label + ": " + ProjectSharedData.Text(ProjectSharedData.Fields(sheet)[conflict.Key].Value), 13, color: Ui.Muted));
                ComboBox? soilType = null;
                if (conflict.Key.StartsWith("Strato · ") && conflict.Key.EndsWith("/tipologia"))
                {
                    soilType = new ComboBox { ItemsSource = new[] { "Granulare", "Coesivo" },
                        SelectedItem = ProjectSharedData.Fields(sheet)[conflict.Key].Value?.ToString(),
                        Width = 180, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0),
                        Tag = (sheet, conflict.Key), ToolTip = "Scegli la tipologia, poi premi Uniforma a questo per applicarla ai fogli compatibili." };
                    System.Windows.Automation.AutomationProperties.SetName(soilType, "Tipologia del terreno · " + sheet.S("nome"));
                    entry.Children.Add(soilType);
                }
                var row = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "ConflictValue" });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                entry.Margin = new Thickness(0, 0, 20, 0);
                entry.MinWidth = 260; entry.MaxWidth = 360;
                row.Children.Add(entry);
                var align = Ui.Button("Uniforma a questo", () => Safe(() =>
                {
                    bool sourceChanged = soilType?.SelectedItem is string selected &&
                        ProjectSharedData.Fields(sheet)[conflict.Key].Value?.ToString() != selected;
                    if (sourceChanged && soilType?.SelectedItem is string soilValue) ProjectSharedData.SetSoilType(sheet, conflict.Key, soilValue);
                    int count = ProjectSharedData.ApplyHierarchy(sheet,
                        new HashSet<string> { conflict.First().Group }, new HashSet<string> { conflict.Key });
                    if (count > 0 || sourceChanged)
                    {
                        MarkDirty();
                        if (currentSheet is not null)
                        {
                            var active = currentSheet; editor?.Dispose(); editor = null; ShowSheet(active);
                        }
                        RefreshTree();
                    }
                    Render();
                }));
                align.Tag = (sheet, conflict.Key);
                align.IsEnabled = references.Length == 0;
                if (references.Length > 0) align.Content = "Usa il riferimento superiore";
                if (soilType is not null) soilType.IsEnabled = references.Length == 0;
                align.ToolTip = references.Length == 0 ? "Uniforma " + label + " nella sezione di questo foglio e nelle sue sottosezioni." : "Questo dato è guidato dal livello superiore. Usa il pulsante sul foglio di riferimento.";
                align.VerticalAlignment = VerticalAlignment.Center;
                align.Margin = new Thickness(0);
                align.Padding = new Thickness(14, 8, 14, 8);
                align.BorderBrush = Ui.Brush("#A9BED3");
                Grid.SetColumn(align, 1); row.Children.Add(align);
                row.Margin = new Thickness(16, 12, 16, 12);
                block.Children.Add(row);
            }
            panel.Children.Add(new Border { Child = block, Background = System.Windows.Media.Brushes.White,
                BorderBrush = Ui.Brush("#CBD7E5"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5),
                Margin = new Thickness(0, 16, 0, 0), ClipToBounds = true });
        }
        foreach (var missingGroup in ProjectSharedData.MissingSoilLayers(section).GroupBy(m => (m.Survey, m.Layer)))
        {
            var (survey, layer) = missingGroup.Key;
            var content = new StackPanel();
            content.Children.Add(Ui.Text($"Conflitto {++conflictNumber}: Sondaggio {survey + 1} · Strato {layer + 1} assente", 16, true));
            foreach (var sourceSheet in missingGroup.Select(m => m.Source).Distinct())
            {
                var sourceRow = sourceSheet["dati"]!["stratigrafie"]![survey]![layer]!;
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 12) };
                var info = Ui.Stack(Ui.Text(sourceSheet.S("nome"), 14, true), Ui.Text(ProjectSharedData.Location(sourceSheet), 11, color: Ui.Muted),
                    Ui.Text("Tipo: " + (string.IsNullOrWhiteSpace(sourceRow.S("tipologia")) ? "Non compilato" : sourceRow.S("tipologia")) + " · Spessore: " + sourceRow.S("spessore") + " m", 13, color: Ui.Muted));
                string typeKey = $"Strato · {survey + 1}/{layer + 1}/tipologia";
                var chooseType = new ComboBox { ItemsSource = new[] { "Granulare", "Coesivo" }, SelectedItem = sourceRow.S("tipologia"),
                    Width = 180, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0), Tag = (sourceSheet, typeKey) };
                System.Windows.Automation.AutomationProperties.SetName(chooseType, "Tipologia del terreno · " + sourceSheet.S("nome"));
                info.Children.Add(chooseType);
                info.Width = 300; info.Margin = new Thickness(0, 0, 20, 0); row.Children.Add(info);
                var copy = Ui.Button("Uniforma a questo", () => Safe(() =>
                {
                    if (chooseType.SelectedItem is string selectedType) ProjectSharedData.SetSoilType(sourceSheet, typeKey, selectedType);
                    if (ProjectSharedData.CopyMissingSoilLayers(sourceSheet, sourceSheet.Parent!.Parent!.AsObject(), survey, layer) > 0)
                    {
                        MarkDirty();
                        if (currentSheet is not null) { var active = currentSheet; editor?.Dispose(); editor = null; ShowSheet(active); }
                        RefreshTree();
                    }
                    Render();
                }));
                copy.Tag = (sourceSheet, $"Strato mancante · {survey + 1}/{layer + 1}");
                copy.IsEnabled = chooseType.IsEnabled = ProjectSharedData.AncestorReferences(sourceSheet, typeKey).Length == 0;
                copy.VerticalAlignment = VerticalAlignment.Center;
                copy.ToolTip = "Aggiunge lo strato e gli eventuali strati precedenti mancanti, copiando i soli dati comuni. Gli strati già presenti restano invariati.";
                row.Children.Add(copy); content.Children.Add(row);
            }
            foreach (var target in missingGroup.Select(m => m.Target).Distinct())
                content.Children.Add(Ui.Text(target.S("nome") + ": strato assente", 13, color: Ui.Muted));
            content.Children.Add(Ui.Text("Uniformando vengono aggiunti anche gli eventuali strati precedenti mancanti, nello stesso ordine.", 12, color: Ui.Muted));
            panel.Children.Add(new Border { Child = content, Padding = new Thickness(16), Background = System.Windows.Media.Brushes.White,
                BorderBrush = Ui.Brush("#CBD7E5"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Margin = new Thickness(0, 16, 0, 0) });
        }
        var notices = new StackPanel();
        notices.Children.Add(Ui.Text("Avvisi", 17, true));
        foreach (string limitation in ProjectSharedData.Limitations(section))
            notices.Children.Add(Ui.Text("• " + limitation, 13, color: System.Windows.Media.Brushes.DarkOrange));
        foreach (var check in CoverChecks(section))
            notices.Children.Add(Ui.Text("• " + check.Text, 13, color: check.Passed == true ? System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.DarkOrange));
        if (notices.Children.Count == 1) notices.Children.Add(Ui.Text("Nessun avviso.", 13));
        panel.Children.Add(new Border { Child = notices, Background = Ui.Brush("#FFFBF2"), BorderBrush = Ui.Brush("#E7D6AE"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(16), Margin = new Thickness(0, 20, 0, 16) });
        var soilTargets = section.Array("fogli").OfType<JsonObject>().Where(ProjectSharedData.SupportsSharedSoils).Select(s => new SharedSheet(s)).ToArray();
        if (soilTargets.Length > 1)
        {
            var soilPanel = new StackPanel { Margin = new Thickness(12) };
            var source = new ComboBox { ItemsSource = soilTargets, SelectedIndex = 0, Margin = new Thickness(0, 6, 0, 10) };
            soilPanel.Children.Add(Ui.Text("Sondaggio di riferimento", 13, true));
            soilPanel.Children.Add(source);
            soilPanel.Children.Add(Ui.Text("Foglio da collegare", 13, true));
            var soilTarget = new ComboBox { ItemsSource = soilTargets, SelectedIndex = 1, Margin = new Thickness(0, 6, 0, 6) };
            soilPanel.Children.Add(soilTarget);
            soilPanel.Children.Add(Ui.Button("Mostra collegamento stratigrafie…", () => Safe(() =>
            {
                if (source.SelectedItem is not SharedSheet from || soilTarget.SelectedItem is not SharedSheet to || ReferenceEquals(from.Sheet, to.Sheet)) return;
                var stagedSource = (JsonObject)from.Sheet.DeepClone(); var stagedTarget = (JsonObject)to.Sheet.DeepClone();
                ProjectSharedData.LinkSoils(stagedSource, stagedTarget);
                var preview = Ui.Stack(Ui.Text(from + " → " + to, 17, true),
                    Ui.Text("Conferma che i sondaggi corrispondano, nello stesso ordine e con la stessa origine delle quote. Saranno collegati gli strati e copiati i parametri fisici indicati sotto. I parametri specifici del metodo restano nel destinatario.", 13),
                    Ui.Text(stagedTarget["dati"]!["stratigrafie"]!.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), 12));
                preview.Margin = new Thickness(18);
                var confirm = Ui.Dialog(dialog, "Collegamento dei sondaggi", new ScrollViewer { Content = preview, Background = Ui.Bg, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 760, 600);
                preview.Children.Add(Ui.Bar(Ui.Button("Conferma stesso sondaggio e collega", () => confirm.DialogResult = true, true), Ui.Button("Annulla", () => confirm.DialogResult = false)));
                if (confirm.ShowDialog() != true) return;
                from.Sheet["dati"] = stagedSource["dati"]!.DeepClone(); to.Sheet["dati"] = stagedTarget["dati"]!.DeepClone(); MarkDirty();
                if (currentSheet is not null) { var active = currentSheet; editor?.Dispose(); editor = null; ShowSheet(active); }
                dialog.Close(); ShowProjects();
            })));
            panel.Children.Add(new Expander { Header = "Collegamento stratigrafie", Content = soilPanel, IsExpanded = false });
        }
        }
        Render();
        dialog.ShowDialog();
        RefreshTree();
        if (ReferenceEquals(body.Content, dashboardViewport)) ShowProjects();
    }
}
