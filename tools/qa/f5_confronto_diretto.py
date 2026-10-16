"""Confronto diretto, a differenza zero, di due catture ui/f3-equivalence (F5, proposta di architettura, sezione 7.2).

Serve a ogni modulo migrato da F5.4: cattura con la vista storica contro cattura con la vista nuova dello stesso codice
(«--check-f3-equivalence <radice> <uscita> --module-impl <modulo>=mvvm», solo nella build UiTests). Nessuna tolleranza:
  - gli stessi file, esclusi solo quelli che il confronto F3 esclude («file_esclusi» di tests/ANTHEA.Testing/tolerances.json:
    manifest.json, log.txt, raw/, confronto*.json);
  - gli stessi byte; se i byte differiscono, un file .json è uguale solo se il JSON canonico è lo stesso: stesse chiavi
    (l'ordine non conta), stessi tipi, stessi testi dei numeri e delle stringhe, stessi elementi nello stesso ordine. La
    cattura scrive già JSON canonico (CanonicalJson del toolkit), quindi di solito i byte coincidono;
  - le sole chiavi ignorate sono le «volatili» di tolerances.json (tempi misurati con Stopwatch), come nel confronto F3:
    sono contate a parte, mai ammesse come differenze di un valore;
  - i testi (.txt dei report, .md della Wiki) sono uguali solo byte per byte: niente scheletro del testo né arrotondamenti
    (NumberText del confronto F3 non si usa).
Ogni cartella deve essere una cattura completa (esito.txt che comincia con PASS).

Uso (Python -I):
  python -I tools/qa/f5_confronto_diretto.py --a <cattura storica> --b <cattura nuova> [--tolerances tests/ANTHEA.Testing/tolerances.json]
         [--out <confronto-diretto.json>]
  python -I tools/qa/f5_confronto_diretto.py --a <cattura> --autoverifica [--out <autoverifica.json>]
Con --autoverifica il programma prova sé stesso: copia la cattura in una cartella temporanea, applica un'alterazione per volta
(un numero, un intero scritto come decimale, una cifra di un report, un file tolto o aggiunto, una chiave tolta) e controlla
che ognuna sia trovata con la sua classe, mentre un JSON solo riformattato, un tempo volatile e un file escluso non lo sono.
Le copie alterate si cancellano alla fine.
Uscita 0 senza differenze (o autoverifica riuscita), 1 con differenze (o autoverifica fallita), 2 per argomenti o catture
non valide.
"""
import argparse
import collections
import json
import os
import re
import shutil
import sys
import tempfile

TOLERANCES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tests", "ANTHEA.Testing", "tolerances.json")
MAX_LISTED = 200


class Raw(str):
    """Testo di un numero JSON, conservato com'è: 1, 1.0 e 1e0 sono diversi."""


def load_json(path):
    with open(path, "rb") as handle:
        text = handle.read().decode("utf-8-sig")
    return json.loads(text, parse_float=Raw, parse_int=Raw, parse_constant=Raw)


def member(name):
    return "." + name if re.fullmatch(r"[^\W\d]\w*", name) else "[" + json.dumps(name, ensure_ascii=False) + "]"


def kind(value):
    if value is None:
        return "null"
    if isinstance(value, bool):
        return "boolean"
    if isinstance(value, Raw):
        return "number"
    if isinstance(value, str):
        return "string"
    return "array" if isinstance(value, list) else "object"


def short(value):
    text = value if isinstance(value, Raw) else json.dumps(value, ensure_ascii=False, sort_keys=True)
    return text if len(text) <= 300 else text[:300] + "…"


