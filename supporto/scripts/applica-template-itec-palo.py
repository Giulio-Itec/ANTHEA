"""Applica il modello ITEC alla proposta Word del palo senza modificare ANTHEA."""
from copy import deepcopy
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
from lxml import etree as ET
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / 'supporto/artefatti/template-itec-palo-2026-11-16'
QA = WORK / 'qa'
REFERENCE = Path(r'C:\Users\g.sciarpa\Desktop\Template\MODELLO-RELAZIONE-ITEC-AA_Ponte in misto.docx')
SOURCE = ROOT / 'supporto/SUPERATI/artefatti/proposta-report-palo-2026-11-16/ante-template-ITEC-2026-11-16/Report palo proposta.docx'
DEST = QA / 'candidata-v3/Report palo proposta.docx'
NS = {'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main',
      'r':'http://schemas.openxmlformats.org/officeDocument/2006/relationships',
      'm':'http://schemas.openxmlformats.org/officeDocument/2006/math',
      'a':'http://schemas.openxmlformats.org/drawingml/2006/main',
      'wp':'http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing'}
REL = 'http://schemas.openxmlformats.org/package/2006/relationships'

def q(n):
    prefix, local = n.split(':')
    return '{'+NS[prefix]+'}'+local

def el(n, **attrs):
    e = ET.Element(q(n))
    for k,v in attrs.items(): e.set(q('w:'+k), str(v))
    return e

def child(parent,n,**attrs):
    e=parent.find(n,NS)
    if e is None: e=ET.SubElement(parent,q(n))
    for k,v in attrs.items():e.set(q('w:'+k),str(v))
    return e

def txt(e): return ''.join(e.xpath('.//w:t/text()|.//m:t/text()',namespaces=NS))

def replace_text(p,text):
    runs=p.findall('w:r',NS)
    rp=deepcopy(runs[0].find('w:rPr',NS)) if runs and runs[0].find('w:rPr',NS) is not None else None
    for c in list(p):
        if c.tag!=q('w:pPr'):p.remove(c)
    r=el('w:r')
    if rp is not None:r.append(rp)
    for i,part in enumerate(text.split('\n')):
        if i:r.append(el('w:br'))
        t=el('w:t');t.set('{http://www.w3.org/XML/1998/namespace}space','preserve');t.text=part;r.append(t)
    p.append(r)

def add_run(p,text):
    r=el('w:r');t=el('w:t');t.set('{http://www.w3.org/XML/1998/namespace}space','preserve');t.text=text;r.append(t);p.append(r)

def field(p,code,result):
    for kind,value in [('begin',None),('instruction',code),('separate',None),('text',result),('end',None)]:
        r=el('w:r')
        if kind=='instruction':
            t=el('w:instrText');t.set('{http://www.w3.org/XML/1998/namespace}space','preserve');t.text=' '+value+' ';r.append(t)
        elif kind=='text':
            t=el('w:t');t.text=str(value);r.append(t)
        else:r.append(el('w:fldChar',fldCharType=kind))
        p.append(r)

def pnew(text='',style=None):
    p=el('w:p')
    if style:child(child(p,'w:pPr'),'w:pStyle',val=style)
    if text:add_run(p,text)
    return p

def normalize_runs(node,size=None,bold=None):
    for rp in node.findall('.//w:rPr',NS):
        for c in list(rp):
            if ET.QName(c).localname not in ['vertAlign','i','iCs','b','bCs','noProof','lang']:rp.remove(c)
        if size:
            child(rp,'w:rFonts',ascii='Manrope',hAnsi='Manrope',cs='Manrope')
            child(rp,'w:sz',val=size*2)
        if bold is not None:child(rp,'w:b',val=int(bold))
    if size:
        for r in node.findall('.//w:r',NS):
            rp=child(r,'w:rPr');child(rp,'w:sz',val=size*2)
            child(rp,'w:rFonts',ascii='Manrope',hAnsi='Manrope',cs='Manrope')
            if bold is not None:child(rp,'w:b',val=int(bold))

