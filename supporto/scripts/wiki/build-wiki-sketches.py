"""Original technical pen sketches, editable SVG plus packaged PNG, deterministic jitter."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import random, html, math
ROOT=Path(__file__).resolve().parents[3]; OUT=ROOT/'X.Desktop/Assets/Wiki'; OUT.mkdir(parents=True,exist_ok=True)
random.seed(6)
for name in ['beam-axes','beam-dof','beam-offset','beam-load']:
    im=Image.new('RGB',(1200,460),'#fffef9'); d=ImageDraw.Draw(im); svg=[]
    font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',26)
    def line(points, color='#374151', width=3):
        points=[(x*2+random.uniform(-1.2,1.2),y*2+random.uniform(-1.2,1.2)) for x,y in points]
        d.line(points,fill=color,width=width,joint='curve')
        svg.append('<polyline points="'+ ' '.join(f'{x:.1f},{y:.1f}' for x,y in points)+f'" fill="none" stroke="{color}" stroke-width="{width}"/>')
    def text(x,y,label):
        d.text((x*2,y*2),label,font=font,fill='#374151')
        svg.append(f'<text x="{x*2}" y="{y*2+26}" font-family="Segoe UI,sans-serif" font-size="26" fill="#374151">{html.escape(label)}</text>')
    def arrow(x1,y1,x2,y2):
        line([(x1,y1),(x2,y2)]); a=math.atan2(y2-y1,x2-x1)
        line([(x2-9*math.cos(a-.45),y2-9*math.sin(a-.45)),(x2,y2),(x2-9*math.cos(a+.45),y2-9*math.sin(a+.45))])
    if name=='beam-axes':
        line([(75,134),(493,128)],width=5); line([(75,134),(75,115),(493,109),(493,128)])
        text(60,155,'nodo i');text(475,155,'nodo j');arrow(190,83,350,83);text(265,58,'x locale')
        arrow(75,134,75,50);text(85,44,'y');arrow(75,134,35,177);text(23,180,'z')
        line([(78,196),(490,196)],width=2);text(275,199,'L');text(390,37,'asse della trave')
    elif name=='beam-dof':
        line([(80,104),(490,104)],width=4);line([(80,104),(145,122),(220,138),(295,143),(380,130),(490,104)],width=3)
        arrow(80,104,145,104);text(122,80,'u');arrow(80,104,80,44);text(42,39,'v')
        line([(62,122),(48,106),(49,84),(67,69),(91,70)]);arrow(91,70,102,77);text(15,88,'θ')
        text(185,174,'deformata amplificata');text(290,65,'spostamenti + rotazioni')
    elif name=='beam-offset':
        line([(90,145),(510,145)],width=5);line([(300,145),(300,89)],width=3)
        arrow(300,30,300,89);text(319,36,'F');line([(320,94),(320,143)],width=2);text(330,114,'e')
        text(370,165,'M = F · e');text(75,165,'asse di riferimento');text(345,76,'punto di applicazione')
    else:
        line([(75,111),(505,111)],width=4)
        for x in range(85,510,35): arrow(x,52,x,105)
        text(260,17,'q = 25 kN/m');line([(75,115),(60,141),(90,141),(75,115)]);line([(505,115),(490,141),(520,141),(505,115)])
        line([(75,155),(505,155)],width=2);text(250,156,'L = 8 m')
        for x in (497,513):
            d.ellipse((x*2-6,146*2-6,x*2+6,146*2+6),outline='#374151',width=2)
            svg.append(f'<circle cx="{x*2}" cy="292" r="6" fill="none" stroke="#374151" stroke-width="2"/>')
        line([(75,182),(150,199),(230,212),(290,218),(355,211),(435,197),(505,182)]);text(335,173,'Mmax = 200 kNm')
    im.save(OUT/f'{name}.png')
    (OUT/f'{name}.svg').write_text('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1200 460"><rect width="1200" height="460" fill="#fffef9"/>'+''.join(svg)+'</svg>',encoding='utf-8')
print('4 schemi tecnici originali PNG + SVG')
