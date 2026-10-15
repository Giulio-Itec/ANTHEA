"""Confronto completo, senza troncamento, di una cattura ui/f3-equivalence con il riferimento F3.

BaselineComparer conserva al più 20000 differenze (MaxStored), comprese le transizioni ammesse: oltre, confronto.json è
troncato e non basta per rigenerare tests/fixtures/f3/transitions-f4.json (tools/qa/f3_transizioni.py lo rifiuta).
Questo programma esegue lo stesso comparatore («ANTHEA.Testing compare», stesse tolleranze) su gruppi di file (una
fixture, il progetto, la Wiki, i file della radice), copiati con lo stesso percorso relativo in una cartella temporanea,
e unisce i rapporti nello stesso formato di confronto.json. Nessuna logica di confronto è reimplementata: differenze,
classi e valori sono quelli del toolkit. Con --transitions le transizioni sono divise per gruppo di file.

Uso (Python -I; la cartella del toolkit conviene copiarla fuori dal checkout, per non bloccare la compilazione):
  python -I tools/qa/f3_confronto_completo.py --a tests/fixtures/f3/expected --b <cattura> --tool <cartella con ANTHEA.Testing.dll>
         --tolerances tests/ANTHEA.Testing/tolerances.json [--transitions tests/fixtures/f3/transitions-f4.json] --out <file.json>
"""
import argparse
import collections
import json
import os
import shutil
import subprocess
import sys
import tempfile

# Escluse dal confronto (file_esclusi di tolerances.json) o scritte dalla cattura dopo il confronto.
SKIPPED = ("manifest.json", "log.txt", "confronto.json", "errore.txt")


def group_of(rel):
    parts = rel.split("/")
    if parts[0] == "sheets" and len(parts) >= 4:
        return "/".join(parts[:3])
    return "." if len(parts) == 1 else parts[0]


def files(root):
    out = []
    for base, _, names in os.walk(root):
        for name in names:
            rel = os.path.relpath(os.path.join(base, name), root).replace("\\", "/")
            if not rel.startswith("raw/") and rel not in SKIPPED:
                out.append(rel)
    return out


def main(argv):
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--a", required=True, help="riferimento (tests/fixtures/f3/expected)")
    parser.add_argument("--b", required=True, help="cattura (…/ui_f3-equivalence/cattura)")
    parser.add_argument("--tool", required=True, help="cartella con ANTHEA.Testing.dll")
    parser.add_argument("--tolerances", required=True)
    parser.add_argument("--transitions")
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    fa, fb = set(files(args.a)), set(files(args.b))
    groups = collections.defaultdict(set)
    for f in fa | fb:
        groups[group_of(f)].add(f)
    by_group = collections.defaultdict(list)
    if args.transitions:
        for t in json.load(open(args.transitions, encoding="utf-8"))["voci"]:
            by_group[group_of(t["file"])].append(t)
    orphans = [t for g, ts in by_group.items() if g not in groups for t in ts]
    merged, per_class, per_entry, volatile = [], collections.Counter(), collections.Counter(), collections.Counter()
    compared = listed = admitted = not_admitted = 0
    work = tempfile.mkdtemp(prefix="f3-completo-")
    try:
        for g in sorted(groups):
            ga, gb = os.path.join(work, "a"), os.path.join(work, "b")
            shutil.rmtree(ga, ignore_errors=True)
            shutil.rmtree(gb, ignore_errors=True)
            os.makedirs(ga)
            os.makedirs(gb)
            for f in groups[g]:
                for src, dst, present in ((args.a, ga, fa), (args.b, gb, fb)):
                    if f in present:
                        target = os.path.join(dst, f)
                        os.makedirs(os.path.dirname(target), exist_ok=True)
                        shutil.copyfile(os.path.join(src, f), target)
            report = os.path.join(work, "confronto.json")
            command = ["dotnet", os.path.join(args.tool, "ANTHEA.Testing.dll"), "compare", ga, gb, "--tolerances", args.tolerances,
                       "--report", report, "--max", "0"]
            if by_group.get(g):
                listing = os.path.join(work, "transizioni.json")
                with open(listing, "w", encoding="utf-8") as handle:
                    json.dump({"voci": by_group[g]}, handle, ensure_ascii=False)
                command += ["--transitions", listing]
            done = subprocess.run(command, capture_output=True, text=True, encoding="utf-8")
            if done.returncode not in (0, 1) or not os.path.exists(report):
                raise SystemExit(f"Confronto del gruppo {g} non riuscito ({done.returncode}):\n{done.stdout}\n{done.stderr}")
            r = json.load(open(report, encoding="utf-8"))
            os.remove(report)
            if r["dettagli_troncati"]:
                raise SystemExit(f"Gruppo {g} troncato: oltre {len(r['differenze'])} differenze in un solo gruppo.")
            compared += r["file_confrontati"]
            not_admitted += r["differenze_non_ammesse"]
            per_class.update(r["per_classe"])
            volatile.update(r["volatili"])
            listed += r["transizioni"]["elencate"]
            admitted += r["transizioni"]["ammesse"]
            per_entry.update(r["transizioni"]["per_voce"])
            merged.extend(r["differenze"])
    finally:
        shutil.rmtree(work, ignore_errors=True)
    for t in orphans:
        merged.append({"file": t["file"], "percorso": t["percorso"], "classe": "transizione-assente", "ammessa": False, "a": t.get("a"),
                       "b": t.get("b"), "delta": None, "tolleranza": None, "nota": "attesa da " + t["voce"] + " come " + t["classe"]})
        not_admitted += 1
        per_class["transizione-assente"] += 1
    merged.sort(key=lambda d: d["ammessa"])  # stabile: prima le non ammesse, per file come il comparatore
    out = {
        "a": os.path.abspath(args.a), "b": os.path.abspath(args.b), "tolleranze": args.tolerances, "file_confrontati": compared,
        "esito": "uguali entro le tolleranze" if not_admitted == 0 else "differenze non ammesse",
        "differenze_non_ammesse": not_admitted, "per_classe": dict(sorted(per_class.items())), "volatili": dict(volatile),
        "transizioni": {"elencate": listed + len(orphans), "ammesse": admitted, "per_voce": dict(sorted(per_entry.items()))},
        "manifest": {"nota": "confronto per gruppi di file (tools/qa/f3_confronto_completo.py)"},
        "dettagli_troncati": False, "differenze": merged,
    }
    with open(args.out, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(out, handle, ensure_ascii=False, indent=1)
    print(f"{compared} file confrontati in {len(groups)} gruppi; non ammesse {not_admitted}; transizioni elencate "
          f"{listed + len(orphans)}, ammesse {admitted}")
    return 0 if not_admitted == 0 else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
