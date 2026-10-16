namespace Anthea.Presentation.Forms;

/// <summary>
/// Labels and symbols of the fields of the forms with a symbol column (geotechnical sheets): the table of the former
/// InputForm.WithSymbol of X.Desktop Ui.cs, moved in F5.2 (proposal F5, section 2.1). InputForm and FieldForm use the same table.
/// </summary>
public static class FieldSymbols
{
    /// <summary>Label and symbol of a field: its own when it has a symbol, otherwise those of the table, otherwise its label
    /// without symbol.</summary>
    public static (string Label, string Symbol) For(string key, string label, string symbol)
    {
        if (symbol != "") return (label, symbol);
        return key switch
        {
            "diametro" => ("Diametro palo", "D"), "lunghezza" => ("Lunghezza palo", "L"),
            "peso_specifico_palo" => ("Peso specifico CLS / palo", "γp"),
            "azione_compressione" => ("Azione assiale — Compressione", "NEd,c"), "azione_trazione" => ("Azione assiale — Trazione", "NEd,t"),
            "profondita_falda" => ("Profondità falda", "zf"), "verticali_indagate" => ("Verticali indagate", "n"),
            "__xi3" => ("Correlazione", "ξ3"), "__xi4" => ("Correlazione", "ξ4"),
            "sicurezza_laterale_compressione" => ("Sicurezza laterale — Compressione", "γs"),
            "sicurezza_laterale_trazione" => ("Sicurezza laterale — Trazione", "γt"),
            "sicurezza_base" => ("Sicurezza di base", "γb"), "peso_palo_sfavorevole" => ("Peso proprio — Sfavorevole", "γG"),
            "peso_palo_favorevole" => ("Peso proprio — Favorevole", "γG"),
            "numero_pali_x" => ("Pali in X", "nx"), "numero_pali_y" => ("Pali in Y", "ny"),
            "interasse_x" => ("Interasse X", "sx"), "interasse_y" => ("Interasse Y", "sy"),
            "eta_compressione" => ("Efficienza compressione", "ηg,c"), "eta_trazione" => ("Efficienza trazione", "ηg,t"),
            "pressione_iniezione" => ("Pressione di iniezione", "pi = pl"), "inclinazione" => ("Inclinazione dalla verticale", "θ"),
            "inizio_aderenza" => ("Inizio aderenza lungo asse", "sb"), "percentuale_punta" => ("Punta rispetto alla laterale", ""),
            _ => (label, symbol)
        };
    }

    /// <summary>The field with the label and the symbol of <see cref="For"/>; the field itself when it has a symbol.</summary>
    public static FieldSpec Apply(FieldSpec field)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (field.Symbol != "") return field;
        var (label, symbol) = For(field.Key, field.Label, field.Symbol);
        return field with { Label = label, Symbol = symbol };
    }
}
