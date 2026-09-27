using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class BridgeConcept
{
    private static Result CalculateAdvanced(JsonObject data)
    {
        var i = data["input"]!; var a = data["assumptions"]!; var rates = data["rates"]!;
        var family = Families.Single(f => f.Id == i.S("family")); string id = family.Id;
        bool towers = HasTowers(id), upper = HasUpperStructure(id), ortho = id == "orthotropic" || upper && i.S("deck_type") == DeckTypes[1];
        double length = i.D("length"), height = i.D("height");
        double width = i.D("lanes") * i.D("lane_width") + 2 * i.D("shoulder") + i.D("median") + 2 * i.D("barrier_width");
        int count = (int)i.D("spans");
        if (towers && count is not 0 and not 3) throw new ArgumentException("Strallato e sospeso: schema simmetrico a 3 campate (riva / principale / riva). Impostare 3 oppure 0.");
        if (id is "tied_arch" or "truss" && i.B("continuous")) throw new ArgumentException("Arco con catena e reticolare: il modello usa campate indipendenti; disattivare Impalcato continuo.");
        if (towers && !i.B("continuous")) throw new ArgumentException("Strallato e sospeso: il modello richiede l’impalcato continuo nello schema a tre campate.");
        if (towers && (i.B("start_pier") || i.B("end_pier"))) throw new ArgumentException("Strallato e sospeso: estremi su spalla / ancoraggio, non su pila di prosecuzione.");
        if (count == 0) count = towers ? 3 : Math.Clamp((int)Math.Ceiling(length / ((family.MinSpan + family.MaxSpan) / 2)), 1, 30);
        double[] weights = towers ? [.5, 1, .5] : Enumerable.Range(0, count).Select(k => !upper && i.B("continuous") && count > 2 && (k == 0 || k == count - 1) ? .8 : 1d).ToArray();
        double[] positions = new double[count + 1];
        for (int k = 0; k < count; k++) positions[k + 1] = positions[k] + length * weights[k] / weights.Sum();
        positions[^1] = length; var warnings = new List<string>();
        if (i.S("obstacle") != "Nessuno" && i.D("obstacle_width") > 0)
        {
            double left = (length - i.D("obstacle_width")) / 2 - 1, right = length - left;
            if (!towers)
            {
                var proposed = (double[])positions.Clone();
                for (int k = 1; k < count; k++) if (proposed[k] > left && proposed[k] < right) proposed[k] = proposed[k] < length / 2 ? left : right;
                if (Enumerable.Range(0, count).All(k => proposed[k + 1] - proposed[k] >= 2)) positions = proposed;
            }
            if (positions.Skip(1).SkipLast(1).Any(x => x > left && x < right)) warnings.Add("Appoggi / antenne interferenti con l’ostacolo. Schema non ammesso nell’ottimizzazione.");
        }
        double[] spans = Enumerable.Range(0, count).Select(k => positions[k + 1] - positions[k]).ToArray();
        if (spans.Min() < 2) throw new ArgumentException("Campate troppo corte.");
        double main = towers ? spans[1] : spans.Max();
        if (main > family.MaxSpan || (towers ? main : spans.Min()) < family.MinSpan) warnings.Add($"Fuori dal campo orientativo {family.MinSpan}–{family.MaxSpan} m ({(towers ? "luce centrale" : "singola campata")}).");
        double depth = Auto(i, "depth", ReferenceDepth(family, main, i.B("continuous")));
        if (height - depth < 1) throw new ArgumentException("Quota del ponte insufficiente per l’altezza dell’impalcato e gli appoggi.");
        var deck = ExtendedDeck(i, family, width, length, depth, ortho);
        double volumeDeck = deck.ConcreteArea * length, deckRebar = volumeDeck * 120 / 1000;
        double deckPermanent = deck.ConcreteArea * 25 + deck.SteelMass / length * 9.81 + width * a.D("wear_load") + (i.D("median") > 0 ? 3 : 2) * 8;
        double live = width * a.D("traffic_load"), extraMass = 0;
        UpperStructure structure = new([], 0, 0, 0, 0, 0); int iteration = 0;
        if (upper)
        {
            for (; iteration < 80; iteration++)
            {
                double design = a.D("gamma_g") * (deckPermanent + extraMass / length * 9.81) + a.D("gamma_q") * live;
                structure = ExtendedMembers(i, a, id, spans, design);
                double next = structure.Steel + structure.Cables;
                if (Math.Abs(next - extraMass) <= 1e-8 * Math.Max(1, next)) { extraMass = next; break; }
                extraMass = next;
            }
            if (iteration == 80 || !double.IsFinite(extraMass)) throw new ArgumentException("Predimensionamento di cavi / aste non convergente: rivedere geometria e tensioni di riferimento.");
        }
        double permanent = deckPermanent + extraMass / length * 9.81, q = permanent + live, qd = a.D("gamma_g") * permanent + a.D("gamma_q") * live;
        double ec = 22000 * Math.Pow((i.D("fc") + 8) / 10, .3);
        var service = upper ? (ExtendedReactions(id, spans, q, structure), Array.Empty<Station>()) : Analyze(spans, q, ec * 1000 * deck.Inertia, i.B("continuous"));
        var factored = upper ? (ExtendedReactions(id, spans, qd, structure), Array.Empty<Station>()) : Analyze(spans, qd, ec * 1000 * deck.Inertia, i.B("continuous"));
        var reactions = service.Item1;
        int soil = Array.IndexOf(Soils, i.S("soil"));
        double pressure = Auto(a, "soil_pressure", new double[] { 1000, 400, 200, 100 }[soil]);
        double skin = Auto(a, "pile_skin", new double[] { 150, 70, 45, 25 }[soil]), tip = Auto(a, "pile_base", new double[] { 8000, 2500, 1500, 500 }[soil]);
        string foundation = i.S("foundation"); if (foundation == "Automatica") foundation = soil <= 1 && height < 25 ? "Plinto diretto" : "Pali Ø 1,5 m";
        double diameter = foundation == "Plinto diretto" ? 0 : foundation == "Pali Ø 1,5 m" ? 1.5 : 1;
        double pileLength = diameter == 0 ? 0 : Auto(i, "pile_length", new double[] { 10, 15, 22, 30 }[soil]);
        double resistance = diameter == 0 ? 0 : Math.PI * diameter * pileLength * skin + Math.PI * diameter * diameter / 4 * tip;
        double anchorVolume = id == "suspension" ? 2 * (Math.Max(0, -factored.Item1[0]) + structure.Horizontal / a.D("anchor_friction")) / 25 : 0;
        double subVolume = 0, towerVolume = 0, foundVolume = 0, pileMetres = 0, maxRatio = 0, maxSlenderness = 0, maxStress = 0;
        var supports = new List<Support>(); var supportSchedule = new List<SupportGeometry>();
        for (int k = 0; k <= count; k++)
        {
            bool tower = towers && k is 1 or 2, pier = k > 0 && k < count || k == 0 && i.B("start_pier") || k == count && i.B("end_pier");
            double ph = tower ? height - depth + structure.Tower : pier ? height - depth : Math.Min(7, height - depth);
            bool wall = !tower && i.S("pier") == "Setto";
            int columns = tower ? 2 : pier && i.S("pier") == "Telaio a colonne" ? Math.Max(2, (int)Math.Ceiling(width / 7)) : 1;
            double automatic = tower ? Math.Max(1.5, Math.Max(Math.Sqrt(factored.Item1[k] / (2 * a.D("concept_tower") * 1000)), 2 * ph * Math.Sqrt(12) / 90))
                : wall ? Math.Max(1, ph / 25) : Math.Max(1.2, ph / 12);
            double size = Auto(i, "pier_size", automatic), area = tower ? 2 * size * size : wall ? size * Math.Max(1, width - 2) : Math.PI * size * size / 4 * columns;
            bool hammer = !tower && i.S("pier") == "Testa a martello";
            double capB = tower ? size : hammer ? 2 : 1.5, capT = tower ? size : hammer ? 1.8 : 1.4;
            double sv = pier ? area * ph + width * capB * capT : width * (ph * Math.Max(.6, ph / 7) + 3);
            double av = id == "suspension" && (k == 0 || k == count) ? anchorVolume / 2 : 0;
            double axial = reactions[k] + 25 * (sv + av);
            if (axial <= 0) throw new ArgumentException($"Appoggio {k + 1}: risultante di fondazione non compressa dopo i pesi delle sottostrutture.");
            if (reactions[k] < 0 && av == 0) warnings.Add("Sollevamento non risolto: la soluzione viene esclusa dalla ricerca.");
            int piles = diameter == 0 ? 0 : (int)Auto(i, "pile_count", Math.Max(4, 2 * Math.Ceiling(axial / resistance / 2)));
            double required = diameter == 0 ? Math.Sqrt(axial / (.85 * pressure)) : (Math.Ceiling(Math.Sqrt(piles)) - 1) * 3 * diameter + 2 * diameter;
            double fs = Auto(i, "footing_size", Math.Ceiling(Math.Max(required, pier ? size + 1 : 0) * 4) / 4);
            double fw = Math.Max(fs, tower ? width + size : pier && !wall && columns == 1 ? size + 1 : width), ft = diameter == 0 ? Math.Max(.6, fs / 6) : 1.5 * diameter;
            double fv = fs * fw * ft;
            maxRatio = Math.Max(maxRatio, diameter == 0 ? (axial + 25 * fv) / (fs * fw * pressure) : (axial + 25 * fv) / (piles * resistance));
            if (pier) { maxSlenderness = Math.Max(maxSlenderness, 2 * ph / (tower || wall ? size / Math.Sqrt(12) : size / 4)); maxStress = Math.Max(maxStress, (reactions[k] + 25 * sv) / area / 1000 / i.D("fc_sub")); }
            if (tower) towerVolume += sv; else subVolume += sv;
            foundVolume += fv; pileMetres += piles * pileLength;
            string type = tower ? "Antenna · 2 fusti quadrati" : pier ? i.S("pier") : "Spalla";
            supports.Add(new(k, positions[k], type, reactions[k], ph, size, columns, fs, fw, piles));
            supportSchedule.Add(new(k + 1, type, positions[k], ph, pier ? columns : 0, size, wall && pier ? Math.Max(1, width - 2) : 0,
                pier ? width : 0, pier ? capB : 0, pier ? capT : 0, fs, fw, ft, piles, diameter, pileLength));
        }
        double pileVolume = pileMetres * Math.PI * diameter * diameter / 4;
        double subRebar = (subVolume + towerVolume) * a.D("rebar_sub") / 1000, foundRebar = (foundVolume + pileVolume + anchorVolume) * a.D("rebar_found") / 1000;
        double cf = a.D("co2_concrete") * (i.B("low_carbon") ? a.D("low_carbon_factor") : 1), sf = a.D("co2_steel") * (i.B("recycled") ? a.D("recycled_factor") : 1);
        var quantities = new List<Quantity>();
        void Add(string group, string name, double amount, string unit, string key, double carbon)
        { if (amount > 0) quantities.Add(new(group, name, amount, unit, rates.D(key), amount * rates.D(key), carbon)); }
        Add("Impalcato", "Calcestruzzo", volumeDeck, "m³", "concrete_deck", volumeDeck * cf / 1000);
        Add("Impalcato", "Armatura ordinaria", deckRebar, "t", "rebar", deckRebar * a.D("co2_rebar"));
        Add("Impalcato", "Carpenteria metallica", deck.SteelMass, "t", ortho ? "steel_ortho" : "steel", deck.SteelMass * sf);
        Add("Impalcato", "Casseforme equivalenti", ortho ? 0 : width * length, "m²", "formwork", 0);
        Add("Struttura principale", id == "tied_arch" ? "Archi e catene, inclusi collegamenti" : "Reticolari, inclusi collegamenti", structure.Steel, "t", "steel", structure.Steel * sf);
        Add("Struttura principale", "Cavi, stralli e pendini", structure.Cables, "t", "cables", structure.Cables * a.D("co2_cable"));
        Add("Costruzione", "Montaggio aggiuntivo struttura speciale", upper ? deck.SteelMass + structure.Steel : 0, "t", "erection_special", 0);
        Add("Sottostrutture", "Calcestruzzo pile e spalle", subVolume, "m³", "concrete_sub", subVolume * cf / 1000);
        Add("Sottostrutture", "Calcestruzzo antenne e traversi", towerVolume, "m³", "concrete_sub", towerVolume * cf / 1000);
        Add("Sottostrutture", "Armatura pile, antenne e spalle", subRebar, "t", "rebar", subRebar * a.D("co2_rebar"));
        Add("Fondazioni", "Calcestruzzo plinti", foundVolume, "m³", "concrete_sub", foundVolume * cf / 1000);
        Add("Fondazioni", "Blocchi di ancoraggio a gravità", anchorVolume, "m³", "concrete_sub", anchorVolume * cf / 1000);
        Add("Fondazioni", $"Pali trivellati Ø {diameter:0.0} m", pileMetres, "m", diameter > 1 ? "pile_15" : "pile_1", pileVolume * cf / 1000);
        Add("Fondazioni", "Armatura plinti, ancoraggi e pali", foundRebar, "t", "rebar", foundRebar * a.D("co2_rebar"));
        Add("Finiture", "Apparecchi d'appoggio", (i.B("continuous") ? count + 1 : 2 * count) * Math.Max(2, deck.Girders), "cad", "bearing", 0);
        Add("Finiture", "Giunti di dilatazione", width * (i.B("continuous") ? 2 : count + 1), "m", "joint", 0);
        Add("Finiture", "Barriere", length * (i.D("median") > 0 ? 3 : 2), "m", "barrier", 0);
        Add("Finiture", "Pavimentazione", (width - 2 * i.D("barrier_width")) * length, "m²", "surfacing", 0);
        double direct = quantities.Sum(t => t.Cost), total = direct * (1 + a.D("prelims") / 100) * (1 + a.D("contingency") / 100);
        double carbon = quantities.Sum(t => t.Carbon) * (1 + a.D("co2_site") / 100);
        double months = Math.Ceiling(a.D("months_base") + count * a.D("months_span") + supports.Count(s => s.Type != "Spalla") * a.D("months_pier") + (diameter > 0 ? 1.5 : 0) + (upper ? 6 + main / 50 : 0));
        var details = new List<Detail> {
            new("Larghezza impalcato", width, "m", "Corsie, banchine, spartitraffico e barriere"),
            new("Altezza in campata", depth, "m", "Imposta o Lmax / rapporto convenzionale della famiglia"),
            new("Area cls impalcato", deck.ConcreteArea, "m²", "Area netta; nulla per piastra ortotropa"),
            new("Inerzia lorda equivalente", deck.Inertia, "m⁴", "Sola sezione dell’impalcato, omogeneizzata a Ec; non rigidezza globale di archi / cavi / reticolari"),
            new("Permanenti sull'intero impalcato", permanent, "kN/m", "Impalcato + struttura superiore distribuita uniformemente + finiture"),
            new("Traffico equivalente sull'intero impalcato", live, "kN/m", "Carico uniforme simultaneo; nessun inviluppo mobile"),
            new("Carico verticale di servizio totale", q * length, "kN", "G + Q, incluse masse superiori; esclusi pesi sottostrutture"),
            new("Somma reazioni verticali", reactions.Sum(), "kN", "Equilibrio globale; per sospeso include reazioni negative agli ancoraggi"),
            new("Rapporto carico / riferimento fondazione", maxRatio, "—", "Sola risultante verticale, inclusi pesi sottostrutture e blocchi"),
            new("Snellezza massima delle pile", maxSlenderness, "—", "2H/r; per antenne H comprende l’altezza sopra impalcato"),
            new("Compressione media pile / fc", maxStress, "—", "Soglia assiale orientativa, non verifica pressoflessionale"),
            new("Carico assiale indicativo per palo", resistance, "kN", "Attrito laterale + punta convenzionali"),
            new("Pressione terreno adottata", pressure, "kPa", "Classe o valore imposto"),
            new("Costo diretto", direct, "€", "Somma del computo"), new("Costo totale indicativo", total, "€", "Diretto con oneri e imprevisti")
        };
        if (!upper)
        {
            details.Add(new("Momento positivo indicativo", Math.Max(0, factored.Item2.Max(s => s.Moment)), "kNm", "Trave prismatica, EI lordo costante"));
            details.Add(new("Momento negativo indicativo", Math.Min(0, factored.Item2.Min(s => s.Moment)), "kNm", "Trave prismatica, EI lordo costante"));
            details.Add(new("Freccia elastica indicativa", service.Item2.Max(s => Math.Abs(s.DeflectionMm)), "mm", "Sola flessione longitudinale, sezioni non fessurate"));
        }
        var dimensions = deck.Dimensions.ToList();
        void Dim(string c, string symbol, double value, string unit, string note) => dimensions.Add(new(c, symbol, value, unit, "Predimensionato", note));
        if (upper)
        {
            Dim("Struttura principale", "L_princ", main, "m", towers ? "Luce centrale = L/2; campate di riva = L/4" : "Luce massima delle campate indipendenti");
            Dim("Struttura principale", "f / h", structure.Rise, "m", id == "suspension" ? "Freccia del cavo principale" : "Freccia arco o altezza reticolare");
            Dim("Antenne", "h_sup", structure.Tower, "m", "Altezza sopra impalcato; nulla senza antenne");
            Dim("Struttura principale", "H_rif", structure.Horizontal, "kN", id == "tied_arch" ? "Trazione totale delle due catene" : "Componente orizzontale di riferimento; due piani");
            foreach (var group in structure.Members.GroupBy(m => m.Kind))
            {
                Dim(group.Key, "Σ n·l", group.Sum(m => m.Count * m.Length), "m", "Sviluppo geometrico complessivo nei due piani");
                Dim(group.Key, "A_max", group.Max(m => m.Area) * 1e6, "mm²", "Area massima per singola asta o cavo = |N| / tensione di riferimento");
                Dim(group.Key, "N_max", group.Max(m => Math.Abs(m.Force)), "kN", "Massimo valore assoluto per singola asta / cavo sotto carico uniforme");
                Dim(group.Key, "m_net", group.Sum(m => m.Mass), "t", "Massa geometrica, prima di eventuali collegamenti");
            }
            details.Add(new("Iterazioni equilibrio peso struttura", iteration + 1, "n.", "Aggiornamento massa superiore e carico fino a tolleranza relativa 10⁻⁸"));
            warnings.Add("Archi, stralli, sospeso e reticolari: equilibrio ideale sotto carico uniforme e aree da tensioni di riferimento. Nessuna verifica di instabilità, fatica, deformabilità globale, fasi costruttive, sisma o vento. Diagrammi globali non disponibili.");
        }
        if (id == "suspension")
        {
            Dim("Ancoraggi", "V_tot", anchorVolume, "m³", "Due blocchi: (sollevamento + H/attrito)/25; stima di peso, senza verifica di ribaltamento e geotecnica");
            warnings.Add("Sospeso: campate di riva su travi appoggiate, cavi di riva rettilinei. Blocchi di ancoraggio stimati a gravità; ribaltamento, pressioni eccentriche, stabilità globale e aerodinamica non calcolati.");
        }
        if (id == "filler_beam") warnings.Add("Travi incorporate: profili a I ideali, cls netto dell’acciaio; collaborazione perfetta assunta. Adesione, armatura trasversale, fasi di getto e verifica del profilo non calcolate.");
        if (ortho) warnings.Add("Piastra ortotropa: lamiera, canalette e cassone conteggiati geometricamente. La rigidezza locale ortotropa, i traversi e la fatica delle saldature richiedono un modello dedicato.");
        if (maxRatio > 1 || maxSlenderness > 100 || maxStress > .3) warnings.Add("Una o più soglie indicative di pile / fondazioni sono superate; soluzione esclusa dalla ricerca.");
        warnings.Add("Costi convenzionali, montaggio speciale esplicito; CO₂ parziale materiali e cantiere, senza EPD. Durata parametrica, non cronoprogramma.");
        if (quantities.Any(t => !double.IsFinite(t.Amount) || !double.IsFinite(t.Cost)) || !double.IsFinite(deck.Inertia)) throw new ArgumentException("Risultato non finito nel predimensionamento della tipologia.");
        return new(family, width, depth, depth, deck.Slab, deck.Spacing, deck.Girders, deck.Web, deck.Bottom, spans, supports.ToArray(), foundation, diameter, pileLength,
            volumeDeck + subVolume + towerVolume + foundVolume + anchorVolume + pileVolume, deck.SteelMass + structure.Steel + structure.Cables,
            deckRebar + subRebar + foundRebar, 0, deck.Inertia, direct, total, total * (1 - a.D("uncertainty") / 100), total * (1 + a.D("uncertainty") / 100),
            carbon, months, quantities.ToArray(), details.ToArray(), factored.Item2, warnings.ToArray()) {
                Advanced = new(ortho, !upper, main, structure.Rise, structure.Tower, deck.SteelMass, structure.Steel, structure.Cables, anchorVolume, towerVolume,
                    structure.Horizontal, structure.Members, dimensions.ToArray()) { FoundationSchedule = supportSchedule.ToArray() }
            };
    }
}
