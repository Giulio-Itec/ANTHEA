from pathlib import Path
from copy import deepcopy
from zipfile import ZipFile,ZIP_DEFLATED
import json,re,sys,hashlib
from docx import Document
from docx.shared import Pt,Mm,RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.text.paragraph import Paragraph
from docx.table import Table
from equations import omml,tex,GENERAL,M
R=Path(__file__).resolve().parents[3]
A=R/'supporto/artefatti/validazione_illustrata_2026_09_26'
BASE=R/'supporto/documentazione/Validazione_CA_ANTHEA/ANTHEA_Validazione_Software_CA_e_Ponti_Rev02.docx'
OUT=BASE.with_name('ANTHEA_Validazione_Software_CA_e_Ponti_Rev03.docx')
doc=Document(BASE)
original_figures=[p._p for p in doc.paragraphs if p._p.xpath('.//w:drawing')]
cases=json.loads((A/'capture-input.json').read_text(encoding='utf8'))
case_map={c['id']:c for c in cases}
root=doc._element.body
eqsources=[];bookmark_id=20000;seq={'Equazione':0,'Figura':0,'Tabella':0};target_ids=set();refs=[]
def bookmark(p,name):
    global bookmark_id
    bookmark_id+=1
    a=OxmlElement('w:bookmarkStart');a.set(qn('w:id'),str(bookmark_id));a.set(qn('w:name'),name)
    b=OxmlElement('w:bookmarkEnd');b.set(qn('w:id'),str(bookmark_id))
    p._p.insert(1 if p._p.pPr is not None else 0,a);p._p.append(b);target_ids.add(name)
def field(p,instr,text):
    r=p.add_run();a=OxmlElement('w:fldChar');a.set(qn('w:fldCharType'),'begin');r._r.append(a)
    r=p.add_run();t=OxmlElement('w:instrText');t.set(qn('xml:space'),'preserve');t.text=instr;r._r.append(t)
    r=p.add_run();a=OxmlElement('w:fldChar');a.set(qn('w:fldCharType'),'separate');r._r.append(a)
    p.add_run(str(text))
    r=p.add_run();a=OxmlElement('w:fldChar');a.set(qn('w:fldCharType'),'end');r._r.append(a)
def ref(p,target,label):
    refs.append(target);field(p,' REF '+target+' \\h ',label)
def para_before(anchor,text='',style='Normal'):
    el=OxmlElement('w:p');anchor.addprevious(el);p=Paragraph(el,doc._body);p.style=style
    if text:p.add_run(text)
    return p
def para_after(anchor,text='',style='Normal'):
    el=OxmlElement('w:p');anchor.addnext(el);p=Paragraph(el,doc._body);p.style=style
    if text:p.add_run(text)
    return p
def caption(anchor,kind,desc,bm=None,before=True):
    p=para_before(anchor,style='Caption') if before else para_after(anchor,style='Caption')
    seq[kind]+=1;p.add_run(kind+' ');target=bm or f'{kind}_{seq[kind]}'
    # Bookmark only the number, not the entire caption.
    global bookmark_id
    bookmark_id+=1;a=OxmlElement('w:bookmarkStart');a.set(qn('w:id'),str(bookmark_id));a.set(qn('w:name'),target);p._p.append(a)
    field(p,' SEQ '+kind+' \\* ARABIC ',seq[kind]);b=OxmlElement('w:bookmarkEnd');b.set(qn('w:id'),str(bookmark_id));p._p.append(b);target_ids.add(target)
    p.add_run(' · '+desc);p.paragraph_format.keep_with_next=before;p.alignment=WD_ALIGN_PARAGRAPH.LEFT
    for r in p.runs:r.font.size=Pt(10)
    return p,target
