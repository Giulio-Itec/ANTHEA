# Incremento 5 — sostegni assiali in acciaio

Il verificatore è in `GPCChecker.Steel`, il trasferimento delle azioni in
`GPC.Design`. L'incremento copre aste circolari piene e tubolari CHS in acciaio
strutturale, soggette a forza assiale centrata, agli SLU statici delle costruzioni
ordinarie secondo NTC 2018. La matrice completa rimane aperta: questo percorso
non chiude le verifiche N–M–V delle sezioni della parete, le altre famiglie di
sezioni, i collegamenti, gli ancoraggi o i bulbi.

## Metodo e trasferimento

Model conserva uno snapshot indipendente delle proprietà della sezione,
materiale, area netta e lunghezze libere d'inflessione, con le rispettive fonti.
Steel calcola snervamento, eventuale rottura della sezione netta e instabilità
flessionale nei due piani. Sono esposti domanda, capacità, utilizzo, classe,
coefficienti, curve, forza critica, snellezze e riduzione. La resistenza del
materiale per lo spessore del prodotto resta distinta dal grado nominale.

Un dato obbligatorio mancante, una CHS di classe 4, un profilo non supportato o
un'azione non dichiarata di progetto producono Non valutato. Il puntone scarico
mantiene i controlli di compressione: N=0 non nasconde una lunghezza libera
d'inflessione assente. I percorsi storici di Steel non sono modificati: le aree
nette/efficaci non vengono sostituite con le aree lorde dei loro segnaposto.
Metodo e fonte primaria verificata sono documentati in
`Checker/docs/metodi/sostegni-acciaio-paratie.md`.

Design conserva combinazione, fase, sostegno e forza fisica del singolo elemento.
Non ripete fattori parziali, proiezioni o conversioni per interasse. La storia
meccanica deve essere completa e convergente. Il raggiungimento del limite
plastico del sostegno resta visibile e non equivale alla verifica dell'intero
meccanismo terreno–ancoraggio.

## Differenza numerica corretta

La forza costitutiva è positiva a trazione per Anchor/Bilateral, positiva a
compressione per Strut. Il recupero precedente usava lo stesso segno per la
componente verticale dei due tipi. Ora `NormalForce`, positiva a trazione,
governa il trasferimento alla parete e al verificatore Steel. Con 20 kN,
inclinazione ±30° e rapporto fascia/interasse 1,5/2, il contributo è ±7,5 kN,
con segno opposto fra tirante e puntone.

La cattura preventiva `steel-projection-before` conserva due casi superati e
due fallimenti dei puntoni; `steel-projection-after` comprende tutti i 43 test
Geotechnics superati. Nessun atteso storico o tolleranza è stato modificato.

## Applicazione, risultati e persistenza

Schema documento 5: tabella `steel_members`, profilo e tipo di verifica espliciti.
La migrazione lascia inattive le nuove verifiche nei documenti precedenti.
Il risultato e l'impronta includono Steel. Editor e scheda Acciaio sostegni
mostrano forze e controlli per fase; modifica dei dati, annullamento e cambio di
risultato invalidano anche selezione ed esportazioni. Report e CSV usano lo
stesso snapshot dell'interfaccia, senza ricalcoli.

Steel è aggiunto al catalogo del set coerente ANTHEA e ai riferimenti dei
progetti pertinenti. Lo staging deriva dall'intero set candidato: nessuna DLL
isolata è stata copiata in `lib/Checker`, che resta invariata.

## Evidenze concluse

Artefatti sotto `supporto/artefatti/paratie/`:

- Attesi indipendenti prodotti prima del kernel da
  `Checker/tools/ParatieSteelExpected.py`, con la sola libreria standard Python
  e geometria analitica. File creato senza sovrascrittura. Cinque casi di
  trazione/compressione, foratura, formazione e grado S460; tolleranza relativa
  2e-12, assoluta minima 1e-9 per formule chiuse in double.
- `steel-members-02`: nove test del nuovo percorso Steel superati, inclusi
  input non supportati, isolamento e puntone scarico; `steel-design-01`: due
  test Design sul trasferimento per fase, segno, interasse e annullamento.
- `candidate-11`: 3118 test superati e soltanto i 17 fallimenti storici Steel
  ammessi. Staging rigenerato integralmente in `snapshot-11-steel` dopo avere
  aggiunto Steel al catalogo; il primo staging senza Steel non è usato per
  dichiarare verificata l'integrazione.
- Le prime compilazioni applicative hanno individuato il riferimento Steel
  mancante nel progetto e poi nel catalogo. Corretti entrambi; `steel-app-03`
  e `steel-app-04`: 102 controlli applicativi/MVVM superati.
- Runner `ci/20261019-105907-paratie-inc05-ui2`: build Release/UiTests,
  architettura e 23 controlli WPF superati. La corsa precedente con solo stadio
  UI aveva usato il vecchio eseguibile e non vale come prova di questo incremento.
- PDF nativo `steel-app-04/paratie/sostegni-acciaio.pdf`: tre pagine A4,
  renderizzate in `steel-report-render-02` e controllate tutte. Tabelle e
  provenienza coincidono con lo snapshot; duplicazioni testuali ridotte senza
  cambiare i risultati. Vista chiara/scura e caso di puntone non verificato
  controllati nelle schermate WPF. Nessuna attestazione di rendering Word.

- Ripetizione finale sul set `candidate-12`, Model `27fb56b5` e Checker
  `e06845b3`: **3119 test superati**, soltanto i **17 fallimenti Steel già
  ammessi**, API compatibili. Il set comprende Steel 2.1.0.1, Design 0.23.0.1,
  Geotechnics 0.8.0.3 e Model 5.10.0.0; tutte le versioni e SHA-256 sono nel
  manifest. Staging integrale `snapshot-12`, senza installazione ufficiale.
- `steel-app-05`: **103 controlli applicativi/MVVM superati**, incluso il
  puntone scarico con dati d'instabilità mancanti. Runner
  `ci/20261019-111149-paratie-inc05-ui3`: nuove build Release/UiTests,
  architettura e **23 controlli WPF superati** sullo stesso set candidato.

- Regressione `paratie-inc05-standard`, sul commit ANTHEA `03afcfa4` e
  `snapshot-12`: **74 PASS, 0 KNOWN, 0 NEW-FAIL, 0 BLOCKED**. Inclusa
  Presentation.Tests (329.2 s), con sorgenti ANTHEA invariati durante tutta
  l'esecuzione. Tre esclusioni infrastrutturali dichiarate: ElasticPile.Checks,
  BridgeValidationCurrent (coperte nei runner GPC) ed ElasticPile.Performance.

Guide globali, rilascio e funzioni residue della matrice non sono dichiarati
conclusi da queste evidenze.
