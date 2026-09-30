"""Repackage the full ANTHEA guides using the user's original ITEC Word template.

Use the bundled Python runtime. Output field caches must be refreshed with the
companion Word QA script, then imported with Finalize-AntheaGuides-Itec.py.
"""
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import json
import re
import shutil

from lxml import etree as E
from docx import Document
from docx.shared import Cm, Pt
from docx.enum.style import WD_STYLE_TYPE
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

ROOT = Path(__file__).resolve().parents[2]
REVISION = '07'
# edition data of the revision: date, contents and description in the revision table of the cover
DATE = '30/09/2026'
CONTENTS = '30 settembre 2026'
CONTENTS_ISO = '2026-09-30'
REVISION_NOTE = 'MURI SISMA SLE E ARMATURE'
ART = ROOT / f'supporto/artefatti/guide_anthea_itec_rev{REVISION}'
OUT = ROOT / 'supporto/documentazione/Guide_ANTHEA'
TEMPLATE = Path('C:/Users/g.pacini/Desktop/MODELLO-RELAZIONE-ITEC-AA.docx')
TEMPLATE_HASH = 'f6f3f04b0bafeea09e4cca1b19fa74e95fc540c264aa518f3d93dee9eebbcd92'
GUIDES = {
    'pratica': ('Guida pratica di ANTHEA', 'Manuale operativo dei moduli disponibili',
                'Progetti e materiali\nGeotecnica e sezioni strutturali\nBridge Design e report', 'ANTHEA-GP-02'),
    'teorica': ('Guida teorica dei calcoli di ANTHEA', 'Modelli formule ipotesi ed esempi dei moduli disponibili',
                'Geotecnica e materiali\nSezioni in calcestruzzo e composte\nBridge Design e modelli di calcolo', 'ANTHEA-GT-02'),
}

def replace_text(p, value):
    if p.runs:
        p.runs[0].text = value
        for r in p.runs[1:]:
            r.text = ''
    else:
        p.add_run(value)

def cell_text(cell, value):
    p = cell.paragraphs[0]
    replace_text(p, value)
    for extra in cell.paragraphs[1:]:
        extra._p.getparent().remove(extra._p)
    for num in p._p.xpath('./w:pPr/w:numPr'):
        num.getparent().remove(num)

def write_table(doc, lines):
    rows = [[c.strip() for c in line.strip().strip('|').split('|')] for line in lines]
    rows = [r for r in rows if not all(re.fullmatch(r'[:\- ]+', c) for c in r)]
    count = len(rows[0])
    widths = {2: [5.3, 11.7], 3: [4.2, 6.0, 6.8], 4: [7.0, 3.2, 3.2, 3.6], 5: [5.0, 3.0, 3.0, 3.0, 3.0]}[count]
    if rows[0][0] == 'Ambito':
        widths = [3.4, 3.0, 3.2, 2.4, 5.0]
    if rows[0][0] == 'Numero di indagini selezionato':
        widths = [9.0, 4.0, 4.0]
    table = doc.add_table(rows=0, cols=count)
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.autofit = False
    for col, width in zip(table.columns, widths):
        col.width = Cm(width)
    borders = OxmlElement('w:tblBorders')
    for side in ('top', 'left', 'bottom', 'right', 'insideH', 'insideV'):
        edge = OxmlElement('w:' + side)
        for k, v in [('val', 'single'), ('sz', '4'), ('color', 'B7B7B7')]:
            edge.set(qn('w:' + k), v)
        borders.append(edge)
    table._tbl.tblPr.append(borders)
    for ri, values in enumerate(rows):
        row = table.add_row()
        row._tr.get_or_add_trPr().append(OxmlElement('w:cantSplit'))
        if ri == 0:
            row._tr.get_or_add_trPr().append(OxmlElement('w:tblHeader'))
        for ci, (cell, value) in enumerate(zip(row.cells, values)):
            cell.width = Cm(widths[ci])
            cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            props = cell._tc.get_or_add_tcPr()
            margins = OxmlElement('w:tcMar')
            for side in ('top', 'left', 'bottom', 'right'):
                m = OxmlElement('w:' + side)
                m.set(qn('w:w'), '90')
                m.set(qn('w:type'), 'dxa')
                margins.append(m)
            props.append(margins)
            if ri == 0:
                shade = OxmlElement('w:shd')
                shade.set(qn('w:fill'), 'E7E6E6')
                props.append(shade)
            p = cell.paragraphs[0]
            p.alignment = WD_ALIGN_PARAGRAPH.LEFT
            if ci > 0 and len(value) < 18:
                p.alignment = WD_ALIGN_PARAGRAPH.CENTER
            p.paragraph_format.space_before = Pt(1)
            p.paragraph_format.space_after = Pt(1)
            r = p.add_run(value)
            r.font.size = Pt(10.5)
            r.bold = ri == 0
    doc.add_paragraph().paragraph_format.space_after = Pt(0)

