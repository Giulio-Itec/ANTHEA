"""Read-only inventory for the domain-boundary review; never a semantic compliance certificate.

MSBuild supplies the actual Release Compile items. Lexical type declarations, GPC imports
and JSON keys are review aids: aliases, unit adapters and presentation DTOs need human review.
No build, calculation, archive migration or library update is performed.
"""
from pathlib import Path
import argparse
import hashlib
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
TYPE = re.compile(r"^[ \t]*(?:public|internal|private|protected)\s+(?:(?:static|sealed|abstract|partial|readonly|ref)\s+)*(class|record(?:\s+struct)?|struct|enum|interface)\s+(\w+)", re.M)
IMPORT = re.compile(r"^[ \t]*(?:global\s+)?using\s+(?:\w+\s*=\s*)?(GPC\.[\w.]+)\s*;", re.M)
KEY = re.compile(r'\["([\w.]+)"\]|\(\s*"([\w.]+)"\s*,')


def git_commit(root):
    run = subprocess.run(["git", "-c", f"safe.directory={root.as_posix()}", "-C", str(root), "rev-parse", "HEAD"], capture_output=True, text=True, check=True)
    return run.stdout.strip()


def inspect(path, relative):
    raw = path.read_bytes()
    source = raw.decode("utf-8-sig")
    return {
        "file": relative, "sha256": hashlib.sha256(raw).hexdigest(),
        "types": [{"kind": m[1], "name": m[2], "line": source.count("\n", 0, m.start()) + 1} for m in TYPE.finditer(source)],
        "gpc_imports": sorted(set(IMPORT.findall(source))),
        "json_key_candidates": sorted(set(a or b for a, b in KEY.findall(source))),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    out = args.output.resolve()
    if not out.is_relative_to((ROOT / "supporto/artefatti").resolve()):
        raise ValueError("Inventory output must stay in supporto/artefatti.")
    out.mkdir(parents=True, exist_ok=True)
    projects = sorted([*ROOT.glob("X.*/*.csproj"), *ROOT.glob("src/*/*.csproj")])
    result = {"schema": 1, "kind": "inventory-not-compliance-test", "anthea_commit": git_commit(ROOT), "projects": [], "libraries": []}
    for project in projects:
        run = subprocess.run(["dotnet", "msbuild", str(project), "-nologo", "-p:Configuration=Release", "-getItem:Compile"], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace", check=True)
        data = json.loads(run.stdout)
        files, generated = [], 0
        for item in data["Items"]["Compile"]:
            path = Path(item["FullPath"])
            if "obj" in path.parts or "bin" in path.parts:
                generated += 1
                continue
            if not path.is_file():
                raise FileNotFoundError(path)
            relative = path.relative_to(ROOT).as_posix()
            files.append(inspect(path, relative))
        result["projects"].append({"project": project.relative_to(ROOT).as_posix(), "generated_items_skipped": generated, "files": sorted(files, key=lambda f: f["file"])})
    for repo in ["Model", "Checker"]:
        directory = ROOT.parent / repo
        listed = subprocess.run(["rg", "--files", "-g", "*.cs", "-g", "!**/obj/**", "-g", "!**/bin/**", str(directory)], capture_output=True, text=True, check=True)
        files = []
        for name in listed.stdout.splitlines():
            path = Path(name)
            relative = path.relative_to(directory)
            if any("test" in part.lower() or part in {"tools", "supporto"} for part in relative.parts[:-1]):
                continue
            files.append(inspect(path, repo + "/" + relative.as_posix()))
        result["libraries"].append({"repository": repo, "commit": git_commit(directory), "scope": "source inventory, not installed DLL API", "files": files})
    (out / "inventory.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"projects": len(result["projects"]), "compiled_source_files": sum(len(p["files"]) for p in result["projects"]), "library_source_files": sum(len(p["files"]) for p in result["libraries"]), "output": str(out / "inventory.json")}, ensure_ascii=False))


if __name__ == "__main__":
    main()
