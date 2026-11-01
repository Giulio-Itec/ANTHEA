# Preparazione di ANTHEA 1.17.0

Richiesta dell'utente del 1 novembre 2026 sera: «fai nuova versione di anthea se tutto passa», poi «ricordati di fare merge dei vari
branch e di pulire a fine lavoro. porta master sul commit della versione e push su git» ([decisioni](decisioni.md), «1 novembre (sera)
— punti 7 e 8»). Il contenuto per l'utente è nelle [note di rilascio](note-rilascio-1.17.0.md).

## Contenuto

- Tutto il contenuto della 1.16.2 (tag `v1.16.2`).
- Lavori del 1 novembre ([piano](piano.md), «Lavori del 1 novembre 2026»):
  - chiusura delle combinazioni del modulo Modelli (agente `combo-chiusura`):
    - scrittura del risultato fuori dal thread dell'interfaccia;
    - revisione per singola definizione (COMBO-REV-01);
    - preparazione GPC in parallelo;
    - bug COMBO-B4;
  - pagine dei metodi del c.a. (agente `ca-metodi`, due giri):
    - μφ nella scheda «Momento–curvatura» (CA-METODI-03);
    - nella libreria, EN 8.7.2(3), 8.7.2(4) e 8.8 (CA-METODI-01 e 02);
  - modulo Modelli, punto 8 (agente `modelli-8`):
    - pannello del generatore NTC 2018 con la regola 100/30 (COMBO-GEN-01);
    - componenti principali dei plate (COMBO-PRIN-01);
    - combinazioni oltre 4096 stati calcolate per termini (COMBO-B2-bis);
  - combinazioni dei progetti 1.16.x ancora valide che restano «Calcolate», con «Ricalcola con le componenti principali»
    (COMBO-REV-01-bis);
  - guide Rev65 (agente `guide-1170`, F5-REL-1170-01);
  - correzioni delle suite Architecture.Tests e ANTHEA.ApiReuse.Checks dopo il secondo giro del c.a. (b71f4ad3).
- Librerie della release AQ.

## Librerie: release AQ

- **Origine:** set ufficiale da Model `f4f78752` e Checker `eb16e72c`, entrambi su origin/master. Geometry `6734eee1` e Utilities
  `e57b440b` sono invariati.
- **Contenuto:**
  - GPCModel 5.34.0.0, GPC.Converter 2.12.0.0, ModelData 0.3.0.20 e Persistence 3.4.0.10;
  - GPCChecker.Concrete 0.35.0.0, CompositeBridge 1.23.0.1;
  - ricompilate all'ultimo componente: Steel 2.127.0.8, Geotechnics 0.27.0.10, ModelChecker 2.35.0.8, Masonry 0.10.0.25 e Design
    0.44.0.5.

  Tutte le suite delle librerie passano e le DLL sono identiche al candidato `cand-aq`.
- **Installazione:** in `lib/Checker` da 079b38bd, con la provenienza F4 aggiornata. Le versioni delle DLL sono in
  `lib/Checker/README.md` («Release AQ»).

## Versione

`<Version>` 1.17.0 in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj`: nome del setup, versione file `1.17.0.0`, prodotto `1.17.0+<commit>`.
Le istruzioni dell'installer, con il PDF, sono aggiornate dall'agente delle guide; gli originali 1.16.2 sono in
`supporto/SUPERATI/rilascio-1170-rev65-20261101`.

## Prove

- **Corsa rapida del ramo d'integrazione con AQ** (`supporto/artefatti/integrazione-aq/quick`): 57 suite passate e 2 fallimenti
  nuovi (Architecture.Tests, ANTHEA.ApiReuse.Checks), corretti in b71f4ad3 e ripassati (`integrazione-aq/mirate`).
- **Campagna dei tempi su AQ, a macchina ferma** (`supporto/artefatti/combo-chiusura/tempi-20261101-aq*`): PASS, impronte dei valori uguali alla 1.16.0 sui modelli 100 × 50 e 500 × 100; prova completa sul 500 × 100 36,0 s (36,6 s con la 1.16.x), inviluppo SLU 3,3 s con le principali (2,0 s).
- **Full di rilascio:** sul commit del setup, profilo full con `-Installer` e `GPC_STRAUS7_ELEMENTS=versioned`
  (`supporto/artefatti/rilascio-1.17.0/full`).
- **Setup:** costruito poi dallo stesso commit con `Crea-Installer.ps1` (`supporto/artefatti/rilascio-1.17.0/installer`) e controllato
  con `Audit-Installer.py`.

Gli esiti della full e del setup (nome, dimensione e SHA-256) sono nell'annotazione del tag `v1.17.0`, sul commit del setup.
`main` resta su quel commit, come chiesto dall'utente; il verbale con gli esiti entra nel commit successivo.

## Lista di controllo

| Controllo | Atteso |
| --- | --- |
| Commit del full = commit del setup, albero pulito | stesso SHA |
| Full `rilascio-1.17.0/full` | PASS, NEW-FAIL 0 |
| Suite installer | `installer/safety`, `installer/guides`, `installer/examples` PASS |
| `7z t` del setup | nessun errore |
| Versione del prodotto di `ANTHEA.exe` estratto | `1.17.0+<commit>` |
| DLL GPC uguali a `lib/Checker/manifest.json` | tutte quelle del manifest, set AQ |
| Processo Straus7 | `ANTHEA.Strand7Worker` accanto ad `ANTHEA.exe`, senza `St7api.dll` |
| Guide | pratica e teorica Rev65 |
| Gmsh, UnsafeEx, assembly di prova o legacy | nessuno |
