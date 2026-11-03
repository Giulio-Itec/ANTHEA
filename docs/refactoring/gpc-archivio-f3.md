# Estrazione del contenitore risultati GPC - F3 comune

3 novembre 2026, branch `codex/gpc-unification-20261102`. Ãˆ F3 del piano comune BridGHe/Arachne, non una riapertura della precedente fase F3 del refactoring dell'interfaccia ANTHEA.

Cattura precedente: `supporto/artefatti/ci/20261103-003513-gpc-f3-before`, commit c4f68c5e. ModelWorkspace.Checks e Application.Tests PASS. Gli archivi della prova, inclusi quelli appositamente danneggiati, sono conservati nella corsa `-Keep -NoPrune`.

Il contenitore Ã¨ estratto nel progetto fratello `Model/GPC.Archive` netstandard2.0 senza dipendenze da Model/Checker. `ModelResultArchive.Shared.cs` conserva in ANTHEA schemi JSON, soglie, riferimenti, storico e messaggi applicativi. La build di prova si seleziona con la proprietÃ  MSBuild `GpcArchiveProject`, impostabile anche come variabile d'ambiente al percorso del csproj. In questa configurazione ANTHEA.ModelWorkspace passa da versione assembly predefinita 1.0.0.0 a 1.1.0.0; l'eseguibile rilasciato non viene versionato nÃ© ricostruito come release.

Il codice precedente rimane attivo senza la proprietÃ . Non Ã¨ stato eliminato nÃ© riscritto: resta il riferimento per l'equivalenza fino all'adozione ordinaria F7/F8, quando potrÃ  essere spostato in tests/ANTHEA.Legacy. Questa transizione esplicita consente il confronto senza introdurre DLL non pubblicate nello snapshot `lib/Checker`. Nessun aggiornamento allo snapshot, nessun aggiramento dei suoi hash.

Confronto dopo: runner ufficiale `build/ci.ps1 -Stage build,fast -Only 'ModelWorkspace.Checks|Application.Tests' -Keep -NoPrune`, con `GpcArchiveProject` esplicito. Prima prova condivisa PASS in `20261103-004054-gpc-f3-shared`; il riepilogo finale e gli hash sono collegati nel rapporto F3 di Model. Le prove esistenti verificano risultati bit-identici, sotto/sopra soglia, documenti storici, missing/corrupt archive, combinazioni, storico, Save As e interruzione fra archivio e documento. La nuova fixture nel repository Model usa l'archivio prima dell'estrazione e dimostra anche la riscrittura byte-identica sui due runtime.

Differenze intenzionali e correzioni registrate in `registro-differenze.json` (GPC-F3-01). Restano da fare F7/F8: adozione predefinita, rilascio del set coerente, snapshot e profilo full ANTHEA. Questa fase non attesta una verifica completa WPF o del nuovo installer.

Corse finali: `20261103-005533-gpc-f3-shared-final` e `20261103-005637-gpc-f3-default-final`, entrambe PASS 4 (due build e due suite). Il manifest dello snapshot ha SHA F593B14ECA13A8AE7C6BB8A87A762F02C835A07B5FEEAFB1F4BD65A6CF4D151F in tutte le corse.
