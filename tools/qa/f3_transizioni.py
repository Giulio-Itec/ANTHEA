"""Elenco delle transizioni ammesse del confronto UI F3 e aggiornamento editoriale della Wiki (decisione D2 del 15/10/2026).

Dal confronto di una cattura (confronto.json del runner, ui/f3-equivalence) scrive:
  - tests/fixtures/f3/transitions-f4.json: una voce per differenza non ammessa fuori dalla Wiki, con file, percorso, classe,
    valore prima e dopo e voce del registro. La voce viene dalla classificazione puntuale delle 6805 differenze
    (classificazione.tsv, stessa chiave); le differenze nuove ricevono la voce solo da regole esplicite di questo file,
    altrimenti il programma si ferma.
  - i file della Wiki e della ricerca con differenze: copiati dalla cattura in tests/fixtures/f3/expected, con le impronte
    prima e dopo nel manifest (editorial_updates), come F3-06.
Non legge né scrive risultati ingegneristici: i valori «prima» restano quelli del riferimento F3.

Con --voci rigenera l'elenco intero (correzioni dei bug del 15-16/10/2026): il confronto è quello completo di una corsa
senza transizioni (tools/qa/f3_confronto_completo.py), il TSV ha una riga per ogni sua differenza non ammessa, nello stesso
ordine, con la voce del registro (classificazione in docs/refactoring/bug-1015-transizioni-ui-classificazione.md). «a» e «b»
vengono dal confronto, cioè valore del riferimento e valore nuovo. Nel manifest cambiano solo impronta e numero delle voci
di «transitions»; le differenze della Wiki fermano il programma (prima l'aggiornamento editoriale, regola F3-06).
"""
import argparse
import collections
import csv
import hashlib
import json
import os
import re
import shutil
import sys

DECISIONE = ("D2 del 15/10/2026 (docs/refactoring/decisioni.md): transizioni ammesse una per una, per file e percorso, con valore "
             "prima e dopo e voce del registro; ogni altra differenza, e ogni transizione elencata che non avviene, fa fallire "
             "ui/f3-equivalence. Nessuna tolleranza nuova. Wiki e ricerca: aggiornamento editoriale nel manifest (F4-48).")

csv.field_size_limit(10_000_000)


def sha(path):
    """Impronta del contenuto con fine riga LF, cioè della forma conservata da git (text=auto): uguale in ogni checkout."""
    with open(path, "rb") as handle:
        return hashlib.sha256(handle.read().replace(b"\r\n", b"\n")).hexdigest()


def key(d):
    return (d["file"], d["percorso"], d["classe"], d.get("a"), d.get("b"))


def new_entry(d):
    """Regole per le differenze misurate nel full del 14/10 sera e assenti dalla classificazione di 82fcd8fc."""
    f, p, c, a, b = d["file"], d["percorso"], d["classe"], d.get("a") or "", d.get("b") or ""
    if f.endswith("report-structure.json") and (f.startswith("sheets/str_palo/") or f == "project/report-structure.json"):
        return "F4-40"  # la nota della norma tolta sposta gli stili successivi
    if f.endswith("report.txt") and c == "riga-rimossa" and "NTC 2018 · coefficienti della DLL" in a:
        return "F4-40"
    if f.endswith("report.txt") and c == "testo" and "ATECAP" in a and "ATECAP" not in b:
        return "F4-47"
    if f.startswith("sheets/geo_fondazioni_superficiali/") and c == "numero" and a == "0.1.3.3" and b == "0.3.0.1":
        return "F4-35"
    if f == "project/report.txt" and c == "riga-aggiunta" and p.endswith("(b)"):
        return "F3-11"  # stesse righe M–χ di F3.4-U5, numerate una riga prima dopo la riga tolta da F4-40
    return None


