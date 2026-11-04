# Sezioni c.a. di ANTHEA 1.18.0 (XML) per il passaggio al formato compatto

Fixture della fase F7a della migrazione GPC (4/11/2026, decisione dell'utente «Compatto dalla prossima»): dalla 1.19 ANTHEA salva le sezioni
in cemento armato nel formato compatto di GPC.Model.Persistence 3.14 e continua a leggere i file delle versioni precedenti.

| File | Contenuto | Provenienza |
|---|---|---|
| `progetto-1.18.0.programma` | archivio progetti di versione 3 (`versione_base` 2) con quattro sezioni GPC in XML (contratto 1): un foglio `str_palo` con una revisione (500 → 600), un modulo estraneo (`geo_efficienza_orizzontale`), una sezione annidata (450) e un foglio del progetto (550), come il progetto di `ConcretePersistenceChecks` | scritto dal vero ANTHEA 1.18.0 (`ANTHEA.Application.dll` del payload dell'installer `ANTHEA-1.18.0-Setup-x64.exe`, commit 2efb5ab0), caricato con le sue librerie in un contesto isolato e chiamato con `ProjectDocuments`, `ProjectRevisions.NewRevision` e `DocumentService.Save`; SHA-256 `34AEC04C8F590130D463A60AAAC5C0D86256552122D1CADA6D4780EE3E996C79` |

Il file non si rigenera: è il formato di 1.18.0. `-text` in `.gitattributes`, così resta uguale byte per byte.

Usato da `tests/ANTHEA.Application.Tests/ConcretePersistenceChecks.cs` insieme ai 15 esempi distribuiti di `supporto/esempi/distribuzione/str_palo`
(versione 3, sezioni in XML): apertura, salvataggio compatto (versione 5, sezioni del contratto 2), riapertura con le stesse sezioni (identità,
proprietà, armature, tensioni allo SLE e documento GPC), secondo salvataggio uguale byte per byte, rifiuto di 1.18 con «Formato o versione del
file non supportati.».
