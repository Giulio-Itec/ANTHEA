"""Verifica PDF delle guide e delle tavole dei muri, con immagini di tutte le pagine."""
from pathlib import Path
import hashlib,json,re
from pypdf import PdfReader
import pypdfium2 as pdfium
from PIL import Image,ImageDraw
root=Path(__file__).resolve().parents[3];art=root/'supporto/artefatti/muri-materiali-distinta-20261005';out=art/'qa';out.mkdir(exist_ok=True)
records=[]
def normalize(s):return re.sub(r'\s+','',s).lower().replace('’',"'")
def inspect(path,name,needles=()):
    reader=PdfReader(path);pdf=pdfium.PdfDocument(path);texts=[normalize(p.extract_text()) for p in reader.pages]
    for needle in needles:assert any(normalize(needle) in t for t in texts),(path,needle)
    found={i for i,t in enumerate(texts) if any(normalize(n) in t for n in needles)}
    found.update({0});selected={j for i in found for j in range(max(0,i-1),min(len(pdf),i+3))}
    folder=out/name;folder.mkdir(exist_ok=True)
    thumbs=[]
    for i in range(len(pdf)):
        page=pdf[i]; thumb=page.render(scale=.5).to_pil().convert('RGB');thumb.thumbnail((310,430))
        tile=Image.new('RGB',(330,465),'#eeeeee');tile.paste(thumb,((330-thumb.width)//2,22));ImageDraw.Draw(tile).text((10,5),f'{name} p.{i+1}',fill='black');thumbs.append(tile)
        page.render(scale=1.15).to_pil().save(folder/f'page-{i+1:03d}.png')
    for start in range(0,len(thumbs),12):
        sheet=Image.new('RGB',(990,1860),'white')
        for n,tile in enumerate(thumbs[start:start+12]):sheet.paste(tile,((n%3)*330,(n//3)*465))
        sheet.save(out/f'{name}-contact-{start//12+1:02d}.png')
    records.append(dict(file=str(path.relative_to(root)),pages=len(pdf),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),changed_pages=[i+1 for i in sorted(selected)]))
    pdf.close()
for kind,needles in [('pratica',['Materiali e copriferro del muro','Tavola delle armature e distinta ferri del muro']),('teorica',['Geometria e quantità della distinta ferri del muro','46,800823495'])]:
    path=root/f'supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev27.pdf'
    inspect(path,kind,needles)
    assert path.read_bytes()==(root/f'supporto/docs/guida-{kind}-anthea.pdf').read_bytes()
for path,name in [(art/'interfaccia/distinta-due-zone.pdf','tavola-due-zone'),(art/'interfaccia/distinta-unica.pdf','tavola-unica'),(art/'interfaccia/report-materiali.pdf','report'),(art/'qa-docx/distinta-due-zone.pdf','word-due-zone'),(art/'qa-docx/distinta-unica.pdf','word-unica'),(root/'supporto/README.pdf','supporto'),(root/'supporto/installer/Indice-guide.pdf','indice')]:
    inspect(path,name,['Distinta ferri del tratto di muro'] if name=='report' else ())
(out/'qa.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(records,ensure_ascii=False))
