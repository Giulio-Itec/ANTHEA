# Materiale di supporto ANTHEA

La documentazione utente è raccolta in due guide globali Rev20 del 5 ottobre 2026:

- [Guida pratica e UI](docs/guida-pratica-anthea.md): procedure e interfaccia di tutti i moduli.
- [Guida teorica](docs/guida-teorica-anthea.md): modelli, formule, ipotesi, limiti, approfondimenti e appendici tecniche di tutti i moduli.

Edizioni Word e PDF in `documentazione/Guide_ANTHEA`, PDF omonimi accanto ai Markdown. Gli approfondimenti di Bridge Design, pali, sezioni, muri e stabilità globale sono inclusi nei due volumi. La Rev11 ha integrato il modulo di efficienza orizzontale delle palificate, con sei teorie, limiti e fonti; le revisioni sostituite sono in `SUPERATI/efficienza-orizzontale-rev11-20261002`. La Rev10 compone le formule da sorgenti LaTeX, conserva equazioni Word strutturate e aggiorna la leggibilità della Wiki. La Rev09 ha aggiunto la Wiki integrata, il capitolo Elementi Beam, la guida Sezione in c.a. e un percorso applicativo con esempio precompilato. Le fonti precedenti sono conservate in `SUPERATI/guide-unificate-rev08-20261002`, `SUPERATI/wiki-rev09-20261002` e `SUPERATI/wiki-formule-rev10-20261002`, con registro di origine e sostituzione.

Ogni nuovo argomento va integrato nelle due guide; per casi particolari chiedere all’utente prima di introdurre una diversa organizzazione. Modelli, esempi di calcolo ed evidenze restano nelle loro cartelle.

| Cartella | Contenuto |
| --- | --- |
| `test/X.Verifiche` | Progetto dei controlli numerici e software, incluso nella soluzione ANTHEA |
| `test/Desktop` | Controlli WPF, compilati nel progetto desktop tramite collegamento |
| `test/casi_confronto.json` | Dati dei confronti numerici |
| `docs` | Documentazione tecnica dei moduli e rapporti di audit |
| `documentazione` | Documenti Word, immagini e fonti di riferimento |
| `esempi` | Esempi di input |
| `scripts` | Strumenti per icone e revisione dei report |
| `installer` | Script NSIS e build del setup di ANTHEA ([Installer](installer/README.md)) |
| `artefatti` | Risultati delle verifiche, schermate e log; esclusi da Git |
| `SUPERATI` | Revisioni precedenti e documenti sostituiti, con registro degli spostamenti |
| `tmp` | Materiale di lavoro e verifiche storiche conservati |

Eseguire i comandi seguenti dalla radice del repository:

```powershell
dotnet run --project supporto/test/X.Verifiche -c Release -- --checker
dotnet run --project supporto/test/X.Verifiche -c Release -- --bridge
dotnet run --project supporto/test/X.Verifiche -c Release -- --bridge-methods
dotnet X.Desktop/bin/Release/net8.0-windows/ANTHEA.dll --smoke-display supporto/artefatti/display
```

`--bridge` include i controlli precedenti e la suite dei metodi cumulativo, storico lineare,
storico non lineare e delle curve di risposta. `--bridge-methods` esegue soltanto la nuova
suite. Casi, riferimenti analitici e limiti sono descritti in
[Test dei metodi per i ponti](docs/guida-teorica-anthea.md).

`Verifica.cmd`, in questa cartella, esegue i confronti completi e salva il rapporto in `artefatti/confronto_numerico.json`.

I valori attesi provengono dal programma Python originale. Quando ANTHEA se ne discosta per scelta, i casi interessati si
rigenerano dal C# e restano marcati dal campo `fonte_atteso`:
`dotnet run --project test/X.Verifiche -c Release -- --attesi test/casi_confronto.json "<filtro sul nome>" <risultati.json>`,
poi `python scripts/aggiorna_attesi.py test/casi_confronto.json <risultati.json> "<motivazione>"` (conserva la struttura e il
formato del file; gli altri casi restano identici). Così sono stati rigenerati il 26 settembre 2026 i 30 casi delle sezioni
a T: l'ultima coppia di barre laterali è agli angoli superiori della staffa d'anima, nell'ala.
I nuovi output di test vanno salvati in `supporto/artefatti/` per mantenere pulita la radice.
Le immagini utilizzate dall'applicazione rimangono in `X.Desktop/Assets`.

Muri con due colonne e attriti: [guida](docs/guida-teorica-anthea.md), PDF omonimo e [controllo della revisione](artefatti/muri-due-colonne-20260930/CONTROLLO.md), anche in PDF. Revisioni precedenti e registro in `SUPERATI/`.

Portanza sismica, cedimenti, Newmark e armature: [rapporto aggiornamento](artefatti/muri-completamento-20260930/CONTROLLO.md), anche PDF; [esempio salvato](artefatti/muri-completamento-20260930/interfaccia-finale/esempio-completo.anthea), relazione Word e PDF nella stessa cartella. Nessuna nuova prova MAX.
