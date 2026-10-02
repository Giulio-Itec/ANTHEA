"""Render every page and record structural checks for consolidated manuals."""
from pathlib import Path
import json, re
import pypdfium2 as pdfium
from pypdf import PdfReader
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[3]; S=ROOT/'supporto'
ART=S/'artefatti/guide_anthea_itec_rev08/qa'
ART.mkdir(parents=True,exist_ok=True)
report=[]
paths=list((S/'documentazione/Guide_ANTHEA').glob('*Rev08.pdf'))
paths += [S/'README.pdf',S/'installer/README.pdf',S/'installer/Indice-guide.pdf',S/'SUPERATI/guide-unificate-rev08-20261002/README.pdf']
paths += [S/'SUPERATI/README.pdf']
for index,path in enumerate(paths):
    out=ART/f'{index:02}-{path.stem}';out.mkdir(exist_ok=True)
    doc=pdfium.PdfDocument(str(path)); tiles=[]
    for n,page in enumerate(doc):
        im=page.render(scale=1.35).to_pil().convert('RGB');im.save(out/f'pagina-{n+1:03}.png')
        thumb=im.copy();thumb.thumbnail((400,566))
        tile=Image.new('RGB',(420,596),'white');tile.paste(thumb,((420-thumb.width)//2,24));ImageDraw.Draw(tile).text((10,5),str(n+1),fill='black');tiles.append(tile)
    for start in range(0,len(tiles),12):
        group=tiles[start:start+12]; sheet=Image.new('RGB',(1680,596*((len(group)+3)//4)),'#dddddd')
        for n,tile in enumerate(group):sheet.paste(tile,((n%4)*420,(n//4)*596))
        sheet.save(out/f'panoramica-{start//12+1:02}.png')
    reader=PdfReader(path); texts=[p.extract_text() or '' for p in reader.pages]
    assert all(t.strip() for t in texts),path
    assert '\ufffd' not in ''.join(texts),path
    report.append(dict(file=str(path.relative_to(ROOT)),pagine=len(doc),pagine_renderizzate=len(tiles),caratteri_estratti=sum(map(len,texts))))
    print(path.name,len(doc),'pagine')
(ART/'controlli.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
