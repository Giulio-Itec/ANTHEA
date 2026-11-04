# Estrazione del contenitore risultati GPC - F3 comune

3 novembre 2026, branch `codex/gpc-unification-20261102`. Ãˆ F3 del piano comune BridGHe/Arachne, non una riapertura della precedente fase F3 del refactoring dell'interfaccia ANTHEA.

Cattura precedente: `supporto/artefatti/ci/20261103-003513-gpc-f3-before`, commit c4f68c5e. ModelWorkspace.Checks e Application.Tests PASS. Gli archivi della prova, inclusi quelli appositamente danneggiati, sono conservati nella corsa `-Keep -NoPrune`.

Il contenitore Ã¨ estratto nel progetto fratello `Model/GPC.Archive` netstandard2.0 senza dipendenze da Model/Checker. `ModelResultArchive.Shared.cs` conserva in ANTHEA schemi JSON, soglie, riferimenti, storico e messaggi applicativi. La build di prova si seleziona con la proprietÃ  MSBuild `GpcArchiveProject`, impostabile anche come variabile d'ambiente al percorso del csproj. In questa configurazione ANTHEA.ModelWorkspace passa da versione assembly predefinita 1.0.0.0 a 1.1.0.0; l'eseguibile rilasciato non viene versionato nÃ© ricostruito come release.

Il codice precedente rimane attivo senza la proprietÃ . Non Ã¨ stato eliminato nÃ© riscritto: resta il riferimento per l'equivalenza fino all'adozione ordinaria F7/F8, quando potrÃ  essere spostato in tests/ANTHEA.Legacy. Questa transizione esplicita consente il confronto senza introdurre DLL non pubblicate nello snapshot `lib/Checker`. Nessun aggiornamento allo snapshot, nessun aggiramento dei suoi hash.

Confronto dopo: runner ufficiale `build/ci.ps1 -Stage build,fast -Only 'ModelWorkspace.Checks|Application.Tests' -Keep -NoPrune`, con `GpcArchiveProject` esplicito. Prima prova condivisa PASS in `20261103-004054-gpc-f3-shared`; il riepilogo finale e gli hash sono collegati nel rapporto F3 di Model. Le prove esistenti verificano risultati bit-identici, sotto/sopra soglia, documenti storici, missing/corrupt archive, combinazioni, storico, Save As e interruzione fra archivio e documento. La nuova fixture nel repository Model usa l'archivio prima dell'estrazione e dimostra anche la riscrittura byte-identica sui due runtime.

Differenze intenzionali e correzioni registrate in `registro-differenze.json` (GPC-F3-01). Restano da fare F7/F8: adozione predefinita, rilascio del set coerente, snapshot e profilo full ANTHEA. Questa fase non attesta una verifica completa WPF o del nuovo installer.

Corse finali: `20261103-005533-gpc-f3-shared-final` e `20261103-005637-gpc-f3-default-final`, entrambe PASS 4 (due build e due suite). Il manifest dello snapshot ha SHA F593B14ECA13A8AE7C6BB8A87A762F02C835A07B5FEEAFB1F4BD65A6CF4D151F in tutte le corse.

## F6, corsia L3: adozione di GPC.Archive

3 novembre 2026, sera, branch `claude/f6-L3`. Revisione di Claude, punti D1 e D2; decisione dell'utente: «per il salvataggio usa GPC.Archive. però vediamo di ottimizzarlo a dovere».

- `ModelResultArchive` usa GPC.Archive (`BlockArchive`) al posto della propria copia: una sola implementazione per ANTHEA, BridGHe e Arachne. Restano in ANTHEA lo schema dei riferimenti nel documento, le revisioni, le soglie (`InlineResultBytes`, `ArchivedCopyBytes`, `ArchivedCopyLimits`) e le copie conservate. Tolti `ModelResultArchive.Shared.cs` e il commutatore `GpcArchiveProject`.
- `GPC.Archive.dll` 1.1.0.0 (Model `62508fef`, non pubblicato) entra nello snapshot `lib/Checker` e in `tools/libs/Update-Snapshot.ps1`, come in `Checker/build/Release-Set.ps1`.
- Formato 1 invariato. `tests/ModelWorkspace.Checks/LegacyResultArchive118.cs` conserva solo per le prove il codice di 1.18.0: gli stessi salvataggi, lungo uno storico di revisioni, scrivono lo stesso archivio byte per byte; 1.18.0 legge gli archivi nuovi e viceversa.
- Differenze di comportamento (registro `GPC-F6-L3-01`): un riferimento danneggiato è un risultato non disponibile, non un progetto che non si apre; un archivio con indice danneggiato o di formato sconosciuto viene rinominato `.danneggiato-<data>` e il progetto si salva; un archivio in uso da un altro programma ferma ancora il salvataggio, come prima; «Salva con nome» su un progetto esistente conserva i blocchi del vecchio archivio fino al salvataggio successivo; i blocchi ricopiati sono verificati una volta per sessione; un file `.lock` serializza i processi sullo stesso archivio.

Tempi (`ModelWorkspace.Checks --archive-timing`, Release, 24 processori, 3 corse, mediana in ms; nella stessa corsa GPC.Archive e il codice di 1.18.0 sullo stesso documento, archivi controllati identici a ogni passo):

| Fase | 1000x100 GPC.Archive | 1000x100 1.18.0 | 2000x50 GPC.Archive | 2000x50 1.18.0 |
|---|---:|---:|---:|---:|
| Salva (archivio + documento) | 335 | 306 | 190 | 187 |
| Salva senza modifiche | 255 | 261 | 173 | 178 |
| Secondo Salva (una combinazione in più) | 281 | 309 | 213 | 223 |
| Salva dopo la riapertura (copia cambiata) | 208 | 252 | | |

Archivi: 40,2 MB (1000x100) e 51,1 MB (2000x50). Il modello 3500x50 non entra nel banco: la copia supera i 256 MiB di `ModelCombinations.Attach` senza limite esplicito. Le differenze stanno nel rumore di misura; nessun peggioramento.

## F6, corsia L4: avvisi del salvataggio e GPC.Archive corretto

4 novembre 2026, notte, branch `claude/f6-L4`. Revisione indipendente delle correzioni P1 (registro `GPC-F6-L4-01`).

- ANTHEA non leggeva mai il resoconto del salvataggio di GPC.Archive (`LastSave`). `ModelResultArchive.SaveNotice()` ne fa un testo in italiano e `WorkspaceSession.Save` lo mostra dopo un salvataggio riuscito, come avviso con il titolo «Salvataggio con avvisi» (accanto a «Salvataggio non riuscito» dei salvataggi rifiutati): l'archivio danneggiato conservato con un altro nome (con il nome del file), i blocchi danneggiati non ricopiati, i blocchi citati dal progetto che l'archivio non ha. Negli altri salvataggi nessun messaggio.
- `GPC.Archive.dll` ricompilata da Model `198b21e0` (stessa versione 1.1.0.0, stessa API, formato 1): un errore di lettura durante il controllo dei blocchi ferma il salvataggio prima del documento (prima toglieva i blocchi dall'archivio); «Salva con nome» blocca anche l'archivio di partenza e copia solo dal file che il suo indice descrive; un blocco danneggiato non torna dalla memoria della sessione dopo un annulla.
- Prove: `ResultArchiveSharedChecks` (riferimento danneggiato, blocco danneggiato con «Salva con nome», archivio messo da parte) e `WorkspaceChecks.ArchiveNotice` (avviso mostrato una volta, con il nome del file conservato).
