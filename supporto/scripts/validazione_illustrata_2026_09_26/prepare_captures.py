from pathlib import Path
import json,copy,math
R=Path(__file__).resolve().parents[3]
A=R/'supporto/artefatti/validazione_illustrata_2026_09_26';A.mkdir(exist_ok=True)
(A/'screens').mkdir(exist_ok=True)
old=R/'supporto/artefatti/validazione_ca_2026_09_25'
base=json.loads((old/'reference.json').read_text(encoding='utf8'))['cases']
extra=json.loads((old/'extra_reference.json').read_text(encoding='utf8'))['cases']
cases=[]
def default():return copy.deepcopy(extra[0]['input'])
def finish(v,i,w,mode,tab,n=0,m=0,my=0,set='SLU',note=''):
    d={'input':i,'versione_sezione':2,'workspace_ca':w,'combinazioni':{k:[] for k in ['SLU','SLV','SLE','SLE_FREQ','SLE_QP']}}
    d['combinazioni'][set]=[{'id':v['id'],'nome':v['id'],'azioni':[n,m,my]}] if mode in ['domain','stress','detail','quick','curve'] else []
    cases.append(dict(id=v['id'],module='ca',mode=mode,tab=tab,state=set,set=set,data=d,note=note,steel=v.get('params',{}).get('steel',False)))
def workspace():return {'versione':2,'normativa':'NTC 2018','sle_comuni':{'modello':'Lineare','phi':'0','trazione_cls':'No','assi':'Locali','esposizione':'XC1','sensibilita':'Poco sensibile','durata':'Lunga','aderenza':'Migliorata'},'trefoli':[]}
def shear(i,w,p,id,axis='y'):
    asw=p.get('Asw',0);i['staffe_presenti']='Sì' if asw else 'No';i['transverse_bar_diameter_mm']=math.sqrt(asw*2/math.pi) if asw else 8;i['transverse_spacing_mm']=p.get('s',150)
    w['taglio']={'modello':'Con staffe' if asw else 'Senza staffe','parametri':'Manuali','modello_circolare':'Parametri assegnati','z_d':p.get('z',.9),'azioni':[{'id':id,'nome':id,'N':p.get('N',-300),'Vx':p.get('V',0) if axis=='x' else 0,'Vy':p.get('V',0) if axis=='y' else 0,'T':0}]}
    for ax in ['x','y']:
        bw=p['bw'] if ax==axis else i.get('height_mm',500) if ax=='x' else i.get('width_mm',300)
        depth=p['d'] if ax==axis else i.get('width_mm',300)-50 if ax=='x' else i.get('height_mm',500)-50
        for key,val in [('bw',bw),('d',depth),('asl',p.get('Asl',628.3185307179587)),('cot',p.get('cot',2)),('alpha',90),('rami',2)]:w['taglio'][key+'_'+ax]=val
for v in base:
    i=default();w=workspace();mode=v['mode'];r=v['reference'];note=''
    if 'section' in v:
        sec=v['section'];i.update(width_mm=sec['b'],height_mm=sec['h'],steel_fu_mpa=450,steel_eps_u=100,transverse_bar_diameter_mm=10,barre_manuali=[dict(x=b[0],y=b[1],phi=b[2]) for b in sec['bars']])
    if mode=='domain':
        set=v['state'];tab=1 if v['dimension']==3 else 2
        for key in ['dominio3d','dominio2d']:w[key]=dict(stato=set,angoli='64',criterio='Eccentricità costante',trazione_cls='No',assi='Locali',strategia='Iterativo',tipo='N–M',theta='0',N='0')
        finish(v,i,w,'domain',tab,r['N'],r['Mx'],r['My'],set)
    elif mode in ['stress','crack_full']:
        w['sle_comuni']['modello']='Lineare' if v['law']=='linear' else 'Non lineare'
        finish(v,i,w,'stress',3,r['N'],r['Mx'],r['My'],v.get('set','SLE_QP'))
    elif mode=='shear':
        p=dict(N=-300,V=v['V'],bw=v['bw'],d=v['d'],Asw=v['asw']);shear(i,w,p,v['id'],v['axis'].lower());finish(v,i,w,'shear',4)
    else:
        i.update(width_mm=400,height_mm=600);w['sle_comuni'].update(n_armature='15',copriferro_fessure='40',spaziatura_fessure='60',durata='Breve')
        finish(v,i,w,'stress',3,0,100,0,'SLE_QP','Vista del modulo di fessurazione. FE 01 è un test scalare con σs assegnata, non una soluzione globale riprodotta dalla schermata; fanno fede i dati e il confronto numerico del testo.')
