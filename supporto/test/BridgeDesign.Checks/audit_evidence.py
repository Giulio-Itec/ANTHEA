"""Summarize the frozen 1,000-site-case regression and audit artifacts without recapturing the site."""
import collections
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[3]
out = root / "supporto/artefatti/bridge_design_general_audit"
old_path = root / "supporto/artefatti/bridge_design_site_1000/engine-results.jsonl"
new_path = out / "regression-2000.jsonl"
def read(path):
    return {(v["id"], v["mode"]): v for v in map(json.loads, path.read_text(encoding="utf-8-sig").splitlines())}
old, new = read(old_path), read(new_path)
assert old.keys() == new.keys() and len(new) == 2000
changes = collections.Counter()
inertia_changes = []
for key, record in new.items():
    before = old[key]
    changes[before["status"] + " -> " + record["status"]] += 1
    if before["status"] != "calculated" or record["status"] != "calculated":
        continue
    a, b = before["result"], record["result"]
    for field in ["Width", "Depth", "PierDepth", "Spans", "Steel", "Inertia", "Concrete", "TotalCost"]:
        if a[field] != b[field]:
            changes["changed_" + field] += 1
            if field == "Inertia": inertia_changes.append(key)
assert not any(v["status"] == "nonfinite" for v in new.values())
assert all(changes["changed_" + k] == 0 for k in ["Width", "Depth", "PierDepth", "Spans", "Steel"])
case_inputs = read(root / "supporto/artefatti/bridge_design_site_1000/engine-inputs.jsonl")
assert all(case_inputs[k]["data"]["input"]["family"] == "psc_u" for k in inertia_changes)
cases = json.loads((out / "calcoli_finali/audit-cases.json").read_text(encoding="utf-8"))
optimization = json.loads((out / "calcoli_finali/audit-optimization.json").read_text(encoding="utf-8"))
summary = {
    "regression": dict(changes), "nonfinite": 0,
    "cases": len(cases), "calculated": sum(c["status"] == "calculated" for c in cases),
    "rejections": dict(collections.Counter(c["reason"] for c in cases if c["status"] == "rejected")),
    "optimization_runs": len(optimization), "optimization_evaluations": sum(c["Evaluated"] for c in optimization),
    "source_sha256": {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in [old_path, new_path, *sorted((out / "fonti").glob("*.pdf"))]},
}
(out / "audit-summary.json").write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")
print(json.dumps(summary, indent=2, ensure_ascii=False))
