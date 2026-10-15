# F4 — confronto con la cattura UI storica F3

Stato del 14/10/2026: **da approvare, nessuna deroga applicata al runner**.
Il confronto rimane esatto secondo le tolleranze originali. Non sono stati
aggiornati i 458 file di riferimento F3 né gli attesi ingegneristici indipendenti.

La prima corsa full su 1.8.0/set W ha confrontato 36 fixture, 71 articoli,
30 ricerche Wiki e il report di progetto. Il confronto produce 6805 differenze
non ammesse, senza troncamento; riguardano 65 file. Il JSON originale contiene
file, percorso, valore precedente, valore nuovo e scarto per ogni differenza:

`C:/Users/g.pacini/.codex/worktrees/model-viewer-draft/ANTHEA/supporto/artefatti/ci/20261014-f4-1.8.0-full/ui_f3-equivalence/cattura/confronto.json`.

La proposta di ammettere transizioni tramite impronte di interi file è stata
respinta dalla revisione automatica: ambito troppo ampio, da restringere e
approvare caso per caso. Il codice proposto è stato rimosso. Questa scheda è
solo un prospetto di revisione e **non viene letta dai test**.

## Riscontri per categoria

| Categoria | Differenze | Riscontro e proposta |
|---|---:|---|
| Muri | 3326 | F4-03, F4-08, F4-13, F4-14. 180 curvature delle tre fixture a mensola diventano tre volte il piano elastico equivalente, con φ=2. Sono la correzione della deformazione totale già verificata da sei attesi cinematici indipendenti. Nei risultati strutturali, le 960 variazioni oltre 1e-10 riguardano soltanto resistenza/rapporto del taglio senza staffe, per il contributo assiale prima omesso. Le altre 2180 variazioni numeriche strutturali sono arrotondamenti entro 1e-10; nessuna tolleranza viene cambiata. Sei valori formattati dei report seguono il taglio corretto. |
| Palo elastico | 1096 | F4-15: aggiunta di Outcome, Execution, Data e richieste di completamento tipizzate. Nessuna variazione dei risultati numerici. La proprietà tempi_ms resta l'unica esclusione temporale già prevista dalla policy originaria. |
| Bridge Design | 994 | F4-23/28/29. In ciascuna delle due fixture, 210 stazioni diventano 209: rimossa una stazione quasi duplicata. Confrontando per campata e ascissa, **tutte** le ordinate di M, V e freccia delle stazioni mantenute sono identiche. Altre differenze: modello uniforme legacy esplicito, nota di migrazione, revisione dei fattori ambientali e sua provenienza nel report. |
| Fondazioni | 4 | F4-35: solo il numero della DLL Geotechnics, 0.1.3.3 → 0.3.0.0, in due risultati e due report. |
| Sezione CA | 145 | F4-31/33: nota di migrazione normativa nei tre archivi privi di norma esplicita; coefficienti conservati. Nel report DIN la nota di ambito resta nell'introduzione: scompare una sua ripetizione, con conseguente spostamento degli stili. Da approvare precisamente sui percorsi elencati nel JSON. |
| Report e snapshot di progetto | 1054 | Capitoli con provenienza e note aggiornate, struttura delle tabelle/figure. Il test indipendente ui/f3-report-chapters confronta tutte le parti XML e le immagini con la scheda e passa dopo aver inizializzato la medesima revisione ambientale. Restano da approvare le differenze testuali/strutturali rispetto alla cattura storica. |
| Wiki e ricerca | 186 | Guide Rev43, input normativi, prezzari e traffico. Fonti e rendering verificati; indice derivato dalle guide. Da approvare come variazioni editoriali sui percorsi specifici. |

## Controlli da mantenere dopo l'approvazione

1. Ammissione limitata a singoli percorsi e valori/transizioni documentati;
   nessuna esclusione generale di file, moduli o famiglie di risultati.
2. Confronti ingegneristici F4 contro le catture preventive e gli attesi
   indipendenti ancora obbligatori. Nessuna rigenerazione degli attesi.
3. Prove negative: nuova variazione numerica, nuovo campo, rimozione imprevista
   e alterazione del riferimento devono fallire.
4. Nuova corsa completa; merge e rilascio definitivo solo dopo esito conforme.

## Evidenze già disponibili

- `supporto/artefatti/refactoring/f4/full-1.8.0.log`: prima corsa, 114 PASS e
  cinque fallimenti; quattro corretti nei commit successivi.
- `predalle-fix.log`: nove esiti PASS, inclusi 53 casi congelati e WPF.
- `wall-inactive.log` e `wall-legacy.log`: nove esiti PASS, verifiche,
  distinte/ricerca e suite storica dei muri.
- `wiki-chapters.log`: tre esiti PASS, Wiki e confronto dei capitoli.
- `f4-taglio-muri.md`, `f4-adattatori-muri.md`, registro F4-03/F4-08/F4-23:
  studi e confronti numerici originari, mantenuti invariati.

La sola presenza di questa classificazione proposta non chiude F4.16.
