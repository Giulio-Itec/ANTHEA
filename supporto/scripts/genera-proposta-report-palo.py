"""Genera la proposta editoriale del report palo, senza eseguire calcoli di progetto."""
from pathlib import Path
from copy import deepcopy
from zipfile import ZipFile, ZIP_DEFLATED
import hashlib
import json
import re
import sys

from docx import Document
from docx.shared import Cm, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.enum.section import WD_SECTION_START, WD_ORIENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'supporto/artefatti/proposta-report-palo-2026-11-16'
QA = OUT / 'qa'
SOURCE = QA / 'riferimento.docx'
FINAL = OUT / 'Report palo proposta.docx'
HASH = '492e76f3e23694a4921c0bf5bbc1fd565e632618ce4b1b3e7d35d91a6407fbc6'
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == HASH
QA.mkdir(parents=True, exist_ok=True)
doc = Document(SOURCE)

# La struttura viene ricostruita sulla copia del riferimento, come richiesto.
for node in list(doc._element.body):
    if node.tag != qn('w:sectPr'):
        doc._element.body.remove(node)
section = doc.sections[0]
section.page_width, section.page_height = Cm(21), Cm(29.7)
section.top_margin, section.bottom_margin = Cm(1.8), Cm(1.7)
section.left_margin, section.right_margin = Cm(2), Cm(2)
section.header_distance, section.footer_distance = Cm(.7), Cm(.7)
section.different_first_page_header_footer = False
section.start_type = WD_SECTION_START.NEW_PAGE
for h in [section.header, section.first_page_header, section.even_page_header]:
    for n in list(h._element):
        h._element.remove(n)
    h._element.append(OxmlElement('w:p'))

def style_font(st, size, bold=False, color='000000'):
    st.font.name = 'Manrope'
    st.font.size = Pt(size)
    st.font.bold = bold
    st.font.color.rgb = RGBColor.from_string(color)
    rp = st.element.get_or_add_rPr()
    rf = rp.find(qn('w:rFonts'))
    if rf is not None:
        for k in list(rf.attrib):
            if 'Theme' in k or 'theme' in k:
                del rf.attrib[k]
    cc = rp.find(qn('w:color'))
    if cc is not None:
        for k in list(cc.attrib):
            if k != qn('w:val'):
                del cc.attrib[k]
    pf = st.paragraph_format
    pf.alignment = WD_ALIGN_PARAGRAPH.LEFT
    pf.space_after = Pt(5)
    pf.line_spacing = 1.08
    pf.widow_control = True
    for prop in ['w:pBdr','w:shd','w:numPr']:
        for el in st.element.findall('.//' + qn(prop)):
            el.getparent().remove(el)

for name, size, bold in [('Normal',10,False),('Title',22,True),('Subtitle',10,False),
                          ('Heading 1',15,True),('Heading 2',11,True),('Heading 3',10,True),
                          ('Caption',8.5,False),('Header',8,False),('Footer',8,False)]:
    style_font(doc.styles[name],size,bold)
for name in ['Heading 1','Heading 2','Heading 3']:
    pf=doc.styles[name].paragraph_format
    pf.space_before=Pt(10)
    pf.space_after=Pt(5)
    pf.keep_with_next=True
doc.styles['Title'].paragraph_format.space_after=Pt(5)
doc.styles['Caption'].paragraph_format.space_before=Pt(3)
doc.styles['Caption'].paragraph_format.space_after=Pt(7)

def rich(p,text,bold=False,size=None,color=None):
    # Indici tipografici editabili, senza font Symbol o immagini.
    for part in re.split(r'([A-Za-zΑ-ω′]+_\{[^}]+\}|\[[^\]]+\])',str(text)):
        m=re.fullmatch(r'([A-Za-zΑ-ω′]+)_\{([^}]+)\}',part)
        parts=[(m[1],False),(m[2],True)] if m else [(part,False)]
        for val,issub in parts:
            r=p.add_run(val);r.bold=bold
            if issub:r.font.subscript=True
            if size:r.font.size=Pt(size)
            if color:r.font.color.rgb=RGBColor.from_string(color)
            elif val.startswith('[') and val.endswith(']') and val[1:-1] not in ['m','°','kN','kPa','kN/m³']:
                r.font.color.rgb=RGBColor.from_string('275F7C')
    return p

def para(text='',style=None,bold=False,size=None,keep=False):
    p=doc.add_paragraph(style=style)
    rich(p,text,bold,size)
    p.paragraph_format.keep_with_next=keep
    return p

def heading(text,level=1,newpage=False):
    p=para(text,'Heading '+str(level))
    if newpage:p.paragraph_format.page_break_before=True
    return p

def note(text):
    p=para(text,size=8.4)
    p.paragraph_format.space_after=Pt(6)
    return p

