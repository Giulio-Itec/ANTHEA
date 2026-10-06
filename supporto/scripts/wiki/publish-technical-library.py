"""Publish prepared content into the two canonical guides, with a recoverable revision archive."""
from pathlib import Path
from hashlib import sha256
import json, re, shutil, sys, importlib.util
sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parents[3]; SUPPORT=ROOT/'supporto'
ART=SUPPORT/'artefatti/wiki-tecnica-fonti'; PREP=ART/'prepared'
spec=importlib.util.spec_from_file_location('technical_chapters',Path(__file__).with_name('technical-chapters.py'))
content=importlib.util.module_from_spec(spec);spec.loader.exec_module(content)
REV='29'; OLD='28'
archive=SUPPORT/'SUPERATI/wiki-tecnica-rev29-20261006'
edition=SUPPORT/'scripts/Build-AntheaGuides-Itec.py'
assert any(f"REVISION = '{r}'" in edition.read_text(encoding='utf-8') for r in [OLD,REV]), 'La revisione corrente è cambiata: rileggere prima di procedere'
archive.mkdir(parents=True,exist_ok=True)
records=[]
paths=[SUPPORT/x for x in ['docs/guida-pratica-anthea.md','docs/guida-pratica-anthea.pdf','docs/guida-teorica-anthea.md','docs/guida-teorica-anthea.pdf','README.md','README.pdf','installer/Indice-guide.md','installer/Indice-guide.pdf']]
paths+=list((SUPPORT/'documentazione/Guide_ANTHEA').glob('*Rev28.*'))
paths+=[ROOT/'X.Desktop/Wiki'/x for x in ['editorial.json','references.json','index.json']]
for p in paths:
    relative=p.relative_to(ROOT);dest=archive/relative
    dest.parent.mkdir(parents=True,exist_ok=True)
    if not dest.exists():shutil.copy2(p,dest)
    records.append(dict(origine=str(relative),archivio=str(dest.relative_to(ROOT)),sha256=sha256(dest.read_bytes()).hexdigest(),motivo='Sostituzione con biblioteca tecnica e aiuti contestuali',revisione_sostitutiva=REV))
(archive/'registro.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')

editorialPath=ROOT/'X.Desktop/Wiki/editorial.json';refsPath=ROOT/'X.Desktop/Wiki/references.json'
editorial=json.loads((archive/'X.Desktop/Wiki/editorial.json').read_text(encoding='utf-8'))
refs=json.loads((archive/'X.Desktop/Wiki/references.json').read_text(encoding='utf-8'))
mdp=json.loads((PREP/'mdp-articles.json').read_text(encoding='utf-8'))
resources=json.loads((PREP/'reading-resources.json').read_text(encoding='utf-8'))
chapters=json.loads((ROOT/'X.Desktop/Wiki/chapters.json').read_text(encoding='utf-8'))
allNew=list(content.CHAPTERS)
originalKeys=[x['key'] for x in allNew]