for v in extra:
    i=copy.deepcopy(v['input']);w=workspace();p=v['params'];mode=v['mode'];note='';tab=0;screen='geometry';n=p.get('N',0);set=p.get('set','SLE_QP')
    if mode=='extra_material':
        screen='material';note=f"Vista del legame costitutivo del materiale. Il punto ε = {p['strain']:.9g} è verificato dalla funzione di libreria nel testo; non è selezionato nel grafico dell’editor."
    elif mode=='extra_anchor':
        screen='anchor';tab=5
        w['ancoraggi']=dict(tipo='Sovrapposizione rettilinea' if p['lap'] else 'Ancoraggio rettilineo',diametro=p['phi'],sigma=p['sigma'],aderenza='Buona' if p['good'] else 'Altre condizioni',lunghezza=p['length'],percentuale=p['percent'],interferro=p['gap'])
        if p['sigma']>450/1.15:note='Il test scalare assegna σsd = 400 MPa. L’interfaccia del B450C con γs = 1,15 rifiuta il valore perché supera fyd = 391,304 MPa: la schermata documenta il controllo di ammissibilità, non il risultato della funzione scalare.'
    elif mode=='extra_cover':
        screen='cover';tab=5;w['sle_comuni']['esposizione']=p['exposure'];w['dettagli_costruttivi']=dict(elemento='Soletta piena' if p['plate'] else 'Trave',vita_durabilita=p['life'],qualita_copriferro='Sì' if p['quality'] else 'No')
    elif mode in ['extra_shear','extra_torsion']:
        screen='shear';tab=4;ps=p.get('sx',p);shear(i,w,ps,v['id'],'x' if v['id'].startswith('TX') else 'y')
        if v['id'].startswith(('TCP','TCF')):
            i.update(shape='Circolare',diameter_mm=600,circular_sides=32,longitudinal_bar_count=8,longitudinal_bar_diameter_mm=20);i.pop('barre_manuali',None)
            if v['id'].startswith('TCF'):i.update(foro_presente=True,inner_diameter_mm=300)
            for ax in ['x','y']:w['taglio']['bw_'+ax]=p['bw'];w['taglio']['d_'+ax]=p['d']
            note='Vista del modello circolare con 32 lati. Il confronto scalare nel testo assume l’area circolare analitica; la vista usa la geometria poligonale di ANTHEA.'
        if mode=='extra_torsion':
            i.update(width_mm=400,height_mm=600,barre_manuali=[dict(x=x,y=y,phi=28) for x in [-150,150] for y in [-250,250]])
            w['taglio'].update(bw_x=600,d_x=350,bw_y=300,d_y=450)
            w['taglio'].update(cot_torsione=p['cot'],as_torsione=p['Al'],chiusura_torsione='Confermato');w['taglio']['azioni'][0].update(Vx=p['Vx'],Vy=p['Vy'],T=p['T'])
            note='Vista del modulo taglio e torsione con le azioni del caso. Il test di libreria assegna Ak, uk e t direttamente; la schermata li ricava dalla geometria. I risultati non costituiscono una replica del test scalare.'
    elif mode=='extra_detail':
        screen='detail';tab=5;set='SLU';n=-n;i.update(staffe_presenti='Sì' if p.get('stirrups',True) else 'No',transverse_bar_diameter_mm=p.get('phiSt',8),transverse_spacing_mm=p.get('s',150))
        w['dettagli_costruttivi']=dict(elemento={'Beam':'Trave','Column':'Pilastro','Slab':'Soletta piena','Wall':'Parete'}[p['kind']],aggregato=p.get('dg',20),delta_c=p.get('dev',10),zona_sovrapposizione='Sì' if p.get('lap') else 'No',as_secondaria=p.get('secondary',0),passo_secondaria=p.get('secondarySpacing',150),zona_critica='Sì' if p.get('critical') else 'No')
        note='Vista dei dettagli con la disposizione dell’esempio. Il test scalare assegna cmin,dur; il modulo lo ricava dall’esposizione XC1. Confrontare separatamente i controlli di durabilità.'
    elif mode=='extra_quick':screen='quick';w['resistenze_rapide']=dict(N=n,tipo='Elastico' if p['elastic'] else 'Plastico')
    elif mode=='extra_curve':screen='curve';tab=6;w['momento_curvatura']=dict(N=n,theta=p['angle'],passi=10,frazione=1,angoli=64,tolleranza_n=1,raffina_snervamento=12,trazione_cls='No',campionamento='Quadratico')
    elif mode=='extra_stress':
        screen='stress';tab=3;w['trefoli']=p.get('tendons',[])
        w['sle_comuni'].update(phi=p.get('phi',0),esposizione=p.get('exposure','XC1'),sensibilita=p.get('sensitivity','Poco sensibile'),durata=p.get('duration','Lunga'),trazione_cls=p.get('tensile','No'))
    finish(v,i,w,screen,tab,n,p.get('Mx',0),p.get('My',0),set,note)
