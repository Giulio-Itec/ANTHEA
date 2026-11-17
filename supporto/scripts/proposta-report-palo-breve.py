"""Two-page editorial preview from the verified ITEC report; no engineering recalculation."""
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import json
from lxml import etree as E

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'supporto/artefatti/report-palo-default-grigio-2026-11-16/Prova palo predefinita.docx'
DATA = ROOT / 'supporto/artefatti/ci/20261116-121930-report-palo-default-grigio/ui_check-itec-pile-report'
OUT = ROOT / 'supporto/artefatti/report-palo-breve-proposta-2026-11-16'
OUT.mkdir(parents=True, exist_ok=True)
DEST = OUT / 'Proposta report breve palo.docx'
NS = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main',
      'wp': 'http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing',
      'a': 'http://schemas.openxmlformats.org/drawingml/2006/main'}
W = '{' + NS['w'] + '}'
def el(tag, **attrs):
    return E.Element(W + tag, {W + k: str(v) for k, v in attrs.items()})
def text(node):
    return ''.join(node.itertext()) if node is None else ''.join(node.xpath('.//w:t/text()', namespaces=NS))
def xml(node):
    return E.tostring(node, encoding='utf-8', xml_declaration=True, standalone=True)
with ZipFile(SOURCE) as z:
    parts = {n: z.read(n) for n in z.namelist()}
doc = E.fromstring(parts['word/document.xml'])
body = doc.find('w:body', NS)
section = deepcopy(body.find('w:sectPr', NS))
tables = body.findall('w:tbl', NS)
rows = lambda t: [[text(c) for c in r.findall('w:tc', NS)] for r in t.findall('w:tr', NS)]
source_tables = {text(t.getprevious()).split('  ', 1)[-1]: rows(t) for t in tables}
geometry = dict(rows(tables[0])[1:])
coefficients = dict(rows(tables[1])[1:])
actions = dict(rows(tables[10])[1:])
verification = rows(tables[11])[1:]
capacity = deepcopy(body.xpath('w:p[.//w:drawing]', namespaces=NS)[-1])
data = json.loads((DATA / 'dati-prova.json').read_text(encoding='utf-8'))
results = json.loads((DATA / 'risultati-prova.json').read_text(encoding='utf-8'))
assert results['copertura_completa'] and len(verification) == 4
assert all(row[-1] == 'Verifica non soddisfatta' for row in verification if 'compressione' in row[0])
assert all(row[3] == '0' for row in verification if 'trazione' in row[0])

# The approved reference was rendered in Word and inspected in the preceding task.
contract = f'''# Report breve palo

Reference: {SOURCE.as_posix()}
SHA256: {sha256(SOURCE.read_bytes()).hexdigest()}
Reference rendering: supporto/artefatti/report-palo-default-grigio-2026-11-16/qa-word (8 pages, inspected).
Reference page system: A4 portrait 11906 x 16838 twips; top/bottom 1701, left/right 1134; no header/footer references.
Typography preserved: Manrope 12 pt body; Titolo1 Manrope ExtraBold capitals; Titolo2 Manrope SemiBold small capitals; Didascalia Manrope ExtraBold 10 pt.
Tables: source 9638 twips, grey E7E6E6 header, 808080 half-point borders. Preview tables 10 pt, centred numbers, left-aligned labels, expanded cell padding.
Intentional changes for the two-page preview: rewrite body, use compact horizontal data tables, one explicit page break, compact heading spacing, capacity figure 16 cm wide. Add Title style derived from the source title family.
Page 1 slots: title and example identification; geometry; geotechnical layers; criteria, coefficients and actions.
Page 2 slots: final verification table; outcome and Nq boundary warning; capacity diagram copied unchanged from the source.
Omitted by the requested brief mode: cover, index, full theory, Nq curves, detailed integration tables.
Preserve-only: all package parts except word/document.xml and word/styles.xml; figure media and relationships byte-identical.
Fidelity gate: exactly 2 pages in native Word; no PDF; all values copied from the existing example report; all paragraphs and tables remain editable; no production code changes.
'''
(OUT / 'artifact.md').write_text(contract, encoding='utf-8')
(OUT / 'reference-parts.json').write_text(json.dumps({n: {'bytes': len(b), 'sha256': sha256(b).hexdigest()} for n, b in parts.items()}, indent=2), encoding='utf-8')

