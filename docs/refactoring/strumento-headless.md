# Strumento senza interfaccia

Lo strumento di sviluppo tools/ANTHEA.Headless usa gli stessi servizi applicativi del Desktop. Richiede .NET 8; non è incluso nell’installer desktop e non produce immagini da solo.

Dalla radice del repository:

```powershell
dotnet run --project tools/ANTHEA.Headless -c Release -- --help
dotnet run --project tools/ANTHEA.Headless -c Release -- validate percorso/documento.anthea
dotnet run --project tools/ANTHEA.Headless -c Release -- inspect percorso/documento.anthea
dotnet run --project tools/ANTHEA.Headless -c Release -- calculate percorso/foglio.anthea percorso/risultati.json
```

Usare il percorso effettivo dell’archivio: l’estensione negli esempi non converte il formato. validate verifica documento e revisioni; inspect stampa conteggio e moduli. L’apertura conserva le decisioni pendenti e non prepara tutti i fogli. calculate richiede un foglio singolo, scrive i valori in un file distinto e non salva modifiche sul documento di ingresso. L’esito del processo indica la disponibilità del calcolo, non il superamento delle verifiche strutturali; leggere esiti e limiti nel JSON.

Il report di progetto usa ProjectReportService, ModuleReportService e IFigureRenderer. Non è esposto da questi tre comandi; il renderer Desktop è grafico e non costruisce editor nascosti. Restano tracciati in F3.7b i contratti nativi e le chiamate residue nella UI.