def cell_text(cell,text,header=False,size=9):
    cell.text=''
    p=cell.paragraphs[0]
    p.paragraph_format.space_after=Pt(0)
    p.paragraph_format.space_before=Pt(0)
    p.paragraph_format.line_spacing=1.03
    p.paragraph_format.keep_with_next=False
    p.alignment=WD_ALIGN_PARAGRAPH.CENTER
    rich(p,text,header,size)
    cell.vertical_alignment=WD_CELL_VERTICAL_ALIGNMENT.CENTER

def table(headers,rows,widths,size=9,left=(0,),caption=None):
    if caption:para(caption,'Caption',keep=True)
    t=doc.add_table(rows=1, cols=len(headers))
    t.alignment=WD_TABLE_ALIGNMENT.CENTER
    t.autofit=False
    pr=t._tbl.tblPr
    tw=pr.find(qn('w:tblW'));tw.set(qn('w:w'),str(round(sum(widths)*567)));tw.set(qn('w:type'),'dxa')
    borders=OxmlElement('w:tblBorders')
    for side in ['top','left','bottom','right','insideH','insideV']:
        b=OxmlElement('w:'+side)
        for k,v in [('val','single'),('sz','4'),('color','D9D9D9')]:b.set(qn('w:'+k),v)
        borders.append(b)
    pr.append(borders)
    margins=OxmlElement('w:tblCellMar')
    for side in ['top','left','bottom','right']:
        e=OxmlElement('w:'+side);e.set(qn('w:w'),'65' if side in ['top','bottom'] else '85');e.set(qn('w:type'),'dxa');margins.append(e)
    pr.append(margins)
    for j,w in enumerate(widths):
        t.columns[j].width=Cm(w)
        t.rows[0].cells[j].width=Cm(w)
        cell_text(t.rows[0].cells[j],headers[j],True,size)
        sh=OxmlElement('w:shd');sh.set(qn('w:fill'),'E8EFF4');t.rows[0].cells[j]._tc.get_or_add_tcPr().append(sh)
    rep=OxmlElement('w:tblHeader');t.rows[0]._tr.get_or_add_trPr().append(rep)
    for vals in rows:
        row=t.add_row()
        for j,(val,w) in enumerate(zip(vals,widths)):
            row.cells[j].width=Cm(w)
            cell_text(row.cells[j],val,False,size)
            if j in left:row.cells[j].paragraphs[0].alignment=WD_ALIGN_PARAGRAPH.LEFT
    for row in t.rows:
        cant=OxmlElement('w:cantSplit');row._tr.get_or_add_trPr().append(cant)
    p=doc.add_paragraph();p.paragraph_format.space_after=Pt(1);p.paragraph_format.space_before=Pt(0)
    p.paragraph_format.line_spacing=Pt(2)
    p.add_run().font.size=Pt(2)
    return t

# Equazioni native Office Math; non vengono rasterizzate.
def mr(text,plain=False):
    r=OxmlElement('m:r')
    if plain:
        prop=OxmlElement('m:rPr');sty=OxmlElement('m:sty');sty.set(qn('m:val'),'p');prop.append(sty);r.append(prop)
    wr=OxmlElement('w:rPr');rf=OxmlElement('w:rFonts');rf.set(qn('w:ascii'),'Cambria Math');rf.set(qn('w:hAnsi'),'Cambria Math');wr.append(rf)
    sz=OxmlElement('w:sz');sz.set(qn('w:val'),'22');wr.append(sz);r.append(wr)
    tt=OxmlElement('m:t');tt.text=text;r.append(tt);return r

def nodes(value):
    if isinstance(value,str):return [mr(value)]
    if isinstance(value,list):return value
    return [value]

def box(tag,content):
    e=OxmlElement('m:'+tag)
    for n in nodes(content):e.append(deepcopy(n))
    return e

def sub(base,idx):
    e=OxmlElement('m:sSub');e.append(box('e',base));e.append(box('sub',mr(idx,True)));return e

def sup(base,power):
    e=OxmlElement('m:sSup');e.append(box('e',base));e.append(box('sup',power));return e

def frac(a,b):
    e=OxmlElement('m:f');e.append(box('num',a));e.append(box('den',b));return e

def integral(content):
    e=OxmlElement('m:nary');p=OxmlElement('m:naryPr')
    ch=OxmlElement('m:chr');ch.set(qn('m:val'),'∫');p.append(ch)
    loc=OxmlElement('m:limLoc');loc.set(qn('m:val'),'subSup');p.append(loc);e.append(p)
    e.append(box('sub','0'));e.append(box('sup','L'));e.append(box('e',content));return e

def delim(contents,left='[',right=']'):
    e=OxmlElement('m:d');p=OxmlElement('m:dPr')
    for tag,val in [('begChr',left),('endChr',right)]:
        c=OxmlElement('m:'+tag);c.set(qn('m:val'),val);p.append(c)
    e.append(p);e.append(box('e',contents));return e