for child in list(body):
    body.remove(child)

def p(value='', style='Normale', before=0, after=100, size=None, keep=False, align='left', page=False, bold=False):
    node = el('p'); prop = el('pPr'); node.append(prop)
    prop.append(el('pStyle', val=style))
    if keep: prop.append(el('keepNext'))
    prop.append(el('keepLines'))
    if page: prop.append(el('pageBreakBefore'))
    prop.append(el('spacing', before=before, after=after, line=240, lineRule='auto'))
    prop.append(el('jc', val=align))
    run = el('r'); node.append(run)
    rp = el('rPr'); run.append(rp)
    if bold: rp.append(el('b'))
    rp.append(el('color', val='000000'))
    if size: rp.append(el('sz', val=int(size * 2)))
    for i, line in enumerate(value.split('\n')):
        if i: run.append(el('br'))
        t = el('t'); t.text = line; t.set('{http://www.w3.org/XML/1998/namespace}space', 'preserve'); run.append(t)
    return node

def para(value, **kw):
    node = p(value, **kw); body.append(node); return node

def heading(value, page=False):
    para(value, style='Titolo1', before=200 if not page else 0, after=120, keep=True, page=page)

def table(headers, values, weights, left=()):
    widths = [round(9638 * w / sum(weights)) for w in weights]
    widths[-1] += 9638 - sum(widths)
    tab = el('tbl'); pr = el('tblPr'); tab.append(pr)
    pr.append(el('tblW', w=9638, type='dxa'))
    borders = el('tblBorders'); pr.append(borders)
    for side in ['top','left','bottom','right','insideH','insideV']:
        borders.append(el(side, val='single', sz=4, color='808080'))
    pr.append(el('tblLayout', type='fixed'))
    margins = el('tblCellMar'); pr.append(margins)
    for side in ['top','left','bottom','right']:
        margins.append(el(side, w=70 if side in ['top','bottom'] else 85, type='dxa'))
    grid = el('tblGrid'); tab.append(grid)
    for width in widths: grid.append(el('gridCol', w=width))
    for i, row in enumerate([headers] + values):
        tr = el('tr'); tab.append(tr); trpr = el('trPr'); tr.append(trpr); trpr.append(el('cantSplit'))
        if not i: trpr.append(el('tblHeader'))
        for j, value in enumerate(row):
            tc = el('tc'); tr.append(tc); cp = el('tcPr'); tc.append(cp)
            cp.append(el('tcW', w=widths[j], type='dxa'))
            if not i: cp.append(el('shd', val='clear', fill='E7E6E6'))
            cp.append(el('vAlign', val='center'))
            tc.append(p(str(value), size=10, after=0, bold=not i, align='left' if i and j in left else 'center'))
    body.append(tab)
    para('', size=2, after=60)

styles = E.fromstring(parts['word/styles.xml'])
title = styles.xpath('w:style[@w:styleId="Title"]', namespaces=NS)
if title:
    title = title[0]
    for c in list(title): title.remove(c)
else:
    title = el('style', type='paragraph', styleId='Title'); styles.append(title)
title.append(el('name', val='Title')); title.append(el('basedOn', val='Normale')); title.append(el('next', val='Normale')); title.append(el('qFormat'))
rp = el('rPr'); title.append(rp); rp.append(el('rFonts', ascii='Manrope ExtraBold', hAnsi='Manrope ExtraBold')); rp.append(el('b')); rp.append(el('color', val='000000')); rp.append(el('sz', val=28))

para('Capacità portante verticale del palo', style='Title', after=100, keep=True)
para('Report breve · Esempio con dati di prova', size=10, after=160)
para('Sintesi della verifica geotecnica a compressione e trazione. Alla quota finale la compressione non risulta verificata nelle due condizioni analizzate.', after=140)

heading('1  Oggetto e dati generali')
table(['Palo', 'D [m]', 'L [m]', 'Testa [m]', 'γp [kN/m³]', 'Verticali'],
      [[geometry['Tipologia del palo'], geometry['Diametro D [m]'], geometry['Lunghezza L [m]'], '0',
        geometry['Peso dell’unità di volume γp [kN/m³]'], geometry['Verticali indagate n']]], [2.4,1,1,1.3,1.7,1.3], left=(0,))
