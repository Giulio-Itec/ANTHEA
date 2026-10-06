"""Rev26: dettagli sismici della testa dei pali, NTC2018 §7.2.5."""
from pathlib import Path
import hashlib,json,shutil,subprocess,sys
root=Path(__file__).resolve().parents[3];support=root/'supporto'
archive=support/'SUPERATI/palo-sisma-rev26-20261005'
paths=[support/f'docs/guida-{kind}-anthea.{ext}' for kind in ('pratica','teorica') for ext in ('md','pdf')]
paths+=list((support/'documentazione/Guide_ANTHEA').glob('*Rev25.*'))
paths+=[support/name for name in ('README.md','README.pdf','installer/Indice-guide.md','installer/Indice-guide.pdf')]
records=[]
for path in paths:
    if not path.is_file():continue
    target=archive/path.relative_to(support);target.parent.mkdir(parents=True,exist_ok=True)
    if not target.exists():shutil.copy2(path,target)
    records.append(dict(origine=str(path.relative_to(root)),archivio=str(target.relative_to(root)),motivo='Aggiornamento dei controlli sismici di testa NTC §7.2.5',sostituzione=str(path.relative_to(root)).replace('Rev25','Rev26'),sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
(archive/'registro.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')

practical=support/'docs/guida-pratica-anthea.md'
text=practical.read_text(encoding='utf-8-sig').replace('revisione documentale 25','revisione documentale 26')
marker='### Ricalcolo delle sollecitazioni e delle resistenze'
addition='''### Controlli sismici della testa del palo

Nella Risposta elastica, aprire Tratti di armatura → Sisma · testa del palo · NTC §7.2.5. Attivare il controllo quando si considera una zona dissipativa presso la testa, perché non è escluso il raggiungimento della capacità. Gli archivi precedenti mantengono la modalità disattivata: non si attribuisce automaticamente natura sismica al carico esistente.

Lasciando vuota l'estensione, Checker usa 10D dalla testa fisica del palo, comprendendo l'eventuale tratto libero. È possibile assegnare una zona più lunga; una più corta non supera il controllo di estensione. Se il palo è più corto di 10D, il programma controlla tutta la lunghezza e dichiara questa convenzione. Il confine della zona entra nella mesh. Ogni tratto che la interseca viene controllato con la propria armatura uniforme; per differenziare la staffatura, dividere il tratto alla quota voluta. L'attivazione non cambia automaticamente barre, passi o quote di taglio.

Scegliere Da definire, Staffe singole oppure Spirale. In zona dissipativa una spirale non soddisfa la prescrizione delle staffe singole. Le conferme della combinazione sismica per N e V e del momento elastico non ridotto, con N concomitante, sono separate. Un modello FEM elastico non dimostra da solo che i carichi derivino da un'analisi sismica con q=1. Senza tali conferme restano disponibili i controlli geometrici, mentre quelli sulle azioni sono indicati come da completare.

Nel riepilogo a destra selezionare Sisma · testa palo. Sono mostrati estensione, minimo longitudinale, diametro e passo delle staffe, tipologia trasversale, margine a taglio, compressione media e momento elastico, con valore, limite, quota critica e criterio. Dentro la zona valgono As almeno 1% Ac e passo massimo 6φL; fuori valgono 0,3% e 8φL. Il diametro trasversale minimo è 8 mm. La modalità sismica applica questi minimi anche se il controllo ordinario dei minimi pali è disattivato. I coefficienti unitari/custom non possono dare un esito di conformità NTC.

Dimensiona usa anche i controlli sismici attivi per selezionare i candidati; richiede le conferme necessarie. Barre, ancoraggi e giunti rimangono da verificare nella disposizione reale. Modificare armature o conferme riutilizza la risposta FEM quando gli input fisici restano invariati; modificare il confine della zona aggiorna la discretizzazione. Sul profilo palo-terreno e su tutti i diagrammi la zona è evidenziata con una fascia viola, il contorno presso il palo e una linea orizzontale alla quota minima 10D. La legenda superiore indica 10D in metri. Se la zona assegnata è più lunga, sono distinti il limite minimo e la fine assegnata; le quote vicine hanno richiami separati per evitare sovrapposizioni. Il controllo Zona sismica 10D permette di nascondere solo la rappresentazione. La fascia è disponibile già con i risultati FEM, prima del completamento di MRd. La tavola armature conserva il contorno arancione della zona. Archivio, JSON, CSV e report conservano impostazioni, fonte ed esiti.

Il controllo riguarda le prescrizioni di testa e le condizioni semplificate in assenza di una valutazione specifica di duttilità. Non genera combinazioni sismiche, azioni cinematiche, zone dissipative profonde, verifiche del nodo palo-plinto o dettagli esecutivi. I relativi limiti sono elencati nel riepilogo. La fonte è apribile dal pulsante dedicato: NTC 2018 §7.2.5, Gazzetta Ufficiale, pagina PDF 217, stampata 213.

'''
assert marker in text
text=text.replace(marker,addition+marker)
practical.write_text(text.rstrip()+'\n',encoding='utf-8')

theory=support/'docs/guida-teorica-anthea.md'
text=theory.read_text(encoding='utf-8-sig').replace('revisione documentale 25','revisione documentale 26')
text=text.replace('Questi ultimi non vengono dedotti dall\'analisi elastica e restano esplicitamente esclusi.', 'I dettagli di testa e le condizioni semplificate sono ora disponibili in una modalità esplicita descritta di seguito; l\'analisi non genera le azioni sismiche e non esegue una valutazione specifica della duttilità.')
text=text.replace('Sono i tre controlli specifici disponibili.', 'Sono i tre minimi ordinari, distinti dalla modalità sismica di testa.')
marker='### Criterio di cambio sezione e tempi delle verifiche'
addition='''### Zona dissipativa di testa secondo NTC 2018

Fonte verificata: D.M. 17 gennaio 2018, §7.2.5, sottoparagrafo Fondazioni su pali, G.U. 20 febbraio 2018, S.O. n.8, pagina stampata 213 / PDF 217. Testo e pagina sono stati letti anche visivamente sul PDF ufficiale: https://www.gazzettaufficiale.it/eli/gu/2018/02/20/42/so/8/sg/pdf#page=217. È stato consultato anche il D.M. 9 marzo 2023 pubblicato nella G.U. 22 marzo 2023: le modifiche esaminate riguardano disposizioni transitorie e i punti 11.4.2 e 11.5.2, non i dettagli qui implementati. Non sono attribuite alla Circolare prescrizioni non consultate.

La modalità riguarda il caso in cui non sia escluso il raggiungimento della capacità presso la testa. L'estensione minima nominale è 10D, con D diametro della sezione circolare piena. La coordinata x parte dalla testa fisica verso la punta: la presenza di tratto libero non sposta l'origine al piano campagna. Per un palo più corto di 10D si controlla l'intera lunghezza, dichiarando la convenzione numerica. Una lunghezza assegnata inferiore a min(10D,L) produce un esito non soddisfatto; una maggiore è ammessa. Il contatto profondo tra strati e la relativa zona di almeno 5D sono richiamati nelle informazioni, ma non sono dedotti né verificati da questa modalità di testa.

Checker applica nella zona As≥0,01Ac, staffe singole e s≤6φL,min; mantiene φst≥8 mm lungo tutto il palo. Fuori dalla zona controlla As≥0,003Ac e s≤8φL,min. Se un tratto uniforme interseca anche parzialmente la zona, i limiti geometrici più restrittivi valgono per quel tratto; l'utente può introdurre una partizione. L'uso del minimo diametro longitudinale in presenza di barre diverse è una scelta conservativa esplicitata dal software. I controlli di trave/pilastro selezionati dall'utente rimangono aggiuntivi.

In assenza di una valutazione specifica di duttilità, il testo prevede anche le condizioni implementate nella tabella seguente. Le condizioni che non sono limitate nel testo alla zona dissipativa sono controllate su tutta la lunghezza modellata.

| Controllo | Confronto | Campo e dati necessari |
| --- | --- | --- |
| Margine a taglio | 1,3 · abs(VEd) ≤ VRd | Tutto il palo; domanda sismica confermata e modello di taglio disponibile |
| Compressione media | max(0,N) · 1000 / Ac < 0,45 fcd | Zona dissipativa; N positivo a compressione in kN, Ac in mm², tensioni in MPa |
| Momento elastico | abs(Mel) < 1,5 MRd(N) | Tutto il palo; momento elastico non ridotto e N della stessa combinazione sismica |

La verifica del momento usa il ramo resistente corrispondente al segno locale e il valore di N alla stessa ascissa. Un punto fuori dominio, una resistenza assente o azioni non confermate non producono un esito favorevole. Le disuguaglianze per compressione e momento sono strette; quella per taglio ammette l'uguaglianza. La conferma q=1 è una dichiarazione sulla provenienza dell'azione elastica, non una trasformazione automatica di H o di M. Restano attive le verifiche ordinarie N–M–V: il limite 1,5MRd non viene usato per sostituire MEd≤MRd.

L'implementazione richiama le resistenze del verificatore esistente. La geometria della zona e i confronti risiedono in PileReinforcement.Seismic di Checker. ANTHEA aggiunge il confine alla discretizzazione, raccoglie gli input e rappresenta i risultati. Per le azioni sono esaminati i campioni FEM con gli estremi già recuperati dal motore; il punto di fine zona è presente esattamente. N, V e M restano concomitanti. Un controllo dello sviluppo non soddisfatto impedisce di presentare le sole resistenze nominali come dettaglio sismico verificato. Giunzioni, confinamento e nodo di testa non sono certificati da questi confronti.

I coefficienti effettivi gamma e alpha_cc vengono confrontati in Checker con il profilo NTC 2018; un profilo custom o unitario lascia i controlli di conformità da completare. Gli archivi precedenti non attivano implicitamente la modalità. Il dimensionamento discreto filtra i candidati con i minimi della zona e, dopo il calcolo delle resistenze, con i confronti sismici: non introduce un secondo motore.

Riferimenti indipendenti dei test: per D=1 m, Ac=785398,1634 mm² e As,min in testa=7853,9816 mm². 24Ø24 forniscono 10857,3442 mm² e passo limite 144 mm; 20Ø20 forniscono 6283,1853 mm² e non soddisfano il minimo di testa. Con fcd=20 MPa il limite di compressione è 9 MPa; N=1000 kN dà 1,2732395 MPa. V=100 kN richiede VRd almeno 130 kN. Con MRd positivo 200 e negativo −150 kNm, i limiti elastici nominali sono 300 e 225 kNm nei rispettivi versi, con uguaglianza non ammessa. Le prove coprono anche zona estesa su più tratti, palo corto, spirale, azioni mancanti, coefficienti custom, resistenze non disponibili e salvataggio. Evidenze in supporto/artefatti/palo-sisma-testa; sorgenti in supporto/test/ElasticPile.Checks e ElasticPile.UiChecks.

'''
assert marker in text
text=text.replace(marker,addition+marker)
theory.write_text(text.rstrip()+'\n',encoding='utf-8')
for name in ('README.md','installer/Indice-guide.md'):
    path=support/name;text=path.read_text(encoding='utf-8-sig').replace('Rev25','Rev26').replace('Revisione 25','Revisione 26')
    text+='\n- Revisione 26: controlli sismici di testa del palo secondo NTC 2018 §7.2.5, con fonte, campo di applicazione ed esiti nel verificatore.\n'
    path.write_text(text,encoding='utf-8')
path=support/'scripts/Build-AntheaGuides-Itec.py'
path.write_text(path.read_text(encoding='utf-8').replace("REVISION = '25'","REVISION = '26'").replace("REVISION_NOTE = 'TAGLI DEI TRATTI E SOVRAPPOSIZIONI DEL PALO'","REVISION_NOTE = 'DETTAGLI SISMICI DELLA TESTA DEI PALI'"),encoding='utf-8')
path=support/'scripts/Render-AntheaGuides-Itec.ps1'
path.write_text(path.read_text(encoding='utf-8-sig').replace("$Revision = '25'","$Revision = '26'"),encoding='utf-8-sig')
path=root/'lib/Checker/manifest.json';manifest=json.loads(path.read_text(encoding='utf-8-sig'))
manifest['source']+='; Concrete da working tree con zona dissipativa di testa e controlli semplificati NTC2018 §7.2.5, 5 ottobre 2026'
for entry in manifest['assemblies']:
    if entry['file']=='GPCChecker.Concrete.dll':entry['sha256']=hashlib.sha256((root/'lib/Checker'/entry['file']).read_bytes()).hexdigest().upper()
path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
subprocess.run([sys.executable,str(support/'scripts/wiki/build-wiki-index.py')],check=True)
art=support/'artefatti/guide_anthea_itec_rev26';art.mkdir(parents=True,exist_ok=True)
(art/'artifact.md').write_text('Aggiornare le due guide globali nel modello ITEC esistente. Descrivere i controlli sismici di testa NTC §7.2.5, distinguere dettagli geometrici e azioni confermate, documentare confronti, limiti e validazione. Conservare immagini e formule precedenti.',encoding='utf-8')
subprocess.run([sys.executable,str(support/'scripts/Build-AntheaGuides-Itec.py')],check=True)
for name in ('README.md','installer/Indice-guide.md'):
    subprocess.run([sys.executable,str(support/'scripts/documentazione/markdown-pdf.py'),str(support/name)],check=True)