def eq(content):
    p=doc.add_paragraph();p.alignment=WD_ALIGN_PARAGRAPH.CENTER
    p.paragraph_format.space_before=Pt(3);p.paragraph_format.space_after=Pt(6)
    om=OxmlElement('m:oMathPara');mp=OxmlElement('m:oMathParaPr');jc=OxmlElement('m:jc');jc.set(qn('m:val'),'center');mp.append(jc);om.append(mp)
    m=OxmlElement('m:oMath')
    for n in nodes(content):m.append(deepcopy(n))
    om.append(m);p._p.append(om);return p

def pic(path,width,caption):
    p=doc.add_paragraph();p.alignment=WD_ALIGN_PARAGRAPH.CENTER;p.paragraph_format.space_after=Pt(0)
    p.add_run().add_picture(str(path),width=Cm(width))
    p.paragraph_format.keep_with_next=True
    cp=para(caption,'Caption');cp.alignment=WD_ALIGN_PARAGRAPH.CENTER

def field(p,name):
    fld=OxmlElement('w:fldSimple');fld.set(qn('w:instr'),name)
    r=OxmlElement('w:r');t=OxmlElement('w:t');t.text='1';r.append(t);fld.append(r);p._p.append(fld)

hp=section.header.paragraphs[0];hp.style=doc.styles['Header']
rich(hp,'ANTHEA  /  FONDAZIONI DI PONTI',size=8)
fp=section.footer.paragraphs[0];fp.style=doc.styles['Footer'];fp.alignment=WD_ALIGN_PARAGRAPH.RIGHT
rich(fp,'Capacità portante verticale  |  ',size=8);field(fp,'PAGE');rich(fp,' / ',size=8);field(fp,'NUMPAGES')
upd=OxmlElement('w:updateFields');upd.set(qn('w:val'),'true');doc.settings.element.append(upd)

# Schema simbolico del profilo, privo di risultati di progetto inventati.
def font(sz,bold=False):
    return ImageFont.truetype(r'C:\Windows\Fonts\arialbd.ttf' if bold else r'C:\Windows\Fonts\arial.ttf',sz)

im=Image.new('RGB',(1800,540),'white');dr=ImageDraw.Draw(im)
x0,x1=360,1320;y0=52;bh=135
colors=['#F2E3BE','#DCE5DD','#E1E7EE']
for i,(col,label) in enumerate(zip(colors,['Strato A','Strato B','Strato C'])):
    y=y0+i*bh;dr.rectangle((x0,y,x1,y+bh),fill=col,outline='#B9BFC4',width=2)
    dr.text((420,y+40),f'{label}  -  [descrizione e parametri]',font=font(27),fill='#333333')
    dr.text((1380,y-14),['z = 0','z = [z1]','z = [z2]'][i],font=font(26),fill='#333333')
dr.text((1380,y0+3*bh-14),'z = [z3]',font=font(26),fill='#333333')
dr.rectangle((1080,y0,1150,y0+3*bh-25),fill='#7B8D9A',outline='#34495A',width=3)
dr.text((1048,13),'Palo',font=font(25,True),fill='#111111')
dr.line((250,y0,250,y0+3*bh-25),fill='#333333',width=3)
dr.polygon([(250,y0),(240,y0+18),(260,y0+18)],fill='#333333')
dr.polygon([(250,y0+3*bh-25),(240,y0+3*bh-43),(260,y0+3*bh-43)],fill='#333333')
dr.text((50,240),'L = [XX] m',font=font(27),fill='#111111')
for x in range(x0,x1,35):dr.line((x,300,min(x+20,x1),300),fill='#287398',width=3)
dr.text((1380,283),'Falda [zf]',font=font(26),fill='#287398')
dr.text((360,495),'Quote e spessori da compilare per la verticale [ID]',font=font(26),fill='#555555')
im.save(QA/'profilo-simbolico.png')

im=Image.new('RGB',(1800,640),'white');dr=ImageDraw.Draw(im)
dr.rectangle((150,65,1710,535),outline='#74828B',width=2)
for x in range(410,1710,260):dr.line((x,65,x,535),fill='#E0E5E8',width=2)
for y in range(159,535,94):dr.line((150,y,1710,y),fill='#E0E5E8',width=2)
dr.text((150,12),'Profondità z [m]',font=font(25),fill='#222222')
dr.text((1290,565),'Forza assiale [kN]',font=font(25),fill='#222222')
dr.text((460,225),'Curve di resistenza e azione lungo il palo',font=font(34,True),fill='#435764')
dr.text((540,285),'[inserire il grafico dai risultati di calcolo]',font=font(28),fill='#6D7A83')
dr.text((150,600),'Drenato e non drenato  |  Compressione e trazione  |  Quota finale L',font=font(24),fill='#435764')
im.save(QA/'spazio-curve.png')
with ZipFile(SOURCE) as z:(QA/'abaco-nq.jpeg').write_bytes(z.read('word/media/image12.jpeg'))

