import pypdfium2 as pdf
from PIL import ImageChops
from pathlib import Path
import json
A=pdf.PdfDocument('tmp/validazione_ca/final.pdf');B=pdf.PdfDocument('tmp/validazione_ca/delivery.pdf')
print('Page counts',len(A),len(B))
changed=[]
for i in range(min(len(A),len(B))):
 a=A[i].render(scale=1.5).to_pil();b=B[i].render(scale=1.5).to_pil()
 if ImageChops.difference(a,b).getbbox():
  changed.append(i+1);b.save(f'tmp/validazione_ca/final_pages/delivery-{i+1}.png')
print('Visually changed pages',changed)
from zipfile import ZipFile
from lxml import etree
z=ZipFile('documentazione/Validazione_CA_ANTHEA/ANTHEA_Validazione_Calcestruzzo_Armato.docx')
xml=etree.fromstring(z.read('word/document.xml'));txt=' '.join(xml.xpath('//*[local-name()="t"]/text()'))
assert 'LIVELLO 1' not in txt and 'lorem ipsum' not in txt and 'R.Vallarino' not in txt
assert all(c['id'] in txt for c in json.load(open('tmp/validazione_ca/reference.json'))['cases'])
print('20 cases and no template sample content: OK')
