# F5.3 — Temi a token (punto di controllo F5.3)

Documento per il punto di controllo **F5.3 [CP]** del [piano](piano.md) (riga F5.3 della sezione 8 della
[proposta](f5-proposta-architettura.md)). Ramo `refactoring/f5-3-temi`, da `refactoring/f5` 5f29067b.

L'utente deve decidere:
- la domanda Q6 della proposta, sezione 10. Il lavoro segue la raccomandazione (c): un insieme semantico di token, usato
  solo dalle viste nuove, con le varianti vicine accorpate modulo per modulo;
- l'elenco dei token (sezione 3) e dei token tipografici (sezione 4);
- gli accorpamenti proposti (sezione 5);
- le domande aperte (sezione 8).

Fuori dalle viste nuove non cambia niente. Le viste storiche restano identiche nei tre aspetti, pixel per pixel
(sezione 7).

## 1. Sintesi

Cosa è stato fatto:
- **Token dei colori.** 69 token in `X.Desktop/Themes/ThemeTokens.cs`: 36 semantici (tabella 6.2 della proposta) e i gruppi
  Series (7), Drawing (8), Swatch (12) e Utilization (6). Ogni token è registrato come `Token.<nome>` con lo stesso pennello
  della tavolozza di `Appearance`.
- **Token tipografici.** Sei token in `Themes/Typography.xaml`, con i valori del censimento.
- **Dizionari separati.** I dizionari di App.xaml sono in `X.Desktop/Themes/*.xaml`, caricabili da soli; App.xaml li unisce.
- **FieldForm.** Usa solo token. Le cinque risorse aggiunte da F5.2 sono assorbite.
- **Ambito chiaro.** Le immagini dei report delle viste nuove si producono con `DocumentAppearance`, nell'ambito chiaro.
- **Regola P7.** Attiva nei test di architettura, con 36 prove dei rilevatori.
- **Nuove prove nello stadio ui:**
  - `ui/check-theme-tokens`: uguaglianza dei colori nei tre aspetti, dizionari, ambito chiaro;
  - `ui/check-contrast`: 0 difetti in Scuro e Molto scuro, Chiara entro la baseline.

  `ui/check-appearance` e `ui/smoke-display` erano già nel runner.

Criteri di uscita della riga F5.3:

| Criterio | Esito |
| --- | --- |
| `--check-contrast`: 0 difetti in Scuro e Molto scuro, Chiara uguale al riferimento | sì: 0 e 0; Chiara 699 difetti a 96 DPI, gli stessi della cattura prima (sezione 7.1) |
| `--check-appearance`, `--smoke-display` | PASS nella corsa finale del runner |
| prova di uguaglianza dei colori | PASS 129 controlli |
| P7 attiva con prove dei rilevatori | sì, 36 prove nuove |
| parità FieldForm | PASS 2025 controlli |
| `-Stage build,fast,wiki,ui` verde, `ui/f3-equivalence` invariata con lo stesso file di transizioni | sì: PASS 110, NEW-FAIL 0; riferimento F3 e transizioni invariati (sezione 7.3) |
| `f5-temi-token.md` pronto | questo documento |

## 2. Meccanismo

### 2.1 Token dei colori

- **Catalogo.** `X.Desktop/Themes/ThemeTokens.cs` elenca i token. Per ognuno indica:
  - il nome;
  - il colore della Chiara;
  - il ruolo della tavolozza di `Appearance`: `background`, `foreground`, `border`, `selected`, `selection`, `calculated`,
    `series`, `disabled` o `swatch`;
  - l'uso;
  - la corrispondenza con le risorse o i colori del codice storico.
- **Registrazione.** `AppearanceResources.Register` registra ogni token nelle risorse dell'applicazione con la chiave
  `Token.<nome>`. Il valore è **lo stesso pennello** di `Appearance.Colour(colore, ruolo)`: stesso oggetto, stessa
  trasformazione, stesso aggiornamento al cambio di aspetto. Ne segue che:
  - Scuro e Molto scuro sono identici per costruzione;
  - `Token.Paper` e `AppearancePaper` sono lo stesso oggetto, come `Token.Ink` e `Appearance.foreground.0B2A4A`.
- **Uso nelle viste nuove.** Le viste nuove usano solo `{DynamicResource Token.<nome>}`.
- **Disegni tecnici.** Il codice dei disegni tecnici delle viste nuove prende i pennelli con `ThemeTokens.Brush("Drawing.…")`.
  I gruppi `Drawing`, `Swatch` e `Utilization` hanno il ruolo identità (`swatch`): restano uguali in ogni aspetto, come
  l'eccezione già accettata per i disegni su fondo bianco.

### 2.2 Token tipografici

`X.Desktop/Themes/Typography.xaml` definisce le dimensioni dei caratteri `Font.<nome>`, in DIP. Le viste nuove le usano
con `{DynamicResource Font.<nome>}`. Le viste storiche conservano le dimensioni scritte nel codice (`Ui.Text`).

### 2.3 Dizionari separati

App.xaml conteneva gli stili impliciti dei controlli. Ora unisce soltanto quattro dizionari, nell'ordine:
1. `Themes/ProjectStyles.xaml`, spostato da `Wpf/`;
2. `Themes/Typography.xaml`;
3. `Themes/Controls.xaml`: gli stili impliciti, senza modifiche. Unisce a sua volta `Themes/AppearanceControls.xaml`,
   spostato da `Wpf/`, perché ne usa i modelli scuri con `StaticResource`;
4. `Views/Common/FieldFormTemplates.xaml`.

Ogni dizionario si carica da solo con un URI pack: lo verifica `--check-theme-tokens`. I dizionari non hanno chiavi in
comune, quindi la ricerca delle risorse non cambia.

La struttura di App.xaml è la stessa di prima (`Application.Resources > ResourceDictionary > MergedDictionaries`). Per
questo i caricatori delle prove esterne che leggono `X.Desktop/App.xaml` dal disco funzionano senza modifiche:
`WpfTestResources`, `HorizontalPileGroup.Checks`, `ElasticPile.UiChecks` e `ModelViewer.UiChecks`.

### 2.4 Immagini dei documenti

Le immagini dei report delle viste nuove si producono nell'ambito chiaro con `X.Desktop/Themes/DocumentAppearance.cs`:
- `DocumentAppearance.Light(radice)` è l'ambito chiaro. Coincide con `Appearance.Document`;
- `DocumentAppearance.Image(elemento)` coincide con `Ui.DocumentSnapshot`;
- `DocumentAppearance.Image(elemento, dimensione)` produce l'immagine di una figura costruita solo per il documento.

I token sono pennelli della tavolozza, quindi dentro l'ambito prendono i colori della Chiara.

Il codice e i testi dei report non cambiano.

### 2.5 Regola P7