# Pagina 1.
para('Verifica della capacità portante\nverticale del palo','Title')
para('Fondazioni di ponti  |  Modello di relazione','Subtitle')
note('Opera [denominazione]  ·  Fondazione [ID]  ·  Palo [ID]  ·  Revisione [XX]  ·  Data [gg/mm/aaaa]')
heading('1 Oggetto e dati generali')
para('La presente relazione riguarda la verifica geotecnica agli stati limite ultimi del palo soggetto a carichi assiali di compressione e trazione. Sono esposte le ipotesi di calcolo, le caratteristiche del terreno e il confronto tra azioni e resistenze di progetto nelle condizioni drenate e non drenate pertinenti al modello geotecnico.')
para('La verifica è condotta con l’Approccio 2, combinazione A1 + M1 + R3. I coefficienti sulle azioni sono quelli dei ponti per la combinazione fondamentale; le resistenze geotecniche sono trattate secondo il § 6.4.3.1.1 delle NTC 2018.')
table(['Grandezza','Simbolo','Valore','Unità'],[
    ['Tipologia del palo','-','Trivellato','-'],['Diametro','D','1,20','m'],['Lunghezza','L','30,00','m'],
    ['Quota della testa rispetto al riferimento','z₀','[XX]','m'],['Peso dell’unità di volume del palo','γ_{p}','[XX]','kN/m³'],
    ['Numero di verticali indagate','n','[XX]','-'],['Identificativo della combinazione','-','[ID]','-']],
    [8.5,2,4.5,2],caption='Tabella 1  Dati geometrici e di inquadramento')
note('I campi tra parentesi quadre sono da compilare. Diametro, lunghezza e tipologia riprendono i valori esemplificativi della base di lavoro.')
heading('Convenzioni di lettura',2)
para('La profondità z è positiva verso il basso a partire dalla testa del palo. Le azioni di compressione e trazione sono riportate come moduli positivi. Le tensioni totali σ_{v} sono distinte dalle tensioni efficaci σ′_{v}; i pedici D e ND indicano rispettivamente condizioni drenate e non drenate.')
heading('Riferimenti normativi',2)
para('D.M. 17 gennaio 2018, Norme tecniche per le costruzioni: tabella 5.1.V per le azioni sui ponti; §§ 6.4.3.1 e 6.4.3.1.1 per le verifiche assiali dei pali; tabelle 6.4.II e 6.4.IV per i coefficienti di resistenza e di correlazione.')
note('Riferimento ufficiale: Gazzetta Ufficiale n. 42 del 20 febbraio 2018, Supplemento ordinario n. 8. La presente scheda riguarda la capacità portante assiale GEO; le altre verifiche della fondazione sono trattate negli elaborati dedicati.')

# Pagina 2.
heading('2 Teoria di calcolo',newpage=True)
para('La resistenza calcolata a compressione è data dalla somma dei contributi di punta e laterale. In trazione si considera il solo contributo laterale.')
eq([sub('R','c,cal'),mr(' = '),sub('R','b,cal'),mr(' + '),sub('R','s,cal'),mr(' = '),frac([mr('π'),sup('D','2')],'4'),mr(' '),sub('q','b'),mr(' + πD '),integral([mr('τ(z) dz')])])
eq([sub('R','t,cal'),mr(' = '),sub('R','s,cal')])
note('D e L: diametro e lunghezza [m]; q_{b}: resistenza unitaria di punta [kPa]; τ: resistenza tangenziale unitaria lungo il fusto [kPa]; R_{b,cal} e R_{s,cal}: contributi resistenti [kN].')
heading('Condizioni drenate',2)
eq([sub('q','b,D'),mr(' = '),sub('N','q'),mr(' '),sub('σ′','v'),mr('(L)     ;     '),sub('τ','D'),mr('(z) = k μ '),sub('σ′','v'),mr('(z)')])
para('La resistenza laterale drenata è valutata con il solo contributo attritivo, senza il termine di coesione efficace c′. Il fattore N_{q} è ricavato dall’abaco adottato in funzione di φ′ e del rapporto L/D; k e μ descrivono l’interazione palo terreno.')
table(['Tipologia del palo','k sciolto','k denso','μ'],[
    ['Battuto, profilato di acciaio','0,7','1,0','tan 20° = 0,36'],['Battuto, tubo di acciaio chiuso','1,0','2,0','tan 20° = 0,36'],
    ['Battuto, calcestruzzo prefabbricato','1,0','2,0','tan(3φ′/4)'],['Battuto, calcestruzzo gettato in opera','1,0','3,0','tan φ′'],
    ['Trivellato','0,5','0,4','tan φ′'],['Trivellato a elica continua','0,7','0,9','tan φ′']],
    [8.4,2.1,2.1,4.4],size=8.5,caption='Tabella 2  Coefficienti k e μ del modello di riferimento')
