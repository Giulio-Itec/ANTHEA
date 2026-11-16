# ANTHEA 1.19.1 — candidata completa

Verbale tecnico della seconda candidata della 1.19.1, preparata il 16 novembre 2026 nel clone `CX/ANTHEA-1191c`,
ramo `codex/208-1191-completa` (compito 208, completato da Claude). La candidata del 191b (`3120a64d`, setup
`CB0FE61A…`) resta separata e invariata nel clone `CX/ANTHEA-aw`. L'utente sceglie fra le due. Se sceglie la
191b, questa preparazione diventa la 1.19.2: vanno cambiati versione e documenti, ricostruito il setup e
ripetuto l'audit; rinominare il file non basta. [Note di rilascio](note-rilascio-1.19.1.md), anche in
[PDF](note-rilascio-1.19.1.pdf). Nessun tag, push o copia sulla condivisione fa parte della preparazione.

## Contenuto e provenienza

| Passaggio | Commit | Contenuto |
|---|---|---|
| Candidata 191b | `3120a64d02dea2002cae64e3669c60c420e1afd3` | 1.19.1 sul set AW, correzioni delle prove 191 |
| Guide Rev72 (205, 207) | `99b715ae`, `84621b6f28bef76a24150a70f1083620975d70c5` | guide, Wiki, GuideRev72.Checks, figure offscreen |
| Avviso P10 | `905e1be61e9d57f88c06617ecf890883918054a8` | da `84c99d93`, ramo `claude/p10-avviso-materiali` |
| Strati per posizione | `7c05ad8b3e3e7841e1ac347f7e822da2a8093bab`, `68192e18615ee1215bdae8762a9d097d67b163a2` | da `48cd276f` e `83e68e0a`, ramo `claude/strati-sondaggio`; escluso il superato `74c139ac` |
| Guide con P10 e strati | `69e9f2d2f6c6460c0b32b477e2c900d184a99f03` | paragrafo P10 e protezione degli strati nelle guide Rev72, Word/PDF e Wiki |

Il vecchio `SoilLayerShiftReproduction.cs` non c'è: le prove degli strati sono in `SoilSubdivisionChecks`,
registrata nella corsa normale di Presentation.Tests. Il set ufficiale resta `gpc-release-20261114-aw`, 14 DLL,
manifest SHA-256 `4E43D4666E40FC4EFCABC3968E9DDDF64D2B0945E6A5BEA11BE09EEB560CA3D0`. Nessun candidato
successivo ad AW.

## Guide Rev72

Pratica e teorica Rev72 aggiornate con l'avviso P10 e la protezione degli strati, con la pipeline ITEC
invariata: pratica 140 pagine, 1409 frammenti, 9 formule; teorica 214 pagine, 2557 frammenti, 1579 formule.
Ogni DOCX conserva byte per byte le 26 parti del modello e le cinque sezioni. Le 44 pagine cambiate sono state
esaminate una per una; le altre 310 immagini coincidono con quelle già controllate nel 207. Wiki rigenerata,
94 capitoli. Articolo Rev01, 16 originali Rev71 archiviati e 1801 file protetti invariati. Dettagli in
[guide-rev72-208.md](guide-rev72-208.md).

## Prove prima delle correzioni delle schede senza dati

Corse seriali sotto `INT/pesante.lock` sul codice di `68192e18` (guide in corso) e di `69e9f2d2` (stesso codice,
guide e Wiki committate). Insieme coprono tutte le **131 suite del profilo full**, comprese **73 UI**:
**130 PASS e un NEW-FAIL, `ui/f3-equivalence`**. Le sei suite Architecture (618 controlli), Application,
Presentation, Projects, Reports (733) e ConcreteLibraryAdapter sono PASS, come le UI delle schede del progetto,
`ui/shell-smoke-sharing` e `ui/shell-smoke-hierarchy`. Rispetto al catalogo del 191 c'è una suite in più,
GuideRev72.Checks; nessuna UI tolta. Non si dichiara una full interamente verde.

- **F3**: 1086 differenze non ammesse, tutte nella Wiki; proposta separata con 629 voci nuove e 457 superate,
  nessuna differenza fuori dalla Wiki e nessuna classe vietata. Serve l'OK dell'utente; la proposta non è
  applicata (`chatgpt-compiti/evidenze-208/wiki-rev72-per-ok.md`).
- **F4**: sei PASS e sei FAIL storici (`last-adapters`, `residual`, `input-contracts`, `section-design`,
  `pile-outcomes`, `structural-adapter`), con oggetti di confronto identici al 191 e quindi alle evidenze AV/AW
  classificate nel 188. La prima corsa si era fermata su input storici assenti nel clone: ripristinati da copie
  identiche verificate con il manifest congelato, senza rigenerazione, e corsa ripetuta. `build/known-failures.json`
  resta vuoto: i sei FAIL non sono accettati da questa preparazione.
- La suite nativa ha eseguito zero controlli nativi; nessuna corsa Straus7 o MIDAS.

Evidenze in `chatgpt-compiti/evidenze-208` (`ci-aggregate.json`, `f4-comparison-to191.json`).

## Pacchetto

Da costruire dal commit di prodotto pulito con `supporto/installer/Crea-Installer.ps1`, poi `7z t`, estrazione
e `tools/qa/Audit-Installer.py --version 1.19.1 --guide-revision 72 --library-set AW`. Collaudi
`installer/safety`, `installer/guides` e `installer/examples` con TEMP e TMP in `%LOCALAPPDATA%\Temp`.
Gli esiti si registrano in questo verbale dopo la costruzione.