def main(argv):
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--confronto", required=True)
    parser.add_argument("--classificazione", help="classificazione.tsv, una riga per differenza del confronto classificato")
    parser.add_argument("--confronto-classificato",
                        help="confronto.json della cattura classificata (82fcd8fc): stesse righe, stesso ordine del TSV, valori esatti")
    parser.add_argument("--voci", help="rigenerazione: TSV (file, percorso, classe, voce) di tutte le differenze del confronto completo")
    parser.add_argument("--commit", help="commit della cattura (summary.json della corsa), con --voci")
    parser.add_argument("--radice", required=True, help="checkout di ANTHEA")
    parser.add_argument("--registro-cattura", required=True, help="cartella della cattura, per la provenienza")
    args = parser.parse_args(argv)
    report = json.load(open(args.confronto, encoding="utf-8"))
    if report.get("dettagli_troncati"):
        raise SystemExit("Confronto troncato: elenco impossibile.")
    if args.voci:
        return regenerate(args, report)
    if not args.classificazione or not args.confronto_classificato:
        parser.error("servono --classificazione e --confronto-classificato, oppure --voci")
    capture = os.path.dirname(os.path.abspath(args.confronto))
    expected = os.path.join(args.radice, "tests", "fixtures", "f3", "expected")
    # The TSV rewrote the values for reading; the exact values come from the classified comparison, row by row in the same order.
    known = collections.defaultdict(list)
    with open(args.classificazione, encoding="utf-8", newline="") as handle:
        rows = list(csv.DictReader(handle, delimiter="\t"))
    classified = [d for d in json.load(open(args.confronto_classificato, encoding="utf-8"))["differenze"] if not d["ammessa"]]
    if len(rows) != len(classified):
        raise SystemExit(f"Classificazione con {len(rows)} righe per {len(classified)} differenze.")
    for row, d in zip(rows, classified):
        if (row["file"], row["percorso"], row["classe"]) != (d["file"], d["percorso"], d["classe"]) or not row["voce"]:
            raise SystemExit("Classificazione non allineata al confronto: " + row["file"] + " " + row["percorso"])
        known[key(d)].append(row["voce"])
    entries, wiki, unknown, sources = [], set(), [], collections.Counter()
    for d in report["differenze"]:
        if d["ammessa"]:
            continue
        if d["file"].startswith("wiki/"):
            wiki.add(d["file"])
            continue
        listed = known.get(key(d))
        voce = listed.pop(0) if listed else new_entry(d)
        sources["classificazione" if listed is not None else "regola"] += 1
        if not voce:
            unknown.append(d)
            continue
        entries.append({"file": d["file"], "percorso": d["percorso"], "classe": d["classe"], "a": d.get("a"), "b": d.get("b"), "voce": voce})
    if unknown:
        for d in unknown[:20]:
            print("SENZA VOCE", d["file"], d["percorso"], d["classe"], str(d.get("a"))[:80], "->", str(d.get("b"))[:80])
        raise SystemExit(f"{len(unknown)} differenze senza voce: nessun file scritto.")
    target = os.path.join(args.radice, "tests", "fixtures", "f3", "transitions-f4.json")
    document = {
        "schema": 1,
        "decisione": DECISIONE,
        "cattura": args.registro_cattura,
        "commit": (report.get("manifest") or {}).get("commit", {}).get("b"),
        "per_voce": dict(sorted(collections.Counter(e["voce"] for e in entries).items())),
        "voci": entries,
    }
    with open(target, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(document, handle, ensure_ascii=False, indent=1)
        handle.write("\n")
    updates = []
    for f in sorted(wiki):
        before, after = os.path.join(expected, f), os.path.join(capture, f)
        updates.append({"file": f, "before_sha256": sha(before), "after_sha256": sha(after)})
        shutil.copyfile(after, before)
    # Two keys appended at the end of the manifest, the rest of the file byte for byte unchanged (CRLF, two spaces).
    manifest_path = os.path.join(args.radice, "tests", "fixtures", "f3", "manifest.json")
    raw = open(manifest_path, "rb").read().decode("utf-8")
    manifest = json.loads(raw)
    if "editorial_updates_f4" in manifest or "transitions" in manifest:
        raise SystemExit("Manifest già aggiornato: ripristinarlo da git prima di rigenerare.")
    added = {
        "editorial_updates_f4": {
            "revision": 44, "record": "F4-48", "capture": args.registro_cattura,
            "reason": "Guide Rev41-Rev44 nella Wiki e nella ricerca (F4-39, F4-43…F4-47, punto 7 dell'audit); decisione D2 del "
                      "15/10/2026. Nessun atteso ingegneristico modificato.",
            "files": updates,
        },
        "transitions": {"file": "tests/fixtures/f3/transitions-f4.json", "sha256": sha(target), "entries": len(entries),
                        "decision": "D2 del 15/10/2026"},
    }
    body = raw.rstrip()
    if not body.endswith("}"):
        raise SystemExit("Manifest inatteso.")
    fragment = "".join(",\r\n  " + json.dumps(k) + ": " + json.dumps(v, ensure_ascii=False, indent=2).replace("\n", "\r\n  ")
                       for k, v in added.items())
    with open(manifest_path, "wb") as handle:
        handle.write((body[:-1].rstrip() + fragment + "\r\n}" + raw[len(body):]).encode("utf-8"))
    print(f"{len(entries)} transizioni in {len(set(e['file'] for e in entries))} file; voci {document['per_voce']}; origine {dict(sources)}")
    print(f"{len(updates)} file della Wiki aggiornati nel riferimento.")
    return 0


def regenerate(args, report):
    """Elenco intero da un confronto completo senza transizioni e dalla voce di ogni differenza (TSV nello stesso ordine)."""
    if (report.get("transizioni") or {}).get("elencate"):
        raise SystemExit("Il confronto va fatto senza transizioni: ogni differenza riceve la sua voce dal TSV.")
    diffs = [d for d in report["differenze"] if not d["ammessa"]]
    wiki = sorted({d["file"] for d in diffs if d["file"].startswith("wiki/")})
    if wiki:
        raise SystemExit(f"{len(wiki)} file della Wiki con differenze: prima l'aggiornamento editoriale (F3-06), poi l'elenco.")
    with open(args.voci, encoding="utf-8", newline="") as handle:
        rows = list(csv.DictReader(handle, delimiter="\t", quoting=csv.QUOTE_NONE))
    if len(rows) != len(diffs):
        raise SystemExit(f"Classificazione con {len(rows)} righe per {len(diffs)} differenze.")
    entries = []
    for row, d in zip(rows, diffs):
        if (row["file"], row["percorso"], row["classe"]) != (d["file"], d["percorso"], d["classe"]):
            raise SystemExit("Classificazione non allineata al confronto: " + row["file"] + " " + row["percorso"])
        if not row["voce"] or "NON SPIEGATA" in row["voce"]:
            raise SystemExit("Differenza senza voce: " + row["file"] + " " + row["percorso"])
        entries.append({"file": d["file"], "percorso": d["percorso"], "classe": d["classe"], "a": d.get("a"), "b": d.get("b"), "voce": row["voce"]})
    target = os.path.join(args.radice, "tests", "fixtures", "f3", "transitions-f4.json")
    document = {
        "schema": 1,
        "decisione": DECISIONE,
        "cattura": args.registro_cattura,
        "commit": args.commit,
        "per_voce": dict(sorted(collections.Counter(e["voce"] for e in entries).items())),
        "voci": entries,
    }
    with open(target, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(document, handle, ensure_ascii=False, indent=1)
        handle.write("\n")
    # Only the fingerprint and the count of "transitions" change; the rest of the manifest stays byte for byte.
    manifest_path = os.path.join(args.radice, "tests", "fixtures", "f3", "manifest.json")
    raw = open(manifest_path, "rb").read().decode("utf-8")
    block = re.search(r'"transitions": \{[^{}]*\}', raw)
    if not block or json.loads("{" + block.group(0) + "}")["transitions"].get("file") != "tests/fixtures/f3/transitions-f4.json":
        raise SystemExit("Manifest senza la chiave transitions attesa.")
    text = re.sub(r'"sha256": "[0-9a-f]{64}"', '"sha256": "' + sha(target) + '"', block.group(0), count=1)
    text = re.sub(r'"entries": \d+', '"entries": ' + str(len(entries)), text, count=1)
    with open(manifest_path, "wb") as handle:
        handle.write((raw[:block.start()] + text + raw[block.end():]).encode("utf-8"))
    print(f"{len(entries)} transizioni in {len(set(e['file'] for e in entries))} file; voci {document['per_voce']}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