hub='''## Biblioteca tecnica fonti e percorsi di approfondimento

La biblioteca collega la teoria di ANTHEA a contributi tecnici, esempi numerici e dispense. Le nuove pagine sui ponti seguono i temi suggeriti dal libro online TheBridgeEng: solette, travi metalliche, precompressione, cassoni, stralli e deformazioni. I testi di ANTHEA sono originali e dichiarano ipotesi, unità, limiti e funzioni effettivamente disponibili.

### Contributi integrati e risorse esterne

La raccolta comprende 179 contributi selezionati dal sito di Marco De Pisapia: adattamenti testuali tecnici e alcune schede di risorse prevalentemente audiovisive. I contenuti promozionali e i blocchi dedicati ai prodotti dell’autore sono stati esclusi. Ogni contributo conserva titolo, autore, fonte, condizioni CC BY-NC 3.0 Italia e descrizione dell’adattamento. Il vincolo non commerciale si applica ai contributi riprodotti; non è una licenza attribuita al codice di ANTHEA. Le immagini di terzi e le formule disponibili solo come immagini restano raggiungibili tramite collegamenti alla fonte.

Le risorse di GeoStru e Simone Caffè sono organizzate in percorsi tematici. Sono letture esterne, non copie integrali dei siti: in assenza di una licenza di riproduzione accertata, i PDF e gli articoli rimangono presso gli autori. Le nuove pagine interne sui parametri geotecnici, consolidazione, indagini geofisiche, palificate e lettura degli esempi forniscono una trattazione originale collegata a tali risorse. Il catalogo non equivale alla revisione tecnica completa di ogni documento esterno.

### Nuove lezioni con esempi svolti

'''
hub+='\n'.join('- ['+x['title']+'](wiki:'+x['key']+')' for x in allNew)
hub+='''

### Come confrontare le fonti

Prima di trasferire un procedimento al progetto, controllare edizione normativa, campo geometrico, unità, segni, stato limite e ipotesi costitutive. NTC, Eurocodici, AASHTO e ACI non costituiscono un unico insieme di coefficienti. Le lezioni storiche possono essere utili per il modello meccanico anche quando la norma citata non è quella adottata nel progetto.

Un collegamento a un modulo indica pertinenza tecnica, non l’implementazione automatica di tutto l’argomento. Le schede contrassegnate come da completare nei riscontri conservano questo stato anche quando sono leggibili e ricercabili. Per comprendere il risultato del programma, partire dalla guida del modulo e dai suoi limiti correnti.

### Altre biblioteche tecniche

- [TheBridgeEng, Bridge Superstructure Design](https://thebridgeeng.com/book/bridge-superstructure-design): percorso editoriale di riferimento per i temi dell’impalcato; nessuna riproduzione integrale del libro.
- [JRC, esempi applicativi Eurocodici](https://eurocodes.jrc.ec.europa.eu/learning-corner): materiale formativo da distinguere dal testo normativo.
- [AISC e NSBA, Steel Bridge Design Handbook](https://www.aisc.org/bridges/bridge-resource-center/steel-bridge-design-handbook/): progettazione dei ponti metallici nel contesto delle norme richiamate.
- [FHWA, fondazioni e geotecnica](https://www.fhwa.dot.gov/engineering/geotech/foundations/): manuali su indagini, fondazioni superficiali e profonde.
- [MIT OpenCourseWare, Structural Mechanics](https://ocw.mit.edu/courses/2-080j-structural-mechanics-fall-2013/download/): meccanica, stabilità e formulazioni analitiche.
- [TU Delft, Open Interactive Textbooks](https://oit.tudelft.nl/): testi universitari aperti, con licenza da leggere per ogni opera.

'''
allNew.append(dict(key='biblioteca-tecnica',title='Biblioteca tecnica fonti e percorsi di approfondimento',chapterId='fondamenti',area='Normativa e riferimenti',modules=[],related=originalKeys,status='reviewed',body=hub))

for chapter in chapters:
    rows=[x for x in resources if x['chapterId']==chapter['id']]
    if not rows:continue
    key='letture-'+chapter['id'];title='Letture tecniche · '+chapter['title']
    body='## '+title+'\n\nPercorso di letture esterne selezionate da GeoStru e Simone Caffè, consultato il 6 ottobre 2026. Gli articoli e i PDF restano presso gli autori; questi rimandi non indicano che i procedimenti siano tutti implementati o validati in ANTHEA. Confrontare norme, ipotesi e unità con le pagine interne prima di utilizzare un esempio.\n\n'
    body+='### Collegamenti con la teoria interna\n\n'
    related=list(dict.fromkeys(y for x in rows for y in x['related']))
    related+= [x['key'] for x in content.CHAPTERS if x['chapterId']==chapter['id']]
    for k in list(dict.fromkeys(related)):
        match=next((x for x in list(editorial.values())+allNew if x['key']==k),None)
        if match:body+='- ['+match['title']+'](wiki:'+k+')\n'
    for source in ['GeoStru','Simone Caffè']:
        sourceRows=[x for x in rows if x['source']==source]
        if not sourceRows:continue
        body+='\n### '+source+'\n\n'
        for x in sourceRows:body+='- ['+x['title'].replace('[','(').replace(']',')')+']('+x['url']+')\n'
    allNew.append(dict(key=key,title=title,chapterId=chapter['id'],area=rows[0]['area'],modules=list(dict.fromkeys(m for x in rows for m in x['modules'])),related=list(dict.fromkeys(related)),status='qualified',body=body+'\n'))