heading('Condizioni non drenate',2)
eq([sub('q','b,ND'),mr(' = '),sub('σ','v'),mr('(L) + '),sub('N','c'),mr(' '),sub('c','u'),mr('(L)     ;     '),sub('τ','ND'),mr('(z) = α '),sub('c','u'),mr('(z)')])
note('σ_{v}(L) è la tensione verticale totale alla punta. La resistenza di punta è espressa in forma lorda. N_{c} = [XX]; c_{u} è la resistenza al taglio non drenata. I tratti ai quali applicare ciascuna condizione sono identificati nel modello geotecnico.')
table(['Tipologia','Intervallo di c_{u} [kPa]','α'],[
    ['Battuto','c_{u} ≤ 25','1,00'],['Battuto','25 < c_{u} < 70','1 − 0,011 (c_{u} − 25)'],['Battuto','c_{u} ≥ 70','0,50'],
    ['Trivellato','c_{u} ≤ 25','0,70'],['Trivellato','25 < c_{u} < 70','0,70 − 0,008 (c_{u} − 25)'],['Trivellato','c_{u} ≥ 70','0,35']],
    [4,5,8],size=8.5,caption='Tabella 3  Coefficiente di adesione α del modello di riferimento')
note('Fonte bibliografica delle correlazioni k, μ e α: [riferimento da indicare].')

# Pagina 3.
heading('Resistenze caratteristiche e di progetto',newpage=True)
para('Per le verticali di indagine rappresentative si determinano le resistenze calcolate. I fattori ξ₃ e ξ₄ riducono, rispettivamente, il valore medio e il valore minimo; il confronto individua il ramo determinante.')
eq([sub('R','c,k'),mr(' = min ',True),delim([frac(sub('R','c,cal,medio'),sub('ξ','3')),mr(' ; '),frac(sub('R','c,cal,min'),sub('ξ','4'))])])
eq([sub('R','t,k'),mr(' = min ',True),delim([frac(sub('R','t,cal,medio'),sub('ξ','3')),mr(' ; '),frac(sub('R','t,cal,min'),sub('ξ','4'))])])
table(['n','1','2','3','4','5','7','≥ 10'],[
    ['ξ₃','1,70','1,65','1,60','1,55','1,50','1,45','1,40'],
    ['ξ₄','1,70','1,55','1,48','1,42','1,34','1,28','1,21']],
    [3,2,2,2,2,2,2,2],size=9,caption='Tabella 4  Fattori di correlazione secondo NTC 2018 tabella 6.4.IV')
para('Il numero di verticali indagate è n = [XX]. I fattori adottati sono ξ₃ = [XX] e ξ₄ = [XX].')
table(['Resistenza','Simbolo','Pali infissi','Pali trivellati','Elica continua'],[
    ['Base','γ_{b}','1,15','1,35','1,30'],['Laterale in compressione','γ_{s}','1,15','1,15','1,15'],
    ['Totale da prove di carico','γ','1,15','1,30','1,25'],['Laterale in trazione','γ_{st}','1,25','1,25','1,25']],
    [6,2,3,3,3],size=8.8,caption='Tabella 5  Coefficienti di resistenza R3 secondo NTC 2018 tabella 6.4.II')
note('Il coefficiente sulla resistenza totale riguarda resistenze caratteristiche dedotte da prove di carico di progetto. Nel calcolo analitico della presente scheda si distinguono i contributi di base e laterale.')
para('Per ciascun ramo r, medio/ξ₃ oppure minimo/ξ₄, i contributi caratteristici di punta e laterale sono ridotti con i rispettivi coefficienti. La resistenza di progetto è il minore dei due valori così ottenuti.')
eq([sub('R','c,d'),mr(' = '),sub('η','c'),mr(' '),sub(mr('min',True),'r'),delim([frac(sub('R','b,k,r'),sub('γ','b')),mr(' + '),frac(sub('R','s,k,r'),sub('γ','s'))])])
eq([sub('R','t,d'),mr(' = '),sub('η','t'),mr(' '),sub(mr('min',True),'r'),delim([frac(sub('R','s,k,r'),sub('γ','st'))])])
note('L’individuazione dei contributi e del ramo determinante deve essere esplicitata nei risultati, conservando la distinzione tra resistenze calcolate, caratteristiche e di progetto.')
heading('Efficienza del gruppo',2)
para('Gli effetti di gruppo sono rappresentati dai fattori η_{c} e η_{t}, applicati alle resistenze rispettivamente in compressione e trazione.')
table(['Voce','Valore adottato'],[
    ['Criterio di valutazione','[metodo oppure assegnazione diretta]'],['Efficienza in compressione η_{c}','[XX]'],
    ['Efficienza in trazione η_{t}','[XX]']], [10,7],size=9)
