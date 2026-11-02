# ANTHEA 1.17.1 — note di rilascio

Note del 2 novembre 2026. La 1.17.1 porta:
- nella Sezione in c.a., un criterio resistente per ciascuno stato limite, comune ai domini 3D e 2D;
- sei fogli Azioni aggiuntivi e un'assistenza grafica uniforme in tutti i fogli Azioni;
- il foglio Viscosità e maturazione fra i materiali;
- l'archivio dei risultati dei modelli accanto al progetto.

Contiene le librerie GPC della release AT e le guide Rev68. Il numero di versione è quello scelto dall'utente.

Il testo è scritto per chi usa ANTHEA ed è anche l'annotazione del tag `v1.17.1`, sul commit del setup. Il verbale tecnico del rilascio è
in [installer-1.17.1.md](installer-1.17.1.md).

## Novità nell'uso

- **Sezione in c.a., criterio resistente per stato limite.** Nelle schede Dominio 3D e Dominio 2D:
  - **Stato limite mostrato** (SLU o SLV) sceglie solo il dominio e le combinazioni visualizzati;
  - sotto, **Criterio resistente SLU** e **Criterio resistente SLV** (Plastico o Elastico) fissano il dominio di ciascuno stato. Il
    predefinito è SLU plastico e SLV elastico.

  I due criteri sono un solo dato, comune alle schede 3D e 2D, ai report e a Calcola armature. Cambiarli in una scheda li cambia anche
  nell'altra e ricalcola solo i domini di quello stato. Il dettaglio della combinazione e il titolo del dominio 2D riportano il criterio
  («SLU · Plastico»).
  - **Prima.** Ogni scheda aveva un solo criterio, usato per entrambi gli stati: non si poteva avere SLU plastico con SLV elastico. Nei
    fogli nuovi, e nelle conversioni delle sollecitazioni storiche, anche SLV veniva calcolato plastico.
  - **Fogli salvati con la 1.17.0.** SLU e SLV prendono il criterio della scheda Dominio 3D, così i risultati del dominio 3D, dei report e
    di Calcola armature non cambiano. Se la scheda Dominio 2D usava un criterio diverso, il suo dominio cambia e una nota sotto il
    criterio lo segnala finché non lo si modifica.
  - **Report breve.** Riporta i criteri effettivi; prima scriveva sempre «SLU plastico; SLV elastico».
- **Azioni aggiuntive.** Sei fogli nuovi: azioni idrauliche, fasi costruttive, ritiro e viscosità, precompressione e perdite, gru e
  carriponte, incendio. Ognuno conserva i casi alternativi e la provenienza dei parametri, con schemi quotati, grafici, CSV, relazione ed
  esempi.
- **Azioni, assistenza grafica.** I sedici fogli Azioni hanno un pannello comune richiudibile: il campo attivo mostra etichetta, valore e
  unità, e lo schema marca le quote e i componenti associati. Scenari, fasi ed età si selezionano anche dai risultati. Gli schemi
  originali sono resi con i colori del tema.
- **Materiali, Viscosità e maturazione.** Il calcolo del coefficiente di viscosità nel tempo e della maturazione del modulo è un foglio
  salvabile (Moduli singoli → Materiali), da tenere da solo o in un progetto.
- **Modelli, archivio dei risultati.** I risultati dei modelli grandi stanno in un archivio a blocchi accanto al progetto; il progetto si
  salva e si riapre molto prima. La scheda del modello elenca le **Copie precedenti** dei risultati.

## Librerie

Release AT, da Model `5449e4d1` e Checker `a2acd3d2` su origin/master. Rispetto alla 1.17.0 comprende le release AR (contratti e calcoli
delle azioni aggiuntive), AS (dati delle sovrapposizioni delle armature nel verificatore del modello) e AT (prestazioni di GPCModel
5.37: la copia di un modello MIDAS di circa 20 000 nodi passa da 27–41 s a meno di un secondo, con gli stessi risultati). Le versioni
delle DLL sono in `lib/Checker/README.md`.

## Guide

Guide pratica e teorica Rev68, in Word e PDF e nella Wiki. Rispetto alla Rev65 della 1.17.0 descrivono i fogli Azioni aggiuntivi,
l'assistenza grafica, il foglio Viscosità e maturazione e il criterio resistente per stato limite.

## Limiti noti

- La full di rilascio non è stata eseguita su questo commit, per scelta dell'utente: è prevista nella notte, con la correzione
  successiva. Sono passate le suite della modifica del c.a. e le altre elencate nel verbale; il set AT ha la sua full (PASS 161).
- Restano quelli della 1.17.0.
