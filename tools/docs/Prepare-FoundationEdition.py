"""Prepare the existing global guide pipeline, preserving its ITEC template."""
from pathlib import Path
from hashlib import sha256
from zipfile import ZipFile
import json
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[2]
for p in (ROOT/'supporto/docs').glob('guida-*-anthea.md'):
    text=p.read_text(encoding='utf-8').replace('Edizione 5 aggiornata il 12 ottobre 2026 — revisione documentale 33','Edizione 5 aggiornata il 12 ottobre 2026 — revisione documentale 34')
    text='\n'.join('$$ '+line[2:-2].strip() if line.startswith('$$') and line.endswith('$$') else line for line in text.split('\n'))
    p.write_text(text,encoding='utf-8',newline='\r\n')
art=ROOT/'supporto/artefatti/guide_anthea_itec_rev34';art.mkdir(parents=True,exist_ok=True)
template=Path('C:/Users/g.pacini/Desktop/MODELLO-RELAZIONE-ITEC-AA.docx')
with ZipFile(template) as z:
    ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
    doc=ET.fromstring(z.read('word/document.xml'))
    sections=[ET.tostring(s,encoding='unicode') for s in doc.findall('.//w:sectPr',ns)]
    (art/'template-sections.json').write_text(json.dumps(sections,indent=2),encoding='utf-8')
(art/'artifact.md').write_text('''# Aggiornamento delle guide globali ANTHEA

Revisione 34: stratigrafia comune, materiali, pressioni di contatto e viste della fondazione.
Il modello ITEC originale resta immutato; la pipeline Build/Render/Finalize conserva parti opache,
stili, intestazioni e relazioni del modello, con audit nel finalizzatore. Impaginazione e stili
sono quelli delle guide Rev33, senza nuova identità grafica. Le nuove formule usano OMML.
Le sezioni del modello sono registrate in template-sections.json. Si aggiornano solo i contenuti
dei due sorgenti canonici, copertina e campi dell'indice. Non alterare il modello sorgente.

Template: '''+str(template)+'\nSHA-256: '+sha256(template.read_bytes()).hexdigest()+'\n',encoding='utf-8')
print('Edizione 34 preparata')