para('Quote positive verso il basso dal piano campagna. La testa del palo coincide con il piano campagna; la punta è a 12 m.', size=11)

heading('2  Modello geotecnico')
para('Una verticale di indagine, estesa a 14 m. Falda a 3 m dal piano campagna; copertura stratigrafica completa.', size=11)
table(['Strato e terreno', 'Quota [m]', 'γ / γsat\n[kN/m³]', 'φ′ [°]', 'c′ / cu\n[kPa]', 'Laterale'],
      [['1  Granulare', '0–6', '18 / 20', '32', '0 / 0', 'Attiva'],
       ['2  Coesivo', '6–14', '18 / 20', '24', '0 / 80', 'Attiva']], [2.8,1.5,1.8,1.1,1.7,1.5], left=(0,))
para('Nc = 9 nel terreno coesivo. Nella verifica non drenata, il tratto sopra falda è trattato in condizioni drenate.', size=11)

heading('3  Criteri coefficienti e azioni')
para('NTC 2018, § 6.4.3.1.1, tabelle 6.4.II e 6.4.IV; azioni sui ponti: tabella 5.1.V. Approccio 2: A1 + M1 + R3.', size=11)
keys = ['ξ3','ξ4','γb','γs,c','γs,t','γG sfavorevole','γG favorevole']
table(['ξ3', 'ξ4', 'γb', 'γs,c', 'γs,t', 'γG sfav.', 'γG fav.'], [[coefficients[k] for k in keys]], [1,1,1,1,1,1.6,1.6])
para('Palo singolo, senza riduzione di gruppo: ηc = ηt = 1. Azioni assegnate in testa già di progetto, prima del contributo del peso del palo.', size=11)
table(['Compressione in testa [kN]', 'Trazione in testa [kN]', 'Peso proprio alla punta [kN]'],
      [[actions['Compressione in testa [kN]'], actions['Trazione in testa [kN]'], actions['Peso proprio alla quota finale [kN]']]], [1,1,1])
para('Le azioni Ed della pagina seguente comprendono il peso e il trattamento della sottospinta previsto dal modello per ciascuna condizione.', size=11, after=0)

heading('4  Resistenze e verifiche alla punta', page=True)
para('Quota finale z = 12 m. Criterio di verifica: Ed ≤ Rd; indice di utilizzo I = Ed/Rd ≤ 1.', size=11)
table(['Verifica', 'Rd [kN]', 'Ed [kN]', 'I', 'Esito'],
      [[r[0], r[2], r[3], r[4], 'Non verificata' if r[-1] == 'Verifica non soddisfatta' else 'Verificata'] for r in verification],
      [3.2,1.5,1.5,.8,2.2], left=(0,))
para('Compressione non verificata in entrambe le condizioni. In trazione, alla quota finale, il peso efficace del palo riduce a zero l’azione netta Ed.', size=11)
para('Avviso Nq: applicati valori al bordo dell’abaco per φ fuori campo e per z/D < 4.', size=10, after=120)
heading('5  Andamento della capacità portante')
pr = capacity.find('w:pPr', NS)
for child in list(pr): pr.remove(child)
pr.append(el('keepNext')); pr.append(el('spacing', before=0, after=0)); pr.append(el('jc', val='center'))
extent = capacity.find('.//wp:extent', NS)
cx = 5760000; cy = round(int(extent.get('cy')) * cx / int(extent.get('cx')))
extent.set('cx', str(cx)); extent.set('cy', str(cy))
for ext in capacity.findall('.//a:xfrm/a:ext', NS): ext.set('cx', str(cx)); ext.set('cy', str(cy))
body.append(capacity)
para('Resistenze di progetto e azioni lungo il palo', style='Didascalia', size=10, align='center', after=0)
body.append(section)

parts['word/document.xml'] = xml(doc)
parts['word/styles.xml'] = xml(styles)
with ZipFile(DEST, 'w', ZIP_DEFLATED) as z:
    for n, b in parts.items(): z.writestr(n, b)
with ZipFile(SOURCE) as ref, ZipFile(DEST) as final:
    assert set(ref.namelist()) == set(final.namelist())
    assert all(ref.read(n) == final.read(n) for n in ref.namelist() if n not in ['word/document.xml','word/styles.xml'])
print(str(DEST))
