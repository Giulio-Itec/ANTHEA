# Fondazioni superficiali

## Stato del lavoro

Implementazione del 12 ottobre 2026 nel ramo `refactoring/f5-fondazioni-superficiali`; sorgenti del motore nel ramo `codex/fondazioni-superficiali` di Checker. Il modulo è registrato come `geo_fondazioni_superficiali` in Geotecnica. Non chiude le altre attività F5 e non elimina motori legacy.

L'insieme distribuito `lib/Checker` è invariato. L'API nuova non esiste nel suo manifest corrente. Il foglio consente preparazione e salvataggio, ma rifiuta il calcolo con un messaggio esplicito finché non viene installato il rilascio approvato. Il confine opzionale verifica `ContractVersion=1`, deserializza il DTO tipizzato della libreria e invoca esclusivamente quel motore: non contiene un algoritmo alternativo. La prova sorgente usa un AssemblyLoadContext separato nel test, senza modificare il percorso delle DLL di produzione.

## Architettura e funzionalità

- Checker: nuova API statica di portanza, separata dai muri; riuso dei coefficienti di `StandardNTC2018Geotechnics` in Model.
- `src/ANTHEA.FoundationWorkspace`: documento versionato, validazione del contenitore, conversione di unità, confine della libreria, presentazione e report Word/PDF; nessuna dipendenza WPF.
- `X.Calculations`: soltanto registrazione e delega. Nessuna formula nuova in X.Calculations, X.Core o X.Desktop.
- `X.Desktop/Wpf/FoundationWorkspace.cs`: campi condizionali, schema, azioni e combinazione, coefficienti e contributi, risultati invalidati alla modifica, aiuto contestuale. Salvataggio/riapertura usano l'archivio esistente; il report del progetto richiama il medesimo report del foglio.
- Wiki: capitoli originali nelle due guide globali, collegamenti bidirezionali, metadati, indice di ricerca e figura originale SVG/PNG. Il manuale Califfo rimane esclusivamente una fonte di lavoro non distribuita.

Le resultanti N/H/M sono già combinate al piano di posa, comprensive dei pesi. Nel drenato N e q sono efficaci; nel non drenato sono totali. Il foglio non genera le combinazioni e non pretende di identificarne l'inviluppo governante. La conferma A1 è esplicita. NTC 2018 usa M1 e R3; il confronto caratteristico non dà un esito normativo.

## Mappa teorica e differenze

La mappa completa delle API, delle fonti e dei riscontri è in `Checker/docs/metodi/fondazioni-superficiali.md`. Prima non esisteva un'API statica generale: si aggiunge una funzionalità, non si corregge indiscriminatamente il motore dei muri. Nessun errore accertato dei calcoli precedenti è stato trovato in questo perimetro. Tre difetti di sviluppo della nuova API sono stati corretti prima del rilascio: lato originale per l'avviso di eccentricità, overflow del risultato e convenzione non drenata con falda.

Varianti implementate: EC7 EN1997-1:2004/AC:2009 allegato D; Terzaghi con Nγ tabulato USACE; Meyerhof; Hansen normale con fattori correlati e ramo non drenato additivo; Vesić con dc correlato. Non si attribuisce validità universale alle osservazioni del manuale. Nessun confronto con il software di riferimento viene assunto come prova indipendente.

Limiti espliciti: terreno resistente omogeneo, piani orizzontali, statico; Terzaghi centrato su striscia/quadrato/cerchio e D/B≤1; Hansen normale; Meyerhof inclinato solo su striscia; cerchio centrato. Esclusi sisma, piani inclinati, compressibilità da indice di rigidezza, generazione combinazioni, cedimenti, scorrimento, stabilità globale e progetto strutturale. Per la compressibilità sono disponibili solo le riduzioni dei parametri Terzaghi e Vesić, con dominio dichiarato. Le lacune che richiedono fonti primarie ancora mancanti non sono state riempite con formule ipotetiche.

## Prove e consegna

Evidenze nella cartella non versionata `supporto/artefatti/fondazioni-superficiali/`:

- `checker/`: 108 test preesistenti e 10 nuovi test passati; attesi numerici indipendenti in Checker `tests/ShallowFoundation.Tests/independent.py`.
- `ci-standard/`: 45 PASS, 1 KNOWN già censito sui default dei muri, 0 NEW-FAIL; nessuna nuova deroga.
- `ci-quick/`: runner `build/ci.ps1`, 29 PASS, 0 nuovi fallimenti. Le esclusioni già previste dal runner restano registrate nel suo riepilogo.
- `final-candidate4/`: 26 controlli su unità, dati invalidi, archivio reale, API assente, API sorgente isolata, UI, risultati obsoleti, ricerca e aiuto Wiki, report Word/PDF e seconda esportazione nella stessa sessione. Ispezionati i temi chiaro, scuro e molto scuro; gli screenshot comprendono i risultati.
- `guide-qa/`: render delle edizioni globali e elenco delle pagine aggiunte. La pipeline Word mantiene 26 parti del template identiche e verifica tutti i frammenti e tutte le formule.

