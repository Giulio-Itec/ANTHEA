from zipfile import ZipFile
from lxml import etree
A=ZipFile('supporto/documentazione/Validazione_CA_ANTHEA/ANTHEA_Validazione_Calcestruzzo_Armato.docx'); B=ZipFile('supporto/tmp/validazione_ca/word_updated.docx')
for z in [A,B]:
 print('REL',z.filename)
 for e in etree.fromstring(z.read('word/_rels/document.xml.rels')):
  if any(k in e.get('Type') for k in ['header','footer','image']):print(e.attrib)
print('new',set(B.namelist())-set(A.namelist()))
