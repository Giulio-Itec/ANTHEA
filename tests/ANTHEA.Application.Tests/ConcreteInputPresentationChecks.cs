using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Application.Concrete;
using Anthea.Application.Materials;

/// <summary>
/// Derivations of the inputs, materials and actions of the RC section sheet moved from the historic view in the preparation of F5.10
/// (ConcreteInputPresentation, ConcreteActionPresentation), with values written by hand. The identity of the historic view with these
/// functions is proved by ui/check-sezione-historic (traces frozen before the move) and by the F3 reference.
/// </summary>
internal static class ConcreteInputPresentationChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        string Failure(Action action) { try { action(); return ""; } catch (ArgumentException error) { return error.Message; } }
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            // Geometry: the fields of every shape, the hole, the manual bars, the second layers.
            Check(string.Join(",", ConcreteInputPresentation.ShapeFields.Where(k => ConcreteInputPresentation.GeometryFieldShown(k, "Circolare"))) == "diameter_mm,circular_sides",
                "Campi della sezione circolare");
            Check(string.Join(",", ConcreteInputPresentation.ShapeFields.Where(k => ConcreteInputPresentation.GeometryFieldShown(k, "Rettangolare"))) == "width_mm,height_mm",
                "Campi della sezione rettangolare");
            Check(string.Join(",", ConcreteInputPresentation.ShapeFields.Where(k => ConcreteInputPresentation.GeometryFieldShown(k, "A T"))) == "height_mm,flange_width_mm,web_width_mm,flange_thickness_mm",
                "Campi della sezione a T");
            Check(string.Join(",", ConcreteInputPresentation.ShapeFields.Where(k => ConcreteInputPresentation.GeometryFieldShown(k, "Generica (da definire)"))) == "height_mm",
                "Campi della sezione generica");
            Check(ConcreteInputPresentation.ReinforcementFieldShown("longitudinal_bar_count", "Circolare") && !ConcreteInputPresentation.ReinforcementFieldShown("top_bar_count", "Circolare")
                && ConcreteInputPresentation.ReinforcementFieldShown("flange_bottom_count", "A T") && !ConcreteInputPresentation.ReinforcementFieldShown("flange_bottom_count", "Rettangolare")
                && ConcreteInputPresentation.ReinforcementFieldShown("side_bar_count_per_side", "A T"), "Campi dell'armatura per forma");
            Check(ConcreteInputPresentation.HoleAvailable("Circolare") && ConcreteInputPresentation.HoleAvailable("Rettangolare") && !ConcreteInputPresentation.HoleAvailable("A T"), "Foro per forma");
            Check(ConcreteInputPresentation.HoleFieldShown("inner_diameter_mm", "Circolare") && !ConcreteInputPresentation.HoleFieldShown("inner_width_mm", "Circolare")
                && ConcreteInputPresentation.HoleFieldShown("inner_height_mm", "Rettangolare"), "Campi del foro");
            var tee = J.Obj(("shape", "A T"), ("foro_presente", true)); ConcreteInputPresentation.NormalizeHole(tee);
            var round = J.Obj(("shape", "Circolare"), ("foro_presente", true)); ConcreteInputPresentation.NormalizeHole(round);
            var generic = J.Obj(("shape", "Generica (da definire)")); ConcreteInputPresentation.NormalizeHole(generic);
            Check(tee.ToJsonString() == "{\"shape\":\"A T\",\"foro_presente\":false}" && round.B("foro_presente") && generic.ToJsonString().EndsWith("\"foro_presente\":false}"),
                "Foro tolto dalle forme che non lo hanno (la chiave si scrive anche se manca)");
            Check(ConcreteInputPresentation.ManualBars(J.Obj(("barre_manuali", new JsonArray()))) && !ConcreteInputPresentation.ManualBars(new JsonObject { ["barre_manuali"] = null })
                && ConcreteInputPresentation.LayersLocked(new JsonObject { ["barre_manuali"] = null }), "Barre manuali: elenco per l'armatura, chiave per gli strati");
            Check(ConcreteInputPresentation.ReinforcementToolTip(true) is null && ConcreteInputPresentation.ReinforcementToolTip(false)!.StartsWith("Sono attive le coordinate delle barre."), "Nota dell'armatura");
            Check(ConcreteInputPresentation.LayerShown("inner", "Circolare") && !ConcreteInputPresentation.LayerShown("inner", "Rettangolare") && ConcreteInputPresentation.LayerShown("top", "Rettangolare")
                && !ConcreteInputPresentation.LayerShown("bottom", "A T"), "Secondi strati per forma");
            Check(ConcreteInputPresentation.LayerToggleText(true, "Seconda fila superiore") == "− Disattiva · Seconda fila superiore"
                && ConcreteInputPresentation.LayerToggleText(false, "Secondo anello interno") == "+ Attiva · Secondo anello interno"
                && ConcreteInputPresentation.LayerToolTip(true).StartsWith("Ripristina le barre da wizard") && ConcreteInputPresentation.LayerToolTip(false).StartsWith("Distanza libera"),
                "Testi dei secondi strati");
            Check(string.Join("|", ConcreteInputPresentation.Layers.Select(l => l.Key + ":" + l.Source)) == "top:top|bottom:bottom|inner:longitudinal", "Secondi strati e loro origine");

            // Sections made by the wizard (F5.10 S1b): a shape of the wizard, or a hole with two contours; the ordinary shapes keep the editor.
            var shapeI = J.Obj(("shape", "A I")); var hollow = J.Obj(("shape", "Rettangolare"), ("foro_presente", true), ("wizard_layout", "Doppio contorno"));
            var holeOnly = J.Obj(("shape", "Rettangolare"), ("foro_presente", true), ("wizard_layout", "Perimetro"));
            var ringOnly = J.Obj(("shape", "Circolare"), ("foro_presente", false), ("wizard_layout", "Doppio contorno"));
            Check(ConcreteInputPresentation.TemplateSection(shapeI) && ConcreteInputPresentation.TemplateSection(hollow) && !ConcreteInputPresentation.TemplateSection(holeOnly)
                && !ConcreteInputPresentation.TemplateSection(ringOnly) && !ConcreteInputPresentation.TemplateSection(J.Obj(("shape", "A T"))),
                "Sezioni del wizard: forme del wizard e foro a doppio contorno");
            Check(!ConcreteInputPresentation.HoleToggleEnabled(hollow) && ConcreteInputPresentation.HoleToggleEnabled(holeOnly) && !ConcreteInputPresentation.HoleToggleEnabled(shapeI),
                "Foro centrale spento per le sezioni del wizard");
            Check(ConcreteInputPresentation.ReinforcementToolTip(shapeI, true) == "Modificare la disposizione da Wizard tipologie oppure dalla tabella delle coordinate delle barre."
                && ConcreteInputPresentation.ReinforcementToolTip(holeOnly, true) is null && ConcreteInputPresentation.ReinforcementToolTip(holeOnly, false)!.StartsWith("Sono attive"),
                "Nota dell'armatura delle sezioni del wizard");
            Check(!ConcreteInputPresentation.LayerVisible("top", hollow) && ConcreteInputPresentation.LayerVisible("top", holeOnly) && !ConcreteInputPresentation.LayerVisible("inner", holeOnly)
                && ConcreteInputPresentation.LayerVisible("inner", ringOnly), "Secondi strati nascosti per le sezioni del wizard");
            var hollowRing = J.Obj(("shape", "Circolare"), ("foro_presente", true), ("wizard_layout", "Doppio contorno"));
            Check(ConcreteInputPresentation.CircularLinkScheme(ringOnly) && !ConcreteInputPresentation.CircularLinkScheme(hollowRing) && !ConcreteInputPresentation.CircularLinkScheme(holeOnly),
                "Schema circolare delle staffe solo per il cerchio ordinario");
            Check(string.Join("|", ConcreteInputPresentation.ShapeChoicesAfterWizard(["Circolare", "Rettangolare"], "A I")!) == "Circolare|Rettangolare|A I"
                && ConcreteInputPresentation.ShapeChoicesAfterWizard(["Circolare", "Rettangolare"], "Rettangolare") is null, "Voci di «Sezione» dopo il wizard");
            Check(ConcreteInputPresentation.ManualShearNote(J.Obj(("nota_wizard", "Nota del wizard"))) == "Nota del wizard"
                && ConcreteInputPresentation.ManualShearNote(new JsonObject()) == "Parametri manuali: verificare geometria, armatura tesa e ancoraggio.", "Nota del taglio con parametri manuali");
            Check(ConcreteInputPresentation.TemplateGeometryNote == "Geometria e copriferro si modificano nel wizard."
                && ConcreteInputPresentation.TemplateReinforcementNote.StartsWith("Disposizione definita nel wizard."), "Note del wizard");

            // Materials: choices, the catalogue class, the materials of the user.
            JsonObject[] catalog = [J.Obj(("nome", "C30/37"), ("fck_mpa", 30)), J.Obj(("nome", "C35/45"), ("fck_mpa", 35))];
            Check(string.Join("|", ConcreteInputPresentation.MaterialChoices(catalog, "C30/37")) == "C30/37|C35/45|Personalizzato"
                && string.Join("|", ConcreteInputPresentation.MaterialChoices(catalog, "")) == "C30/37|C35/45|Personalizzato"
                && string.Join("|", ConcreteInputPresentation.MaterialChoices(catalog, "C99")) == "C30/37|C35/45|C99|Personalizzato", "Scelte di una classe alla costruzione");
            Check(string.Join("|", ConcreteInputPresentation.StandardChoices(catalog, "")) == "C30/37|C35/45||Personalizzato", "Scelte dopo il cambio di normativa (testo vuoto conservato)");
            Check(ConcreteInputPresentation.StandardMaterial(catalog, "C30/37", "classe_cls").D("fck_mpa") == 30
                && ConcreteInputPresentation.StandardMaterial(catalog, "C99", "classe_cls").S("nome") == "C35/45", "Classe del catalogo o predefinita");
            var values = ConcreteInputPresentation.StandardMaterialValues(J.Obj(("nome", "C40/50"), ("classe_cls", "C40/50"), ("fck_mpa", 40), ("cls_diagramma", "Bilineare"), ("materiale_cls_nome", "C40/50"))).ToList();
            Check(string.Join(",", values.Select(v => v.Key)) == "classe_cls,fck_mpa,materiale_cls_nome" && values[1].Value!.GetValue<int>() == 40, "Materiale di catalogo senza nome né legge");
            var custom = new JsonObject(); ConcreteInputPresentation.CustomConcrete(custom); ConcreteInputPresentation.CustomSteel(custom);
            Check(custom.ToJsonString() == "{\"classe_cls\":\"Personalizzato\",\"materiale_cls_nome\":\"CLS personalizzato\",\"classe_acciaio\":\"Personalizzato\",\"materiale_acciaio_nome\":\"Acciaio personalizzato\"}",
                "Materiali resi personalizzati da un valore digitato");
            Check(ConcreteInputPresentation.SteelProperties.Contains("steel_eps_u") && !ConcreteInputPresentation.SteelProperties.Contains("steel_diagramma"), "Proprietà dell'acciaio che lo rendono personalizzato");
            var saved = J.Obj(("tipo", "Acciaio"), ("nome", "A prova"), ("steel_modulus_mpa", "205000"), ("fyk_mpa", "500"), ("steel_fu_mpa", "560"), ("steel_eps_u", "60"), ("steel_diagramma", "Incrudente"));
            var steelInput = new JsonObject(); var settings = new JsonObject();
            Check(ConcreteInputPresentation.ApplySavedMaterial(steelInput, settings, saved) == "Acciaio" && steelInput.S("classe_acciaio") == "Personalizzato"
                && steelInput.S("materiale_acciaio_nome") == "A prova" && steelInput.S("steel_diagramma") == "Incrudente" && settings.Count == 0, "Acciaio salvato applicato");
            var tendon = J.Obj(("tipo", "Trefoli"), ("nome", "T prova"), ("id", "x1"));
            Check(ConcreteInputPresentation.ApplySavedMaterial(new JsonObject(), settings, tendon) == "Trefoli" && settings["materiale_trefolo"].S("nome") == "T prova"
                && !ReferenceEquals(settings["materiale_trefolo"], tendon), "Trefolo salvato come materiale dei nuovi cavi");
            Check(ConcreteInputPresentation.SavedMaterialLabel(saved) == "Acciaio · A prova", "Voce di un materiale salvato");
            var draft = ConcreteInputPresentation.NewMaterial("Calcestruzzo", catalog, "C35/45", "NTC 2018");
            Check(draft.S("nome") == "Calcestruzzo personalizzato" && draft.D("fck_mpa") == 35 && draft.S("tipo") == "Calcestruzzo" && draft.S("id").Length == 32
                && draft.S("origine") == "Personalizzato" && draft.S("normativa_origine") == "NTC 2018"
                && ConcreteInputPresentation.NewMaterial("Calcestruzzo", catalog, "Personalizzato", "NTC 2018").D("fck_mpa") == 30, "Bozza di un nuovo materiale");
            var named = J.Obj(("materiali_custom", new JsonArray(J.Obj(("tipo", "Calcestruzzo"), ("nome", "Mio")))));
            Check(ConcreteInputPresentation.NewMaterialNameError(J.Obj(("nome", " ")), catalog, named, "Calcestruzzo") == "Inserire il nome del materiale."
                && ConcreteInputPresentation.NewMaterialNameError(J.Obj(("nome", "C30/37")), catalog, named, "Calcestruzzo") == "Usare un nome distinto dai materiali esistenti."
                && ConcreteInputPresentation.NewMaterialNameError(J.Obj(("nome", "Mio")), catalog, named, "Calcestruzzo") == "Usare un nome distinto dai materiali esistenti."
                && ConcreteInputPresentation.NewMaterialNameError(J.Obj(("nome", "Mio")), catalog, named, "Acciaio") is null, "Nome di un nuovo materiale");
            Check(ConcreteInputPresentation.NewMaterialCurrent("Acciaio", J.Obj(("classe_acciaio", "B450C")), new JsonObject()) == "B450C"
                && ConcreteInputPresentation.NewMaterialCurrent("Trefoli", new JsonObject(), J.Obj(("materiale_trefolo", J.Obj(("nome", "Y1860"))))) == "Y1860", "Classe corrente della finestra");
            Check(ConcreteInputPresentation.NewMaterialCatalog("Trefoli", "NTC 2018").Any(m => m.S("nome") == "Y1860") && ConcreteInputPresentation.NewMaterialCatalog("Acciaio", "NTC 2018").All(m => m.S("nome") is "B450A" or "B450C"),
                "Cataloghi della finestra «Nuovo materiale»");
            Check(ConcreteInputPresentation.RheologyStrength(J.Obj(("fck_mpa", "-32"))) == 32, "Resistenza passata alla reologia");

            // Tendons.
            Check(ConcreteInputPresentation.Exact(0.1) == "0.1" && ConcreteInputPresentation.Exact(1d / 3) == "0.3333333333333333", "Numeri scritti esattamente");
            // √(4·150/π) = 13,8197…; π·10²/4 = 78,5398…: the formulas are those of the libraries, read through ConcreteEditor.
            string d150 = ConcreteInputPresentation.Exact(ConcreteEditor.ReinforcementDiameter(150));
            Check(d150.StartsWith("13.8197", StringComparison.Ordinal) && ConcreteInputPresentation.TendonDiameter(J.Obj(("area", "150"))) == d150
                && ConcreteInputPresentation.TendonDiameter(J.Obj(("area", "0"))) == "" && ConcreteInputPresentation.TendonDiameter(new JsonObject()) == "", "Diametro equivalente di un cavo");
            Check(ConcreteInputPresentation.TendonCounterpart("area", "150") == d150
                && ConcreteInputPresentation.TendonCounterpart("diametro", "10").StartsWith("78.5398", StringComparison.Ordinal)
                && ConcreteInputPresentation.TendonCounterpart("diametro", "10") == ConcreteInputPresentation.Exact(ConcreteEditor.ReinforcementArea(10))
                && ConcreteInputPresentation.TendonCounterpart("diametro", "1,5") == ConcreteInputPresentation.Exact(ConcreteEditor.ReinforcementArea(1.5))
                && ConcreteInputPresentation.TendonCounterpart("area", "-1") == "" && ConcreteInputPresentation.TendonCounterpart("area", "abc") == "", "Area e diametro equivalente collegati");
            var library = J.Obj(("nome", "Y1860"), ("Ep", 195000), ("fpk", 1860));
            Check(ConcreteInputPresentation.TendonMaterialValue(library, "Ep") == "195000" && ConcreteInputPresentation.TendonMaterialValue(library, "diagramma") == "Incrudente"
                && ConcreteInputPresentation.TendonMaterialValue(library, "eps_u") == "", "Valori del materiale copiati nel cavo");
            var form = ConcreteInputPresentation.NewTendonForm();
            Check(form.S("numero") == "1" && form.S("sigma0") == "1000" && form.S("diametro") == d150, "Modulo del nuovo cavo all'apertura");
            form["numero"] = "3"; form["y"] = "-250"; form["sigma0"] = "1250";
            var cable = ConcreteInputPresentation.NewTendon(form, library, ["T01", "T03"]);
            Check(cable.S("id") == "T02" && cable.S("y") == "-250" && cable.S("sigma0") == "1250" && cable.S("area").StartsWith("450", StringComparison.Ordinal)
                && cable.S("area") == ConcreteInputPresentation.Exact(ConcreteEditor.ReinforcementArea(ConcreteEditor.ReinforcementDiameter(150), 3)), "Nuovo cavo: primo identificativo libero, area dei trefoli");
            Check(Failure(() => ConcreteInputPresentation.NewTendon(form, null, [])) == "Creare e selezionare un materiale trefolo nel gruppo Materiali.", "Cavo senza materiale");
            form["numero"] = "2,5"; Check(Failure(() => ConcreteInputPresentation.NewTendon(form, library, [])) == "Numero trefoli: intero fra 1 e 1000.", "Numero di trefoli non intero");
            form["numero"] = "1001"; Check(Failure(() => ConcreteInputPresentation.NewTendon(form, library, [])) == "Numero trefoli: intero fra 1 e 1000.", "Troppi trefoli");
            form["numero"] = "1"; form["sigma0"] = "1860"; Check(Failure(() => ConcreteInputPresentation.NewTendon(form, library, [])) == "σp0 deve essere inferiore a fpk.", "σp0 non minore di fpk");

            // Actions.
            var row = ConcreteActionPresentation.NewRow("Combo 2", "-10", "5", "0", "abc", false);
            Check(string.Join(",", row.Select(p => p.Key)) == "id,nome,N,Mx,My,visible,eta,esito" && row.S("id") == "abc" && row.S("N") == "-10" && !row.B("visible", true)
                && row.S("eta") == "—" && row.S("esito") == "Da calcolare", "Riga nuova delle azioni");
            Check(ConcreteActionPresentation.NewRow("A", "0", "0", "0").S("id").Length == 32, "Identificativo nuovo di una riga");
            Check(ConcreteActionPresentation.NextName(["Combo 1", "Combo 3", "SLU"]) == "Combo 2" && ConcreteActionPresentation.NextName([]) == "Combo 1", "Nome della riga nuova");
            var shear = ConcreteActionPresentation.ShearRow(J.Obj(("nome", "V"), ("T", "4")));
            Check(shear.S("T") == "4" && shear.S("Mx") == "0" && shear.S("My") == "0", "Riga del taglio completata");
            var newShear = ConcreteActionPresentation.NewShearRow(ConcreteActionPresentation.ShearName(2));
            Check(newShear.S("nome") == "Taglio 3" && newShear.S("Vx") == "0" && !newShear.ContainsKey("T") && newShear.S("id").Length == 32, "Riga nuova del taglio");
            Check(ConcreteActionPresentation.CopyText([J.Obj(("nome", "A"), ("N", "1"), ("Mx", "2"), ("My", "3"), ("Vx", "4"))], "SLU") == "A\t1\t2\t3"
                && ConcreteActionPresentation.CopyText([J.Obj(("nome", "A"), ("N", "1"))], "Taglio") == "A\t1\t\t\t\t\t", "Testo copiato");
            Check(ConcreteActionPresentation.CopyStatus(2).StartsWith("Copiate 2 righe") && ConcreteActionPresentation.PasteStatus(3) == "3 righe incollate · aggiornamento automatico in attesa…", "Stati di copia e incolla");
            var plan = ConcreteActionPresentation.Plan("SLU", "nome\tN\tMx\tMy\r\nA\t-1\t2\t3\n\nB\t-4\t5\t6", "");
            Check(plan.StartColumn == 0 && plan.Width == 4 && plan.Rows.Count == 2 && plan.Rows[1][0] == "B", "Incolla di righe complete con intestazione");
            var cells = ConcreteActionPresentation.Plan("SLU", "-7\t8", "N");
            Check(cells.StartColumn == 1 && cells.Width == 2 && string.Join(",", cells.Keys) == "nome,N,Mx,My", "Incolla dalla cella corrente");
            var history = ConcreteActionPresentation.Plan("Taglio", "V\t-1\t2\t3", "");
            Check(string.Join(",", history.Keys) == "nome,N,Vx,Vy,T" && history.StartColumn == 0, "Formato storico del taglio");
            Check(ConcreteActionPresentation.Plan("Taglio", "-1\t0\t0\t2\t3\t0", "").StartColumn == 1, "Taglio senza nome");
            Check(Failure(() => ConcreteActionPresentation.Plan("SLU", "\n \n", "")) == "Gli appunti non contengono righe.", "Incolla vuoto");
            Check(Failure(() => ConcreteActionPresentation.Plan("SLU", "a\tb\tc\td\te", "")) == "Numero di colonne non valido per la famiglia selezionata.", "Troppe colonne");
            Check(Failure(() => ConcreteActionPresentation.Plan("SLU", "1\t2", "")) == "Selezionare prima una cella di input (nome o sollecitazione). Per aggiungere righe complete usare 3 o 4 colonne.",
                "Incolla parziale senza cella");
            Check(Failure(() => ConcreteActionPresentation.Plan("SLU", "1\t2", "My")) == "La selezione supera le colonne di input. Selezionare la cella iniziale corretta.", "Incolla oltre le colonne");
            Check(Failure(() => ConcreteActionPresentation.Plan("SLU", "nome\tN\tMx\tMy", "")) == "Sono presenti solo le intestazioni.", "Solo intestazioni");
            Check(Failure(() => ConcreteActionPresentation.Plan("SLU", " \t1\t2\t3", "")) == "Riga 1: nome mancante.", "Nome mancante");
            Check(Failure(() => ConcreteActionPresentation.Plan("SLU", "A\t1\tx\t3", "")) == "Riga 1, Mx: inserire un numero finito.", "Valore non numerico");
            Check(ConcreteActionPresentation.PastedName(4) == "Combo 5", "Nome di una riga incollata in più");

            // F5.10 S1: the shell of the sheet (ConcreteSheetPresentation), main tab and dividers.
            Check(ConcreteSheetPresentation.InitialTab(new JsonObject()) == 0 && ConcreteSheetPresentation.InitialTab(J.Obj(("tab", 3))) == 3
                && ConcreteSheetPresentation.InitialTab(J.Obj(("tab", "5"))) == 5 && ConcreteSheetPresentation.InitialTab(J.Obj(("tab", 12))) == 7
                && ConcreteSheetPresentation.InitialTab(J.Obj(("tab", -2))) == 0 && ConcreteSheetPresentation.InitialTab(J.Obj(("tab", "abc"))) == 0, "Scheda iniziale della sezione");
            var layout = J.Obj(("layout", J.Obj(("ingressi", 0.4), ("righe", 0.9), ("risultati", "abc"))));
            Check(ConcreteSheetPresentation.SplitRatio(layout, "ingressi", 0.3) == 0.4 && ConcreteSheetPresentation.SplitRatio(layout, "righe", 0.65) == 0.65
                && ConcreteSheetPresentation.SplitRatio(layout, "risultati", 0.6) == 0.6 && ConcreteSheetPresentation.SplitRatio(new JsonObject(), "ingressi", 0.3) == 0.3,
                "Divisori salvati in layout");
            Check(ConcreteSheetPresentation.MovedSplitRatio(0.05) == 0.15 && ConcreteSheetPresentation.MovedSplitRatio(0.5) == 0.5 && ConcreteSheetPresentation.MovedSplitRatio(0.95) == 0.85,
                "Divisore spostato limitato a 0,15–0,85");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        return checks;
    }
}
