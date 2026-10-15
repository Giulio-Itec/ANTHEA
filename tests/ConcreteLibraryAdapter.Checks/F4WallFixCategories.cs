using System.Text.Json.Nodes;
using Anthea.Calculations;

/// <summary>
/// Differenze intenzionali classificate dei muri (analisi dei bug del 15/10/2026, integrazione del 16/10/2026), applicate solo al risultato
/// attuale prima del confronto con i riferimenti (congelati o del motore Legacy), che non vengono mai riscritti. Ogni categoria conta i casi
/// e controlla la propria regola; ciò che resta deve coincidere con il riferimento.
/// <list type="bullet">
/// <item>MUR-1: le gemelle «…b» (VariableSurchargeOnHeel falso: SLU, sismiche ed eccezionali) seguono la propria riga con la stessa spinta motrice;
/// sono tolte dai casi, dalle verifiche e dalle righe del documento e verificate a parte (RetainingWall.Checks).</item>
/// <item>MUR-1/MUR-2 campi nuovi: «heel_surcharge» e «rpassive» nelle righe; VariableSurchargeOnHeel, PassiveFactor, SlidingDemand,
/// OverturningDemand nei casi; «passiva_fattore» e «passiva_progetto_kN_m» nell'audit. Senza passiva a valle (tutto il corpus F4) Ed di scorrimento
/// e ribaltamento coincidono con H e Mrib, γR,e vale 1,4 nelle SLU e 1 altrove e la passiva di progetto è nulla.</item>
/// <item>Testi MUR-1/MUR-2/MUR-3: metodo, aiuto del terreno, approccio e nota della passiva e dei diagrammi del fusto (testi storici qui sotto,
/// copiati dal commit dcc2ed44).</item>
/// </list>
/// </summary>
internal static class F4WallFixCategories
{
    internal sealed class Counts
    {
        public int Mur1Twins, Mur1Checks, Mur1Rows, Mur2Fields, Texts;
        public object Json() => new { MUR1_gemelle = Mur1Twins, MUR1_verifiche = Mur1Checks, MUR1_righe = Mur1Rows, MUR2_campi = Mur2Fields, testi_MUR1_2_3 = Texts };
        public string Line() => $"MUR-1 twins {Mur1Twins} (checks {Mur1Checks}, rows {Mur1Rows}), MUR-1/2 fields {Mur2Fields}, MUR-1/2/3 texts {Texts}";
    }

    static readonly (string Old, string New)[] TextPairs =
    [
        (OLD_METHOD, RetainingWall.Method),
        (OLD_SOIL, RetainingWall.SoilHelp),
        (OLD_APPROACH, RetainingWall.ApproachHelp),
        (OLD_NOTE, RetainingWall.EquilibriumNote),
    ];

