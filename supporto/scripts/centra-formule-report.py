"""Center existing display equations, preserving report text and all other package parts."""
import json
import sys
from pathlib import Path
from zipfile import ZipFile
from lxml import etree as E

source, output = map(Path, sys.argv[1:3])
output.mkdir(parents=True, exist_ok=True)
W = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'
M = 'http://schemas.openxmlformats.org/officeDocument/2006/math'
NS = {'w': W, 'm': M}
manifest = []
for path in source.glob('*.docx'):
    if path.name.startswith('~'):
        continue
    with ZipFile(path) as original:
        xml = E.fromstring(original.read('word/document.xml'))
        def text():
            return xml.xpath('//w:t/text() | //m:t/text()', namespaces=NS)
        before = text()
        count = 0
        for paragraph in xml.findall('.//w:p', NS):
            if not paragraph.findall('.//m:oMath', NS):
                continue
            properties = paragraph.find('w:pPr', NS)
            if properties is None:
                properties = E.Element(f'{{{W}}}pPr')
                paragraph.insert(0, properties)
            for alignment in properties.findall('w:jc', NS):
                properties.remove(alignment)
            E.SubElement(properties, f'{{{W}}}jc').set(f'{{{W}}}val', 'center')
            for equation in paragraph.findall('m:oMath', NS):
                index = paragraph.index(equation)
                paragraph.remove(equation)
                display = E.Element(f'{{{M}}}oMathPara')
                display.append(equation)
                paragraph.insert(index, display)
            for display in paragraph.findall('m:oMathPara', NS):
                properties = display.find('m:oMathParaPr', NS)
                if properties is None:
                    properties = E.Element(f'{{{M}}}oMathParaPr')
                    display.insert(0, properties)
                for alignment in properties.findall('m:jc', NS):
                    properties.remove(alignment)
                E.SubElement(properties, f'{{{M}}}jc').set(f'{{{M}}}val', 'center')
            count += 1
        assert text() == before, path.name
        with ZipFile(output / path.name, 'w') as target:
            for info in original.infolist():
                target.writestr(info, E.tostring(xml, xml_declaration=True, encoding='UTF-8', standalone=True)
                                if info.filename == 'word/document.xml' else original.read(info.filename))
    manifest.append({'file': path.name, 'formule_centrate': count})
(output.parent / 'documenti.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(manifest, ensure_ascii=False))
