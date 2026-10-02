"""Preserve previous editions, update global guide headers/index, build Rev09 with ITEC template.
Old Word/PDF files are archived only after the new edition is verified.
"""
from pathlib import Path
import shutil, json, hashlib, importlib.util
ROOT=Path(__file__).resolve().parents[3]; S=ROOT/'supporto'; ARCH=S/'SUPERATI/wiki-rev09-20261002'
ART=S/'artefatti/guide_anthea_itec_rev09'; ART.mkdir(parents=True,exist_ok=True)
registry=[]
def preserve(path, data=None):
    dest=ARCH/path.relative_to(S); dest.parent.mkdir(parents=True,exist_ok=True)
    if not dest.exists():
        if data is None: shutil.copy2(path,dest)
        else: dest.write_bytes(data)
    registry.append(dict(origine=str(path.relative_to(ROOT)),archivio=str(dest.relative_to(ROOT)),motivo='Edizione precedente alla Wiki Rev09',sostituzione='Guide globali Rev09 del 2 ottobre 2026',sha256=hashlib.sha256(dest.read_bytes()).hexdigest()))
for kind,boundary in [('pratica','## Wiki e centro della conoscenza'),('teorica','## Elementi Beam')]:
    source=S/f'docs/guida-{kind}-anthea.md'
    if 'revisione documentale 08' in source.read_text(encoding='utf-8'):
        preserve(source,source.read_bytes().split(boundary.encode('utf-8'))[0])
    p=source.with_suffix('.pdf')
    if p.is_file(): preserve(p)
    text=source.read_text(encoding='utf-8').replace('revisione documentale 08','revisione documentale 09')
    source.write_text(text,encoding='utf-8')
for p in (S/'documentazione/Guide_ANTHEA').glob('*Rev08.*'): preserve(p)
for relative in ['README.md','README.pdf','installer/Indice-guide.md','installer/Indice-guide.pdf','installer/README.md','installer/README.pdf']:
    if (S/relative).exists(): preserve(S/relative)
for rel in ['README.md','installer/README.md']:
    p=S/rel; p.write_text(p.read_text(encoding='utf-8').replace('Rev08','Rev09'),encoding='utf-8')
(ARCH/'registro.json').write_text(json.dumps(registry,ensure_ascii=False,indent=2),encoding='utf-8')
(ARCH/'README.md').write_text('# Edizioni precedenti alla Wiki\n\nOrigine: guide globali e indici Rev08 del 2 ottobre 2026. Motivo: introduzione del centro della conoscenza Wiki, del capitolo Elementi Beam e della guida Sezione in c.a. Revisione sostitutiva: guide globali Rev09 in supporto/documentazione/Guide_ANTHEA e sorgenti in supporto/docs.\n\nIl registro JSON conserva percorsi e hash. Nessun contenuto viene eliminato. Le copie Markdown conservano il testo precedente ai nuovi capitoli; le figure continuano a trovarsi nelle risorse originarie.\n',encoding='utf-8')
index='# ANTHEA Indice delle guide globali\n\nITEC Engineering · Revisione 09 · 2 ottobre 2026\n\nDue volumi globali consultabili anche nella Wiki integrata. La Wiki collega teoria, guide operative ed esempi nei moduli disponibili. Le appendici storiche mantengono data e campo di validità.\n\n'
for kind in ['pratica','teorica']:
    index+=f'## Guida {kind} ANTHEA\n\n'
    for line in (S/f'docs/guida-{kind}-anthea.md').read_text(encoding='utf-8').splitlines():
        if line.startswith('## ') and line!='## Approfondimenti integrati': index+='- '+line[3:]+'\n'
    index+='\n'
(S/'installer/Indice-guide.md').write_text(index.rstrip()+'\n',encoding='utf-8')
(ART/'artifact.md').write_text('# Aggiornamento delle due guide globali\n\nRev09: Wiki integrata, teoria Beam, guida Sezione in c.a., ricerca e lettura. Si conserva il modello ITEC esistente.\n',encoding='utf-8')
spec=importlib.util.spec_from_file_location('builder',S/'scripts/Build-AntheaGuides-Itec.py'); B=importlib.util.module_from_spec(spec);spec.loader.exec_module(B)
for kind in B.GUIDES: B.build(kind)
spec=importlib.util.spec_from_file_location('pdf',S/'scripts/documentazione/markdown-pdf.py'); P=importlib.util.module_from_spec(spec);spec.loader.exec_module(P)
for p in [ARCH/'README.md',S/'installer/Indice-guide.md',S/'README.md',S/'installer/README.md']: P.build(p)
