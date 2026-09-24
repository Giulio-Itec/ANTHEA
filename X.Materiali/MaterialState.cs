using System.Text.Json.Nodes;
using System.Windows.Controls;

namespace Materiali;

public sealed partial class MaterialView
{
    public event Action? Modified;
    private bool restoring;
    private JsonObject? lastState;
    private JsonArray? legacyExposures;
    private Dictionary<string, CheckBox> Flags => new()
    {
        ["highStrength"] = highStrength, ["slab"] = slab, ["quality"] = quality,
        ["rough"] = rough, ["ntcQuality"] = ntcQuality
    };

    public JsonObject CaptureState()
    {
        var state = new JsonObject { ["versione_materiali"] = 1, ["classe"] = choice.SelectedItem?.ToString(),
            ["esposizione_principale"] = exposureSelector.SelectedItem?.ToString() };
        var values = new JsonObject(); foreach (var (key, box) in numbers) values[key] = box.Text;
        var selections = new JsonObject(); foreach (var (key, box) in choices) selections[key] = box.SelectedItem?.ToString();
        var flags = new JsonObject(); foreach (var (key, box) in Flags) flags[key] = box.IsChecked == true;
        var exposures = new JsonArray(exposureSelector.SelectedItem?.ToString());
        if (legacyExposures is not null) state["esposizioni_precedenti"] = legacyExposures.DeepClone();
        state["numeri"] = values; state["scelte"] = selections; state["opzioni"] = flags; state["esposizioni"] = exposures;
        return state;
    }

    public void RestoreState(JsonObject state)
    {
        legacyExposures = state["esposizioni_precedenti"]?.DeepClone() as JsonArray;
        if (state["esposizioni"] is JsonArray old && old.Count > 1) legacyExposures = (JsonArray)old.DeepClone();
        restoring = true; detailsReady = false; updatingExposure = true;
        try
        {
            if (state["classe"] is JsonValue cls)
            {
                var name = cls.GetValue<string>();
                if (!choice.Items.Contains(name)) throw new ArgumentException("Classe del calcestruzzo non riconosciuta.");
                choice.SelectedItem = name;
            }
            if (state["numeri"] is JsonObject values)
                foreach (var (key, box) in numbers) if (values[key] is JsonValue v) box.Text = v.GetValue<string>();
            void Selection(string key)
            {
                if (state["scelte"]?[key] is not JsonValue v) return;
                string value = v.GetValue<string>(); var box = choices[key];
                if (!box.Items.Contains(value)) throw new ArgumentException("Scelta materiali non valida: " + key);
                box.SelectedItem = value;
            }
            foreach (string key in choices.Keys.Where(k => k != "deviationValue")) Selection(key);
            UpdateDeviation(); Selection("deviationValue");
            if (state["opzioni"] is JsonObject flags)
                foreach (var (key, box) in Flags) if (flags[key] is JsonValue v) box.IsChecked = v.GetValue<bool>();
            if (state["esposizioni"] is JsonArray exposures)
            {
                var codes = exposures.Select(v => v!.GetValue<string>()).ToHashSet();
                if (codes.Any(c => !exposureChecks.ContainsKey(c))) throw new ArgumentException("Classe di esposizione non riconosciuta.");
                foreach (var (key, box) in exposureChecks) box.IsChecked = codes.Contains(key);
            }
            string selected = state["esposizione_principale"]?.ToString() ?? (state["esposizioni"] as JsonArray)?.FirstOrDefault()?.ToString() ?? "XC1";
            if (!exposureChecks.ContainsKey(selected)) throw new ArgumentException("Classe di esposizione non riconosciuta.");
            exposureSelector.SelectedItem = selected;
            foreach (var (key, box) in exposureChecks) box.IsChecked = key == selected;
        }
        finally { restoring = false; detailsReady = true; updatingExposure = false; }
        Refresh(); lastState = CaptureState();
    }

    private void AttachStateEvents()
    {
        void Changed()
        {
            if (restoring || updatingExposure || updatingDeviation) return;
            var current = CaptureState();
            if (JsonNode.DeepEquals(current, lastState)) return;
            lastState = current; Modified?.Invoke();
        }
        foreach (var box in numbers.Values) box.TextChanged += (_, _) => Changed();
        foreach (var box in choices.Values.Append(choice).Append(exposureSelector)) box.SelectionChanged += (_, _) => Changed();
        foreach (var box in Flags.Values.Concat(exposureChecks.Values))
        { box.Checked += (_, _) => Changed(); box.Unchecked += (_, _) => Changed(); }
        lastState = CaptureState();
    }
}