La regola P7 di `ANTHEA.Architecture.Tests` controlla le viste nuove: `X.Desktop/Views/`, e `X.Desktop/Shell/` quando
esisterà. Nel C# e nel XAML vieta:
- colori esadecimali;
- `Brushes.*` e `Colors.*`;
- `Color.From*`;
- pennelli o colori creati nel codice;
- `ColorConverter` e `BrushConverter`;
- i membri colore della tavolozza storica (`Appearance.Background`, `Paper`, `Colour`…, `Ui.Brush`, `Ui.Muted`…);
- le risorse storiche (`AppearancePaper`, `Appearance.<ruolo>.<HEX>`);
- nel XAML, i colori letterali sulle proprietà dei pennelli, anche nei `Setter`.

Sono ammessi `Transparent`, `{x:Null}` e l'ambito chiaro `Appearance.Document`. Non ci sono esenzioni: i disegni tecnici
usano i token `Drawing.*`.

Ci sono 36 prove nuove dei rilevatori, da 91 a 127. Sul FieldForm di F5.2 la regola avrebbe trovato 16 usi di risorse
storiche.

## 3. Elenco dei token

Colori nei tre aspetti dalla tabella congelata `tests/fixtures/f5/token-colori.json`. «Usi esatti» conta le occorrenze
nel codice storico dello stesso colore con un ruolo compatibile, secondo il censimento della sezione 5.

### 3.1 Insieme semantico

| Token | Chiara | Scuro | Molto scuro | Ruolo | Uso | Corrispondenza storica | Usi esatti |
|---|---|---|---|---|---|---|---|
| `Surface` | #F3F5F8 | #1D3047 | #090A0C | background | fondo delle pagine e delle finestre | AppearanceSurface, Appearance.background.F3F5F8, Ui.Bg | 53 |
| `Paper` | #FFFFFF | #142337 | #141518 | background | schede, pannelli, pulsanti | AppearancePaper, Appearance.background.FFFFFF, Appearance.Paper | 64 |
| `PaperAlt` | #F8FAFC | #1D3047 | #090A0C | background | righe alterne delle tabelle | Appearance.background.F8FAFC (RowBackground delle DataGrid) | 5 |
| `Field.Background` | #F8FAFC | #1D3047 | #090A0C | background | campi modificabili | Appearance.background.F8FAFC, #F8FAFC di InputForm | 5 |
| `Header` | #E8EFF7 | #1D3047 | #090A0C | background | intestazioni delle griglie | Appearance.background.E8EFF7 | 3 |
| `Ink` | #0B2A4A | #E5EDF7 | #E8E9EB | foreground | testo | AppearanceInk, Appearance.foreground.0B2A4A, Appearance.Ink, Ui.Navy | 44 |
| `InkSecondary` | #234767 | #E5EDF7 | #E8E9EB | foreground | frecce e glifi dell'albero | Appearance.foreground.234767 | 2 |
| `Muted` | #5B697E | #B9C9DD | #BFC2C7 | foreground | testo secondario, unità di misura (#64748B fino al 27/10/2026, UI-CONTRASTO-01) | Ui.Muted e #64748B in primo piano; ex Appearance.foreground.64748B di FieldForm (F5.2) | 205 |
| `Accent` | #0B5CAD | #B9C9DD | #BFC2C7 | foreground | collegamenti, enfasi | Appearance.Accent, Ui.Blue | 13 |
| `Disabled` | #767676 | #8291A5 | #8A8D92 | disabled | testo disattivato o valore automatico in grigio (#808080 fino al 27/10/2026, UI-CONTRASTO-01) | Appearance.Dim(Brushes.Gray) | 1 |
| `Border` | #DCE2E9 | #596B7E | #454C55 | border | bordi dei controlli | Appearance.border.DCE2E9 | 10 |
| `Border.Strong` | #CAD7E5 | #596B7E | #454C55 | border | bordi dei pulsanti di progetto | Appearance.border.CAD7E5 | 2 |
| `Border.Default` | #D3D3D3 | #596B7E | #454C55 | border | bordi di sistema (schede, menu, suggerimenti) | AppearanceBorder (LightGray) | 12 |
| `Divider` | #E4EAF1 | #596B7E | #454C55 | border | righe delle griglie | Appearance.border.E4EAF1 | 3 |
| `TreeGuide` | #DCE5EE | #596B7E | #454C55 | border | guide dell'albero | Appearance.border.DCE5EE | 2 |
| `Focus` | #3986CB | #3986CB | #3986CB | border | fuoco da tastiera | Appearance.border.3986CB | 2 |
| `Selection.Border` | #2582D2 | #2582D2 | #2582D2 | border | riga selezionata dell'albero | Appearance.border.2582D2 | 2 |
| `CommandBar` | #0B2A4A | #0B2A4A | #17191C | background | barre dei comandi Navy | Appearance.background.0B2A4A | 10 |
| `CommandBar.Hover` | #244665 | #244665 | #17191C | background | passaggio del mouse sulle barre | Appearance.background.244665 | 2 |
| `OnCommandBar` | #FFFFFF | #FFFFFF | #FFFFFF | foreground | testo sulle barre | Appearance.foreground.FFFFFF | 11 |
| `OnCommandBar.Muted` | #B9C8D8 | #B9C9DD | #BFC2C7 | foreground | testo secondario sulle barre | #B9C8D8 (primo piano) | 2 |
| `Hover` | #E3EFFA | #253B4A | #25323C | background | passaggio del mouse su pulsanti e menu | Appearance.background.E3EFFA | 4 |
| `Hover.Row` | #F0F6FC | #1D3047 | #090A0C | background | passaggio del mouse sulle righe dell'albero | Appearance.background.F0F6FC | 4 |
| `Selected` | #0B2A4A | #24507E | #2F4C6E | selected | pulsante primario, scelta attiva | Appearance.Selected | 1 |
| `Selection` | #E2EFFC | #26425E | #2E3D52 | selection | riga e scheda selezionate | AppearanceSelection, Appearance.selection.E2EFFC | 3 |
| `Calculated` | #EAF2FA | #2B4252 | #26323C | calculated | campi calcolati o collegati | Appearance.Calculated; ex Appearance.calculated.EAF2FA di FieldForm (F5.2) | 3 |
| `Note.Background` | #FFF6DD | #423A1E | #353021 | background | fondo delle note dei campi | Appearance.Background("#FFF6DD") di InputForm; ex Appearance.background.FFF6DD di FieldForm (F5.2) | 3 |
| `Note.Foreground` | #865D16 | #C8B595 | #C8B595 | foreground | testo delle note dei campi | #865D16 di InputForm; ex Appearance.foreground.865D16 di FieldForm (F5.2) | 3 |
| `Note.Border` | #EEDCAF | #596B7E | #454C55 | border | bordo delle note dei campi | #EEDCAF di InputForm; ex Appearance.border.EEDCAF di FieldForm (F5.2) | 0 |
| `Warning.Background` | #FFFAEB | #1D3047 | #090A0C | background | fondo degli avvisi | Appearance.Background("#FFFAEB") | 1 |
| `Warning.Foreground` | #9A4D0A | #D1AE90 | #D1AE90 | foreground | testo degli avvisi e degli errori dei campi | Appearance.foreground.9A4D0A | 2 |
| `Status.Ok` | #006400 | #8CB98C | #8CB98C | foreground | esito soddisfatto | Brushes.DarkGreen (vicino: ForestGreen) | 6 |
| `Status.Fail` | #B22222 | #DC9B9B | #DC9B9B | foreground | esito non soddisfatto | Brushes.Firebrick | 14 |
| `Status.Warn` | #FF8C00 | #FECB8C | #FECB8C | foreground | esito con riserva, avviso | Brushes.DarkOrange | 4 |
| `Badge.Ok` | #EAF5ED | #1D3047 | #090A0C | background | badge di coerenza | Appearance.Background("#EAF5ED") | 2 |
| `Badge.Warn` | #FFF0D8 | #44381D | #372F21 | background | badge di coerenza con avviso | Appearance.Background("#FFF0D8") | 1 |

