"""Original deterministic figures. SVG is the UI source; matching PNG is the Word rendition."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import math,html
OUT=Path(__file__).resolve().parents[3]/'X.Desktop/Assets/Wiki'
class Figure:
    def __init__(self,name,title,height=310):
        self.name=name;self.height=height;self.svg=[f'<title>{html.escape(title)}</title>'];self.im=Image.new('RGB',(1440,height*2),'white');self.d=ImageDraw.Draw(self.im)
    def line(self,points,color='currentColor',width=2,dash=False):
        self.svg.append('<polyline points="'+' '.join(f'{x:.2f},{y:.2f}' for x,y in points)+f'" fill="none" stroke="{color}" stroke-width="{width}"'+(' stroke-dasharray="6 4"' if dash else '')+'/>')
        self.d.line([(x*2,y*2) for x,y in points],fill='#0B2A4A' if color=='currentColor' else color,width=max(1,int(width*2)))
    def text(self,x,y,text,size=16):
        self.svg.append(f'<text x="{x}" y="{y}" font-size="{size}" fill="currentColor">{html.escape(text)}</text>')
        self.d.text((x*2,(y-size-3)*2),text,font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',size*2),fill='#0B2A4A')
    def arrow(self,x,y,a,b,color='#0B5CAD'):
        self.line([(x,y),(a,b)],color);t=math.atan2(b-y,a-x)
        self.line([(a-8*math.cos(t-.45),b-8*math.sin(t-.45)),(a,b),(a-8*math.cos(t+.45),b-8*math.sin(t+.45))],color)
    def box(self,x,y,w,h):self.line([(x,y),(x+w,y),(x+w,y+h),(x,y+h),(x,y)])
    def circle(self,x,y,r):
        self.svg.append(f'<circle cx="{x}" cy="{y}" r="{r}" fill="none" stroke="currentColor" stroke-width="1.5"/>')
        self.d.ellipse(((x-r)*2,(y-r)*2,(x+r)*2,(y+r)*2),outline='#0B2A4A',width=3)
    def support(self,x,y,roller=False):
        self.line([(x,y),(x-12,y+18),(x+12,y+18),(x,y)])
        self.line([(x-18,y+26 if roller else y+20),(x+18,y+26 if roller else y+20)])
        if roller:
            for q in [x-6,x+6]:self.circle(q,y+22,2)
    def save(self):
        (OUT/f'{self.name}.svg').write_text(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 720 {self.height}">'+''.join(self.svg)+'</svg>',encoding='utf-8')
        self.im.save(OUT/f'{self.name}.png')

f=Figure('bridge-notebook','Taccuino di un viadotto a due campate',280)
f.line([(25,220),(125,195),(210,222),(295,230),(410,223),(590,180),(695,205)],'#9AA9B9',1)
f.line([(80,130),(615,130),(647,101),(111,101),(80,130),(80,146),(615,146),(647,117),(647,101)])
f.line([(615,130),(615,146)])
for x in [94,344,597]:
    f.box(x,146,20,60 if x==344 else 42);f.box(x-15,206 if x==344 else 188,50,13)
    for dx in [0,12,24]:f.line([(x-10+dx,220 if x==344 else 202),(x-20+dx,237 if x==344 else 219)],'#9AA9B9',1)
f.text(270,67,'IMPALCATO',18);f.line([(316,74),(320,109)],width=1)
f.text(382,194,'pila');f.line([(377,187),(366,180)],width=1)
f.text(40,264,'spalla');f.line([(85,248),(102,180)],width=1)
f.text(465,258,'fondazione');f.line([(470,242),(370,214)],width=1)
for x in range(93,613,24):f.line([(x,129),(x+31,101)],'#9AA9B9',.8)
f.save()
f=Figure('bridge-load-path','Trasferimento verticale del carico agli appoggi',300)
f.box(95,95,530,20)
for x in [140,240,340,440,540]:f.arrow(x,40,x,90)
for x in [145,575]:
    f.support(x,115,x==575);f.box(x-12,151,24,75);f.box(x-45,226,90,22);f.arrow(x+35,163,x+35,221);f.arrow(x,178,x,139)
f.text(242,30,'carichi verticali sull’impalcato');f.text(270,171,'appoggi → pile → terreno');f.text(257,265,'reazioni del terreno verso l’alto')
for x in [115,145,175,545,575,605]:f.arrow(x,288,x,251)
f.save()
f=Figure('euler-mode','Asta ideale compressa e primo modo',310)
f.line([(150,60),(150,242)],dash=True);f.line([(150+55*math.sin(math.pi*i/60),60+182*i/60) for i in range(61)],'#0B5CAD',3)
f.support(150,242);f.line([(150,60),(132,48),(132,72),(150,60)]);f.line([(118,39),(118,81)])
f.circle(125,53,4);f.circle(125,67,4)
f.arrow(150,4,150,52);f.arrow(150,301,150,268)
f.text(170,28,'N');f.text(173,290,'N');f.text(219,149,'v(x)')
f.line([(104,60),(104,242)]);f.line([(98,60),(110,60)]);f.line([(98,242),(110,242)]);f.text(73,158,'L')
f.text(370,80,'Cerniera + carrello assiale',20);f.text(370,120,'Traslazione laterale impedita');f.text(370,151,'Rotazione libera ai due estremi');f.text(370,198,'Linea tratteggiata: asse iniziale');f.text(370,230,'Linea blu: primo modo')
f.save()
f=Figure('euler-curve','Tensione critica rispetto alla snellezza',325)
f.arrow(75,265,665,265);f.arrow(75,265,75,30);f.text(20,23,'σcr [MPa]');f.text(500,315,'λ = L₀/i [−]')
for lam in [60,100,140,180]:
    x=75+(lam-50)/150*550;f.line([(x,265),(x,270)]);f.text(x-12,293,str(lam),14)
for stress in [0,200,400,600]:
    y=265-stress/800*220;f.line([(69,y),(75,y)]);f.text(26,y+5,str(stress),14)
f.line([(75+(l-50)/150*550,265-(math.pi**2*210000/l**2)/800*220) for l in range(52,201)],'#0B5CAD',3)
y=265-355/800*220;f.line([(75,y),(640,y)],'#9AA9B9',1,True);f.text(340,y-10,'fy = 355 MPa · limite elastico',14)
f.text(330,52,'Euler ideale · E = 210000 MPa',16);f.save()
f=Figure('beam-benchmark','Trave appoggiata e diagramma del momento',325)
f.line([(90,105),(635,105)],width=4)
for x in range(100,636,53):f.arrow(x,47,x,99)
f.support(90,105);f.support(635,105,True);f.text(270,31,'q = 25 kN/m')
f.arrow(90,181,90,140);f.arrow(635,181,635,140);f.text(15,208,'100 kN');f.text(600,208,'100 kN');f.text(315,153,'L = 8 m')
f.line([(90,230),(635,230)],width=1);f.line([(90+545*i/80,230+62*4*(i/80)*(1-i/80)) for i in range(81)],'#0B5CAD',3)
f.text(122,215,'M [kNm] · positivo sotto la linea base',14);f.text(316,319,'200 kNm');f.text(91,251,'0',13);f.text(625,251,'0',13);f.save()
f=Figure('crack-transfer','Armatura continua in tirante fessurato',270)
f.box(100,70,510,95);f.line([(75,138),(640,138)],'#0B5CAD',5)
for x in [240,465]:f.line([(x,70),(x-6,95),(x+5,113),(x-3,139),(x,165)],width=2)
f.arrow(100,138,36,138);f.arrow(610,138,681,138)
f.line([(240,190),(465,190)]);f.line([(240,182),(240,199)]);f.line([(465,182),(465,199)])
f.text(295,219,'distanza tra fessure');f.text(115,35,'calcestruzzo teso');f.text(400,35,'armatura aderente continua')
f.line([(491,42),(530,134)],width=1);f.text(103,258,'La fessura interrompe il calcestruzzo; la barra resta continua.',15);f.save()
f=Figure('footing','Sezione di una fondazione nastriforme',325)
f.line([(40,90),(275,90)]);f.line([(445,90),(680,90)]);f.box(275,125,170,30);f.box(337,50,46,75)
f.arrow(360,6,360,48);f.text(397,33,'V [kN/m]')
for x in range(280,446,27):f.arrow(x,200,x,158)
f.text(469,176,'pressione di contatto');f.line([(495,180),(440,187)],width=1)
f.line([(275,238),(445,238)]);f.line([(275,231),(275,245)]);f.line([(445,231),(445,245)]);f.text(350,266,'B [m]')
f.line([(224,90),(224,155)]);f.line([(218,90),(230,90)]);f.line([(218,155),(230,155)]);f.text(154,125,'D [m]')
for x in range(110,651,33):f.line([(x,295),(x+38,270)],'#9AA9B9',1)
f.text(70,316,'Terreno omogeneo · sezione per metro di sviluppo longitudinale',15);f.save()
print('7 SVG originali e relative versioni PNG')
