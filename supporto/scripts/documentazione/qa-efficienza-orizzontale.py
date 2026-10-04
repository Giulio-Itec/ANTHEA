"""Render the changed guide sections and test report; archive superseded current editions after PDF verification."""
from pathlib import Path
from pypdf import PdfReader
from PIL import Image, ImageDraw
import pypdfium2 as pdfium
import json, re, hashlib
ROOT = Path(__file__).resolve().parents[3]
ART = ROOT/'supporto/artefatti/efficienza-orizzontale/documenti'
ART.mkdir(parents=True, exist_ok=True)
records=[]
for kind, count in [('pratica',5),('teorica',8)]:
    path=ROOT/f'supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev11.pdf'
    reader=PdfReader(path); texts=[p.extract_text() for p in reader.pages]
    hits=[i for i,t in enumerate(texts) if 'efficienzaorizzontale' in re.sub(r'\s+','',t).lower()]
    assert hits and len(hits)>1, kind
    start=max(hits); pages=sorted(set([0,1,2,3] + list(range(start,min(start+count,len(texts))))))
    document=pdfium.PdfDocument(str(path)); tiles=[]
    for i in pages:
        picture=document[i].render(scale=1.4).to_pil().convert('RGB'); picture.save(ART/f'{kind}-{i+1:03d}.png')
        picture.thumbnail((550,780)); tile=Image.new('RGB',(570,810),'white'); tile.paste(picture,((570-picture.width)//2,25)); ImageDraw.Draw(tile).text((10,5),f'{kind} pagina {i+1}',fill='black'); tiles.append(tile)
    for at in range(0,len(tiles),4):
        contact=Image.new('RGB',(1140,1620),'#ccd2db')
        for j,tile in enumerate(tiles[at:at+4]): contact.paste(tile,(j%2*570,j//2*810))
        contact.save(ART/f'{kind}-panoramica-{at//4+1}.png')
    records.append(dict(pdf=str(path),pages=len(texts),rendered=[i+1 for i in pages],sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    assert (ROOT/f'supporto/docs/guida-{kind}-anthea.pdf').read_bytes()==path.read_bytes()
(ART/'qa.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
print(json.dumps(records,indent=2))