# Bridge inputs are merged onto production defaults by the capture entry point.
def phase(name,kind='Solo acciaio',n=0,m=0,phi=0,psi=1,eps=0):return dict(nome=name,tipo=kind,attiva=True,N=n,Mx=m,V=0,q_conn=0,epsilon_cs=eps,modo='Da φ',phi=phi,psi=psi,n=18,riferimento_N='Quota comune')
def bridge(id,method,phases,curve=None,note='',**kw):
    d=dict(methodIndex=method,classe4=False,fibre_anima=32,fibre_flange=4,fibre_cls=16,sottopassi=4,fasi=phases,**kw)
    if curve:d['curve_sezione']=curve
    cases.append(dict(id='P-'+id,module='bridge',mode='bridge',data=d,curve=bool(curve),note=note))
for method in range(3):
    for j,(n,m) in enumerate([(-200,100),(150,-80)],1):bridge(f'EL{method}-{j}',method,[phase('Carico',n=n,m=m)])
for method in [1,2]:
    for j,m in enumerate([100,-80],1):
        bridge(f'GET{method}-{j}',method,[phase('Prima del getto',m=m),phase('Getto senza incremento','Composta')])
        bridge(f'SC{method}-{j}',method,[phase('Carico',m=m),phase('Scarico',m=-m)])
    bridge(f'RIT-{method}',method,[phase('Ritiro '+str(j),'Ritiro',psi=.55,eps=-1) for j in range(1,25)])
yc=-1058.0281090289608
for family in ['MK','NE','PL']:
    for j,sign in enumerate([-1,1],1):
        q=dict(originIndex=1,typeIndex=0 if family=='MK' else 1,fase=0,punti=4,sottopassi=2,y=yc,n_storico=False,N=-50,k_storico=False,k=0,incremento_k=sign*.00001,incremento_e=(3000 if j==1 else 4000) if family=='PL' else sign*10)
        bridge(f'{family}-{j}',2,[phase('Origine')],q,**({'fy_override':True,'fy':235} if family=='PL' else {}))
for j,phi in enumerate([.5,2],1):bridge(f'HOM-{j}',1,[phase('Composta','Composta',-200,100,phi)])
five=[phase('G1',n=-100,m=1500),phase('G2','Composta',-200,2000,2,1.1),phase('R1','Ritiro',phi=1,psi=.55,eps=-100),phase('Q','Composta',50,-500),phase('R2','Ritiro',phi=2,psi=.55,eps=-200)]
for m in [0,1]:bridge('CINQUE-'+str(m),m,five,rebars_top=False,rebars_bottom=False)
(A/'capture-input.json').write_text(json.dumps(cases,ensure_ascii=False,indent=2),encoding='utf8')
(A/'artifact.md').write_text((R/'supporto/artefatti/validazione_integrata_2026_09_26/artifact.md').read_text(encoding='utf8')+'\nRev03: riferimenti incrociati Word, formule LaTeX convertite in OMML, tabelle più leggibili, viste WPF collegate ai casi. Consentiti nuovi stili di formula e didascalie numerate. Conservare risultati e limiti della Rev02; le viste della build corrente non sostituiscono le evidenze numeriche datate.\n',encoding='utf8')
print(len(cases),'casi')
