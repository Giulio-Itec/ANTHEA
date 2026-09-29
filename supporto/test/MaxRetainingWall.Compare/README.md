# Ripetizione del confronto con MAX

Questo progetto non esegue MAX e non inventa risultati di riferimento. Legge un modello ANTHEA e un cerchio effettivamente rilevato da MAX, poi calcola il medesimo cerchio con il solutore Bishop separato.

Il JSON di riferimento deve contenere:

| Campo | Contenuto |
|---|---|
| `model` | Percorso del file `.anthea`, relativo al JSON oppure assoluto |
| `max_model` | Percorso del file nativo `.mrt`, accompagnato dalla sua cartella dati |
| `max_evidence` | Relazione o schermata che documenta il risultato letto |
| `max_version` | Versione precisa rilevata in MAX |
| `combination` | Nome esatto della combinazione globale salvata in ANTHEA |
| `max_value` | Valore realmente letto da MAX |
| `max_value_kind` | `F`, `F/gammaR` oppure `gammaR/F`, secondo la definizione documentata del risultato |
| `max_slices` | Numero di conci di MAX |
| `circle` | Oggetto con `x`, `y`, `radius`, `left`, `right`, `translation_x`, `translation_y` |

Le coordinate del cerchio sono quelle di MAX. Le due traslazioni, dichiarate esplicitamente, portano le coordinate nell'origine ANTHEA, posta al piede di valle della fondazione. Le ascisse `left` e `right` sono gli estremi effettivi della superficie di scorrimento. Il programma controlla il loro allineamento al profilo, con tolleranza di 5 mm per dati letti arrotondati. Per un riscontro più preciso usare tutti i decimali disponibili.

Esempio di comando, dopo aver compilato il file con i valori misurati:

```powershell
dotnet run --project supporto/test/MaxRetainingWall.Compare -c Release -- supporto/artefatti/stabilita-globale/confronti-max/01/riferimento.json supporto/artefatti/stabilita-globale/confronti-max/01/confronto
```

Il JSON e il CSV prodotti distinguono F, F/γR e tasso η. La suddivisione ANTHEA aggiunge i confini geometrici e stratigrafici ai conci richiesti: il numero effettivo è sempre registrato. Il controllo ripete il calcolo con 60, 120 e 200 conci per valutare questa sensibilità, senza dichiarare automaticamente accettabile uno scostamento. Il minimo della ricerca ANTHEA è conservato separatamente dal risultato sul cerchio assegnato.

Prima del confronto allineare terreno, falda, geometria, azioni, coefficienti M/A/R, verso del sisma e peso del muro. La documentazione MAX descrive una ricerca per maglia di centri e famiglie passanti per il tacco, differente dalla ricerca ANTHEA per ingresso, uscita e profondità. Una differenza fra i minimi può quindi derivare dal dominio esplorato.
