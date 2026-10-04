"""Update the two canonical editions and their index, then use the existing ITEC builder."""
from pathlib import Path
import importlib.util,re,json,shutil,hashlib
ROOT=Path(__file__).resolve().parents[3];S=ROOT/'supporto'
for kind in ['pratica','teorica']:
    p=S/f'docs/guida-{kind}-anthea.md';text=p.read_text(encoding='utf-8')
    text=re.sub(r'Edizione [34] del [234] ottobre 2026 — revisione documentale \d+','Edizione 4 del 4 ottobre 2026 — revisione documentale 14',text)
    p.write_text(text,encoding='utf-8')
index='# ANTHEA Indice delle guide globali\n\nITEC Engineering · Revisione 14 · 4 ottobre 2026\n\nEngineering Handbook: capitoli, navigazione e sei pagine pilota. Le appendici storiche conservano il loro campo originale; la revisione non equivale alla riscrittura di tutti i contenuti preesistenti.\n\n'
for kind in ['pratica','teorica']:
    index+=f'## Guida {kind} ANTHEA\n\n'
    index+=f'[Word Rev14](../documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev14.docx) · [PDF Rev14](../documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev14.pdf)\n\n'
    index+='\n'.join('- '+line[3:] for line in (S/f'docs/guida-{kind}-anthea.md').read_text(encoding='utf-8').splitlines() if line.startswith('## '))+'\n\n'
(S/'installer/Indice-guide.md').write_text(index.rstrip()+'\n',encoding='utf-8')
spec=importlib.util.spec_from_file_location('builder',S/'scripts/Build-AntheaGuides-Itec.py');B=importlib.util.module_from_spec(spec);spec.loader.exec_module(B)
for kind in B.GUIDES:B.build(kind)
spec=importlib.util.spec_from_file_location('pdf',S/'scripts/documentazione/markdown-pdf.py');P=importlib.util.module_from_spec(spec);spec.loader.exec_module(P)
for relative in ['README.md','installer/README.md']:
    p=S/relative;old=p.read_bytes();archive=S/'SUPERATI/wiki-handbook-rev14-20261003'/relative
    if not archive.exists():
        archive.parent.mkdir(parents=True,exist_ok=True);archive.write_bytes(old)
        if p.with_suffix('.pdf').exists():shutil.copy2(p.with_suffix('.pdf'),archive.with_suffix('.pdf'))
        registry=S/'SUPERATI/wiki-handbook-rev14-20261003/registro.json'
        records=json.loads(registry.read_text(encoding='utf-8'))
        records.append(dict(origine=str(p.relative_to(ROOT)),archivio=str(archive.relative_to(ROOT)),motivo='Indice aggiornato alla Rev14',sostituzione=str(p.relative_to(ROOT)),sha256=hashlib.sha256(old).hexdigest()))
        registry.write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
    text=old.decode('utf-8').replace('\r\r\n','\n').replace('\r\n','\n').replace('Rev13 del 3 ottobre 2026','Rev14 del 4 ottobre 2026').replace('Rev13','Rev14')
    p.write_text(text,encoding='utf-8')
for p in [S/'installer/Indice-guide.md',S/'README.md',S/'installer/README.md']:
    output=S/'artefatti/wiki-handbook'/p.relative_to(S).with_suffix('.pdf')
    P.build(p,output)
    shutil.copy2(output,p.with_suffix('.pdf'))
