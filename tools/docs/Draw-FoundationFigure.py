"""Original ANTHEA schematic: shared vector/raster coordinates, no engineering computation."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
from html import escape
ROOT=Path(__file__).resolve().parents[2]
out=ROOT/'X.Desktop/Assets/Wiki/fondazione-schema'
im=Image.new('RGB',(1200,450),'white');d=ImageDraw.Draw(im)
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',22)
svg=['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1200 450"><title>Fondazione superficiale convenzioni geometriche e azioni</title>']
def line(x1,y1,x2,y2,color='#334b66',width=2):
    d.line((x1,y1,x2,y2),fill=color,width=width);svg.append(f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{color}" stroke-width="{width}"/>')
def rect(x,y,w,h):
    d.rectangle((x,y,x+w,y+h),outline='#334b66',width=3);svg.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="none" stroke="#334b66" stroke-width="3"/>')
def text(x,y,s):
    d.text((x,y),s,font=font,fill='#153756');svg.append(f'<text x="{x}" y="{y+21}" font-family="Arial" font-size="22" fill="#153756">{escape(s)}</text>')
def arrow(x,y,dx,dy):
    line(x,y,x+dx,y+dy,'#0b5cad',3);line(x+dx,y+dy,x+dx-.17*dx-.10*dy,y+dy-.17*dy+.10*dx,'#0b5cad',3);line(x+dx,y+dy,x+dx-.17*dx+.10*dy,y+dy-.17*dy-.10*dx,'#0b5cad',3)
text(45,20,'SEZIONE X');text(705,20,'PIANTA');line(30,160,570,160);rect(185,185,240,65);rect(275,105,60,80)
arrow(305,40,0,100);text(330,78,'N');arrow(340,124,110,0);text(415,90,'Hx')
line(150,160,150,250);text(113,193,'D');line(185,289,425,289);text(293,300,'B')
rect(760,108,230,185);arrow(875,205,180,0);arrow(875,205,0,-130);text(1030,221,'X / Hx');text(895,70,'Y / Hy')
text(785,308,'B lungo X');text(1004,130,'L lungo Y');text(705,356,'My produce ex; Mx produce ey')
text(45,396,'Schema simbolico: base e piano campagna orizzontali. N positivo in compressione.')
svg.append('</svg>');out.with_suffix('.svg').write_text(''.join(svg),encoding='utf-8');im.save(out.with_suffix('.png'))
p=ROOT/'supporto/docs/guida-teorica-anthea.md';s=p.read_text(encoding='utf-8');marker='### Convenzioni e area efficace\n'
figure='\n![Fondazione superficiale e convenzioni delle azioni nei due assi. Schema originale ANTHEA.](../../X.Desktop/Assets/Wiki/fondazione-schema.svg)\n'
if 'fondazione-schema.svg' not in s:s=s.replace(marker,marker+figure)
p.write_text(s,encoding='utf-8',newline='\r\n')
