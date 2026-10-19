# Incremento 7 — sezioni tubolari della parete in Checker Steel

Le azioni N–M–V di ogni taglio della paratia vengono trasferite da Design a
Checker Steel conservando caso, fase, sezione e lato. Model conserva geometria
CHS e materiale; ANTHEA gestisce dati, unità, risultati e documenti.

## Campo implementato

NTC 2018, costruzioni ordinarie, SLU statici, acciaio strutturale S235–S460,
tubi CHS integri di classe 1–2. Resistenza locale della sezione: taglio,
sforzo normale e flessione con N e V concomitanti. La capacità N–M deriva
dall'equilibrio plastico esatto dell'anello; oltre metà della resistenza a
taglio, la riduzione della tensione resistente è applicata cautelativamente
a tutta la sezione. Non si dichiara la massima capacità possibile N–M–V.

Fonte primaria: NTC 2018 §4.2.4.1.2 e tab.4.2.V/VII, G.U.20/02/2018,
capitolo 4. Conservato `fonti/ntc2018-cap4.pdf`; pagine PDF 29, 31–33
(stampate 97, 99–101) controllate visivamente, oltre alla classificazione
già esaminata nell'incremento 5. Formule, unità, ipotesi e riferimenti sono
in `Checker/docs/metodi/sezioni-tubolari-paratie.md`.

Per una fila unica di tubi identici, interasse s e fascia b, Design fornisce
EI = E I b/s e trasferisce tutte e tre le azioni con s/b una sola volta.
La trave usa N positivo a compressione; il risultato Steel usa N positivo a
trazione. Ripartizione uniforme e trasferimento delle pressioni attraverso
il fronte richiedono una giustificazione esplicita. Nessuna equivalenza
implicita per più file, effetto arco o risposta tridimensionale.

La verifica plastica locale non introduce cerniere nel modello elastico
strutturale. Classi 3–4, altre famiglie, instabilità della membratura,
pressioni radiali/ovalizzazione, torsione, collegamenti, sisma e altri
profili normativi restano da implementare. Dati o campo non supportati
producono Non valutato; una storia incompleta non viene certificata.

## ANTHEA

Schema 7: tabella `steel_tubes`, scelta EI assegnato/da tubo CHS e profilo
delle verifiche. La migrazione 6→7 mantiene EI assegnato e verifiche
disattivate. Tabella WPF con azioni fisiche concomitanti, fattore di
conversione, classe, coefficienti e controlli del taglio selezionato.
Persistenza, invalidazione, CSV a 23 colonne e report condividono lo stesso
risultato. Il PDF sintetico seleziona tagli terminali e governanti senza
ricombinare massimi; CSV e opzione estesa conservano tutti i tagli.

## Prove effettivamente eseguite

- Prima del kernel, `Checker/tools/ParatieTubeExpected.py` ha creato una
  nuova fixture indipendente: integrazione Simpson delle calotte su 16384
  intervalli, asse neutro assegnato. La produzione usa formule analitiche e
  inversione di N. Sei casi e tutti i segni: flessione pura, taglio basso,
  soglia, elevato, quasi limite assiale e classe 2. Tolleranze relative
  2e-10 e assolute 1e-7 nelle rispettive unità; attesi storici invariati.
- `tube-steel-03`: 13 test PASS (nove assiali precedenti e quattro nuovi).
  `tube-design-02`: due test PASS, mensola analitica e trasferimento di
  tutti i tagli. Compresi capacità nulle, campo non supportato, isolamento
  e annullamento. Corretto un conflitto di nome locale intercettato nella
  prima compilazione; conservati i log dei tentativi.
- `candidate-14`: 3134 PASS, stessi 17 fallimenti storici Steel ammessi,
  API compatibili. Model `63b69d39e82d33aeb2c8165c2eb271b042b1fb9c`, Checker
  `b95ba56645746999ece48c2c892af05f48e248de`. Model 5.12.0.0, Steel 2.2.0.0,
  Design 0.25.0.0; versioni e SHA-256 completi nel manifest. Staging
  integrale `snapshot-14`, nessuna installazione in `lib/Checker`.
- `tube-app-02`: 303 controlli applicativi/MVVM PASS, incluse migrazione,
  riapertura, isolamento, annullamento, invalidazione, unità, provenienza,
  geometria/EI e coerenza CSV/report. Mensola di 6 m, fascia 1.5 m,
  interasse 2 m, CHS 200×8: EI circa 3508.225 kNm² e spostamento di testa
  circa 4.10464 mm per forza trasversale di 0.2 kN.
- `ci/20261019-121547-paratie-inc07-ui2`: build Release/UiTests,
  architettura e 31 controlli WPF PASS. Controllate le tre nuove schermate:
  chiaro, scuro e classe 3 non valutata. Il dettaglio identifica il taglio
  selezionato anche quando la riga è fuori dall'area visibile della tabella.
- `tube-app-02/paratie/sezioni-tubolari.pdf`: tre pagine A4 renderizzate e
  controllate in `tube-report-render-02`. Precisate le diciture delle
  verifiche in c.a. per distinguerle da quelle Steel. PDF nativo ANTHEA;
  non viene attestato un rendering Word non eseguito.

Gli artefatti sono in `supporto/artefatti/paratie/`, salvo le corse CI
indicate. Regressione standard `ci/20261019-122147-paratie-inc07-standard`
sul commit `f52db8d832abe20daa92f48027c1d404520b43a6` e `snapshot-14`:
**74 PASS, 0 KNOWN, 0 NEW-FAIL, 0 BLOCKED**, sorgenti ANTHEA invariati.
Non eseguite le tre suite infrastrutturali dichiarate: ElasticPile.Checks,
BridgeValidationCurrent (coperte dai runner GPC), ElasticPile.Performance.
Guide globali, matrice completa, rilascio e merge restano aperti.
