"""Render all exported PDF pages and check the archival manifest, without calculations."""
from pathlib import Path
import hashlib
import json
import pypdfium2 as pdfium
from pypdf import PdfReader
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[3]
QA=ROOT/'supporto/artefatti/riordino-documenti-20260929'
jobs=json.loads((QA/'pdf-manifest.json').read_text(encoding='utf-8'))
render=QA/'render'; render.mkdir(exist_ok=True)
results=[]; tiles=[]; sheets=[]
for n,job in enumerate(jobs,1):
    path=Path(job['output']); assert path.is_file(), path
    reader=PdfReader(path); texts=[p.extract_text() or '' for p in reader.pages]
    assert all(len(text.strip())>15 for text in texts), f'Pagina quasi vuota: {path}'
    assert not any('\ufffd' in t for t in texts), f'Carattere sostitutivo: {path}'
    label=f'{n:02d}-{path.parent.name}-{path.stem}'
    folder=render/label; folder.mkdir(exist_ok=True)
    doc=pdfium.PdfDocument(str(path))
    for i,page in enumerate(doc):
        im=page.render(scale=1.2).to_pil().convert('RGB'); im.save(folder/f'page-{i+1}.png')
        im.thumbnail((420,600))
        tile=Image.new('RGB',(440,630),'white'); tile.paste(im,((440-im.width)//2,26))
        ImageDraw.Draw(tile).text((8,5),f'{n:02d} | {path.stem[:40]} | p {i+1}',fill='black'); tiles.append(tile)
    results.append({'source':job['source'],'pdf':str(path),'pages':len(reader.pages),'render':str(folder),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
    print(f'{n:02d}: {len(reader.pages)} pagine | {path.name}')
    doc.close()
for start in range(0,len(tiles),8):
    group=tiles[start:start+8]; sheet=Image.new('RGB',(440*4,630*((len(group)+3)//4)),'#bcc5cd')
    for i,tile in enumerate(group):sheet.paste(tile,((i%4)*440,(i//4)*630))
    target=render/f'panoramica-{start//8+1:02d}.png'; sheet.save(target); sheets.append(str(target))
records=json.loads((ROOT/'supporto/SUPERATI/registro-20260929.json').read_text(encoding='utf-8-sig'))
for r in records:
    original=ROOT/'supporto'/r['original']; archived=ROOT/'supporto'/r['archived']
    assert not original.exists(), original
    assert archived.is_file(), archived
    assert hashlib.sha256(archived.read_bytes()).hexdigest().upper()==r['sha256'], archived
(QA/'verifica-documenti.json').write_text(json.dumps({'pdfs':results,'total_pages':sum(x['pages'] for x in results),'archived_files_verified':len(records),'contact_sheets':sheets},ensure_ascii=False,indent=2),encoding='utf-8')
print(f'PDF {len(results)}, pagine {sum(x["pages"] for x in results)}, file archiviati e verificati {len(records)}')
