import sys,json,math,copy
from pathlib import Path
import numpy as np
from types import SimpleNamespace
def brentq(f,lo,hi,xtol=1e-10):
 fl=f(lo)
 assert fl*f(hi)<0,(fl,f(hi))
 for i in range(100):
  mid=(lo+hi)/2;fm=f(mid)
  if abs(hi-lo)<xtol:return mid
  if fl*fm<=0:hi=mid
  else:lo=mid;fl=fm
 return mid
def root(f,x):
 x=np.array(x,dtype=float)
 for i in range(80):
  y=np.array(f(x));err=np.linalg.norm(y)
  if err<1e-9:return SimpleNamespace(success=True,x=x,fun=y)
  jac=np.column_stack([(np.array(f(x+np.eye(2)[j]*h))-y)/h for j,h in enumerate([1e-8,1e-5])])
  dx=np.linalg.solve(jac,-y)
  scale=1
  while scale>1e-5:
   candidate=x+dx*scale
   if np.linalg.norm(f(candidate))<err:break
   scale/=2
  x=candidate
 return SimpleNamespace(success=False,x=x,fun=np.array(f(x)))
import reference_base as rb
O=rb.ROOT; cases=[]; F=450/1.15; E=rb.EC; fct=.3*30**(2/3)
def inp(b=300,h=500,phi=20,n=2):
 bars=[dict(x=x,y=y,phi=phi) for y in (-h/2+50,h/2-50) for x in np.linspace(-b/2+50,b/2-50,n)]
 return dict(shape='Rettangolare',width_mm=b,height_mm=h,fck_mpa=30,fyk_mpa=450,alpha_cc=.85,gamma_c=1.5,gamma_s=1.15,steel_modulus_mpa=200000,steel_fu_mpa=540,steel_eps_u=75,cls_diagramma='Parabola-rettangolo',steel_diagramma='Elastoplastico',cover_mm=30,transverse_bar_diameter_mm=8,barre_manuali=bars)
def add(id,family,mode,params,expected,steps,ref,title='',input=None):
 cases.append(dict(id=id,family=family,mode='extra_'+mode,params=params,expected=expected,steps=steps,ref=ref,title=title or family,input=input or inp()))
def fmt(x):return f'{x:.6g}'.replace('.',',')
for diagram in ['Parabola-rettangolo','Bilineare','Stress block','Non lineare']:
 ey=.002 if diagram=='Parabola-rettangolo' else .00175 if diagram=='Bilineare' else .0007 if diagram=='Stress block' else .0007*38**.31
 for j,e in enumerate([ey/2,ey if diagram!='Stress block' else .002],1):
  if diagram=='Parabola-rettangolo':factor=1-(1-e/ey)**2;formula=f'17 [1 − (1 − {fmt(e)}/0,002)²]'
  elif diagram=='Bilineare':factor=min(e/ey,1);formula=f'17 min({fmt(e)}/0,00175; 1)'
  elif diagram=='Stress block':factor=0 if e<ey else 1;formula=f'17 × {factor}; soglia (1 − 0,8) × 0,0035 = 0,0007'
  else:
   eta=e/ey;K=1.05*E*ey/38;factor=(K*eta-eta*eta)/(1+(K-2)*eta);formula=f'17 × ({fmt(K)} × {fmt(eta)} − {fmt(eta)}²) / [1 + ({fmt(K)} − 2) × {fmt(eta)}]'
  data=inp();data['cls_diagramma']=diagram
  add(f'MC{["Parabola-rettangolo","Bilineare","Stress block","Non lineare"].index(diagram)+1}-{j:02}',f'Legame CLS {diagram}','material',dict(strain=-e),dict(stress=-17*factor),[f'fcd = 0,85 × 30 / 1,5 = 17 MPa; ε = −{fmt(e)}.',f'|σc| = {formula} = {fmt(17*factor)} MPa. Compressione con segno negativo.']+([f'εc1 = 0,0007 × 38^0,31 = {fmt(ey)}; Ecm = {fmt(E)} MPa; K = 1,05 Ecm εc1/38 = {fmt(K)}. I riferimenti sono valutati a εc1/2 e a εc1; il motore può interpolare la propria tabella materiale. La DLL scala la curva caratteristica con αcc/γc.'] if diagram=='Non lineare' else []),'R1 §4.1.2.1.2.1; R3 §3.1.7; R8 ConcreteMaterialEuropeanCommon',input=data)