def equation(anchor,latex,bm=None,source=None):
    p=para_before(anchor,style='Formula ANTHEA');seq['Equazione']+=1
    node=omml(latex);p._p.append(node)
    # Align equation number at the right margin using a right tab.
    p.add_run('\t(')
    global bookmark_id
    bookmark_id+=1;name=bm or f'eq_{seq["Equazione"]}'
    a=OxmlElement('w:bookmarkStart');a.set(qn('w:id'),str(bookmark_id));a.set(qn('w:name'),name);p._p.append(a)
    field(p,' SEQ Equazione \\* ARABIC ',seq['Equazione'])
    b=OxmlElement('w:bookmarkEnd');b.set(qn('w:id'),str(bookmark_id));p._p.append(b);p.add_run(')');target_ids.add(name)
    eqsources.append(dict(number=seq['Equazione'],bookmark=name,latex=latex,source=source))
    return p,name
def image_at(anchor,id):
    c=case_map[id];file=A/('screens-ca' if c['module']=='ca' else 'screens')/(id+'.png')
    if (A/'screens-refined'/(id+'.png')).exists():file=A/'screens-refined'/(id+'.png')
    if (A/'screens-anchors'/(id+'.png')).exists():file=A/'screens-anchors'/(id+'.png')
    if (A/'screens-bridge'/(id+'.png')).exists():file=A/'screens-bridge'/(id+'.png')
    if not file.exists():raise FileNotFoundError(file)
    p=para_before(anchor);p.paragraph_format.keep_with_next=True;p.add_run().add_picture(str(file),width=Mm(170))
    for pr in p._p.xpath('.//wp:docPr'):pr.set('descr','ANTHEA caso '+id+' · '+c['mode'])
    cap,target=caption(p._p,'Figura','ANTHEA · '+id,'fig_'+id.replace('-','_'),before=False)
    cap.paragraph_format.keep_with_next=bool(c['note'])
    if c['note']:
        note=para_after(cap._p,c['note']);note.style='Nota ANTHEA'
    return target
# Preserve template fonts and geometry, improve only requested content roles.
for name in ['Formula ANTHEA','Nota ANTHEA']:
    if name not in doc.styles:doc.styles.add_style(name,1)
s=doc.styles['Formula ANTHEA'];s.font.name='Cambria Math';s.font.size=Pt(12);s.paragraph_format.space_before=Pt(3);s.paragraph_format.space_after=Pt(5);s.paragraph_format.keep_together=True
from docx.enum.text import WD_TAB_ALIGNMENT
s.paragraph_format.tab_stops.add_tab_stop(Mm(170),WD_TAB_ALIGNMENT.RIGHT)
s=doc.styles['Nota ANTHEA'];s.base_style=doc.styles['Normal'];s.font.size=Pt(10);s.font.color.rgb=RGBColor.from_string('505050');s.paragraph_format.space_after=Pt(5)
# Remove cached TOC entries; retain the field instructions and let Word rebuild it.
# Word handles these automatically in the final update, so they are excluded from edits.
body_start=next(p._p for p in doc.paragraphs if p.style.name=='Heading 1')
body_children=list(root);start=body_children.index(body_start)
for el in body_children[start:]:
    if el.tag!=qn('w:p'):continue
    p=Paragraph(el,doc._body)
    if p.style.name.startswith('Heading'):continue
    if p.text.strip() and not el.xpath('.//w:instrText'):
        p.paragraph_format.space_after=Pt(4)
        for run in p.runs:
            if run.font.size and run.font.size.pt<11:run.font.size=Pt(11)
# Cover revision only.
for t in doc.tables[:5]:
    for row in t.rows:
        if any('INTEGRAZIONE PONTI' in c.text for c in row.cells):
            for c,value in zip(row.cells,['03','FORMULE RIMANDI E VISTE','26/09/2026','','','']):c.text=value
doc.core_properties.subject='Revisione 03 con formule native da LaTeX riferimenti incrociati e schermate dei casi'
# Headings, bibliography targets and example boundaries.
headings={};ca_heads={};active=False;chap='';case='';section_number=0;bridge_flow=False
for p in list(doc.paragraphs):
    if p._p is body_start:active=True
    if not active:continue
    if p.style.name.startswith('Heading'):
        if p.style.name=='Heading 1':section_number+=1
        if p.text=='Modelli e convenzioni comuni':bridge_flow=True
        if bridge_flow and p.style.name=='Heading 1':p.paragraph_format.page_break_before=False
        name='sec_'+str(len(headings)+1);bookmark(p,name);headings[p.text]=(p,name)
        m=re.match(r'^([A-Z0-9]+) (\d{2})\b',p.text)
        if m:
            id=m[1]+'-'+m[2];ca_heads[id]=p;bookmark(p,'caso_'+id.replace('-','_'))
            # Each complete example has its own clear start.
            p.paragraph_format.page_break_before=True
    m=re.match(r'^R([1-8])\.',p.text)
    if m:bookmark(p,'bib_ca_R'+m[1])
    m=re.match(r'^\[([1-7])\]',p.text)
    if m:bookmark(p,'bib_ponte_'+m[1])
