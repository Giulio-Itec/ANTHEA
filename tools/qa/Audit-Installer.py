"""Audit of an ANTHEA setup extracted with 7-Zip, against the staging of Crea-Installer.ps1 and the versioned sources.

Generalises the scripts of 1.12.0 and 1.12.1 (supporto/artefatti/rilascio-1.12.x/Audit-Installer*.py). Checks:
  - clean source tree, src/ANTHEA.Desktop/ANTHEA.Desktop.csproj <Version> and ProductVersion of the extracted ANTHEA.exe = <version>+<commit>;
  - every staged file (app, Guide, Esempi) present in the extraction with the same SHA-256;
  - the GPC DLLs equal to lib/Checker/manifest.json; the examples equal to supporto/esempi/distribuzione/catalogo.json;
  - the two guides equal to the Rev<n> PDFs of supporto/documentazione/Guide_ANTHEA and the index to supporto/installer;
  - no Gmsh, UnsafeEx, legacy or test assemblies.
Writes the result (summary and payload) as JSON and stops at the first failed check.

Usage, from the repository root of the build commit (Python -I):
  & "C:\\Program Files\\7-Zip\\7z.exe" x <setup.exe> -o<cartella>\\estratto -y
  python -I tools/qa/Audit-Installer.py --setup <setup.exe> --extract <cartella>\\estratto --version 1.13.0
         --guide-revision 58 --library-set AF [--stage supporto/artefatti/installer/stage] [--out <cartella>\\audit-payload.json]
"""
import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

FORBIDDEN = ("gmsh", "unsafeex", "anthea.legacy", "anthea.testing", ".tests.dll")


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest().upper()


def check(condition, message):
    if not condition:
        sys.exit("FAIL " + message)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--setup", required=True, type=Path)
    parser.add_argument("--extract", required=True, type=Path)
    parser.add_argument("--version", required=True)
    parser.add_argument("--guide-revision", required=True, type=int)
    parser.add_argument("--library-set", required=True)
    parser.add_argument("--stage", type=Path, default=Path("supporto/artefatti/installer/stage"))
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    root = Path.cwd()
    extract, stage = args.extract.resolve(), args.stage.resolve()

    commit = subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip()
    check(not subprocess.check_output(["git", "status", "--porcelain"], text=True).strip(), "albero dei sorgenti non pulito")
    project = (root / "src/ANTHEA.Desktop/ANTHEA.Desktop.csproj").read_text(encoding="utf-8-sig")
    declared = re.search(r"<Version>([^<]+)</Version>", project)
    check(declared and declared.group(1) == args.version, f"ANTHEA.Desktop.csproj dichiara {declared and declared.group(1)}")
    check(args.setup.name == f"ANTHEA-{args.version}-Setup-x64.exe", f"nome del setup {args.setup.name}")

    payload = []
    for name, target in (("app", ""), ("guide", "Guide"), ("esempi", "Esempi")):
        for staged in sorted((stage / name).rglob("*")):
            if not staged.is_file():
                continue
            relative = Path(target) / staged.relative_to(stage / name)
            extracted = extract / relative
            check(extracted.is_file() and sha(staged) == sha(extracted), f"file distribuito diverso dallo staging: {relative}")
            payload.append({"file": relative.as_posix(), "bytes": extracted.stat().st_size, "sha256": sha(extracted)})
    check(payload, f"staging vuoto: {stage}")

    manifest_path = root / "lib/Checker/manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    check(not manifest.get("candidate"), "lib/Checker contiene un set candidato")
    for assembly in manifest["assemblies"]:
        check(sha(extract / assembly["file"]) == assembly["sha256"].upper(), f"DLL GPC diversa dal manifest: {assembly['file']}")

    catalog = json.loads((root / "supporto/esempi/distribuzione/catalogo.json").read_text(encoding="utf-8-sig"))
    for entry in catalog["entries"]:
        check(sha(extract / "Esempi" / entry["path"]) == entry["sha256"].upper(), f"esempio diverso dal catalogo: {entry['path']}")

    guides = root / "supporto/documentazione/Guide_ANTHEA"
    for kind in ("pratica", "teorica"):
        source = guides / f"ANTHEA_Guida_{kind}_ITEC_Rev{args.guide_revision}.pdf"
        check(source.is_file(), f"guida assente: {source}")
        check(sha(extract / "Guide" / f"Guida {kind} ANTHEA.pdf") == sha(source), f"guida {kind} diversa dalla Rev{args.guide_revision}")
    check(sha(extract / "Guide/Indice-guide.pdf") == sha(root / "supporto/installer/Indice-guide.pdf"), "indice delle guide diverso")

    forbidden = sorted(p.relative_to(extract).as_posix() for p in extract.rglob("*") if any(x in p.name.lower() for x in FORBIDDEN))
    check(not forbidden, f"file vietati nel pacchetto: {forbidden}")

    product = subprocess.check_output(["powershell", "-NoProfile", "-Command",
                                       f"(Get-Item -LiteralPath '{extract / 'ANTHEA.exe'}').VersionInfo.ProductVersion"], text=True).strip()
    check(product == f"{args.version}+{commit}", f"versione del prodotto {product}")

    result = {
        "status": "PASS", "source_commit": commit, "source_clean": True, "version": args.version, "product_version": product,
        "library_set": args.library_set, "gpc_manifest_sha256": sha(manifest_path), "gpc_assemblies": len(manifest["assemblies"]),
        "setup": {"file": args.setup.name, "bytes": args.setup.stat().st_size, "sha256": sha(args.setup)},
        "application_files": sum(p.is_file() for p in (stage / "app").rglob("*")), "payload_files": len(payload),
        "guide_revision": args.guide_revision, "example_documents": len(catalog["entries"]),
        "example_modules": len({e["module"] for e in catalog["entries"]}), "forbidden_files": forbidden, "payload": payload,
    }
    out = args.out or args.setup.with_name("audit-payload.json")
    out.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in result.items() if k != "payload"}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