for dia in ['Elastoplastico','Incrudente']:
 for j,e in enumerate([.001,.01],1):
  val=min(200000*e,F) if dia=='Elastoplastico' else (200000*e if e<F/200000 else F+(540-450)/(0.075-450/200000)*(e-450/200000))
  data=inp();data['steel_diagramma']=dia
  steps=[f'fyd = 450/1,15 = {fmt(F)} MPa; εyd = {fmt(F/200000)}; Es = 200000 MPa; ε = {e}; fu = 540 MPa; εu = 0,075.']
  steps += [f'σs = min(200000 × {e}; {fmt(F)}) = {fmt(val)} MPa.'] if dia=='Elastoplastico' else [f'εyk = 450/200000 = 0,00225; Et = (540 − 450)/(0,075 − 0,00225) = {fmt(90/.07275)} MPa.',f'σs = Es ε = {fmt(val)} MPa.' if e<F/200000 else f'Nel ramo adottato dalla DLL: σs = fyd + Et(ε − εyk) = {fmt(F)} + {fmt(90/.07275)}({e} − 0,00225) = {fmt(val)} MPa. Il ramo va confrontato con la convenzione del materiale nativo, non con un fu,d introdotto separatamente.']
  add(('MEP' if dia=='Elastoplastico' else 'MIN')+f'-{j:02}',f'Legame acciaio {dia}','material',dict(strain=e,steel=True),dict(stress=val),steps,'R1 §4.1.2.1.2.2; R3 §3.2.7; R8 SteelMaterial',input=data)
for shape in ['Rettangolare','Circolare']:
 for hole in [False,True]:
  for j in [1,2]:
   data=inp(400 if j==1 else 600,600 if j==1 else 800,20)
   if shape=='Rettangolare':
    b=data['width_mm'];h=data['height_mm'];bi=b-160 if hole else 0;hi=h-160 if hole else 0
    A=b*h-bi*hi;AP=A;steps=[f'A = {b} × {h} − {bi} × {hi} = {fmt(A)} mm².',f'Ix = ({b} × {h}³ − {bi} × {hi}³)/12 = {fmt((b*h**3-bi*hi**3)/12)} mm⁴.',f'Iy = ({h} × {b}³ − {hi} × {bi}³)/12 = {fmt((h*b**3-hi*bi**3)/12)} mm⁴. Baricentro (0;0).']
    if hole:data.update(foro_presente=True,inner_width_mm=bi,inner_height_mm=hi)
    sides=4
   else:
    D=600 if j==1 else 800;di=D-200 if hole else 0;sides=32 if j==1 else 64
    data.pop('barre_manuali');data.update(shape=shape,diameter_mm=D,circular_sides=sides,longitudinal_bar_count=8,longitudinal_bar_diameter_mm=20)
    if hole:data.update(foro_presente=True,inner_diameter_mm=di)
    A=math.pi*(D*D-di*di)/4;AP=sides/8*(D*D-di*di)*math.sin(2*math.pi/sides)
    steps=[f'Acerchio = π({D}² − {di}²)/4 = {fmt(A)} mm².',f'Contorni inscritti con n = {sides}; vertici (D/2 cos(2πi/n); D/2 sin(2πi/n)), i = 0…n−1; stesso schema per il foro.',f'Apoligono = n/8 (D²−di²) sin(2π/n) = {sides}/8 × ({D}²−{di}²) × sin(2π/{sides}) = {fmt(AP)} mm².',f'Scarto geometrico rispetto al cerchio = {fmt(100*(A-AP)/A)}%; Ix = Iy = π({D}⁴−{di}⁴)/64 = {fmt(math.pi*(D**4-di**4)/64)} mm⁴ per il cerchio ideale. Il verificatore riceve il poligono, mentre alcuni parametri geometrici di sintesi usano il cerchio ideale.']
   add(f'G{shape[0]}{"F" if hole else "P"}-{j:02}',f'Geometria {shape.lower()} {"forata" if hole else "piena"}','geometry',{},dict(area=A,polygonArea=AP,vertices=sides,holes=int(hole)),steps,'R8 CheckerSection.PrepareModel e geometria analitica',input=data)