# Intro notes and navigation.
intro=para_after(body_start,'Questa revisione aggiunge equazioni Word modificabili da sorgenti LaTeX, rimandi cliccabili e viste del software. Le tabelle mantengono gli esiti numerici della Revisione 02: gli arrotondamenti migliorano la lettura e non cambiano le tolleranze del confronto. Le schermate sono acquisizioni delle viste WPF del 26 settembre 2026; per le prove scalari o su fibre ideali la didascalia distingue la vista pertinente dalla replica esatta del caso.')
intro.paragraph_format.space_after=Pt(6)
# General typeset equations inserted in the common-data chapter, before the cases.
anchor=headings['Domini analisi e verifiche SLE'][0]._p
formula_group={}
for key,title,*formulas in GENERAL:
    ph=para_before(anchor,title,'Heading 2');bookmark(ph,'metodo_'+key)
    for j,formula in enumerate(formulas):
        _,name=equation(anchor,formula,'eq_'+key+('_'+str(j+1) if j else ''))
        if j==0:formula_group[key]=name
    if key=='ancoraggio':para_before(anchor,'Per gli esempi si assumono α1…α5 = 1. I minimi e i coefficienti delle sovrapposizioni sono sviluppati nei singoli casi.','Nota ANTHEA')
    if key=='fessure':para_before(anchor,'La distanza adottata e la scelta fra barre ravvicinate e distanziate sono sviluppate nel singolo caso; non si applica automaticamente la sola espressione ravvicinata.','Nota ANTHEA')
    if key=='taglio':para_before(anchor,'Forze in N con geometria in mm e resistenze in MPa; dividere per 1000 per ottenere kN.','Nota ANTHEA')
    if key=='torsione':para_before(anchor,'Momenti in Nmm con geometria in mm; dividere per 10⁶ per kNm. La somma biassiale sul calcestruzzo è la scelta conservativa dichiarata nel modulo.','Nota ANTHEA')
# Convert the numerical derivations without recomputing or changing any value.
# Split only at sentence boundaries and at semicolons outside parentheses.
def chunks(text):
    out=[];buf='';depth=0
    for i,ch in enumerate(text):
        if ch in '([':depth+=1
        if ch in ')]':depth=max(0,depth-1)
        buf+=ch
        split=(ch==';' and depth==0) or (ch=='.' and depth==0 and (i==len(text)-1 or text[i+1].isspace())) or ch=='\n'
        if split:out.append(buf.strip());buf=''
    if buf.strip():out.append(buf.strip())
    return out

LHS=r'[A-Za-zα-ωΑ-ΩεσΔΣ][A-Za-z0-9α-ωΑ-Ωεσ₀₁₂₃₄₅₆₇₈₉ᵢᵣ,._±]*(?:\([^=;]{0,25}\))?'
def equation_latex(expr):
    # Separate independent assignments before aligning equality chains.
    assignments=re.split(r'(?:,\s+|:\s+|\s+e\s+|,\s+con\s+)(?='+LHS+r'\s*=)',expr)
    lines=[]
    for assignment in assignments:
        operands=assignment.split('=')
        formatted=[]
        for operand in operands:
            repeated=[x.strip() for x in operand.strip().split(' + ')]
            if len(repeated)>2 and len(set(repeated))==1:
                operand=str(len(repeated))+'×('+repeated[0]+')'
            frac=re.fullmatch(r'\s*\[(.+)\]/\(([^()]+)\)\s*',operand)
            if frac:
                numerator=frac[1];depth=0;split=None
                for k,ch in enumerate(numerator):
                    if ch=='(':depth+=1
                    elif ch==')':depth-=1
                    elif ch in '−+-' and depth==0 and k>0 and numerator[k-1] not in 'eE':split=k;break
                if split is not None and len(numerator)>55:
                    num=r'\begin{gathered}'+tex(numerator[:split])+r'\\{}'+tex(numerator[split:])+r'\end{gathered}'
                else:num=tex(numerator)
                formatted.append(r'\frac{'+num+'}{'+tex(frac[2])+'}')
            else:formatted.append(tex(operand))
        if len(assignment)>90 and len(formatted)>=3:
            lines.append(formatted[0]+'&='+formatted[1])
            lines.extend('&='+x for x in formatted[2:])
        else:lines.append(formatted[0]+('&='+'='.join(formatted[1:]) if len(formatted)>1 else ''))
    if len(lines)>1:return r'\begin{align*}'+r'\\'.join(lines)+r'\end{align*}'
    return lines[0].replace('&=','=')

