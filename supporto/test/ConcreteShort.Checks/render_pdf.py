import sys
from pathlib import Path
import pypdfium2 as pdfium
source=Path(sys.argv[1])
doc=pdfium.PdfDocument(source)
assert len(doc)<=2, f"Short report has {len(doc)} pages"
for i,page in enumerate(doc):
    page.render(scale=1.6).to_pil().save(str(source.with_name(f"page-{i+1}.png")))
    print(f"Page {i+1}: {len(page.get_textpage().get_text_range())} characters")