# Anchorage and laps, two examples of each including a failing check.
for lap in [False,True]:
 for j in [1,2]:
  phi=20 if j==1 else 16;stress=400 if j==1 else 300;good=j==1;fctk=.7*fct;fbd=2.25*(1 if good else .7)*fctk/1.5;basic=phi*stress/4/fbd;perc=25 if j==1 else 100;alpha=max(1,min(1.5,math.sqrt(perc/25)));L=(alpha if lap else 1)*basic;L=max(L,20*phi,200 if lap else 150);avail=math.ceil(L/10)*10 if j==1 else 300;gap=20 if j==1 else 70
  add(('SO' if lap else 'AN')+f'-{j:02}','Sovrapposizione rettilinea' if lap else 'Ancoraggio rettilineo','anchor',dict(phi=phi,sigma=stress,fct=fctk,good=good,length=avail,lap=lap,percent=perc,gap=gap),dict(Fbd=fbd,BasicLength=basic,RequiredLength=L,Passed=avail>=L and(not lap or gap<=4*phi)),[f'fctm = 0,3 × 30^(2/3) = {fmt(fct)} MPa; fctk,0.05 = 0,7 fctm = {fmt(fctk)} MPa.',f'η1 = {1 if good else .7}; η2 = 1; fbd = 2,25 × η1 × η2 × {fmt(fctk)}/1,5 = {fmt(fbd)} MPa.',f'lb,rqd = {phi} × {stress}/(4 × {fmt(fbd)}) = {fmt(basic)} mm.',f'α1…α5 = 1. '+(f'ρ1 = {perc}%; α6 = min(1,5; max(1; √({perc}/25))) = {alpha}; l0 = max({fmt(alpha*basic)}; {fmt(.3*alpha*basic)}; {20*phi}; 200) = {fmt(L)} mm.' if lap else f'lbd = max({fmt(basic)}; 20 × {phi}; 150) = {fmt(L)} mm.'),f'Ldisponibile = {avail} mm: confronto {avail} ≥ {fmt(L)}.' ]+([f'Interferro = {gap} mm ≤ 4 × {phi} = {4*phi} mm: {"sì" if gap<=4*phi else "no"}.'] if lap else []),'R1 §§4.1.2.1.1.4, 4.1.2.3.10, 4.1.6.1.4; R3 §§8.4, 8.7; R4 §4.1')
for j,(ex,fck,plate,life,quality,table,low) in enumerate([('XC1',30,False,50,False,25,0),('XS3',30,True,100,True,40,5)],1):
 dur=table+(10 if life==100 else 0)+low-(5 if quality else 0);nom=max(10,20,dur)+10
 add(f'CD-{j:02}','Copriferro da esposizione SLE','cover',dict(exposure=ex,fck=fck,plate=plate,life=life,quality=quality),dict(dur=dur,nominal=nom),[f'Esposizione {ex}; fck = {fck} MPa; elemento {"a piastra" if plate else "monodimensionale"}; vita {life} anni; qualità {"con controllo copriferri" if quality else "ordinaria"}.',f'Tab. C4.1.IV: ctab = {table} mm. cmin,dur = {table} + {10 if life==100 else 0} + {low} − {5 if quality else 0} = {dur} mm.',f'Ø = 20 mm; dg = 20 mm; cmin,b = 20 mm; cnom = max(10; 20; {dur}) + 10 = {nom} mm. Modificare la classe SLE deve aggiornare questo valore senza un secondo input di esposizione.'],'R2 §C4.1.6.1.3 tab. C4.1.IV; R3 §4.4')
