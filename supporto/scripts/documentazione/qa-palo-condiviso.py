"""Render the revised guide chapters, example report and current indices for visual QA."""
from pathlib import Path
import json,re
from pypdf import PdfReader
import pypdfium2 as pdfium
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[3];ART=ROOT/'supporto/artefatti/palo-condiviso/documenti';ART.mkdir(parents=True,exist_ok=True)
records=[]
paths=[(k,ROOT/f'supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{k}_ITEC_Rev16.pdf') for k in ('pratica','teorica')]
paths += [('esempio',ROOT/'supporto/artefatti/palo-condiviso/esempio.pdf'),('indice',ROOT/'supporto/installer/Indice-guide.pdf'),('supporto',ROOT/'supporto/README.pdf')]
for kind,path in paths:
    reader=PdfReader(path);texts=[p.extract_text() or '' for p in reader.pages]
    if kind in ('pratica','teorica'):
        hits=[i for i,t in enumerate(texts) if 'rispostaelasticadelpaloorizzontale' in re.sub(r'\s+','',t).lower()];assert hits,(kind,'chapter missing')
        start=max(hits);next_title='wiki e centro della conoscenza' if kind=='pratica' else 'elementi beam'
        end=next((i for i in range(start+1,len(texts)) if next_title in texts[i].lower()),min(start+12,len(texts)-1))
        pages=sorted(set([0,1,2,3]+list(range(start,end+1))))
        assert (ROOT/f'supporto/docs/guida-{kind}-anthea.pdf').read_bytes()==path.read_bytes()
        assert 'media iniziale' in '\n'.join(texts).lower()
    else:pages=list(range(len(texts)))
    document=pdfium.PdfDocument(str(path));tiles=[]
    for i in pages:
        picture=document[i].render(scale=1.4).to_pil().convert('RGB');picture.save(ART/f'{kind}-{i+1:03d}.png')
        picture.thumbnail((570,800));tile=Image.new('RGB',(590,830),'white');tile.paste(picture,((590-picture.width)//2,25));ImageDraw.Draw(tile).text((10,5),f'{kind} pagina {i+1}',fill='black');tiles.append(tile)
    for at in range(0,len(tiles),4):
        contact=Image.new('RGB',(1180,1660),'#ccd2db')
        for j,tile in enumerate(tiles[at:at+4]):contact.paste(tile,(j%2*590,j//2*830))
        contact.save(ART/f'{kind}-panoramica-{at//4+1:02d}.png')
    records.append(dict(file=str(path),pages=len(texts),rendered=[i+1 for i in pages]))
(ART/'qa.json').write_text(json.dumps(records,indent=2),encoding='utf-8');print(json.dumps(records))
