"""Censimento dei colori e delle dimensioni dei caratteri dell'interfaccia storica (F5.3, proposta F5 sezione 6; Q6).

Legge i sorgenti C# e XAML di X.Desktop (escluse le viste nuove X.Desktop/Views e X.Desktop/Shell, la cartella dei temi
X.Desktop/Themes, bin e obj) e il catalogo dei token X.Desktop/Themes/ThemeTokens.cs, e produce:
  - per ogni colore scritto nel codice (esadecimale, Brushes.*, Colors.*, Ui.Navy/Blue/Bg/Muted) le occorrenze, i file e il
    ruolo dedotto dal contesto della riga (sfondo, primo piano, bordo, selezione, serie, campione, disattivato, disegno, ?);
  - per ogni token il numero di usi esatti (stesso colore e ruolo compatibile) e le varianti vicine: colori diversi con lo
    stesso ruolo (o ruolo non dedotto) a distanza CIE76 ΔE ≤ 5 nello spazio Lab D65. Sono le varianti che la raccomandazione
    (c) di Q6 accorperebbe modulo per modulo nelle viste nuove, con schermate prima/dopo; nella Chiara non cambia niente;
  - le dimensioni dei caratteri: Ui.Text(testo, dimensione…) (13 se omessa), FontSize = n nel C#, FontSize="n" nel XAML e
    new FormattedText(…, n, …) nei disegni (contati a parte).
Il ruolo è dedotto da un'euristica sulla riga (proprietà o metodo della tavolozza più vicini a sinistra del colore): il
censimento è indicativo, non una prova. Nessuna scrittura nel repository.

Uso (Python -I):
  python -I tools/qa/f5_censimento_temi.py [--radice <repository>] --out <cartella>
Scrive <cartella>/censimento-temi.json e <cartella>/censimento-temi.md. Uscita 0, 2 per argomenti non validi.
"""
import argparse
import collections
import json
import math
import os
import re
import sys

