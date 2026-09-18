# Workspace calcestruzzo armato

## Organizzazione

1. **Pannello di controllo**: normativa di riferimento, geometria, materiali e
   coefficienti, armature e trefoli a sinistra; viewport geometrico e coordinate
   delle barre al centro; riepilogo delle verifiche a destra.
2. **Dominio 3D**: selezione SLU plastico / SLV elastico, discretizzazione, percorso
   a eccentricità costante oppure N costante, mesh orbitabile e tabella N–Mx–My.
3. **Dominio 2D**: taglio effettivo della stessa superficie. N–M nella direzione θ
   o Mx–My a N fissato. Il pulsante “Sezione sull’azione selezionata” imposta θ o N
   a partire dalla combinazione. Le azioni fuori piano non ricevono un esito 2D;
   sono mostrate solo in proiezione.
4. **Tensioni e fessurazione**: tre sottoschede Rara, Frequente, Quasi permanente,
   con azioni e opzioni indipendenti, mappa tensionale e tensioni nelle barre.

I separatori fra pannelli sono trascinabili. Ogni viewport offre adattamento,
esportazione PNG e apertura ingrandita. In 2D: rotella per zoom, trascinamento per
spostamento. In 3D: trascinamento per orbita, tasto destro o Maiusc per spostamento,
rotella per zoom. I marcatori Ed e Rd restano visibili anche dentro la superficie.
Gli assi N/Mx/My sono scalati separatamente, con valori di riferimento visualizzati.

SLU e SLV condividono le azioni fra 3D e 2D. Il filtro degli esiti e la casella
“Mostra” non modificano le azioni. Le tabelle permettono aggiunta, duplicazione,
rimozione e incolla di 3 colonne N–Mx–My o 4 colonne Nome–N–Mx–My, separate da tab.
Le azioni SLE vanno inserite già combinate: non vengono applicati coefficienti ψ.

## Calcoli disponibili e predisposizioni

Questa fase riguarda l'interfaccia. Non sostituisce ancora il motore con Checker.

- Geometrie attive: circolare, rettangolare e a T; armature generate dai parametri.
- Domini: superficie campionata dai profili dell'attuale `Domini.Profili`.
  I tassi visualizzati sono ricavati da intersezioni con la mesh, coerenti con i
  punti resistenti disegnati. Non sono il risultato di un nuovo solutore Checker.
  Sono sensibili alla discretizzazione e al percorso selezionato; non devono
  essere confusi con i tassi della precedente schermata, che usava un criterio
  diverso. Il dominio elastico mantiene l'ipotesi del motore attuale (limite acciaio).
- SLE lineare: `SezioneElastica.Tensioni`, metodo n con calcestruzzo non resistente
  a trazione; n automatico o manuale. I valori coincidono con il motore esistente.
- Limiti tensionali: confronto solo con limiti **manuali** eventualmente inseriti.
  Senza limiti, si mostrano le tensioni senza dichiarare la verifica soddisfatta.
  Un solo limite produce un esito esplicitamente parziale.
- Normativa NTC / EC2: riferimento salvato; coefficienti materiali espliciti,
  senza modifica silenziosa dei parametri e senza verifica normativa completa.
- Non lineare, fessurazione e precompressione: predisposizioni, non risultati
  simulati. Il modello non lineare non ripiega sul lineare. Inserire trefoli
  permette di vederne le posizioni ma blocca i calcoli, per non ignorarli.
- Taglio, secondo ordine e report Word della sezione restano esclusi.

Il riepilogo non dichiara mai una conformità normativa globale. Gli errori di una
combinazione non diventano tassi nulli o verifiche positive. Le modifiche agli
input invalidano risultati, mesh e mappe; un calcolo annullato conserva soltanto
le analisi già completate con gli stessi dati.

## Dati e separazione del codice

Restano il formato archivio versione 1 e `versione_sezione: 2`. I nuovi dati sono:

- `workspace_ca`: versione 1, normativa, opzioni dei domini, opzioni SLE e trefoli;
- `combinazioni.SLE`: combinazioni pregresse, ora mostrate nella scheda Rara;
- `combinazioni.SLE_FREQ` e `combinazioni.SLE_QP`: nuove liste inizialmente vuote;
- `id` e `visible` nelle combinazioni: identificazione stabile e visibilità grafica.

L'apertura non duplica le vecchie SLE nelle nuove famiglie. I vecchi file privi di
tabelle conservano le azioni presenti nell'input. I risultati sono ricalcolati,
non salvati come esiti validi nei fogli; l'esportazione JSON comprende input,
opzioni, risultati identificati per ID e limitazioni esplicite.

`SectionWorkspace` e `SectionDomainMesh` in X.Core sono indipendenti da WPF.
`ConcreteWorkspace`, `ConcreteDomains`, `ConcreteStress` gestiscono l'interfaccia;
`ConcreteViewports` contiene i viewport riutilizzabili. Questi confini consentono
di collegare successivamente mesh, resistenze e stati tensionali di Checker.

Sono stati consultati i sorgenti del precedente `CheckerUI.7z`, in particolare
`MainViewDomain3DCheck.xaml`, `MainViewDomain2DCheck.xaml`, `MainViewTensionCheck.xaml`
e `MainViewModel.cs`. L'organizzazione opzioni / viewport / azioni, i percorsi di
ricerca e la selezione azione–resistenza ne riprendono l'impostazione. Le vecchie
dipendenze .NET Framework 4.8, Eyeshot e Prism non sono state importate: il rendering
è WPF nativo e non richiede licenze o pacchetti grafici aggiuntivi.
