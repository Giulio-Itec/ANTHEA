from pathlib import Path
import json,math,datetime,openpyxl
from pypdf import PdfReader
import pypdfium2 as pdfium
from PIL import Image,ImageOps,ImageDraw
root=Path(__file__).resolve().parents[3];art=root/'supporto/artefatti/programma-anthea-20261006';out=root/'supporto/documentazione/Programma_ANTHEA/ANTHEA_Programma_sviluppo_2026-10-06'
d=json.loads((art/'contenuti.json').read_text(encoding='utf-8'));w=openpyxl.load_workbook(out.with_suffix('.xlsx'),data_only=True);formulas=openpyxl.load_workbook(out.with_suffix('.xlsx'),data_only=False)
assert len(w.sheetnames)==9
assert len(d['original'])==33 and len(d['tasks'])==48
for s in w:
    for row in s:
        for cell in row:assert cell.data_type!='e',(s.title,cell.coordinate,cell.value)
full=part=0
for i,phase in enumerate(d['phases'],13):
    items=[t for t in d['tasks'] if t['phase']==phase[0]]
    totals=[sum(t[k] for t in items) for k in ['lo','base','hi']]
    assert [w['Tempi'].cell(i,j).value for j in [3,4,5]]==totals
    f=math.ceil(totals[1]*1.2/5);p=math.ceil(totals[1]*1.2/2)
    assert w['Tempi'].cell(i,7).value==f and w['Tempi'].cell(i,10).value==p
    full+=f;part+=p
assert w['Sintesi']['B9'].value==full==72 and w['Sintesi']['C9'].value==part==171
assert w['Sintesi']['C13'].value==275
assert len(formulas['Milestone'].data_validations.dataValidation)>0
assert formulas['Attivita']['H6'].value==2 and formulas['Attivita']['I53'].value==5
reader=PdfReader(out.with_suffix('.pdf'));alltext='\n'.join(p.extract_text() for p in reader.pages)
for t in d['tasks']:assert t['id'] in alltext
for phase in d['phases']:assert phase[2] in alltext
for test in d['tests']:assert test[0] in alltext
assert all(len(p.extract_text())>200 for p in reader.pages)
pdf=pdfium.PdfDocument(out.with_suffix('.pdf'));thumbs=[]
for i,page in enumerate(pdf):
    img=page.render(scale=1.35).to_pil().convert('RGB');img.save(art/f'pdf-{i+1:02d}.png')
    thumb=ImageOps.contain(img,(265,375));canvas=Image.new('RGB',(280,402),'#e2e7ec');canvas.paste(thumb,((280-thumb.width)//2,5));ImageDraw.Draw(canvas).text((10,383),f'Pagina {i+1}',fill='black');thumbs.append(canvas)
for offset in range(0,len(thumbs),12):
    contact=Image.new('RGB',(1120,402*math.ceil(len(thumbs[offset:offset+12])/4)),'white')
    for i,im in enumerate(thumbs[offset:offset+12]):contact.paste(im,((i%4)*280,(i//4)*402))
    contact.save(art/f'pdf-contatto-{offset//12+1}.png')
report=dict(sheets=len(w.sheetnames),activities=len(d['tasks']),milestones=len(d['phases']),tests=len(d['tests']),original_items=len(d['original']),days=275,weeks_full=full,weeks_part=part,pdf_pages=len(pdf),formulas='PASS',coverage='PASS')
(art/'qa.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(report)
