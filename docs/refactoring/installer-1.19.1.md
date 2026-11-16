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
| Note e verbale | `283815e3` | documenti della candidata ripresi dopo l'interruzione di Codex |
| Schede senza dati | da `e50dd72e` a `d468c6254e71b80f6cdce233b69d0659cf856afc` | otto commit del ramo `claude/copriferro-materiali-vuoto` (clone `CX/ANTHEA-copriferro`, da main `6c7d03a7`), tabella sotto |
| Note di rilascio complete | **`5c52a79902d9cb3d3f440c30550bfeae6123fdcb`**, commit di prodotto | note con le schede senza dati, PDF, piano e verbale; da qui il setup |
| Verbale finale | commit figlio del prodotto | solo questo verbale e il piano, nessuna modifica al prodotto |

Correzioni delle schede senza dati, prese con cherry-pick nell'ordine del ramo:

| Originale | Nella candidata | Conflitti e scelta |
|---|---|---|
| `3c5a3754` copriferro non verificabile per la scheda senza dati | `e50dd72e` | `ProjectValidation.cs`: P10 aveva spostato il calcolo nel sovraccarico privato `CoverChecks(sheet, name, material)`; il controllo dei dati della scheda va lì, quindi vale per i Materiali usati e per quelli più vicini dell'avviso P10. `piano.md`: tenute le due sezioni. |
| `fab28349` diametro del palo orizzontale come nel registro F4 | `d8ccb618` | Stesso punto: rimesso il testo `sheet["dati"]!["generali"].D("diametro")` registrato nella baseline F4 dell'architettura, nel sovraccarico privato (stesso proprietario `ProjectValidation.CoverChecks`). |
| `83051c0f` terreni e report con una scheda senza dati | `59dfacdf` | Solo `piano.md`: la sezione 208 va prima di quella del copriferro, che resta in fondo come nel suo ramo. |
| `e4d46f76` muro senza dati fuori dai confronti | `209aa1d3` | nessuno |
| `1bf9b93c` comandi dei terreni con una scheda senza dati | `e36faa45` | nessuno |
| `b9f7f880` dati comuni non scritti nelle schede senza dati | `a47edb9e` | `SharedDataCoordinator.cs`: unite le due regole. Bersagli della conferma: le schede aggiornabili (`Updatable`, copriferro) che condividono un campo secondo `Shares` (strati: niente strati per posizione con una suddivisione diversa). La domanda riceve gli esclusi (copriferro) e filtra le righe con `Shares` (strati). `ApplyTargets` ha sia il filtro `Updatable` sia il parametro `skip`. |
| `2a7a937c` scheda nuova senza riferimenti da schede senza dati | `bb75d2f8` | nessuno |
| `23287e44` prova WPF degli esclusi e di «Non leggibile» | `d468c625` | nessuno |

Il registro delle differenze passa a 439 voci senza duplicati (`BUG-COPERTINA-SCHEDA-SENZA-DATI`,
`BUG-COPERTINA-TERRENI-SENZA-DATI`, `BUG-COPERTINA-MURO-SENZA-DATI`, `BUG-COMANDI-TERRENI-SENZA-DATI`,
`BUG-APPLICA-SENZA-DATI`, `BUG-EREDITA-SENZA-DATI`).

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

## Prove dopo le correzioni delle schede senza dati

Sul commit `d468c625`, albero pulito, sotto `INT/pesante.lock`, priorità BelowNormal, TEMP nel clone,
`-m:1 -p:UseSharedCompilation=false` (copia locale del runner, `build/ci-208.ps1`, che cambia solo questo).

| Corsa | Suite | Esito |
|---|---|---|
| `20261116-074610-c1191-fast`, 07:46–07:59 | 9 build; Architecture, Reports, Application, Projects, Presentation, FoundationWorkspace, ConcreteLibraryAdapter (rapido, completo, attesi), verifiche/project-audit | 19 PASS, NEW-FAIL 0. Controlli: Architecture 618, Reports 733, Application 3715, Projects 22, Presentation 5186, ConcreteLibraryAdapter 18563 + 175115 + 677559, project-audit 988 |
| `20261116-075927-c1191-ui`, 07:59–08:06 | build UiTests; `ui/f3-report-chapters`, `ui/f3-equivalence`, `ui/check-shell-draft`, `ui/shell-smoke-projects`, `-project-workspace`, `-hierarchy`, `-sharing`, `-project-report`, `-display`, `ui/shell-capture`, `ui/shell-capture-confronto` | 11 PASS e il NEW-FAIL F3. Sharing 577 controlli, hierarchy 39, projects 221 al primo tentativo |

Nessun file versionato modificato dalle corse. Il confronto F3 ha le stesse 1086 differenze non ammesse della
corsa di Codex sul `68192e18`: stesso elenco (file, percorso, classe, valori), tutte nella Wiki, stessi conteggi
per classe; le correzioni non aggiungono differenze nei report o nei progetti.

**Full non ripetuta.** Gli otto commit toccano soltanto il codice dei progetti (`src/ANTHEA.Application/Projects`),
il piano del report del progetto (`ProjectReportPlan.cs`), la coerenza e la conferma dei dati condivisi in
`src/ANTHEA.Presentation` e la finestra «Dati condivisi della sezione» in `WpfDesktopServices.cs`, più le prove.
Nessuna libreria, modulo di calcolo, foglio dei moduli, guida o figura. Le suite che usano quel codice sono
tutte nelle due corse sopra; la full del 208 copre il resto, che non cambia.

## Pacchetto

### Contrasto