La nuova suite ANTHEA è nel runner (fast e ui); quella Checker è nel runner del treno di rilascio. La prova del candidato non sostituisce il collaudo del prodotto con il set approvato.

L'utente ha assegnato Geotechnics 0.1.2.0 e autorizzato merge/push Checker: la prima implementazione è su master e origin al commit 64575e3a. Il tentativo di set coerente ha completato le suite, ma è stato rifiutato dal controllo delle impronte/versioni. Le altre versioni proposte non sono autorizzate: l'utente ha chiesto di attendere le sue assegnazioni. Anche quattro DLL di Model cambiano impronta a parità di commit: la riproducibilità deve essere chiarita prima del prossimo rilascio. Nessuno snapshot è stato installato.

Passi residui: integrare le estensioni del ramo `codex/fondazioni-visualizzazioni`, versioni assegnate dall'utente, set coerente da sorgenti pushati e tutte le suite, installazione esclusivamente con `tools/libs/Update-Snapshot.ps1 -ReleaseSet`, manifest/README e runner full. Nessun prerequisito viene aggirato.

Controllo delle guide Rev33: 185 pagine con corpo graficamente identico alla Rev32; le restanti pagine cambiate e le nuove sono state ispezionate. Il confronto testuale esclude variazioni fuori da copertina, indice e aggiunta finale. Le Rev32 e i precedenti sorgenti canonici sono archiviati con impronte in `supporto/SUPERATI/fondazioni-superficiali-rev33/`.

## Estensione con profili e viste del 12 ottobre

La gestione terreno riutilizza direttamente `GPC.Model.Geotechnics.Soil`, `SoilLayer` e `SoilProfile`, già usati dal palo verticale. Non viene introdotta una seconda classe di strato in Checker. L'adattatore senza WPF traduce le chiavi canoniche `stratigrafie`, `spessore`, `tipologia`, `peso_specifico`, `peso_specifico_saturo`, `angolo_attrito`, `coesione_efficace` e `coesione_non_drenata` in Model. La tabella è lo stesso componente `StratigraphyTable` del palo; γsat vuoto segue γ, senza modificare il dato salvato. La falda usa `generali/presenza_falda` e `generali/profondita_falda`.

Il profilo esaminato è esplicito. Checker integra le tensioni del ricoprimento attraverso Model e sceglie il terreno immediatamente sotto la base. Proprietà eterogenee sotto la base, profilo insufficiente o assenza dell'ipotesi di semispazio arrestano il calcolo. Il prolungamento come semispazio è una dichiarazione del modello, non una misura dell'indagine. I vecchi dati a due zone restano migrabili senza perdere i valori originali. Il documento passa da versione 1 a 2: il vecchio lettore rifiuta un profilo comune nuovo invece di calcolare i parametri a due zone inattivi.

Il CLS usa catalogo GPC e chiavi condivise `sezione/fck_mpa`, `classe_cls`, `materiale_cls_nome`, `cls_diagramma`. È descrittivo: nessuna nuova formula del CLS, verifica strutturale o aggiunta automatica del peso. Progetti, trasferimento terreno e piano del report riconoscono il nuovo modulo. Azioni e risultati non si propagano come materiali; nel report non vengono ripetuti i vecchi parametri terreno inattivi.

La pagina unica mantiene input, strati ed esito; sei schede interessano soltanto il riquadro grafico. Gruppi richiudibili e Raccogli input ampliano il grafico; lo stato della vista è salvato senza invalidare il risultato. La mappa di contatto risolve p=max(0,a+bx+cy) in Checker con integrazione esatta del poligono e residuo normalizzato inferiore a 1e-11. La zona compressa è distinta da A′. Lo schema di Prandtl è dichiarato illustrativo, peso del terreno nullo: non si spaccia per superficie critica del metodo scelto o soluzione 3D. Fonti e limiti sono nelle guide e nel documento Checker.

Nuove prove: 23 test Checker complessivi, 67 controlli adattatore/Progetti/UI, mappe centrate ed eccentriche, sei viste e tre temi. Gli attesi della portanza precedente restano invariati; confronto e schermate in `supporto/artefatti/fondazioni-superficiali/visualizzazioni/`. La prova sorgente condivide il Model installato, poiché il suo contratto SoilProfile è già presente; la DLL Geotechnics candidata è isolata e non sostituisce quella distribuita. Guide globali aggiornate alla Rev34.


## Collaudo finale dell’estensione