### 3.2 Gruppo Series

| Token | Chiara | Scuro | Molto scuro | Ruolo | Uso | Corrispondenza storica | Usi esatti |
|---|---|---|---|---|---|---|---|
| `Series.Blue` | #0B5CAD | #90B5D9 | #90B5D9 | series | serie principale | Appearance.Series(Ui.Blue) | 7 |
| `Series.Steel` | #4682B4 | #ABC6DD | #ABC6DD | series | falda, serie secondaria | Appearance.Series(Brushes.SteelBlue) | 2 |
| `Series.Orange` | #FF8C00 | #FECB8C | #FECB8C | series | contorni e limiti | Appearance.Series(Brushes.DarkOrange) | 2 |
| `Series.Red` | #BF5545 | #E1B2AB | #E1B2AB | series | asse Mx, serie | Appearance.Series(Ui.Brush("#BF5545")) | 1 |
| `Series.Green` | #218463 | #9AC7B8 | #9AC7B8 | series | asse N, serie | Appearance.Series(Ui.Brush("#218463")) | 1 |
| `Series.Amber` | #B45309 | #DDB190 | #DDB190 | series | limiti analitici | Appearance.Series(Ui.Brush("#B45309")) | 1 |
| `Series.Magenta` | #C02672 | #E29DBF | #E29DBF | series | letture puntuali | Appearance.Series(Ui.Brush("#C02672")) | 1 |

### 3.3 Gruppo Drawing

| Token | Chiara | Scuro | Molto scuro | Ruolo | Uso | Corrispondenza storica | Usi esatti |
|---|---|---|---|---|---|---|---|
| `Drawing.Paper` | #FFFFFF | #FFFFFF | #FFFFFF | swatch | fondo dei disegni tecnici | Brushes.White nei disegni OnRender | 53 |
| `Drawing.Canvas` | #F8FAFD | #F8FAFD | #F8FAFD | swatch | fondo delle viste tecniche (sezioni, ponte) | Ui.Brush("#F8FAFD") nei disegni | 4 |
| `Drawing.Ink` | #0B2A4A | #0B2A4A | #0B2A4A | swatch | linee e testi principali dei disegni | Ui.Navy nei disegni | 75 |
| `Drawing.Muted` | #64748B | #64748B | #64748B | swatch | quote e testi secondari dei disegni | Ui.Muted nei disegni | 83 |
| `Drawing.Accent` | #0B5CAD | #0B5CAD | #0B5CAD | swatch | elementi evidenziati dei disegni | Ui.Blue nei disegni | 37 |
| `Drawing.Axis` | #94A3B8 | #94A3B8 | #94A3B8 | swatch | assi e limiti tratteggiati | Ui.Brush("#94A3B8") nei disegni | 7 |
| `Drawing.Grid` | #E8EDF3 | #E8EDF3 | #E8EDF3 | swatch | griglia dei grafici | Ui.Brush("#E8EDF3") nei disegni | 1 |
| `Drawing.Guide` | #CBD5E1 | #CBD5E1 | #CBD5E1 | swatch | linee guida e bordi delle aree dei disegni | Ui.Brush("#CBD5E1") nei disegni | 4 |

### 3.4 Gruppo Swatch

| Token | Chiara | Scuro | Molto scuro | Ruolo | Uso | Corrispondenza storica | Usi esatti |
|---|---|---|---|---|---|---|---|
| `Swatch.Soil1` | #F4C95D | #F4C95D | #F4C95D | swatch | strato 1 nei disegni dei terreni | Drawings.LayerColors[0] | 1 |
| `Swatch.Soil2` | #DFA06E | #DFA06E | #DFA06E | swatch | strato 2 nei disegni dei terreni | Drawings.LayerColors[1] | 1 |
| `Swatch.Soil3` | #A8C686 | #A8C686 | #A8C686 | swatch | strato 3 nei disegni dei terreni | Drawings.LayerColors[2] | 1 |
| `Swatch.Soil4` | #8FB8DE | #8FB8DE | #8FB8DE | swatch | strato 4 nei disegni dei terreni | Drawings.LayerColors[3] | 1 |
| `Swatch.Soil5` | #C6A0D5 | #C6A0D5 | #C6A0D5 | swatch | strato 5 nei disegni dei terreni | Drawings.LayerColors[4] | 1 |
| `Swatch.Soil6` | #C9B79C | #C9B79C | #C9B79C | swatch | strato 6 nei disegni dei terreni | Drawings.LayerColors[5] | 1 |
| `Swatch.Wall1` | #DBCDAE | #DBCDAE | #DBCDAE | swatch | strato 1 del muro (tabella e disegno) | Appearance.Swatch(RetainingWallDrawing.LayerColors[0]) | 1 |
| `Swatch.Wall2` | #B9D6C6 | #B9D6C6 | #B9D6C6 | swatch | strato 2 del muro (tabella e disegno) | Appearance.Swatch(RetainingWallDrawing.LayerColors[1]) | 1 |
| `Swatch.Wall3` | #D2C2DC | #D2C2DC | #D2C2DC | swatch | strato 3 del muro (tabella e disegno) | Appearance.Swatch(RetainingWallDrawing.LayerColors[2]) | 1 |
| `Swatch.Wall4` | #D9C3A7 | #D9C3A7 | #D9C3A7 | swatch | strato 4 del muro (tabella e disegno) | Appearance.Swatch(RetainingWallDrawing.LayerColors[3]) | 1 |
| `Swatch.Wall5` | #B9CFDC | #B9CFDC | #B9CFDC | swatch | strato 5 del muro (tabella e disegno) | Appearance.Swatch(RetainingWallDrawing.LayerColors[4]) | 1 |
| `Swatch.Wall6` | #DAD6A8 | #DAD6A8 | #DAD6A8 | swatch | strato 6 del muro (tabella e disegno) | Appearance.Swatch(RetainingWallDrawing.LayerColors[5]) | 1 |

