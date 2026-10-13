# F4.2 — protezioni automatiche del confine di dominio

Estensione del progetto esistente `ANTHEA.Architecture.Tests`, 13 ottobre 2026.
Il controllo usa l'albero sintattico C# del compilatore incluso nell'SDK:
nessun pacchetto o assembly Roslyn viene aggiunto all'applicazione.

## Regole e riferimento iniziale

| Regola | Occorrenze iniziali | Segnale rilevato |
| --- | ---: | --- |
| R1 | 255 | Funzioni matematiche, anche tramite alias o using static |
| R2 | 2404 | Letterali numerici nelle espressioni aritmetiche, esclusi fattori elementari |
| R3 | 18 | NTC usata come ripiego nella lettura o riconosciuta per prefisso |
| R4 | 1391 | Letture numeriche tramite D, comprese quelle con default esplicito |
| R5 | 13 | Decisioni basate su etichette testuali di verifica |
| R6 | 529 | Fattori di conversione dimensionali scritti direttamente nell'aritmetica |
| R7 | 0 | Dipendenze WPF/Office o creazione di controlli negli strati non grafici |

507 file di produzione, 4610 occorrenze: X.Calculations 1841, Desktop 2160,
src 609. È un censimento sintattico, non un conteggio di difetti normativi.
Si scandiscono i sorgenti dei progetti X.* e src, esclusi bin/obj, nel ramo
preprocessato Release/net8; non si certificano configurazioni o simboli diversi.

Ogni residuo è identificato in `tests/ANTHEA.Architecture.Tests/f4-domain-baseline.json`
da file, tipo/metodo, regola, SHA dei token, conteggio ed estratto. La provenienza
è il commit `7b2a0c4b` prima delle modifiche di produzione F4. Nuove formule,
duplicazioni, spostamenti e modifiche di coefficienti falliscono anche a
conteggio totale invariato. Commenti e formattazione non cambiano l'impronta.
Quando un residuo viene rimosso il test richiede di ridurre contestualmente la
baseline: non resta un'autorizzazione riutilizzabile per reintrodurlo.

## Geometria grafica e limiti

Non si escludono intere cartelle o viste. Rimangono censite puntualmente, ad
esempio, le trasformazioni cromatiche di `Appearance.Gamma/Linear`, le funzioni
trigonometriche di `BridgeOptimizationPlot.Star`, la trasformazione logaritmica
di `ConcreteTimeEffectsView.RefreshPlot.Tick` e gli orientamenti di disegno.
Queste occorrenze hanno uno scopo grafico; la loro presenza non autorizza nuovi
calcoli di resistenza. Ogni nuova eccezione necessita di motivazione puntuale
e revisione della modifica, mai di rigenerazione indiscriminata della baseline.

R2 esclude 0, ±1, ±2 e ±100; R6 ammette conversioni nominate; R4 censisce una
chiamata D senza dedurne se il dato sia obbligatorio. Simboli, formule riscritte
con altri operatori, costanti nominate, reflection e stringhe dinamiche possono
richiedere revisione manuale. I controlli sui metadati e sulle dipendenze di F3
restano attivi. Il vincolo del manifest GPC F3 verrà aggiornato con una nuova
provenienza solo al rilascio del set F4; non è stato rimosso anticipatamente.

## Prove

26 prove del rilevatore: regole positive/negative, alias, commenti e stringhe,
coefficienti cambiati, duplicazioni, file nuovo, formattazione, riduzioni e
sintassi invalida. L'inventario non aggiorna la baseline: produce una proposta
separata con conteggi e impronte, da esaminare.

Runner `20261013-f4-architecture-inventory`: 2 PASS.
Runner `20261013-f4-rules`: 5 PASS, zero nuovi fallimenti; build Desktop Release,
architettura e prove del banco di cattura. Evidenze nelle omonime cartelle
`supporto/artefatti/ci/`. Nessun calcolo o risultato numerico modificato.