def patch_footer(data, filename):
    for name in ('word/footer1.xml', 'word/footer4.xml', 'word/footer5.xml'):
        root = E.fromstring(data[name])
        for instr in root.findall('.//' + qn('w:instrText')):
            if 'FILENAME' not in (instr.text or ''):
                continue
            sibling = instr.getparent().getnext()
            while sibling is not None:
                text = sibling.find('.//' + qn('w:t'))
                if text is not None:
                    text.text = filename
                    break
                sibling = sibling.getnext()
        data[name] = E.tostring(root, xml_declaration=True, encoding='UTF-8', standalone=True)

def build(kind):
    title, subtitle, summary, code = GUIDES[kind]
    source = ROOT / f'supporto/docs/guida-{kind}-anthea.md'
    lines = source.read_text(encoding='utf-8').splitlines()
    filename = f'ANTHEA_Guida_{kind}_ITEC_Rev{REVISION}.docx'
    qa = ART / kind
    qa.mkdir(parents=True, exist_ok=True)
    doc = Document(TEMPLATE)
    sections = [deepcopy(s._sectPr) for s in doc.sections]
    index_title = deepcopy(list(doc._element.body)[24])

    replace_text(doc.paragraphs[2], title)
    replace_text(doc.paragraphs[4], subtitle)
    cell_text(doc.tables[0].cell(3, 0), 'Ambito:')
    cell_text(doc.tables[0].cell(5, 1), 'Documentazione software ANTHEA')
    # The original roster table also reserves the cover's second column.
    # Retain its geometry and replace names with actual edition metadata.
    for row in doc.tables[1].rows:
        for cell in row.cells:
            cell_text(cell, '')
    cell_text(doc.tables[1].cell(0, 1), 'Edizione')
    for ri, values in enumerate([('Rev.', REVISION), ('Data', DATE), ('Testi', DATE)], 1):
        for ci, value in enumerate(values, 1):
            cell_text(doc.tables[1].cell(ri, ci), value)
    cell_text(doc.tables[2].cell(1, 0), 'Guida pratica all’uso' if kind == 'pratica' else 'Guida teorica dei calcoli')
    cell_text(doc.tables[2].cell(2, 0), summary + '\nContenuti al ' + CONTENTS)
    for ri, values in enumerate([
        ['Tipo documento: MANUALE', 'Documento:', code],
        ['Software: ANTHEA', 'Revisione:', REVISION + ' — Modello ITEC'],
    ]):
        for ci, value in enumerate(values):
            cell_text(doc.tables[3].cell(ri, ci), value)
    for ci, value in enumerate([REVISION, REVISION_NOTE, DATE, '', '', '']):
        cell_text(doc.tables[4].cell(1, ci), value)
    for ti, size in [(1, 8), (2, 11), (3, 10), (4, 8)]:
        for row in doc.tables[ti].rows:
            for cell in row.cells:
                for p in cell.paragraphs:
                    p.alignment = WD_ALIGN_PARAGRAPH.LEFT
                    for run in p.runs:
                        run.font.size = Pt(size)
    body = doc._element.body
    for child in list(body)[23:]:
        body.remove(child)
    body.append(deepcopy(sections[4]))
    if 'Title' not in doc.styles:
        s = doc.styles.add_style('Title', WD_STYLE_TYPE.PARAGRAPH)
        s.base_style = doc.styles['Normal']
        s.font.name = 'Manrope'
        s.font.size = Pt(14)
    doc.paragraphs[2].style = doc.styles['Title']
    doc.paragraphs[2].alignment = WD_ALIGN_PARAGRAPH.LEFT

    # Reuse the reference index heading and build a native two-level Word TOC.
    body.insert(len(body) - 1, index_title)
    toc = doc.add_paragraph()
    fld = OxmlElement('w:fldSimple')
    fld.set(qn('w:instr'), 'TOC \\o "1-2" \\h \\z \\u')
    toc._p.append(fld)
    idxsect = deepcopy(sections[3])
    idxsect.find(qn('w:type')).set(qn('w:val'), 'continuous')
    idxsect.find(qn('w:pgMar')).set(qn('w:bottom'), '1134')
    doc.add_paragraph()._p.get_or_add_pPr().append(idxsect)

    finalsect = doc.sections[-1]._sectPr
    finalsect.find(qn('w:pgMar')).set(qn('w:bottom'), '1134')
    for tag in ('titlePg', 'pgNumType', 'type', 'headerReference'):
        for el in finalsect.findall(qn('w:' + tag)):
            finalsect.remove(el)
    number = OxmlElement('w:pgNumType')
    number.set(qn('w:start'), '1')
    finalsect.append(number)
    for ref in sections[3].findall(qn('w:headerReference')):
        if ref.get(qn('w:type')) == 'default':
            finalsect.insert(0, deepcopy(ref))

    # The title, subtitle and edition are on the cover; all subsequent content
    # is preserved, including the opening context preceding chapter one.
    start = next(i for i, line in enumerate(lines) if line.startswith('Edizione ')) + 1
    i = start
    while i < len(lines):
        line = lines[i].strip()
        i += 1
        if not line:
            continue
        if line.startswith('## ') or line.startswith('### '):
            level = 2 if line.startswith('### ') else 1
            text = re.sub(r'^\d+\s+(?:\d+\s+)?', '', line[level + 2:])
            p = doc.add_paragraph(text, style=f'Heading {level}')
            p.paragraph_format.keep_with_next = True
            p.paragraph_format.space_after = Pt(7)
            continue
        if line.startswith('|'):
            table_lines = [line]
            while i < len(lines) and lines[i].strip().startswith('|'):
                table_lines.append(lines[i])
                i += 1
            write_table(doc, table_lines)
            continue
        p = doc.add_paragraph()
        p.paragraph_format.space_after = Pt(7)
        if line.startswith('$$ '):
            p.paragraph_format.left_indent = Cm(.45)
            p.paragraph_format.space_before = Pt(3)
            p.paragraph_format.space_after = Pt(4)
            p.paragraph_format.keep_with_next = i < len(lines) and lines[i].startswith('$$ ')
            math = OxmlElement('m:oMath')
            r = OxmlElement('m:r')
            props = OxmlElement('m:rPr')
            style = OxmlElement('m:sty')
            style.set(qn('m:val'), 'p')
            props.append(style)
            r.append(props)
            wprops = OxmlElement('w:rPr')
            size = OxmlElement('w:sz')
            size.set(qn('w:val'), '21')
            wprops.append(size)
            r.append(wprops)
            t = OxmlElement('m:t')
            t.set(qn('xml:space'), 'preserve')
            t.text = line[3:]
            r.append(t)
            math.append(r)
            p._p.append(math)
        elif line.startswith('!['):
            match = re.fullmatch(r'!\[(.*?)\]\((.*?)\)', line)
            image_path = (source.parent / match.group(2)).resolve()
            p.paragraph_format.keep_with_next = True
            pic = p.add_run().add_picture(str(image_path), width=Cm(17))
            pic._inline.docPr.set('descr', match.group(1))
            doc.add_paragraph(match.group(1), style='Caption')
        else:
            if re.match(r'^\d+\. ', line):
                p.paragraph_format.left_indent = Cm(.5)
                p.paragraph_format.first_line_indent = Cm(-.5)
            p.add_run(line)

    settings = doc.settings.element
    for el in settings.findall(qn('w:updateFields')):
        settings.remove(el)
    update = OxmlElement('w:updateFields')
    update.set(qn('w:val'), 'true')
    settings.append(update)
    doc.core_properties.title = title
    doc.core_properties.subject = subtitle
    doc.core_properties.author = 'ANTHEA'
    doc.core_properties.last_modified_by = 'ANTHEA'
    doc.core_properties.revision = int(REVISION)
    doc.core_properties.keywords = f'ANTHEA; ITEC; Manuale; Rev{REVISION}; Contenuti {CONTENTS_ISO}'
    authored = qa / 'authored.docx'
    doc.save(authored)

    # python-docx is only the editing model. Restore every untouched original
    # package part so opaque logos, header XML and styles are not reserialized.
    editable = {'word/document.xml', 'word/_rels/document.xml.rels', 'word/styles.xml',
                'word/settings.xml', 'docProps/core.xml', 'docProps/app.xml', '[Content_Types].xml'}
    with ZipFile(TEMPLATE) as original, ZipFile(authored) as edited:
        data = {n: edited.read(n) for n in edited.namelist()}
        for n in original.namelist():
            if n not in editable:
                data[n] = original.read(n)
        # Keep existing style elements byte-semantically intact; add Title only.
        old_styles = E.fromstring(original.read('word/styles.xml'))
        new_styles = E.fromstring(edited.read('word/styles.xml'))
        old_ids = {s.get(qn('w:styleId')) for s in old_styles.findall(qn('w:style'))}
        for s in new_styles.findall(qn('w:style')):
            if s.get(qn('w:styleId')) not in old_ids:
                old_styles.append(deepcopy(s))
        data['word/styles.xml'] = E.tostring(old_styles, xml_declaration=True, encoding='UTF-8', standalone=True)
        old_rels = E.fromstring(original.read('word/_rels/document.xml.rels'))
        new_rels = E.fromstring(edited.read('word/_rels/document.xml.rels'))
        edited_by_id = {r.get('Id'): r for r in new_rels}
        for r in old_rels:
            if r.get('Id') in edited_by_id:
                assert dict(r.attrib) == dict(edited_by_id[r.get('Id')].attrib)
            else:
                new_rels.append(deepcopy(r))
        data['word/_rels/document.xml.rels'] = E.tostring(new_rels, xml_declaration=True, encoding='UTF-8', standalone=True)
    patch_footer(data, filename)
    target = OUT / filename
    with ZipFile(target, 'w', ZIP_DEFLATED) as z:
        for name, content in data.items():
            z.writestr(name, content)
    print(target)

if __name__ == '__main__':
    assert sha256(TEMPLATE.read_bytes()).hexdigest() == TEMPLATE_HASH
    assert (ART / 'artifact.md').is_file()
    (ART / 'template').mkdir(parents=True, exist_ok=True)
    shutil.copyfile(TEMPLATE, ART / 'template' / TEMPLATE.name)
    with ZipFile(TEMPLATE) as z:
        inventory = {n: {'bytes': len(z.read(n)), 'sha256': sha256(z.read(n)).hexdigest()} for n in z.namelist()}
    (ART / 'template/package-inventory.json').write_text(json.dumps(inventory, indent=2), encoding='utf-8')
    OUT.mkdir(parents=True, exist_ok=True)
    for kind in GUIDES:
        build(kind)