### 3.5 Gruppo Utilization

| Token | Chiara | Scuro | Molto scuro | Ruolo | Uso | Corrispondenza storica | Usi esatti |
|---|---|---|---|---|---|---|---|
| `Utilization.None` | #9AA4AF | #9AA4AF | #9AA4AF | swatch | η assente o non finito | UtilizationColors.Color(null) | 0 |
| `Utilization.Low` | #397BCC | #397BCC | #397BCC | swatch | η ≤ 0,50 | UtilizationColors.Color(≤ 0,50) | 0 |
| `Utilization.Fair` | #39A879 | #39A879 | #39A879 | swatch | 0,50 < η ≤ 0,70 | UtilizationColors.Color(≤ 0,70) | 2 |
| `Utilization.High` | #E4C441 | #E4C441 | #E4C441 | swatch | 0,70 < η ≤ 0,90 | UtilizationColors.Color(≤ 0,90) | 0 |
| `Utilization.Critical` | #EF8C32 | #EF8C32 | #EF8C32 | swatch | 0,90 < η ≤ 1,00 | UtilizationColors.Color(≤ 1,00) | 0 |
| `Utilization.Exceeded` | #CB3939 | #CB3939 | #CB3939 | swatch | η > 1,00 | UtilizationColors.Color(> 1,00) | 0 |

## 4. Token tipografici

Dimensioni censite nel codice storico con `tools/qa/f5_censimento_temi.py` (sezione 5). Le fonti sono due:
- `Ui.Text(testo, dimensione…)`: 13 quando la dimensione è omessa;
- `FontSize = n` nel C#.

Per ogni fascia il token prende la dimensione più usata.

| Token | Valore | Uso | Censimento della fascia (occorrenze) |
| --- | --- | --- | --- |
| `Font.Display` | 30 | titoli delle pagine: home, moduli, modelli | 30 (4), 26 (4), 24 (2), 34 (1) |
| `Font.Title` | 22 | titoli di sezione e delle finestre | 22 (9), 21 (8), 20 (7), 23 (4), 19 (3) |
| `Font.Heading` | 16 | intestazioni dei gruppi | 16 (19), 17 (12), 18 (9) |
| `Font.Subheading` | 14 | sottotitoli e valori in evidenza (aggiunto dopo il punto di controllo, D3) | 14 (45), 15 (27), 15,5 (1) |
| `Font.Body` | 13 | testo ed etichette dei campi; dimensione dello stile `Window` | 13 (107) |
| `Font.Small` | 12 | etichette del form compatto, tabelle, stato | 12 (240) |
| `Font.Caption` | 11 | unità, note ed errori dei campi, legende | 11 (181), 10 (12) |

Note sul censimento:
- **Display.** 30 e 26 sono pari (4 usi ciascuno). Il 30 è il valore della proposta ed è quello dei titoli delle pagine;
  il 26 è usato per il nome della sezione e per la Wiki.
- **Title.** La proposta indicava 21/20; il censimento dà 22 (9 usi contro 8 del 21). Tre dei nove usi del 22 sono i
  valori grandi della palificata, non titoli.
- **Heading.** La proposta indicava 17/16; il censimento dà 16.
- **14 e 15.** Il 14 (45 usi), il 15 (27) e il 15,5 (1) non avevano un token. Al punto di controllo del 16/10 l'utente ha
  scelto `Font.Subheading` = 14 (D3); il 15 e il 15,5 vanno a 14 o a 16 modulo per modulo, con schermate prima/dopo.
- **Disegni.** I disegni (`FormattedText`: 10, 11, 12 e 13 espressioni) restano come sono.

## 5. Varianti vicine da accorpare modulo per modulo (Q6, raccomandazione c)

### 5.1 Censimento

`tools/qa/f5_censimento_temi.py` legge i sorgenti C# e XAML di `X.Desktop`. Esclude:
- le viste nuove;
- il catalogo dei token;
- la tavolozza `Appearance.cs`, che contiene i colori di arrivo di Scuro e Molto scuro.

Comprende i dizionari storici spostati in `Themes`. Il risultato è in
`supporto/artefatti/refactoring/f5/f5-3-temi/censimento/` (`censimento-temi.json` e `.md`).

I numeri:
- 168 file, **243 colori distinti, 1 066 occorrenze**. Sono contati esadecimali, `Brushes.*`, `Colors.*`, `Ui.Navy/Blue/Bg/Muted`,
  i pennelli nominati di `Appearance` e le risorse `Appearance*`;
- i 54 colori distinti dei token coprono **794 occorrenze**;
- le varianti vicine sono **57 colori, con 77 occorrenze**. Una variante vicina è un colore diverso con ruolo compatibile
  e distanza CIE76 ΔE ≤ 5 nello spazio Lab D65. Tutti gli altri colori sono tinte distinte: esiti, serie, colori dei
  disegni e dei materiali.

Il ruolo di un'occorrenza si deduce dalla proprietà o dal metodo della tavolozza più vicini sulla riga. Il censimento è
quindi indicativo, non una prova.

### 5.2 Come si accorpano

Con la raccomandazione (c) le varianti si accorpano solo nelle viste nuove, quando il modulo migra:
- la vista nuova usa il token al posto della variante;
- la revisione del modulo mostra le schermate prima e dopo nei tre aspetti;
- la variante resta nelle viste storiche finché esistono;
- la Chiara delle viste non migrate non cambia.

La tabella elenca le varianti di ogni token con distanza, occorrenze e file principali. Le più frequenti:
- `#D8E0EB`, 6 occorrenze, bordo dei pannelli (`Ui.Paper`, materiali, reologia, fondazioni, progetti): vicino a `Border` (ΔE 2,3) e a `Border.Strong` (4,2). Da decidere a quale
  token assegnarlo (domanda D4);
- `#DCE5EF`, 3 occorrenze, progetti: praticamente `TreeGuide` (ΔE 0,5);
- `#F3F6FA`, `#F6F9FC`, `#F5F9FD`: fondi chiari di muri e progetti, fra `Surface` e `PaperAlt`;
- `#FFF3DF`, `#FFEDD5`, `#FFFBF2`: fondi degli avvisi, fra `Note.Background`, `Badge.Warn` e `Warning.Background`;
- `#895C17`, `#8B5916`: testo delle note del ponte, vicino a `Note.Foreground`;
- nei disegni, una ventina di grigi chiari intorno a `Drawing.Grid` e dieci intorno a `Drawing.Guide`.

