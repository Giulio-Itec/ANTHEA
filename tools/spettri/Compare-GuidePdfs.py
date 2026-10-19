"""Render every PDF page, identify layout changes and save changed pages for visual QA."""
import argparse
import json
from pathlib import Path

import pypdfium2 as pdfium
from PIL import ImageChops

parser = argparse.ArgumentParser()
parser.add_argument("--before", required=True, type=Path)
parser.add_argument("--after", required=True, type=Path)
parser.add_argument("--output", required=True, type=Path)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=False)
before = pdfium.PdfDocument(args.before)
after = pdfium.PdfDocument(args.after)
changed = []
for index in range(len(after)):
    current_page = after[index]
    current = current_page.render(scale=1).to_pil().convert("RGB")
    differs = index >= len(before)
    if not differs:
        old_page = before[index]
        old = old_page.render(scale=1).to_pil().convert("RGB")
        differs = old.size != current.size or ImageChops.difference(old, current).getbbox() is not None
        old_page.close()
    if differs:
        changed.append(index + 1)
        current_page.render(scale=1.5).to_pil().save(args.output / f"page-{index + 1:03}.png")
    current_page.close()
result = dict(before_pages=len(before), after_pages=len(after), changed_pages=changed)
(args.output / "comparison.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps(result))
before.close()
after.close()
