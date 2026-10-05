"""Controllo dei PDF delle guide e dell'esempio dopo la correzione dei giunti."""
from pathlib import Path
import json,re
from pypdf import PdfReader
import pypdfium2 as pdfium

root=Path(__file__).resolve().parents[3]
out=root/'supporto/artefatti/palo-giunti/documenti'
out.mkdir(parents=True,exist_ok=True)
records=[]
for kind,needle in [('pratica','alcambiodiarmaturaladistintasegue'),('teorica','alcambiodidiametrooraggionominale')]:
    path=root/f'supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev24.pdf'
    reader=PdfReader(path);pdf=pdfium.PdfDocument(path)
    found=[i for i,p in enumerate(reader.pages) if needle in re.sub(r'\s+','',p.extract_text()).lower()]
    assert found,(kind,'Testo aggiornato assente')
    selected=sorted({0,*found,*(min(i+1,len(reader.pages)-1) for i in found)})
    for i in selected:pdf[i].render(scale=1.3).to_pil().save(out/f'{kind}-{i+1:03d}.png')
    assert path.read_bytes()==(root/f'supporto/docs/guida-{kind}-anthea.pdf').read_bytes()
    records.append(dict(file=str(path),pages=len(reader.pages),rendered=[i+1 for i in selected]))
for name,relative in [('indice','installer/Indice-guide.pdf'),('supporto','README.pdf'),('esempio','esempi/palo-orizzontale-armature/palo-12m-quattro-tratti.pdf')]:
    pdf=pdfium.PdfDocument(root/'supporto'/relative)
    for i in range(len(pdf)):pdf[i].render(scale=1.3).to_pil().save(out/f'{name}-{i+1:03d}.png')
(out/'qa.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
print(json.dumps(records))
