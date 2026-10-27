using System.Collections.Immutable;
using System.Windows;
using System.Windows.Media;

namespace X.Desktop.Themes;

/// <summary>Roles of the palette of <see cref="Appearance"/>: the transformation of a colour in Scuro and Molto scuro depends on them.</summary>
internal static class ThemeRole
{
    internal const string Background = "background", Foreground = "foreground", Border = "border", Selected = "selected", Selection = "selection",
        Calculated = "calculated", Series = "series", Disabled = "disabled", Swatch = "swatch";
}

/// <summary>A semantic colour of the interface: its colour in Chiara, the role of the palette that gives the other appearances,
/// the use, and the resources or colours of the historic code it corresponds to (proposal F5, section 6.2).</summary>
internal sealed record ThemeToken(string Name, string Light, string Role, string Use, string Historic)
{
    /// <summary>Key of the token in the application resources: the new views use only <c>{DynamicResource Token.…}</c>.</summary>
    internal string Key => ThemeTokens.Prefix + Name;
    internal Color Color => (Color)ColorConverter.ConvertFromString(Light);
    /// <summary>The palette brush of <see cref="Appearance"/>: the same object as <c>Appearance.Colour(colore, ruolo)</c>, with the same
    /// transformation and the same update when the appearance changes.</summary>
    internal Brush Brush => Appearance.Colour(Color, Role);
}

/// <summary>
/// Catalogue of the theme tokens (refactoring F5.3, proposal F5 section 6; decision Q6 at the F5.3 checkpoint, recommendation c).
/// Only the new views (src/ANTHEA.Desktop/Views, src/ANTHEA.Desktop/Shell) use the tokens, through <c>{DynamicResource Token.&lt;nome&gt;}</c> (rule P7 of
/// ANTHEA.Architecture.Tests); the historic views keep their colours and resources. Every token is registered in the application
/// resources with the palette brush of <see cref="Appearance"/>, so that Scuro and Molto scuro stay identical by construction: the check
/// --check-theme-tokens compares, for every token and the three appearances, the colour with the transformation of the palette.
/// The groups Drawing, Swatch and Utilization have the identity role (swatch): technical drawings and identification colours stay
/// the same in every appearance. The typographic tokens Font.* are in Themes/Typography.xaml. List, census and near variants:
/// docs/refactoring/f5-temi-token.md.
/// </summary>
internal static class ThemeTokens
{
    internal const string Prefix = "Token.";

