# Rilascio coerente F5: paratie e spettri

Il 19 ottobre 2026 l'utente ha autorizzato il push delle due revisioni già
integrate e collaudate, l'aggiornamento ufficiale di `lib/Checker` e il profilo
full. Successivamente ha autorizzato anche versione, commit, push di ANTHEA
su F5 e installer, con le deroghe ambientali esplicite riportate sotto.
I nuovi rami di prosecuzione restano locali.

## Revisioni pubblicate e set

- Model/master: `e1e6e1079fb7b90b4ca1fee60e01d05a1b0423d9`.
- Checker/master: `dfb808c8ab5463b0df0ab965813320ddc38eb5f0`.
- Utilities: `e57b440b16a0a311a7bf0af2f9cf4d2681b83491`, già pubblicata.
- Geometry: `6734eee1004142576b96759e34c15dec48776e23`, già pubblicata.

Fetch e verifica della discendenza prima dei due push; conferma di entrambi
gli SHA con `git ls-remote`, senza force push. Nuovo set non candidato:
`supporto/artefatti/paratie/rilascio-f5/gpc-release-01`, prodotto con
`Checker/build/Release-Set.ps1 -Root ... -Previous lib/Checker/release-set.json`.
La copia dei metadati del rilascio Z precedenti all'installazione è in
`rilascio-f5/precedente-release-z/`; il riferimento F3 rimane congelato.

Suite effettivamente rieseguite: 3172 PASS, soltanto i 17 fallimenti Steel
storici ammessi; due test Model nativi ignorati. Model Core 18, UnitTest 1249,
Concrete 795, Steel 88 PASS, CompositeBridge 284, Geotechnics 191,
Calculation 174, ShallowFoundation 28, EmbeddedWalls 67, Steel.Members 13,
Design 91, ModelChecker 174. Compatibilità API superata.

Audit indipendente: 14 DLL identiche byte per byte alla candidata integrata,
versioni aumentate rispetto a Z per ogni DLL cambiata; Utilities, Geometry
e Delaunay identiche a Z. Nessun Gmsh nel set. Installazione con
`tools/libs/Update-Snapshot.ps1 -ReleaseSet ... -Staging ... -Install`;
13 DLL verificate anche dopo la copia in `lib/Checker`.

Impronte SHA-256:

- Set completo: `b07095e0b1585bc0f6817e7380f8590ba0eb9fce0f734ad4eeed8bc3ca87b573`.
- Manifest installato: `397e9315c536de3954fc59b86f4589505e874343dcb341e1145b69c49573ac40`.

Il registro di provenienza F4 punta ora al rilascio AA: cambiano soltanto
nome, impronte del set e nota. Input, attesi numerici, transizioni F3/F5,
tolleranze e liste dei fallimenti ammessi restano invariati.

## Collaudo ANTHEA e pacchetto

Il profilo full è stato eseguito interamente sullo snapshot installato;
gli esiti effettivi e le riesecuzioni sono riportati nella chiusura sotto.
Il confezionatore locale accetta `-PublishedLibraries` soltanto con un
full completo sulla revisione pulita, tutti gli stadi eseguiti, set non
candidato e sorgenti pubblicate; copia anche il manifest del set completo.
Questo controllo non è stato allentato: la 1.11.0 usa il flusso NSIS esistente,
con le deroghe documentate. La matrice paratie rimane aperta.

La candidata precedente `candidata-integrata-01` resta conservata e
utilizzabile. I nuovi rami `refactoring/f5-paratie-seguito` e
`codex/paratie-seguito` non sono stati pubblicati o reintegrati nei master.
L'approfondimento di pali/micropali resta in coda al modulo paratie.

## Rilievi del primo full e versione 1.11.0

Il primo full su `e39741c9` ha rilevato 155 differenze F3: 16 versioni
Geotechnics e le relative 16 transizioni non riconosciute; 123 segnalazioni
editoriali (122 differenze effettive e la vecchia transizione del conteggio
articoli). Il dettaglio originale è troncato a 20000 voci: confronto Wiki
separato conservato in `rilascio-f5/diagnostica-f3-01/`.

Il driver F3 delle fondazioni usa ora la proiezione di sola provenienza già
collaudata in Presentation, verificando prima la versione runtime. Il
controllo della vista storica usa la proiezione F5; la modalità di cattura
conserva la versione effettiva. DOCX/PDF prodotti conservano i metadati reali.

