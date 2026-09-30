"""Render every page of a PDF manifest, check text and record hashes for review."""
from pathlib import Path
import hashlib
import json
import sys
import pypdfium2 as pdfium
from pypdf import PdfReader
from PIL import Image, ImageDraw

manifest = Path(sys.argv[1]).resolve()
output = manifest.parent / 'qa-pdf'
output.mkdir(exist_ok=True)
jobs = json.loads(manifest.read_text(encoding='utf-8-sig'))
records, sheets, tiles = [], [], []
for n, job in enumerate(jobs, 1):
    source, pdf = Path(job['source']), Path(job['output'])
    assert source.is_file() and pdf.is_file(), job
    reader = PdfReader(pdf)
    texts = [p.extract_text() or '' for p in reader.pages]
    assert all(len(t.strip()) > 15 for t in texts), str(pdf)
    assert all('\ufffd' not in t for t in texts), str(pdf)
    folder = output / f'{n:02d}-{pdf.stem}'
    folder.mkdir(exist_ok=True)
    document = pdfium.PdfDocument(str(pdf))
    for index, page in enumerate(document):
        picture = page.render(scale=1.5).to_pil().convert('RGB')
        picture.save(folder / f'page-{index+1:03d}.png')
        picture.thumbnail((420, 598))
        tile = Image.new('RGB', (440, 625), 'white')
        tile.paste(picture, ((440-picture.width)//2, 25))
        ImageDraw.Draw(tile).text((6, 5), f'{n:02d} {pdf.stem[:32]} | p {index+1}', fill='black')
        tiles.append(tile)
        if len(tiles) == 8:
            sheet = Image.new('RGB', (1760, 1250), '#bcc5cd')
            for i, item in enumerate(tiles): sheet.paste(item, (i%4*440, i//4*625))
            target = output / f'panoramica-{len(sheets)+1:02d}.png'
            sheet.save(target); sheets.append(str(target)); tiles.clear()
    document.close()
    records.append({**job, 'pages':len(texts), 'render':str(folder), 'sha256':hashlib.sha256(pdf.read_bytes()).hexdigest()})
    print(f'{n:02d} | {len(texts)} pagine | {pdf.name}', flush=True)
if tiles:
    sheet = Image.new('RGB', (1760, 1250), '#bcc5cd')
    for i, item in enumerate(tiles): sheet.paste(item, (i%4*440, i//4*625))
    target = output / f'panoramica-{len(sheets)+1:02d}.png'
    sheet.save(target); sheets.append(str(target))
(output / 'indice.json').write_text(json.dumps({'pdfs':records, 'pages':sum(r['pages'] for r in records), 'contact_sheets':sheets}, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'Totale {len(records)} PDF, {sum(r["pages"] for r in records)} pagine, {len(sheets)} panoramiche', flush=True)