    /// <summary>The current result of a wall (serialized) with the classified differences removed or restored to their historical form.</summary>
    internal static JsonNode Classify(JsonNode actual, Counts counts, List<string> errors, string id)
    {
        var a = actual.DeepClone().AsObject();
        if (a["Cases"] is not JsonArray cases) return a;
        // MUR-1: twins after their main row, same thrust.
        var twins = new HashSet<string>();
        for (int i = 0; i < cases.Count; i++)
        {
            var c = cases[i]!.AsObject();
            if (c["VariableSurchargeOnHeel"]?.GetValue<bool>() != false) continue;
            string name = c.S("Name"), main = name.EndsWith('b') ? name[..^1] : "";
            var previous = i > 0 ? cases[i - 1]!.AsObject() : null;
            if (c.S("State") is not ("SLU" or "SISMA" or "ECCEZIONALE") || previous is null || previous.S("Name") != main || previous["VariableSurchargeOnHeel"]?.GetValue<bool>() != true
                || Math.Abs(previous.D("SlidingDemand") - c.D("SlidingDemand")) > 1e-9 * Math.Max(1, Math.Abs(c.D("SlidingDemand"))))
                errors.Add(id + ": MUR-1 twin " + name + " without its main row or with another thrust");
            twins.Add(name); counts.Mur1Twins++;
        }
        foreach (var c in cases.OfType<JsonObject>().Where(c => twins.Contains(c.S("Name"))).ToArray()) cases.Remove(c);
        foreach (string key in new[] { "Checks", "Structural" })
            if (a[key] is JsonArray list)
                foreach (var check in list.OfType<JsonObject>().Where(x => twins.Contains(x.S("Combination"))).ToArray()) { list.Remove(check); counts.Mur1Checks++; }
        void RowFields(JsonObject row)
        {
            if (row["heel_surcharge"] is { } heel) { if (heel.GetValue<bool>() != true) errors.Add(id + ": MUR-1 row " + row.S("name") + " without the surcharge on the heel"); row.Remove("heel_surcharge"); counts.Mur2Fields++; }
            if (row["rpassive"] is { } passive) { if (passive.GetValue<double>() != (row.S("state") == "SLU" ? 1.4 : 1)) errors.Add(id + ": MUR-2 γR,e of " + row.S("name")); row.Remove("rpassive"); counts.Mur2Fields++; }
        }
        if (a["Input"]?["combinations"] is JsonArray rows)
        {
            foreach (var row in rows.OfType<JsonObject>().Where(r => twins.Contains(r.S("name"))).ToArray()) { rows.Remove(row); counts.Mur1Rows++; }
            foreach (var row in rows.OfType<JsonObject>()) RowFields(row);
        }
        foreach (var c in cases.OfType<JsonObject>()) if (c["Factors"] is JsonObject factors) RowFields(factors);
        // MUR-1/MUR-2 fields of the cases: without the passive in front the demands of the checks are the equilibrium values.
        foreach (var c in cases.OfType<JsonObject>())
        {
            string name = c.S("Name");
            void Same(string field, double expected)
            {
                if (c[field] is null) { errors.Add(id + "/" + name + ": " + field + " missing"); return; }
                if (Math.Abs(c.D(field) - expected) > 1e-9 * Math.Max(1, Math.Abs(expected))) errors.Add(id + "/" + name + ": " + field + " " + c.D(field) + " != " + expected);
                c.Remove(field); counts.Mur2Fields++;
            }
            if (c["VariableSurchargeOnHeel"] is { } flag) { if (flag.GetValue<bool>() != true) errors.Add(id + "/" + name + ": twin left"); c.Remove("VariableSurchargeOnHeel"); counts.Mur2Fields++; }
            Same("PassiveFactor", c.S("State") == "SLU" ? 1.4 : 1);
            Same("SlidingDemand", c.D("Horizontal"));
            Same("OverturningDemand", c.D("Overturning"));
            if (c["SoilAudit"] is JsonObject audit)
            {
                foreach (var (key, expected) in new[] { ("passiva_fattore", c.S("State") == "SLU" ? 1.4 : 1), ("passiva_progetto_kN_m", 0d) })
                {
                    if (audit[key] is null) { errors.Add(id + "/" + name + ": " + key + " missing"); continue; }
                    if (Math.Abs(audit.D(key) - expected) > 1e-12) errors.Add(id + "/" + name + ": " + key);
                    audit.Remove(key); counts.Mur2Fields++;
                }
            }
        }
        // Texts of the method, of the soils, of the approach and of the passive and stem diagrams.
        if (a["Notes"] is JsonArray notes)
            for (int i = 0; i < notes.Count; i++)
                foreach (var (old, current) in TextPairs)
                    if (notes[i]?.GetValue<string>() == current) { notes[i] = old; counts.Texts++; break; }
        return a;
    }

    const string OLD_METHOD = "Statica: Rankine per strati (K₀ nel modello Wood), pressioni efficaci e acqua separate. Preset SLU A1+M1+R3: inviluppo γG,muro e γG,terra = 1 / 1,3; γQ = 0 / 1,5; γG,acqua = 1 / 1,3, correlato a spinta e sottospinta. γR scorrimento 1,1, ribaltamento 1,15, portanza 1,4. Portanza: EN 1997-1, allegato D, fondazione nastriforme c′=0, q′ di ricoprimento da valle, Nγ=2(Nq−1)tanφ, iγ=(1−H/V)³; base ruvida δ≥φ/2. Contatto elastico senza trazione; B′=B−2|e|. Oltre |e|=B/3 la portanza è fuori campo. Passiva Rankine opzionale e parzializzabile, esclusa nel sisma.";
    const string OLD_SOIL = "Due colonne con profondità dalla rispettiva superficie. Hlib è misurata dalla sommità al terreno di valle; Dv=H+t−Hlib dal piano di posa. Collegare le colonne copia spessori e materiali, non le quote assolute. Peso del terreno sopra la mensola e ricoprimento efficace nella portanza sempre inclusi. Passiva Rankine attivabile con frazione mobilitata 0–1; esclusa nei casi sismici. Il terreno di posa resta un input distinto.";
    const string OLD_APPROACH = "A modifica le azioni; M riduce i parametri del terreno (tan φd = tan φk / γMφ); R divide le resistenze. Per i muri NTC 2018 §6.5.3.1.1 il preset locale è Approccio 2: A1+M1+R3, con γR = 1,10 scorrimento, 1,15 ribaltamento, 1,40 portanza. A1+M1+R1 appartiene alla combinazione 1 dell’Approccio 1 e non sostituisce la verifica A2+M2+R2. La stabilità globale A2+M2+R2 richiede un’analisi dedicata. Nella matrice ogni riga espone i fattori realmente usati: modificarli produce una combinazione personalizzata, non una certificazione dell’approccio.";
    const string OLD_NOTE = "Passiva applicata al piano verticale esterno a valle e limitata alla spinta motrice; nessuna maggiorazione favorevole γG>1. Diagrammi del fusto: equilibrio della sezione con terreno sul paramento inclinato e attrito di monte. Ricoprimento q′ di valle usato con iq=(1−|H|/V)²; nessuna maggiorazione di profondità.";
}
