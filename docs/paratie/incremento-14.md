# Incremento 14: avvio dopo l'integrazione F5

Il merge anticipato richiesto dall'utente è concluso. La cartella principale
ANTHEA resta su `refactoring/f5`; la candidata integrata paratie/spettri viene
collaudata separatamente. Il nuovo lavoro non entra implicitamente in quella
candidata né in master delle librerie.

| Repository | Ramo di prosecuzione | Base integrata |
| --- | --- | --- |
| ANTHEA | refactoring/f5-paratie-seguito | e5441a0a78e301cb1a1d82b479b157bc2e2741c0 |
| Model | codex/paratie-seguito | e1e6e1079fb7b90b4ca1fee60e01d05a1b0423d9 |
| Checker | codex/paratie-seguito | dfb808c8ab5463b0df0ab965813320ddc38eb5f0 |

La correzione del solo driver delle catture F3/F5 (`0f989159`) è poi confluita
anche nella candidata: mantiene gli attesi e controlla la provenienza runtime.
Prova Presentation nel worktree: 3411 controlli PASS; F3 conserva le sedici
transizioni previste, senza differenze non ammesse. Il seguito geotecnico
descritto sotto resta escluso dal set candidato in staging.

## Passo concluso nelle librerie

Model `8c1e79a9`, versione 5.16.0.0: contratto di input e identità normativa
dell'instabilità non drenata del fondo, distinta da UPL/HYD. Checker
`56f48103`, Geotechnics 0.14.0.0: bilancio di pressioni per blocco omogeneo,
con contributo del piano esterno facoltativo, Nc assegnato e fonti obbligatorie.
Situazioni non implementate e ipotesi non confermate producono `NotEvaluated`.
Non è ancora una funzione utilizzabile dal foglio ANTHEA.

Gli attesi sono stati preparati prima del motore, con Decimal a 50 cifre in
m e kPa, e congelati nel commit Checker `a85600ed`. Impronta del JSON:
`f191a6e438f41770f1cb4cd38bbda381d2600f62c683d236bed432112fc56b9d`.
Quattro casi per confronto caratteristico e due combinazioni NTC, con
conversioni `SoilUnits`. Le tolleranze sono 1e-12 MPa e 1e-11 sull'utilizzo.

Eseguiti: sette nuovi test del kernel e suite completa EmbeddedWalls, 74 PASS;
runner completo Model, 19 Core + 1249 UnitTest PASS, due ignorati preesistenti,
compatibilità API senza rimozioni inattese e audit di uguaglianza concluso.
Runner completo Checker eseguito su `56f48103`: 1912 test PASS e i soli
17 fallimenti Steel storici ammessi; nessuna rimozione API inattesa. Concrete
795, Geotechnics 191, EmbeddedWalls 74 e Design 91 test PASS. Il riepilogo
è in `incremento-14/checker-full-01/summary.json`. Questo è il collaudo dei
sorgenti del nuovo kernel, non un rilascio coerente né un collaudo del foglio.
Il primo tentativo filtrato non includeva il nuovo file nel progetto; il
secondo ha individuato l'esito EC7 mancante, poi corretto. Questi tentativi
non vengono conteggiati come prove superate.

Evidenze: `supporto/artefatti/paratie/incremento-14/`. Le fonti e le equazioni
sono in `Checker/docs/metodi/instabilita-fondo-paratie.md`; non vengono
attribuite al modello capacità tridimensionali o metodi proprietari.

## Passi ancora aperti

- Coordinamento con terreno, geometria e fasi reali, con modello omogeneo
  verificato esplicitamente e provenienza di Nc.
- Documento, migrazione, editor, risultati per caso/fase/combinazione,
  report e CSV, ricerca dell'infissione.
- Guide globali, prove integrate e set coerente del nuovo incremento.
- Estensioni a modelli non omogenei, metodi ulteriori e tampone di fondo.

PAR-16 rimane in corso; nessuna riga della matrice è dichiarata chiusa.
L'approfondimento di pali/micropali resta in coda al modulo paratie.
