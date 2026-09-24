import pypdfium2 as pdfium
from pathlib import Path
p=Path('tmp/validazione_ca/template_pages');p.mkdir(exist_ok=True)
d=pdfium.PdfDocument('tmp/validazione_ca/template.pdf')
print('PAGES',len(d))
for i,page in enumerate(d):
 page.render(scale=1).to_pil().save(p/f'page-{i+1}.png')
 print(i+1,page.get_textpage().get_text_range()[:100].replace('\r\n',' '))