converted=0;failures=[]
original_paras=list(doc.paragraphs)
for p in original_paras:
    text=p.text
    if p.style.name.startswith(('Heading','TOC','Formula','Caption')) or p._p.xpath('.//w:instrText|.//w:hyperlink|.//w:sectPr|.//w:drawing'):continue
    if len(text)<3 or '=' not in text:continue
    if list(root).index(p._p)<list(root).index(body_start):continue
    if text.startswith(('Sezione R;','Sezione W;')):
        if 'Piano noto ' in text:
            prefix,expression=text.split('Piano noto ',1)
            para_before(p._p,prefix.strip()+' Piano di deformazione noto:')
            equation(p._p,equation_latex(expression.rstrip('.')),source=expression.rstrip('.'));converted+=1
            root.remove(p._p)
        continue
    if 'copriferro nominale input' in text or text.startswith(('NEd di compressione','As ortogonale totale')):continue
    if text.startswith('AD−B² =') or text.startswith('AD-B² ='):
        equation(p._p,tex(text.rstrip('.')),source=text.rstrip('.'));converted+=1;root.remove(p._p);continue
    if text.startswith(('Criterio','Tolleranz','Controlli interno','Per riprodurre','Eseguire','Comando','La nuova','Il confronto','Le schermate','Modello a linea','Dominio ','Per gli esempi si assumono')):continue
    if text.lstrip().startswith('dotnet '):continue
    if not any(op in text for op in ['×','∫','Σ','/','^',' = −',' = -','max','min',' + ']):continue
    parts=chunks(text);new=[];has=False
    for part in parts:
        # Locate a conventional equation left hand side, preserving any prose prefix.
        match=re.search(r'(?<![\w])([A-Za-zα-ωΑ-ΩεσΔΣ][A-Za-z0-9α-ωΑ-Ωεσ₀₁₂₃₄₅₆₇₈₉ᵢᵣ,._±]*\s*(?:\([^=;]{0,30}\))?\s*=)',part)
        if not match:
            match=re.search(r'(\|[σA-Za-z]+\|\s*=|AD[−-]B²\s*=|(?:min|max)[(\[][^=]+[)\]]\s*=|Ø[A-Za-z]*\s*=)',part)
        if not match:new.append(('text',part));continue
        prefix=part[:match.start()].strip();expr=part[match.start():].rstrip('; .')
        # Pure prose after a formula belongs back in normal text.
        suffix=''
        split=re.search(r'\b(?:quindi|dove|confrontare|confronto|rispetto|compatibile|richiesto|richiesta|senza|anche|e tensione|e risolvendo|e, dopo|Si applica|al termine|Il |La |Le |Gli |nel |nelle |per i |per le )',expr)
        if split and split.start()>expr.index('=')+2:expr,suffix=expr[:split.start()].rstrip(' ,;'),expr[split.start():]
        words=re.findall(r'[A-Za-zÀ-ÿ]{5,}',expr)
        if len(words)>5 or len(expr)>260:new.append(('text',part));continue
        try:
            latex=equation_latex(expr)
            omml(latex)
            if prefix:new.append(('text',prefix))
            new.append(('eq',(latex,expr)));has=True
            if suffix:new.append(('text',suffix))
        except Exception as ex:failures.append([expr,str(ex)]);new.append(('text',part))
    if not has:continue
    merged=[]
    for typ,value in new:
        if typ=='text' and merged and merged[-1][0]=='text':merged[-1]=('text',merged[-1][1]+' '+value)
        else:merged.append((typ,value))
    for typ,value in merged:
        if typ=='eq':equation(p._p,value[0],source=value[1]);converted+=1
        elif value:para_before(p._p,value)
    root.remove(p._p)
