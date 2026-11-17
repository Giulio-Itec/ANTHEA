"""Build the editable module guide; Word is used separately for PDF layout QA."""
from pathlib import Path
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

root = Path(__file__).resolve().parents[3]
out = root / 'supporto/docs/sollecitazioni-spalla.docx'
doc = Document()
sec = doc.sections[0]
sec.page_width = Inches(8.5); sec.page_height = Inches(11)
sec.top_margin = sec.bottom_margin = Inches(.65)
sec.left_margin = sec.right_margin = Inches(.7)
normal = doc.styles['Normal']
normal.font.name = 'Arial'; normal.font.size = Pt(10.5)
normal.paragraph_format.space_after = Pt(7)
for name in ['Title', 'Heading 1', 'Heading 2']:
    doc.styles[name].font.name = 'Arial'
    doc.styles[name].font.color.rgb = RGBColor.from_string('000000')
doc.styles['Title'].font.size = Pt(24)
for borders in list(doc.styles.element.iter(qn('w:pBdr'))):
    borders.getparent().remove(borders)
doc.styles['Heading 1'].font.size = Pt(16)
footer = sec.footer.paragraphs[0]
footer.text = 'ANTHEA | Sollecitazioni spalla | 30 ottobre 2026 | '
field = OxmlElement('w:fldSimple'); field.set(qn('w:instr'), 'PAGE'); footer._p.append(field)
footer.style = 'Caption'
def p(text): doc.add_paragraph(text)
def h(text): doc.add_heading(text, 1)
def table(headers, rows):
    t = doc.add_table(rows=1, cols=len(headers)); t.style = 'Table Grid'
    widths = [1.55, 5.55] if len(headers) == 2 else [2.15, 2.0, 2.95]
    t.autofit = False
    for col,width in zip(t.columns,widths): col.width = Inches(width)
    for c,v in zip(t.rows[0].cells,headers): c.text=v
    for row in rows:
        for c,v in zip(t.add_row().cells,row): c.text=v
    for ri,row in enumerate(t.rows):
        for ci,c in enumerate(row.cells):
            c.width = Inches(widths[ci])
            fill=OxmlElement('w:shd'); fill.set(qn('w:fill'),'DCE7EE' if ri == 0 else 'F5F7F9' if ri % 2 else 'FFFFFF')
            c._tc.get_or_add_tcPr().append(fill)
            for para in c.paragraphs:
                para.paragraph_format.space_after=Pt(4)
                for run in para.runs:
                    run.font.size=Pt(10); run.font.color.rgb=RGBColor.from_string('000000'); run.bold=ri == 0
    head=OxmlElement('w:tblHeader'); t.rows[0]._tr.get_or_add_trPr().append(head)
    doc.add_paragraph()

doc.add_paragraph('Sollecitazioni della spalla', 'Title')
p('Guida al modulo geometrico di ANTHEA. Inserendo le dimensioni di basamento, muro frontale, paraghiaia, ali e pali si ottengono volumi, pesi, baricentri, bracci e sollecitazioni. Le reazioni del ponte e i coefficienti di combinazione restano dati assegnati dal progettista.')
h('Uso del modulo')
p('Aprire Moduli singoli → Strutture → Sollecitazioni spalla, oppure File → Nuova spalla. Il modulo può essere aggiunto come foglio di un progetto. Le dimensioni iniziali sono un esempio modificabile e non ricostruiscono automaticamente la spalla del file Excel.')
table(['Scheda', 'Dati da inserire'], [
    ('Geometria','Dimensioni del basamento, tallone, muro, paraghiaia, posizione e rialzo degli appoggi, peso specifico del calcestruzzo.'),
    ('Ali e pali','Presenza indipendente delle ali e loro lunghezze/spessori; maglia, interassi, diametro, lunghezza ed eccentricità del gruppo pali.'),
    ('Ponte','Sei reazioni concomitanti per rara, frequente, quasi permanente e SLV, riferite all’asse degli appoggi.'),
    ('Terreno e casi','Peso specifico e attrito del terreno, traffico a tergo, coefficienti SLU, ψ e kh longitudinale assegnato.')])
p('Il ricalcolo è automatico. Un dato mancante o una geometria incompatibile elimina i risultati precedenti e mostra il campo da correggere. Il selettore del caso aggiorna le tabelle; il rosso in pianta identifica i pali in trazione.')
h('Geometria assunta')
p('Gli elementi sono rettangolari e centrati trasversalmente. Il retro del paraghiaia è allineato al retro del muro frontale. Le ali sono parallele all’asse del ponte, comprese nel tallone e sulla stessa fondazione; la loro altezza è la somma delle altezze di muro e paraghiaia. Sono ammesse lunghezze e spessori diversi a destra e sinistra.')
p('Il terreno gravante occupa il prisma sul tallone largo quanto il muro frontale, al netto delle ali. La spinta globale agisce su un piano virtuale verticale a monte alto fino all’intradosso della fondazione, largo quanto il muro frontale. Le azioni locali usano le rispettive larghezze degli elementi.')

