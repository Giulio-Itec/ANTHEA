"""Render the entire generated editions; record text coverage and the pages added by this task."""
from pathlib import Path
import json
import pypdfium2 as pdfium
from pypdf import PdfReader
ROOT=Path(__file__).resolve().parents[2]
out=ROOT/'supporto/artefatti/fondazioni-superficiali/guide-qa';out.mkdir(parents=True,exist_ok=True)
report={}
for kind in ['pratica','teorica']:
    file=ROOT/f'supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev33.pdf'
    reader=PdfReader(file);texts=[p.extract_text() for p in reader.pages]
    # The new chapter is the final chapter. Locate it by its unique opening paragraph, not the TOC.
    first=next(i for i,t in enumerate(texts) if ('Il foglio Geotecnica' in t and 'Fondazioni superficiali' in t) if kind=='pratica') if kind=='pratica' else next(i for i,t in enumerate(texts) if 'Il modello valuta il carico limite' in t)
    dest=out/kind;dest.mkdir(exist_ok=True);doc=pdfium.PdfDocument(file)
    for i,page in enumerate(doc):page.render(scale=1.5).to_pil().save(dest/f'page-{i+1:03}.png')
    report[kind]={'pages':len(texts),'new_chapter_pages':list(range(first+1,len(texts)+1)),'text_chars':sum(map(len,texts))}
    (dest/'text.txt').write_text('\n\n'.join(texts),encoding='utf-8')
(out/'pages.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))
