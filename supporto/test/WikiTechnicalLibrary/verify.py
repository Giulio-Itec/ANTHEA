"""Verify numerical examples and provenance; render the two global guides for visual QA."""
from pathlib import Path
import json, math, re, sys
import pypdfium2 as pdfium
from PIL import Image,ImageDraw,ImageFont
sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'supporto/artefatti/guide_anthea_itec_rev29'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',18)
checks=[]
def check(ok,name):
    assert ok,name
    checks.append(name)
fyd=450/1.15;z=450;B=z*300*.5*17/1000
A=z*(2*math.pi*8**2/4)/200*fyd/1000
check(abs(A*2.5-221.276)<.01,'Taglio due rami Ø8')
A=z*(2*math.pi*10**2/4)/100*fyd/1000
check(abs(math.sqrt(A*(B-A))-490.8)<.1,'Intersezione puntone e staffe Ø10')
check(abs((25*30**2/8/.02+15*30**2/8/.04)/1000-182.8125)<1e-8,'Fasi sezione composta')
check(abs((3000/.72-3000*.35/.144+1000/.144)/1000-3.8194444)<1e-6,'Precompressione fibra superiore')
check(abs((1000e3/(80e9*(4*8**2/(12/.02))))*30*180/math.pi-.0503576)<1e-6,'Rotazione cassone')
check(abs(5*30000*30**4/(384*210e9*.3)*1000-5.022321)<1e-5,'Freccia impalcato')
check(abs(.848*2**2/1e-7/86400-392.5926)<.001,'Tempo di consolidazione')
index=json.loads((ROOT/'X.Desktop/Wiki/index.json').read_text(encoding='utf-8'))
# External corpus removed on 6/10/2026 (W0.5): only content written for ANTHEA.
check(not [x for x in index if x['key'].startswith(('mdp-','letture-'))],'Nessun contributo esterno nel catalogo')
theory=(ROOT/'supporto/docs/guida-teorica-anthea.md').read_text(encoding='utf-8').lower()
for term in ['Approfondimento ·','Letture tecniche','De Pisapia','CC BY-NC','marcodepisapia','geostru','simonecaffe','http://','https://']:
    check(term.lower() not in theory,'Guida teorica senza '+term)
reports=[]
for kind in ['pratica','teorica']:
    folder=OUT/kind/'visual';folder.mkdir(parents=True,exist_ok=True)
    pdfpath=ROOT/f'supporto/docs/guida-{kind}-anthea.pdf'
    doc=pdfium.PdfDocument(pdfpath)
    images=[];empties=[];topics={};risky=[]
    needles=['Taglio nel calcestruzzo armato e scelta','Due meccanismi che devono','Esempio numerico con due quantità','Dalle indagini geotecniche ai parametri','Funzioni disponibili e limiti del modulo','Wiki del modulo e aiuti','Cassoni sottili torsione','Precompressione delle travi','Stralli equilibrio','Biblioteca tecnica lezioni']
    for i in range(len(doc)):
        page=doc[i];textpage=page.get_textpage();text=textpage.get_text_range()
        if i<25:check(not re.search(r"Errore\. (?:L.intervallo|Il segnalibro|L.origine)|Error!|No table of contents",text,re.I),kind+' indice senza errori pagina '+str(i+1))
        if len(text.strip())<40:empties.append(i+1)
        for needle in needles:
            if needle in text and needle not in topics:topics[needle]=i+1
        # Sample every text rectangle for page-bound overflow, excluding no glyphs.
        w,h=page.get_size();count=textpage.count_rects()
        for j in range(count):
            l,b,r,t=textpage.get_rect(j)
            if l<-2 or r>w+2 or b<-2 or t>h+2:risky.append(dict(page=i+1,rect=[l,b,r,t]));break
        thumb=page.render(scale=300/w).to_pil().convert('RGB');thumb.thumbnail((300,425))
        canvas=Image.new('RGB',(320,455),'#eeeeee');canvas.paste(thumb,((320-thumb.width)//2,8));ImageDraw.Draw(canvas).text((12,431),str(i+1),fill='black',font=font)
        images.append(canvas)
        # Render a full readable page for all original lessons, and all source pages
        # independently of whether their contact sheet is selected for inspection.
        full=page.render(scale=1000/w).to_pil().convert('RGB');full.save(folder/f'page-{i+1:04d}.jpg',quality=85)
        textpage.close();page.close()
        if len(images)==36 or i==len(doc)-1:
            sheet=Image.new('RGB',(1920,2730),'white')
            for j,im in enumerate(images):sheet.paste(im,((j%6)*320,(j//6)*455))
            sheet.save(folder/f'contact-{(i//36)+1:02d}.jpg',quality=88);images=[]
        if (i+1)%100==0:print(kind,i+1,'pagine',flush=True)
    reports.append(dict(kind=kind,pages=len(doc),emptyPages=empties,outsidePage=risky,topicPages=topics))
    doc.close()
(OUT/'visual-audit.json').write_text(json.dumps(dict(checks=len(checks),documents=reports),ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(reports,ensure_ascii=False,indent=2))
