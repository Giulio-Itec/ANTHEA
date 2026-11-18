"""Typesetting delle cinque formule PDF, corrispondenti agli originali OMML.
Solo composizione tipografica: nessun calcolo della sezione. Python del runtime
documenti, Pillow e font Cambria di Windows. PNG a 300 dpi, carattere 12 pt.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
TARGET = ROOT / 'src/ANTHEA.Reports/Templates/QuickSection'
TARGET.mkdir(parents=True, exist_ok=True)

def text(value, size=50, italic=True):
    font = ImageFont.truetype('C:/Windows/Fonts/' + ('cambriai.ttf' if italic else 'cambria.ttc'), size)
    box = font.getbbox(value, anchor='ls')
    image = Image.new('RGB', (max(1, int(font.getlength(value)) + 6), box[3] - box[1] + 6), 'white')
    baseline = 3 - box[1]
    ImageDraw.Draw(image).text((3, baseline), value, font=font, fill='black', anchor='ls')
    return image, baseline

def join(*parts):
    baseline = max(p[1] for p in parts)
    image = Image.new('RGB', (sum(p[0].width for p in parts), max(baseline-p[1]+p[0].height for p in parts)), 'white')
    x = 0
    for part, base in parts:
        image.paste(part, (x, baseline-base)); x += part.width
    return image, baseline

def sub(value, index):
    a, base = text(value); b, bbase = text(index, 35)
    y = base + 13 - bbase
    image = Image.new('RGB', (a.width+b.width, max(a.height,y+b.height)), 'white')
    image.paste(a,(0,0)); image.paste(b,(a.width,y))
    return image, base

def fraction(numerator, denominator):
    n, _ = numerator; d, _ = denominator; width = max(n.width,d.width)+14
    line = n.height+8; top = line+10
    image = Image.new('RGB', (width,top+d.height), 'white')
    image.paste(n,((width-n.width)//2,0)); image.paste(d,((width-d.width)//2,top))
    ImageDraw.Draw(image).line((2,line,width-3,line),fill='black',width=2)
    return image, line+16

equations = {
    'eta': text('η ≤ 1'),
    'ductility': join(sub('ε','s'), text(' ≥ '), sub('ε','yd'), text(' :  comportamento duttile',italic=False)),
    'n': join(sub('n','0'),text(' = '),fraction(sub('E','s'),sub('E','cm'))),
    'stress': join(text('|'),sub('σ','c,comp'),text('| ≤ '),sub('σ','c,lim'),text(' ;  '),sub('σ','s'),text(' ≤ '),sub('σ','s,lim')),
    'crack': join(fraction(sub('w','k'),sub('w','lim')),text(' ≤ 1')),
}
for name,(formula,_) in equations.items():
    image=Image.new('RGB',(formula.width+16,formula.height+16),'white'); image.paste(formula,(8,8))
    image.save(TARGET/(name+'.png'),dpi=(300,300))
    print(name,image.size)
