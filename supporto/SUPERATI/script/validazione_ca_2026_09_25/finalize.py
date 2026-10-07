from zipfile import ZipFile,ZIP_DEFLATED
from pathlib import Path
from lxml import etree as E
from hashlib import sha256
import json
repo=Path(__file__).resolve().parents[3]
root=repo/'supporto/artefatti/validazione_ca_2026_09_25'
out=repo/'supporto/documentazione/Validazione_CA_ANTHEA/ANTHEA_Validazione_Calcestruzzo_Armato_Rev01.docx'
import pypdfium2
pages=len(pypdfium2.PdfDocument(root/'final.pdf'))
W='http://schemas.openxmlformats.org/wordprocessingml/2006/main';R='http://schemas.openxmlformats.org/officeDocument/2006/relationships'
with ZipFile(out) as a,ZipFile(root/'word_updated.docx') as b:
 data={n:a.read(n) for n in a.namelist()}
 ra=E.fromstring(a.read('word/_rels/document.xml.rels'));rb=E.fromstring(b.read('word/_rels/document.xml.rels'))
 oldrels={r.get('Id'):r for r in ra};newrels={r.get('Id'):r for r in rb}
 def ident(z,r):
  typ=r.get('Type');target=r.get('Target')
  if typ.endswith('/image'):return typ,sha256(z.read('word/'+target)).hexdigest()
  return typ,target
 amap={ident(a,r):r.get('Id') for r in ra}
 src=E.fromstring(data['word/document.xml']); upd=E.fromstring(b.read('word/document.xml'))
 original_sections=src.findall('.//{'+W+'}sectPr')
 # Tutte le sezioni e le loro intestazioni/piè di pagina rimangono quelle del modello adattato.
 usections=upd.findall('.//{'+W+'}sectPr');assert len(original_sections)==len(usections)==5
 for old,new in zip(original_sections,usections):new.getparent().replace(new,old)
 # Gli id nelle sezioni sono già quelli originali; rimappa gli altri riferimenti del corpo.
 for el in upd.iter():
  if el.tag in ['{'+W+'}headerReference','{'+W+'}footerReference']:continue
  for key,value in list(el.attrib.items()):
   if key.startswith('{'+R+'}') and value in newrels:
    sig=ident(b,newrels[value]);assert sig in amap,(value,sig)
    el.set(key,amap[sig])
 data['word/document.xml']=E.tostring(upd,xml_declaration=True,encoding='UTF-8',standalone=True)
 # Cache dei contatori nel footer corpo; i campi PAGE/SECTIONPAGES restano attivi.
 foot=E.fromstring(data['word/footer4.xml'])
 for instr in foot.findall('.//{'+W+'}instrText'):
  if 'SECTIONPAGES' in instr.text:
   parent=instr.getparent();sib=parent.getnext()
   while sib is not None:
    t=sib.find('.//{'+W+'}t')
    if t is not None:t.text=str(pages-2);break
    sib=sib.getnext()
 data['word/footer4.xml']=E.tostring(foot,xml_declaration=True,encoding='UTF-8',standalone=True)
tmp=root/'final_cached.docx'
with ZipFile(tmp,'w',ZIP_DEFLATED) as z:
 for n,content in data.items():z.writestr(n,content)
out.write_bytes(tmp.read_bytes())
with ZipFile(Path(r'C:/Users/g.pacini/Desktop/MODELLO-RELAZIONE-ITEC-AA.docx')) as src,ZipFile(out) as final:
 preserve=[n for n in src.namelist() if n.startswith(('word/media/','word/header','word/_rels/header','word/theme/','word/font','word/numbering'))]
 assert all(src.read(n)==final.read(n) for n in preserve)
 print('Preserved parts:',len(preserve),'bytes:',out.stat().st_size)
 print('Final docx:',out)
