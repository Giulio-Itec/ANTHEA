"""Register the authored pilot chapters without duplicating their canonical bodies."""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[3]; W=ROOT/'X.Desktop/Wiki'
def read(name):return json.loads((W/name).read_text(encoding='utf-8'))
def write(name,value):(W/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
m=read('editorial.json')
entries=[
("Cos'è un ponte?",'bridge','ponti','Ponti','introductory',['load-path','beam'],[],['fhwa-bridge'],['ponti_concept']),
('Fessurazione del calcestruzzo armato','cracking','calcestruzzo','Calcestruzzo armato','advanced',['guida-sezione-ca','beam'],['beam'],['jrc-cracking'],['str_palo']),
('Capacità portante delle fondazioni superficiali','bearing-capacity','geotecnica','Fondazioni e geotecnica','advanced',['load-path'],['load-path'],['fhwa-foundations'],[]),
('BIM e controllo dello scambio informativo','bim','bim','BIM','introductory',['beam'],[],['ifc'],[]),
('Progettazione parametrica e controllo delle dipendenze','parametric','computational','Computational Design','introductory',['beam','bim'],['beam'],['grasshopper','delft-beam'],[])]
# BridgeConcept.Module is checked against the repository, not inferred from its display name.
import re
module_source=(ROOT/'X.Calculations/BridgeConcept.cs').read_text(encoding='utf-8')
bridge_module=re.search(r'const string Module\s*=\s*"([^"]+)"',module_source)[1]
for title,key,chapter,area,level,related,pre,refs,modules in entries:
    if key=='bridge':modules=[bridge_module]
    m['theory:'+title]=dict(id=f'/wiki/manuale/{chapter}/{key}',key=key,chapterId=chapter,area=area,level=level,status='reviewed',related=related,prerequisites=pre,references=refs,modules=modules,order={'bridge':0,'cracking':1,'bearing-capacity':2}.get(key,100))
for key,v in m.items():
    if v['key']=='guida-sezione-ca':v['references']=[]
    if v['key']=='beam':v['related']=['load-path','euler','cracking','guida-sezione-ca'];v['order']=0
    if v['key']=='euler':v['related']=['beam','load-path'];v['order']=0
    if v['key']=='load-path':v['related']=['bridge','beam','bearing-capacity'];v['order']=0
    summaries={
        'bridge':'Anatomia, percorso dei carichi, appoggi e fasi: leggere un ponte prima di costruirne il modello.',
        'euler':'Biforcazione elastica, lunghezza efficace e snellezza. Derivazione, diagramma ed esempio con unità coerenti.',
        'cracking':'Apertura caratteristica delle fessure: meccanismo, area efficace, formulazione classica ed esempio SLE.',
        'bearing-capacity':'Pressione ultima, convenzioni lordo/netto e drenaggio. Un caso non drenato completamente specificato.',
        'beam':'Dalla struttura reale ai gradi di libertà: cinematica, rigidezze, connettività e controllo dei risultati.',
        'bim':'Identità, proprietà e coordinate: controllare uno scambio prima del trasferimento al modello analitico.',
        'parametric':'Geometria, analisi e verifica in un processo riproducibile: dipendenze, dati e limiti dell’ottimizzazione.'}
    if v['key'] in summaries:v['summary']=summaries[v['key']]
write('editorial.json',m)
write('references.json',[
dict(id='mit-buckling',title='MIT OpenCourseWare · Buckling of Beams · 16.001 · 2021',kind='Modello teorico',url='https://ocw.mit.edu/courses/16-001-unified-engineering-materials-and-structures-fall-2021/mit16_001_f21_lec33lec34lec35.pdf'),
dict(id='delft-beam',title='TU Delft · Computational Modelling · Euler-Bernoulli beam elements',kind='Formulazione FEM',url='https://interactivetextbooks.citg.tudelft.nl/computational-modelling/structural_linear/euler_bernouilli.html'),
dict(id='jrc-cracking',title='J.C. Walraven · Eurocode 2 · JRC · 2008 · apertura delle fessure',kind='Formulazione normativa classica; verificare edizione di progetto',url='https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/EN1992_1_Walraven.pdf'),
dict(id='fhwa-foundations',title='FHWA · GEC No. 6 · Shallow Foundations',kind='Modello geotecnico; non prescrizione italiana',url='https://www.fhwa.dot.gov/engineering/geotech/pubs/010943.pdf'),
dict(id='fhwa-bridge',title="FHWA · Bridge Inspector’s Reference Manual e materiali di ispezione",kind='Terminologia e componenti',url='https://www.fhwa.dot.gov/bridge/inspection/index.cfm'),
dict(id='ifc',title='buildingSMART · IFC 4.3 · Documentazione dello schema',kind='Schema informativo',url='https://ifc43-docs.standards.buildingsmart.org/'),
dict(id='grasshopper',title='McNeel · Grasshopper data trees and Python',kind='Documentazione software',url='https://developer.rhino3d.com/guides/rhinopython/grasshopper-datatrees-and-python/')])
g=read('glossary.json')
for term,definition,target,aliases in [
('SLU','Stato limite ultimo: controllo di una condizione di collasso o perdita di equilibrio. Non equivale a una verifica di esercizio.','/wiki/manuale/azioni-e-combinazioni/azioni-e-combinazioni-del-modello','ultimate limit state'),
('SLE','Stato limite di esercizio: funzionalità, deformazioni o fessure rispetto ai requisiti assegnati.','cracking','serviceability limit state'),
('FEM','Metodo degli elementi finiti: discretizzazione di un problema continuo mediante elementi e incognite nodali.','beam','finite element method'),
('DOF','Grado di libertà: componente cinematica indipendente, per esempio traslazione o rotazione nodale.','beam#gradi-di-liberta','degree of freedom'),
('MPC','Vincolo multipunto: relazione fra gradi di libertà di nodi diversi; controllare compatibilità e trasferimento delle azioni.','beam#offset-ed-eccentricita','multi point constraint rigid link'),
('LTB','Instabilità flesso-torsionale di un elemento inflesso, con spostamento laterale e torsione accoppiati. Non coincide con Euler di un’asta compressa.','euler#dalla-teoria-alla-verifica','lateral torsional buckling instabilita flesso torsionale'),
('M-N','Dominio di interazione fra momento e sforzo normale; il punto di domanda deve conservare la stessa combinazione.','guida-sezione-ca','pressoflessione dominio interazione'),
('SRSS','Combinazione modale con radice della somma dei quadrati; l’uso richiede modi sufficientemente separati.','/wiki/manuale/dinamica-e-sisma/dinamica-e-sisma-del-modello','square root sum squares'),
('CQC','Combinazione quadratica completa, che considera la correlazione fra risposte modali in funzione di frequenze e smorzamento.','/wiki/manuale/dinamica-e-sisma/dinamica-e-sisma-del-modello','complete quadratic combination'),
('p-y','Relazione locale fra reazione laterale del terreno per unità di lunghezza e spostamento del palo. Una molla elastica lineare ne rappresenta solo una idealizzazione.','palo-elastico','curve reazione terreno palo')]:g[term]=[definition,target,aliases]
write('glossary.json',g)
print('Registered pilots, sources and glossary')