allNew+=mdp

# Connect source contributions to the most relevant original lessons, in both directions.
special={265:'taglio-traliccio',202:'taglio-traliccio',89:'geotecnica-parametri',194:'geotecnica-parametri',201:'geotecnica-parametri',65:'cedimenti-consolidazione',193:'geofisica-liquefazione',152:'geofisica-liquefazione',234:'palo-elastico'}
for a in mdp:
    i=int(a['key'].split('-')[1])
    if i in special:a['related']=list(dict.fromkeys([special[i]]+a['related']))

for n,a in enumerate(allNew):
    body=a['body']
    if a['related']:body+='\n### Proseguire nella Wiki\n\n'+'\n'.join('- ['+next(x['title'] for x in list(editorial.values())+allNew if x['key']==k)+'](wiki:'+k+')' for k in a['related'])+'\n'
    a['body']=body
    meta={k:v for k,v in a.items() if k not in ['body','sourceUrl','license']}
    meta.update(id='/wiki/manuale/'+a['chapterId']+'/'+a['key'],level='advanced' if a['key'] in originalKeys else 'intermediate',prerequisites=[],references=[],order=200+n)
    prose=[line for line in body.splitlines()[1:] if line and not line.startswith(('#','|','-','!','```'))]
    meta['summary']=(prose[0][:210]+'…') if prose and len(prose[0])>210 else (prose[0] if prose else a['title'])
    if a in mdp:
        refid=a['key']+'-source';meta['references']=[refid]
        meta['summary']='Marco De Pisapia · adattamento tecnico con fonte e licenza CC BY-NC 3.0 Italia. '+a['title'].replace('Approfondimento · ','')
        refs.append(dict(id=refid,title='Marco De Pisapia · '+a['title'].replace('Approfondimento · ',''),kind='Contributo CC BY-NC 3.0 Italia; edizione normativa da verificare',url=a['sourceUrl']))
    editorial['theory:'+a['title']]=meta

backlinks={'sezione-in-calcestruzzo-armato':['taglio-traliccio'],'sezione-composta-da-ponte':['ponti-acciaio-fasi','ponti-deformazioni'],'bridge':['ponti-solette','ponti-precompressione','ponti-cassoni','ponti-stralli'],'bearing-capacity':['geotecnica-parametri','cedimenti-consolidazione'],'palo-elastico':['geotecnica-gruppi'],'guida-muri-di-sostegno-con-stratigrafie-di-monte-e-valle':['muri-metodi-perimetro']}
for a in editorial.values():
    if a['key'] in backlinks:a['related']=list(dict.fromkeys(a['related']+backlinks[a['key']]))

