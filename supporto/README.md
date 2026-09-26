# Materiale di supporto ANTHEA

Questa cartella raccoglie test, documentazione, esempi, immagini di verifica e strumenti di sviluppo.

| Cartella | Contenuto |
| --- | --- |
| `test/X.Verifiche` | Progetto dei controlli numerici e software, incluso nella soluzione ANTHEA |
| `test/Desktop` | Controlli WPF, compilati nel progetto desktop tramite collegamento |
| `test/casi_confronto.json` | Dati dei confronti numerici |
| `docs` | Documentazione tecnica dei moduli e rapporti di audit |
| `documentazione` | Documenti Word, immagini e fonti di riferimento |
| `esempi` | Esempi di input |
| `scripts` | Strumenti per icone e revisione dei report |
| `artefatti` | Risultati delle verifiche, schermate e log; esclusi da Git |
| `tmp` | Materiale di lavoro e verifiche storiche conservati |

Eseguire i comandi seguenti dalla radice del repository:

```powershell
dotnet run --project supporto/test/X.Verifiche -c Release -- --checker
dotnet run --project supporto/test/X.Verifiche -c Release -- --bridge
dotnet X.Desktop/bin/Release/net8.0-windows/ANTHEA.dll --smoke-display supporto/artefatti/display
```

`Verifica.cmd`, in questa cartella, esegue i confronti completi e salva il rapporto in `artefatti/confronto_numerico.json`.

I valori attesi provengono dal programma Python originale. Quando ANTHEA se ne discosta per scelta, i casi interessati si
rigenerano dal C# e restano marcati dal campo `fonte_atteso`:
`dotnet run --project test/X.Verifiche -c Release -- --attesi test/casi_confronto.json "<filtro sul nome>" <risultati.json>`,
poi `python scripts/aggiorna_attesi.py test/casi_confronto.json <risultati.json> "<motivazione>"` (conserva la struttura e il
formato del file; gli altri casi restano identici). Così sono stati rigenerati il 26 settembre 2026 i 30 casi delle sezioni
a T: l'ultima coppia di barre laterali è agli angoli superiori della staffa d'anima, nell'ala.
I nuovi output di test vanno salvati in `supporto/artefatti/` per mantenere pulita la radice.
Le immagini utilizzate dall'applicazione rimangono in `X.Desktop/Assets`.
