from pathlib import Path
import re,json
import pypdfium2 as pdfium
from PIL import Image
root=Path(__file__).resolve().parents[3];art=root/'supporto/artefatti/palo-approvazioni-staffe';out=art/'documenti';out.mkdir(exist_ok=True)
def normalized(s):return re.sub(r'\s+','',s).lower()
records=[]
for kind,needle in [('pratica','approvazionedelleipotesi'),('teorica','geometriadellestaffe')]:
    path=root/f'supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev30.pdf';pdf=pdfium.PdfDocument(path)
    found=None
    for i in range(len(pdf)):
        # The relevant body section follows the covers and contents.
        if i<15:continue
        if needle in normalized(pdf[i].get_textpage().get_text_range()):found=i;break
    assert found is not None,(kind,'sezione aggiornata assente')
    pages=[0,*range(found,min(found+3,len(pdf)))];
    for i in pages:pdf[i].render(scale=1.3).to_pil().save(out/f'{kind}-{i+1:03d}.png')
    assert path.read_bytes()==(root/f'supporto/docs/guida-{kind}-anthea.pdf').read_bytes()
    records.append(dict(file=str(path),pages=len(pdf),rendered=[i+1 for i in pages]))
for relative in ['README.pdf','installer/Indice-guide.pdf']:
    path=root/'supporto'/relative;pdf=pdfium.PdfDocument(path)
    for i in range(len(pdf)):pdf[i].render(scale=1.1).to_pil().save(out/f'{path.stem}-{i+1:03d}.png')
for p in (art/'chiarezza-finale').glob('*.bmp'):Image.open(p).save(p.with_suffix('.png'))
report=art/'chiarezza-finale/report.pdf'
if report.exists():
    pdf=pdfium.PdfDocument(report);selected=[]
    for i in range(len(pdf)):
        text=normalized(pdf[i].get_textpage().get_text_range())
        if any(k in text for k in ['fuoridalperimetro','distintaspirale','armatura:']):selected.append(i)
    assert selected
    for i in selected:pdf[i].render(scale=1.2).to_pil().save(out/f'report-{i+1:03d}.png')
    records.append(dict(report=str(report),pages=len(pdf),rendered=[i+1 for i in selected]))
(out/'qa.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(records))
