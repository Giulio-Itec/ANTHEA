# Attesi indipendenti

Programmi che producono gli attesi di `supporto/test/casi_confronto.json` senza usare il codice di ANTHEA.

## `sezione_ca.py`

Ricostruzione Python del programma originale delle sezioni in c.a.: RCSectionEngine e `sezione_elastica.py`. Copre i
tipi `sezione`, `elastica`, `resistenza_elastica` e `dominio`, per le forme rettangolare, circolare e a T. Usa solo la
libreria standard di Python, dalla 3.8 in poi.

- **Fedeltà.** È uguale bit per bit ai 90 casi di sezione del programma originale, controlli compresi:
  - i rettangolari e i circolari del file attuale;
  - le 30 sezioni a T del commit `51e1c114` (25/09/2026), calcolate con la regola precedente delle barre laterali.
- **Barre laterali delle sezioni a T, regola attuale.** Le barre si ripartiscono in parti uguali fra gli angoli inferiore
  e superiore della staffa d'anima e l'ultima coppia cade nell'ala. Il programma originale le ripartiva in n + 1 parti
  fino all'intradosso dell'ala.

```text
python -I tools/attesi/sezione_ca.py --valida supporto/test/casi_confronto.json
python -I tools/attesi/sezione_ca.py --valida <copia di 51e1c114:supporto/test/casi_confronto.json> --barre-laterali originale
python -I tools/attesi/sezione_ca.py --confronta supporto/test/casi_confronto.json
python -I tools/attesi/sezione_ca.py --scrivi supporto/test/casi_confronto.json --provenienza "<testo>"
```

Il runner (`build/ci.ps1`, stadio fast) esegue `--valida` e `--confronta` su `supporto/test/casi_confronto.json`:
suite `attesi/sezione_ca-valida` e `attesi/sezione_ca-confronta` (decisione E6 del 15/10/2026, F2.12).

`--scrivi` sostituisce solo gli attesi dei casi di sezione che hanno `fonte_atteso` e rifiuta il file se la sua
riscrittura non è identica byte per byte all'originale. Tolleranza dei confronti: la stessa di `supporto/test/X.Verifiche`,
cioè |Δ| ≤ 1e-8 + 1e-10·|atteso|, con testi uguali. Registro: TEST-02.

## `palo_granulare.py`

Ricalcola gli attesi di `palo_storico_0` e `palo_storico_3` partendo dai risultati del programma originale, salvati in
`51e1c114:supporto/test/casi_confronto.json`, e cambia solo ciò che dipende da c′.

- **Strati granulari.** τs = K·μ·σ′v senza c′, come prescrive la guida teorica. I valori K, μ e σ′v vengono dai campi
  del tratto.
- **Valori derivati.** Resistenze laterali, aggregazioni NTC 2018 (media e minimo fra i sondaggi, ξ3 e ξ4, η/γ) e curve
  di compressione, trazione e progetto.
- **Validazione.** Le regole di aggregazione sono provate su tutti i casi di palo originali. Il programma lo fa a ogni
  esecuzione.

```text
python -I tools/attesi/palo_granulare.py --originale <copia di 51e1c114:supporto/test/casi_confronto.json> --confronta supporto/test/casi_confronto.json
```