class Comparison:
    def __init__(self, excluded, volatile):
        self.excluded, self.volatile = excluded, volatile
        self.differences, self.volatile_counts = [], collections.Counter()
        self.compared = self.same_bytes = self.same_json = 0

    def is_volatile(self, key, path):
        for key_rule, path_rule, reason in self.volatile:
            if (key_rule or path_rule) and (key_rule is None or key_rule.search(key)) and (path_rule is None or path_rule.search(path)):
                return reason
        return None

    def add(self, file, path, cls, a=None, b=None):
        self.differences.append({"file": file, "percorso": path, "classe": cls, "a": a, "b": b})

    def json_values(self, file, x, y, path, key):
        reason = self.is_volatile(key, file + ":" + path)
        if reason is not None:
            if x != y:
                self.volatile_counts[reason] += 1
            return
        kx, ky = kind(x), kind(y)
        if kx != ky:
            self.add(file, path, "tipo", short(x), short(y))
        elif kx == "object":
            for name in sorted(set(x) | set(y)):
                child = path + member(name)
                if name in x and name in y:
                    self.json_values(file, x[name], y[name], child, name)
                elif (reason := self.is_volatile(name, file + ":" + child)) is not None:
                    self.volatile_counts[reason] += 1
                else:
                    self.add(file, child, "chiave-rimossa" if name in x else "chiave-aggiunta",
                             short(x[name]) if name in x else None, short(y[name]) if name in y else None)
        elif kx == "array":
            for i in range(max(len(x), len(y))):
                child = path + "[" + str(i) + "]"
                if i >= len(y):
                    self.add(file, child, "elemento-rimosso", short(x[i]), None)
                elif i >= len(x):
                    self.add(file, child, "elemento-aggiunto", None, short(y[i]))
                else:
                    self.json_values(file, x[i], y[i], child, key)
        elif x != y or type(x) is not type(y):
            self.add(file, path, "valore", short(x), short(y))

    def file(self, rel, pa, pb):
        self.compared += 1
        with open(pa, "rb") as ha, open(pb, "rb") as hb:
            ba, bb = ha.read(), hb.read()
        if ba == bb:
            self.same_bytes += 1
            return
        if rel.lower().endswith(".json"):
            try:
                x, y = load_json(pa), load_json(pb)
            except ValueError as error:
                self.add(rel, "", "json-non-valido", None, str(error))
                return
            before = len(self.differences)
            self.json_values(rel, x, y, "$", "")
            if len(self.differences) == before:
                self.same_json += 1
            return
        la, lb = ba.split(b"\n"), bb.split(b"\n")
        line = next((i for i in range(min(len(la), len(lb))) if la[i] != lb[i]), min(len(la), len(lb)))
        def text(lines):
            return lines[line].decode("utf-8", "replace").rstrip("\r") if line < len(lines) else None
        self.add(rel, "riga " + str(line + 1), "byte", text(la), text(lb))


def files(root, excluded):
    out = set()
    for base, _, names in os.walk(root):
        for name in names:
            rel = os.path.relpath(os.path.join(base, name), root).replace("\\", "/")
            if not any(rule.search(rel) for rule in excluded):
                out.add(rel)
    return out


def complete(root):
    path = os.path.join(root, "esito.txt")
    if not os.path.isfile(path):
        return False
    with open(path, encoding="utf-8-sig") as handle:
        return handle.read().startswith("PASS ")


def rules(path):
    with open(path, encoding="utf-8-sig") as handle:
        tolerances = json.load(handle)
    excluded = [re.compile(p) for p in tolerances.get("file_esclusi", [])]
    volatile = [(re.compile(v["chiave"]) if v.get("chiave") else None, re.compile(v["percorso"]) if v.get("percorso") else None,
                 v.get("motivo", "")) for v in tolerances.get("volatili", [])]
    return excluded, volatile


def compare(a, b, excluded, volatile):
    comparison = Comparison(excluded, volatile)
    fa, fb = files(a, excluded), files(b, excluded)
    for rel in sorted(fa | fb):
        if rel not in fb:
            comparison.add(rel, "", "file-rimosso", rel, None)
        elif rel not in fa:
            comparison.add(rel, "", "file-aggiunto", None, rel)
        else:
            comparison.file(rel, os.path.join(a, rel), os.path.join(b, rel))
    return comparison


# ---------------------------------------------------------------- autoverifica

def first(root, pattern, predicate):
    for rel in sorted(files(root, [])):
        if re.fullmatch(pattern, rel):
            path = os.path.join(root, rel)
            with open(path, encoding="utf-8") as handle:
                text = handle.read()
            if predicate(text):
                return rel, path, text
    raise SystemExit(f"Autoverifica: nessun file {pattern} adatto nella cattura.")


def write(path, text):
    with open(path, "w", encoding="utf-8", newline="") as handle:
        handle.write(text)


