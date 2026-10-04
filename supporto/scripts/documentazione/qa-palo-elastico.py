from pathlib import Path
import json,re
from pypdf import PdfReader
import pypdfium2 as pdfium
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[3]
ART=ROOT/'supporto/artefatti/palo-elastico/documenti-rev13';ART.mkdir(parents=True,exist_ok=True)
records=[]
for kind in ('pratica','teorica'):
    path=ROOT/f'supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev13.pdf'
    reader=PdfReader(path);texts=[p.extract_text() or '' for p in reader.pages]
    hits=[i for i,t in enumerate(texts) if 'rispostaelasticadelpaloorizzontale' in re.sub(r'\s+','',t).lower()]
    assert len(hits)>1,(kind,hits)
    start=max(hits);end=next((i for i in range(start+1,len(texts)) if 'approfondimenti integrati' in texts[i].lower()),min(start+7,len(texts)))
    pages=sorted(set([0,1,2,3]+list(range(max(0,start-4),end+1))))
    document=pdfium.PdfDocument(str(path));tiles=[]
    for i in pages:
        picture=document[i].render(scale=1.35).to_pil().convert('RGB');picture.save(ART/f'{kind}-{i+1:03d}.png')
        picture.thumbnail((550,780));tile=Image.new('RGB',(570,810),'white');tile.paste(picture,((570-picture.width)//2,25));ImageDraw.Draw(tile).text((10,5),f'{kind} pagina {i+1}',fill='black');tiles.append(tile)
    for at in range(0,len(tiles),4):
        contact=Image.new('RGB',(1140,1620),'#ccd2db')
        for j,tile in enumerate(tiles[at:at+4]):contact.paste(tile,(j%2*570,j//2*810))
        contact.save(ART/f'{kind}-panoramica-{at//4+1}.png')
    assert (ROOT/f'supporto/docs/guida-{kind}-anthea.pdf').read_bytes()==path.read_bytes()
    records.append(dict(kind=kind,pages=len(texts),rendered=[i+1 for i in pages]))
(ART/'qa.json').write_text(json.dumps(records,indent=2),encoding='utf-8');print(json.dumps(records))
