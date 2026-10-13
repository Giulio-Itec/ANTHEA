using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using GPC.Checkers.Concrete.Serviceability;
using GPC.Checkers.Concrete.Shear;

/// <summary>Opt-in candidate comparison in a test-only copy; lib/Checker remains untouched.</summary>
internal static class F4WallPortChecks
{
    internal static int Run(string root, string reference, string output)
    {
        var errors = new List<string>(); int checks = 0, sections = 0, cases = 0, walls = 0, uncrackedCount = 0, shearChecks = 0, shearChanged = 0;
        double maximumError = 0;
        try
        {
            var assembly = typeof(SectionShearCalculator).Assembly;
            Type Api(string name) => assembly.GetType("GPC.Checkers.Concrete.Walls." + name, true)!;
            Type stripType = Api("WallStrip"), memberType = Api("WallMember"), sessionType = Api("WallSectionSession"), actionsType = Api("WallSectionActions");
            bool hasShear = sessionType.GetMethod("CheckShear") is not null;
            var shearReference = new Dictionary<(string, string, string, double), JsonObject>();
            if (hasShear)
            {
                byte[] study = File.ReadAllBytes(Path.Combine(root, "supporto/artefatti/refactoring/f4/wall-sigma-study/independent.json"));
                if (Convert.ToHexString(SHA256.HashData(study)) != "4D6837431389ED7D0C71D8206047961B1D81C55EBE580A86A446C2FB223B5F8C") throw new InvalidOperationException("Independent shear expectations changed");
                foreach (var r in JsonNode.Parse(study)!["rows"]!.AsArray().OfType<JsonObject>())
                    shearReference.Add((r.S("case"), r.S("combination"), r.S("member"), r.D("position_m")), r);
            }
            var index = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f4/reference.json")))!;
            var hashes = index["files"]!.AsArray().ToDictionary(f => f!["file"]!.GetValue<string>(), f => f!["sha256"]!.GetValue<string>());
            JsonObject Read(string relative)
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(reference, relative));
                if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hashes[relative], StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Reference changed: " + relative);
                return JsonNode.Parse(bytes)!.AsObject();
            }
            void Same(double? expected, double? actual, string key)
            {
                checks++;
                if (expected.HasValue != actual.HasValue) { errors.Add(key + ": availability differs"); return; }
                if (!expected.HasValue) return;
                double delta = Math.Abs(expected.Value - actual!.Value); maximumError = Math.Max(maximumError, delta);
                if (!double.IsFinite(actual.Value) || delta > 1e-9 + 1e-9 * Math.Max(Math.Abs(expected.Value), Math.Abs(actual.Value))) errors.Add($"{key}: {expected:R} -> {actual:R}");
            }
            foreach (string file in Directory.GetFiles(Path.Combine(reference, "results/walls"), "*.json").Order(StringComparer.Ordinal))
            {
                string id = Path.GetFileName(file);
                var original = Read("inputs/walls/" + id); var captured = Read("results/walls/" + id);
                if (captured.S("status") != "ok" || original.S("family") == "gravity") continue;
                walls++; string before = original.ToJsonString(); var value = captured["value"]!;
                var cache = new Dictionary<(string, double), object>();
                var expectedChecks = value["Structural"]!.AsArray().OfType<JsonObject>().ToArray();
                foreach (var c in value["Cases"]!.AsArray().OfType<JsonObject>())
                {
                    cases++;
                    foreach (var f in c["Sections"]!.AsArray().OfType<JsonObject>())
                    {
                        string member = f.S("Name"); double position = f.D("Position"), thickness = f.D("Thickness");
                        var expected = expectedChecks.Where(x => x.S("Combination") == c.S("Name") && x.S("Member") == member && x.D("Position") == position).ToArray();
                        if (expected.Length == 0) continue;
                        string memberKey = RetainingWall.ReinforcementKey(original, member, position);
                        if (!cache.TryGetValue((memberKey, thickness), out object? session))
                        {
                            var input = RetainingWall.SectionInput(original, member, thickness, position);
                            var document = SezioneCA.DefaultData(); document["input"] = input.DeepClone(); var workspace = SectionWorkspace.Prepare(document);
                            var prepared = CheckerSection.PrepareModel(input, workspace);
                            var strip = Activator.CreateInstance(stripType, Enum.Parse(memberType, member == "Fusto" ? "Stem" : member == "Valle" ? "Toe" : "Heel"),
                                1000d, thickness * 1000, original["materials"].D("cover"), (int)input.D("top_bar_count"), input.D("top_bar_diameter_mm"),
                                (int)input.D("bottom_bar_count"), input.D("bottom_bar_diameter_mm"));
                            session = Activator.CreateInstance(sessionType, strip, prepared.Section, ConcreteStandards.Effective(input, workspace),
                                original["materials"].D("creep"), original["materials"].S("exposure"))!;
                            cache.Add((memberKey, thickness), session); sections++;
                        }
                        dynamic native = session;
                        dynamic action = Activator.CreateInstance(actionsType, -f.D("N") * 1000, f.D("V") * 1000, f.D("M") * 1e6)!;
                        string state = c.S("State"); var observed = new Dictionary<string, object>();
                        if (state is "SLU" or "SISMA" or "ECCEZIONALE")
                        {
                            observed["N–M GPC"] = native.CheckMoment(action, CancellationToken.None);
                            if (hasShear)
                            {
                                dynamic shear = native.CheckShear(action, CancellationToken.None);
                                var independent = shearReference[(Path.GetFileNameWithoutExtension(id), c.S("Name"), member, position)];
                                var predicted = J.Number(independent["predicted_kn_per_m"]);
                                Same(predicted, ((double?)shear.Check.Capacity) / 1000, id + "/shear-independent");
                                Same(independent.D("demand_kn_per_m"), (double)shear.Check.Demand / 1000, id + "/shear-demand");
                                shearChecks++;
                                if (Math.Abs(independent.D("delta_kn_per_m")) > 1e-9) shearChanged++;
                            }
                        }
                        else
                        {
                            var combination = state == "SLE" ? ServiceabilityCombination.Characteristic : state == "SLE_FREQ" ? ServiceabilityCombination.Frequent : ServiceabilityCombination.QuasiPermanent;
                            dynamic response = native.CheckServiceability(action, combination, CancellationToken.None);
                            if ((bool)response.ProvenUncracked) uncrackedCount++;
                            foreach (dynamic check in response.Checks)
                            {
                                string? suffix = (string)check.Method switch { "Wall.ConcreteStress" => "tensione CLS", "Wall.SteelStress" => "tensione acciaio", "Wall.Cracking" => "fessurazione", _ => null };
                                if (suffix is null) errors.Add(id + ": unexpected service result " + check.Method + ": " + check.Diagnostic);
                                else observed[suffix] = check;
                            }
                            if (member == "Fusto")
                            {
                                double y = original["geometry"].D("height") - position;
                                var curve = c["Curvatures"]!.AsArray().OfType<JsonObject>().Single(x => x.D("Y") == y);
                                Same(curve.D("Curvature"), ((double?)response.Curvature) * 1000, id + "/curvature/" + position);
                            }
                            bool expectedUncracked = expected.Any(x => x.S("Status").StartsWith("Non fessurata:", StringComparison.Ordinal));
                            if (expectedUncracked != (bool)response.ProvenUncracked) errors.Add(id + ": uncracked proof differs");
                        }
                        foreach (var old in expected)
                        {
                            string? suffix = new[] { "N–M GPC", "tensione CLS", "tensione acciaio", "fessurazione" }.FirstOrDefault(s => old.S("Name").EndsWith(" · " + s, StringComparison.Ordinal));
                            if (suffix is not null && !observed.ContainsKey(suffix)) errors.Add(id + ": missing check " + suffix);
                        }
                        foreach (var item in observed)
                        {
                            dynamic check = item.Value;
                            var old = expected.SingleOrDefault(x => x.S("Name").EndsWith(" · " + item.Key, StringComparison.Ordinal));
                            if (old is null) { errors.Add(id + ": unexpected check " + item.Key); continue; }
                            double factor = (string)check.Unit == "Nmm" ? 1e6 : (string)check.Unit == "N" ? 1000 : 1;
                            string key = id + "/" + c.S("Name") + "/" + member + "/" + position + "/" + item.Key;
                            Same(old.D("Demand"), (double)check.Demand / factor, key + "/demand");
                            Same(J.Number(old["Resistance"]), ((double?)check.Capacity) / factor, key + "/capacity");
                            Same(J.Number(old["Ratio"]), (double?)check.Ratio, key + "/ratio");
                        }
                    }
                }
                if (original.ToJsonString() != before) throw new InvalidOperationException(id + ": input mutated");
                Console.WriteLine($"{id}: {errors.Count} differences accumulated");
            }
            if (walls != 26 || sections == 0 || checks == 0) errors.Add("Incomplete wall corpus");
            if (hasShear && (shearChecks != 12512 || shearChanged != 4192)) errors.Add("Incomplete independent shear comparison");
            var report = new { walls, sections, cases, checks, shearChecks, shearChanged, uncrackedCount, maximumError, differences = errors.Count, errors = errors.Take(100), candidate = assembly.FullName };
            File.WriteAllText(Path.Combine(output, "comparison.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(JsonSerializer.Serialize(report)); return errors.Count == 0 ? 0 : 1;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
