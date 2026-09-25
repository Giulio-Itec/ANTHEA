"""Riferimenti indipendenti: integrazione polinomiale a tratti, nessuna DLL ANTHEA."""
import json, math
from pathlib import Path
import numpy as np
ROOT=Path(__file__).resolve().parent
ES=200000.; FCK=30.; FCD=.85*FCK/1.5; FYD=450/1.15; EC=22000*((FCK+8)/10)**.3
R={'b':300.,'h':500.,'bars':[[-100,-200,20],[100,-200,20],[-100,200,20],[100,200,20]]}
W={'b':600.,'h':500.,'bars':[[-250,-200,20],[250,-200,20],[-250,200,20],[250,200,20]]}
def response(s,plane,law='parabola',order=6,net=True):
 a,b,c=plane; width,height=s['b'],s['h']; gx,gw=np.polynomial.legendre.leggauss(order)
 def strain(x,y):return a+b*x+c*y
 def cs(e):
  if law=='linear': return EC*np.maximum(e,0)
  t=np.clip(e/.002,0,1);return FCD*(2*t-t*t)
 cuts=[0] if law=='linear' else [0,.002]
 xs=[-width/2,width/2]
 if abs(b)>1e-20:
  for e in cuts:
   for y in [-height/2,height/2]:
    v=(e-a-c*y)/b
    if -width/2<v<width/2:xs.append(v)
 out=np.zeros(3)
 for xl,xh in zip(sorted(xs)[:-1],sorted(xs)[1:]):
  for x,wx in zip((xl+xh)/2+gx*(xh-xl)/2,gw*(xh-xl)/2):
   ys=[-height/2,height/2]
   if abs(c)>1e-20:
    for e in cuts:
     v=(e-a-b*x)/c
     if -height/2<v<height/2:ys.append(v)
   for yl,yh in zip(sorted(ys)[:-1],sorted(ys)[1:]):
    y=(yl+yh)/2+gx*(yh-yl)/2; f=cs(strain(x,y))*gw*(yh-yl)/2*wx
    out+=np.array([f.sum(),(f*y).sum(),(-f*x).sum()])
 stresses=[];strains=[]
 for x,y,d in s['bars']:
  e=strain(x,y); st=ES*e if law=='linear' else float(np.clip(ES*e,-FYD,FYD)); area=math.pi*d*d/4
  force=area*(st-(float(cs(e)) if net else 0));out+=force*np.array([1,y,-x]);stresses.append(-st);strains.append(-e*1000)
 corners=[strain(x,y) for x in [-width/2,width/2] for y in [-height/2,height/2]]
 return {'N':-out[0]/1000,'Mx':out[1]/1e6,'My':out[2]/1e6,'sigma_c':-float(max(cs(np.array(corners)))),'sigma_s':max(abs(v) for v in stresses),'bars':stresses,'eps_bars':strains,'plane':list(plane)}
def plane_domain(s,p,q,q0,elastic=False):
 top=abs(p)*s['b']/2+abs(q)*s['h']/2
 k=(.002 if elastic else .0035)/(top-q0)
 if elastic:k=min(k,FYD/ES/max(abs(p*x+q*y-q0) for x,y,d in s['bars']))
 return [-k*q0,k*p,k*q]
cases=[]
def add(id,family,title,s,plane,**kw):
 law=kw.pop('law','parabola');ref=response(s,plane,law);assert max(abs(ref[k]-response(s,plane,law,order=10)[k]) for k in ['N','Mx','My'])<1e-8
 cases.append(dict(id=id,family=family,title=title,section=s,reference=ref,law=law,**kw));return cases[-1]
add('P3D-01','3D plastico','Flessione deviata con asse neutro baricentrico',R,plane_domain(R,.6,1,0),mode='domain',state='SLU',dimension=3)
add('P3D-02','3D plastico','Pressoflessione deviata con asse neutro traslato',R,plane_domain(R,-.35,1,-60),mode='domain',state='SLU',dimension=3)
add('E3D-01','3D elastico','Limite convenzionale del calcestruzzo in flessione deviata',R,plane_domain(R,.6,1,0,True),mode='domain',state='SLV',dimension=3)
add('E3D-02','3D elastico','Primo snervamento di una barra in flessione deviata',R,plane_domain(R,-.35,1,120,True),mode='domain',state='SLV',dimension=3)
for id,q0 in [('P2D-01',100),('P2D-02',-50)]:add(id,'2D plastico','Pressoflessione retta con profondità compressa '+str(250-q0)+' mm',R,plane_domain(R,0,1,q0),mode='domain',state='SLU',dimension=2)
for id,q0 in [('E2D-01',0),('E2D-02',120)]:add(id,'2D elastico','Limite del calcestruzzo' if q0==0 else 'Primo snervamento dell’armatura tesa',R,plane_domain(R,0,1,q0,True),mode='domain',state='SLV',dimension=2)
A=R['b']*R['h'];AS=sum(math.pi*d*d/4 for x,y,d in R['bars']);eps=1e6/(EC*(A-AS)+ES*AS)
add('TL-01','Calcolo tensionale lineare','Compressione uniforme della sezione omogeneizzata',R,[eps,0,0],law='linear',mode='stress',set='SLE')
add('TL-02','Calcolo tensionale lineare','Sezione parzializzata in pressoflessione retta',R,[-80*10/(EC*170),0,10/(EC*170)],law='linear',mode='stress',set='SLE')
add('TN-01','Calcolo tensionale non lineare','Compressione uniforme sul ramo parabolico',R,[.001,0,0],mode='stress',set='SLE')
add('TN-02','Calcolo tensionale non lineare','Pressoflessione con calcestruzzo parzializzato',R,[-80*.0015/170,0,.0015/170],mode='stress',set='SLE')
add('VT-01','Verifica tensionale','Combinazione rara entro i limiti',R,[-80*16/(EC*170),0,16/(EC*170)],law='linear',mode='stress',set='SLE')
add('VT-02','Verifica tensionale','Combinazione quasi permanente oltre il limite',R,[-80*15/(EC*170),0,15/(EC*170)],law='linear',mode='stress',set='SLE_QP')
for v in cases:
 if v['mode']=='stress':v['reference']['ratio']=max(abs(v['reference']['sigma_c'])/18,v['reference']['sigma_s']/360) if v['set']=='SLE' else abs(v['reference']['sigma_c'])/13.5
