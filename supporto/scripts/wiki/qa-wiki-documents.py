"""Render every current PDF page, create contact sheets and audit document parity."""
from pathlib import Path
import json, re, unicodedata
import pypdfium2 as pdfium
from pypdf import PdfReader
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[3]; S=ROOT/'supporto'; ART=S/'artefatti/wiki/latex-layout/pdf'; ART.mkdir(parents=True,exist_ok=True)
report=[]
for kind,marker in [('pratica','Wiki e centro della conoscenza'),('teorica','Elementi Beam')]:
    path=S/f'documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev10.pdf'; out=ART/kind; out.mkdir(exist_ok=True)
    assert path.read_bytes()==(S/f'docs/guida-{kind}-anthea.pdf').read_bytes(),'PDF omonimo non aggiornato'
    texts=[p.extract_text() or '' for p in PdfReader(path).pages]
    assert all(t.strip() for t in texts),'Pagina vuota'
    assert not any('\ufffd' in t or '□' in t for t in texts),'Glifi sostitutivi'
    starts=[i for i,t in enumerate(texts) if marker.casefold() in t.casefold() and i>15]; assert starts,'Capitolo nuovo assente'
    start=min(starts); doc=pdfium.PdfDocument(str(path)); tiles=[]; changed=[]
    def normalized(value): return re.sub('[^a-z0-9]','', ''.join(c for c in unicodedata.normalize('NFD',value.lower()) if unicodedata.category(c)!='Mn'))
    sign_phrase="N è negativo a compressione" if kind=='pratica' else "N negativo a compressione"
    assert normalized(sign_phrase) in normalized(''.join(texts[start:])), 'PDF non allineato alla convenzione UI corrente'
    for n,page in enumerate(doc):
        im=page.render(scale=1.35).to_pil().convert('RGB'); im.save(out/f'pagina-{n+1:03}.png')
        thumb=im.copy();thumb.thumbnail((400,566)); tile=Image.new('RGB',(420,596),'white');tile.paste(thumb,((420-thumb.width)//2,24));ImageDraw.Draw(tile).text((10,5),str(n+1),fill='black');tiles.append(tile)
        if n>=start:
            large=im.copy();large.thumbnail((650,920));tile=Image.new('RGB',(670,950),'white');tile.paste(large,((670-large.width)//2,25));ImageDraw.Draw(tile).text((12,4),str(n+1),fill='black');changed.append(tile)
    for group,prefix,width,height,cols in [(tiles,'panoramica',420,596,4),(changed,'nuovi-capitoli',670,950,3)]:
        for offset in range(0,len(group),12):
            subset=group[offset:offset+12]; sheet=Image.new('RGB',(width*cols,height*((len(subset)+cols-1)//cols)),'#dddddd')
            for n,tile in enumerate(subset):sheet.paste(tile,((n%cols)*width,(n//cols)*height))
            sheet.save(out/f'{prefix}-{offset//12+1:02}.png')
    report.append(dict(volume=kind,pagine=len(texts),tutte_renderizzate=True,nuovi_capitoli_da_pagina=start+1,pdf_markdown_identico=True))
    print(kind,len(texts),'pagine; nuovi capitoli da',start+1)
for path in [S/'installer/Indice-guide.pdf',S/'README.pdf',S/'installer/README.pdf',S/'SUPERATI/wiki-rev09-20261002/README.pdf']:
    folder=ART/path.parent.name;folder.mkdir(exist_ok=True)
    for n,page in enumerate(pdfium.PdfDocument(str(path))):page.render(scale=1.35).to_pil().save(folder/f'{path.stem}-{n+1:02}.png')
(ART/'controlli.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