def shear(p):
 sc=min(-p.get('N',0)*1000/p['A'],.2*17);bw=p['bw'];d=p['d'];asw=p['Asw'];z=p.get('z',.9)*d;cot=p.get('cot',2);s=p.get('s',150);rho=min(.02,p['Asl']/bw/d);k=min(2,1+math.sqrt(200/d))
 if asw==0:
  a=(.12*k*(100*rho*30)**(1/3)+.15*sc)*bw*d/1000;b=(.035*k**1.5*math.sqrt(30)+.15*sc)*bw*d/1000;rd=max(a,b)
  steps=[f'ρl = min(0,02; {fmt(p["Asl"])}/({bw} × {d})) = {fmt(rho)}; k = min(2; 1 + √(200/{d})) = {fmt(k)}.',f'σcp = min(−1000 × {p.get("N",0)}/{fmt(p["A"])}; 0,2 × 17) = {fmt(sc)} MPa.',f'V1 = [0,18/1,5 × {fmt(k)} × (100 × {fmt(rho)} × 30)^(1/3) + 0,15 × {fmt(sc)}] × {bw} × {d}/1000 = {fmt(a)} kN.',f'V2 = [0,035 × {fmt(k)}^1,5 × √30 + 0,15 × {fmt(sc)}] × {bw} × {d}/1000 = {fmt(b)} kN; VRd = max(V1;V2) = {fmt(rd)} kN.']
 else:
  sc=max(0,-p.get('N',0)*1000/p['A']);ac=1+sc/17 if sc<=4.25 else 1.25 if sc<=8.5 else max(0,2.5*(1-sc/17))
  a=z*asw/s*F*cot/1000;b=z*bw*ac*.5*17*cot/(1+cot*cot)/1000;rd=min(a,b)
  steps=[f'σcp = {fmt(sc)} MPa; αc = {fmt(ac)}; z = {p.get("z",.9)} × {d} = {fmt(z)} mm; α = 90°; cotθ = {cot}.',f'VRsd = {fmt(z)} × {fmt(asw)}/{s} × {fmt(F)} × {cot}/1000 = {fmt(a)} kN.',f'VRcd = {fmt(z)} × {bw} × {fmt(ac)} × 0,5 × 17 × {cot}/(1+{cot}²)/1000 = {fmt(b)} kN; VRd = min(VRsd;VRcd) = {fmt(rd)} kN.']
 steps+=[f'ηV = |{p["V"]}|/{fmt(rd)} = {fmt(abs(p["V"])/rd)}; soddisfatta se ηV ≤ 1.']
 return dict(VRsd=a,VRcd=b,VRd=rd,Ratio=abs(p['V'])/rd),steps
for axis,bw,d in [('X',500,250),('Y',300,450)]:
 for staff in [False,True]:
  p=dict(N=-300,V=500,A=150000,bw=bw,d=d,Asl=2*math.pi*100,Asw=2*math.pi*16 if staff else 0)
  r,steps=shear(p);add(f'T{axis}-{4 if staff else 3:02}',f'Taglio {axis} {"con staffe" if staff else "senza staffe"}','shear',p,r,steps,'R1 §4.1.2.3.5')
for hole in [False,True]:
 for j in [1,2]:
  D=600;di=300 if hole else 0;A=math.pi*(D*D-di*di)/4;bw=D-di;d=450;v=100 if j==1 else 800
  p=dict(N=-200,V=v,A=A,bw=bw,d=d,Asl=1000,Asw=2*math.pi*25,s=150,cot=2,z=.60 if hole else .75)
  r,steps=shear(p);add(f'TC{"F" if hole else "P"}-{j:02}',f'Taglio circolare {"cavo" if hole else "pieno"} X e Y','shear',p,r,[f'D = 600 mm; di = {di} mm; A = π(600² − {di}²)/4 = {fmt(A)} mm²; bw = {bw} mm; d assegnato = 450 mm. Eseguire separatamente Vx = {v}, Vy = 0 e Vx = 0, Vy = {v} kN. Simmetria: identica resistenza.',f'Asw = 2 × π × 10²/4 = {fmt(p["Asw"])} mm². Modello esplicito delle pile, z/d = {p["z"]}; non è una regola generale del capitolo 4.']+steps,'R1 §§4.1.2.3.5 e 7.9.5.2')
