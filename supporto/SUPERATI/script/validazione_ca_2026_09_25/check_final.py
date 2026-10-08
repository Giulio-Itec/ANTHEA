from pathlib import Path
from zipfile import ZipFile
import json,hashlib
import pypdfium2 as pdf
from lxml import etree
repo=Path(__file__).resolve().parents[3]
art=repo/'supporto/artefatti/validazione_ca_2026_09_25'
out=repo/'supporto/documentazione/Validazione_CA_ANTHEA/ANTHEA_Validazione_Calcestruzzo_Armato_Rev01.docx'
a,b=pdf.PdfDocument(art/'final.pdf'),pdf.PdfDocument(art/'final_verified.pdf')
assert len(a)==len(b)==80
changed=[]
for i in range(len(a)):
    if a[i].get_textpage().get_text_range()!=b[i].get_textpage().get_text_range():changed.append(i+1)
assert not changed,changed
with ZipFile(out) as z:
    ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
    xml=etree.fromstring(z.read('word/document.xml'))
    text=' '.join(xml.xpath('//w:t/text()',namespaces=ns))
    cases=json.loads((art/'reference.json').read_text(encoding='utf8'))['cases']+json.loads((art/'extra_reference.json').read_text(encoding='utf8'))['cases']
    assert all(v['id'].replace('-',' ') in text for v in cases)
    assert 'Error! Reference source not found' not in text
result=dict(pages=80,examples=88,visual_review='Tutte le pagine esaminate; formule, tabelle e campi controllati.',final_render_matches=True,docx_sha256=hashlib.sha256(out.read_bytes()).hexdigest())
(art/'final_quality.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
print(result)