note('Se si assume efficienza unitaria, riportare comunque η = 1,00 e la motivazione dell’assunzione.')

# Pagina 4.
heading('3 Modello geotecnico',newpage=True)
para('Le stratigrafie e i parametri di seguito riportati definiscono il modello di sottosuolo utilizzato nella verifica. La posizione della falda e le quote degli strati sono riferite al medesimo piano della testa del palo.')
heading('Verticale di indagine ID',2)
para('Identificativo [ID]  ·  Indagine di riferimento [documento e data]  ·  Ubicazione [XX]')
pic(QA/'profilo-simbolico.png',17,'Figura 1  Schema del palo e della stratigrafia con campi da compilare')
table(['Strato','Terreno e stato','z iniziale\n[m]','z finale\n[m]','Spessore\n[m]'],[
    ['A','[descrizione]','[XX]','[XX]','[XX]'],['B','[descrizione]','[XX]','[XX]','[XX]'],['C','[descrizione]','[XX]','[XX]','[XX]']],
    [1.4,6.6,3,3,3],size=9,caption='Tabella 6  Geometria della stratigrafia')
table(['Strato','γ\n[kN/m³]','γ_{sat}\n[kN/m³]','φ′\n[°]','c′\n[kPa]','c_{u}\n[kPa]'],[
    ['A','[XX]','[XX]','[XX]','[XX]','[XX]'],['B','[XX]','[XX]','[XX]','[XX]','[XX]'],['C','[XX]','[XX]','[XX]','[XX]','[XX]']],
    [1.4,3.2,3.2,3.2,3,3],size=9,caption='Tabella 7  Parametri geotecnici')
note('γ: peso dell’unità di volume naturale; γ_{sat}: saturo. La coesione efficace c′ è riportata come parametro di caratterizzazione e non concorre alla resistenza laterale drenata del presente modello.')
table(['Strato','k','μ','α','Laterale attiva','Condizione applicata'],[
    ['A','[XX]','[XX]','[XX]','[sì/no]','[D / ND]'],['B','[XX]','[XX]','[XX]','[sì/no]','[D / ND]'],['C','[XX]','[XX]','[XX]','[sì/no]','[D / ND]']],
    [1.4,2.1,2.1,2.1,3.6,5.7],size=9,caption='Tabella 8  Coefficienti e condizioni di calcolo lungo il fusto')
para('Falda: [presente / assente]. Profondità z_{f} = [XX] m. Eventuali tratti esclusi dalla resistenza laterale: [indicazione e motivazione].')
note('Il blocco della verticale viene ripetuto per ogni stratigrafia impiegata. Le indagini e l’eventuale estrapolazione del modello devono coprire le profondità rilevanti per la verifica.')

# Pagina 5.
heading('4 Calcolo della capacità portante',newpage=True)
para('Le resistenze sono valutate alla lunghezza di progetto L = 30,00 m. Per ciascuna verticale sono identificati lo strato di punta, i fattori di capacità portante e i contributi di base e laterale.')
table(['Verticale','Strato di punta','φ′\n[°]','L/D','N_{q}','N_{c}'],[
    ['[ID 1]','[XX]','[XX]','25,00','[XX]','[XX]'],['[ID 2]','[XX]','[XX]','25,00','[XX]','[XX]']],
    [2.8,4.2,2.5,2.5,2.5,2.5],size=9,caption='Tabella 9  Parametri alla punta del palo')
heading('Fattore di capacità portante Nq',2)
para('Il fattore N_{q} è determinato mediante l’abaco di Berezantzev riportato nella base di lavoro. Il punto corrispondente a φ′ e L/D e il valore adottato sono associati alla verticale di riferimento.')
pic(QA/'abaco-nq.jpeg',6.5,'Figura 2  Abaco di riferimento per Nq tratto dalla base di lavoro')
note('Verticale [ID]  ·  φ′ = [XX]°  ·  L/D = 25,00  ·  N_{q} = [XX]  ·  Criterio di interpolazione [XX]. Il punto di lettura sarà evidenziato sul grafico compilato.')
heading('Contributi resistenti alla quota finale',2)
table(['Verticale','Condizione','R_{b,cal}\n[kN]','R_{s,cal}\n[kN]','R_{c,cal}\n[kN]','R_{t,cal}\n[kN]'],[
    ['[ID 1]','D','[XX]','[XX]','[XX]','[XX]'],['[ID 1]','ND','[XX]','[XX]','[XX]','[XX]'],
    ['[ID 2]','D','[XX]','[XX]','[XX]','[XX]'],['[ID 2]','ND','[XX]','[XX]','[XX]','[XX]']],
    [2.4,2.2,3.1,3.1,3.1,3.1],size=9,caption='Tabella 10  Resistenze calcolate per verticale')
