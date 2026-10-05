"""Render delle pagine aggiornate delle guide globali e degli indici."""
from pathlib import Path
import json, re
from pypdf import PdfReader
import pypdfium2 as pdfium

root = Path(__file__).resolve().parents[3]
art = root / 'supporto/artefatti/palo-b450c/documenti'
art.mkdir(parents=True, exist_ok=True)
records = []
for kind, needle in [('pratica', 'neinuovifoglidelpalo'), ('teorica', 'inuovifoglidelpaloinizializzano')]:
    path = root / f'supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev22.pdf'
    reader = PdfReader(path)
    selected = [i for i, page in enumerate(reader.pages) if any(term in re.sub(r'\s+', '', page.extract_text() or '').lower() for term in [needle, 'ilpulsantedistintaferridiognitratto'])]
    assert selected, (kind, 'contenuto aggiornato assente')
    document = pdfium.PdfDocument(str(path))
    pages = sorted(set([0] + selected + [min(i + 1, len(reader.pages) - 1) for i in selected]))
    for i in pages:
        document[i].render(scale=1.5).to_pil().save(art / f'{kind}-{i + 1:03d}.png')
    assert path.read_bytes() == (root / f'supporto/docs/guida-{kind}-anthea.pdf').read_bytes()
    records.append(dict(file=str(path), pages=len(reader.pages), rendered=[i + 1 for i in pages]))
for kind, name in [('indice', 'installer/Indice-guide.pdf'), ('supporto', 'README.pdf')]:
    document = pdfium.PdfDocument(str(root / 'supporto' / name))
    for i in range(len(document)):
        document[i].render(scale=1.5).to_pil().save(art / f'{kind}-{i + 1:03d}.png')
(art / 'qa.json').write_text(json.dumps(records, indent=2), encoding='utf-8')
print(json.dumps(records))
