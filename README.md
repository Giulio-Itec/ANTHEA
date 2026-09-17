# ANTHEA

Applicazione Windows in C# per calcoli geotecnici di pali e micropali e verifiche di sezioni in cemento armato.

## Avvio

Nella distribuzione locale avviare `Avvia ANTHEA.cmd` oppure `app/ANTHEA.exe`. È richiesto .NET Desktop Runtime 8 per Windows. Python non è necessario.

Il repository contiene i sorgenti, non gli eseguibili generati. Per compilare serve Windows con SDK .NET 8 o successivo compatibile:

```powershell
.\Compila.cmd
```

In alternativa aprire `ANTHEA.sln` in Visual Studio con gli strumenti di sviluppo desktop .NET. La compilazione produce la cartella `app`.

## Struttura

- `X.Core`: motore di calcolo, archivi e report DOCX.
- `X.Desktop`: interfaccia Windows Forms e risorse incorporate.
- `X.Verifiche`: test numerici e controlli software in C#.
- `casi_confronto.json`: dati e risultati di riferimento per la regressione; non richiede Python.
- `documentazione`: provenienza dei metodi, coefficienti e modifiche autorizzate.

I nomi interni `X` sono conservati per limitare le modifiche tecniche; il prodotto e l'eseguibile si chiamano ANTHEA. Gli identificatori storici dei file salvati sono mantenuti per compatibilità.

## Verifiche

```powershell
dotnet run --project X.Verifiche -c Release -- casi_confronto.json confronto_numerico.json
```

È disponibile anche `Verifica.cmd`. Il confronto comprende 464 casi numerici e 25 controlli software. Tolleranza: `1e-8 + 1e-10 * abs(riferimento)`. Non modificare i valori attesi per adeguarli a risultati diversi senza indagare la causa.

## Metodi e limiti

Geotecnica: lunghezze in m, forze in kN, tensioni in kPa, pesi di volume in kN/m³, angoli in gradi. Sezioni: mm, MPa, kN e kNm; N positivo a compressione, `Mx = ΣF·y`, `My = −ΣF·x`.

Nq esclusivamente parametrizzato, con coefficienti e limiti NQ-2026-09-09. Gli archivi con metodi precedenti conservano la provenienza e segnalano la migrazione. I metodi orizzontali non implementati non sono introdotti da questa versione. Il report DOCX è disponibile per pali e micropali, non per le sezioni in c.a.

Le scelte e le modifiche ingegneristiche pregresse sono documentate in `documentazione/MODIFICHE_PYTHON.md`: è documentazione storica, non codice eseguibile. Le verifiche di regressione misurano l'equivalenza software, non costituiscono una nuova validazione normativa. I risultati richiedono revisione professionale.

## File esclusi dal repository

Eseguibili compilati, cartelle `bin`/`obj`, output delle verifiche e file di lavoro locali sono esclusi tramite `.gitignore`. Materiali temporanei di conversione e backup non fanno parte della distribuzione pulita.
