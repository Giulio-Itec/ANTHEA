# X.Desktop (cartella residua)

Il progetto dell'interfaccia è in `src/ANTHEA.Desktop` dal passo F5.14 (24/10/2026). Qui resta soltanto `Assets/Wiki`, con
le figure delle due guide globali.

Le figure restano qui perché le guide (`supporto/docs/guida-*.md`) le citano con il percorso `../../X.Desktop/Assets/Wiki/…`,
e le stesse righe stanno nei riferimenti F3 congelati della Wiki (`tests/fixtures/f3/expected/wiki/*.md`). Da questo percorso
dipendono anche `src/ANTHEA.Wiki/Content/assets.json` e `sources.props`, `supporto/scripts/wiki/build-wiki-index.py`,
`validate_wiki.py` e gli script che disegnano le figure. `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` le incorpora con gli
stessi nomi di prima (`Assets/Wiki/…` e `Wiki.figures.*`).

Lo spostamento delle figure va fatto con una revisione delle guide, insieme ai PDF e alle transizioni editoriali della Wiki
(decisione provvisoria del coordinatore del 24/10, opzione A di `docs/refactoring/f5-14-chiusura.md`, sezione 3).
Nessun altro file va aggiunto a questa cartella.
