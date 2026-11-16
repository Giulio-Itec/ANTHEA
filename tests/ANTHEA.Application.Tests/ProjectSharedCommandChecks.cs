using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Calculations;
using X.Core;

/// <summary>
/// The commands that change the shared data of a project with a sheet without dati (an unresolved reference opened with diagnosis) or
/// that could not be read (DocumentQuarantine, step 2b): they skip it or refuse with a message, never with a NullReferenceException,
/// and never fill it with the defaults of its module (ProjectSharedData.Updatable).
/// </summary>
internal static class ProjectSharedCommandChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
        }
        string Refusal(Action action, string message)
        {
            try { action(); }
            catch (ArgumentException error) { checks++; return error.Message; }
            throw new InvalidOperationException(message);
        }
        static JsonNode Row(JsonObject sheet) => ((sheet["dati"]!["stratigrafie"] as JsonArray)?.OfType<JsonArray>().FirstOrDefault()?.FirstOrDefault() ?? new JsonObject()).DeepClone();

        // Soils: «Uniforma a questo» on a missing layer, «Collega» and the soil type. A section with a horizontal pile of two layers, vertical
        // piles of one layer, vertical piles without dati and vertical piles that could not be read (quarantine, with their dati).
        foreach (bool explicitNull in new[] { false, true })
        {
            var archive = ProjectDocuments.CreateArchive();
            var owner = ProjectDocuments.AddProject(archive, "Prova comandi terreni");
            var section = ProjectDocuments.AddSection(owner, "Spalla");
            var pile = ProjectDocuments.AddSheet(section, PaloOrizzontale.Module, "Palo completo");
            var vertical = ProjectDocuments.AddSheet(section, "geo_palo_verticale", "Pali verticali");
            var empty = ProjectDocuments.AddSheet(section, "geo_palo_verticale", "Pali senza dati");
            var quarantined = ProjectDocuments.AddSheet(section, "geo_palo_verticale", "Pali non leggibili");
            pile["dati"]!["stratigrafie"] = new JsonArray(new JsonArray(Row(pile), Row(pile)));
            vertical["dati"]!["stratigrafie"] = new JsonArray(new JsonArray(Row(vertical)));
            quarantined["dati"]!["stratigrafie"] = new JsonArray(new JsonArray(Row(quarantined)));
            quarantined[DocumentQuarantine.Property] = "riferimento dei dati non risolto (prova)";
            if (explicitNull) empty["dati"] = null;
            else empty.Remove("dati");
            string label = explicitNull ? " (dati null)" : " (dati assenti)";
            string emptyBefore = empty.ToJsonString(), quarantinedBefore = quarantined.ToJsonString();

            Check(ProjectSharedData.CopyMissingSoilLayers(pile, section, 0, 1) == 1 && vertical["dati"]!["stratigrafie"]![0]!.AsArray().Count == 2,
                "Uniforma a questo: lo strato mancante arriva alla scheda leggibile" + label);
            Check(empty.ToJsonString() == emptyBefore && quarantined.ToJsonString() == quarantinedBefore,
                "Uniforma a questo: la scheda senza dati e quella non leggibile restano come nel file" + label);

            string before = archive.ToJsonString();
            foreach (var (source, target, unreadable) in new[] { (pile, empty, empty), (empty, pile, empty), (pile, quarantined, quarantined) })
            {
                string message = Refusal(() => ProjectSharedData.LinkSoils((JsonObject)source.DeepClone(), (JsonObject)target.DeepClone()),
                    "Collega con " + unreadable.S("nome") + " accettato" + label);
                Check(message.StartsWith("«" + unreadable.S("nome") + "» non è leggibile", StringComparison.Ordinal), "Collega: messaggio " + message);
            }
            string typeKey = ProjectSharedData.Fields(quarantined).Keys.First(k => k.StartsWith("Strato · ", StringComparison.Ordinal) && k.EndsWith("/tipologia", StringComparison.Ordinal));
            foreach (var sheet in new[] { empty, quarantined })
            {
                string message = Refusal(() => ProjectSharedData.SetSoilType(sheet, typeKey, "Coesivo"), "Tipologia scritta in " + sheet.S("nome") + label);
                Check(message.StartsWith("«" + sheet.S("nome") + "» non è leggibile", StringComparison.Ordinal), "Tipologia: messaggio " + message);
            }
            Check(archive.ToJsonString() == before, "Collega e tipologia rifiutati senza modifiche" + label);
        }

        // «Applica ai fogli collegati» from Materiali: a wall and a pile without dati and a pile that could not be read are not targets
        // (never filled with the defaults of the module), the complete wall is updated; a sheet without dati cannot be the source.
        {
            var archive = ProjectDocuments.CreateArchive();
            var owner = ProjectDocuments.AddProject(archive, "Prova applica");
            var material = ProjectDocuments.AddSheet(owner, "mat_calcestruzzo", "CLS del progetto");
            var section = ProjectDocuments.AddSection(owner, "Elemento");
            var wall = ProjectDocuments.AddSheet(section, RetainingWall.Module, "Muro completo");
            var emptyWall = ProjectDocuments.AddSheet(section, RetainingWall.Module, "Muro senza dati");
            var emptyPile = ProjectDocuments.AddSheet(section, PaloOrizzontale.Module, "Palo senza dati");
            var quarantined = ProjectDocuments.AddSheet(section, PaloOrizzontale.Module, "Palo non leggibile");
            emptyWall.Remove("dati");
            emptyPile["dati"] = null;
            quarantined[DocumentQuarantine.Property] = "riferimento dei dati non risolto (prova)";
            string wallBefore = wall.ToJsonString(), quarantinedBefore = quarantined.ToJsonString();
            material["dati"]!["classe"] = material["dati"].S("classe") == "C45/55" ? "C30/37" : "C45/55";
            int updated = ProjectSharedData.ApplyHierarchy(material, new HashSet<string> { "Materiali" });
            Check(updated == 1 && wall.ToJsonString() != wallBefore && emptyWall["dati"] is null && emptyPile["dati"] is null && quarantined.ToJsonString() == quarantinedBefore,
                "Applica ai fogli collegati: aggiornati " + updated + ", fogli senza dati o non leggibili lasciati come nel file");
            string before = archive.ToJsonString();
            string message = Refusal(() => ProjectSharedData.ApplyHierarchy(emptyPile, new HashSet<string> { "Materiali" }), "Dati di un foglio senza dati applicati agli altri");
            Check(message.StartsWith("«Palo senza dati» non è leggibile", StringComparison.Ordinal) && archive.ToJsonString() == before, "Applica da un foglio senza dati: " + message);
        }
        return checks;
    }
}