- Runner standard `visualizzazioni/ci-standard-finale`: 45 PASS, 1 KNOWN già censito sui default dei muri, 0 NEW-FAIL e nessun file modificato dalle suite.
- Dopo il controllo finale della migrazione D = 0 scritto come `0`, `0.0` o `0,00`, runner mirato `visualizzazioni/ci-foundation-finale`: build, controlli documento e WPF tutti PASS. La migrazione non crea un ricoprimento di spessore nullo; gli input incompleti restano modificabili.
- `visualizzazioni/qa-consegna`: 67 controlli con la libreria candidata isolata, sei viste, tre temi, casi centrati ed eccentrici e comandi Raccogli/Mostra input. Risultato conservato durante il solo cambio del layout. Ultima correzione grafica: il cerchio non espone vertici del rettangolo circoscritto come campioni di pressione.
- Checker: 23/23 prove fondazioni, 108/108 regressioni geotecniche preesistenti. Attesi indipendenti immutati.
- Guide Rev34: pratica 68 pagine, teoria 140; 332 formule Word modificabili nella teoria, 26 parti del modello ITEC identiche. Confronto Rev33: 183 pagine con corpo graficamente identico; ispezionate tutte le pagine cambiate e i capitoli aggiunti. Nessuna differenza testuale precedente ai capitoli aggiornati fuori da copertina e indice. Report del foglio: entrambe le pagine controllate. Rev33 archiviate con manifest in `supporto/SUPERATI/fondazioni-superficiali-rev34/`.

Diagnosi di riproducibilità prima del prossimo rilascio: il confronto dei PDB del set precedente e del tentativo rifiutato identifica 17 sorgenti GPCModel e un sorgente Persistence con impronte diverse spiegate esattamente dai fine riga LF/CRLF. Tutti i corpi IL dei metodi coincidono (GPCModel 7543, ModelData 361, Persistence 28, Converter 1450). Evidenze `visualizzazioni/model-pdb-line-endings.json`, `model-other-pdb-line-endings.json` e `model-il-comparison.json`. Questo riscontro non autorizza l’installazione né sostituisce il confronto binario di una nuova build da checkout coerenti: il controllo SHA-256 rimane obbligatorio.

## Rilascio S e contratto 2 — 12 ottobre 2026

Geotechnics 0.1.3.0 pubblicata nel set coerente S attiva il modulo tramite il contratto 2. Il controllo del contratto 1 avrebbe disabilitato il foglio: corretto e fissato con test obbligatorio sul rilascio distribuito (FON-03). Nessuna formula aggiunta ad ANTHEA.

Azioni oltre il dominio resistente conservano resistenza nulla, esito e avvisi della libreria; pressione agente e indice indefiniti restano nulli e sono rappresentati con un trattino. Le viste non disponibili hanno un messaggio esplicito. I coefficienti additivi Hansen sono separati dai moltiplicatori e condivisi tra UI e report. Guide globali allineate alla Rev36; revisioni precedenti archiviate.

Runner mirato: 44 controlli funzionali e 86 WPF passati. Verificati esempio EC7 indipendente, risultante esterna, Hansen non drenato, API assente, esportazioni Word/PDF e tre temi. Evidenze: `supporto/artefatti/ci/20261012-165204-release-s-foundation-contract2`. Collaudo completo del set: 88 PASS / 1 KNOWN preesistente / 0 NEW-FAIL; baseline 6 PASS, nessuna differenza non ammessa su 548 file e 50996 righe dense. Nessun atteso o tolleranza modificato. Guide Rev36: 72 e 140 pagine, 332 formule modificabili e 26 parti del modello ITEC conservate per guida; controllati tutti i testi e la resa delle pagine cambiate. Evidenze complete in `supporto/artefatti/ci/20261012-170114-release-s-full`, `20261012-165624-release-s-baseline` e `supporto/artefatti/guide_anthea_itec_rev36`.

## Hansen con carico inclinato — 1.16.0 (29 ottobre 2026)

GPCChecker.Geotechnics 0.26.0.0 calcola Brinch Hansen anche con azione orizzontale, drenato (manuale di riferimento 3.23–3.25) e non drenato (3.27), nelle direzioni di B′ e di L′ con la minore; prima la libreria rifiutava il caso e il foglio mostrava l'errore. Formule, fonti e discontinuità con il carico normale sono nella pagina di metodo di Checker (`docs/metodi/fondazioni-superficiali.md`, sezione «Hansen con carico inclinato»). Nessuna formula in ANTHEA.

Nel foglio cambia solo l'etichetta del metodo, da «Brinch Hansen · carico normale» a «Brinch Hansen»; i documenti salvano l'indice, quindi restano invariati. La relazione riporta riferimento e avvisi della libreria: qlim nelle due direzioni, direzione che governa, qlim con H = 0 e discontinuità dichiarata. La tabella delle correzioni additive mostra quelle della direzione che governa. Il risultato non ha campi nuovi: impronte delle fondazioni cambiate solo per la versione della libreria.

Prove: `FoundationWorkspace.Checks` confronta Hansen drenato e non drenato con H = 60 kN con gli attesi indipendenti di Checker (`califfo-reference.json`, casi 426 e 459) e controlla relazione e tabella; le tracce della vista storica cambiano solo nell'etichetta (registro FON-HANSEN-01).