def format_table(table, selected=None, widths=None):
    t=deepcopy(table)
    if selected is not None:
        for row in t.findall('w:tr',NS):
            cells=row.findall('w:tc',NS)
            for c in cells:row.remove(c)
            for i in selected:row.append(cells[i])
    n=len(t.find('w:tr',NS).findall('w:tc',NS))
    if widths is None:
        original=table.find('w:tblGrid',NS)
        weights=[int(x.get(q('w:w'))) for x in original]
        if selected is not None:weights=[weights[i] for i in selected]
    else:weights=widths
    sizes=[round(9638*v/sum(weights)) for v in weights]
    sizes[-1]+=9638-sum(sizes)
    pr=t.find('w:tblPr',NS)
    if pr is None:pr=el('w:tblPr');t.insert(0,pr)
    for c in list(pr):pr.remove(c)
    child(pr,'w:tblW',w=9638,type='dxa');child(pr,'w:jc',val='center');child(pr,'w:tblLayout',type='fixed')
    borders=child(pr,'w:tblBorders')
    for side in ['top','left','bottom','right','insideH','insideV']:child(borders,'w:'+side,val='single',sz=4,color='808080')
    margins=child(pr,'w:tblCellMar')
    for side,val in [('left',70),('right',70),('top',40),('bottom',40)]:child(margins,'w:'+side,w=val,type='dxa')
    grid=t.find('w:tblGrid',NS)
    if grid is None:grid=el('w:tblGrid');t.insert(1,grid)
    for c in list(grid):grid.remove(c)
    for w in sizes:childcol=el('w:gridCol',w=w);grid.append(childcol)
    for ri,row in enumerate(t.findall('w:tr',NS)):
        for override in row.findall('w:tblPrEx',NS):row.remove(override)
        rpr=child(row,'w:trPr')
        for c in list(rpr):rpr.remove(c)
        rpr.append(el('w:cantSplit'))
        if ri==0:rpr.append(el('w:tblHeader'))
        for ci,cell in enumerate(row.findall('w:tc',NS)):
            cp=child(cell,'w:tcPr')
            for c in list(cp):cp.remove(c)
            child(cp,'w:tcW',w=sizes[ci],type='dxa');child(cp,'w:vAlign',val='center')
            child(cp,'w:shd',val='clear',fill='DDEBF7' if ri==0 else 'FFFFFF')
            for p in cell.findall('w:p',NS):
                pp=child(p,'w:pPr')
                for c in list(pp):pp.remove(c)
                child(pp,'w:spacing',before=0,after=0,line=240,lineRule='auto')
                child(pp,'w:jc',val='left' if ci==0 and sizes[ci]>1700 and ri>0 else 'center')
                if ri==0:child(pp,'w:keepNext')
            normalize_runs(cell,10,ri==0)
    return t

def caption(kind,title,chapter,count,keep=True):
    p=pnew(style='Didascalia');pp=child(p,'w:pPr')
    child(pp,'w:spacing',before=80,after=80)
    if keep:child(pp,'w:keepNext')
    add_run(p,kind+' ');field(p,'STYLEREF 1 \\s',chapter);add_run(p,'.')
    field(p,'SEQ '+kind+' \\* ARABIC \\s 1',count);add_run(p,'  '+title)
    return p

baseline=json.loads((QA/'template-inventory.json').read_text(encoding='utf-8'))
assert hashlib.sha256(REFERENCE.read_bytes()).hexdigest()==baseline['sha256']
with ZipFile(REFERENCE) as z:parts={n:z.read(n) for n in z.namelist()}
with ZipFile(SOURCE) as z:source_parts={n:z.read(n) for n in z.namelist()}
document=ET.fromstring(parts['word/document.xml']);body=document.find('w:body',NS);orig=list(body)
source_root=ET.fromstring(source_parts['word/document.xml']);source_body=source_root.find('w:body',NS)

# Copertina: stessa geometria, loghi e componenti del documento ITEC.
cover=[deepcopy(x) for x in orig[:23]]
replace_text(cover[0].findall('.//w:p',NS)[12],'[Committente]')
replace_text(cover[3],'Verifica della capacità portante verticale del palo')
replace_text(cover[5],'CIG: [da compilare]   CUP: [da compilare]')
team=cover[7].findall('.//w:p',NS)
for i,p in enumerate(team):
    if i in [3,6,9,12,15,18,21,24]:replace_text(p,'')
    elif i in [4,7,10,13,16,19,22,25]:replace_text(p,'[Nominativo]' if i==4 else '')
obj=cover[9].findall('.//w:p',NS)
replace_text(obj[1],'Relazione geotecnica')
for i,p in enumerate(obj[2:]):
    replace_text(p,'Compressione e trazione' if i==0 else '')
    pp=p.find('w:pPr',NS)
    if pp is not None:
        for c in list(pp):
            if c.tag in [q('w:numPr'),q('w:pStyle'),q('w:ind')]:pp.remove(c)