for family in ['Torsione pura','Interazione taglio torsione']:
 for j in [1,2]:
  p=dict(Ak=100000,uk=1300,t=80,At=100,s=150,Al=1000,cot=1,T=20 if j==1 else 80,Vx=0 if family=='Torsione pura' else (80 if j==1 else 180),Vy=0)
  sx=dict(N=0,V=p['Vx'],A=240000,bw=300,d=450,Asl=1000,Asw=200,s=150,cot=1);sy=dict(sx,V=0)
  p.update(sx=sx,sy=sy);vx,_=shear(sx);vy,_=shear(sy)
  rc=2*p['Ak']*80*.5*17/2/1e6;rs=2*p['Ak']*100/150*F/1e6;rl=2*p['Ak']*1000/1300*F/1e6;rd=min(rc,rs,rl);et=p['T']/rd;ec=p['T']/rc+p['Vx']/vx['VRcd'];es=p['T']/rs+p['Vx']/vx['VRsd'];al=p['T']*1e6*1300/(2*100000*F)
  steps=[f'Ak = 100000 mm², uk = 1300 mm, t = 80 mm, At = 100 mm² (un braccio), s = 150 mm, Al = 1000 mm², cotθ = 1; TEd = {p["T"]} kNm; Vx = {p["Vx"]} kN; Vy = 0.',f'TRcd = 2 × 100000 × 80 × 0,5 × 17 × 1/(1+1²)/10⁶ = {fmt(rc)} kNm.',f'TRsd = 2 × 100000 × 100/150 × {fmt(F)} × 1/10⁶ = {fmt(rs)} kNm.',f'TRld = 2 × 100000 × 1000/1300 × {fmt(F)}/1/10⁶ = {fmt(rl)} kNm.',f'TRd = min({fmt(rc)}; {fmt(rs)}; {fmt(rl)}) = {fmt(rd)} kNm; ηT = {p["T"]}/{fmt(rd)} = {fmt(et)}.',f'Al,req = {p["T"]} × 10⁶ × 1300 × 1/(2 × 100000 × {fmt(F)}) = {fmt(al)} mm².']
  if family!='Torsione pura':steps+=['Per il taglio si assumono bw = 300, d = 450, z/d = 0,9, Asw = 200 mm², s = 150 mm; N = 0; stessa cotθ = 1.']+shear(sx)[1][:-1]
  steps += [f'ηc = T/TRcd + |Vx|/VRcd,x + |Vy|/VRcd,y = {fmt(p["T"]/rc)} + {fmt(p["Vx"]/vx["VRcd"])} + 0 = {fmt(ec)}.',f'ηs = T/TRsd + max(|Vx|/VRsd,x; |Vy|/VRsd,y) = {fmt(p["T"]/rs)} + {fmt(p["Vx"]/vx["VRsd"])} = {fmt(es)}. Tutti i tassi devono essere ≤ 1.']
  add(('TO' if family=='Torsione pura' else 'TV')+f'-{j:02}',family,'torsion',p,dict(TRcd=rc,TRsd=rs,TRld=rl,TRd=rd,TorsionRatio=et,ConcreteCombinedRatio=ec,SteelCombinedRatio=es,RequiredLongitudinalArea=al,Passed=max(et,ec,es)<=1),steps,'R1 §4.1.2.3.6 eq. 4.1.35–4.1.40; R3 §6.3')
# Full tension, formation and decompression.
for j,sigma in enumerate([100,300],1):
 Asteel=4*math.pi*100;N=Asteel*sigma/1000;rhoX=2*math.pi*100/(500*125);rhoY=2*math.pi*100/(300*125)
 widths=[rb.crack(sigma,200000,E,fct,rho,20,40,spacing,height,False,1) for rho,spacing,height in [(rhoX,400,300),(rhoY,200,500)]]
 expected=max(c['wk'] for c in widths);steps=[f'Sezione R, N = As σs/1000 = {fmt(Asteel)} × {sigma}/1000 = {fmt(N)} kN; Mx = My = 0. CLS teso escluso: ε = {sigma}/200000 = {fmt(sigma/200000)} uniforme; k2 = 1.']
 for face,rho,c,area,spacing,height in zip(['±x','±y'],[rhoX,rhoY],widths,[62500,37500],[400,200],[300,500]):
  steps += [f'Facce {face}: hc,eff = min(2,5 × 50; {height}/2) = 125 mm; Ac,eff = {area} mm²; As,eff = 2π20²/4 = 628,3185 mm²; ρ = {fmt(rho)}; s = {spacing} mm; soglia 5(40+10)=250 mm.',f'αe = {fmt(200000/E)}; riduzione = 0,4 × {fmt(fct)} × (1 + {fmt(200000/E)} × {fmt(rho)})/{fmt(rho)} = {fmt(c["stiffening"])} MPa.',f'Δε = max[({sigma} − {fmt(c["stiffening"])})/200000; 0,6 × {sigma}/200000] = {fmt(c["strain"])}.',f'Δsm,v = (3,4 × 40 + 0,8 × 1 × 0,425 × 20/{fmt(rho)})/1,7 = {fmt(c["near"])} mm; Δsm,l = 0,75 × {height} = {fmt(c["far"])} mm; ramo scelto Δsm = {fmt(c["distance"])}.',f'wk,{face} = 1,7 × {fmt(c["distance"])} × {fmt(c["strain"])} = {fmt(c["wk"])} mm.']
 add(f'FT-{j:02}','Fessurazione interamente tesa','stress',dict(N=N),dict(width=expected),steps+[f'wk = massimo fra le facce = {fmt(expected)} mm; wlim XC1 QP = 0,30 mm; ηw = {fmt(expected/.3)}. Non sommare le aree delle quattro facce.'],'R2 §C4.1.2.2.4.5; R3 §7.3.4 eq. 7.10')
