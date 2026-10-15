using System.Text.Json.Nodes;
using Anthea.Application.Stratigraphy;
using X.Core;
using Anthea.Calculations;

internal static class StratigraphyTransferChecks
{
    internal static int Run()
    {
        int count = 0;
        void Check(bool value) { if (!value) throw new Exception("Trasferimento stratigrafia non coerente"); count++; }
        void Reject(Action action) { try { action(); } catch (ArgumentException) { count++; return; } throw new Exception("Trasferimento non valido accettato"); }
        var data = StratigraphyDocument.Create();
        var row = data["layers"]![0]!.AsObject();
        row["soil_type"] = "Sabbia"; row["source"] = "Relazione pag. 8";
        row["gamma"] = 18; row["phi"] = 30; row["gamma_sat"] = 20; row["c"] = 0;
        row["minimum"] = StratigraphyDocument.NewMinimum(); row["minimum"]!["gamma"] = 17; row["minimum"]!["phi"] = 26;
        data["scenario"] = "Entrambi";
        var packet = SoilProfileTransfer.Extract(StratigraphyDocument.Module, data);
        var copy = SoilProfileTransfer.Create(StratigraphyDocument.Module, packet);
        Check(copy.S("id") != data.S("id"));
        Check(copy["layers"]![0]!["minimum"]!["phi"]!.GetValue<int>() == 26);
        Check(copy["layers"]![0]!["source"]!.ToString() == "Relazione pag. 8");
        copy["layers"]![0]!["phi"] = 10; Check(row["phi"]!.GetValue<int>() == 30);
        Reject(() => StratigraphyTransfer.ToLegacy(packet, "Medi")); // unknown water
        data["water_state"] = "Assegnata"; data["water_depth"] = 1.5;
        packet = SoilProfileTransfer.Extract(StratigraphyDocument.Module, data);
        Reject(() => StratigraphyTransfer.ToLegacy(packet, "Medi")); // behavior not inferred from sand
        row["behavior"] = "Granulare"; row["thickness"] = "3,5";
        packet = SoilProfileTransfer.Extract(StratigraphyDocument.Module, data);
        var means = StratigraphyTransfer.ToLegacy(packet, "Medi");
        var minima = StratigraphyTransfer.ToLegacy(packet, "Minimi");
        Check(means["layers"]![0].D("spessore") == 3.5);
        Check(means["layers"]![0].D("angolo_attrito") == 30 && minima["layers"]![0].D("angolo_attrito") == 26);
        Check(minima["layers"]![0]!["peso_specifico_saturo"] is null);
        Check(means["water"].D("depth") == 1.5);
        Reject(() => StratigraphyTransfer.ToLegacy(packet, "Entrambi"));
        var pile = SoilProfileTransfer.Create("geo_palo_verticale", packet, "Minimi");
        Check(pile["stratigrafie"]![0]![0].D("angolo_attrito") == 26);
        Check(pile["soil_transfer"].S("scenario") == "Minimi");
        var returned = SoilProfileTransfer.Create(StratigraphyDocument.Module, means);
        Check(returned["layers"]![0]!["minimum"] is null);
        Check(returned["layers"]![0].S("soil_type") == "Non classificato");
        row["infinite"] = true; row["thickness"] = null;
        packet = SoilProfileTransfer.Extract(StratigraphyDocument.Module, data);
        Check(StratigraphyDocument.Infinite(SoilProfileTransfer.Create(StratigraphyDocument.Module, packet)["layers"]![0]!.AsObject()));
        Reject(() => StratigraphyTransfer.ToLegacy(packet, "Medi"));
        return count;
    }
}