note('Il dettaglio lungo la profondità è riportato nelle appendici A e B. Il riepilogo delle resistenze di progetto e degli esiti è raccolto nella tabella 13.')

# Pagina 6.
heading('5 Azioni e verifica',newpage=True)
para('Le azioni in testa, già di progetto nella combinazione [ID], escludono il peso proprio del palo. Quest’ultimo è aggiunto una sola volta mediante i coefficienti A1 per i ponti.')
eq([sub('G','p,k'),mr(' = '),sub('γ','p'),mr(' '),frac([mr('π'),sup('D','2')],'4'),mr(' L')])
table(['Voce','Simbolo','Valore','Unità'],[
    ['Peso caratteristico del palo','G_{p,k}','[XX]','kN'],['Sottospinta considerata','U_{k}','[XX / 0]','kN'],
    ['Peso efficace nelle azioni','G_{k,eff}','[XX]','kN']], [8,3,4,2],size=9,caption='Tabella 11  Peso e contributi idrostatici')
note('G_{k,eff} = G_{p,k} − U_{k}. L’eventuale sottospinta è dichiarata con il relativo criterio; quando non è considerata, U_{k} = 0.')
table(['Effetto del peso proprio','Coefficiente A1','Riferimento'],[
    ['Sfavorevole in compressione','1,35','NTC 2018, tabella 5.1.V'],['Favorevole in trazione','1,00','NTC 2018, tabella 5.1.V']],
    [8,3,6],size=9,caption='Tabella 12  Coefficienti sulle azioni permanenti per ponti')
eq([sub('E','d,C'),mr(' = '),sub('N','Ed,C,testa'),mr(' + 1,35 '),sub('G','k,eff'),mr(' = [XX] kN',True)])
eq([sub('E','d,T'),mr(' = max ',True),delim([mr('0 ; '),sub('N','Ed,T,testa'),mr(' − 1,00 '),sub('G','k,eff')]),mr(' = [XX] kN',True)])
note('Per la trazione si assume il modulo residuo dopo il contributo favorevole del peso. Eventuali ulteriori azioni assiali, incluso l’attrito negativo ove pertinente, sono dichiarate e incluse nell’azione di progetto.')
heading('Riepilogo delle verifiche',2)
eq([sub('E','d'),mr(' ≤ '),sub('R','d'),mr('     ;     I = '),frac(sub('E','d'),sub('R','d')),mr(' ≤ 1')])
table(['Condizione','Azione','E_{d}\n[kN]','R_{d}\n[kN]','I = E_{d}/R_{d}','Esito'],[
    ['Drenata','Compressione','[XX]','[XX]','[XX]','[da compilare]'],['Drenata','Trazione','[XX]','[XX]','[XX]','[da compilare]'],
    ['Non drenata','Compressione','[XX]','[XX]','[XX]','[da compilare]'],['Non drenata','Trazione','[XX]','[XX]','[XX]','[da compilare]']],
    [3.0,3.2,2.2,2.2,2.4,4.0],size=8.6,caption='Tabella 13  Riepilogo alla lunghezza di progetto')
note('Condizione determinante [XX]  ·  Utilizzo massimo [XX]  ·  Esito complessivo [XX]. Le condizioni non pertinenti al modello geotecnico sono indicate come non applicabili.')
pic(QA/'spazio-curve.png',13.5,'Figura 3  Curve di capacità portante e azione lungo il palo')

# Appendici orizzontali con tabelle coordinate per quota e non trentacolonne.
sec=doc.add_section(WD_SECTION_START.NEW_PAGE)
sec.orientation=WD_ORIENT.LANDSCAPE;sec.page_width=Cm(29.7);sec.page_height=Cm(21)
sec.top_margin=Cm(1.7);sec.bottom_margin=Cm(1.5);sec.left_margin=Cm(1.8);sec.right_margin=Cm(1.8)
heading('Appendice A Parametri e tensioni lungo il palo')
note('Verticale [ID]  ·  Profondità dalla testa del palo  ·  Passo nominale 0,50 m, con inserimento delle discontinuità stratigrafiche, della falda e della quota finale.')
zs=['0,50','1,00','1,50','2,00','…','L = 30,00']
table(['z\n[m]','Strato','γ\n[kN/m³]','γ_{sat}\n[kN/m³]','γ′\n[kN/m³]','φ′\n[°]','c′\n[kPa]','c_{u}\n[kPa]','k','μ','α'],
    [[z]+['[XX]']*10 for z in zs],[2.5,2.5,2.7,2.7,2.7,2.1,2.3,2.3,2.1,2.1,2.1],size=9,left=(),caption='Tabella A1  Parametri del terreno e coefficienti alla profondità z')