for name,tag,ex,limit,set_ in [('Decompressione','DE','XD1',0,'SLE_QP'),('Formazione fessure','FF','XS3',fct/1.2,'SLE_FREQ')]:
 for j,sc in enumerate(([-1,1] if limit==0 else [1,3]),1):
  Aeq=150000+(200000/E-1)*4*math.pi*100;N=sc*Aeq/1000
  add(f'{tag}-{j:02}',name,'stress',dict(N=N,exposure=ex,sensitivity='Sensibile',set=set_,tensile='Sì'),dict(maxStress=sc,limit=limit,passed=sc<=limit),[f'Sezione R non fessurata, n = Es/Ecm = {fmt(200000/E)}; Aeq = 150000 + ({fmt(200000/E)} − 1) × 1256,637061 = {fmt(Aeq)} mm².',f'N = {fmt(N)} kN; Mx = My = 0; σc = 1000N/Aeq = {fmt(sc)} MPa uniforme.',f'Limite = '+('0 MPa' if limit==0 else f'fctm/1,2 = {fmt(fct)}/1,2 = {fmt(limit)} MPa')+f'; confronto {sc} ≤ {fmt(limit)}: {"soddisfatto" if sc<=limit else "non soddisfatto"}.',f'Combinazione {set_}, esposizione {ex}, armatura sensibile; il programma deve selezionare {name.lower()}, non un confronto wk/0.'],'R1 §§4.1.2.2.4.4–4.1.2.2.4.5 tab. 4.1.IV')
# Two additional stress checks to cover both rare and quasi permanent twice.
for j,(set_,sc) in enumerate([('SLE',20),('SLE_QP',12)],3):
 Aeq=150000+(200000/E-1)*4*math.pi*100;N=-sc*Aeq/1000;lim=18 if set_=='SLE' else 13.5
 add(f'VT-{j:02}','Limitazione tensionale '+set_,'stress',dict(N=N,set=set_),dict(sigma_cls=-sc,sigma_acciaio=sc*200000/E,Ratio=sc/lim),[f'Aeq = 150000 + (200000/{fmt(E)} − 1) × 1256,637061 = {fmt(Aeq)} mm².',f'N = −{sc} × {fmt(Aeq)}/1000 = {fmt(N)} kN; σc = −{sc} MPa; |σs| = {sc} × 200000/{fmt(E)} = {fmt(sc*200000/E)} MPa.',f'Limite CLS = {lim} MPa; ηc = {sc}/{lim} = {fmt(sc/lim)}.' ]+([f'Limite acciaio rara = 0,8 × 450 = 360 MPa; ηs = {fmt(sc*200000/E)}/360 = {fmt(sc*200000/E/360)}; governa ηc.'] if set_=='SLE' else ['La quasi permanente applica il limite del CLS, non il limite rara dell’acciaio.']),'R1 §4.1.2.2.5')