# Avoid orphaned chapter headings immediately before a forced example start.
for id in ['P3D-01','MC1-01']:ca_heads[id].paragraph_format.page_break_before=False
headings['Dati comuni e formule di equilibrio'][0].paragraph_format.page_break_before=False
headings['Domini analisi e verifiche SLE'][0].paragraph_format.page_break_before=True
for el in list(root)[list(root).index(body_start):]:
    if el.tag==qn('w:p') and not Paragraph(el,doc._body).text.strip() and el.xpath('.//w:br[@w:type="page"]'):
        root.remove(el)
# Add screenshots after each complete CA example, before next heading.
for id,hp in ca_heads.items():
    next_el=hp._p.getnext()
    while next_el is not None:
        if next_el.tag==qn('w:p') and Paragraph(next_el,doc._body).style.name.startswith('Heading'):break
        next_el=next_el.getnext()
    if next_el is None:raise ValueError(id)
    image_at(next_el,id)
# Bridge examples: one indexed atlas, every example has a named figure and data summary.
atlas_anchor=headings['Riproduzione dei test applicativi aggiornati'][0]._p
ph=para_before(atlas_anchor,'Viste degli esempi applicativi dei ponti','Heading 1');bookmark(ph,'atlante_ponti')
ph.paragraph_format.page_break_before=True
para_before(atlas_anchor,'Le figure seguenti mostrano gli stessi ingressi degli esempi EL, GET, SC, RIT, HOM, MK, NE e PL. Le prime due figure riproducono il benchmark comune in cinque fasi. I modelli ideali e i confronti OpenSees hanno una discretizzazione di prova distinta: il loro rimando alla vista di un modulo illustra la modalità operativa, non una replica del modello esterno.','Nota ANTHEA')
for c in sorted([c for c in cases if c['module']=='bridge'],key=lambda c:('0' if 'CINQUE' in c['id'] else '1')+c['id']):
    id=c['id'];p=para_before(atlas_anchor,id.replace('P-','',1)+' Vista del caso','Heading 2');bookmark(p,'caso_'+id.replace('-','_'))
    p.paragraph_format.page_break_before=id!='P-CINQUE-0'
    d=c['data'];summary=[]
    for f in d['fasi'][:5]:summary.append(f"{f['nome']}: {f['tipo']}, N = {f['N']:g} kN, M₀ = {f['Mx']:g} kNm, φ = {f['phi']:g}, εcs = {f['epsilon_cs']:g} µε")
    if len(d['fasi'])>5:summary=[f"{len(d['fasi'])} incrementi di ritiro da −1 µε; φ = 0; risultato all’ultima fase."]
    if c['curve']:
        q=d['curve_sezione'];summary.append(f"Curva {'M–κ' if q['typeIndex']==0 else 'N–ε'}; {q['punti']} punti; Δκ = {q['incremento_k']:g} 1/m; Δε = {q['incremento_e']:g} µε; quota yᵣ = {q['y']:.6f} mm.")
    para_before(atlas_anchor,' · '.join(summary))
    image_at(atlas_anchor,id)
# Number and format all data tables after the cover. Do not add headers to cover tables.
for index,el in enumerate(original_figures,1):
    following=el.getnext();oldcaption=Paragraph(following,doc._body) if following is not None and following.tag==qn('w:p') else None
    description=oldcaption.text if oldcaption is not None else 'Grafico del confronto'
    if oldcaption is not None and description:root.remove(following)
    caption(el,'Figura',description,'fig_grafico_'+str(index),before=False)
    note=para_before(el,'Lettura del grafico in figura ','Nota ANTHEA');ref(note,'fig_grafico_'+str(index),'…');note.paragraph_format.keep_with_next=True