for kind in ['pratica','teorica']:
    p=SUPPORT/f'docs/guida-{kind}-anthea.md'
    text=(archive/f'supporto/docs/guida-{kind}-anthea.md').read_text(encoding='utf-8-sig')
    text=text.replace('5 ottobre 2026','6 ottobre 2026').replace('revisione documentale 28','revisione documentale 29')
    if kind=='teorica':text+='\n\n'+'\n\n'.join(a['body'].strip() for a in allNew)+'\n'
    else:
        marker='## Wiki e centro della conoscenza'
        assert marker in text
        addition='''

### Wiki del modulo e aiuti sui singoli dati

Ogni modulo dispone del pulsante Wiki del modulo. Il comando Approfondimenti apre una scelta di argomenti e sezioni pertinenti. Accanto ai dati per cui esiste una spiegazione specifica, il pulsante ? raggiunge il punto della Wiki: per esempio copriferro nominale, copriferro per fessurazione, materiali, rigidezza del palo, armature e sisma. F1 nell’editor apre l’aiuto del dato, quando disponibile, oppure la guida del modulo.

La consultazione conserva il foglio di lavoro: il comando Torna al lavoro riporta ai dati correnti. Anche le revisioni in sola lettura consentono di consultare gli aiuti. I collegamenti riguardano il tema del dato, non avviano una modifica dei parametri o un diverso metodo di verifica.

### Consultare la nuova biblioteca tecnica

La [biblioteca tecnica](wiki:biblioteca-tecnica) raccoglie lezioni con esempi, adattamenti attribuiti e percorsi di lettura esterni. Cercare un termine come taglio, copriferro, consolidazione, MASW o cassoni. Le schede delle fonti distinguono testo adattato, risorsa audiovisiva e materiale esterno; le immagini collegate richiedono accesso al sito originale. La data di consultazione non significa aggiornamento normativo automatico del contenuto.
'''
        start=text.index(marker)+len(marker);text=text[:start]+addition+text[start:]
    p.write_text(text,encoding='utf-8')

editorialPath.write_text(json.dumps(editorial,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
refsPath.write_text(json.dumps(refs,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for rel in ['README.md','installer/Indice-guide.md']:
    p=SUPPORT/rel;t=(archive/'supporto'/rel).read_text(encoding='utf-8-sig').replace('Rev28','Rev29').replace('Revisione 28','Revisione 29').replace('5 ottobre 2026','6 ottobre 2026')
    t=t.replace('49 articoli consolidati',str(len(editorial))+' articoli e percorsi')
    if rel.startswith('installer'):t+='\n\n## Biblioteca tecnica Rev29\n\n'+str(len(content.CHAPTERS))+' lezioni originali con esempi; 179 contributi attribuiti di Marco De Pisapia; 174 letture esterne selezionate GeoStru e Simone Caffè. I contributi riprodotti conservano la licenza non commerciale indicata. Figure esterne e video non sono inclusi offline.\n'
    p.write_text(t,encoding='utf-8')
t=edition.read_text(encoding='utf-8');t=t.replace("REVISION = '28'","REVISION = '29'").replace("DATE = '05/10/2026'","DATE = '06/10/2026'").replace("CONTENTS = '5 ottobre 2026'","CONTENTS = '6 ottobre 2026'").replace("CONTENTS_ISO = '2026-10-05'","CONTENTS_ISO = '2026-10-06'")
t=re.sub(r"REVISION_NOTE = '[^']*'","REVISION_NOTE = 'BIBLIOTECA TECNICA E AIUTI CONTESTUALI'",t);edition.write_text(t,encoding='utf-8')
p=SUPPORT/'scripts/Render-AntheaGuides-Itec.ps1';p.write_text(p.read_text(encoding='utf-8-sig').replace("$Revision = '28'","$Revision = '29'"),encoding='utf-8-sig')
qa=SUPPORT/'artefatti/guide_anthea_itec_rev29';qa.mkdir(exist_ok=True)
(qa/'artifact.md').write_text('Conservare il modello ITEC e le due guide globali. Integrare biblioteca tecnica attribuita e lezioni originali; mantenere evidenti limiti, fonti e licenze. Verificare formule, collegamenti, impaginazione e riapertura dei moduli. Figure di fonti esterne restano collegate; nessuna riproduzione integrale delle risorse senza licenza.',encoding='utf-8')
(ART/'publication.json').write_text(json.dumps(dict(revision=REV,articles=len(editorial),newArticles=len(allNew),originalLessons=len(content.CHAPTERS),attributedContributions=len(mdp),externalReadings=len(resources)),indent=2),encoding='utf-8')
print((ART/'publication.json').read_text())
