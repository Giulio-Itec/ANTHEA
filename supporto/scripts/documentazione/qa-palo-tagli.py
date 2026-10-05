"""Controlla le edizioni Rev25 e rende le pagine aggiornate per ispezione."""
from pathlib import Path
import json,re
from pypdf import PdfReader
import pypdfium2 as pdfium

root=Path(__file__).resolve().parents[3]
out=root/'supporto/artefatti/palo-tagli-tratti/documenti'
out.mkdir(parents=True,exist_ok=True)
records=[]
for kind,needle in [('pratica','lafinedeltrattoèlafinefisicadellebarre'),('teorica','lapartizionedetermina itaglifisici'.replace(' ',''))]:
    path=root/f'supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev25.pdf'
    reader=PdfReader(path);pdf=pdfium.PdfDocument(path)
    found=[i for i,p in enumerate(reader.pages) if needle in re.sub(r'\s+','',p.extract_text()).lower()]
    assert found,(kind,'Testo aggiornato assente')
    selected=sorted({0,*found,*(min(i+1,len(reader.pages)-1) for i in found)})
    for i in selected:pdf[i].render(scale=1.3).to_pil().save(out/f'{kind}-{i+1:03d}.png')
    assert path.read_bytes()==(root/f'supporto/docs/guida-{kind}-anthea.pdf').read_bytes()
    records.append(dict(file=str(path),pages=len(reader.pages),rendered=[i+1 for i in selected]))
for name,relative in [('indice','installer/Indice-guide.pdf'),('supporto','README.pdf'),('esempio12','esempi/palo-orizzontale-armature/palo-12m-quattro-tratti.pdf'),('esempio20','esempi/palo-orizzontale-armature/palo-20m-tagli-12-18-20.pdf')]:
    pdf=pdfium.PdfDocument(root/'supporto'/relative)
    for i in range(len(pdf)):pdf[i].render(scale=1.3).to_pil().save(out/f'{name}-{i+1:03d}.png')
    records.append(dict(file=relative,pages=len(pdf)))
(out/'qa.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
report=root/'supporto/artefatti/palo-tagli-tratti/ui-finale/esempio.pdf'
if report.exists():
    reader=PdfReader(report);pdf=pdfium.PdfDocument(report)
    pages=[i for i,p in enumerate(reader.pages) if 'ognitrattodefiniscelafinefisica' in re.sub(r'\s+','',p.extract_text()).lower()]
    assert pages,'Report privo della nuova regola dei tagli'
    for i in pages:pdf[i].render(scale=1.3).to_pil().save(out/f'report-{i+1:03d}.png')
    print('Report aggiornato, pagine:',[i+1 for i in pages])
print(json.dumps(records))