Il file editoriale `transitions-f5-wiki.json` registra soltanto le differenze
esatte dei nuovi articoli: quattro file dei due capitoli paratie, 115
posizioni delle ricerche, due righe vuote finali e il conteggio 75 → 77.
Verifica indipendente: rimuovendo soltanto i due ID paratie dai 30 elenchi,
ogni ricerca storica è identica, compreso l'ordine. I due corpi Markdown
storici differiscono esclusivamente per una riga vuota finale. Il registro
F4 e gli attesi sono immutati; il compositore può sostituire solo il conteggio
editoriale e non accetta transizioni numeriche del contenuto, file rimossi
o percorsi esterni alla Wiki. Testing.Tests: 13 controlli delle transizioni
superati, compresi cinque controlli aggiunti. WPF finale ancora da eseguire.

L'utente autorizza anche commit, nuova versione, push ANTHEA e installer.
Versione 1.11.0 per le nuove funzioni integrate; istruzioni installer
aggiornate con PDF di tre pagine controllato visivamente e originali
conservati in `supporto/SUPERATI/installer-1110-20261019/`. La suite della
vista palo verticale si è interrotta senza attestazione: causa ancora da
isolare; il rilascio non è dichiarato concluso.

La diagnosi successiva identifica due eventi Trend Micro `NON_POLICY_JUST_ADC`
con il percorso univoco della corsa e dei test palo verticale/muri:
`rilascio-f5/diagnostica-protezione/trendmicro-eventi-full01.txt`.
La protezione non viene modificata; non si cambiano esportazioni o test per
evitarne l'intervento. Il desktop è a 144 DPI, come rilevato anche dal
controllo Spettri (45 catture, zero difetti), mentre il confronto generale
richiede 96 DPI. L'utente autorizza il rilascio della 1.11.0 documentando
entrambi i vincoli; restano tre prove da completare nell'ambiente corretto.

Verifiche della preparazione su `9994ded5`, revisione pulita e invariata:
`ParatieWork/ANTHEA/supporto/artefatti/ci/20261019-225804-f5-release-1110-fast-01`,
nove PASS (architettura, Wiki, Testing, indici e build). Build UiTests
indipendente conclusa senza errori. Anche il compositore C# effettivo,
applicato alla copia diagnostica della cattura, riconosce tutte le 22259
transizioni esatte e zero differenze non ammesse; non sostituisce la nuova
cattura WPF ancora da eseguire.

## Chiusura della versione testabile 1.11.0

Il full `ci/20261019-220735-f5-release-aa-full-01` su `e39741c9`
si è concluso con **160 PASS e 5 NEW-FAIL**, revisione pulita e nessuna
modifica tracciata durante la corsa. I cinque esiti originali sono conservati:
due confronti di provenienza/editoria corretti nei driver e tre prove aperte
per i vincoli autorizzati. Il contrasto ha zero difetti in Scuro e Molto scuro;
Chiara rileva 698 occorrenze in 184 voci, non confrontabili con la baseline
di 699 a 96 DPI perché la corsa è a 144 DPI. Non è un PASS del full.

Le correzioni sono state riportate su F5 nei commit `7684b993` e `f82d7ae3`.
Sul secondo, pulito e invariato, il runner filtrato
`ci/20261019-233545-f5-release-1110-final-01` ha **18 PASS** e due errori
ambientali dei test installer. F3 supera 104 fixture, 77 articoli e 30
ricerche; le fondazioni conservano 15 scenari e 8198 righe congelate.
Passano anche viste fondazioni/paratie/spettri, contrasto Spettri,
architettura, Testing, Wiki, indici e assenza di codice di test in Release.

I due errori installer dipendevano da `PSModulePath`: il runner avviato da
PowerShell 7 passava a Windows PowerShell 5 i moduli della versione 7,
rendendo indisponibile `Get-FileHash`. Riproduzione separata confermata;
nessuna modifica a sorgenti, protezioni o attesi. Riesecuzione con Windows
PowerShell 5 e percorso dei moduli coerente:
`ci/20261019-234146-f5-release-1110-installer-02`, **2 PASS, 0 NEW-FAIL**,
26 controlli di sicurezza e 30 sulle guide. Stesso commit pulito e invariato.

Installer self-contained win-x64 costruito da
`f82d7ae35b67907d3fbc026b8b924da7b6618d3e`, identificato dal tag `v1.11.0`.
Rispetto al full, il codice distribuito cambia soltanto il numero di versione;
le altre modifiche riguardano driver, registro editoriale e documentazione.
Audit del setup estratto: 325 file applicativi identici allo staging,
tre PDF Rev52 e tutte le 13 DLL uguali al manifest AA; nessun Gmsh o assembly
di test. Nove esempi forniti anche in ZIP, identici ai file collaudati.

[Installer, impronte e limiti](../refactoring/installer-1.11.0.md).
La consegna non chiude la matrice né la richiesta sui pali interrati.