case='';chapter='';table_for_case={};table_targets={}
for el in list(root):
    if el.tag==qn('w:p'):
        p=Paragraph(el,doc._body)
        if p.style.name=='Heading 1':chapter=p.text;case=''
        m=re.match(r'^([A-Z0-9]+) (\d{2})\b',p.text)
        if p.style.name=='Heading 2' and m:case=m[1]+'-'+m[2]
    if el.tag!=qn('w:tbl') or not chapter:continue
    t=Table(el,doc._body);headers=[c.text for c in t.rows[0].cells]
    isresult=any(x in ' '.join(headers) for x in ['ANTHEA','Checker','Atteso','Soddisfatto'])
    desc=(case+' · ' if case else '')+('Risultati del confronto' if isresult else ('Dati e parametri' if len(headers)<5 else 'Dati del calcolo'))
    cap,target=caption(el,'Tabella',desc)
    if case and isresult and case not in table_for_case:table_for_case[case]=target
    pr=t._tbl.tblPr
    borders=pr.find(qn('w:tblBorders'))
    if borders is None:borders=OxmlElement('w:tblBorders');pr.append(borders)
    for edge in ['top','left','bottom','right','insideH','insideV']:
        b=OxmlElement('w:'+edge);b.set(qn('w:val'),'single');b.set(qn('w:sz'),'4');b.set(qn('w:color'),'D9D9D9');borders.append(b)
    margins=OxmlElement('w:tblCellMar')
    for side,n in [('top',70),('bottom',70),('left',90),('right',90)]:
        v=OxmlElement('w:'+side);v.set(qn('w:w'),str(n));v.set(qn('w:type'),'dxa');margins.append(v)
    pr.append(margins)
    for ri,row in enumerate(t.rows):
        rp=row._tr.get_or_add_trPr();cant=OxmlElement('w:cantSplit');rp.append(cant)
        if ri==0:rp.append(OxmlElement('w:tblHeader'))
        for ci,cell in enumerate(row.cells):
            cell.vertical_alignment=WD_CELL_VERTICAL_ALIGNMENT.CENTER
            cp=cell._tc.get_or_add_tcPr();sh=OxmlElement('w:shd');sh.set(qn('w:fill'),'E7E6E6' if ri==0 else ('F5F5F5' if ri%2==0 else 'FFFFFF'));cp.append(sh)
            for p in cell.paragraphs:
                p.paragraph_format.space_after=Pt(1);p.paragraph_format.space_before=Pt(1);p.paragraph_format.line_spacing=1
                p.alignment=WD_ALIGN_PARAGRAPH.LEFT if ci==0 else WD_ALIGN_PARAGRAPH.CENTER
                for run in p.runs:run.font.size=Pt(10.5);run.bold=ri==0 or (ri>0 and headers[ci] in ['ANTHEA','Checker','ANTHEA valore / limite'])
                if ri>0 and isresult:
                    value=p.text.strip()
                    if value=='DA COMPLETARE':
                        p.text='Da completare';p.runs[0].font.size=Pt(10.5)
                    if re.fullmatch(r'[-−+]?\d+[,.]\d+(?:[eE][−+-]?\d+)?',value):
                        num=float(value.replace('−','-').replace(',','.'))
                        formatted=('0' if num==0 else format(num,'.6g')).replace('.',',')
                        if 'e' in formatted:
                            a,b=formatted.split('e');formatted=a+' × 10'+str(int(b)).translate(str.maketrans('0123456789-','⁰¹²³⁴⁵⁶⁷⁸⁹⁻'))
                        p.text=formatted
                        p.runs[0].font.size=Pt(10.5);p.runs[0].bold=headers[ci] in ['ANTHEA','Checker']
                    if ci==0:
                        labels={'Fbd':'fbd [MPa]','BasicLength':'lb,rqd [mm]','RequiredLength':'L richiesta [mm]','VRd':'VRd [kN]','VRsd':'VRsd [kN]','VRcd':'VRcd [kN]','TRd':'TRd [kNm]','TRsd':'TRsd [kNm]','TRcd':'TRcd [kNm]','TRld':'TRld [kNm]','stress':'Tensione [MPa]','area':'Area CLS [mm²]','polygonArea':'Area poligonale [mm²]','steel':'Area armatura [mm²]','vertices':'Vertici esterni','holes':'Numero di fori','Ratio':'Rapporto di verifica','TorsionRatio':'η torsione','ConcreteCombinedRatio':'η interazione CLS','SteelCombinedRatio':'η interazione staffe','RequiredLongitudinalArea':'As richiesta [mm²]'}
                        if value in labels:p.text=labels[value];p.runs[0].font.size=Pt(10.5)
