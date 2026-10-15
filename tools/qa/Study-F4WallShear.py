"""Independent NTC 2018 4.1.23 study; never calls or replaces production code.

Reads the frozen F4 corpus, checks the legacy zero-axial formula, and measures
the isolated sigma_cp contribution. Does not certify the future wall adapter.
Creates a new evidence file only; independent expectations are never rewritten.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path


def resistance(bw, depth, steel, fck, gamma, sigma):
    k = min(2.0, 1 + math.sqrt(200 / depth))
    rho = min(0.02, steel / (bw * depth))
    return max(0.18 / gamma * k * (100 * rho * fck) ** (1 / 3),
               0.035 * k ** 1.5 * math.sqrt(fck)) * bw * depth + 0.15 * sigma * bw * depth


def study(root):
    rows, excluded, hashes = [], [], {}
    for path in sorted((root / "results/walls").glob("*.json")):
        input_path = root / "inputs/walls" / path.name
        for source in (path, input_path):
            hashes[str(source.relative_to(root))] = hashlib.sha256(source.read_bytes()).hexdigest()
        result = json.loads(path.read_text(encoding="utf-8-sig"))
        doc = json.loads(input_path.read_text(encoding="utf-8-sig"))
        if result["status"] != "ok":
            excluded.append({"case": path.stem, "reason": result["status"]})
            continue
        if doc["family"] != "cantilever":
            excluded.append({"case": path.stem, "reason": "outside reinforced concrete wall scope"})
            continue
        material = doc["materials"]
        fck, gamma = float(material["fck"]), float(material["gamma_c"])
        fcd = float(material["alpha_cc"]) * fck / gamma
        # This bank is ordinary concrete; any future thin-cast case must first
        # define its material resistance explicitly, not silently reuse this.
        forces = {(case["Name"], force["Name"], force["Position"]): force
                  for case in result["value"]["Cases"] for force in case["Sections"]}
        checks = [c for c in result["value"]["Structural"] if c["Name"].endswith("taglio senza staffe")]
        if not checks:
            excluded.append({"case": path.stem, "reason": "no completed shear checks"})
        for check in checks:
            force = forces[(check["Combination"], check["Member"], check["Position"])]
            assert force["Thickness"] >= 0.05, "Thin-cast material contract requires separate treatment"
            key = "stem" if force["Name"] == "Fusto" else "toe" if force["Name"] == "Valle" else "heel"
            if key == "stem" and doc["reinforcement"]["two_zones"] and force["Position"] < doc["geometry"]["height"] - doc["reinforcement"]["lower_height"] - 1e-9:
                key = "stem_upper"
            bar = doc["reinforcement"][key]
            prefix = "opposite_" if force["M"] < 0 and not bar["symmetric"] else ""
            diameter, count = bar[prefix + "diameter"], bar[prefix + "count"]
            depth = force["Thickness"] * 1000 - float(material["cover"]) - diameter / 2
            steel = count * math.pi * diameter ** 2 / 4
            legacy = resistance(1000, depth, steel, fck, gamma, 0) / 1000
            assert math.isclose(legacy, check["Resistance"], rel_tol=1e-10, abs_tol=1e-9), (path.name, check, legacy)
            assert math.isclose(abs(force["V"]), check["Demand"], rel_tol=1e-10, abs_tol=1e-9)
            # Wall force: kN per metre, compression positive. Area of a 1 m strip.
            sigma = min(force["N"] / (force["Thickness"] * 1000), 0.2 * fcd)
            predicted = None if force["N"] < 0 else resistance(1000, depth, steel, fck, gamma, sigma) / 1000
            rows.append({"case": path.stem, "combination": check["Combination"], "member": check["Member"],
                         "position_m": check["Position"], "n_kn_per_m": force["N"], "sigma_mpa": sigma,
                         "demand_kn_per_m": check["Demand"], "legacy_kn_per_m": legacy,
                         "predicted_kn_per_m": predicted,
                         "delta_kn_per_m": None if predicted is None else predicted - legacy,
                         "legacy_satisfied": check["Demand"] <= legacy,
                         "predicted_satisfied": None if predicted is None else check["Demand"] <= predicted})
    assert rows, "No captured shear results"
    numeric = [r for r in rows if r["predicted_kn_per_m"] is not None]
    changed = [r for r in numeric if abs(r["delta_kn_per_m"]) > 1e-9]
    vectors = [{"n_N": n, "sigma_mpa": min(-n / 400000, 3.4),
                "expected_N": None if n > 0 else resistance(1000, 350, 1500, 30, 1.5, min(-n / 400000, 3.4))}
               for n in (0, -400000, -1360000, -2000000, 40000)]
    return {"kind": "independent formula study, not production wall adapter validation",
            "source": "NTC 2018 4.1.2.3.5.1, equation 4.1.23",
            "summary": {"cases": len(set(r["case"] for r in rows)), "checks": len(rows),
                        "changed_capacity": len(changed), "tension_declined": len(rows) - len(numeric),
                        "changed_verdict": sum(r["legacy_satisfied"] != r["predicted_satisfied"] for r in numeric),
                        "max_delta_kn_per_m": max((r["delta_kn_per_m"] for r in numeric), default=0),
                        "max_delta_percent": max((100*r["delta_kn_per_m"]/r["legacy_kn_per_m"] for r in numeric), default=0)},
            "synthetic_vectors": vectors, "excluded": excluded, "input_sha256": hashes, "rows": rows}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("capture", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    report = study(args.capture)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("x", encoding="utf-8") as stream:
        json.dump(report, stream, ensure_ascii=False, indent=2, allow_nan=False)
    print(json.dumps(report["summary"], ensure_ascii=False))
    print(json.dumps(report["excluded"], ensure_ascii=False))
