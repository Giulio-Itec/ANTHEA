"""Resa delle pagine aggiornate, provenienza NTC e verifica delle consegne."""
from pathlib import Path
import json,re,hashlib
from pypdf import PdfReader
import pypdfium2 as pdfium
from PIL import Image
root=Path(__file__).resolve().parents[3];art=root/'supporto/artefatti/palo-sisma-testa';out=art/'documenti';out.mkdir(exist_ok=True)
records=[]
def normalized(s):return re.sub(r'\s+','',s).lower().replace('’',"'")
for kind,needle in [('pratica','apriretrattidiarmatura'),('teorica',"l'estensioneminimanominaleè10d")]:
    path=root/f'supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev26.pdf'
    reader=PdfReader(path);pdf=pdfium.PdfDocument(path)
    found=[i for i,p in enumerate(reader.pages) if needle in normalized(p.extract_text())]
    assert found,(kind,'Contenuto sismico aggiornato assente')
    selected=sorted({0,*found,*(min(i+j,len(reader.pages)-1) for i in found for j in (1,2))})
    for i in selected:pdf[i].render(scale=1.3).to_pil().save(out/f'{kind}-{i+1:03d}.png')
    assert path.read_bytes()==(root/f'supporto/docs/guida-{kind}-anthea.pdf').read_bytes()
    records.append(dict(file=str(path),pages=len(pdf),rendered=[i+1 for i in selected]))
for name,relative in [('indice','installer/Indice-guide.pdf'),('supporto','README.pdf')]:
    pdf=pdfium.PdfDocument(root/'supporto'/relative)
    for i in range(len(pdf)):pdf[i].render(scale=1.3).to_pil().save(out/f'{name}-{i+1:03d}.png')
for path in (art/'sisma-visibile').glob('*.bmp'):Image.open(path).save(path.with_suffix('.png'))
report=art/'sisma-visibile/report-sisma.pdf'
if report.exists():
    reader=PdfReader(report);pdf=pdfium.PdfDocument(report)
    pages=[i for i,p in enumerate(reader.pages) if 'controllosismico' in normalized(p.extract_text())]
    assert pages,'Controlli sismici assenti dal PDF del report'
    for i in pages:pdf[i].render(scale=1.2).to_pil().save(out/f'report-{i+1:03d}.png')
    records.append(dict(report=str(report),pages=len(pdf),rendered=[i+1 for i in pages]))
(out/'qa.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
source=root/'supporto/artefatti/palo-chiarezza/ntc-2018-gu.pdf'
sources=dict(consultato='2026-10-05',ntc=dict(url='https://www.gazzettaufficiale.it/eli/gu/2018/02/20/42/so/8/sg/pdf',paragrafo='7.2.5 Fondazioni su pali',pagina_pdf=217,pagina_stampata=213,pdf_locale=str(source),sha256=hashlib.sha256(source.read_bytes()).hexdigest(),visivo='fonti/ntc-pdf217.png'),modifiche2023='https://www.gazzettaufficiale.it/atto/serie_generale/caricaArticoloDefault/originario?atto.codiceRedazionale=23A01847&atto.dataPubblicazioneGazzetta=2023-03-22&atto.tipoProvvedimento=DECRETO')
(art/'fonti/fonti-verificate.json').write_text(json.dumps(sources,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(records))