### 5.3 Tabella delle varianti


| Token | Chiara | Varianti (ΔE CIE76; occorrenze; file principali) |
|---|---|---|
| `Surface` | #F3F5F8 | #F3F6FA (ΔE 0.7; 2; RetainingWallWorkspace.Inputs.cs, RetainingWallWorkspace.Results.cs); #F6F9FC (ΔE 1.4; 1; ProjectWorkspace.cs); #F5F9FD (ΔE 1.5; 1; ProjectHierarchy.cs); #EDF3F9 (ΔE 2.2; 1; ProjectSharing.cs); #E6ECF3 (ΔE 4.1; 2; ProjectWorkspace.cs); #EDF5FF (ΔE 4.1; 2; MinimumConcrete.cs, MixAutomation.cs) |
| `Paper` | #FFFFFF | #F6F9FC (ΔE 2.9; 1; ProjectWorkspace.cs); #F5F9FD (ΔE 3.3; 1; ProjectHierarchy.cs); #F3F6FA (ΔE 4.0; 2; RetainingWallWorkspace.Inputs.cs, RetainingWallWorkspace.Results.cs); #FFFBF2 (ΔE 5.0; 1; ProjectSharing.cs) |
| `PaperAlt` | #F8FAFC | #F6F9FC (ΔE 0.7; 1; ProjectWorkspace.cs); #F5F9FD (ΔE 1.3; 1; ProjectHierarchy.cs); #F3F6FA (ΔE 1.8; 2; RetainingWallWorkspace.Inputs.cs, RetainingWallWorkspace.Results.cs); #EDF3F9 (ΔE 3.6; 1; ProjectSharing.cs); #EDF5FF (ΔE 4.9; 2; MinimumConcrete.cs, MixAutomation.cs) |
| `Field.Background` | #F8FAFC | #F6F9FC (ΔE 0.7; 1; ProjectWorkspace.cs); #F5F9FD (ΔE 1.3; 1; ProjectHierarchy.cs); #F3F6FA (ΔE 1.8; 2; RetainingWallWorkspace.Inputs.cs, RetainingWallWorkspace.Results.cs); #EDF3F9 (ΔE 3.6; 1; ProjectSharing.cs); #EDF5FF (ΔE 4.9; 2; MinimumConcrete.cs, MixAutomation.cs) |
| `Header` | #E8EFF7 | #E6ECF3 (ΔE 1.2; 2; ProjectWorkspace.cs); #EDF3F9 (ΔE 1.8; 1; ProjectSharing.cs); #EDF5FF (ΔE 2.3; 2; MinimumConcrete.cs, MixAutomation.cs); #F3F6FA (ΔE 3.6; 2; RetainingWallWorkspace.Inputs.cs, RetainingWallWorkspace.Results.cs); #DBEBF7 (ΔE 4.1; 2; FoundationViews.cs); #F5F9FD (ΔE 4.3; 1; ProjectHierarchy.cs); #F6F9FC (ΔE 4.7; 1; ProjectWorkspace.cs) |
| `Border` | #DCE2E9 | #DCE5EF (ΔE 2.1; 3; ProjectPresentation.cs, ProjectRevisionNavigation.cs); #D8E0EB (ΔE 2.3; 6; ConcreteTimeEffectsView.cs, FoundationViews.cs) |
| `Border.Strong` | #CAD7E5 | #CBD7E5 (ΔE 0.3; 2; ProjectSharing.cs); #D8E0EB (ΔE 4.2; 6; ConcreteTimeEffectsView.cs, FoundationViews.cs) |
| `Divider` | #E4EAF1 | #DCE5EF (ΔE 2.7; 3; ProjectPresentation.cs, ProjectRevisionNavigation.cs); #EDF1F6 (ΔE 2.8; 1; ProjectPresentation.cs); #EDF2F7 (ΔE 3.0; 1; MainWindow.cs); #D8E0EB (ΔE 4.2; 6; ConcreteTimeEffectsView.cs, FoundationViews.cs) |
| `TreeGuide` | #DCE5EE | #DCE5EF (ΔE 0.5; 3; ProjectPresentation.cs, ProjectRevisionNavigation.cs); #D8E0EB (ΔE 2.0; 6; ConcreteTimeEffectsView.cs, FoundationViews.cs) |
| `OnCommandBar.Muted` | #B9C8D8 | #BCCFE2 (ΔE 3.0; 1; BridgeDesignWorkspace.cs) |
| `Hover` | #E3EFFA | #DBEBF7 (ΔE 2.2; 2; FoundationViews.cs); #EDF5FF (ΔE 2.8; 2; MinimumConcrete.cs, MixAutomation.cs); #E6ECF3 (ΔE 2.9; 2; ProjectWorkspace.cs); #EDF3F9 (ΔE 3.7; 1; ProjectSharing.cs) |
| `Hover.Row` | #F0F6FC | #EDF3F9 (ΔE 1.0; 1; ProjectSharing.cs); #F3F6FA (ΔE 1.4; 2; RetainingWallWorkspace.Inputs.cs, RetainingWallWorkspace.Results.cs); #F5F9FD (ΔE 1.7; 1; ProjectHierarchy.cs); #EDF5FF (ΔE 2.2; 2; MinimumConcrete.cs, MixAutomation.cs); #F6F9FC (ΔE 2.2; 1; ProjectWorkspace.cs); #E6ECF3 (ΔE 3.5; 2; ProjectWorkspace.cs) |
| `Selection` | #E2EFFC | #DCEBFA (ΔE 1.9; 1; StratigraphyWorkspace.cs) |
| `Note.Background` | #FFF6DD | #FFF3DF (ΔE 2.8; 1; ProjectPresentation.cs); #FFEDD5 (ΔE 4.2; 1; PileReinforcementEditor.cs) |
| `Note.Foreground` | #865D16 | #895C17 (ΔE 2.0; 2; BridgeDesignOptimization.cs, BridgeDesignWorkspace.cs); #8B5916 (ΔE 4.7; 3; BridgeDetachedView.cs, BridgeDrawing.cs) |
| `Warning.Background` | #FFFAEB | #FFFBF2 (ΔE 3.1; 1; ProjectSharing.cs); #FFF3DF (ΔE 4.2; 1; ProjectPresentation.cs) |
| `Badge.Warn` | #FFF0D8 | #FFEDD5 (ΔE 1.4; 1; PileReinforcementEditor.cs); #FFF3DF (ΔE 2.5; 1; ProjectPresentation.cs) |
| `Drawing.Paper` | #FFFFFF | #F5F8FC (ΔE 3.4; 1; BridgeMethodSketch.cs); #F5F5F5 (ΔE 3.5; 1; Ui.cs); #F2F4F6 (ΔE 4.1; 1; RetainingWallBarDrawing.cs); #F1F6FA (ΔE 4.3; 1; BridgeDetailSketch.cs); #F1F4F7 (ΔE 4.4; 1; RetainingWallDrawing.Reinforcement.cs); #F1F2F3 (ΔE 4.6; 1; RetainingWallDrawing.Ground.cs) |
| `Drawing.Canvas` | #F8FAFD | #F5F8FC (ΔE 1.0; 1; BridgeMethodSketch.cs); #F1F6FA (ΔE 2.0; 1; BridgeDetailSketch.cs); #F2F4F6 (ΔE 2.2; 1; RetainingWallBarDrawing.cs); #F1F4F7 (ΔE 2.2; 1; RetainingWallDrawing.Reinforcement.cs); #F5F5F5 (ΔE 2.4; 1; Ui.cs); #F1F2F3 (ΔE 3.0; 1; RetainingWallDrawing.Ground.cs); #EFF1F4 (ΔE 3.1; 1; GlobalStabilityDrawing.cs); #EBF0F6 (ΔE 4.1; 1; BridgeDesignDrawing.cs); #E9EFF3 (ΔE 4.4; 1; BridgeDesignDrawing.cs) |
| `Drawing.Muted` | #64748B | #66788A (ΔE 3.4; 1; ConcreteDimensions.cs); #667788 (ΔE 3.8; 1; GlobalStabilityDrawing.cs); #58758C (ΔE 4.3; 1; ProjectPresentation.cs); #536F87 (ΔE 4.8; 1; BridgeDesignDrawing.cs) |
| `Drawing.Grid` | #E8EDF3 | #EBF0F6 (ΔE 1.0; 1; BridgeDesignDrawing.cs); #E9EFF3 (ΔE 1.2; 1; BridgeDesignDrawing.cs); #E3EBF1 (ΔE 1.4; 1; BridgeDesignDrawing.cs); #E3EAF1 (ΔE 1.5; 3; BridgeOptimizationPlot.cs, ConcreteStrainPlane.cs); #E2E8EF (ΔE 1.9; 1; BridgeMethodSketch.cs); #E2E8F0 (ΔE 2.1; 2; HorizontalDiagrams.cs, PileReinforcementDrawing.cs); #E5E7EB (ΔE 2.4; 1; Drawings.cs); #EFF1F4 (ΔE 2.4; 1; GlobalStabilityDrawing.cs); #F1F4F7 (ΔE 3.0; 1; RetainingWallDrawing.Reinforcement.cs); #F1F6FA (ΔE 3.3; 1; BridgeDetailSketch.cs); #DCE7EF (ΔE 3.4; 1; BridgeDetailSketch.cs); #F2F4F6 (ΔE 3.4; 1; RetainingWallBarDrawing.cs); #DCE6EA (ΔE 3.5; 1; BridgeDesignDrawing.cs); #F1F2F3 (ΔE 3.5; 1; RetainingWallDrawing.Ground.cs); #DCE3EB (ΔE 3.8; 2; BridgeDrawing.Stress.cs); #F5F8FC (ΔE 4.1; 1; BridgeMethodSketch.cs); #DAE2E9 (ΔE 4.2; 1; BridgeDrawing.cs); #DDECF7 (ΔE 4.3; 1; BridgeDesignDrawing.cs); #F5F5F5 (ΔE 4.6; 1; Ui.cs) |
| `Drawing.Guide` | #CBD5E1 | #CBD6E2 (ΔE 0.4; 1; BridgeMethodSketch.cs); #CED8E3 (ΔE 1.2; 1; BridgeDrawing.Annotations.cs); #CAD2DD (ΔE 1.3; 1; GlobalStabilityDrawing.cs); #CAD6E0 (ΔE 1.3; 1; RetainingWallIcon.cs); #CCD3DD (ΔE 1.5; 1; BridgeOptimizationPlot.cs); #CCD5DE (ΔE 1.6; 1; RetainingWallDrawing.cs); #CFDAE5 (ΔE 1.8; 1; BridgeMethodSketch.cs); #D5DEE7 (ΔE 3.6; 1; BridgeDrawing.cs); #D7DFE8 (ΔE 4.0; 1; ConcreteViewports.cs); #D5E1EA (ΔE 4.4; 1; BridgeDesignDrawing.cs) |
| `Swatch.Wall1` | #DBCDAE | #DDD3B9 (ΔE 3.7; 1; RetainingWallDrawing.Ground.cs) |
| `Swatch.Wall5` | #B9CFDC | #B7C9D6 (ΔE 2.3; 1; BridgeDrawing.cs); #B9C7D4 (ΔE 3.6; 1; ConcreteViewports.cs); #BDDBE6 (ΔE 4.7; 1; BridgeDesignDrawing.cs); #CAD6E0 (ΔE 4.8; 1; RetainingWallIcon.cs); #CBD6E2 (ΔE 5.0; 1; BridgeMethodSketch.cs) |
| `Utilization.Exceeded` | #CB3939 | #BF3737 (ΔE 4.7; 1; BridgeOptimizationPlot.cs) |

