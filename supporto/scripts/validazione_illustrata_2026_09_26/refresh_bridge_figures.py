"""Replace only native bridge screenshots; preserve layout and field caches."""
from pathlib import Path
from zipfile import ZipFile,ZIP_DEFLATED
from lxml import etree as E
from PIL import Image
from hashlib import sha256
R=Path(__file__).resolve().parents[3]
A=R/'supporto/artefatti/validazione_illustrata_2026_09_26'
DOC=R/'supporto/documentazione/Validazione_CA_ANTHEA/ANTHEA_Validazione_Software_CA_e_Ponti_Rev03.docx'
with ZipFile(DOC) as z:parts={n:z.read(n) for n in z.namelist()}
root=E.fromstring(parts['word/document.xml']);rels=E.fromstring(parts['word/_rels/document.xml.rels'])
ns={'wp':'http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing','a':'http://schemas.openxmlformats.org/drawingml/2006/main'}
RP='http://schemas.openxmlformats.org/package/2006/relationships';RR='http://schemas.openxmlformats.org/officeDocument/2006/relationships'
count=0
for pr in root.findall('.//wp:docPr',ns):
    descr=pr.get('descr','')
    if not descr.startswith('ANTHEA caso P-'):continue
    id=descr.split(' · ')[0].removeprefix('ANTHEA caso ');file=A/'screens-bridge'/(id+'.png')
    if not file.exists():continue
    assert Image.open(file).size==Image.open(A/'screens'/(id+'.png')).size
    rid='rIdBridgeCapture'+str(count+1);target='media/bridge-results-'+id+'.png'
    assert not any(r.get('Id')==rid for r in rels)
    E.SubElement(rels,'{'+RP+'}Relationship',Id=rid,Type=RR+'/image',Target=target)
    pr.getparent().find('.//a:blip',ns).set('{'+RR+'}embed',rid)
    parts['word/'+target]=file.read_bytes();count+=1
assert count==20,count
parts['word/document.xml']=E.tostring(root,xml_declaration=True,encoding='UTF-8',standalone=True)
parts['word/_rels/document.xml.rels']=E.tostring(rels,xml_declaration=True,encoding='UTF-8',standalone=True)
with ZipFile(DOC,'w',ZIP_DEFLATED) as z:
    for n,data in parts.items():z.writestr(n,data)
print('Replaced bridge result screens',count)