table(['z\n[m]','σ_{v}\n[kPa]','σ′_{v}\n[kPa]','N_{q}','N_{c}','Laterale attiva','Condizione locale'],
    [[z]+['[XX]']*4+['[sì/no]','[D / ND]'] for z in zs],[2.5,3.8,3.8,3,3,4.5,5.5],size=9,left=(),caption='Tabella A2  Tensioni e fattori di capacità portante')
note('γ′ indica il peso dell’unità di volume immerso. La coesione efficace c′ è riportata per tracciabilità dei dati geotecnici e non compare nella formula della resistenza laterale drenata. Le tensioni alla punta e i parametri lungo il fusto sono mantenuti distinti.')

heading('Appendice B Contributi e resistenze lungo il palo',newpage=True)
note('Verticale [ID] per i contributi calcolati  ·  Inviluppo delle verticali per le resistenze caratteristiche e di progetto  ·  Valori in kN salvo diversa indicazione.')
table(['z\n[m]','τ_{D}\n[kPa]','R_{s,cal,D}','q_{b,D}\n[kPa]','R_{b,cal,D}','R_{c,cal,D}','τ_{ND}\n[kPa]','R_{s,cal,ND}','q_{b,ND}\n[kPa]','R_{b,cal,ND}','R_{c,cal,ND}'],
    [[z]+['[XX]']*10 for z in zs],[2.5,2.1,2.5333,2.1,2.5333,2.5333,2.1,2.5333,2.1,2.5333,2.5333],size=8.6,left=(),caption='Tabella B1  Resistenze unitarie e contributi calcolati')
note('R_{t,cal,D} = R_{s,cal,D} e R_{t,cal,ND} = R_{s,cal,ND}. I contributi laterali sono cumulati fino a z; il contributo di base è valutato alla punta posta alla medesima profondità.')
table(['z\n[m]','R_{c,k,D}','R_{t,k,D}','R_{c,k,ND}','R_{t,k,ND}','R_{c,d,D}','R_{t,d,D}','R_{c,d,ND}','R_{t,d,ND}'],
    [[z]+['[XX]']*8 for z in zs],[2.5]+[2.95]*8,size=8.8,left=(),caption='Tabella B2  Resistenze caratteristiche e di progetto')
note('Ramo determinante alla quota finale: compressione D [medio/minimo]; trazione D [medio/minimo]; compressione ND [medio/minimo]; trazione ND [medio/minimo]. I valori riportati sono arrotondati ai soli fini della presentazione.')

for current_section in doc.sections:
    for number_type in current_section._sectPr.findall(qn('w:pgNumType')):
        if qn('w:start') in number_type.attrib:
            del number_type.attrib[qn('w:start')]
doc.core_properties.title='Verifica della capacità portante verticale del palo'
doc.core_properties.subject='Proposta di modello per fondazioni di ponti'
doc.core_properties.author='ANTHEA'
doc.core_properties.keywords='palo, capacità portante, ponti, modello'
doc.save(FINAL)

# Conserva byte per byte le parti opache che non appartengono alla revisione.
baseline=json.loads((QA/'inventario-riferimento.json').read_text(encoding='utf-8'))
with ZipFile(SOURCE) as original, ZipFile(FINAL) as edited:
    entries={n:edited.read(n) for n in edited.namelist()}
    for n,meta in baseline.items():
        if meta['role']=='preserve' and n in entries:entries[n]=original.read(n)
with ZipFile(FINAL,'w',ZIP_DEFLATED) as z:
    for name,data in entries.items():z.writestr(name,data)

with ZipFile(FINAL) as z:
    from lxml import etree
    rt=etree.fromstring(z.read('word/document.xml'))
    ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main','m':'http://schemas.openxmlformats.org/officeDocument/2006/math'}
    audit={'native_equations':len(rt.xpath('//m:oMath',namespaces=ns)),
           'native_tables':len(rt.xpath('//w:tbl',namespaces=ns)),
           'sections':len(rt.xpath('//w:sectPr',namespaces=ns)),
           'reference_hash_unchanged':hashlib.sha256(SOURCE.read_bytes()).hexdigest()==HASH,
           'cohesion_excluded':True,'action_factors':[1.35,1.0],
           'preserved_parts':{n:hashlib.sha256(z.read(n)).hexdigest()==m['sha256'] for n,m in baseline.items() if m['role']=='preserve' and n in z.namelist()}}
    (QA/'audit-strutturale.json').write_text(json.dumps(audit,indent=2),encoding='utf-8')
sys.stdout.reconfigure(encoding='utf-8')
print(FINAL)
print(json.dumps(audit,ensure_ascii=False))
