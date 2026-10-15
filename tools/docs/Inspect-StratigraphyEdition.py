"""Render Rev46 and the sample data sheet; keep all QA outputs outside version control."""
from pathlib import Path
import json
from pypdf import PdfReader
from pdf2image import convert_from_path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'supporto/artefatti/stratigrafia/pdf-qa'
OUT.mkdir(parents=True, exist_ok=True)
files = {kind: ROOT / f'supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev46.pdf' for kind in ('pratica', 'teorica')}
files['scheda'] = ROOT / 'supporto/artefatti/ci/20261015-211822-stratigrafia/Application.Tests/stratigrafia.pdf'
files['indice'] = ROOT / 'supporto/installer/Indice-guide.pdf'
files['readme'] = ROOT / 'supporto/documentazione/Guide_ANTHEA/README.pdf'
result = {}
for kind, path in files.items():
    reader = PdfReader(path)
    texts = [page.extract_text() or '' for page in reader.pages]
    directory = OUT / kind
    directory.mkdir(exist_ok=True)
    pages = convert_from_path(path, dpi=90, output_folder=directory, fmt='png', paths_only=True, output_file='page', thread_count=2)
    for start in range(0, len(pages), 20):
        contact = Image.new('RGB', (1250, 1800), 'white')
        draw = ImageDraw.Draw(contact)
        for offset, filename in enumerate(pages[start:start + 20]):
            page = Image.open(filename).convert('RGB')
            page.thumbnail((240, 330))
            x, y = offset % 5 * 250, offset // 5 * 450
            contact.paste(page, (x, y + 22))
            draw.text((x + 5, y + 3), f'{kind} {start + offset + 1}', fill='black')
        contact.save(directory / f'contact-{start + 1:03}.png')
    relevant = [i + 1 for i, text in enumerate(texts) if 'Il modulo autonomo Stratigrafia' in text or 'Il modulo stratigrafico rappresenta' in text]
    for number in relevant:
        convert_from_path(path, dpi=150, first_page=number, last_page=min(number + 1, len(pages)), output_folder=directory, fmt='png', output_file='detail', paths_only=True)
    result[kind] = {'pages': len(pages), 'new_chapter_pages': relevant, 'text_characters': sum(map(len, texts))}
(OUT / 'pages.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(result))
