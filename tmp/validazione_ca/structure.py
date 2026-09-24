from zipfile import ZipFile
from lxml import etree
from docx import Document
p=r'C:/Users/g.pacini/Desktop/MODELLO-RELAZIONE-ITEC-AA.docx';d=Document(p)
for i,ch in enumerate(d._element.body):
 text=''.join(ch.itertext()) if False else ' | '.join(ch.xpath('.//w:t/text()'))
 sect=ch.xpath('.//w:sectPr')
 print(i,etree.QName(ch).localname, text[:90], 'SECTION' if sect else '')
for n in ['Normal','Heading 1','Heading 2','Heading 3','Caption']:
 print(n,d.styles[n].element.xml)
for i,s in enumerate(d.sections):print('SECT',i,s._sectPr.xml)
