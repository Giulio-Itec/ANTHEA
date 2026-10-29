# Preparazione di ANTHEA 1.16.0

Decisioni dell'utente del 28 ottobre 2026 sera e del 29 ottobre ([decisioni](decisioni.md), «28 ottobre (sera) — decisioni della 1.15.0»
e «29 ottobre — decisioni della 1.16.0»): la coda della 1.16.0 era autorizzata fino al rilascio («Fino alla 1.16.0 pulita»: unione nei
master delle librerie, push, set ufficiale, full, setup, tag e push se pulita), con i riferimenti cambiati approvati se ogni riga è
spiegata («Approvati se spiegati»); il rilascio aspetta che tutte le sessioni delle librerie abbiano finito («almeno esce con l'ultima
versione delle librerie in sviluppo»). Il contenuto per l'utente è nelle [note di rilascio](note-rilascio-1.16.0.md).

## Contenuto

- Tutto il contenuto della 1.15.0 (5d5185ba, tag `v1.15.0`).
- Contrasto della Chiara (1eaa0dc8, 243449a8): unità «−» della «Scala del dominio 3D», baseline del contrasto da 3 a 0 (UI-CONTRASTO-03).
- Importazione Straus7 con fasi, `.nla` per fasi, truss e cavi nelle combinazioni, reazioni, masse e temperature (ef0b1dd7, MOD-33).
- Combinazioni D6–D11 generate da Model (667948d1) e seguiti della mattina (6bf591f0): D7 b, vetro EN 16612, Bridge Design su Model (D8);
  prova del traffico con la scelta dei coefficienti (d81387dc).
- Colonna modello fase 2, tutte le combinazioni e dominio ridotto N–M1Rd,max (a9fcbd02, CA-COLONNA-02), con la conversione nominata del
  dominio ridotto (e8c52b6e).
- Sezione c.a. e risposta elastica del palo dal limitatore, taglio sullo sfondo (a3c35e58, INFRA-LIMITATORE-01).
- Fondazioni, Hansen con carico inclinato (63c90d92, FON-HANSEN-01).
- Seguiti dei muri: documenti della versione 1 e nota del sisma assegnato (6fde828e, UPG-PSI0-01, COMB-MURI-D5-NOTA).
- Voci R della revisione delle librerie (d3a4681d, ea03e928: R6, R7, R8, R14, R16, R17, R20).
- Paratie, bordi laterali della filtrazione a carico imposto o a flusso nullo (76211d82, PARATIE-21, documento versione 12).
- Muri a mensola, armature e distinta come da tavola, ferri longitudinali (1a8ee676, R-MURI-01…09; dbea2eb6 per la regola F5 P3).
- Guide pratica e teorica Rev62 e articolo «Capacità orizzontale del palo in terreno stratificato» (fd5b5397, 6730cb93; transizioni
  editoriali della Wiki F5-REL-1160-01 e -02).

## Librerie: release AM

Set ufficiale da Model `d0d76b51` e Checker `c301be7a`, entrambi su origin/master, con il lavoro della 1.16.0 (Straus7, combinazioni,
colonna modello, Hansen, muri, voci R) unito a quello delle altre sessioni delle librerie (revisione Steel e verifiche delle aste,
domini della sezione c.a. in memoria); Geometry e Utilities invariati. Installato in `lib/Checker` da f105c299. Manifest delle librerie
`18577AE4…58D8`. Le versioni delle DLL sono in `lib/Checker/README.md` («Release AM»). Le impronte delle fondazioni superficiali cambiano
solo per la versione di Geotechnics 0.27.0.0 (B-FON-01, `seguito_am`).

## Versione

`<Version>` 1.16.0 in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` (bc6add07): nome del setup, versione file `1.16.0.0`, prodotto
`1.16.0+<commit>`. Istruzioni dell'installer con PDF; originali 1.15.0 in `supporto/SUPERATI/installer-1160-20261029`.

## Prove

Full sulle integrazioni con i set candidati (`supporto/artefatti/1160`), l'ultima sull'unione completa con il set uguale ad AM: PASS 154,
con le due prove attese dal set candidato (provenienza delle librerie in Architecture.Tests e impronte delle fondazioni), risolte con AM.
Full di rilascio sul commit bc6add07 (`supporto/artefatti/rilascio-1.16.0/full`, profilo full con `-Installer` e
`GPC_STRAUS7_ELEMENTS=versioned`): **PASS 160, KNOWN 0, NEW-FAIL 0** al primo giro, senza ripetizioni. Non eseguite come nelle full
precedenti ElasticPile.Checks e BridgeValidationCurrent (passano nei test di libreria) ed ElasticPile.Performance (misura dei tempi).
Setup costruito poi dallo stesso commit con `Crea-Installer.ps1` (`supporto/artefatti/rilascio-1.16.0/installer`), albero pulito.

## Lista di controllo

| Controllo | Atteso | Esito |
| --- | --- | --- |
| Commit del full = commit del setup, albero pulito | stesso SHA | bc6add07 per entrambi, albero pulito (`source_clean: true`) |
| Full `rilascio-1.16.0/full` | PASS, NEW-FAIL 0 | PASS 160, KNOWN 0, NEW-FAIL 0, nessuna ripetizione |
| Suite installer | PASS | `installer/safety`, `installer/guides`, `installer/examples` PASS |
| `7z t` del setup | nessun errore | nessun errore, 393 file |
| Versione del prodotto di `ANTHEA.exe` estratto | `1.16.0+<commit>` | `1.16.0+bc6add07b2d5370c56611cb425debbb4533836c5` |
| DLL GPC uguali a `lib/Checker/manifest.json` | 13, set AM | 13, set AM, manifest `18577AE4…58D8` |
| Processo Straus7 | `ANTHEA.Strand7Worker` accanto ad `ANTHEA.exe`, senza `St7api.dll` | 4 file del worker, `St7api.dll` assente |
| Guide | pratica e teorica Rev62 | Rev62 |
| Gmsh, UnsafeEx, assembly di prova o legacy | nessuno | nessuno (`forbidden_files` vuoto) |
| Setup | nome, dimensione, SHA-256 | `ANTHEA-1.16.0-Setup-x64.exe`, 71 112 952 byte, `4BFD53B3E4B92EDF61EAB9D8314C866429855D4A1568C5AA9108D16BFC97CCC8` |

## Esito

- **File distribuiti:** 388, uno in più della 1.15.0: il PDF dell'articolo «Palo orizzontale in terreno stratificato» nella cartella Guide;
  file applicativi 331, come nella 1.15.0. Esempi: 52 documenti per 15 moduli.
- **Audit:** `Audit-Installer.py` PASS (`audit-payload.json` accanto al setup in `supporto/artefatti/rilascio-1.16.0/installer`).
- **Pubblicazione:** con l'ok dell'utente («Fino alla 1.16.0 pulita»), tag `v1.16.0` sul commit del setup e push di `main` e
  `refactoring/f5`.