NAMED = dict(item.split(':') for item in (
    "AliceBlue:F0F8FF AntiqueWhite:FAEBD7 Aqua:00FFFF Aquamarine:7FFFD4 Azure:F0FFFF Beige:F5F5DC Bisque:FFE4C4 Black:000000 "
    "BlanchedAlmond:FFEBCD Blue:0000FF BlueViolet:8A2BE2 Brown:A52A2A BurlyWood:DEB887 CadetBlue:5F9EA0 Chartreuse:7FFF00 "
    "Chocolate:D2691E Coral:FF7F50 CornflowerBlue:6495ED Cornsilk:FFF8DC Crimson:DC143C Cyan:00FFFF DarkBlue:00008B DarkCyan:008B8B "
    "DarkGoldenrod:B8860B DarkGray:A9A9A9 DarkGreen:006400 DarkKhaki:BDB76B DarkMagenta:8B008B DarkOliveGreen:556B2F DarkOrange:FF8C00 "
    "DarkOrchid:9932CC DarkRed:8B0000 DarkSalmon:E9967A DarkSeaGreen:8FBC8F DarkSlateBlue:483D8B DarkSlateGray:2F4F4F DarkTurquoise:00CED1 "
    "DarkViolet:9400D3 DeepPink:FF1493 DeepSkyBlue:00BFFF DimGray:696969 DodgerBlue:1E90FF Firebrick:B22222 FloralWhite:FFFAF0 "
    "ForestGreen:228B22 Fuchsia:FF00FF Gainsboro:DCDCDC GhostWhite:F8F8FF Gold:FFD700 Goldenrod:DAA520 Gray:808080 Green:008000 "
    "GreenYellow:ADFF2F Honeydew:F0FFF0 HotPink:FF69B4 IndianRed:CD5C5C Indigo:4B0082 Ivory:FFFFF0 Khaki:F0E68C Lavender:E6E6FA "
    "LavenderBlush:FFF0F5 LawnGreen:7CFC00 LemonChiffon:FFFACD LightBlue:ADD8E6 LightCoral:F08080 LightCyan:E0FFFF "
    "LightGoldenrodYellow:FAFAD2 LightGray:D3D3D3 LightGreen:90EE90 LightPink:FFB6C1 LightSalmon:FFA07A LightSeaGreen:20B2AA "
    "LightSkyBlue:87CEFA LightSlateGray:778899 LightSteelBlue:B0C4DE LightYellow:FFFFE0 Lime:00FF00 LimeGreen:32CD32 Linen:FAF0E6 "
    "Magenta:FF00FF Maroon:800000 MediumAquamarine:66CDAA MediumBlue:0000CD MediumOrchid:BA55D3 MediumPurple:9370DB MediumSeaGreen:3CB371 "
    "MediumSlateBlue:7B68EE MediumSpringGreen:00FA9A MediumTurquoise:48D1CC MediumVioletRed:C71585 MidnightBlue:191970 MintCream:F5FFFA "
    "MistyRose:FFE4E1 Moccasin:FFE4B5 NavajoWhite:FFDEAD Navy:000080 OldLace:FDF5E6 Olive:808000 OliveDrab:6B8E23 Orange:FFA500 "
    "OrangeRed:FF4500 Orchid:DA70D6 PaleGoldenrod:EEE8AA PaleGreen:98FB98 PaleTurquoise:AFEEEE PaleVioletRed:DB7093 PapayaWhip:FFEFD5 "
    "PeachPuff:FFDAB9 Peru:CD853F Pink:FFC0CB Plum:DDA0DD PowderBlue:B0E0E6 Purple:800080 Red:FF0000 RosyBrown:BC8F8F RoyalBlue:4169E1 "
    "SaddleBrown:8B4513 Salmon:FA8072 SandyBrown:F4A460 SeaGreen:2E8B57 SeaShell:FFF5EE Sienna:A0522D Silver:C0C0C0 SkyBlue:87CEEB "
    "SlateBlue:6A5ACD SlateGray:708090 Snow:FFFAFA SpringGreen:00FF7F SteelBlue:4682B4 Tan:D2B48C Teal:008080 Thistle:D8BFD8 "
    "Tomato:FF6347 Turquoise:40E0D0 Violet:EE82EE Wheat:F5DEB3 White:FFFFFF WhiteSmoke:F5F5F5 Yellow:FFFF00 YellowGreen:9ACD32").split())
UI = {"Navy": "0B2A4A", "Blue": "0B5CAD", "Bg": "F3F5F8", "Muted": "64748B"}
ROLE_CALLS = [("Appearance.Selection(", "selection"), ("Appearance.Swatch(", "swatch"), ("Appearance.Series(", "series"),
              ("Appearance.Dim(", "disabled"), ("Appearance.Background(", "background"), ("Appearance.Outline(", "border")]
PROPERTY_ROLES = [(r"(?:Row|AlternatingRow)?Background\b", "background"), (r"\bForeground\b|\bcolor:", "foreground"),
                  (r"\b(?:BorderBrush|HorizontalGridLinesBrush|VerticalGridLinesBrush)\b", "border"),
                  (r"\b(?:DrawRectangle|DrawEllipse|DrawLine|DrawGeometry|new Pen|Fill|Stroke)\b", "drawing")]
TOKEN = re.compile(r'new\("([^"]+)", "#([0-9A-Fa-f]{6})", ThemeRole\.(\w+), "([^"]*)"')
HEX = re.compile(r'#([0-9A-Fa-f]{8}|[0-9A-Fa-f]{6})\b')
NAMED_USE = re.compile(r'\b(?:Brushes|Colors)\.([A-Z][A-Za-z]+)\b')
UI_USE = re.compile(r'\bUi\.(Navy|Blue|Bg|Muted)\b')
# Brushes of the palette with a fixed colour and role, and the resource keys of App.xaml and AppearanceResources.
PALETTE = {"Paper": ("FFFFFF", "background"), "Surface": ("F3F5F8", "background"), "Ink": ("0B2A4A", "foreground"),
           "Accent": ("0B5CAD", "foreground"), "Selected": ("0B2A4A", "selected"), "Calculated": ("EAF2FA", "calculated")}
