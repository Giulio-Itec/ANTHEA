"""Publish the consolidated Rev15 sources using the established ITEC pipeline."""
from pathlib import Path
import importlib.util,shutil
ROOT=Path(__file__).resolve().parents[3];S=ROOT/'supporto';ART=S/'artefatti/wiki-integrazione'
index='# ANTHEA Indice delle guide globali\n\nITEC Engineering · Revisione 15 · 4 ottobre 2026\n\nEngineering Handbook: 49 articoli consolidati in 12 capitoli. I resoconti precedenti sono conservati nell’archivio Rev14; i vecchi indirizzi Wiki raggiungono le pagine correnti. Lo stato editoriale non equivale a una certificazione normativa.\n\n'
for kind in ['pratica','teorica']:
    index+=f'## Guida {kind} ANTHEA\n\n[Word Rev15](../documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev15.docx) · [PDF Rev15](../documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev15.pdf)\n\n'
    index+='\n'.join('- '+line[3:] for line in (S/f'docs/guida-{kind}-anthea.md').read_text(encoding='utf-8').splitlines() if line.startswith('## '))+'\n\n'
(S/'installer/Indice-guide.md').write_text(index.rstrip()+'\n',encoding='utf-8')
for rel in ['README.md','installer/README.md']:
    p=S/rel;p.write_text(p.read_text(encoding='utf-8').replace('Rev14','Rev15'),encoding='utf-8')
spec=importlib.util.spec_from_file_location('builder',S/'scripts/Build-AntheaGuides-Itec.py');B=importlib.util.module_from_spec(spec);spec.loader.exec_module(B)
B.ART.mkdir(parents=True,exist_ok=True)
(B.ART/'artifact.md').write_text('# Specifica interna\n\nAggiornare le due guide globali Rev15 nel modello ITEC originale, conservando formule modificabili, indici e immagini. Output Word e PDF; controllo di identità dei contenuti e rendering di ogni pagina.\n',encoding='utf-8')
for kind in B.GUIDES:B.build(kind)
spec=importlib.util.spec_from_file_location('pdf',S/'scripts/documentazione/markdown-pdf.py');P=importlib.util.module_from_spec(spec);spec.loader.exec_module(P)
for p in [S/'installer/Indice-guide.md',S/'README.md',S/'installer/README.md']:
    output=ART/p.relative_to(S).with_suffix('.pdf');P.build(p,output);shutil.copy2(output,p.with_suffix('.pdf'))