    internal static ImmutableArray<ThemeToken> Catalogue { get; } =
    [
        // Surfaces.
        new("Surface", "#F3F5F8", ThemeRole.Background, "fondo delle pagine e delle finestre", "AppearanceSurface, Appearance.background.F3F5F8, Ui.Bg"),
        new("Paper", "#FFFFFF", ThemeRole.Background, "schede, pannelli, pulsanti", "AppearancePaper, Appearance.background.FFFFFF, Appearance.Paper"),
        new("PaperAlt", "#F8FAFC", ThemeRole.Background, "righe alterne delle tabelle", "Appearance.background.F8FAFC (RowBackground delle DataGrid)"),
        new("Field.Background", "#F8FAFC", ThemeRole.Background, "campi modificabili", "Appearance.background.F8FAFC, #F8FAFC di InputForm"),
        new("Header", "#E8EFF7", ThemeRole.Background, "intestazioni delle griglie", "Appearance.background.E8EFF7"),
        // Text.
        new("Ink", "#0B2A4A", ThemeRole.Foreground, "testo", "AppearanceInk, Appearance.foreground.0B2A4A, Appearance.Ink, Ui.Navy"),
        new("InkSecondary", "#234767", ThemeRole.Foreground, "frecce e glifi dell'albero", "Appearance.foreground.234767"),
        new("Muted", "#5B697E", ThemeRole.Foreground, "testo secondario, unità di misura (5,1:1 sulla Surface; #64748B fino al 27/10/2026)", "Ui.Muted e #64748B in primo piano; ex Appearance.foreground.64748B di FieldForm (F5.2)"),
        new("Accent", "#0B5CAD", ThemeRole.Foreground, "collegamenti, enfasi", "Appearance.Accent, Ui.Blue"),
        new("Disabled", "#767676", ThemeRole.Disabled, "testo disattivato o valore automatico in grigio (4,5:1 sul bianco; #808080 fino al 27/10/2026)", "Appearance.Dim(Brushes.Gray)"),
        // Lines.
        new("Border", "#DCE2E9", ThemeRole.Border, "bordi dei controlli", "Appearance.border.DCE2E9"),
        new("Border.Strong", "#CAD7E5", ThemeRole.Border, "bordi dei pulsanti di progetto", "Appearance.border.CAD7E5"),
        new("Border.Default", "#D3D3D3", ThemeRole.Border, "bordi di sistema (schede, menu, suggerimenti)", "AppearanceBorder (LightGray)"),
        new("Divider", "#E4EAF1", ThemeRole.Border, "righe delle griglie", "Appearance.border.E4EAF1"),
        new("TreeGuide", "#DCE5EE", ThemeRole.Border, "guide dell'albero", "Appearance.border.DCE5EE"),
        new("Focus", "#3986CB", ThemeRole.Border, "fuoco da tastiera", "Appearance.border.3986CB"),
        new("Selection.Border", "#2582D2", ThemeRole.Border, "riga selezionata dell'albero", "Appearance.border.2582D2"),
        // Commands and states.
        new("CommandBar", "#0B2A4A", ThemeRole.Background, "barre dei comandi Navy", "Appearance.background.0B2A4A"),
        new("CommandBar.Hover", "#244665", ThemeRole.Background, "passaggio del mouse sulle barre", "Appearance.background.244665"),
        new("OnCommandBar", "#FFFFFF", ThemeRole.Foreground, "testo sulle barre", "Appearance.foreground.FFFFFF"),
        new("OnCommandBar.Muted", "#B9C8D8", ThemeRole.Foreground, "testo secondario sulle barre", "#B9C8D8 (primo piano)"),
        new("Hover", "#E3EFFA", ThemeRole.Background, "passaggio del mouse su pulsanti e menu", "Appearance.background.E3EFFA"),
        new("Hover.Row", "#F0F6FC", ThemeRole.Background, "passaggio del mouse sulle righe dell'albero", "Appearance.background.F0F6FC"),
        new("Selected", "#0B2A4A", ThemeRole.Selected, "pulsante primario, scelta attiva", "Appearance.Selected"),
        new("Selection", "#E2EFFC", ThemeRole.Selection, "riga e scheda selezionate", "AppearanceSelection, Appearance.selection.E2EFFC"),
        new("Calculated", "#EAF2FA", ThemeRole.Calculated, "campi calcolati o collegati", "Appearance.Calculated; ex Appearance.calculated.EAF2FA di FieldForm (F5.2)"),
        // Notes, warnings and outcomes.
        new("Note.Background", "#FFF6DD", ThemeRole.Background, "fondo delle note dei campi", "Appearance.Background(\"#FFF6DD\") di InputForm; ex Appearance.background.FFF6DD di FieldForm (F5.2)"),
        new("Note.Foreground", "#865D16", ThemeRole.Foreground, "testo delle note dei campi", "#865D16 di InputForm; ex Appearance.foreground.865D16 di FieldForm (F5.2)"),
        new("Note.Border", "#EEDCAF", ThemeRole.Border, "bordo delle note dei campi", "#EEDCAF di InputForm; ex Appearance.border.EEDCAF di FieldForm (F5.2)"),
        new("Warning.Background", "#FFFAEB", ThemeRole.Background, "fondo degli avvisi", "Appearance.Background(\"#FFFAEB\")"),
        new("Warning.Foreground", "#9A4D0A", ThemeRole.Foreground, "testo degli avvisi e degli errori dei campi", "Appearance.foreground.9A4D0A"),
        new("Status.Ok", "#006400", ThemeRole.Foreground, "esito soddisfatto", "Brushes.DarkGreen (vicino: ForestGreen)"),
        new("Status.Fail", "#B22222", ThemeRole.Foreground, "esito non soddisfatto", "Brushes.Firebrick"),
        new("Status.Warn", "#FF8C00", ThemeRole.Foreground, "esito con riserva, avviso", "Brushes.DarkOrange"),
        // F5.5: the two colours of the status of the steel sheet, tints of their own (ΔE from Status.Ok 27.8, from Warning.Foreground 17.5).
        new("Status.Coherent", "#18794E", ThemeRole.Foreground, "esito coerente di una scheda materiale", "Appearance.Foreground(Ui.Brush(\"#18794E\")) di RebarMaterialView"),
        new("Status.Caution", "#9A6700", ThemeRole.Foreground, "valori da verificare o dati incompleti di una scheda materiale", "Appearance.Foreground(Ui.Brush(\"#9A6700\")) di RebarMaterialView"),
        new("Badge.Ok", "#EAF5ED", ThemeRole.Background, "badge di coerenza", "Appearance.Background(\"#EAF5ED\")"),
        new("Badge.Warn", "#FFF0D8", ThemeRole.Background, "badge di coerenza con avviso", "Appearance.Background(\"#FFF0D8\")"),
        // Series: curves and legends (the hue identifies the curve; lightened in the dark appearances).
        new("Series.Blue", "#0B5CAD", ThemeRole.Series, "serie principale", "Appearance.Series(Ui.Blue)"),
        new("Series.Steel", "#4682B4", ThemeRole.Series, "falda, serie secondaria", "Appearance.Series(Brushes.SteelBlue)"),
        new("Series.Orange", "#FF8C00", ThemeRole.Series, "contorni e limiti", "Appearance.Series(Brushes.DarkOrange)"),
        new("Series.Red", "#BF5545", ThemeRole.Series, "asse Mx, serie", "Appearance.Series(Ui.Brush(\"#BF5545\"))"),
        new("Series.Green", "#218463", ThemeRole.Series, "asse N, serie", "Appearance.Series(Ui.Brush(\"#218463\"))"),
        new("Series.Amber", "#B45309", ThemeRole.Series, "limiti analitici", "Appearance.Series(Ui.Brush(\"#B45309\"))"),
        new("Series.Magenta", "#C02672", ThemeRole.Series, "letture puntuali", "Appearance.Series(Ui.Brush(\"#C02672\"))"),
        // Technical drawings: white background and the same colours in every appearance (accepted exception, identity role).
        new("Drawing.Paper", "#FFFFFF", ThemeRole.Swatch, "fondo dei disegni tecnici", "Brushes.White nei disegni OnRender"),
        new("Drawing.Canvas", "#F8FAFD", ThemeRole.Swatch, "fondo delle viste tecniche (sezioni, ponte)", "Ui.Brush(\"#F8FAFD\") nei disegni"),
        new("Drawing.Ink", "#0B2A4A", ThemeRole.Swatch, "linee e testi principali dei disegni", "Ui.Navy nei disegni"),
        new("Drawing.Muted", "#64748B", ThemeRole.Swatch, "quote e testi secondari dei disegni", "Ui.Muted nei disegni"),
        new("Drawing.Accent", "#0B5CAD", ThemeRole.Swatch, "elementi evidenziati dei disegni", "Ui.Blue nei disegni"),
        new("Drawing.Axis", "#94A3B8", ThemeRole.Swatch, "assi e limiti tratteggiati", "Ui.Brush(\"#94A3B8\") nei disegni"),
        new("Drawing.Grid", "#E8EDF3", ThemeRole.Swatch, "griglia dei grafici", "Ui.Brush(\"#E8EDF3\") nei disegni"),
        new("Drawing.Guide", "#CBD5E1", ThemeRole.Swatch, "linee guida e bordi delle aree dei disegni", "Ui.Brush(\"#CBD5E1\") nei disegni"),
        // Identification colours: soil layers of the drawings and of the wall, the same in every appearance.
        new("Swatch.Soil1", "#F4C95D", ThemeRole.Swatch, "strato 1 nei disegni dei terreni", "Drawings.LayerColors[0]"),
        new("Swatch.Soil2", "#DFA06E", ThemeRole.Swatch, "strato 2 nei disegni dei terreni", "Drawings.LayerColors[1]"),
        new("Swatch.Soil3", "#A8C686", ThemeRole.Swatch, "strato 3 nei disegni dei terreni", "Drawings.LayerColors[2]"),
        new("Swatch.Soil4", "#8FB8DE", ThemeRole.Swatch, "strato 4 nei disegni dei terreni", "Drawings.LayerColors[3]"),
        new("Swatch.Soil5", "#C6A0D5", ThemeRole.Swatch, "strato 5 nei disegni dei terreni", "Drawings.LayerColors[4]"),
        new("Swatch.Soil6", "#C9B79C", ThemeRole.Swatch, "strato 6 nei disegni dei terreni", "Drawings.LayerColors[5]"),
        new("Swatch.Wall1", "#DBCDAE", ThemeRole.Swatch, "strato 1 del muro (tabella e disegno)", "Appearance.Swatch(RetainingWallDrawing.LayerColors[0])"),
        new("Swatch.Wall2", "#B9D6C6", ThemeRole.Swatch, "strato 2 del muro (tabella e disegno)", "Appearance.Swatch(RetainingWallDrawing.LayerColors[1])"),
        new("Swatch.Wall3", "#D2C2DC", ThemeRole.Swatch, "strato 3 del muro (tabella e disegno)", "Appearance.Swatch(RetainingWallDrawing.LayerColors[2])"),
        new("Swatch.Wall4", "#D9C3A7", ThemeRole.Swatch, "strato 4 del muro (tabella e disegno)", "Appearance.Swatch(RetainingWallDrawing.LayerColors[3])"),
        new("Swatch.Wall5", "#B9CFDC", ThemeRole.Swatch, "strato 5 del muro (tabella e disegno)", "Appearance.Swatch(RetainingWallDrawing.LayerColors[4])"),
        new("Swatch.Wall6", "#DAD6A8", ThemeRole.Swatch, "strato 6 del muro (tabella e disegno)", "Appearance.Swatch(RetainingWallDrawing.LayerColors[5])"),
        // Utilization ratio η (UtilizationColors of ANTHEA.Application), as in the drawings and the verification cards.
        new("Utilization.None", "#9AA4AF", ThemeRole.Swatch, "η assente o non finito", "UtilizationColors.Color(null)"),
        new("Utilization.Low", "#397BCC", ThemeRole.Swatch, "η ≤ 0,50", "UtilizationColors.Color(≤ 0,50)"),
        new("Utilization.Fair", "#39A879", ThemeRole.Swatch, "0,50 < η ≤ 0,70", "UtilizationColors.Color(≤ 0,70)"),
        new("Utilization.High", "#E4C441", ThemeRole.Swatch, "0,70 < η ≤ 0,90", "UtilizationColors.Color(≤ 0,90)"),
        new("Utilization.Critical", "#EF8C32", ThemeRole.Swatch, "0,90 < η ≤ 1,00", "UtilizationColors.Color(≤ 1,00)"),
        new("Utilization.Exceeded", "#CB3939", ThemeRole.Swatch, "η > 1,00", "UtilizationColors.Color(> 1,00)"),
        // F5.8: the cards of the segments of the reinforcement of the elastic pile (PileReinforcementEditor.SegmentColor), backgrounds of texts and
        // fields. Role background, as the historic Appearance.ApplyTree painted the local background of the cards in Scuro and Molto scuro; a group of
        // its own (Segment.), outside the semantic set, as Series., Drawing., Swatch. and Utilization.
        new("Segment.1", "#DBEAFE", ThemeRole.Background, "scheda del tratto 1, 7… dell’armatura del palo", "PileReinforcementEditor.SegmentColor(0)"),
        new("Segment.2", "#DCFCE7", ThemeRole.Background, "scheda del tratto 2, 8… dell’armatura del palo", "PileReinforcementEditor.SegmentColor(1)"),
        new("Segment.3", "#F3E8FF", ThemeRole.Background, "scheda del tratto 3, 9… dell’armatura del palo", "PileReinforcementEditor.SegmentColor(2)"),
        new("Segment.4", "#FFEDD5", ThemeRole.Background, "scheda del tratto 4, 10… dell’armatura del palo", "PileReinforcementEditor.SegmentColor(3)"),
        new("Segment.5", "#CFFAFE", ThemeRole.Background, "scheda del tratto 5, 11… dell’armatura del palo", "PileReinforcementEditor.SegmentColor(4)"),
        new("Segment.6", "#FCE7F3", ThemeRole.Background, "scheda del tratto 6, 12… dell’armatura del palo", "PileReinforcementEditor.SegmentColor(5)"),
    ];

    /// <summary>The token with this name (without the prefix).</summary>
    internal static ThemeToken Get(string name) => Catalogue.FirstOrDefault(t => t.Name == name)
        ?? throw new KeyNotFoundException("Token di tema sconosciuto: " + name);

    /// <summary>Palette brush of a token, for code that cannot use a DynamicResource (the technical drawings of the new views).</summary>
    internal static Brush Brush(string name) => Get(name).Brush;

    /// <summary>Registers every token in the application resources (called by <see cref="AppearanceResources.Register"/>).</summary>
    internal static void Register(ResourceDictionary resources)
    {
        foreach (var token in Catalogue) resources[token.Key] = token.Brush;
    }
}
