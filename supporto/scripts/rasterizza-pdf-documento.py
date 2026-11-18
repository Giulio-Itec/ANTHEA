"""Rende le pagine PDF conservando il bitmap PDFium fino al salvataggio PIL."""
from pathlib import Path
import argparse
import json
import subprocess
import sys
import pypdfium2 as pdfium

parser = argparse.ArgumentParser()
parser.add_argument('pdf', type=Path)
parser.add_argument('--out', type=Path, required=True)
parser.add_argument('--scale', type=float, default=1.5)
parser.add_argument('--render-page', type=int)
args = parser.parse_args()
args.out.mkdir(parents=True, exist_ok=True)
if args.render_page is not None:
    single = pdfium.PdfDocument(args.pdf)
    page = single[args.render_page-1]
    bitmap = page.render(scale=args.scale)
    pil = bitmap.to_pil()
    pil.save(args.out / f'page-{args.render_page:02d}.png')
    pil.close()
    bitmap.close()
    page.close()
    single.close()
    raise SystemExit(0)
pdf = pdfium.PdfDocument(args.pdf)
page_count = len(pdf)
pdf.close()
texts = []
for index in range(page_count):
    # Processo separato per isolare completamente la rasterizzazione di ogni pagina.
    subprocess.run([sys.executable, str(Path(__file__).resolve()), str(args.pdf),
                    '--out', str(args.out), '--scale', str(args.scale),
                    '--render-page', str(index+1)], check=True)
    pdf = pdfium.PdfDocument(args.pdf)
    page = pdf[index]
    textpage = page.get_textpage()
    text = textpage.get_text_range()
    texts.append({'page': index+1, 'text': text})
    textpage.close()
    page.close()
    pdf.close()
(args.out / 'pages-text.json').write_text(json.dumps(texts, ensure_ascii=False, indent=2), encoding='utf-8')
print('Pagine:',page_count)
for page in texts:
    lines=page['text'].splitlines()
    print(page['page'], ' | '.join(lines[:5]))
