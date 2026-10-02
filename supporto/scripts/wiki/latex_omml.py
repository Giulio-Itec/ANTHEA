"""Compile guide LaTeX to native editable Office Math using Office's MathML transform."""
from pathlib import Path
import sys
from functools import lru_cache
from lxml import etree

sys.path.insert(0, str(Path(__file__).with_name('vendor')))
from latex2mathml.converter import convert

@lru_cache(maxsize=1)
def transform():
    path = Path('C:/Program Files/Microsoft Office/root/Office16/MML2OMML.XSL')
    if not path.is_file(): raise RuntimeError('Trasformazione Office MathML mancante: '+str(path))
    return etree.XSLT(etree.parse(str(path)))

def office_math(latex):
    mathml = etree.fromstring(convert(latex).encode('utf-8'))
    result = transform()(mathml).getroot()
    return result
