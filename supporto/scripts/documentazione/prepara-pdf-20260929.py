"""Prepare PDF exports from existing evidence without running engineering tests."""
from pathlib import Path
import importlib.util
import json

ROOT=Path(__file__).resolve().parents[3]
QA=ROOT/'supporto/artefatti/riordino-documenti-20260929'
QA.mkdir(parents=True,exist_ok=True)
spec=importlib.util.spec_from_file_location('markdown_pdf',Path(__file__).with_name('markdown-pdf.py'))
converter=importlib.util.module_from_spec(spec); spec.loader.exec_module(converter)
examples=ROOT/'supporto/artefatti/stabilita-globale/regressione-tangenti-max/esempi'
word=list((ROOT/'supporto/documentazione/Guide_ANTHEA').glob('*Rev04.docx'))+list(examples.glob('*/relazione-anthea.docx'))
markdown=[ROOT/'supporto/artefatti/stabilita-globale/confronti-max/SOMMARIO-PARZIALE.md', ROOT/'supporto/docs/muri-sostegno.md',ROOT/'supporto/artefatti/stabilita-globale/DOCUMENTI-CORRENTI.md',ROOT/'supporto/SUPERATI/README.md']+list(examples.glob('*/riproduzione.md'))
assert len(word)==12 and len(markdown)==14
jobs=[{'source':str(p),'output':str(p.with_suffix('.pdf'))} for p in word]
(QA/'word-jobs.json').write_text(json.dumps(jobs,ensure_ascii=False,indent=2),encoding='utf-8')
for source in markdown: converter.build(source)
all_jobs=jobs+[{'source':str(p),'output':str(p.with_suffix('.pdf'))} for p in markdown]
(QA/'pdf-manifest.json').write_text(json.dumps(all_jobs,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'{len(markdown)} PDF da Markdown pronti; {len(word)} documenti Word da esportare.')
