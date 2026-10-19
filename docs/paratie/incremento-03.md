# Paratie — incremento 3, verifiche idrauliche locali

## Funzioni implementate

Il profilo idraulico NTC 2018 di Model e `HydraulicSafety` in Geotechnics
verificano UPL da risultanti caratteristiche, HYD da gradiente medio o di uscita
e HYD su frontiera caricata. Fonte primaria verificata: NTC 2018 §6.2.4.2,
Tab.6.2.III, G.U. 20/02/2018, p.187. Il profilo è persistente/transitorio;
EN 1997-1 e situazione sismica restituiscono esplicitamente Non valutato.
Non vengono applicati coefficienti dei muri a gravità o coefficienti parziali
una seconda volta. L'eventuale resistenza Rd è già di progetto e richiede fonte
e descrizione del trattamento M2 e dei coefficienti pertinenti.

ANTHEA aggiunge corpi UPL, azioni caratteristiche e punti/colonne HYD nelle
tabelle, norma e situazione nella scheda Filtrazione, risultati e dettaglio
nella scheda UPL/HYD. Ogni esito conserva caso, fase, elemento, metodo, fonte,
coefficienti, domanda, capacità e utilizzo. Dati mancanti, condizioni di
applicabilità non dichiarate, campi assenti o campionamenti esterni al dominio
producono Non valutato. Identificativi duplicati o riferimenti a fasi inesistenti
sono errori espliciti.

Il campionamento e gli integrali sono in Geotechnics: integrazione esatta del
campo P1 lungo segmenti, controllo dei vuoti del dominio, gradiente medio lungo
una colonna e massimi dei gradienti negli elementi incidenti senza mediare
attraverso interfacce. UPL può utilizzare la pressione FEM sulla faccia inferiore
orizzontale, una pressione uniforme superiore e la larghezza ortogonale
esplicita del corpo. Le due pressioni correlate sono riunite in una risultante
prima di applicare il coefficiente. La scelta di trascurare la suzione è esplicita.

La migrazione 2→3 aggiunge tabelle vuote e conserva la saturazione dai livelli
di falda assegnati. L'opzione nuova «Pressioni FEM» usa γsat dove u>0 e γ naturale
altrove, con tutti gli attraversamenti di u=0 conservati dal profilo. Non è un
modello della frangia capillare o del peso in funzione del grado di saturazione.
La selezione richiede una soluzione FEM: non viene ignorata in modalità idrostatica.

Report DOCX/PDF e CSV leggono lo stesso snapshot dell'interfaccia. Modificare
azioni, controlli, criteri o dati idraulici invalida tutti i risultati interessati.
Le verifiche di sezioni e le verifiche idrauliche mantengono contratti separati;
nessuna terna fittizia N–M–V viene attribuita a HYD o UPL.

## Prove eseguite

- Model: 2 prove dei profili paratie/idraulici PASS (`hyd-safety-model-01`).
- Geotechnics: 30 test complessivi paratie/filtrazione/idraulica PASS;
  7 nuovi coprono UPL, i tre criteri HYD, campionamento, pesi da pressioni e
  annullamento prima della pubblicazione di un campo inizialmente equilibrato.
- Set coerente `candidate-08`: **3095 PASS**, 17 fallimenti Steel già ammessi,
  2 test ignorati nella suite Model. Nessuna lista dei fallimenti cambiata.
- Applicazione/MVVM: **62 controlli PASS**, ultimo `hyd-safety-app-02`.
  Attesi manuali UPL 18/23,4 kN; HYD medio utilizzo 1,2, uscita 0,8;
  frontiera caricata 20/15 kPa. Controllati fattori, unità, provenienza,
  migrazione, archivio, isolamento, invalidazione, profili indisponibili,
  campo fuori dominio e campo non convergente.
- Integrale FEM idrostatico: domanda UPL
  `(9,81×4−10)×2×1×1,1 = 64,328 kN`, capacità 90 kN;
  tolleranza assoluta 1e-8 kN. Gradiente idrostatico nullo entro 1e-10.
- Runner `20261019-093124-paratie-inc03-ui3`: build Release/UiTests,
  architettura e **14 controlli WPF PASS** (5 voci del runner, nessun rilievo).
  La prima invocazione con filtro errato non aveva selezionato alcuna suite:
  `inc03-ui-01` ha zero prove e non costituisce evidenza.
- PDF nativo `hyd-safety-app-02/paratie/upl-hyd.pdf`, tre pagine A4, tutte
  renderizzate e controllate (`hyd-safety-report-render-02`). Corretto un
  accostamento della categoria lunga alla forza usando G1/G2/Q. Viste UPL e
  Non valutato controllate nei temi chiaro/scuro. Nessuna attestazione della
  resa Word mediante LibreOffice.

Le tolleranze strette riguardano bilanci algebrici e campi affini esatti, non
una stima generale dell'errore FEM su geometrie arbitrarie. Gli attesi indipendenti
e le catture legacy non sono stati sovrascritti.

## Revisioni e rilascio

Model `838ff24c4323afd8e32525f74fd7c09e6e900fcf`, Checker
`cdc4913945932c31e5fdd60751349c9ffb3b2ef5`. Versioni principali Model 5.8.0.0,
Geotechnics 0.7.0.0; tutte le versioni del treno, commit e SHA-256 sono in
`candidate-08/release-set.json`. Creato con `Release-Set.ps1 -Candidate`,
validato da `Update-Snapshot.ps1 -AllowCandidate` in `snapshot-08`.
`lib/Checker` non è stato aggiornato; Delaunay conserva la stessa impronta.
Nessun push o merge eseguito. I merge richiesti a lavoro concluso sono registrati
in [integrazione-rami.md](integrazione-rami.md).

## Limiti e requisiti aperti

PAR-15 resta in corso: il campo di validità verticale e saturo va motivato,
le combinazioni delle azioni variabili sono assegnate, non generate; non sono
modellati erosione interna, sifonamento tridimensionale o criteri per qualunque
flusso obliquo. UPL non progetta automaticamente il tampone o le resistenze
laterali/degli ancoraggi. L'instabilità geotecnica del fondo rimane PAR-16.
La matrice complessiva, il collegamento alla stabilità globale, le funzioni
avanzate, le guide globali e il collaudo finale completo restano aperti.
