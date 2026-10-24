# ANTHEA.Desktop.UiTests

Prove WPF fuori schermo dell'interfaccia (stadio `ui` di `build/ci.ps1`). Dal passo F5.14 (24/10/2026) raccolgono le
sorgenti che stavano in `tests/Desktop` e `supporto/test/Desktop`, e in `Equivalence/` la cattura F3 che stava in
`tests/ANTHEA.Desktop.Equivalence`. Le sorgenti non sono cambiate: è cambiata solo la cartella.

## Come si compilano

La cartella non ha un progetto proprio. Le sorgenti entrano in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` solo nella
configurazione `UiTests` (ottimizzata come la Release, simbolo `UITESTS`, uscita in `bin/UiTests`), come prima di F5.14:

```
dotnet build src/ANTHEA.Desktop/ANTHEA.Desktop.csproj -c UiTests
dotnet src/ANTHEA.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --check-theme-tokens <cartella>
```

La riga di comando delle prove è in `AppHarness.cs` (`--smoke-*`, `--check-*`, `--capture-validation`,
`--interactive-shell`, `--module-impl`). La Release non contiene codice di prova: lo controlla `qa/no-test-code`
(`tools/qa/Assert-NoTestCode.ps1`).

Nel runner: `build\ci.ps1 -Stage build,ui` (tutte), con `-Only '<espressione>'` per sceglierne alcune; `-List` le elenca e le
conta senza eseguirle.

## Perché non è un eseguibile separato

Alcune prove estendono tipi di produzione con classi `partial` che usano membri privati (`App` con `RunTestHarness`,
`WikiView`, `RetainingWallDrawing`, `DomainViewport3D`, `BridgeOptimizationPlot`, `ModuleImplementations`): devono stare
nello stesso assembly. La forma della proposta F5 (§7.3: eseguibile separato con `InternalsVisibleTo`, risorse da
`Themes/*.xaml`) è una voce da fare dopo F5.15; i passi sono in `docs/refactoring/f5-14-chiusura.md`, sezione 4.3.

Restano fuori da questa cartella, per ora: `tests/Shared` e `supporto/test/Shared` (usati anche da altri progetti) e i
progetti di prova che caricano l'interfaccia per riflessione (`tests/ModelViewer.UiChecks`, `tests/FoundationWorkspace.Checks`,
`tests/ConcreteInput.Checks`, `tests/ConcreteTimeEffects.Checks`, `supporto/test/ElasticPile.UiChecks`,
`supporto/test/HorizontalPileGroup.Checks`).
