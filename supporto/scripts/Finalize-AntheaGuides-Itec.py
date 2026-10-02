"""Import Word field caches, preserve original ITEC package parts, audit content.

Run after Render-AntheaGuides-Itec.ps1 and before its -VerifyFinal pass.
"""
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import importlib.util
import json
import re

from lxml import etree as E
from pypdf import PdfReader

spec = importlib.util.spec_from_file_location('guide_builder', Path(__file__).with_name('Build-AntheaGuides-Itec.py'))
B = importlib.util.module_from_spec(spec)
spec.loader.exec_module(B)
W = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
R = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
M = 'http://schemas.openxmlformats.org/officeDocument/2006/math'
NS = {'w': W, 'm': M}

def xml(data):
    return E.tostring(data, xml_declaration=True, encoding='UTF-8', standalone=True)

def identity(z, rel):
    kind, target = rel.get('Type'), rel.get('Target')
    if kind.endswith('/image'):
        return kind, sha256(z.read('word/' + target)).hexdigest()
    return kind, target

def shape(node):
    # Namespace-prefix-independent structural comparison of style definitions.
    return [node.tag, sorted(node.attrib.items()), node.text, [shape(c) for c in node]]

def finalize(kind):
    qa = B.ART / kind
    target = B.OUT / f'ANTHEA_Guida_{kind}_ITEC_Rev{B.REVISION}.docx'
    pdf = PdfReader(qa / 'fields_updated.pdf')
    body_pages = int(re.search(r'Pagina\s+\d+\s+di\s+(\d+)', pdf.pages[-1].extract_text()).group(1))
    with ZipFile(target) as original, ZipFile(qa / 'word_updated.docx') as updated:
        data = {n: original.read(n) for n in original.namelist()}
        old_rels = E.fromstring(original.read('word/_rels/document.xml.rels'))
        new_rels = E.fromstring(updated.read('word/_rels/document.xml.rels'))
        old_ids = {identity(original, r): r.get('Id') for r in old_rels}
        updated_by_id = {r.get('Id'): r for r in new_rels}
        source = E.fromstring(data['word/document.xml'])
        final = E.fromstring(updated.read('word/document.xml'))
        original_sections = source.findall('.//w:sectPr', NS)
        updated_sections = final.findall('.//w:sectPr', NS)
        assert len(original_sections) == len(updated_sections) == 5
        for old, new in zip(original_sections, updated_sections):
            new.getparent().replace(new, deepcopy(old))
        for element in final.iter():
            if element.tag in (f'{{{W}}}headerReference', f'{{{W}}}footerReference'):
                continue
            for key, value in list(element.attrib.items()):
                if key.startswith('{' + R + '}') and value in updated_by_id:
                    signature = identity(updated, updated_by_id[value])
                    assert signature in old_ids, signature
                    element.set(key, old_ids[signature])
        # Word normalizes Unicode minus and prime characters when saving math.
        # Only field caches are wanted from Word: restore the editable original
        # math elements so even those symbols remain identical to the sources.
        with ZipFile(qa / 'authored.docx') as authored:
            math_source = E.fromstring(authored.read('word/document.xml'))
        original_math = math_source.findall('.//m:oMath', NS)
        updated_math = final.findall('.//m:oMath', NS)
        assert len(original_math) == len(updated_math)
        for old, new in zip(original_math, updated_math):
            new.getparent().replace(new, deepcopy(old))
        data['word/document.xml'] = xml(final)
    foot = E.fromstring(data['word/footer4.xml'])
    for instr in foot.findall('.//w:instrText', NS):
        code = (instr.text or '').strip()
        if code.startswith('SECTIONPAGES') or code == 'PAGE':
            sib = instr.getparent().getnext()
            while sib is not None:
                text = sib.find('.//w:t', NS)
                if text is not None:
                    text.text = str(body_pages) if code.startswith('SECTIONPAGES') else '1'
                    break
                sib = sib.getnext()
    data['word/footer4.xml'] = xml(foot)
    B.patch_footer(data, target.name)
    app = E.fromstring(data['docProps/app.xml'])
    for element in app.iter():
        if E.QName(element).localname == 'Pages':
            element.text = str(len(pdf.pages))
    data['docProps/app.xml'] = xml(app)
    with ZipFile(target, 'w', ZIP_DEFLATED) as z:
        for name, content in data.items():
            z.writestr(name, content)

    editable = {'word/document.xml', 'word/_rels/document.xml.rels', 'word/styles.xml',
                'word/settings.xml', 'docProps/core.xml', 'docProps/app.xml', '[Content_Types].xml',
                'word/footer1.xml', 'word/footer4.xml', 'word/footer5.xml'}
    with ZipFile(B.TEMPLATE) as template, ZipFile(target) as result:
        preserved = [n for n in template.namelist() if n not in editable]
        assert all(template.read(n) == result.read(n) for n in preserved)
        old_styles = E.fromstring(template.read('word/styles.xml'))
        new_styles = E.fromstring(result.read('word/styles.xml'))
        new_style_map = {s.get(f'{{{W}}}styleId'): s for s in new_styles.findall('w:style', NS)}
        for s in old_styles.findall('w:style', NS):
            assert shape(s) == shape(new_style_map[s.get(f'{{{W}}}styleId')])
        original_rels = E.fromstring(template.read('word/_rels/document.xml.rels'))
        result_rels = E.fromstring(result.read('word/_rels/document.xml.rels'))
        new_rel_map = {r.get('Id'): dict(r.attrib) for r in result_rels}
        assert all(dict(r.attrib) == new_rel_map[r.get('Id')] for r in original_rels)
        numbering = E.fromstring(result.read('word/numbering.xml'))
        number_ids = {'0'} | {n.get(f'{{{W}}}numId') for n in numbering.findall('w:num', NS)}
        assert all(n.get(f'{{{W}}}val') in number_ids for n in final.findall('.//w:numId', NS))
        for element in final.iter():
            for key, value in element.attrib.items():
                if key.startswith('{' + R + '}'):
                    assert value in new_rel_map, (key, value)
        # Text-level identity after accounting for Word's native heading numbers.
        doc_text = ''.join(final.xpath('//w:body//w:t/text()|//w:body//m:t/text()', namespaces=NS))
        source_lines = (B.ROOT / f'supporto/docs/guida-{kind}-anthea.md').read_text(encoding='utf-8').splitlines()
        start = next(i for i, line in enumerate(source_lines) if line.startswith('Edizione ')) + 1
        required = []
        formulas = []
        for line in source_lines[start:]:
            line = line.strip()
            if not line or line.startswith(('<!--', '```')):
                continue
            if line.startswith('##'):
                required.append(re.sub(r'^\d+\s+(?:\d+\s+)?', '', line.lstrip('#').strip()))
            elif line.startswith('|'):
                cells = [c.strip() for c in line.strip('|').split('|')]
                if not all(re.fullmatch(r'[:\- ]+', c) for c in cells):
                    required.extend(B.display_text(c) for c in cells)
            elif line.startswith('$$ '):
                formulas.append(line[3:])
            elif line.startswith('!['):
                required.append(re.fullmatch(r'!\[(.*?)\]\((.*?)\)', line).group(1))
            else:
                required.append(B.display_text(line))
        missing = [t for t in required if t not in doc_text]
        assert not missing, missing
        result_formulas = [''.join(n.xpath('.//m:t/text()', namespaces=NS)) for n in final.findall('.//m:oMath', NS)]
        assert result_formulas == formulas, (len(result_formulas), len(formulas))
        for placeholder in ('lorem ipsum', 'CIG: 916', 'R.Vallarino', 'QUESTO FILE È UN MODELLO', 'Committente progetto'):
            assert placeholder not in doc_text
        headings = final.xpath('//w:p[w:pPr/w:pStyle[@w:val="Titolo1" or @w:val="Titolo2"]]', namespaces=NS)
        expected_headings = sum(line.startswith('##') for line in source_lines)
        assert len(headings) == expected_headings
        report = {
            'file': str(target), 'pdf_pages': len(pdf.pages), 'body_pages': body_pages,
            'template_sha256': sha256(B.TEMPLATE.read_bytes()).hexdigest(),
            'template_parts_preserved_byte_for_byte': preserved,
            'original_styles_preserved': len(old_styles.findall('w:style', NS)),
            'original_relationships_preserved': len(original_rels),
            'sections': len(final.findall('.//w:sectPr', NS)),
            'headings': expected_headings, 'content_fragments_verified': len(required),
            'editable_formulas_preserved': len(formulas),
            'technical_tables': len(final.findall('.//w:body/w:tbl', NS)) - 5,
            'missing_content': missing, 'output_sha256': sha256(target.read_bytes()).hexdigest(),
        }
        (qa / 'audit.json').write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf-8')
        print(f'{kind}: {len(pdf.pages)} pagine, {len(required)} frammenti e {len(formulas)} formule conservati; {len(preserved)} parti ITEC identiche.')
    assert sha256(B.TEMPLATE.read_bytes()).hexdigest() == B.TEMPLATE_HASH

if __name__ == '__main__':
    for kind in B.GUIDES:
        finalize(kind)