## 6. FieldForm sui token

Il `FieldForm` (`X.Desktop/Views/Common/FieldForm.xaml`, F5.2) è la prima vista nuova. Usa solo token. I token sono gli
stessi pennelli della tavolozza, quindi i colori e le dimensioni non cambiano.

| Elemento | Prima (F5.2) | Dopo (F5.3) |
| --- | --- | --- |
| testo di etichette, simboli e titoli dei gruppi | `Appearance.foreground.0B2A4A` | `Token.Ink` |
| pulsante «?» | `AppearancePaper`, `AppearanceInk` | `Token.Paper`, `Token.Ink` |
| campo modificabile | `Appearance.background.F8FAFC` | `Token.Field.Background` |
| campo calcolato | `Appearance.calculated.EAF2FA` (F5.2) | `Token.Calculated` |
| unità | `Appearance.foreground.64748B` (F5.2) | `Token.Muted` |
| nota del campo | `Appearance.background.FFF6DD`, `border.EEDCAF`, `foreground.865D16` (F5.2) | `Token.Note.Background`, `Note.Border`, `Note.Foreground` |
| errore del validatore | `Appearance.foreground.9A4D0A` | `Token.Warning.Foreground` |
| dimensioni | 13/12 con un convertitore, 12, 11 scritti | `Font.Body`, con `Font.Small` dal trigger del form compatto; `Font.Small`; `Font.Caption` |

Le cinque risorse marcate «F5.2» esistevano solo per il FieldForm. Ora sono assorbite dai token e tolte da
`AppearanceResources`.

### Schermate nei tre aspetti

