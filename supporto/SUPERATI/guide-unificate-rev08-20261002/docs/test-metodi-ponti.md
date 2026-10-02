# Test dei metodi per i ponti

Aggiornamento del 26 settembre 2026, sulle DLL presenti in `lib/Checker`.
La suite `supporto/test/X.Verifiche/BridgeMethodChecks.cs` verifica l'integrazione
degli ingressi e risultati di ANTHEA con i nuovi metodi di Checker.
Si aggiunge a `BridgeSectionChecks.cs`, senza sostituirne i riferimenti.

## Esecuzione

Dalla radice del repository:

```powershell
dotnet run --project supporto/test/X.Verifiche -c Release -- --bridge
```

Per eseguire soltanto i nuovi test usare `--bridge-methods`.
Gli errori restituiscono codice di uscita 1, con grandezza, risultato e riferimento.
Il log della campagna è in `supporto/artefatti/ponti_metodi_2026_09_26/bridge.log`.

## Copertura

| Funzione | Casi e controlli |
| --- | --- |
| Tre metodi di analisi | Due coppie N–M di segno diverso; confronto elastico indipendente su carpenteria |
| Getto e attivazione | Due momenti iniziali per ciascun metodo storico; deformazione al getto conservata, CLS e barre inizialmente scarichi |
| Scarico elastico | Due cicli carico/scarico per ciascun metodo storico; tensioni finali nulle |
| Ritiro incrementale | 24 eventi da −1 microdeformazione in ciascun metodo storico; somma −24 microdeformazioni e superamento del vecchio limite di venti fasi |
| Momento–curvatura | Rampe positiva e negativa, N = −50 kN, origine nello storico, M = EIκ al baricentro |
| Forza–deformazione | Rampe positiva e negativa, κ = 0, N = EAε |
| Ramo plastico | Trazione uniforme fino a 3000 e 4000 microdeformazioni, acciaio elastoplastico con fy = 235 MPa; tensione al plateau e deformazione plastica analitica |
| Omogeneizzazione | φ = 0,5 e 2, confronto con l'ingresso equivalente n nel metodo storico lineare |
| Passaggio al non lineare | φ e classe 4 salvati non vengono modificati; la soluzione istantanea usa la sezione lorda |
| Risultati | Equilibrio integrato N–M, memoria degli stati, esportazione JSON/CSV e conversioni kN/N, 1/m/1/mm, microdeformazioni/deformazioni |
| Errori | Metodo inesistente, discretizzazioni nulle o frazionarie, punti/sottopassi frazionari, incremento nullo e annullamento dei tre metodi |

## Riferimenti indipendenti

La carpenteria di riferimento è composta da tre rettangoli:
piattabanda superiore 500 × 25 mm a y = −12,5 mm;
anima 14 × 1800 mm a y = −925 mm;
piattabanda inferiore 700 × 30 mm a y = −1840 mm.
Si usano E = 210000 MPa e le formule:

- A = Σ bi hi; yc = Σ Ai yi / A;
- I = Σ [bi hi³/12 + Ai(yi−yc)²];
- Mc = M0 + N yc; κ = Mc/(EI);
- ε0 = N/(EA) + κ yc; σ(y) = E(ε0−κy);
- oltre snervamento, per la legge elastoplastica assegnata: σ = fy,
  εp = ε−fy/E e N = fy A.

Aree, inerzie e risultati attesi sono calcolati nel test senza interrogare
il risolutore di produzione. L'equilibrio viene ricostruito dalle singole fibre:
N = Σ σi Ai e M0 = −Σ σi Ai yi.
Le tolleranze assolute sono esplicite accanto alle asserzioni, nelle rispettive
unità; per l'equilibrio integrato sono 0,01 N e 10 Nmm.

Campagna eseguita: **117 controlli precedenti e 2957 nuovi controlli superati**.
Il conteggio comprende i confronti delle singole fibre, non 2957 esempi distinti.
Queste prove verificano le API e l'integrazione del modulo: non sostituiscono
le prove della libreria su plasticità ciclica, classe 4, confronti OpenSees
o i controlli grafici WPF. Il documento Word di validazione non è rigenerato
da questo comando.
