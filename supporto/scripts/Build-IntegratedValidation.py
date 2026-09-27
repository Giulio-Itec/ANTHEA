"""Rev02: retain the ITEC concrete dossier and regenerate the bridge dossier from current evidence."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import json, math, hashlib
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'supporto/artefatti/validazione_integrata_2026_09_26'
BASE=ROOT/'supporto/documentazione/Validazione_CA_ANTHEA/ANTHEA_Validazione_Calcestruzzo_Armato_Rev01.docx'
TARGET=ROOT/'supporto/documentazione/Validazione_CA_ANTHEA/ANTHEA_Validazione_Software_CA_e_Ponti_Rev02.docx'
source=(ROOT/'supporto/scripts/Build-BridgeValidation.py').read_text(encoding='utf8')
source=source.replace('ART = ROOT / "supporto/artefatti/ponte_curve_validazione"','ART = ROOT / "supporto/artefatti/validazione_integrata_2026_09_26"')
source=source.replace('ART / "plot-runtime"','ROOT / "supporto/artefatti/ponte_curve_validazione/plot-runtime"')
start=source.index('doc = Document()'); end=source.index('def p(text, style=None)')
source=source[:start]+'''doc = Document(BASE)
doc.core_properties.title="Validazione del software ANTHEA per sezioni in calcestruzzo armato e ponti composti"
doc.core_properties.subject="Revisione 02 del 26 settembre 2026 con aggiornamento dei metodi per i ponti"
doc.core_properties.author=""; doc.core_properties.last_modified_by=""
for text in doc._element.xpath('.//w:t'):
    if text.text == 'Validazione del calcolo delle sezioni in calcestruzzo armato': text.text='Validazione del software ANTHEA'
    elif text.text == 'Software ANTHEA  |  NTC 2018 e Circolare 2019': text.text='Calcestruzzo armato e sezioni composte da ponte'
for section in doc.sections:
    for para in section.footer.paragraphs:
        for run in para.runs:
            if 'ANTHEA Validazione CA' in run.text: run.text=run.text.replace('ANTHEA Validazione CA','ANTHEA Validazione software')
for t in doc.tables[:5]:
    for row in t.rows:
        for cell in row.cells:
            if cell.text.startswith('88 esempi numerici'):
                cell.text='88 esempi CA e validazione dei ponti composti\\nCalcoli espliciti e confronti indipendenti\\nMetodi cumulativo e storici e curve di risposta'
for para in doc.paragraphs:
    if para.style.name == "Title":
        para.text="Validazione del software ANTHEA per sezioni in calcestruzzo armato e ponti composti"
        break

# Find cover revision table by its existing revision text, without changing numerical tables.
for t in doc.tables[:5]:
    for row in t.rows:
        if any('REVISIONE COMPLETA' in c.text for c in row.cells):
            for c,value in zip(row.cells,['02','INTEGRAZIONE PONTI','26/09/2026','','','']): c.text=value
for para in doc.paragraphs:
    if para.text.startswith('La relazione documenta la validazione numerica del modulo ANTHEA'):
        para.text='La relazione raccoglie la validazione delle sezioni in calcestruzzo armato e delle sezioni composte da ponte di ANTHEA. La parte CA conserva gli 88 esempi e gli esiti della campagna del 25 settembre 2026. La parte ponti è aggiornata alle DLL del 26 settembre 2026, con esempi analitici, confronti OpenSees e prove di integrazione. Gli esiti CA non sono una nuova esecuzione sulle DLL aggiornate.'
    if para.text.startswith('L’ambito è il modulo di sezione in c.a.'):
        para.text=para.text.replace('L’ambito è il modulo di sezione in c.a., non gli altri moduli di ANTHEA né l’analisi globale della struttura.', 'I capitoli CA riguardano il modulo di sezione in c.a.; i successivi capitoli trattano separatamente i ponti composti. Non è compresa l’analisi globale della struttura.')
doc.add_page_break()
''' + source[end:]
# Fonts and repeated table headings follow the ITEC dossier.
source=source.replace('Pt(9.3)','Pt(10.5)').replace('"E5EBF1"','"E7E6E6"').replace('"F5F7F9"','"FFFFFF"')
source=source.replace('"Subtitle"','"Normal"')
source=source.replace('def page(title): doc.add_page_break(); doc.add_heading(title, level=1)', 'def page(title):\n    heading=doc.add_heading(title, level=1); heading.paragraph_format.page_break_before=True')
source=source.replace('doc.add_heading("Validazione dei metodi di calcolo della sezione composta da ponte",0)','doc.add_heading("Validazione delle sezioni composte da ponte",1)')
source=source.replace('Revisione 01   ·   25 settembre 2026','Aggiornamento ponti del 26 settembre 2026')
source=source.replace('I casi dei difetti noti delle vecchie DLL sono eseguiti separatamente: {bugs[\'failed\']} non superati e {known_skipped} ignorati.', 'La campagna separata sui difetti precedenti ha {bugs[\'passed\']} casi superati, {bugs[\'failed\']} non superati e {known_skipped} ignorati.')
source=source.replace('ART/"ui-finale/curve_momento_curvatura.png"','ROOT/"supporto/artefatti/ponte_curve_validazione/ui-finale/curve_momento_curvatura.png"')
source=source.replace('ART/"ui-05/curve_momento_curvatura.png"','ROOT/"supporto/artefatti/ponte_curve_validazione/ui-05/curve_momento_curvatura.png"')
source=source.replace('Scheda delle curve in ANTHEA. Il cursore','Schermata della precedente campagna del 25 settembre, non acquisita nuovamente in questa revisione. Il cursore')
source=source.replace('La suite WPF del modulo controlla','La suite WPF eseguita nella precedente campagna controllava')
a=source.index('page("Difetti aperti e approssimazioni")'); b=source.index('p("Le tensioni disegnate',a)
source=source[:a]+'''page("Aggiornamento dei difetti e approssimazioni")
p("La nuova esecuzione distingue le correzioni dimostrate dai casi ancora esclusi. I quattro test del costruttore con acciaio nullo sono superati. Anche ZeroCompositeActionShouldReportSameIntegrationInertiaAsNonzeroAction è ora superato nella suite ordinaria di integrazione.")
table(["Controllo","Esito attuale","Evidenza"],[
["Costruttore con acciaio nullo","4 superati","Constructor_NullSteelShouldHonorDocumentedContract"],
["Inerzia indipendente dal carico nullo","Superato","Suite BridgeAudit ordinaria"],
["Riferimento ruotato","3 ignorati","Non verificato in questa campagna"],
["Acciaio immerso nel CLS","4 ignorati","Non verificato in questa campagna"],
["Asse neutro globale","3 ignorati","Non verificato in questa campagna"]],[5,3,9])
h("Due piastre inferiori")
p("L’implementazione attuale conserva le due piastre reali nel calcolo delle proprietà e nello storico e ne tratta separatamente gli sbalzi efficaci. Superano la nuova campagna TwoBottomPlatesFollowNavierOnTheRealPlates, TwoBottomPlatesAreSeparateOutstands e TwoBottomPlatesInTheHistoryMethod. La precedente descrizione di un unico rettangolo equivalente è superata per questi percorsi. Il caso parametrico e le formule di area, baricentro e inerzia sono riportati nei test sorgente.")
''' + source[b:]
source=source.replace('ANTHEA/supporto/artefatti/ponte_curve_validazione','ANTHEA/supporto/artefatti/validazione_integrata_2026_09_26')
source=source.replace('ANTHEA/supporto/scripts/Build-BridgeValidation.py','ANTHEA/supporto/scripts/Build-IntegratedValidation.py')
source=source.replace('dotnet test GPCChecker.Test.CompositeBridge -c Release\\n  -p:BridgeLibraryDir=<ANTHEA>/lib/Checker','dotnet test supporto/test/BridgeValidationCurrent -c Release')
# Preserve source bibliography, but give the bridge references a distinct scope from CA R1-R8.
source=source.replace('page("Riferimenti e valutazione finale")','page("Riferimenti bibliografici della parte ponti")\np("I rimandi numerici [1]–[7] dei capitoli ponti appartengono a questa bibliografia; sono distinti dai rimandi R1–R8 dei capitoli CA.")')
source=source[:source.index('target=OUT/')]+'''append_current_examples()
target=TARGET
doc.save(target)
'''

def append_current_examples():
    g=globals(); p,h,page,table,fmt=g['p'],g['h'],g['page'],g['table'],g['fmt']
    rows=json.loads((ART/'metodi.json').read_text(encoding='utf8'))['Rows']
    def comparison(ids,names):
        selected=[r for r in rows if r['Case'] in ids and r['Name'] in names]
        # Repeated fiber comparisons are summarized only when all values are identical.
        seen=set(); items=[]
        for r in selected:
            key=(r['Case'],r['Name'],round(r['Expected'],12),round(r['Actual'],12))
            if key in seen:continue
            seen.add(key); items.append([r['Case'],r['Name'],fmt(r['Expected'],9),fmt(r['Actual'],9)])
        table(['Caso','Grandezza','Atteso','ANTHEA'],items,[2.1,6.1,4.4,4.4])
    page('Esempi espliciti dei nuovi test applicativi')
    p('La suite ANTHEA del 26 settembre aggiunge 2957 asserzioni ai 117 controlli preesistenti, tutti superati. Il conteggio comprende le singole fibre, non 3074 esempi distinti. I seguenti casi esplicitano gli ingressi e i risultati principali; metodi.json conserva tutti i confronti, incluse tolleranze e valori non arrotondati.')
    table(['Famiglia','Esempi'],[['Cumulativo','EL0 1 ed EL0 2'],['Storico lineare','EL1 1 ed EL1 2'],['Storico non lineare in campo elastico','EL2 1 ed EL2 2'],['Getto e scarico','GET1/GET2 e SC1/SC2, due momenti ciascuno'],['Ritiro ripetuto','RIT 1 e RIT 2'],['Momento curvatura','MK 1 e MK 2'],['Forza deformazione','NE 1 e NE 2'],['Plasticizzazione','PL 1 e PL 2'],['Rapporto modulare','HOM 1 e HOM 2']],[8.5,8.5])
    rect=[(500,25,-12.5),(14,1800,-925),(700,30,-1840)]
    A=sum(b*t for b,t,y in rect); S=sum(b*t*y for b,t,y in rect); yc=S/A
    I=sum(b*t**3/12+b*t*(y-yc)**2 for b,t,y in rect); E=210000
    h('Geometria comune e convenzioni')
    p('Solo carpenteria saldata, nessuna riduzione di classe 4 nei riferimenti analitici. Rettangoli (b;h;y): (500;25;−12,5), (14;1800;−925), (700;30;−1840), in mm. E = 210000 MPa. y = 0 è la sommità della carpenteria, N è positivo a trazione e ε(y) = ε0−κy.')
    p(f'A = 500×25 + 14×1800 + 700×30 = {fmt(A,12)} mm². S = 12500×(−12,5)+25200×(−925)+21000×(−1840) = {fmt(S,12)} mm³; yc = S/A = {fmt(yc,12)} mm.')
    table(['Rettangolo','b h³/12 mm⁴','A(y−yc)² mm⁴'],[[i+1,fmt(b*t**3/12,12),fmt(b*t*(y-yc)**2,12)] for i,(b,t,y) in enumerate(rect)],[3,7,7])
    p(f'I = Σ [bh³/12 + A(y−yc)²] = {fmt(I,12)} mm⁴. EA = {fmt(E*A,12)} N; EI = {fmt(E*I,12)} Nmm². Formule elementari di Navier e trasporto delle inerzie; confronto indipendente, non benchmark pubblicato.')
    h('Due soluzioni N M per ciascuno dei tre metodi')
    for k,(N,M) in enumerate([(-200,100),(150,-80)],1):
        mc=M*1e6+N*1000*yc; kap=mc/(E*I); e=N*1000/(E*A)+kap*yc
        p(f'Esempio {k}: N = {N} kN, M0 = {M} kNm alla quota 0. Mc = {M}×10⁶ + ({N}×1000)×({fmt(yc,12)}) = {fmt(mc,12)} Nmm. κ = Mc/(EI) = {fmt(kap,12)} 1/mm. ε0 = ({N}×1000)/({fmt(E*A,12)}) + κ×({fmt(yc,12)}) = {fmt(e,12)}.')
        p(f'σ(y) = 210000[({fmt(e,12)})−({fmt(kap,12)})y]. Alle facce y = 0 e −1855 mm: σsup = {fmt(E*e,12)} MPa; σinf = {fmt(E*(e+1855*kap),12)} MPa. Il cumulativo espone le facce; lo storico confronta la stessa retta a ogni coordinata di integrazione, senza attribuire alla fibra interna la quota del bordo.')
        comparison([f'EL{i}-{k}' for i in [0,1,2]],['deformazione assiale analitica','curvatura analitica','tensione cumulativa analitica'])
    h('Getto senza carico e scarico')
    for k,M in enumerate([100,-80],1):
        kap=M*1e6/(E*I);e=kap*yc
        p(f'GET1 {k} e GET2 {k}: primo incremento N = 0, M = {M} kNm, solo acciaio. κ1 = {M}×10⁶/({fmt(E*I,12)}) = {fmt(kap,12)}; ε01 = κ1 yc = {fmt(e,12)}. Nella fase di getto ΔN = ΔM = 0: εgetto(y)=ε01−κ1y; εmecc = εtot−εgetto = 0, quindi σCLS = σbarre = 0. Il test confronta il piano al getto su tutte le fibre di CLS e armatura nei due metodi storici.')
        p(f'SC1 {k} e SC2 {k}: solo acciaio, incrementi M = {M} e {-M} kNm. κ2 = κ1−κ1 = 0; ε02 = ε01−ε01 = 0; tutte le tensioni finali attese sono nulle. Si tratta di scarico elastico; il getto non appartiene a questa seconda sequenza.')
    comparison(['SC1-1','SC1-2','SC2-1','SC2-2'],['scarico elastico completo'])
    h('Ritiro ripetuto e omogeneizzazione')
    p('RIT 1 lineare e RIT 2 non lineare: 24 fasi di ritiro da −1 µε ciascuna; φ = 0 e ψL = 0,55. εimposta,CLS = Σ24(−1×10⁻⁶) = −24×10⁻⁶; εimposta,armatura = 0. Le azioni esterne sono nulle, quindi ΣσiAi = 0 e −ΣσiAiyi = 0. Il ritiro è un ingresso; la deformazione meccanica e le tensioni autoequilibrate sono risultati, non vanno poste uguali a −24 µε.')
    comparison(['RIT-1','RIT-2'],['ritiro incrementale cumulato','equilibrio N integrato','equilibrio M integrato'])
    Ec=34077.14619918933
    for k,phi in enumerate([.5,2],1):
        n=E/Ec*(1+phi)
        p(f'HOM {k}: C35/45, Ecm = 22000[(35+8)/10]^0,3 = {fmt(Ec,12)} MPa; φ = {phi}, ψL = 1. n = (210000/{fmt(Ec,12)})×(1+{phi}) = {fmt(n,12)}; Eeff = Ecm/(1+φ) = {fmt(Ec/(1+phi),12)} MPa. Unica fase composta N = −200 kN e M0 = 100 kNm. L’ingresso Da n deve riprodurre piano e tensioni dell’ingresso Da φ.')
    comparison(['HOM-1','HOM-2'],['storico phi/n equivalente','storico phi/n curvatura'])
    page('Sviluppo delle curve e della plasticizzazione')
    for k,sign in enumerate([-1,1],1):
        kap=sign*1e-8; m=E*I*kap
        p(f'MK {k}: origine dopo una fase nulla di solo acciaio, quattro punti oltre lo stato iniziale e due sottopassi per punto. N = −50 kN = −50000 N; yr = yc. Δκ = {sign}×0,00001 1/m = {fmt(kap,12)} 1/mm. A ogni punto ε0 = N/(EA)+κyc. Mr = M0+Nyc = EIκ; al termine Mr = {fmt(E*I,12)}×({fmt(kap,12)}) = {fmt(m,12)} Nmm = {fmt(m/1e6,12)} kNm.')
        comparison([f'MK-{k}'],['M=EIκ al baricentro','conversione incremento 1/m o microdeformazioni'])
    for k,sign in enumerate([-1,1],1):
        eps=sign*1e-5;n=E*A*eps
        p(f'NE {k}: stessi origine e passi; κ = 0, εr finale = {sign}×10 µε = {fmt(eps,12)}. ε0 = εr; σ = Eε = {fmt(E*eps,12)} MPa; N = EAε = {fmt(E*A,12)}×({fmt(eps,12)}) = {fmt(n,12)} N = {fmt(n/1000,12)} kN. Il momento al baricentro è nullo; M0 = −Nyc.')
        comparison([f'NE-{k}'],['N=EAε','conversione incremento 1/m o microdeformazioni'])
    for k,micro in enumerate([3000,4000],1):
        ep=micro*1e-6-235/E
        p(f'PL {k}: acciaio elastoplastico perfetto con fy esplicitamente assegnato a 235 MPa; κ = 0 e ε = {micro}×10⁻⁶. εy = 235/210000 = {fmt(235/E,12)}; ε > εy. σ = 235 MPa; N = 235×{fmt(A,12)} = {fmt(235*A,12)} N; εp = {micro}×10⁻⁶−235/210000 = {fmt(ep,12)}. Carico monotono: p accumulata = |εp|. Lo stato iniziale deve restare scarico dopo il calcolo dei punti successivi.')
        comparison([f'PL-{k}'],['plateau N=fy A','deformazione plastica analitica','accumulo monotono'])
    page('Riproduzione dei test applicativi aggiornati')
    p('Eseguire dalla radice ANTHEA: dotnet run --project supporto/test/X.Verifiche -c Release -- --bridge. Per i soli nuovi metodi: --bridge-methods. Impostare BRIDGE_METHOD_OUTPUT al percorso di un file JSON per acquisire i confronti numerici. I sorgenti sono BridgeSectionChecks.cs e BridgeMethodChecks.cs; gli hash delle librerie sono conservati negli artefatti della campagna.')
    p('Le 503 prove ordinarie delle suite MSTest e le 3074 asserzioni del programma ANTHEA hanno granularità diversa e non vengono sommate come numero di esempi indipendenti. Le quattro prove del costruttore corretto sono elencate separatamente; dieci prove ignorate non sono successi. Le schermate WPF conservate appartengono alla campagna precedente. Gli 88 esempi CA mantengono data, build e scostamenti originari.')
    p('Esito dell’integrazione: concordanza nei casi numerici rieseguiti e nelle conversioni dell’interfaccia di calcolo. I confronti sulle fibre non certificano la normativa applicabile a un intero ponte e non estendono il campo dei metodi storici a taglio, connessione o instabilità non inclusi.')
    page('Sviluppo numerico del benchmark composto in cinque fasi')
    p('Per rendere ripercorribili i casi CU e HL si riportano anche i coefficienti del sistema. Carpenteria come sopra; soletta 3000×250 mm, Ac = 750000 mm², yc,CLS = 125 mm, Ic = 3000×250³/12 = 3906250000 mm⁴. Armature disattivate; Eacciaio = 210000 MPa, Ecm = 34077,14619918933 MPa. Tutte le azioni sono applicate alla quota 0.')
    p('Porre A = ΣEiAi, B = −ΣEiAi yi e D = ΣEi(Ii+Ai yi²). Risolvere [A B; B D][Δε0;Δκ] = [n;m], con n = ΔN + Ec,eff Ac Δεcs e m = ΔM − Ec,eff Ac yc,CLS Δεcs. Pertanto Δε0 = (Dn−Bm)/(AD−B²), Δκ = (Am−Bn)/(AD−B²). Unità: A in N, B in Nmm, D in Nmm², n in N, m in Nmm.')
    phases=[('G1',-100,1500,0,1,0),('G2',-200,2000,2,1.1,0),('R1',0,0,1,.55,-100),('Q',50,-500,0,1,0),('R2',0,0,2,.55,-200)]
    for historical in [False,True]:
        h('Rettangoli pieni nello storico lineare' if historical else 'Modello a linea media nelle fasi composte cumulative')
        e_total=k_total=stress_total=0
        for index,(name,n0,m0,phi,psi,eigen_micro) in enumerate(phases):
            composite=index>0; ec=Ec/(1+phi*psi); eigen=eigen_micro*1e-6
            parts=[(E*b*t,y, E*b*t**3/12 if historical or not composite or j==1 else 0) for j,(b,t,y) in enumerate(rect)]
            if composite:parts.append((ec*750000,125,ec*3906250000))
            aa=sum(x[0] for x in parts);bb=-sum(x[0]*x[1] for x in parts);dd=sum(x[2]+x[0]*x[1]**2 for x in parts)
            nn=n0*1000+ec*750000*eigen;mm=m0*1e6-ec*750000*125*eigen;det=aa*dd-bb*bb
            de=(dd*nn-bb*mm)/det;dk=(aa*mm-bb*nn)/det;e_total+=de;k_total+=dk;stress_total+=E*(de+1855*dk)
            p(f'{name}: φ = {phi}, ψL = {psi}; Ec,eff = 34077,14619918933/(1+φψL) = {fmt(ec,10)} MPa. ΔN = {n0} kN, ΔM = {m0} kNm, Δεcs = {eigen_micro}×10⁻⁶. A = {fmt(aa,11)}, B = {fmt(bb,11)}, D = {fmt(dd,11)}. n = {fmt(nn,11)}, m = {fmt(mm,11)}; AD−B² = {fmt(det,11)}.')
            p(f'Δε0 = [({fmt(dd,9)})×({fmt(nn,9)})−({fmt(bb,9)})×({fmt(mm,9)})]/({fmt(det,9)}) = {fmt(de,11)}; Δκ = [({fmt(aa,9)})×({fmt(mm,9)})−({fmt(bb,9)})×({fmt(nn,9)})]/({fmt(det,9)}) = {fmt(dk,11)} 1/mm.')
            if historical:
                values=g['linear']['Rows']; ar=values[2*index]['Actual']; kr=values[2*index+1]['Actual']
                p(f'Somma fino a {name}: ε0 = {fmt(e_total,11)}, κ = {fmt(k_total,11)}. Checker: ε0 = {fmt(ar,11)}, κ = {fmt(kr,11)}. A una fibra y la tensione di acciaio è 210000Σ(Δε0−Δκy); nel CLS si sommano soltanto le fasi successive al getto con Ec,eff della relativa fase e sottrazione di Δεcs.')
            else:
                ar=next(r['Actual'] for r in g['cum']['Rows'] if r['Id']=='CU'+str(index+1) and r['Quantity']=='Acciaio · intradosso')
                p(f'Acciaio a y = −1855 mm: σ = 210000Σ(Δε0+1855Δκ) = {fmt(stress_total,11)} MPa; Checker = {fmt(ar,11)} MPa. Tolleranza assoluta 2×10⁻⁶ MPa.')

exec(compile(source,str(ROOT/'supporto/scripts/Build-BridgeValidation.py'),'exec'),globals())
# Preserve opaque ITEC furniture byte for byte; retain newly generated figure parts.
with ZipFile(TARGET) as z: data={n:z.read(n) for n in z.namelist()}
with ZipFile(BASE) as z:
    for n in z.namelist():
        if n.startswith(('word/header','word/_rels/header','word/theme/','word/font','word/numbering','word/media/')):data[n]=z.read(n)
with ZipFile(TARGET,'w',ZIP_DEFLATED) as z:
    for n,b in data.items():z.writestr(n,b)
print('FINAL',TARGET)
