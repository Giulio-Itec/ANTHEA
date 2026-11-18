"""Risorse statiche ITEC: copertina PDF vettoriale, formule Word e immagini matematiche per PDF."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
from copy import deepcopy
from lxml import etree as E
import json, sys
import posixpath
from pypdf import PdfReader, PdfWriter

ROOT = Path(__file__).resolve().parents[2]
QA = ROOT / 'supporto/artefatti/report-palo-programma-2026-11-16'
ASSETS = ROOT / 'src/ANTHEA.Reports/Templates'
SOURCE = ROOT / 'supporto/artefatti/proposta-report-palo-2026-11-16/Report palo proposta.docx'
W='http://schemas.openxmlformats.org/wordprocessingml/2006/main'
M='http://schemas.openxmlformats.org/officeDocument/2006/math'
NS={'w':W,'m':M}
def x(name, **attrs): return E.Element('{'+W+'}'+name, {'{'+W+'}'+k:str(v) for k,v in attrs.items()})
def xml(e): return E.tostring(e,xml_declaration=True,encoding='UTF-8',standalone=True)
def save(path,parts):
    with ZipFile(path,'w',ZIP_DEFLATED) as z:
        for n,v in parts.items():z.writestr(n,v)

QA.mkdir(parents=True,exist_ok=True);ASSETS.mkdir(parents=True,exist_ok=True)
with ZipFile(SOURCE) as z:parts={n:z.read(n) for n in z.namelist()}
doc=E.fromstring(parts['word/document.xml']);body=doc.find('w:body',NS);blocks=list(body)
indices=[57,58,61,66,82,83,120,130]
equations=[deepcopy(blocks[i]) for i in indices]
# Il motore attuale include c′ nei terreni coesivi. Il report ne descrive il comportamento reale.
for t in equations[2].findall('.//m:t',NS):
    if t.text == 'k':t.text='c′ + k'
for e in equations:
    pr=e.find('w:pPr',NS)
    if pr is not None:e.remove(pr)
    pr=x('pPr');pr.append(x('spacing',before=0,after=0));e.insert(0,pr)
root=E.Element('equations')
for i,e in enumerate(equations):
    item=E.SubElement(root,'equation',id=str(i));item.append(e)
parts['anthea/equations.xml']=xml(root)
section=deepcopy(blocks[-1])
for c in list(section):
    if E.QName(c).localname not in ['pgSz','pgMar']:section.remove(c)
from docx import Document
minimal=Document();minimal.save(QA/'Formule-base.docx')
with ZipFile(QA/'Formule-base.docx') as z:eqparts={n:z.read(n) for n in z.namelist()}
eqdoc=E.fromstring(eqparts['word/document.xml']);eqbody=eqdoc.find('w:body',NS);eqbody.clear()
for i,e in enumerate(equations):
    q=deepcopy(e)
    if i:q.find('w:pPr',NS).append(x('pageBreakBefore'))
    eqbody.append(q)
eqbody.append(section)
eqparts['word/document.xml']=xml(eqdoc)
save(QA/'Formule.docx',eqparts)
if '--finish' not in sys.argv:
    print('Preparato Formule.docx; rieseguire con --finish per il pacchetto.');sys.exit()
sys.path.insert(0,str(QA/'python-deps'))
import matplotlib
matplotlib.use('Agg')
from matplotlib.mathtext import math_to_image
from matplotlib.font_manager import FontProperties
from PIL import Image
matplotlib.rcParams['mathtext.fontset']='stix'
latex=[
 r'R_{c,cal}=R_{b,cal}+R_{s,cal}=\frac{\pi D^2}{4}q_b+\pi D\int_0^L\tau(z)\,dz',
 r'R_{t,cal}=R_{s,cal}',
 r"q_{b,D}=N_q\,\sigma'_v(L)\quad;\quad\tau_D(z)=c'+k\,\mu\,\sigma'_v(z)",
 r'q_{b,ND}=\sigma_v(L)+N_c\,c_u(L)\quad;\quad\tau_{ND}(z)=\alpha\,c_u(z)',
 r'R_{c,d}=\eta_c\,\min_r\left[\frac{R_{b,k,r}}{\gamma_b}+\frac{R_{s,k,r}}{\gamma_s}\right]',
 r'R_{t,d}=\eta_t\,\min_r\left[\frac{R_{s,k,r}}{\gamma_{st}}\right]',
 r'G_{p,k}=\gamma_p\,\frac{\pi D^2}{4}\,L',
 r'E_d\leq R_d\quad;\quad I=\frac{E_d}{R_d}\leq 1',
]
assert 'c′' in ''.join(equations[2].xpath('.//m:t/text()',namespaces=NS))
for i,formula in enumerate(latex):
    p=QA/f'equation-{i}.png'
    math_to_image('$'+formula+'$',str(p),prop=FontProperties(size=12),dpi=288,format='png',color='black')
    im=Image.open(p)
    parts[f'anthea/equation-{i}.png']=p.read_bytes()
    root[i].set('widthPt',str(im.width/4));root[i].set('heightPt',str(im.height/4))
parts['anthea/equations.xml']=xml(root)
writer=PdfWriter();writer.add_page(PdfReader(SOURCE.with_suffix('.pdf')).pages[0]);writer.write(QA/'Copertina.pdf')
parts['anthea/cover.pdf']=(QA/'Copertina.pdf').read_bytes()
# 23 blocchi: copertina originale con le sue sezioni. Nessun contenuto esemplificativo nel corpo.
body.clear()
for b in blocks[:23]:body.append(b)
parts['anthea/body-section.xml']=xml(blocks[-1])
parts['anthea/layout.xml']=b'<layout coverBlocks="23" />'
parts['word/document.xml']=xml(doc)
# Stili ITEC, numerazione esplicita dei titoli per concordanza DOCX/PDF.
styles=E.fromstring(parts['word/styles.xml'])
for name in ['Titolo1','Titolo2','Titolo3']:
    for st in styles.xpath('./w:style[@w:styleId="'+name+'"]',namespaces=NS):
        for n in st.findall('.//w:numPr',NS):n.getparent().remove(n)
parts['word/styles.xml']=xml(styles)
# Conservare solo le risorse raggiungibili: nessuna figura delle vecchie relazioni del modello.
rels=E.fromstring(parts['word/_rels/document.xml.rels'])
R='http://schemas.openxmlformats.org/officeDocument/2006/relationships'
used={v for e in body.iter() for k,v in e.attrib.items() if k.startswith('{'+R+'}')}
used.update(v for e in blocks[-1].iter() for k,v in e.attrib.items() if k.startswith('{'+R+'}'))
essential={'styles','settings','webSettings','fontTable','theme','numbering'}
for r in list(rels):
    if r.get('Id') not in used and r.get('Type','').split('/')[-1] not in essential:rels.remove(r)
parts['word/_rels/document.xml.rels']=xml(rels)
keep={'[Content_Types].xml'}
def visit(name):
    if name in keep or name not in parts:return
    keep.add(name)
    relname=posixpath.join(posixpath.dirname(name),'_rels',posixpath.basename(name)+'.rels') if name else '_rels/.rels'
    if relname not in parts:return
    keep.add(relname)
    for r in E.fromstring(parts[relname]):
        if r.get('TargetMode')!='External':visit(posixpath.normpath(posixpath.join(posixpath.dirname(name),r.get('Target'))).lstrip('/'))
keep.add('_rels/.rels')
for r in E.fromstring(parts['_rels/.rels']):visit(r.get('Target'))
keep.update(n for n in parts if n.startswith('anthea/'))
parts={n:v for n,v in parts.items() if n in keep}
types=E.fromstring(parts['[Content_Types].xml']);tn=E.QName(types).namespace
for t in list(types):
    if t.get('PartName') and t.get('PartName').lstrip('/') not in parts:types.remove(t)
if not any(t.get('Extension')=='pdf' for t in types):E.SubElement(types,'{'+tn+'}Default',Extension='pdf',ContentType='application/pdf')
parts['[Content_Types].xml']=xml(types)
save(ASSETS/'ItecPile.docx',parts)
(QA/'risorse.json').write_text(json.dumps({'source':str(SOURCE),'equations':len(equations),'coverBlocks':23},indent=2),encoding='utf-8')
print('Risorsa ItecPile.docx pronta.')