meta=cover[11].findall('.//w:p',NS)
replace_text(meta[0],'Livello di progetto: [fase]')
replace_text(meta[2],'[Codice documento]')
replace_text(meta[3],'Codice progetto: [codice]')
replace_text(meta[5],'[numero]')
rev=cover[19].findall('.//w:p',NS)
for index,value in [(6,'[rev]'),(7,'[Descrizione revisione]'),(8,'[data]'),(9,'[sigla]'),(10,'[sigla]'),(11,'[sigla]')]:replace_text(rev[index],value)

# I disegni del report sono aggiunti al pacchetto senza alterare i loghi ITEC.
rels=ET.fromstring(parts['word/_rels/document.xml.rels'])
source_rels=ET.fromstring(source_parts['word/_rels/document.xml.rels'])
rid_map={}
for rel in source_rels:
    if rel.get('Type','').endswith('/image'):
        old=rel.get('Id');target=rel.get('Target');newid='rIdPalo'+str(len(rid_map)+1)
        newtarget='media/palo-'+Path(target).name
        rid_map[old]=newid;nr=ET.SubElement(rels,'{'+REL+'}Relationship')
        nr.attrib.update({'Id':newid,'Type':rel.get('Type'),'Target':newtarget})
        parts['word/'+newtarget]=source_parts['word/'+target]

for e in list(body):body.remove(e)
for e in cover:body.append(e)
body.append(deepcopy(orig[23]));body.append(deepcopy(orig[24]))
toc=pnew();field(toc,'TOC \\o "1-2" \\h \\z \\u','Indice');body.append(toc)
toc_end=deepcopy(orig[233]);sect=toc_end.find('w:pPr/w:sectPr',NS)
child(sect,'w:type',val='continuous')
body.append(toc_end)

chapter=0;table_count=0;figure_count=0;source_to_caption={}
new_nodes=[]
for i,original in enumerate(source_body):
    if i==0 or original.tag==q('w:sectPr'):continue
    if i in [109,112,118,122]:continue # Didascalie degli annessi ricostruite con le tabelle leggibili in verticale.
    e=deepcopy(original)
    for n in e.xpath('.//w:lastRenderedPageBreak|.//w:sectPr|.//w:bookmarkStart|.//w:bookmarkEnd',namespaces=NS):n.getparent().remove(n)
    for n in e.xpath('.//w:br[@w:type="page"]',namespaces=NS):n.getparent().remove(n)
    for n in e.iter():
        for attr in ['embed','link','id']:
            k=q('r:'+attr)
            if n.get(k) in rid_map:n.set(k,rid_map[n.get(k)])
    if e.tag==q('w:tbl'):
        if i in [110,113,119,123]:
            splits={
                110:[([0,1,2,3,4],'Parametri di peso del terreno alla profondità z',[1.7,2,2.7,2.7,2.7]),([0,1,5,6,7,8,9,10],'Parametri di resistenza e coefficienti alla profondità z',[1.7,1.7,2,2,2,1.5,1.5,1.5])],
                113:[(None,'Tensioni e fattori di capacità portante',[1.6,2,2,1.4,1.4,3.1,3.3])],
                119:[([0,1,2,3,4,5],'Resistenze unitarie e contributi calcolati in condizioni drenate',[1.7,2.6,3,2.6,3,3]),([0,6,7,8,9,10],'Resistenze unitarie e contributi calcolati in condizioni non drenate',[1.7,2.6,3,2.6,3,3])],
                123:[([0,1,2,3,4],'Resistenze caratteristiche lungo il palo',[2,3,3,3,3]),([0,5,6,7,8],'Resistenze di progetto lungo il palo',[2,3,3,3,3])]
            }
            for columns,title,widths in splits[i]:
                table_count+=1
                cp=caption('Tabella',title,chapter,table_count)
                if i==123 and columns[1]==1:child(child(cp,'w:pPr'),'w:pageBreakBefore')
                new_nodes.append(cp)
                new_nodes.append(format_table(e,columns,widths))
        else:
            if i==49:
                table_count+=1;new_nodes.append(caption('Tabella','Efficienza del gruppo',chapter,table_count))
            new_nodes.append(format_table(e,widths=[2.1,2.5,3.1,3.1,3.1,3.1] if i==82 else None))
        continue
    if e.tag!=q('w:p'):continue
    text=txt(e);st=e.find('w:pPr/w:pStyle',NS);style=st.get(q('w:val')) if st is not None else ''
    if not text and not e.xpath('.//w:drawing|.//m:oMath',namespaces=NS):continue
    oldpp=e.find('w:pPr',NS)
    if oldpp is not None:e.remove(oldpp)
    pp=el('w:pPr');e.insert(0,pp)
    normalize_runs(e)
    if style in ['Titolo1','Titolo2']:
        if i==31:style='Titolo2'
        text=re.sub(r'^\d+\s+','',text).replace('Appendice A','Annesso A').replace('Appendice B','Annesso B')
        replace_text(e,text)
        for rpr in e.findall('.//w:rPr',NS):rpr.getparent().remove(rpr)
        child(pp,'w:pStyle',val=style);child(pp,'w:keepNext');child(pp,'w:keepLines')
        if i==98:child(pp,'w:pageBreakBefore')
        if style=='Titolo1':
            chapter+=1;table_count=0;figure_count=0
            if chapter>1:child(pp,'w:pageBreakBefore')
    elif style=='Didascalia':
        match=re.match(r'(Tabella|Figura)\s+\S+\s+(.+)',text)
        if match:
            kind,title=match.groups()
            if kind=='Tabella':table_count+=1;count=table_count
            else:figure_count+=1;count=figure_count
            source_to_caption[text.split('  ')[0]]=f'{kind} {chapter}.{count}'
            e=caption(kind,title,chapter,count,keep=kind=='Tabella')
    else:
        child(pp,'w:spacing',after=60)
        child(pp,'w:widowControl')
        if i in [7,17,26,30,38,42,46,51,55,64,68,69,79,84,91,97,103,108,115,117,121,125]:
            normalize_runs(e,10)
        if e.xpath('.//m:oMath|.//w:drawing',namespaces=NS):
            child(pp,'w:jc',val='center');child(pp,'w:spacing',before=40,after=60)
            child(pp,'w:keepNext')
            if i==56:
                for extent in e.xpath('.//wp:extent|.//a:xfrm/a:ext',namespaces=NS):
                    for dimension in ['cx','cy']:extent.set(dimension,str(round(int(extent.get(dimension))*.9)))
        # I rimandi seguono gli annessi e la numerazione della relazione ITEC.
        for t in e.findall('.//w:t',NS):
            if t.text:t.text=t.text.replace('nelle appendici A e B','negli annessi A e B').replace('tabella 13','tabella 5.3')
    new_nodes.append(e)