Le schermate vengono dal passo «componenti-campi» di `--check-contrast` dopo F5.3: un dialogo della finestra principale con
i due form di prova, fuori schermo. Le immagini sono nella cartella non versionata
`supporto/artefatti/refactoring/f5/f5-3-temi/schermate/` del worktree `C:\Users\g.pacini\AppData\Local\Temp\aw-f5-3-temi`:

| Aspetto | Form | Form con il gruppo aperto |
| --- | --- | --- |
| Chiara | `fieldform-chiara.png` | `fieldform-chiara-espansa.png` |
| Scuro | `fieldform-scuro.png` | `fieldform-scuro-espansa.png` |
| Molto scuro | `fieldform-molto-scuro.png` | `fieldform-molto-scuro-espansa.png` |

Ci sono anche le immagini dello stesso form prodotte nell'ambito chiaro dei documenti partendo dai tre aspetti:
`documento-da-chiara.png`, `documento-da-scuro.png` e `documento-da-molto-scuro.png`. Sono uguali byte per byte, come
verifica `--check-theme-tokens`.

## 7. Verifiche

Le cartelle citate sono in `supporto/artefatti/refactoring/f5/f5-3-temi/` del worktree e non sono versionate.

### 7.1 Contrasto e invarianza delle viste storiche

**Il DPI.** La misura del contrasto dai pixel dipende dal DPI delle finestre, e il DPI cambia con il modo di avvio:
- il runner avvia `ANTHEA.exe`, che con `app.manifest` è PerMonitorV2. Le finestre della prova stanno fuori da ogni monitor
  e ricevono **96 DPI**, qualunque sia la scala dello schermo;
- `dotnet ANTHEA.dll` ha il DPI di sistema: 120 oggi, 144 l'8/10.

I 719 difetti delle note dell'8/10 erano misurati a 144 DPI.

Per questo la baseline della prova è a 96 DPI, catturata con il binario del commit base avviato come nel runner. Una corsa
a un altro DPI fallisce senza confrontare.

