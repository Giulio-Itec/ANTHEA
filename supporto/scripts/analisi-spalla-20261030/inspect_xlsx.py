"""Read OOXML formulas and cached values without changing the source workbook."""
import json
import sys
import zipfile
from pathlib import Path
import xml.etree.ElementTree as ET

source = Path(sys.argv[1])
output = source.parent
ns = {'m': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
rel_ns = '{http://schemas.openxmlformats.org/package/2006/relationships}'
doc_rel = '{http://schemas.openxmlformats.org/officeDocument/2006/relationships}'
with zipfile.ZipFile(source) as z:
    strings = []
    if 'xl/sharedStrings.xml' in z.namelist():
        strings = [''.join(si.itertext()) for si in ET.fromstring(z.read('xl/sharedStrings.xml'))]
    wb = ET.fromstring(z.read('xl/workbook.xml'))
    rels = {r.attrib['Id']: r.attrib['Target'] for r in ET.fromstring(z.read('xl/_rels/workbook.xml.rels'))}
    sheets = []
    for s in wb.find('m:sheets', ns):
        target = rels[s.attrib[doc_rel + 'id']]
        target = target.lstrip('/') if target.startswith('/') else 'xl/' + target
        xml = ET.fromstring(z.read(target))
        cells = []
        for c in xml.findall('.//m:sheetData/m:row/m:c', ns):
            v = c.find('m:v', ns)
            f = c.find('m:f', ns)
            value = v.text if v is not None else None
            if c.get('t') == 's' and value is not None:
                value = strings[int(value)]
            elif c.get('t') == 'inlineStr':
                value = ''.join(c.find('m:is', ns).itertext())
            if value is not None or f is not None:
                cells.append({'cell': c.get('r'), 'value': value, 'formula': f.text if f is not None else None,
                              'formula_attributes': f.attrib if f is not None else None, 'type': c.get('t'), 'style': c.get('s')})
        sheets.append({'name': s.get('name'), 'state': s.get('state', 'visible'), 'cells': cells,
                       'merges': [r.get('ref') for r in xml.findall('.//m:mergeCell', ns)]})
    result = {'source': str(source), 'sheets': sheets,
              'names': [n.attrib | {'value': n.text} for n in wb.findall('.//m:definedName', ns)],
              'calculation': wb.find('m:calcPr', ns).attrib if wb.find('m:calcPr', ns) is not None else {},
              'external_links': [n for n in z.namelist() if n.startswith('xl/externalLinks/')],
              'media': [n for n in z.namelist() if n.startswith('xl/media/')],
              'errors': [{'sheet': s['name'], **c} for s in sheets for c in s['cells'] if c['type'] == 'e']}
    (output / 'workbook-data.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    lines = []
    for s in sheets:
        lines.append('\n### ' + s['name'])
        for c in s['cells']:
            lines.append(c['cell'] + ' | ' + str(c['value']) + (' | =' + str(c['formula']) if c['formula_attributes'] is not None else ''))
    (output / 'workbook-cells.txt').write_text('\n'.join(lines), encoding='utf-8')
    for media in result['media']:
        (output / Path(media).name).write_bytes(z.read(media))
    print(json.dumps({k: v for k, v in result.items() if k != 'sheets'}, ensure_ascii=False, indent=2))
    print(json.dumps([{'name': s['name'], 'cells': len(s['cells']), 'formulas': sum(c['formula_attributes'] is not None for c in s['cells'])} for s in sheets], ensure_ascii=False, indent=2))
