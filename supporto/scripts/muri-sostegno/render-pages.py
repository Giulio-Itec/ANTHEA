from pathlib import Path
import sys
import pypdfium2 as pdfium

source, target = Path(sys.argv[1]), Path(sys.argv[2])
target.mkdir(parents=True, exist_ok=True)
doc = pdfium.PdfDocument(str(source))
for i, page in enumerate(doc):
    page.render(scale=1.2).to_pil().save(target / f'page-{i+1}.png')
print(f'Rendered {len(doc)} pages')
