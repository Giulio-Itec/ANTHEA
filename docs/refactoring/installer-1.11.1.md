# Installer ANTHEA 1.11.1 — 20 ottobre 2026

Versione richiesta dall'utente con la sezione grafica delle paratie accanto
all'editor, selezione degli elementi, aggiornamento per caso/fase e guide
Rev53. Il disegno degli input rimane distinto dai risultati del calcolo.
La matrice completa e l'approfondimento della testa di pali/micropali
restano aperti: questo rilascio non ne dichiara il completamento.

## Revisione e pacchetto

- Commit esatto della build: `5f85ea81d5d30dd7931107767ca99e3f5c3888b1`.
- Tag della build: `v1.11.1`; ramo di distribuzione `refactoring/f5`.
- Versione applicativa: `1.11.1`, assegnata su richiesta esplicita dell'utente.
- Versione file ANTHEA.exe: `1.11.1.0`.
- Prodotto: `1.11.1+5f85ea81d5d30dd7931107767ca99e3f5c3888b1`.
- Setup NSIS self-contained Windows x64, runtime .NET 8 incluso.
- File: `supporto/artefatti/paratie/rilascio-1.11.1/installer/ANTHEA-1.11.1-Setup-x64.exe`.
- Dimensione: **69063049 byte**.
- SHA-256: `E85B634955E2EE1671D4B63345BDC3AEF65EC44E5586E21E3A94BCD1663AA289`.
- Esempi: `ANTHEA-1.11.1-Esempi.zip`, **186178 byte**;
  SHA-256 `BF074AE43FA3030FF2E6BD8EE93580EA29E72907526E7B001C21ED916D32D310`.

L'installer è compilato dal commit pulito sopra indicato. Il verbale
successivo non cambia il pacchetto; il tag mantiene la revisione esatta
della build. Setup, esempi e log sono esclusi da Git. La consegna principale
è nella cartella artefatti del checkout ANTHEA; gli output integrali sono
conservati anche nel worktree `ParatieWork/ANTHEA`.

## Verifiche eseguite

Entrambe le corse si riferiscono a `5f85ea81`, albero pulito e invariato,
manifest GPC `397E9315C536DE3954FC59B86F4589505E874343DCB341E1145B69C49573AC40`.

| Corsa in `supporto/artefatti/paratie/rilascio-1.11.1/` | Esito |
| --- | --- |
| `standard`: `build/ci.ps1 -Profile standard -Installer` | 77 PASS, zero fallimenti |
| `ui`: `-Profile full -Stage build,ui -Only '^(ui/check-paratie-view\|ui/check-wiki-offscreen\|ui/check-module-host-offscreen\|ui/f3-equivalence)$'` | 5 PASS, zero fallimenti |

Conteggi significativi: Presentation 3433; Spettri 1566; paratie WPF 69;
Wiki WPF 3439; contenitore MVVM 51. Equivalenza F3: 104 fixture, 77 articoli,
30 ricerche e report di progetto. Installer: 26 controlli di sicurezza
e 30 controlli delle guide, comprese installazione/disinstallazione isolate.
Il percorso dei moduli Windows PowerShell è assegnato soltanto al processo
di collaudo per evitare il difetto ambientale già diagnosticato nella 1.11.0.

Nove JSON di calcolo delle paratie coincidono byte per byte con la cattura
anteriore alla modifica grafica. Attesi, tolleranze, baseline e liste dei
fallimenti ammessi restano invariati. Nessun nuovo test è dichiarato passato
per semplice presenza del codice.

## Audit del setup ed esempi

`Crea-Installer.ps1` pubblica in uno staging separato da `bin/obj`.
L'archivio NSIS è stato verificato ed estratto con 7-Zip:

- **325 file applicativi** identici per SHA-256 alla pubblicazione;
- **13 DLL GPC** identiche al manifest ufficiale AA;
- **tre PDF** identici alle guide globali Rev53 e all'indice;
- versione e commit dell'eseguibile estratto corrispondenti alla build;
- assenti Gmsh e assembly di prova; controllo dei metadati degli assembly
  tramite `Assert-NoTestCode.ps1` superato durante il confezionamento.

Le due guide Rev53 conservano le 260 pagine e 387 formule già controllate.
Le istruzioni dell'installer sono state aggiornate alla 1.11.1 e i tre fogli
PDF controllati visivamente; due originali conservati con SHA-256 in
`supporto/SUPERATI/installer-1111-20261020/`.

Lo ZIP contiene i nove esempi della 1.11.0, conservati identici, e
`10-paratia-grafica.anthea`: il nuovo esempio con falda, due strati e tirante,
salvato dal collaudo WPF dopo un calcolo convergente. Tutti i dieci file
nello ZIP sono verificati contro le impronte di provenienza.

`audit-payload.json`, `esempi-provenienza.json`, manifest GPC, riepiloghi
delle corse, `rilascio.json` e `SHA256SUMS.txt` accompagnano il setup.
La versione installata dell'utente non è stata sostituita per le prove.

## Ambito dei controlli e seguito

Questo è uno standard completo con WPF mirate, non un nuovo full completo.
Le due prove WPF interrotte da Trend Micro e il confronto generale a 96 DPI
del [rilascio precedente](installer-1.11.0.md) restano aperti; questa corsa
non ne modifica l'esito né estende la lista dei fallimenti ammessi.

Il set AA è quello già pubblicato: nessun sorgente o DLL GPC è modificato.
I kernel di prosecuzione nelle librerie non fanno parte del setup.
L'integrazione in F5 riguarda il lavoro ANTHEA autorizzato; rami di prova,
rami remoti storici e `main` restano separati.

Dettagli funzionali: [interfaccia grafica](../paratie/interfaccia-grafica.md)
e [matrice dei requisiti ancora aperti](../paratie/requisiti.md).
