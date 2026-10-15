"""Riduce la baseline F4 ai residui osservati, senza mai aggiungere voci.

Baseline: tests/ANTHEA.Architecture.Tests/f4-domain-baseline.json. La prova di architettura chiede di ridurla nello stesso commit quando
un residuo esce dai sorgenti del prodotto (per esempio con lo spostamento del codice legacy in tests/ANTHEA.Legacy, decisione D3 del
15/10/2026). Uso:

  dotnet exec tests/ANTHEA.Architecture.Tests/bin/Release/net8.0/ANTHEA.Architecture.Tests.dll <uscita> --f4-inventory
  python -I tools/qa/f4_baseline_riduci.py <uscita>/f4-findings.json [--radice <checkout di ANTHEA>]

Ogni voce scende al conteggio osservato (il minore dei due) e sparisce se non è più osservata. Se l'inventario contiene una segnalazione
oltre la baseline il programma si ferma senza scrivere: le aggiunte richiedono una revisione separata (docs/refactoring/f4-architettura.md).
Il resto del file resta byte per byte com'era: si tolgono blocchi e si cambiano solo le righe "Count".
"""
import argparse
import json
import os
import re
import sys


def key(f):
    return (f["File"], f["Rule"], f["Owner"], f["Fingerprint"])


def main(argv):
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("findings", help="f4-findings.json di un inventario (--f4-inventory)")
    parser.add_argument("--radice", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    args = parser.parse_args(argv)
    path = os.path.join(args.radice, "tests", "ANTHEA.Architecture.Tests", "f4-domain-baseline.json")
    raw = open(path, "rb").read().decode("utf-8")
    newline = "\r\n" if "\r\n" in raw else "\n"
    observed = {}
    for f in json.load(open(args.findings, encoding="utf-8-sig"))["Findings"]:
        observed[key(f)] = observed.get(key(f), 0) + f["Count"]
    head, sep, rest = raw.partition('"Findings": [')
    if not sep:
        raise SystemExit("Baseline senza Findings.")
    body, close, tail = rest.rpartition("]")
    opener = newline + "    {"
    blocks = body.split(opener)
    prefix, blocks = blocks[0], blocks[1:]
    baseline = {}
    kept, removed, lowered = [], 0, 0
    for i, block in enumerate(blocks):
        text = "{" + block.rstrip().rstrip(",")
        entry = json.loads(text)
        baseline[key(entry)] = entry["Count"]
        count = min(entry["Count"], observed.get(key(entry), 0))
        if count == 0:
            removed += 1
            continue
        if count < entry["Count"]:
            lowered += 1
            block, n = re.subn(r'("Count": )\d+', r"\g<1>" + str(count), block, count=1)
            if n != 1:
                raise SystemExit("Voce senza Count: " + entry["File"])
        kept.append(block)
    additions = [k for k, c in observed.items() if c > baseline.get(k, 0)]
    if additions:
        for k in additions[:20]:
            print("AGGIUNTA", " | ".join(k[:3]), observed[k], "su", baseline.get(k, 0))
        raise SystemExit(f"{len(additions)} segnalazioni oltre la baseline: nessun file scritto.")
    # Ogni blocco termina con "}," tranne l'ultimo, che termina con "}" e il ritorno a capo prima di "]".
    ending = newline + "  "
    normalized = []
    for i, block in enumerate(kept):
        core = block.rstrip()
        core = core[:-1] if core.endswith(",") else core
        normalized.append(core + ("," if i < len(kept) - 1 else ending))
    body = prefix + "".join(opener + b for b in normalized) if kept else prefix + ending
    open(path, "wb").write((head + sep + body + close + tail).encode("utf-8"))
    print(f"Baseline F4: {len(blocks)} voci, {removed} tolte, {lowered} ridotte, {len(kept)} restano.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
