using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;

/// <summary>
/// Ricerca delle armature sui fogli con azioni unificate (analisi dei bug del 15/10/2026, ARM-2): percorso reale della UI,
/// SectionActionInputs.Replace e CalculationCopy (StructuralQueries.ConcreteReinforcement), poi ConcreteReinforcementDesign.Optimize.
/// Attesi indipendenti in tests/ConcreteLibraryAdapter.Checks/attesi/correzioni_20261015.py.
/// </summary>
internal static class DesignActionChecks
{
    internal static int Run()
    {
        int count = 0;
        void Check(bool value, string message) { if (!value) throw new Exception("Ricerca armature, azioni unificate: " + message); count++; }
        // Trave 400×600, copriferro 40, staffe Ø8/150; riga SLU C1 = (N 0; Mx 0; My 150; Vx 0; Vy 30; T 0) negli assi Personalizzati ruotati
        // di 90°, che la scheda N–M legge come flessione sull'asse forte: (0, 0, 150) diventa (0, −150, 0) negli assi locali.
        var data = SezioneCA.DefaultData(); var ws = SectionWorkspace.Prepare(data); var input = data["input"]!;
        input["shape"] = "Rettangolare"; input["cover_mm"] = "40"; input["width_mm"] = "400"; input["height_mm"] = "600";
        input["transverse_bar_diameter_mm"] = "8"; input["transverse_spacing_mm"] = "150";
        ws["dettagli_costruttivi"]!["elemento"] = "Trave"; ws["sle_comuni"]!["esposizione"] = "XC1"; ws["ancoraggi"]!["lunghezza"] = "2000";
        SectionActionInputs.Replace(data, [J.Obj(("id", "u1"), ("nome", "C1"), ("stato", "SLU"), ("N", "0"), ("Mx", "0"), ("My", "150"), ("Vx", "0"), ("Vy", "30"), ("T", "0"), ("elemento", ""))]);
        ws["dominio3d"]!["assi"] = "Personalizzati"; ws["dominio3d"]!["rotazione"] = "90";
        var copy = SectionActionInputs.CalculationCopy(data);
        Check(copy["workspace_ca"]!["taglio"]!.Array("azioni").Count == 1, "la copia di calcolo non porta la riga SLU fra le azioni di taglio");
        var options = new ConcreteDesignOptions { Diameters = [16], TopCounts = [3, 4], BottomCounts = [3, 4], SideCounts = [0, 2],
            CircularCounts = [8], StirrupDiameters = [8], StirrupSpacings = [150], MaxEvaluations = 100000, MaxParallelism = 1 };
        var result = ConcreteReinforcementDesign.Optimize(copy, options);
        Check(result.Candidates.Count > 0, "nessuna proposta: " + string.Join("; ", result.Exclusions.Keys));
        // Ogni riga SLU è verificata a N–Mx–My una sola volta, negli assi di dominio3d: nessuna copia «Taglio · C1» in assi locali.
        Check(result.Candidates.All(c => !c.Checks.Any(x => x.Combination.StartsWith("Taglio · ")) && c.Checks.Count(x => x.Key == "slu") == 1),
            "riga SLU verificata due volte (copia di taglio in assi locali)");
        // Sup. 4Ø16, inf. 3Ø16, senza barre laterali: esclusa prima del 15/10 dalla sola copia in assi locali (η 1,575); negli assi del foglio
        // MRd = 165,124 kNm (fibre, NTC 4.1.2.3.4, correzioni_20261015.py), η = 150/165,124 = 0,9084. Scarto ammesso 0,5 % per la
        // discretizzazione della sezione (poligono e mesh della libreria contro le fibre del riferimento).
        var lean = result.Candidates.SingleOrDefault(c => c.Input.D("top_bar_count") == 4 && c.Input.D("bottom_bar_count") == 3 && new SezioneCA(c.Input).Bars.Count == 7);
        Check(lean is not null, "proposta sup. 4Ø16 / inf. 3Ø16 senza barre laterali esclusa");
        double eta = lean!.Checks.Single(x => x.Key == "slu").Ratio!.Value;
        Check(Math.Abs(eta - 150 / 165.124297) <= 5e-3 * eta, $"η N–Mx–My negli assi del foglio {eta:G6} invece di 0,9084");
        Check(result.Candidates[0].Metrics.KgPerM <= lean.Metrics.KgPerM + 1e-12, "proposta più leggera non in testa");
        return count;
    }
}