PALETTE_USE = re.compile(r'\bAppearance\.(Paper|Surface|Ink|Accent|Selected|Calculated)\b')
RESOURCE_USE = re.compile(r'\bAppearance\.(background|foreground|border|selection|calculated)\.([0-9A-Fa-f]{6})\b')
NAMED_RESOURCES = {"AppearancePaper": ("FFFFFF", "background"), "AppearanceSurface": ("F3F5F8", "background"),
                   "AppearanceInk": ("0B2A4A", "foreground"), "AppearanceBorder": ("D3D3D3", "border"), "AppearanceSelection": ("E2EFFC", "selection")}
NAMED_RESOURCE_USE = re.compile(r'\b(AppearancePaper|AppearanceSurface|AppearanceInk|AppearanceBorder|AppearanceSelection)\b')
EXCLUDED = ("X.Desktop/Views/", "X.Desktop/Shell/", "X.Desktop/Themes/")


def sources(root):
    base = os.path.join(root, "X.Desktop")
    for folder, dirs, files in os.walk(base):
        dirs[:] = [d for d in dirs if d not in ("bin", "obj")]
        for name in files:
            if name.endswith((".cs", ".xaml")):
                path = os.path.join(folder, name)
                relative = os.path.relpath(path, root).replace(os.sep, "/")
                if not relative.startswith(EXCLUDED):
                    yield relative, path


def role_of(prefix, drawing_file):
    for call, role in ROLE_CALLS:
        position = prefix.rfind(call)
        if position >= 0 and prefix.count("(", position) > prefix.count(")", position):
            return role
    best, best_role = -1, None
    for pattern, role in PROPERTY_ROLES:
        for match in re.finditer(pattern, prefix):
            if match.start() > best:
                best, best_role = match.start(), role
    if best_role:
        return best_role
    return "drawing" if drawing_file else "?"


def lab(hex6):
    def linear(v):
        v /= 255
        return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4
    r, g, b = (linear(int(hex6[i:i + 2], 16)) for i in (0, 2, 4))
    x, y, z = (r * .4124 + g * .3576 + b * .1805) / .95047, r * .2126 + g * .7152 + b * .0722, (r * .0193 + g * .1192 + b * .9505) / 1.08883
    f = lambda t: t ** (1 / 3) if t > .008856 else 7.787 * t + 16 / 116
    return 116 * f(y) - 16, 500 * (f(x) - f(y)), 200 * (f(y) - f(z))


def delta(a, b):
    return math.dist(lab(a), lab(b))