def number_token(text, decimal):
    """Primo numero JSON fuori dalle stringhe (dopo «:», «[» o «,»), con o senza parte decimale."""
    body = r"-?\d+\.\d+" if decimal else r"-?\d+"
    return re.search(r"(?<=[:\[,])(\s*)(" + body + r")(?=\s*[,\]\}])", text)


def change_decimal(root):
    rel, path, text = first(root, r"sheets/.+/result\.json", lambda t: number_token(t, True) is not None)
    m = number_token(text, True)
    token = m.group(2)
    dot = token.index(".")  # la prima cifra decimale: un valore diverso anche come double, non solo come testo
    changed = token[:dot + 1] + str((int(token[dot + 1]) + 1) % 10) + token[dot + 2:]
    write(path, text[:m.start(2)] + changed + text[m.end(2):])
    return f"{rel}: {token} -> {changed}"


def integer_as_decimal(root):
    rel, path, text = first(root, r"sheets/.+/result\.json", lambda t: number_token(t, False) is not None)
    m = number_token(text, False)
    write(path, text[:m.end(2)] + ".0" + text[m.end(2):])
    return f"{rel}: {m.group(2)} -> {m.group(2)}.0 (stessa quantità, testo diverso)"


def report_character(root):
    rel, path, text = first(root, r"sheets/.+/report\.txt", lambda t: re.search(r"\d", t[len(t) // 2:]) is not None)
    i = len(text) // 2 + re.search(r"\d", text[len(text) // 2:]).start()
    write(path, text[:i] + str((int(text[i]) + 1) % 10) + text[i + 1:])
    return f"{rel}: una cifra al carattere {i}"


def remove_file(root):
    rel, path, _ = first(root, r"wiki/.+\.md", lambda t: True)
    os.remove(path)
    return rel


def add_file(root):
    write(os.path.join(root, "sheets", "aggiunto.json"), "{}\n")
    return "sheets/aggiunto.json"


def remove_key(root):
    rel, path, text = first(root, r"sheets/.+/status\.json", lambda t: '"report"' in t)
    value = json.loads(text)
    del value["report"]
    write(path, json.dumps(value, ensure_ascii=False, indent=2) + "\n")
    return f"{rel}: chiave report tolta"


def reformat(root):
    rel, path, text = first(root, r"sheets/.+/status\.json", lambda t: len(json.loads(t)) > 1)
    value = json.loads(text)
    write(path, json.dumps(dict(reversed(list(value.items()))), ensure_ascii=False, indent=4))
    return f"{rel}: chiavi in ordine inverso e rientro diverso (stesso JSON canonico)"


def volatile_time(root):
    rel, path, text = first(root, r"sheets/.+\.json", lambda t: re.search(r'"tempi_ms":[^\d]*\d', t) is not None)
    m = re.search(r'("tempi_ms":[^\d]*)(\d)', text)
    write(path, text[:m.start(2)] + str((int(m.group(2)) + 1) % 10) + text[m.end(2):])
    return f"{rel}: tempi_ms (chiave volatile di tolerances.json)"


def excluded_file(root):
    path = os.path.join(root, "confronto.json")
    if not os.path.isfile(path):
        raise SystemExit("Autoverifica: confronto.json assente nella cattura.")
    write(path, "{}")
    return "confronto.json (escluso come nel confronto F3)"


# Alterazione, classi attese (nessuna: le copie sono uguali), file JSON attesi uguali solo in forma canonica.
ALTERATIONS = [
    ("copia-intatta", lambda root: "nessuna", [], 0),
    ("numero-decimale", change_decimal, ["valore"], 0),
    ("numero-intero-come-decimale", integer_as_decimal, ["valore"], 0),
    ("cifra-nel-report", report_character, ["byte"], 0),
    ("file-tolto", remove_file, ["file-rimosso"], 0),
    ("file-aggiunto", add_file, ["file-aggiunto"], 0),
    ("chiave-tolta", remove_key, ["chiave-rimossa"], 0),
    ("json-riformattato", reformat, [], 1),
    ("tempo-volatile", volatile_time, [], 1),
    ("file-escluso", excluded_file, [], 0),
]


def self_check(source, tolerances, out):
    """Copie temporanee di una cattura, ognuna con un'alterazione voluta: il confronto deve trovarla, con la sua classe."""
    excluded, volatile = rules(tolerances)
    work = tempfile.mkdtemp(prefix="f5-confronto-diretto-")
    outcomes, failed = [], 0
    try:
        base = os.path.join(work, "a")
        shutil.copytree(source, base, ignore=shutil.ignore_patterns("raw"))
        for name, alter, expected, canonical in ALTERATIONS:
            copy = os.path.join(work, name)
            shutil.copytree(base, copy)
            detail = alter(copy)
            result = compare(base, copy, excluded, volatile)
            classes = sorted({d["classe"] for d in result.differences})
            ok = classes == sorted(expected) and len(result.differences) == len(expected) and result.same_json == canonical
            failed += not ok
            outcomes.append({"alterazione": name, "dettaglio": detail, "attese": expected, "trovate": classes,
                             "differenze": result.differences[:5], "json_canonici": result.same_json,
                             "volatili": sum(result.volatile_counts.values()), "esito": "OK" if ok else "FALLITA"})
            print(f"{'OK' if ok else 'FALLITA':8} {name}: {detail} -> {', '.join(classes) or 'nessuna differenza'}")
            shutil.rmtree(copy)
    finally:
        shutil.rmtree(work, ignore_errors=True)
    if out:
        with open(out, "w", encoding="utf-8", newline="\n") as handle:
            json.dump({"cattura": os.path.abspath(source), "alterazioni": outcomes}, handle, ensure_ascii=False, indent=1)
    print(f"ESITO autoverifica: {len(ALTERATIONS) - failed} su {len(ALTERATIONS)} alterazioni trattate come atteso"
          + ("" if not failed else f"; {failed} FALLITE"))
    return 0 if not failed else 1


def main(argv):
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--a", required=True, help="cattura di riferimento (vista storica)")
    parser.add_argument("--b", help="cattura da confrontare (vista nuova)")
    parser.add_argument("--autoverifica", action="store_true",
                        help="altera copie temporanee di --a e controlla che ogni alterazione sia trovata (nessuna copia resta)")
    parser.add_argument("--tolerances", default=TOLERANCES, help="tolerances.json: solo file_esclusi e volatili")
    parser.add_argument("--out", help="rapporto JSON")
    args = parser.parse_args(argv)
    if args.autoverifica == (args.b is not None):
        parser.error("indicare --b oppure --autoverifica")
    for root in (args.a, args.b):
        if root is not None and (not os.path.isdir(root) or not complete(root)):
            print(f"Cattura assente o incompleta (esito.txt senza PASS): {root}", file=sys.stderr)
            return 2
    if args.autoverifica:
        return self_check(args.a, args.tolerances, args.out)
    excluded, volatile = rules(args.tolerances)
    comparison = compare(args.a, args.b, excluded, volatile)
    differences = comparison.differences
    per_class = collections.Counter(d["classe"] for d in differences)
    report = {
        "a": os.path.abspath(args.a), "b": os.path.abspath(args.b), "tolleranze": os.path.abspath(args.tolerances),
        "regola": "differenza zero: stessi file, stessi byte o stesso JSON canonico; nessuna tolleranza",
        "esito": "identiche" if not differences else "differenze",
        "file_confrontati": comparison.compared, "file_uguali_byte": comparison.same_bytes,
        "file_json_uguali_canonici": comparison.same_json, "differenze": len(differences),
        "per_classe": dict(sorted(per_class.items())), "volatili": dict(sorted(comparison.volatile_counts.items())),
        "elenco_troncato": len(differences) > MAX_LISTED, "elenco": differences[:MAX_LISTED],
    }
    if args.out:
        with open(args.out, "w", encoding="utf-8", newline="\n") as handle:
            json.dump(report, handle, ensure_ascii=False, indent=1)
    volatile_note = ", ".join(f"{n} x {r}" for r, n in sorted(comparison.volatile_counts.items())) or "nessuno"
    print(f"{comparison.compared} file confrontati: {comparison.same_bytes} uguali byte per byte, "
          f"{comparison.same_json} JSON uguali in forma canonica; valori volatili ignorati: {volatile_note}")
    for d in differences[:20]:
        print(f"  {d['classe']}: {d['file']} {d['percorso']}  a={d['a']}  b={d['b']}")
    summary = ", ".join(f"{k} {v}" for k, v in sorted(per_class.items()))
    print("ESITO: identiche" if not differences else f"ESITO: {len(differences)} differenze ({summary})")
    return 0 if not differences else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
