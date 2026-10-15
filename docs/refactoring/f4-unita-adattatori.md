# F4.6b — conversioni esplicite

13 ottobre 2026. L'adattatore esistente `ConcreteLibraryMapping` ricava da
`GPC.Model.Units` i fattori N/kN, Nmm/kNm e mm/m. Le operazioni mantengono
l'ordine moltiplicazione/divisione precedente, anche per lo zero con segno.
I tre passaggi di diametro del palo elastico (sezione, CHS e zona sismica)
richiamano la stessa mappatura nominata. Nessun secondo convertitore.

In Checker `0cb1c206`, i confini delle armature usano i medesimi fattori
con nome: azioni, capacità, ancoraggi, sovrapposizioni, traslazione del diagramma,
staffe/spirali e prescrizioni sismiche. Contratti pubblici kN/kNm/m invariati;
i metodi di sezione ricevono N/Nmm/mm. Concrete candidata 0.2.0.0, non ancora
distribuita in ANTHEA: l'attivazione richiede il successivo set coerente.

Corretta una descrizione dell'inventario: la tolleranza assiale storica è
max(1000 N, 1e-6·|N in N|), non max(1000 N, 0.001·|N in N|).
Non si cambia il criterio del solutore durante questo port di unità.

## Evidenze

- 48 controlli nuovi sui sei versi di conversione, valori positivi/negativi,
  molto piccoli/grandi e zeri con segno: coincidenza bit per bit.
- Runner mirato finale `20261013-f4-units-final`: 5 PASS, nessun fallimento.
  Corrette due anomalie del test iniziale (nome locale duplicato e vecchio
  elenco dei letterali: ora è ammesso soltanto 1 nel file di mappatura).
- Corpus generale: 548 file prima/dopo equivalenti, stessa diagnostica,
  `20261013-f4-units`. Nessuna modifica a risultati attesi o tolleranze.
- Corpus F4 ripetuto: 115 casi, 14 rifiuti attesi e 234 file identici fra
  le due corse; confronto con il riferimento congelato: zero differenze
  non ammesse. `20261013-f4-units-corpus` e
  `supporto/artefatti/refactoring/f4/units-f4-before-after.json`.
- 735 prove Concrete PASS; 11 casi di palo mantengono SHA-256
  `5E75A6432480694166F77216554D772AF00FFFB079495F6A45BF12458851985E`.
- Baseline architetturale ridotta da 4610 a 4604 segnalazioni: rimossi
  soltanto i sei rilievi dei tre fattori letterali eliminati.

Output del runner nell'area `supporto/artefatti/ci` del worktree di prova
`C:/Users/g.pacini/.codex/worktrees/model-viewer-draft/ANTHEA`.
Nessun calcolo, norma, mesh Delaunay o documento utente alterato.
