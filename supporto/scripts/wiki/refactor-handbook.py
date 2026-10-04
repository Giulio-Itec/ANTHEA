"""One-time Rev14 migration. Preserve complete prior editions before changing the manuals."""
from pathlib import Path
import json, shutil, hashlib, re
ROOT=Path(__file__).resolve().parents[3]
W=ROOT/'X.Desktop/Wiki'; S=ROOT/'supporto'
ARCH=S/'SUPERATI/wiki-handbook-rev14-20261003'
if ARCH.exists(): raise SystemExit('Migration already started: use the edited canonical sources.')
paths=[S/f'docs/guida-{k}-anthea.{e}' for k in ('pratica','teorica') for e in ('md','pdf')]
paths+=list((S/'documentazione/Guide_ANTHEA').glob('*Rev13.*'))
paths += [S/'installer/Indice-guide.md',S/'installer/Indice-guide.pdf']
registry=[]
for p in paths:
    if not p.exists(): continue
    dest=ARCH/p.relative_to(S);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
    registry.append(dict(origine=str(p.relative_to(ROOT)),archivio=str(dest.relative_to(ROOT)),motivo='Refactoring editoriale Wiki; copia integrale prima della revisione',sostituzione='Guide globali Rev14',sha256=hashlib.sha256(dest.read_bytes()).hexdigest()))
(ARCH/'registro.json').write_text(json.dumps(registry,ensure_ascii=False,indent=2),encoding='utf-8')
articles=json.loads((W/'index.json').read_text(encoding='utf-8'))
audit=[]
for a in articles:
    data=(S/f'docs/guida-{a["source"].split(".")[0]}-anthea.md').read_bytes()
    body=data[a['offset']:a['offset']+a['length']].decode('utf-8')
    audit.append(dict(id=a['id'],title=a['title'],words=len(body.split()),figures=len(re.findall(r'^!\[',body,re.M)),formulas=len(re.findall(r'^```math|^\$\$ ',body,re.M)),related=len(a['related']),sections=len(a['sections']),duplicateTitle=sum(x['title']==a['title'] and x['type']==a['type'] for x in articles)>1))
(S/'artefatti/wiki-handbook/audit-before.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf-8')
chapters=[
('fondamenti','Fondamenti','Equilibrio, unità, azioni e affidabilità','Parti dal percorso dei carichi. Dichiara i riferimenti delle azioni e separa domanda, capacità e criteri di esercizio prima di scegliere un modello.'),
('meccanica','Scienza e tecnica delle costruzioni','Cinematica, rigidezza e stabilità','Una struttura deve soddisfare equilibrio, compatibilità e legge costitutiva. La stabilità aggiunge una domanda diversa: la configurazione di equilibrio resiste a una perturbazione?'),
('materiali','Materiali','Legami costitutivi e durabilità','Una resistenza non descrive da sola un materiale. Modulo elastico, deformazioni differite, aderenza e ambiente influenzano modello e dettagli; conserva fonte e unità dei parametri.'),
('calcestruzzo','Calcestruzzo armato','Sezioni, fessurazione e dettagli','Acciaio e calcestruzzo collaborano mediante aderenza. Distingui lo stato limite ultimo dalle verifiche di esercizio e collega il risultato alla disposizione effettiva delle armature.'),
('acciaio','Acciaio','Aste compresse e fenomeni di instabilità','Sezione, asta e sistema possono avere modi di crisi differenti. Leggi prima il modello ideale di Euler, poi distingui instabilità flessionale, flesso-torsionale e locale.'),
('geotecnica','Geotecnica','Fondazioni, pali e opere di sostegno','Il drenaggio e il livello di deformazione governano la scelta dei parametri. Parti dal problema fisico, poi confronta resistenza, spostamenti e interazione con la struttura.'),
('ponti','Ponti e infrastrutture','Anatomia, sistemi e costruzione','Segui le azioni dall’impalcato alle fondazioni. Schema degli appoggi, sequenza costruttiva e manutenzione determinano la risposta tanto quanto la sezione trasversale.'),
('sismica','Ingegneria sismica','Masse, modi e risposta dinamica','Il periodo dipende da massa e rigidezza; la domanda sismica richiede anche smorzamento e spettro. Le combinazioni modali non restituiscono una storia temporale con segni simultanei.'),
('fem','FEM e modellazione numerica','Idealizzazione, elementi e risultati','Scegli i gradi di libertà necessari al fenomeno. Verifica connettività, vincoli, equilibrio e convergenza prima di interpretare picchi di tensione o coefficienti di utilizzo.'),
('bim','BIM','Informazione e interoperabilità','Un modello informativo collega geometria, identità e proprietà. Prima dello scambio definisci quale informazione deve arrivare, in quale riferimento e con quali controlli.'),
('computational','Computational Design','Parametri, dipendenze e automazione','Un processo parametrico rende esplicite dipendenze e vincoli. Mantieni distinguibili generazione geometrica, analisi, verifica e scelta progettuale.'),
('anthea','Anthea','Moduli, progetti e procedure','Le guide operative seguono i moduli realmente presenti. Ogni risultato va letto con combinazione, metodo, unità e limiti; la Wiki apre il modulo senza duplicarne il motore.')]
(W/'chapters.json').write_text(json.dumps([dict(id=i,number=n,title=t,description=d,introduction=p) for n,(i,t,d,p) in enumerate(chapters,1)],ensure_ascii=False,indent=2),encoding='utf-8')
editorial=json.loads((W/'editorial.json').read_text(encoding='utf-8'))
mapping={'Fondamenti di ingegneria strutturale':'fondamenti','Azioni e combinazioni':'fondamenti','Calcestruzzo armato':'calcestruzzo','Acciaio':'acciaio','Fondazioni e geotecnica':'geotecnica','Ponti':'ponti','Metodi costruttivi':'ponti','Dinamica e sisma':'sismica','FEM e modellazione':'fem','Normativa e riferimenti':'anthea'}
for key,m in editorial.items():
    m['key']=('guida-' if key.startswith('guide:') else '')+m['id'].split('/')[-1]
    m['chapterId']='anthea' if key.startswith('guide:') else mapping[m['area']]
    m['status']='historical' if re.search(r':(?:TEORI(?:CA|A)|PRATICA) A\d+|:Approfondimenti integrati',key) else 'existing'
    m['level']='intermediate';m['prerequisites']=[];m['references']=[]
# Identifiers stay stable even when a chapter, route or display title changes.
for key,m in editorial.items():
    if key=='theory:Instabilità delle aste compresse': m.update(key='euler',status='reviewed',level='advanced',references=['mit-buckling'],prerequisites=['load-path'])
    if key=='theory:Elementi Beam': m.update(key='beam',status='reviewed',references=['delft-beam'],prerequisites=['load-path'])
    if key=='theory:Fondamenti del percorso dei carichi': m.update(key='load-path',level='introductory')
    if key=='guide:Modulo Sezione in c.a.': m.update(key='guida-sezione-ca',status='reviewed',references=['anthea-section'],prerequisites=['beam'])
    if key=='theory:2 Calcestruzzo armature e copriferro':m['chapterId']='materiali'
    if key=='theory:Releases e connettività':m['chapterId']='meccanica'
seen={}
for key,m in editorial.items():
    if m['key'] in seen:m['key']+='-appendice'
    seen[m['key']]=key
(W/'editorial.json').write_text(json.dumps(editorial,ensure_ascii=False,indent=2),encoding='utf-8')
print('Preserved',len(paths),'documents; audited',len(audit),'articles')
