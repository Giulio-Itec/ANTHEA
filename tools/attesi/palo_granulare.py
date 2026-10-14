"""Attesi indipendenti dei casi di palo con coesione efficace in strati granulari (palo_storico_0, palo_storico_3).

Il 27/09/2026 gli attesi di questi due casi di supporto/test/casi_confronto.json erano stati rigenerati dal C#: negli
strati granulari la resistenza laterale drenata è τs = K·μ·σ′v, senza il termine c′ (guida teorica), mentre il programma
Python originale sommava c′ e questi casi hanno c′ = 2 kPa in uno strato granulare.

Il programma originale non è più disponibile; i suoi risultati sì (commit 51e1c114). Questo programma parte da quelli e
cambia solo ciò che dipende da c′:
  - τ dei tratti negli strati granulari: K·μ·σ′v dai campi del tratto (k, mu, sigma_media);
  - resistenza laterale del tratto: τ·π·D·(fondo − cielo); del sondaggio: somma dei tratti;
  - aggregazioni NTC 2018 §6.4.3.1.1: media e minimo fra i sondaggi, k = calc/ξ (ξ3 per la media, ξ4 per il minimo),
    d = k·η/γ (γ e η dal rapporto d/k del programma originale, che non dipende da c′);
  - curve: compressione = d laterale + d di base, trazione = k laterale · η_t/γst (rapporto del programma originale),
    progetto = minimo fra media e minima.
Le regole di aggregazione sono provate su tutti i casi di palo del programma originale (--valida). Nessun codice di ANTHEA.
"""
import argparse
import json
import math
import sys

XI = {1: (1.70, 1.70), 2: (1.65, 1.55), 3: (1.60, 1.48), 4: (1.55, 1.42), 5: (1.50, 1.34), 7: (1.45, 1.28), 10: (1.40, 1.21)}
TOL_ABS, TOL_REL = 1e-8, 1e-10
CASES = ("palo_storico_0", "palo_storico_3")


def load(path):
    with open(path, "rb") as handle:
        return json.loads(handle.read().decode("utf-8"))


def xi_of(case):
    return XI[int(case["input"]["generali"].get("verticali_indagate", "1"))]


def layers_of(case):
    """Strati di ogni stratigrafia: (cielo, fondo, granulare, c′)."""
    result = []
    for strata in case["input"]["stratigrafie"]:
        top, rows = 0.0, []
        for layer in strata:
            bottom = top + float(layer["spessore"])
            rows.append((top, bottom, layer["tipologia"] == "Granulare", float(layer.get("coesione_efficace") or 0)))
            top = bottom
        result.append(rows)
    return result


def ratio(value, base):
    return value / base if base else None


def aggregate(case, detail, lateral):
    """Componenti di un dettaglio dai valori laterali per sondaggio (drenante, non drenante); basi invariate."""
    xi = xi_of(case)
    out = {}
    for fam in ("drenante", "non_drenante"):
        lats = lateral[fam]
        bases = [s[fam]["base"] for s in detail["sondaggi"]]
        old = detail["componenti"][fam]
        comp = {}
        for label, calc, factor in (("Media", [sum(lats) / len(lats), sum(bases) / len(bases)], xi[0]),
                                    ("Minimo", [min(lats), min(bases)], xi[1])):
            k = [calc[0] / factor, calc[1] / factor]
            # η/γ per componente dal programma originale (non dipende da c′)
            d = [k[i] * ratio(old[label]["d"][i], old[label]["k"][i]) if old[label]["k"][i] else 0.0 for i in range(2)]
            comp[label] = {"calc": calc, "k": k, "d": d}
        out[fam] = comp
    return out


def recompute(case):
    """Attesi del programma originale con τ granulare senza c′."""
    a = json.loads(json.dumps(case["atteso"]))
    diameter = float(case["input"]["generali"]["diametro"])
    strata = layers_of(case)
    old_details = case["atteso"]["dettagli"]
    traction_ratio = {}
    for d_old in old_details:
        for fam in ("drenante", "non_drenante"):
            for label in ("Media", "Minimo"):
                k_old = d_old["componenti"][fam][label]["k"][0]
                curve = "media" if label == "Media" else "minima"
                pts = [p for p in case["atteso"]["curve"][fam + "_trazione"][curve] if abs(p[0] - d_old["z"]) < 1e-12]
                if pts and k_old:
                    traction_ratio.setdefault((fam, curve), pts[0][1] / k_old)
    for d in a["dettagli"]:
        lateral = {"drenante": [], "non_drenante": []}
        for index, sounding in enumerate(d["sondaggi"]):
            for t in sounding["tratti"]:
                middle = (t["cielo"] + t["fondo"]) / 2
                layer = next(l for l in strata[index] if l[0] <= middle <= l[1])
                if layer[2] and layer[3] != 0:
                    tau = t["k"] * t["mu"] * t["sigma_media"]
                    t["tau_d"] = t["tau_u"] = tau
                    t["laterale_d"] = t["laterale_u"] = tau * math.pi * diameter * (t["fondo"] - t["cielo"])
            for fam, key in (("drenante", "laterale_d"), ("non_drenante", "laterale_u")):
                total = 0.0
                for t in sounding["tratti"]:
                    total += t[key]
                sounding[fam]["laterale"] = total
                lateral[fam].append(total)
        d["componenti"] = aggregate(case, d, lateral)
    by_z = {d["z"]: d for d in a["dettagli"]}
    for fam in ("drenante", "non_drenante"):
        for kind in ("compressione", "trazione"):
            curves = a["curve"][fam + "_" + kind]
            for curve, label in (("media", "Media"), ("minima", "Minimo")):
                for p in curves[curve]:
                    d = next((v for z, v in by_z.items() if abs(z - p[0]) < 1e-12), None)
                    if d is None:
                        if p[1] != 0:
                            raise SystemExit(f"punto senza dettaglio: {case['nome']} {fam} {kind} {p}")
                        continue
                    comp = d["componenti"][fam][label]
                    p[1] = comp["d"][0] + comp["d"][1] if kind == "compressione" else comp["k"][0] * traction_ratio[(fam, curve)]
            for i, p in enumerate(curves["progetto"]):
                p[1] = min(curves["media"][i][1], curves["minima"][i][1])
    return a


