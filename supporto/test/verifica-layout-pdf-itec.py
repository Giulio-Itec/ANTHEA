"""Controlla la separazione fra il testo del corpo e il pie di pagina del PDF ITEC di prova."""
from pathlib import Path
import argparse
import json
import re
import pypdfium2 as pdfium

parser = argparse.ArgumentParser()
parser.add_argument('pdf', type=Path)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
document = pdfium.PdfDocument(str(args.pdf))
pages = []
for index in range(1, len(document)):
    page = document[index]
    textpage = page.get_textpage()
    text = textpage.get_text_range()
    footer = set()
    for match in re.finditer(r'Prova palo [—–-] template ITEC|Pagina\s+\d+', text):
        # The sheet title also occurs in chapter 1; only its footer occurrence belongs here.
        if textpage.get_charbox(match.start())[3] < 100:
            footer.update(range(match.start(), match.end()))
    if not footer:
        raise AssertionError(f'Pie di pagina assente nella pagina {index + 1}')
    body_boxes = []
    footer_boxes = []
    for i, char in enumerate(text):
        if char.isspace():
            continue
        box = textpage.get_charbox(i)
        if box[3] <= box[1]:
            continue
        (footer_boxes if i in footer else body_boxes).append(box)
    footer_top = max(b[3] for b in footer_boxes)
    body_bottom = min(b[1] for b in body_boxes)
    pages.append({'page': index + 1, 'body_bottom_pt': body_bottom,
                  'footer_top_pt': footer_top, 'gap_pt': body_bottom - footer_top})
    textpage.close()
    page.close()
result = {'pages': len(document), 'minimum_gap_pt': min(p['gap_pt'] for p in pages),
          'pass': all(p['gap_pt'] >= 5 for p in pages), 'details': pages}
args.output.write_text(json.dumps(result, indent=2), encoding='utf-8')
print(f"{'PASS' if result['pass'] else 'FAIL'}: {len(pages)} pagine di contenuto; distanza minima {result['minimum_gap_pt']:.1f} pt")
raise SystemExit(0 if result['pass'] else 1)