doc.add_page_break()
h('Azioni e risultati')
p('Forze in kN, momenti in kNm, lunghezze in m. Risultanti totali sulla larghezza dell’elemento, non per metro. Origine al centro dell’intradosso del basamento: x verso valle, y verso ala destra, z verso l’alto. Fz positivo a compressione. Mx adotta la convenzione tabellare dell’Excel.')
p('Trasporto: My = Fz·x + Fx·z; Mx = Fz·y + Fy·z; Mz = Fy·x − Fx·y. Le coppie assegnate agli appoggi si sommano ai momenti di trasporto.')
table(['Contributo','Modello'],[
 ('Calcestruzzo','Peso = volume × peso specifico; momento dal baricentro di ciascun blocco.'),
 ('Terreno statico','K₀ = 1 − sin φ. Spinta triangolare K₀γH²b/2, applicata a H/3.'),
 ('Traffico a tergo','Spinta rettangolare K₀qHb, applicata a H/2. Il carico verticale q sul tallone è escluso, come nell’Excel.'),
 ('SLV longitudinale','Inerzia kh·G del calcestruzzo ai baricentri; incremento Wood semplificato khγH²b a H/2.'),
 ('Muri d’ala','Peso e spinte trasversali statiche. Due ali uguali annullano le risultanti trasversali globali ma conservano le sollecitazioni locali.')])
p('SLE rara usa le reazioni rare del ponte e il traffico completo. Frequente e quasi permanente usano le rispettive reazioni, applicando ψ solo al traffico a tergo. SLU applica coefficienti separati a ponte raro, calcestruzzo, terreno e traffico. SLV usa reazioni SLV già combinate e ψ₂ sul traffico a tergo. Un singolo caso per ciascuna famiglia.')
h('Pali e basamento')
p('Plinto rigido con pali uguali: Nᵢ = Fz/n + My·xᵢ/Σx² + Mx·yᵢ/Σy², con coordinate rispetto al baricentro del gruppo e momenti trasportati a quel punto. Taglio uniforme più contributo torsionale Mz. La lunghezza dei pali è rappresentata schematicamente e non modifica la ripartizione per pali di uguale rigidezza.')
p('Coppie sulle file estreme: ciascun momento è affidato alle file estreme, senza contributo delle file interne. Riproduce la semplificazione dell’Excel per la maglia 2 × 5. Entrambi i metodi rispettano l’equilibrio delle sei componenti.')
p('Il basamento mostra N·a per ogni palo, con a dalla distanza fra asse del palo e faccia del muro, e le sezioni globali a monte e valle. Queste sommano le reazioni e sottraggono i pesi sulle mensole. Non sono verifiche a bielle e tiranti, taglio o punzonamento.')

doc.add_page_break()
h('Confronto con il foglio ricevuto')
p('Riferimento: Sollecitazioni spalla.xlsx, spalla A del viadotto V2, commessa Rigolato. Fogli: Parghiaia, SLU, SLV e Foglio1. Nessun collegamento esterno o errore Excel memorizzato rilevato. L’originale non è stato modificato.')
table(['Controllo','Excel','Modulo e osservazione'],[
 ('Paraghiaia SLU M','3.286,491947 kNm','Riprodotto: H=4,60 m, b=10,65 m.'),
 ('Paraghiaia SLU V','1.861,342693 kN','Riprodotto: γ=20, φ=35°, q=20.'),
 ('Palo SLU N massimo','3.017,985241 kN','Riprodotto con coppie estreme e risultanti Excel.'),
 ('Palo SLU N massimo rigido','Non presente','3.001,953991 kN, stessi carichi e maglia.'),
 ('Momento ponte QP e FR','D77/E77 usano D21','Usate F21/E21: 413,25 / 570 kNm, anziché 712,50.'),
 ('Pesi e baricentri','Manuali','Derivati dalla geometria, non dalla replica dei valori manuali.')])
p('Foglio1 contiene conteggi di armatura, esclusi da questa versione dedicata alle sollecitazioni. I confronti usano formule e valori effettivi del file; il confronto dei pali isola la ripartizione applicando le risultanti originali.')
h('Controlli e campo di impiego')
p('Abutment.Checks: 2.528 controlli superati su geometria, ali, formule Excel, 200 maglie eccentriche con torsione per entrambi i metodi, dati invalidi e salvataggio. La prova WPF copre ricalcolo, dati incompleti, ali, sisma, navigazione, salvataggio e layout a 1.600 e 1.050 pixel.')
p('Limiti: terreno asciutto omogeneo, geometria ortogonale, ali rettangolari sulla stessa fondazione, pali verticali di uguale rigidezza. Esclusi ali inclinate o con fondazione propria, falda, sisma trasversale delle ali, inerzia del terreno gravante, interazione terreno–struttura, redistribuzione dei pali in trazione e verifiche di resistenza/geotecniche.')
p('I casi non sono un inviluppo normativo automatico: occorrono reazioni concomitanti e coefficienti appropriati, con segni e direzioni pertinenti. kh è il coefficiente di calcolo assegnato; non viene ricavato automaticamente da ag. Nessun esito di conformità complessiva è emesso.')
p('Riferimento del gruppo elastico: FHWA, Design and Construction of Driven Pile Foundations, Volume I, GEC 12, metodo del plinto rigido con pali di uguale rigidezza. https://www.fhwa.dot.gov/engineering/geotech/pubs/gec12/nhi16009_v1.pdf')
out.parent.mkdir(parents=True,exist_ok=True); doc.save(out); print(out)
