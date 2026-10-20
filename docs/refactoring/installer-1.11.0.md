# Installer ANTHEA 1.11.0 — 19 ottobre 2026

Versione testabile dei moduli Paratie e Spettri integrati in `refactoring/f5`.
La matrice delle paratie resta aperta; non sono approvate esclusioni funzionali.
L'approfondimento della testa di pali e micropali rimane in coda al modulo.

## Revisione e pacchetto

- Commit della build: `f82d7ae35b67907d3fbc026b8b924da7b6618d3e`, albero pulito.
- Tag: `v1.11.0`, riferito esattamente al commit della build.
- Versione file: `1.11.0.0`; prodotto: `1.11.0+f82d7ae35b67907d3fbc026b8b924da7b6618d3e`.
- Script: `supporto/installer/Crea-Installer.ps1`; NSIS, self-contained win-x64.
- File: `supporto/artefatti/paratie/rilascio-f5/installer-1.11.0/ANTHEA-1.11.0-Setup-x64.exe`.
- Dimensione: **68914134 byte**.
- SHA-256: `9064DA5D2189D77E0738C2919DB721E42F8E1CAB646F7880C9E9BF8FDFA50654`.
- Esempi: `ANTHEA-1.11.0-Esempi.zip` nella stessa cartella;
  SHA-256 `2775E28E6330C97274107746CC2A16642D412BE2AD88E49F752BCD0AF675F68D`.

La chiusura documentale successiva non cambia il codice del pacchetto: il tag
identifica la build, il ramo contiene anche il presente verbale. Il setup e
gli output di prova sono locali, esclusi da Git. L'installazione esistente
dell'utente non è stata sostituita per il collaudo.

## Verifiche eseguite

| Corsa | Revisione | Esito effettivo |
| --- | --- | --- |
| `ci/20261019-220735-f5-release-aa-full-01` | `e39741c9` | full completo, 160 PASS e 5 NEW-FAIL |
| `ci/20261019-233545-f5-release-1110-final-01` | `f82d7ae3` | 18 PASS; due errori di ambiente nei test installer, poi risolti |
| `ci/20261019-234146-f5-release-1110-installer-02` | `f82d7ae3` | 2 PASS, 0 NEW-FAIL; 26 controlli sicurezza e 30 guide |

Tutte le corse partono da un albero pulito e non modificano file tracciati.
I due errori del primo full nei confronti F3/fondazioni sono chiusi: 104
fixture, 77 articoli e 30 ricerche F3 superati; 15 scenari e 8198 righe delle
fondazioni uguali alle tracce congelate. Nessun atteso numerico, tolleranza,
transizione ingegneristica F4 o elenco dei fallimenti ammessi modificato.
Rispetto al codice distribuito sottoposto al full cambia solo la versione
applicativa; le altre modifiche sono nei test e nella documentazione.

I test installer erano avviati da PowerShell 7 con `PSModulePath` ereditato
dal processo Windows PowerShell 5: `Get-FileHash` non era disponibile.
Il difetto ambientale è stato riprodotto e risolto per la corsa usando Windows
PowerShell 5 con i suoi percorsi dei moduli. Nessuna modifica al prodotto.

Audit indipendente del setup estratto con 7-Zip: **325 file applicativi**
identici allo staging, **tre PDF Rev52** identici agli originali e **13 DLL
GPC** identiche al manifest ufficiale. Assenti Gmsh e assembly di test;
`Assert-NoTestCode.ps1` superato. File `audit-payload.json`, `SHA256SUMS.txt`,
manifest GPC e log della compilazione/estrazione accanto al setup.
Le guide globali conservano le 257 pagine e 387 formule già verificate;
indice incluso. Le istruzioni installer hanno tre pagine PDF controllate.

Il set AA deriva dai master pubblicati Model `e1e6e107` e Checker `dfb808c8`:
3172 PASS, soli 17 fallimenti Steel storici censiti e due test Model nativi
ignorati. Delaunay, Geometry e Utilities invariate. La pubblicazione non
include il nuovo kernel del fondo scavo presente nei rami di prosecuzione.

## Tre prove aperte con autorizzazione esplicita

L'utente ha confermato «Rilascia documentando entrambi i vincoli»:

1. `ui/check-palo-verticale-view` e `ui/check-muri-view`: interrotte da Trend
   Micro. Due eventi `NON_POLICY_JUST_ADC` con i percorsi della corsa sono
   conservati in `rilascio-f5/diagnostica-protezione/trendmicro-eventi-full01.txt`.
   L'amministratore deve esaminare le segnalazioni prima della riesecuzione.
2. `ui/check-contrast`: 144 DPI rilevati, baseline a 96 DPI. Zero difetti in
   Scuro e Molto scuro; 698 occorrenze in Chiara, senza confronto valido con
   la baseline di 699 a diversa risoluzione. Va ripetuto a 96 DPI.

Non è un full interamente superato. Nessuna protezione disabilitata e nessuna
baseline modificata. Il confezionatore portatile `New-LocalPreview.ps1`
mantiene il requisito del full PASS per `-PublishedLibraries`; non è stato
usato né modificato per aggirarlo. Questa consegna usa il flusso installer
esistente e la deroga documentata.

## Contenuto testabile e seguito

Analisi per fasi, sostegni, casi indipendenti, filtrazione stazionaria,
controlli locali idraulici, stabilità globale nel campo documentato,
verifiche Concrete/Steel supportate, ricerca discreta dell'infissione,
inviluppi con provenienza, persistenza, CSV e report. Modulo Spettri integrato.
Gli otto esempi Paratie e l'esempio Spettri usano estensione `.anthea` per
il dialogo Apri; il contenuto è identico per SHA-256 ai file collaudati.

Le limitazioni sono nelle guide e nella [matrice completa](../paratie/requisiti.md).
La 1.11.0 non implementa ancora tutte le famiglie, i metodi e le funzioni
avanzate richieste, né l'approfondimento della testa dei pali/micropali.