def compare(expected, actual, path, errors, stats):
    if isinstance(expected, dict):
        if not isinstance(actual, dict) or set(expected) != set(actual):
            errors.append(path + ": campi diversi")
            return
        for key in expected:
            compare(expected[key], actual[key], path + "." + key, errors, stats)
    elif isinstance(expected, list):
        if not isinstance(actual, list) or len(expected) != len(actual):
            errors.append(path + ": lunghezza diversa")
            return
        for i, (x, y) in enumerate(zip(expected, actual)):
            compare(x, y, f"{path}[{i}]", errors, stats)
    elif isinstance(expected, (int, float)) and not isinstance(expected, bool) and isinstance(actual, (int, float)) and not isinstance(actual, bool):
        stats[0] += 1
        delta = abs(expected - actual)
        stats[1] = max(stats[1], delta)
        if delta > TOL_ABS + TOL_REL * abs(expected):
            errors.append(f"{path}: atteso={expected!r}; calcolato={actual!r}")
    elif expected != actual:
        errors.append(f"{path}: {expected!r} / {actual!r}")


def validate(original):
    """Le regole di aggregazione rifanno componenti e curve di tutti i casi di palo del programma originale."""
    status, stats = 0, [0, 0.0]
    count = 0
    for case in original:
        if case["tipo"] != "palo" or case.get("atteso_errore") or case["atteso"].get("errore"):
            continue
        a = json.loads(json.dumps(case["atteso"]))
        for d in a["dettagli"]:
            lateral = {fam: [s[fam]["laterale"] for s in d["sondaggi"]] for fam in ("drenante", "non_drenante")}
            d["componenti"] = aggregate(case, d, lateral)
        errors = []
        compare(case["atteso"], a, case["nome"], errors, stats)
        count += 1
        if errors:
            status = 1
            print("DIVERSO " + "; ".join(errors[:3]))
    print(f"Aggregazioni sui casi di palo originali: {count} casi, {stats[0]} numeri, scarto massimo {stats[1]:.3e}")
    return status


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--originale", required=True, help="casi_confronto.json del commit 51e1c114")
    parser.add_argument("--confronta", metavar="CASI")
    parser.add_argument("--scrivi", metavar="CASI")
    parser.add_argument("--provenienza", default="")
    args = parser.parse_args(argv)
    original = load(args.originale)
    status = validate(original)
    computed = {c["nome"]: recompute(c) for c in original if c["nome"] in CASES}
    if args.confronta:
        stats = [0, 0.0]
        for case in load(args.confronta):
            if case["nome"] in computed:
                errors = []
                compare(case["atteso"], computed[case["nome"]], case["nome"], errors, stats)
                print(f"{case['nome']}: {len(errors)} differenze" + ("; " + "; ".join(errors[:3]) if errors else ""))
                status |= 1 if errors else 0
        print(f"Confronto con gli attesi del file: {stats[0]} numeri, scarto massimo {stats[1]:.3e}")
    if args.scrivi:
        raw = open(args.scrivi, "rb").read()
        cases = json.loads(raw.decode("utf-8"))
        if json.dumps(cases, ensure_ascii=False, separators=(",", ":")).encode("utf-8") != raw:
            raise SystemExit("Il file non si riscrive identico: nessuna modifica.")
        if not args.provenienza:
            raise SystemExit("Indicare --provenienza.")
        for case in cases:
            if case["nome"] in computed:
                if case["input"] != next(c for c in original if c["nome"] == case["nome"])["input"]:
                    raise SystemExit("Input cambiato rispetto al programma originale: " + case["nome"])
                case["atteso"] = computed[case["nome"]]
                case["fonte_atteso"] = args.provenienza
        with open(args.scrivi, "wb") as handle:
            handle.write(json.dumps(cases, ensure_ascii=False, separators=(",", ":")).encode("utf-8"))
        print(f"{len(computed)} casi riscritti.")
    return status


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
