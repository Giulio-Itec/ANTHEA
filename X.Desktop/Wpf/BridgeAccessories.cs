using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    private readonly ContentControl shearResults = new(), studResults = new();
    private UIElement BuildAccessories()
    {
        var transverseInputs = BuildTransverseInputs(); var fatigueInputs = BuildFatigueInputs();
        InputForm? studs = null;
        studs = Form(Data, [new("pioli", "Verifica pioli a testa saldata", Bool: true), new("n_pioli", "Pioli per fila trasversale"),
            new("d_pioli", "Diametro gambo d", "mm"), new("h_pioli", "Altezza dopo saldatura", "mm"),
            new("passo_pioli", "Passo longitudinale", "mm"), new("passo_trasv_pioli", "Passo trasversale", "mm"),
            new("fu_pioli", "Resistenza ultima fu", "MPa"), new("d_testa_pioli", "Diametro testa", "mm"), new("t_testa_pioli", "Spessore testa", "mm"),
            new("copriferro_pioli", "Copriferro minimo di progetto", "mm"), new("pioli_fatica", "Dettagli per azioni ripetute / fatica", Bool: true)], _ =>
            { UpdateStuds(); Dispatcher.BeginInvoke(BuildPhases); });
        void UpdateStuds()
        {
            transverseInputs.Visibility = fatigueInputs.Visibility = Data.B("pioli") ? Visibility.Visible : Visibility.Collapsed;
            foreach (string key in new[] { "n_pioli", "d_pioli", "h_pioli", "passo_pioli", "passo_trasv_pioli", "fu_pioli", "d_testa_pioli", "t_testa_pioli", "copriferro_pioli", "pioli_fatica" })
                studs?.ShowField(key, Data.B("pioli") && (key != "passo_trasv_pioli" || Data.D("n_pioli") > 1));
        }
        UpdateStuds();
        return Ui.Stack(BuildStiffenerInputs(), BuildSupportInputs(), Group("Connessione a pioli", Ui.Stack(studs,
            Ui.Text("File uniformi e centrate sulla piattabanda. Soletta piena, pioli Ø16–25 mm. q = Σ(V·S/I + Δq); gli effetti locali richiedono Δq assegnato per fase.", 11, color: Ui.Muted),
            transverseInputs, fatigueInputs)));
    }

    private bool PhaseHomogenizationNeeded(string kind) => BridgeSection.HasConcrete(kind)
        || kind == "Soletta esclusa" && Data.B("pioli") && !Data.S("normativa").StartsWith("NTC");
    private void ShowAccessoryResults(BridgeStage stage)
    {
        shearResults.Content = Ui.Text("Taglio e irrigidimenti non verificati nel metodo selezionato.", 12, color: Ui.Muted);
        studResults.Content = Ui.Text("Connessione a pioli non verificata nel metodo selezionato.", 12, color: Ui.Muted);
        if (stage.Shear is { } s)
        {
            var w = s.Web;
            shearResults.Content = ResultTable(["Parametro", "Valore", "Unità"], new[] {
                new[] { "V cumulato", F(s.V), "kN" }, new[] { "kτ", F(w.KTau), "—" }, new[] { "τcr", F(w.TauCritical), "MPa" },
                new[] { "λw", F(w.Slenderness), "—" }, new[] { "χw", F(w.Chi), "—" }, new[] { "Vpl,Rd", F(w.PlasticResistance / 1000), "kN" },
                new[] { "Vbw,Rd", F(w.BucklingResistance / 1000), "kN" }, new[] { "VRd adottato", F(w.Resistance / 1000), "kN" },
                new[] { "τ nominale V/Av / τ max elastica lorda", F(s.TauAverage) + " / " + F(s.TauMaximum), "MPa" },
                new[] { "Beneficio irrigidimenti", s.UsesStiffeners ? "Sì" : "No", "—" }, new[] { "Montante terminale adottato", s.RigidEndPost ? "Rigido verificato" : "Non rigido", "—" } }.Concat(s.Stiffener is { } st ? new[] {
                    new[] { "Irrigidimento · area con anima", F(st.Area), "mm²" }, new[] { "Irrigidimento · inerzia fuori piano", E(st.Inertia), "mm⁴" },
                    new[] { "Irrigidimento · Nst", F(st.AxialForce / 1000), "kN" }, new[] { "Irrigidimento · λ / χ", F(st.Lambda) + " / " + F(st.Chi), "—" },
                    new[] { "Irrigidimento · imperfezione iniziale", F(st.InitialDeflection), "mm" } } : Array.Empty<string[]>()).Concat((s.Details ?? []).Select(x => new[] { x.Name, F(x.Value), x.Unit })));
            summaryCards.AddCheck("Taglio e irrigidimenti", s.Checks.Count, s.Checks.Select(c => (c.Name, c.Ratio, c.Ratio is { } r ? (bool?)(r <= 1) : null)));
        }
        if (stage.Studs is { } p)
        {
            studResults.Content = Ui.Stack(Ui.Text(p.Enabled
                ? $"q = {F(p.Flow)} kN/m · PEd = {F(p.ForcePerStud)} kN/piolo · PRd adottato = {F(p.ResistancePerStud)} kN/piolo · qRd = {F(p.ResistancePerLength)} kN/m"
                : p.Contributions.All(c => c.Kind == "Solo acciaio") ? "Connessione non attiva nella situazione di solo acciaio." : "Verifica pioli disattivata; flussi riportati per consultazione.", 12),
                p.Resistance is { } r ? Ui.Text($"α = {F(r.Alpha)} · fu adottato = {F(r.UsedFu)} MPa · PRd acciaio = {F(r.SteelResistance / 1000)} kN · PRd CLS = {F(r.ConcreteResistance / 1000)} kN", 11, color: Ui.Muted) : Ui.Text(""),
                ResultTable(["Fase", "V [kN]", "S* [mm³]", "I* [mm⁴]", "q(V) [kN/m]", "Δq [kN/m]", "Proprietà adottate"],
                    p.Contributions.Select(c => new[] { c.Phase, F(c.V), E(c.StaticMoment), E(c.Inertia), F(c.Flow), F(c.AdditionalFlow), c.Basis })),
                p.Details is { Count: > 0 } details ? ResultTable(["Parametro", "Valore", "Unità"], details.Select(x => new[] { x.Name, F(x.Value), x.Unit })) : Ui.Text(""));
            if (p.Enabled) summaryCards.AddCheck("Pioli · controlli locali", p.Checks.Count, p.Checks.Select(c => (c.Name, c.Ratio, c.Ratio is { } r ? (bool?)(r <= 1) : null)));
        }
    }
}
