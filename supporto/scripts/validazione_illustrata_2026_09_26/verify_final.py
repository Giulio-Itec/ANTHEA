from pathlib import Path
from zipfile import ZipFile
from hashlib import sha256
from lxml import etree as E
import json,re
import pypdfium2 as pdfium

R=Path(__file__).resolve().parents[3]
A=R/'supporto/artefatti/validazione_illustrata_2026_09_26'
DOC=R/'supporto/documentazione/Validazione_CA_ANTHEA/ANTHEA_Validazione_Software_CA_e_Ponti_Rev03.docx'
TEMPLATE=Path('C:/Users/g.pacini/Desktop/MODELLO-RELAZIONE-ITEC-AA.docx')
W='http://schemas.openxmlformats.org/wordprocessingml/2006/main'
M='http://schemas.openxmlformats.org/officeDocument/2006/math'
ns={'w':W,'m':M}
with ZipFile(DOC) as z:
    assert z.testzip() is None
    root=E.fromstring(z.read('word/document.xml'))
    names=[x.get('{'+W+'}name') for x in root.findall('.//{'+W+'}bookmarkStart')]
    assert len(names)==len(set(names))
    names=set(names);refs=[];sequences={}
    for instr in root.findall('.//{'+W+'}instrText'):
        text=instr.text or ''
        m=re.search(r'\bREF\s+(\w+)',text)
        if m:refs.append(m[1])
        m=re.search(r'\bSEQ\s+(\w+)',text)
        if m:
            r=instr.getparent().getnext();cache=''
            while r is not None:
                if any(c.get('{'+W+'}fldCharType')=='end' for c in r.findall('.//{'+W+'}fldChar')):break
                cache+=''.join(t.text or '' for t in r.findall('.//{'+W+'}t'));r=r.getnext()
            sequences.setdefault(m[1],[]).append(int(cache))
    refs.extend(x.get('{'+W+'}anchor') for x in root.findall('.//{'+W+'}hyperlink') if x.get('{'+W+'}anchor'))
    assert not set(refs)-names,set(refs)-names
    for kind,values in sequences.items():assert values==list(range(1,len(values)+1)),kind
    cases=json.loads((A/'capture-input.json').read_text(encoding='utf8'))
    media={sha256(z.read(n)).hexdigest() for n in z.namelist() if n.startswith('word/media/')}
    for c in cases:
        candidates=[A/p/(c['id']+'.png') for p in ['screens-bridge','screens-anchors','screens-refined','screens-ca' if c['module']=='ca' else 'screens','screens']]
        file=next(p for p in candidates if p.exists())
        assert sha256(file.read_bytes()).hexdigest() in media,c['id']
        assert 'fig_'+c['id'].replace('-','_') in names
    with ZipFile(TEMPLATE) as source:
        preserved=[n for n in source.namelist() if n.startswith(('word/media/','word/header','word/_rels/header','word/theme/','word/font','word/numbering'))]
        assert all(source.read(n)==z.read(n) for n in preserved)
    counts={k:len(v) for k,v in sequences.items()}
    counts['native_math']=len(root.findall('.//{'+M+'}oMath'))

before=pdfium.PdfDocument(A/'final.pdf');after=pdfium.PdfDocument(A/'final_verified.pdf')
assert len(before)==len(after),(len(before),len(after))
for i in range(len(before)):
    a=before[i].get_textpage().get_text_range();b=after[i].get_textpage().get_text_range()
    assert a==b,('render text differs',i+1)
    assert not any(t in b for t in ['Error!','Errore. Il segnalibro','Errore. L’origine'])
report=dict(pages=len(after),counts=counts,screens_verified=len(cases),references_checked=len(refs),
            preserved_parts=len(preserved),template_sha256=sha256(TEMPLATE.read_bytes()).hexdigest(),
            document_sha256=sha256(DOC.read_bytes()).hexdigest(),render_text_identical=True)
(A/'final-integrity.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(report,ensure_ascii=False,indent=2))
