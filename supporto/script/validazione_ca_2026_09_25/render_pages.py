import pypdfium2 as pdfium
from pathlib import Path
from PIL import Image,ImageDraw
p=Path('supporto/artefatti/validazione_ca_2026_09_25/final_pages');p.mkdir(exist_ok=True)
d=pdfium.PdfDocument('supporto/artefatti/validazione_ca_2026_09_25/final.pdf')
print('PAGES',len(d))
texts=[]
for i,page in enumerate(d):
 page.render(scale=1.5).to_pil().save(p/f'page-{i+1}.png')
 t=page.get_textpage().get_text_range();texts.append(t)
 print(i+1,len(t),t[:95].replace('\r\n',' | '))
Path('supporto/artefatti/validazione_ca_2026_09_25/final_text.txt').write_text('\n\n'.join(f'PAGE {i+1}\n{t}' for i,t in enumerate(texts)),encoding='utf-8')
for start in range(0,len(d),8):
 sheet=Image.new('RGB',(1200,4*430),'#cccccc');draw=ImageDraw.Draw(sheet)
 for j in range(start,min(start+8,len(d))):
  img=Image.open(p/f'page-{j+1}.png');img.thumbnail((580,400)); x=((j-start)%2)*600;y=((j-start)//2)*430
  sheet.paste(img,(x,y+22));draw.text((x+8,y+5),str(j+1),fill='black')
 sheet.save(p/f'contact-{start+1}.png')
