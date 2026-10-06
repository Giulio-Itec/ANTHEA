from pathlib import Path
import json, shutil
from pypdf import PdfReader
import pypdfium2 as pdfium

root=Path(__file__).resolve().parents[3]
support=root/'supporto'
out=support/'artefatti/report-short-cls-20261005/guide'
out.mkdir(parents=True,exist_ok=True)
archive=support/'SUPERATI/report-short-rev28-20261005'
records=json.loads((archive/'registro.json').read_text(encoding='utf-8'))
for kind,needle in [('pratica','Report short della sezione'),('teorica','Selezione delle verifiche nel report short')]:
    current=support/f'documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev28.pdf'
    reader=PdfReader(current)
    found=[i for i,p in enumerate(reader.pages) if needle.lower() in ' '.join(p.extract_text().split()).lower()]
    assert found, (kind,'new content missing')
    assert current.read_bytes()==(support/f'docs/guida-{kind}-anthea.pdf').read_bytes()
    pdf=pdfium.PdfDocument(current)
    for i in sorted({j for i in found for j in (i,min(i+1,len(pdf)-1))}):
        pdf[i].render(scale=1.15).to_pil().save(out/f'{kind}-{i+1}.png')
    for suffix in ['docx','pdf']:
        previous=support/f'documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev27.{suffix}'
        if previous.exists():
            dest=archive/previous.relative_to(support)
            assert previous.resolve().is_relative_to(support.resolve()) and dest.resolve().is_relative_to(archive.resolve())
            dest.parent.mkdir(parents=True,exist_ok=True)
            assert not dest.exists()
            shutil.move(previous,dest)
            records.append(dict(origine=str(previous.relative_to(support)),archivio=str(dest.relative_to(support)),motivo='Sostituita dalla revisione 28 con Report short CLS',revisione_sostitutiva='28'))
    print(kind,len(pdf),'pages; changed pages:',[i+1 for i in found])
(archive/'registro.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
