"""Render every guide page and retain readable sheets for the revised chapters."""
from pathlib import Path
import pypdfium2 as pdfium
from pypdf import PdfReader
from PIL import Image,ImageDraw
import json,re,sys
REV=sys.argv[1] if len(sys.argv)>1 else '14'
ROOT=Path(__file__).resolve().parents[3];S=ROOT/'supporto';OUT=S/('artefatti/wiki-integrazione/pdf' if REV=='15' else 'artefatti/wiki-handbook/pdf')
results=[]
for kind,marker in [('pratica','Engineering Handbook'),('teorica','Instabilità delle aste compresse')]:
    path=S/f'documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev{REV}.pdf'
    assert path.read_bytes()==(S/f'docs/guida-{kind}-anthea.pdf').read_bytes()
    texts=[p.extract_text() or '' for p in PdfReader(path).pages]
    assert all(t.strip() for t in texts)
    out=OUT/kind;out.mkdir(parents=True,exist_ok=True)
    doc=pdfium.PdfDocument(str(path));tiles=[];changed=[]
    normalize=lambda text:re.sub(r'\s+','',text).casefold()
    start=0 if REV=='15' else min(i for i,t in enumerate(texts) if normalize(marker) in normalize(t) and i>20)
    for n,page in enumerate(doc):
        im=page.render(scale=1.1).to_pil().convert('RGB');im.save(out/f'page-{n+1:03}.png')
        thumb=im.copy();thumb.thumbnail((225,320));tile=Image.new('RGB',(235,344),'#eeeeee');tile.paste(thumb,(5,20));ImageDraw.Draw(tile).text((5,3),str(n+1),fill='black');tiles.append(tile)
        if n>=start:
            tile=Image.new('RGB',(im.width+10,im.height+28),'#eeeeee');tile.paste(im,(5,24));ImageDraw.Draw(tile).text((10,5),str(n+1),fill='black');changed.append(tile)
    for group,prefix,cols,batch in [(tiles,'all',6,24),(changed,'revised',2,4)]:
        for offset in range(0,len(group),batch):
            subset=group[offset:offset+batch];w,h=subset[0].size
            sheet=Image.new('RGB',(w*cols,h*((len(subset)+cols-1)//cols)),'#eeeeee')
            for j,tile in enumerate(subset):sheet.paste(tile,((j%cols)*w,(j//cols)*h))
            sheet.save(out/f'{prefix}-{offset//batch+1:02}.png')
    results.append(dict(volume=kind,pages=len(texts),reviewFrom=start+1,matchingMarkdownPdf=True))
    print(kind,len(texts),'pages; revised from',start+1)
for rel in ['installer/Indice-guide.pdf','README.pdf','installer/README.pdf']:
    p=S/rel;folder=OUT/'indices'/p.parent.name;folder.mkdir(parents=True,exist_ok=True)
    for n,page in enumerate(pdfium.PdfDocument(str(p))):page.render(scale=1.1).to_pil().save(folder/f'{p.stem}-{n+1:02}.png')
(OUT/'checks.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