def crack(sigma,es,ecm,fct,rho,phi,cover,spacing,tdepth,short,k2=.5):
 kt=.6 if short else .4;alpha=es/ecm;stiff=kt*fct/rho*(1+alpha*rho);de=max((sigma-stiff)/es,.6*sigma/es);near=(3.4*cover+.8*k2*.425*phi/rho)/1.7;far=.75*tdepth
 dist=near if spacing<=5*(cover+phi/2) else max(near,far)
 return dict(wk=1.7*dist*de,rho=rho,alpha=alpha,stiffening=stiff,strain=de,near=near,far=far,distance=dist,spacing_limit=5*(cover+phi/2))
args=[234,ES,ES/15,2.9,2712/(400*(600-237.8)/3),24,40,60,600-237.8,True]
cases.append(dict(id='FE-01',family='Fessurazione',title='Riproduzione del valore pubblicato ECP esempio 7 punto 3',mode='crack_scalar',args=args,reference=crack(*args)))
# N=0: equilibrio lineare con sottrazione del CLS in corrispondenza delle barre.
lo,hi=-249,249
for _ in range(80):
 q0=(lo+hi)/2;r=response(W,[-q0,0,1],'linear')
 if r['N']<0:lo=q0
 else:hi=q0
q0=(lo+hi)/2;k=250/ES/(200+q0)
v=add('FE-02','Fessurazione','Flessione semplice con barre distanziate e carico di lunga durata',W,[-q0*k,0,k],law='linear',mode='crack_full',set='SLE_QP')
tdepth=q0+250;hc=min(125,tdepth/3,250);rho=(2*math.pi*20**2/4)/(600*hc)
v['args']=[250,ES,EC,.3*30**(2/3),rho,20,40,500,tdepth,False];v['crack']=crack(*v['args']);v['crack'].update(hc=hc,aceff=600*hc,as_eff=2*math.pi*20**2/4,x=250-q0)
def shear(bw,d,asw,V):
 asl=2*math.pi*20**2/4;sig=2.;rho=min(.02,asl/(bw*d));k=min(2,1+math.sqrt(200/d));cot=2.;z=.9*d
 if asw==0:
  a=(.18/1.5*k*(100*rho*30)**(1/3)+.15*sig)*bw*d/1000;b=(.035*k**1.5*math.sqrt(30)+.15*sig)*bw*d/1000;rd=max(a,b)
 else:
  ac=1+sig/FCD;a=z*asw/150*FYD*cot/1000;b=z*bw*ac*.5*FCD*cot/(1+cot*cot)/1000;rd=min(a,b)
 return dict(branch1=a,branch2=b,VRd=rd,ratio=V/rd,rho=rho,k=k,sigma_cp=sig)
for ax,bw,d,V in [('X',500,250,100),('Y',300,450,180)]:
 for i,asw in enumerate([0,2*math.pi*8**2/4],1):
  load=50 if i==1 else V
  cases.append(dict(id=f'T{ax}-0{i}',family='Taglio '+ax,title='Sezione senza armatura resistente a taglio' if i==1 else 'Sezione con staffe verticali a due bracci',mode='shear',axis=ax,bw=bw,d=d,asw=asw,V=load,reference=shear(bw,d,asw,load)))
data=dict(materials=dict(Es=ES,Ecm=EC,fck=FCK,fcd=FCD,fyd=FYD,fctm=.3*30**(2/3)),cases=cases)
(ROOT/'reference.json').write_text(json.dumps(data,indent=2,ensure_ascii=False),encoding='utf-8')
for v in cases:print(v['id'],json.dumps(v['reference'],ensure_ascii=False))
