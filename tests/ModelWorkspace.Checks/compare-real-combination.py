"""Read-only comparison of GPC display output against an independently acquired MIDAS table."""
import json
import math
from pathlib import Path
import sys

calculated_path, reference_path, output_path = map(Path, sys.argv[1:4])
calculated = json.loads(calculated_path.read_text(encoding="utf-8"))
reference = next(iter(json.loads(reference_path.read_text(encoding="utf-8")).values()))
assert reference["FORCE"].upper() == "N" and reference["DIST"].upper() == "MM"
components = ["Mxx", "Myy", "Mxy", "Fxx", "Fyy", "Fxy", "Vxx", "Vyy"]
columns = {name: i for i, name in enumerate(reference["HEAD"])}
observed = next(r for r in calculated["Results"] if r["Name"].endswith(" (max)"))
values = {(v["Element"], v["Node"]): v["Values"] for v in observed["Values"]}
errors = []
max_error = 0.0
for row in reference["DATA"]:
    key = (int(row[columns["Elem"]]), int(row[columns["Node"]]))
    for i, name in enumerate(components):
        expected = float(row[columns[name]]) * (0.001 if name.startswith("M") else 1.0)
        actual = values[key][i]
        difference = abs(actual - expected)
        max_error = max(max_error, difference)
        if not math.isclose(actual, expected, rel_tol=1e-8, abs_tol=1e-8):
            errors.append([*key, name, expected, actual])
assert len(values) == len(reference["DATA"])
report = dict(reference=str(reference_path), calculation=str(calculated_path),
              samples=len(values), components=len(components), compared=len(values)*len(components),
              maximum_absolute_error=max_error, failures=len(errors), first_failures=errors[:10])
output_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
sys.exit(bool(errors))