**Corse.** Tutte le corse hanno 877 catture. L'archivio di prova `supporto/artefatti/prova-f1.4.programma` manca in tutti i
checkout, quindi le viste dei progetti con quell'archivio non sono coperte, prima come dopo. Dal 24/10 (G8) l'archivio è versionato in
`tests/fixtures/f5/contrasto-progetti.programma` e quelle viste sono nella corsa ([f5-13-shell §14.5](f5-13-shell.md#145-contrasto-d12)).

| Corsa | Codice | Avvio, DPI | Chiara | Scuro | Molto scuro |
| --- | --- | --- | --- | --- | --- |
| `contrasto-prima` | base 5f29067b | dotnet, 120 | 702 difetti, 190 voci | 0 | 0 |
| `contrasto-prima-2` | base 5f29067b | dotnet, 120 | 702, stesse voci | 0 | 0 |
| `contrasto-dopo` | F5.3 | dotnet, 120 | 702, stesse voci | 0 | 0 |
| `contrasto-prima-96` | base 5f29067b | ANTHEA.exe, 96 | 699 difetti, 187 voci: è la baseline | 0 | 0 |
| runner `20261016-110825-f53-finale` | F5.3, 945ef25c | ANTHEA.exe, 96 | 699, stesse 187 voci | 0 | 0 |

Invarianza pixel per pixel prima e dopo F5.3:
- **A 120 DPI** (`contrasto-dopo` con `--riferimento contrasto-prima`):
  - Chiara: 294 catture su 295 identiche. Quella diversa, il verificatore c.a. del palo orizzontale con 3 pixel, varia
    anche fra le due corse del codice base: la cattura dopo è identica a `contrasto-prima-2`;
  - Scuro e Molto scuro: 289 su 291 identiche;
  - le 2+2 catture diverse sono `str_mista_ponte`, scheda Soletta: cambia il fondo dei tre campi φ, ψL e n. È la condizione
    di tempo nota di quelle catture (`decisioni.md`, «Tema scuro», seguiti). Due corse ripetute solo su `str_mista_ponte`
    con lo stesso binario (`ponte-dopo-1`, `ponte-dopo-2`) danno 23 catture su 23 identiche alla cattura prima, in tutti e
    tre gli aspetti.
- **A 96 DPI** (runner contro `contrasto-prima-96`): 875 PNG su 877 identici byte per byte. I 2 diversi sono le stesse
  catture Scuro di `str_mista_ponte`.
- **FieldForm:** le catture del passo «componenti-campi» sono identiche byte per byte prima e dopo, nei tre aspetti.

Le viste storiche restano quindi identiche nei tre aspetti.

**Baseline della Chiara** (`tests/fixtures/f5/contrasto-chiara.json`):
- 699 difetti in 187 voci di vista e chiave, a 96 DPI;
- 10 sono del FieldForm nel passo «componenti-campi».

Prove dell'esito con il solo passo componenti (`baseline-prove`, `baseline-prove-96`):
- baseline uguale: PASS;
- un'occorrenza in meno nella baseline: FAIL, con la voce;
- due in più: PASS, con la nota di abbassare la baseline;
- baseline assente: FAIL;
- DPI diverso (`dotnet ANTHEA.dll`, 120): FAIL.

### 7.2 Altre prove

| Prova | Esito | Cartella |
| --- | --- | --- |
| `--check-theme-tokens` (uguaglianza dei colori, dizionari, ambito chiaro) | PASS 129 controlli | `token-3` |
| `--check-field-form-parity` | PASS 2025 controlli di parità FieldForm/InputForm, come in F5.2 | `parita` |
| `ANTHEA.Architecture.Tests` | PASS 506 controlli; P1–P7 rispettate; 127 prove dei rilevatori; P3 130 voci; F4 senza aggiunte | `arch-c4` |
| P7 sul FieldForm di F5.2 | 16 violazioni, tutte risorse storiche: la regola le avrebbe trovate | `arch-p7-prima` |

### 7.3 Runner

**Prima corsa**, `-Stage build,fast,wiki,ui`, tag `f53-finale`, su 945ef25c (`supporto/artefatti/ci/20261016-110825-f53-finale`):
PASS 108 e NEW-FAIL 2.
- `ui/check-contrast` è fallita perché la baseline era a 120 DPI, misurata con `dotnet`, mentre il runner misura a 96 DPI. La
  baseline è stata ricatturata dal binario base con `ANTHEA.exe`, e i difetti del runner coincidono con essa (sezione 7.1).
- `ui/smoke-display` è fallita in `VerifyStratigraphyEditing` con «Il ricalcolo interrompe la digitazione». La prova chiede
  il fuoco da tastiera in una finestra vera: quel fuoco dipende dal desktop, che altre prove in corso sulla macchina stavano
  usando. Ripetuta da sola sullo stesso binario: PASS (`20261016-114203-f53-smoke-display`).

Nella stessa corsa sono passate:
- `ui/check-appearance`;
- `ui/f3-equivalence`, con lo stesso file di transizioni;
- `ui/check-theme-tokens`;
- `ui/check-field-form-parity`;
- le altre suite.

**Corsa finale**, `-Stage build,fast,wiki,ui`, tag `f53-finale2`, su 766bde04
(`supporto/artefatti/ci/20261016-120021-f53-finale2`): **PASS 110, KNOWN 0, NEW-FAIL 0**. Erano 108 suite in F5.2; le due
nuove sono `ui/check-theme-tokens` e `ui/check-contrast`.

| Suite | Esito |
| --- | --- |
| `ui/check-contrast` | PASS in 799 s: Scuro 0, Molto scuro 0; Chiara 699 difetti in 187 voci, uguali alla baseline |
| `ui/check-theme-tokens` | PASS |
| `ui/check-appearance` | PASS |
| `ui/smoke-display` | PASS |
| `ui/check-field-form-parity` | PASS |
| `ui/f3-equivalence` | PASS: 36 fixture, 73 articoli, 30 ricerche, report di progetto |
| `Architecture.Tests` | PASS |

Per `ui/f3-equivalence`: il riferimento `tests/fixtures/f3` e il file delle transizioni sono quelli del commit base
5f29067b, senza alcuna differenza.

Lo stadio ui dura circa 13 minuti in più per `ui/check-contrast`.

## 8. Domande aperte e scelte da confermare

**Esito del punto di controllo (16/10).**
- L'utente ha accettato tutte le raccomandazioni: D1 (c), D7 con nota quando i difetti scendono, e D2, D4, D5, D6, D8 e
  D10 come proposte.
- Per D3 ha scelto `Font.Subheading` = 14.
- D9 resta fuori dal runner.

Dettagli in [decisioni](decisioni.md), nella sezione del 16 ottobre pomeriggio.

Il lavoro segue le raccomandazioni e, dove serviva una scelta, l'opzione più prudente. Da confermare:

**D1 — Q6.** Raccomandazione (c): token semantici usati solo dalle viste nuove; varianti vicine accorpate modulo per modulo,
con schermate prima/dopo nei tre aspetti; la Chiara non cambia fuori dalle viste migrate.

**D2 — Elenco dei token** (sezione 3). Ci sono 36 token semantici e i gruppi Series (7), Drawing (8), Swatch (12) e
Utilization (6). In particolare:
- (a) `PaperAlt` (righe alterne) e `Field.Background` (campi) hanno lo stesso colore, `#F8FAFC`, e lo stesso pennello:
  restano due nomi per due usi?
- (b) `Status.Ok` è `DarkGreen` (`#006400`, 6 usi). `ForestGreen` (`#228B22`, 2 usi) resta una variante: è il «Disponibile»
  della home, che nella Chiara ha un difetto di contrasto noto (4,35:1).
- (c) `Disabled` è `Gray` (`#808080`), il colore di `Appearance.Dim(Brushes.Gray)`. Il sistema usa `GrayText` per gli
  elementi disattivati delle tendine: nella Chiara è un altro grigio, mentre negli aspetti scuri i due coincidono.
- (d) `Utilization.*` ha il ruolo identità, come nei disegni e nelle schede di verifica. Le legende storiche schiariscono
  gli stessi colori con `Appearance.Series`: una legenda su fondo scuro di una vista nuova userebbe il gruppo Series, che
  oggi non ha i colori di η. Si aggiungono quando servono?
- (e) Ci sono due tavolozze degli strati: `Swatch.Soil1–6` per i disegni dei terreni e `Swatch.Wall1–6` per il muro. Le
  unifica il modulo che le usa?
- (f) Gli 8 token `Drawing.*` coprono fondo, testo, assi, griglia e guide dei disegni tecnici.

**D3 — Token tipografici** (sezione 4).
- I valori vengono dal censimento: Title 22 e Heading 16. La proposta diceva 21/20 e 17/16.
- Il 14 (45 usi) e il 15 (27) non hanno un token. Si aggiunge `Font.Subheading` = 14, oppure 14 e 15 si accorpano in Body
  o Heading modulo per modulo?

**D4 — Varianti vicine** (sezione 5).
- Criterio: ΔE ≤ 5 con un ruolo compatibile.
- `#D8E0EB` (bordo di `Ui.Paper`, 6 usi) va a `Border` o a `Border.Strong`?

**D5 — Regola P7** (sezione 2.5).
- Ammessi `Transparent` e `{x:Null}` nel XAML.
- Vietati nel C# anche `Brushes.Transparent` e `Colors.*`, come `Brushes.*`.
- Vietati i membri colore della tavolozza storica e le risorse storiche.
- `SystemColors` non è controllato: le sue chiavi puntano già alla tavolozza.

**D6 — Baseline della Chiara.**
- I difetti sono 699, in 187 voci, a 96 DPI, la condizione del runner (`ANTHEA.exe`, sezione 7.1).
- I 719 dell'8/10 e i 702 di oggi con `dotnet ANTHEA.dll` sono misure al DPI di sistema, 144 e 120. Una corsa a un altro
  DPI fallisce senza confrontare: va bene così?
- 10 dei 699 difetti sono del FieldForm nel passo «componenti-campi»: unità `#64748B` su `#F3F5F8`. È la stessa
  combinazione di `InputForm` su fondo `Surface`, come nel gruppo delle fondazioni. Correggerla cambierebbe la Chiara del
  form rispetto a `InputForm`, quindi resta nella baseline.

**D7 — Politica della baseline.**
- Fallisce una voce nuova o una voce con più occorrenze.
- Se le occorrenze scendono, la prova passa con una nota e la baseline si abbassa in un commit dedicato.
- In alternativa la prova fallisce anche quando i difetti scendono senza aggiornare la baseline, come la baseline F4 e
  quella P3. È più rigoroso, ma rende fragile una misura su pixel.

**D8 — Durata.** `ui/check-contrast` aggiunge circa 13 minuti allo stadio ui, che prima ne durava circa 15.

**D9 — Immagini dei report storici.** `--check-report-appearance-offscreen` controlla che le immagini dei report storici
siano uguali nei tre aspetti. Resta fuori dal runner. Lo si aggiunge allo stadio ui? F5.3 non lo richiede.

**D10 — Tabella congelata dei colori.** `tests/fixtures/f5/token-colori.json` si aggiorna solo con una modifica motivata
della tavolozza o del catalogo, nello stesso commit. Confermare.

**D11 — Documenti di avanzamento.** In questo ramo non sono aggiornati `piano.md` e `diario.md`, per non creare conflitti con
il ramo del pilota (f5-4-pilota). Li aggiorna il coordinatore al punto di controllo.