for e in new_nodes:body.append(e)
main_sect=deepcopy(orig[1410].find('w:pPr/w:sectPr',NS))
child(main_sect,'w:type',val='nextPage')
for e in list(main_sect):
    if e.tag in [q('w:headerReference'),q('w:titlePg')]:main_sect.remove(e)
header=el('w:headerReference',type='default');header.set(q('r:id'),'rId16');main_sect.insert(0,header)
body.append(main_sect)

# Un solo piede per corpo e annessi, con conteggio continuo della sezione.
settings=ET.fromstring(parts['word/settings.xml']);child(settings,'w:updateFields',val='true')
for e in settings.findall('w:evenAndOddHeaders',NS):settings.remove(e)
for fname in ['word/footer1.xml','word/footer4.xml']:
    footer=ET.fromstring(parts[fname])
    for t in footer.findall('.//w:t',NS):
        if t.text and t.text.startswith('MODELLO-RELAZIONE'):t.text='Report palo proposta'
    parts[fname]=ET.tostring(footer,xml_declaration=True,encoding='UTF-8',standalone=True)
parts['word/settings.xml']=ET.tostring(settings,xml_declaration=True,encoding='UTF-8',standalone=True)
parts['word/document.xml']=ET.tostring(document,xml_declaration=True,encoding='UTF-8',standalone=True)
parts['word/_rels/document.xml.rels']=ET.tostring(rels,xml_declaration=True,encoding='UTF-8',standalone=True)
DEST.parent.mkdir(parents=True,exist_ok=True)
with ZipFile(DEST,'w',ZIP_DEFLATED) as z:
    for name,data in parts.items():z.writestr(name,data)
editable={'word/document.xml','word/settings.xml','word/_rels/document.xml.rels','word/footer1.xml','word/footer4.xml'}
preserved=[]
with ZipFile(REFERENCE) as z:
    for name in z.namelist():
        if name not in editable:
            assert parts[name]==z.read(name),name
            preserved.append(name)
(QA/'fidelity-authoring.json').write_text(json.dumps({'reference_sha256':baseline['sha256'],'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'output':str(DEST),'preserved_parts':preserved,'edited_parts':sorted(editable),'chapter_count':chapter,'caption_map':source_to_caption},ensure_ascii=False,indent=2),encoding='utf-8')
print(DEST)
print('Parti del template conservate byte per byte:',len(preserved))