def call_arguments(text, start):
    """Top-level arguments of the call whose '(' is at start."""
    depth, current, items, quote = 0, [], [], None
    i = start
    while i < len(text):
        c = text[i]
        if quote:
            current.append(c)
            if c == "\\":
                current.append(text[i + 1]); i += 2; continue
            if c == quote:
                quote = None
        elif c in "\"'":
            quote = c; current.append(c)
        elif c in "([{":
            depth += 1
            if depth > 1:
                current.append(c)
        elif c in ")]}":
            depth -= 1
            if depth == 0:
                items.append("".join(current).strip()); return items
            current.append(c)
        elif c == "," and depth == 1:
            items.append("".join(current).strip()); current = []
        else:
            current.append(c)
        i += 1
    return items


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--radice", default=os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
    parser.add_argument("--out", required=True)
    args = parser.parse_args()
    root = os.path.abspath(args.radice)
    catalogue_path = os.path.join(root, "X.Desktop", "Themes", "ThemeTokens.cs")
    if not os.path.isfile(catalogue_path):
        print("Catalogo dei token assente: " + catalogue_path, file=sys.stderr); return 2
    with open(catalogue_path, encoding="utf-8") as handle:
        tokens = [{"nome": m[0], "chiara": "#" + m[1].upper(), "ruolo": m[2].lower(), "uso": m[3]} for m in TOKEN.findall(handle.read())]

    colours = collections.defaultdict(lambda: {"occorrenze": 0, "ruoli": collections.Counter(), "file": collections.Counter(), "nomi": set()})
    sizes = {"Ui.Text": collections.Counter(), "FontSize (C#)": collections.Counter(), "FontSize (XAML)": collections.Counter(),
             "FormattedText (disegni)": collections.Counter()}
    files = 0
    for relative, path in sources(root):
        files += 1
        with open(path, encoding="utf-8-sig") as handle:
            text = handle.read()
        drawing_file = "OnRender(DrawingContext" in text or "DrawingContext dc" in text
        for line in text.splitlines():
            found = [(m.start(), m.group(1)[-6:].upper(), "#" + m.group(1).upper()) for m in HEX.finditer(line)]
            found += [(m.start(), NAMED[m.group(1)], m.group(0)) for m in NAMED_USE.finditer(line) if m.group(1) in NAMED]
            found += [(m.start(), UI[m.group(1)], m.group(0)) for m in UI_USE.finditer(line)]
            fixed = [(m.start(), *PALETTE[m.group(1)], m.group(0)) for m in PALETTE_USE.finditer(line)]
            fixed += [(m.start(), m.group(2).upper(), m.group(1), m.group(0)) for m in RESOURCE_USE.finditer(line)]
            fixed += [(m.start(), *NAMED_RESOURCES[m.group(1)], m.group(0)) for m in NAMED_RESOURCE_USE.finditer(line)]
            # A registration line (key and colour of the same brush) counts once.
            found = [f for f in found if f[1] not in {hex6 for _, hex6, _, _ in fixed}]
            items = [(position, hex6, None, name) for position, hex6, name in found] + [(position, hex6, role, name) for position, hex6, role, name in fixed]
            for position, hex6, fixed_role, name in items:
                if name.endswith("Transparent"):
                    continue
                entry = colours[hex6]
                entry["occorrenze"] += 1
                entry["ruoli"][fixed_role or role_of(line[:position], drawing_file)] += 1
                entry["file"][relative] += 1
                entry["nomi"].add(name)
        if relative.endswith(".cs"):
            for match in re.finditer(r"\bUi\.Text\(", text):
                arguments = call_arguments(text, match.end() - 1)
                named = [a.split(":", 1)[1].strip() for a in arguments if re.match(r"size\s*:", a)]
                positional = [a for a in arguments if not re.match(r"\w+\s*:", a)]
                value = named[0] if named else positional[1] if len(positional) > 1 else "13"
                sizes["Ui.Text"][value if re.fullmatch(r"\d+(\.\d+)?", value) else "espressione"] += 1
            for match in re.finditer(r"\bFontSize\s*=\s*([0-9.]+)\b", text):
                sizes["FontSize (C#)"][match.group(1)] += 1
            for match in re.finditer(r"FontSizeProperty\s*,\s*([0-9.]+)\b", text):
                sizes["FontSize (C#)"][match.group(1)] += 1
            for match in re.finditer(r"\bnew FormattedText\(", text):
                arguments = call_arguments(text, match.end() - 1)
                value = arguments[4] if len(arguments) > 4 else "?"
                sizes["FormattedText (disegni)"][value if re.fullmatch(r"\d+(\.\d+)?", value) else "espressione"] += 1
        else:
            for match in re.finditer(r'FontSize="([0-9.]+)"|Property="FontSize" Value="([0-9.]+)"', text):
                sizes["FontSize (XAML)"][match.group(1) or match.group(2)] += 1

    compatible = {"background": {"background", "?"}, "foreground": {"foreground", "?"}, "border": {"border", "?"},
                  "selected": {"selected"}, "selection": {"selection"}, "calculated": {"calculated"}, "series": {"series", "?"},
                  "disabled": {"disabled"}, "swatch": {"swatch", "drawing", "?"}}
    report_tokens = []
    covered = {t["chiara"][1:] for t in tokens}
    for token in tokens:
        hex6 = token["chiara"][1:]
        roles = compatible[token["ruolo"]]
        exact = colours.get(hex6)
        exact_uses = sum(n for r, n in exact["ruoli"].items() if r in roles) if exact else 0
        variants = []
        for other, entry in colours.items():
            if other == hex6:
                continue
            uses = sum(n for r, n in entry["ruoli"].items() if r in roles)
            distance = delta(hex6, other)
            if uses and distance <= 5 and other not in covered:
                variants.append({"colore": "#" + other, "delta_e": round(distance, 1), "occorrenze": uses,
                                 "file": [f for f, _ in entry["file"].most_common(3)]})
        variants.sort(key=lambda v: (v["delta_e"], -v["occorrenze"]))
        report_tokens.append(dict(token, usi_esatti=exact_uses, varianti=variants))

    colour_list = [{"colore": "#" + k, "occorrenze": v["occorrenze"], "ruoli": dict(v["ruoli"]), "nomi": sorted(v["nomi"]),
                    "file": dict(v["file"].most_common())} for k, v in sorted(colours.items(), key=lambda kv: -kv[1]["occorrenze"])]
    near = {v["colore"][1:] for t in report_tokens for v in t["varianti"]}
    summary = {
        "file": files, "colori_distinti": len(colours), "occorrenze": sum(v["occorrenze"] for v in colours.values()),
        "colori_dei_token": sum(1 for c in colours if c in covered), "occorrenze_dei_token": sum(colours[c]["occorrenze"] for c in colours if c in covered),
        "varianti_vicine": len(near - covered), "occorrenze_varianti": sum(colours[c]["occorrenze"] for c in near - covered),
    }
    result = {"strumento": "tools/qa/f5_censimento_temi.py", "riepilogo": summary, "token": report_tokens, "colori": colour_list,
              "dimensioni": {k: dict(sorted(v.items(), key=lambda kv: -kv[1])) for k, v in sizes.items()}}
    os.makedirs(args.out, exist_ok=True)
    with open(os.path.join(args.out, "censimento-temi.json"), "w", encoding="utf-8", newline="\r\n") as handle:
        json.dump(result, handle, ensure_ascii=False, indent=2)

    lines = ["# Censimento dei colori e dei caratteri (tools/qa/f5_censimento_temi.py)", "",
             f"File: {files}. Colori distinti: {summary['colori_distinti']} ({summary['occorrenze']} occorrenze). "
             f"Colori dei token: {summary['colori_dei_token']} ({summary['occorrenze_dei_token']} occorrenze). "
             f"Varianti vicine (ΔE ≤ 5, ruolo compatibile): {summary['varianti_vicine']} ({summary['occorrenze_varianti']} occorrenze).", "",
             "## Token, usi esatti e varianti vicine", "", "| Token | Chiara | Ruolo | Usi esatti | Varianti vicine (ΔE, occorrenze) |", "|---|---|---|---|---|"]
    for t in report_tokens:
        variants = ", ".join(f"{v['colore']} ({v['delta_e']}; {v['occorrenze']})" for v in t["varianti"]) or "—"
        lines.append(f"| `{t['nome']}` | {t['chiara']} | {t['ruolo']} | {t['usi_esatti']} | {variants} |")
    lines += ["", "## Dimensioni dei caratteri", "", "| Origine | Dimensioni (occorrenze) |", "|---|---|"]
    for origin, counter in result["dimensioni"].items():
        lines.append(f"| {origin} | " + ", ".join(f"{k} ({v})" for k, v in counter.items()) + " |")
    with open(os.path.join(args.out, "censimento-temi.md"), "w", encoding="utf-8", newline="\r\n") as handle:
        handle.write("\n".join(lines) + "\n")
    print(f"{files} file, {summary['colori_distinti']} colori, {len(tokens)} token; risultati in {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
