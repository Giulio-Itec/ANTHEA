"""Preserve images referenced by the previous guide sources, keeping their relative paths usable."""
from pathlib import Path
import re, json, shutil, hashlib
ROOT=Path(__file__).resolve().parents[3]; S=ROOT/'supporto'; ARCH=S/'SUPERATI/wiki-rev09-20261002'
registry=json.loads((ARCH/'registro.json').read_text(encoding='utf-8'))
known={r['archivio'] for r in registry}
for source in (ARCH/'docs').glob('*.md'):
    for relative in re.findall(r'^!\[[^\]]*\]\(([^)]+)\)',source.read_text(encoding='utf-8'),re.M):
        original=(S/'docs'/relative).resolve(); target=(source.parent/relative).resolve()
        assert original.is_relative_to(ROOT) and target.is_relative_to(ARCH)
        target.parent.mkdir(parents=True,exist_ok=True)
        if not target.exists(): shutil.copy2(original,target)
        identity=str(target.relative_to(ROOT))
        if identity not in known:
            registry.append(dict(origine=str(original.relative_to(ROOT)),archivio=identity,motivo='Figura necessaria alla consultazione della precedente edizione; originale ancora in uso',sostituzione='Guide Rev09',sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
            known.add(identity)
(ARCH/'registro.json').write_text(json.dumps(registry,ensure_ascii=False,indent=2),encoding='utf-8')
print('Immagini delle guide precedenti conservate con percorsi relativi validi')