# Detail benchmark datasets; formulas and both cases for every leaf are generated in the document.
for kind,prefix,b,h in [('Beam','DT',300,500),('Column','DP',300,500),('Slab','DS',1000,200),('Wall','DW',1000,200)]:
 for j in [1,2]:
  phi=20 if kind in ['Beam','Column'] and j==1 else 12 if j==1 else 8
  data=inp(b,h,phi,5 if j==1 and kind in ['Slab','Wall'] else 2)
  p=dict(kind=kind,N=500 if j==1 else 2000,phiSt=8 if j==1 else 4,s=150 if j==1 else 400,secondary=600 if j==1 else 30,secondarySpacing=150 if j==1 else 500,critical=j==2,stirrups=kind!='Slab',dg=20,dur=25,dev=10)
  data.update(cover_mm=40 if j==1 else 10,transverse_bar_diameter_mm=p['phiSt'],transverse_spacing_mm=p['s'])
  add(f'{prefix}-{j:02}',f'Dettagli {kind}','detail',p,{},[f'b = {b} mm; h = {h} mm; {len(data["barre_manuali"])} barre Ø{phi}; coordinate riportate nella tabella seguente; copriferro nominale input = {data["cover_mm"]} mm.',f'NEd di compressione = {p["N"]} kN; staffe {"assenti" if kind=="Slab" else "a due bracci Ø"+str(p["phiSt"])+", passo "+str(p["s"])+" mm"}; dg = 20 mm; cmin,dur = 25 mm; Δcdev = 10 mm.',f'As ortogonale totale = {p["secondary"]} mm²/m; interasse ortogonale = {p["secondarySpacing"]} mm; zona {"critica" if p["critical"] else "ordinaria"}. I riscontri manuali sono lasciati da confermare.'],'R1 §4.1.6.1; R3 §§8.2,9.2,9.3,9.5,9.6',input=data)
for j in [1,2]:
 data=inp(300 if j==1 else 140,500 if j==1 else 140,20 if j==1 else 32)
 add(f'ZG-{j:02}','Armatura massima nella giunzione','detail',dict(kind='Column',N=500,phiSt=8,s=100,lap=True),{},['La sezione contiene tutte le barre materialmente presenti nella giunzione. La scelta zona di giunzione non raddoppia automaticamente As.'],'R3 §9.5.2; R1 §4.1.6.1.4',input=data)
# Reference for moment resistance in all four directions at two axial forces, each elastic/plastic.
def limit_at_N(sec,N,elastic):
 def pl(q):
  h=sec['h'];k=(.002 if elastic else .0035)/(h/2-q)
  if elastic:k=min(k,F/200000/max(abs(y-q) for x,y,d in sec['bars']))
  return [-k*q,0,k]
 q=brentq(lambda q:rb.response(sec,pl(q))['N']-N,-sec['h']/2+1e-5,sec['h']/2-1e-5,xtol=1e-10)
 return rb.response(sec,pl(q)),q
for elastic in [False,True]:
 for j,N in enumerate([0,-500],1):
  rx,qx=limit_at_N(rb.R,N,elastic);rot=dict(b=500,h=300,bars=[[y,x,d] for x,y,d in rb.R['bars']]);ry,qy=limit_at_N(rot,N,elastic)
  steps=[f'N assegnato = {N} kN; modalità {"elastica convenzionale" if elastic else "plastica"}; sezione R simmetrica. Risolvere N(q) − Nassegnato = 0 con bisezione.',f'Per Mx: q = {fmt(qx)} mm; e(y) = {fmt(rx["plane"][0])} + {fmt(rx["plane"][2])} y. Integrazione con le formule del capitolo Domini: Mx = {fmt(rx["Mx"])} kNm.',f'Per My ruotare il rettangolo: b = 500, h = 300, coordinate barre (±200;±100); q = {fmt(qy)} mm; e = {fmt(ry["plane"][0])} + {fmt(ry["plane"][2])} y; |My| = {fmt(ry["Mx"])} kNm.',f'Per simmetria Mx− = −Mx+ e My− = −My+. La ricerca ANTHEA non richiede la costruzione del dominio.']
  add(('RE' if elastic else 'RP')+f'-{j:02}','Resistenze rapide '+('elastiche' if elastic else 'plastiche'),'quick',dict(N=N,elastic=elastic),dict(Mx=rx['Mx'],My=ry['Mx']),steps,'R1 §4.1.2.3.4; R8 SectionMomentResistance')
