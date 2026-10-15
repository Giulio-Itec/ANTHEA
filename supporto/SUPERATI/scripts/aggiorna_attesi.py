"""Regenerates the expected values of some comparison cases from the C# results.

Use it only when a behaviour of ANTHEA is deliberately different from the original Python program:
1. dotnet run --project supporto/test/X.Verifiche -c Release -- --attesi supporto/test/casi_confronto.json "<filter>" <results.json>
2. python supporto/scripts/aggiorna_attesi.py supporto/test/casi_confronto.json <results.json> "<note>"

The key structure of each expected value is kept (only the values change); a subtree whose structure differs is replaced as a whole.
Every regenerated case gets the field "fonte_atteso" with the note. The file keeps its compact format, so the other cases are unchanged.
"""
import json
import sys


def merge(expected, actual):
    """The expected structure with the actual values."""
    if isinstance(expected, dict) and isinstance(actual, dict) and all(k in actual for k in expected):
        return {k: merge(v, actual[k]) for k, v in expected.items()}
    if isinstance(expected, list) and isinstance(actual, list) and len(expected) == len(actual):
        return [merge(e, a) for e, a in zip(expected, actual)]
    if isinstance(expected, float) and isinstance(actual, int) and not isinstance(actual, bool):
        return float(actual)  # the C# JSON writes 16 where Python wrote 16.0: keep the type of the reference
    return actual


def main(cases_path, results_path, note):
    raw = open(cases_path, "rb").read()
    cases = json.loads(raw.decode("utf-8"))
    results = json.load(open(results_path, encoding="utf-8-sig"))
    updated = 0
    for case in cases:
        if case["nome"] in results:
            case["atteso"] = merge(case["atteso"], results[case["nome"]])
            case["fonte_atteso"] = note
            updated += 1
    text = json.dumps(cases, separators=(",", ":"), ensure_ascii=False)
    if raw.endswith(b"\n"):
        text += "\n"
    open(cases_path, "wb").write(text.encode("utf-8"))
    print(f"{updated} casi aggiornati su {len(results)} risultati")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], sys.argv[3])
