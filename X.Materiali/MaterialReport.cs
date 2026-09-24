namespace Materiali;

public sealed partial class MaterialView
{
    // Use the same values and notes displayed by the module, without a second calculation model.
    public IReadOnlyList<PropertyRow> ReportProperties()
    {
        Refresh();
        var rows = table.Items.OfType<PropertyRow>().Where(r => r.Simbolo != "fck").ToList();
        void Add(string name, string value) { if (!string.IsNullOrWhiteSpace(value)) rows.Add(new(name, "", value, "")); }
        Add("Aderenza", bondValue.Text); Add("Dettagli aderenza", bondDetails.Text);
        Add("Copriferro nominale", coverHeadline.Text); Add("Dettagli copriferro", coverSteps.Text); Add("Applicabilità copriferro", coverNotes.Text);
        foreach (var (key, name, unit) in new[] { ("chloride", "Classe cloruri", ""), ("ratio", "Rapporto A/C massimo", ""),
            ("cement", "Dosaggio minimo di cemento", "kg/m³"), ("air", "Contenuto minimo di aria", "%") })
            rows.Add(new(name, "", compositionValues[key].Text, unit));
        Add("Requisiti della composizione", compositionRequirements.Text); Add("Indicazioni aria", compositionAirNote.Text);
        Add("Indicazioni cloruri", compositionChlorideNote.Text); Add("Riferimenti della composizione", compositionSource.Text);
        return rows;
    }
}
