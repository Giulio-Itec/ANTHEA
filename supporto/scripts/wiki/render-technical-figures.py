from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
root=Path(__file__).resolve().parents[3]
out=root/'X.Desktop/Assets/Wiki';out.mkdir(exist_ok=True)
x=np.linspace(1,2.5,300);B=450*300*.5*17/1000
fig=Image.new('RGB',(1700,690),'white');draw=ImageDraw.Draw(fig)
font=lambda n:ImageFont.truetype('C:/Windows/Fonts/arial.ttf',n)
for panel,(diameter,spacing) in enumerate([(8,200),(10,100)]):
    A=450*(2*np.pi*diameter**2/4)/spacing*(450/1.15)/1000
    steel=A*x;concrete=B*x/(1+x*x)
    opt=max(1,min(2.5,np.sqrt(max(0,B/A-1))))
    left=100+panel*840;top=110;w=670;h=430
    px=lambda v:left+(v-1)/1.5*w
    py=lambda v:top+h-v/750*h
    draw.text((left,25),f'Due rami Ø{diameter} ogni {spacing} mm',font=font(30),fill='#17232e')
    draw.text((left,70),'Resistenza [kN]',font=font(24),fill='#333333')
    for v in range(0,751,150):
        y=py(v);draw.line((left,y,left+w,y),fill='#dddddd',width=1);draw.text((left-55,y-14),str(v),font=font(20),fill='#444444')
    for v in [1,1.5,2,2.5]:draw.text((px(v)-14,top+h+12),str(v).replace('.',','),font=font(22),fill='#333333')
    draw.text((left+w-55,top+h+49),'cot θ',font=font(25),fill='#333333')
    for ys,color in [(steel,'#176f9e'),(concrete,'#b06619')]:draw.line([(px(a),py(b)) for a,b in zip(x,ys)],fill=color,width=5)
    governing=np.minimum(steel,concrete)
    for i in range(0,len(x)-3,8):draw.line([(px(a),py(b)) for a,b in zip(x[i:i+4],governing[i:i+4])],fill='#111111',width=4)
    gx=px(opt);gy=py(min(A*opt,B*opt/(1+opt**2)));draw.ellipse((gx-7,gy-7,gx+7,gy+7),fill='#111111')
    for j,(text,color) in enumerate([('Staffe','#176f9e'),('Puntone','#b06619'),('Minimo dei due rami','#111111')]):
        dx=left+j*220;draw.line((dx,635,dx+25,635),fill=color,width=4);draw.text((dx+35,619),text,font=font(20),fill=color)
fig.save(out/'taglio-traliccio.png')
print('Grafico taglio prodotto con formule e dati indipendenti')