# Reference strips for each example (the first result table, figure and method).
for id,hp in ca_heads.items():
    p=para_after(hp._p,style='Nota ANTHEA')
    if id in table_for_case:p.add_run('Risultati: tabella ');ref(p,table_for_case[id],'…');p.add_run(' · ')
    p.add_run('Vista: figura ');ref(p,'fig_'+id.replace('-','_'),'…')
    prefix=id.split('-')[0]
    if prefix in ['DT','DP','DS','DW','CD','PC','SP','ZG','VI']:
        p.paragraph_format.keep_with_next=True
        continue
    p.add_run(' · Metodo: equazione ')
    key='materiali' if prefix.startswith(('MC','MEP','MIN')) else 'geometria' if prefix.startswith(('GR','GC')) else 'ancoraggio' if prefix in ['AN','SO'] else 'fessure' if prefix in ['FE','FT','DE','FF'] else 'taglio' if prefix in ['TX','TY','TCP','TCF'] else 'torsione' if prefix in ['TO','TV'] else 'equilibrio_ca'
    ref(p,formula_group[key],'…');p.paragraph_format.keep_with_next=True
# Clickable bibliographic references and case IDs throughout body and table cells.
def linkrun(p,run,pattern,lookup):
    text=run.text;matches=list(re.finditer(pattern,text))
    if not matches:return
    old=run._r;pos=0
    for m in matches:
        target=lookup(m)
        if not target or target not in target_ids:continue
        if m.start()>pos:
            r=OxmlElement('w:r');
            if old.rPr is not None:r.append(deepcopy(old.rPr))
            t=OxmlElement('w:t');t.set(qn('xml:space'),'preserve');t.text=text[pos:m.start()];r.append(t);old.addprevious(r)
        h=OxmlElement('w:hyperlink');h.set(qn('w:anchor'),target);h.set(qn('w:history'),'1');r=OxmlElement('w:r');pr=OxmlElement('w:rPr');co=OxmlElement('w:color');co.set(qn('w:val'),'245A79');pr.append(co);r.append(pr);t=OxmlElement('w:t');t.text=m.group();r.append(t);h.append(r);old.addprevious(h);pos=m.end();refs.append(target)
    if not pos:return
    if pos<len(text):
        r=deepcopy(old)
        for child in list(r):
            if child.tag!=qn('w:rPr'):r.remove(child)
        t=OxmlElement('w:t');t.set(qn('xml:space'),'preserve');t.text=text[pos:];r.append(t);old.addprevious(r)
    p._p.remove(old)
paras=list(doc.paragraphs)+[p for t in doc.tables for row in t.rows for c in row.cells for p in c.paragraphs]
for p in paras:
    if p.style.name.startswith(('Heading','TOC','Formula','Caption')) or p._p.xpath('.//w:instrText') or re.match(r'^(R\d\.|\[\d\])',p.text):continue
    # In the bridge dossier R1/R2 identify shrinkage phases, not CA references.
    ancestor=p._p
    while ancestor.getparent() is not root:ancestor=ancestor.getparent()
    bridge_start=headings['Validazione delle sezioni composte da ponte'][0]._p
    if list(root).index(ancestor)<list(root).index(bridge_start):
        for run in list(p.runs):linkrun(p,run,r'\bR([1-8])\b',lambda m:'bib_ca_R'+m[1])
    for run in list(p.runs):linkrun(p,run,r'\[([1-7])\]',lambda m:'bib_ponte_'+m[1])
    for run in list(p.runs):linkrun(p,run,r'\b([A-Z][A-Z0-9]{0,3})[- ](0[1-4])\b',lambda m:'caso_'+m[1]+'_'+m[2])