`ui/check-contrast` sul commit di prodotto `5c52a799`, in una tenuta del lock a parte (corsa
`20261116-080852-c1191-contrast`, 08:08–08:38): build UiTests e prova PASS in 1739 s, «Scuro 0, Molto scuro 0;
Chiara 0 difetti in 0 voci (baseline 0)». Serviva perché `23287e44` cambia il modo in cui si costruisce la
finestra «Dati condivisi della sezione», che la prova del contrasto apre. Nessun file versionato toccato.

### Costruzione e audit

Dal commit di prodotto `5c52a79902d9cb3d3f440c30550bfeae6123fdcb`, albero pulito, sotto `INT/pesante.lock`
(08:39–08:48), TEMP e TMP in `%LOCALAPPDATA%\Temp`, `dotnet build/publish` con `-m:1 -p:UseSharedCompilation=false`:

```powershell
& .\supporto\installer\Crea-Installer.ps1 `
  -OutputDirectory .\supporto\artefatti\rilascio-1.19.1-completa\installer `
  -MakeNsis 'C:\Program Files (x86)\NSIS\makensis.exe'
& 'C:\Program Files\7-Zip\7z.exe' t .\supporto\artefatti\rilascio-1.19.1-completa\installer\ANTHEA-1.19.1-Setup-x64.exe
& 'C:\Program Files\7-Zip\7z.exe' x .\supporto\artefatti\rilascio-1.19.1-completa\installer\ANTHEA-1.19.1-Setup-x64.exe `
  '-o.\supporto\artefatti\rilascio-1.19.1-completa\installer\estratto' -y
py -3 -X utf8 -I .\tools\qa\Audit-Installer.py `
  --setup .\supporto\artefatti\rilascio-1.19.1-completa\installer\ANTHEA-1.19.1-Setup-x64.exe `
  --extract .\supporto\artefatti\rilascio-1.19.1-completa\installer\estratto `
  --version 1.19.1 --guide-revision 72 --library-set AW `
  --out .\supporto\artefatti\rilascio-1.19.1-completa\installer\audit-payload.json
```

| Controllo | Risultato |
|---|---|
| Setup | `supporto/artefatti/rilascio-1.19.1-completa/installer/ANTHEA-1.19.1-Setup-x64.exe` nel clone `CX/ANTHEA-1191c` |
| Dimensione | **74.814.011 byte** (191b: 74.290.636; +523.375, soprattutto le guide Rev72) |
| SHA-256 | **`AD8A32F5BDE18669F33A0F9B4AE1513CEF49AF0C2ADF392E7EBB268C99FDAA6E`** |
| ProductVersion di ANTHEA.exe estratto | `1.19.1+5c52a79902d9cb3d3f440c30550bfeae6123fdcb` |
| Integrità | `7z t` «Everything is Ok»; estrazione con `7z x` |
| Audit-Installer | PASS: 332 file applicazione, 432 file payload uguali allo staging, sorgente pulito |
| DLL GPC | 14/14 uguali al manifest AW (`4E43D466…CA3D0`) |
| Guide ed esempi | Rev72 (pratica e teorica uguali ai PDF Rev72 del repository), indice, articolo, 95 esempi in 32 moduli |
| Worker Straus7 | quattro file presenti; `St7api.dll` assente |
| File vietati | nessuno (anche nessun Gmsh, GMesh, UnsafeEx, GPCModelChecker nell'estrazione) |
| Codice di prova | `Assert-NoTestCode.ps1` PASS durante la costruzione |

La compilazione registra gli stessi warning del 191b: 15 occorrenze, 11 messaggi distinti, negli stessi file e
nelle stesse righe (per esempio CS8618 in `WallModel.Data.cs`), nessuno nei file toccati qui; non sono corretti. Non è stata eseguita l'installazione completa sul profilo
dell'utente né una prova nativa del worker.

### Collaudi dell'installer

Corsa `20261116-084732-c1191-installer` sul commit di prodotto, `build/ci-208.ps1 -Stage fast -Installer -NoBuild
-Only '^installer/(safety|guides|examples)$'`, TEMP e TMP in `%LOCALAPPDATA%\Temp`, NSIS 3 di
`C:\Program Files (x86)\NSIS`:

| Suite | Esito |
|---|---|
| `installer/safety` | PASS, 26 controlli |
| `installer/guides` | PASS, 35 controlli: installazione, hash dei quattro PDF, collegamenti, disinstallatore fino alla fine della sezione di rimozione, PDF rimossi, documenti personali conservati |
| `installer/examples` | PASS, 206 controlli |

Nessun file versionato toccato. Il FAIL di `installer/guides` del 191b (rimozione non completata con TEMP sotto
Documents/CX) non si ripresenta con TEMP in `%LOCALAPPDATA%\Temp`, come già indicato dal sorgente del test;
la causa del comportamento sotto Documents resta non determinata.

## Cosa resta all'utente

- Scegliere fra questa candidata e quella del 191b. Se sceglie questa, il tag `v1.19.1` va sul commit di prodotto
  `5c52a799`; `main` e `refactoring/f5` sul commit figlio di questo verbale, come per la 1.19.0. Lo script di
  pubblicazione del 191b controlla l'HEAD `3120a64d`: per questa candidata va adattato agli SHA di qui.
- Decidere sui sei confronti storici F4: oggi sono FAIL dichiarati, non voci di `build/known-failures.json`.
- Approvare o no le transizioni F3 della Wiki Rev72 (`chatgpt-compiti/evidenze-208/wiki-rev72-per-ok.md`).
- Le prove native Straus7 e MIDAS non fanno parte di questa preparazione.