for j,N in enumerate([0,-500],1):
 lim,q=limit_at_N(rb.R,N,False);M=lim['Mx'];trace=[]
 for frac in [0,.25,.5,.75,1]:
  if frac==0 and N==0:a=k=0
  elif frac==1:a,k=lim['plane'][0],lim['plane'][2]
  else:
   sol=root(lambda v:[(rb.response(rb.R,[v[0],0,v[1]*1e-5])['N']-N)/1000,(rb.response(rb.R,[v[0],0,v[1]*1e-5])['Mx']-M*frac)/100],[.0001,.1+frac]);a=sol.x[0];k=sol.x[1]*1e-5
   assert sol.success or np.linalg.norm(sol.fun)<1e-6
  rr=rb.response(rb.R,[a,0,k]);trace.append(dict(f=frac,N=rr['N'],M=rr['Mx'],a=a,k=k,chi=k*1000,sigma=rr['sigma_s']))
 add(f'MK-{j:02}','Diagramma momento curvatura','curve',dict(N=N,angle=0),dict(limit=M,trace=trace),[f'N = {N} kN, θ = 0°, 10 passi uniformi, frazione finale 1, tolleranza residuo N = 1 kN, 12 bisezioni per lo snervamento. CLS parabola rettangolo, acciaio elastoplastico, trazione CLS esclusa.',f'A ogni momento M = f × {fmt(M)} risolvere due equazioni: N(a,k) = {N} e Mx(a,k) = M, con e = a + ky. χ = 1000 |k| in 1/m. La tabella riporta i punti indipendenti da ricostruire; non è una discretizzazione imposta al programma.',f'Limite: emax = 0,0035; q = {fmt(q)} mm; k = 0,0035/(250 − {fmt(q)}) = {fmt(lim["plane"][2])} 1/mm; χu = {fmt(lim["plane"][2]*1000)} 1/m.'],'R2 §C4.1.2.3.4.2 fig. C4.1.12; R1 §4.1.2.3.4')

for family,prefix in [('Viscosità lineare','VI'),('Elemento gettato sottile','SP'),('Precompressione aderente','PC')]:
 for j in [1,2]:
  data=inp();As=4*math.pi*100;phi=j if prefix=='VI' else 0;Ec=E/(1+phi);Ap=150*j if prefix=='PC' else 0;Ep=195000;sig0=1000
  N=-500 if prefix!='SP' else (-1200 if j==1 else -2400)
  denom=Ec*(150000-As-Ap)+200000*As+Ep*Ap
  strain=(1000*N-Ap*sig0)/denom;sc=Ec*strain;ss=200000*strain;sp=sig0+Ep*strain
  if prefix=='SP':data['gettato_sottile']='Sì'
  params=dict(N=N,set='SLE',phi=phi)
  if prefix=='PC':params['tendons']=[dict(id='T01',x=0,y=0,area=Ap,Ep=Ep,fpyk=1670,fpk=1860,eps_u=35,sigma0=sig0,diagramma='Incrudente')]
  limit=.8*18 if prefix=='SP' else 18
  steps=[f'Sezione R; N = {N} kN; Mx = My = 0; As = {fmt(As)} mm²; φ = {phi}; Eeff = Ecm/(1+φ) = {fmt(Ec)} MPa.',
    f'Ap = {Ap} mm²; Ep = {Ep} MPa; σp0 = {sig0 if Ap else 0} MPa. K = Eeff(Ac−As−Ap) + EsAs + EpAp = {fmt(denom)} N.',
    f'ε = (1000N−Apσp0)/K = ({N*1000}−{Ap}×{sig0})/{fmt(denom)} = {fmt(strain)}.',
    f'σc = Eeff ε = {fmt(sc)} MPa; σs = Es ε = {fmt(ss)} MPa.']+([f'σp = σp0 + Epε = 1000 + 195000 × {fmt(strain)} = {fmt(sp)} MPa. La predeformazione σp0/Ep è assegnata al trefolo; nessuna perdita differita è introdotta.'] if Ap else [])+[f'Limite rara CLS = {limit} MPa; '+('riduzione 0,8 × 0,6 × 30 = 14,4 MPa per l’opzione di getto sottile.' if prefix=='SP' else 'limite acciaio ordinario = 0,8 × 450 = 360 MPa.')]
  add(f'{prefix}-{j:02}',family,'stress',params,dict(sigma_cls=sc,sigma_acciaio=max(abs(ss),abs(sp)) if Ap else abs(ss)),steps,'R1 §§4.1.2.2.5,4.1.8; R3 §§3.1.4,5.10; R8 convenzione predeformazione',input=data)
(O/'extra_reference.json').write_text(json.dumps(dict(cases=cases),ensure_ascii=False,indent=2),encoding='utf8')
print('EXTRA',len(cases))