# Add meaningful bridge chapter cross-references to the available example views.
bridgelinks={
 'Validazione del metodo cumulativo':['P-CINQUE-0','P-EL0-1','P-EL0-2'],
 'Validazione dello storico lineare':['P-CINQUE-1','P-GET1-1','P-SC1-1','P-RIT-1'],
 'Validazione dello storico non lineare':['P-EL2-1','P-EL2-2','P-PL-1','P-PL-2'],
 'Metodo momento curvatura':['P-MK-1','P-MK-2'],
 'Metodo forza deformazione':['P-NE-1','P-NE-2'],
 'Confronto esterno delle nuove curve':['P-MK-1','P-NE-1','P-PL-1'],
 'Dati dei modelli OpenSees':['P-MK-1','P-NE-1'],
 'Percorsi del confronto storico esterno':['P-GET1-1','P-RIT-1','P-PL-1'],
 'Classe 4 e verifiche accessorie':['P-CINQUE-0'],
 'Due soluzioni N M per ciascuno dei tre metodi':[f'P-EL{m}-{j}' for m in range(3) for j in [1,2]],
 'Getto senza carico e scarico':[f'P-{f}{m}-{j}' for f in ['GET','SC'] for m in [1,2] for j in [1,2]],
 'Ritiro ripetuto e omogeneizzazione':['P-RIT-1','P-RIT-2','P-HOM-1','P-HOM-2'],
 'Sviluppo delle curve e della plasticizzazione':['P-MK-1','P-MK-2','P-NE-1','P-NE-2','P-PL-1','P-PL-2'],
 'Sviluppo numerico del benchmark composto in cinque fasi':['P-CINQUE-0','P-CINQUE-1']}
for title,ids in bridgelinks.items():
    p=para_after(headings[title][0]._p,style='Nota ANTHEA');p.add_run('Viste del modulo: ')
    for j,id in enumerate(ids):
        if j:p.add_run('; ')
        p.add_run(id.removeprefix('P-')+' figura ');ref(p,'fig_'+id.replace('-','_'),'…')
    p.paragraph_format.keep_with_next=True
# Index of all illustrated cases, with page fields.
anchor=headings['Matrice di copertura'][0]._p
p=para_after(anchor,'Ogni identificativo nella matrice conduce al relativo esempio. All’inizio di ciascun caso i rimandi portano alla tabella dei risultati, alla figura e alla formula generale. Le didascalie, le equazioni e i rimandi sono campi Word aggiornabili.','Nota ANTHEA')
# All fields need real updates; Word COM does this before the final render.
settings=doc.settings.element
for old in settings.findall(qn('w:updateFields')):settings.remove(old)
u=OxmlElement('w:updateFields');u.set(qn('w:val'),'true');settings.append(u)
assert not(set(refs)-target_ids),set(refs)-target_ids
doc.save(OUT)
# Preserve original opaque ITEC parts; figures and relationships added by python-docx remain.
with ZipFile(BASE) as z:opaque={n:z.read(n) for n in z.namelist() if n.startswith(('word/header','word/_rels/header','word/theme/','word/font','word/numbering'))}
with ZipFile(OUT) as z:parts={n:z.read(n) for n in z.namelist()}
parts.update(opaque)
with ZipFile(OUT,'w',ZIP_DEFLATED) as z:
    for n,data in parts.items():z.writestr(n,data)
(A/'equations.json').write_text(json.dumps(eqsources,ensure_ascii=False,indent=2),encoding='utf8')
(A/'equations.tex').write_text('\n\n'.join('% '+e['bookmark']+'\n\\begin{equation}\n'+e['latex']+'\n\\end{equation}' for e in eqsources),encoding='utf8')
(A/'authoring-audit.json').write_text(json.dumps(dict(counts=seq,numerical_equations=converted,cases=len(ca_heads),screens=len(cases),bookmarks=len(target_ids),references=len(refs),conversion_errors=failures),ensure_ascii=False,indent=2),encoding='utf8')
print(OUT);print(seq,'Converted',converted,'refs',len(refs),'errors',len(failures))
