# Aggiornamento SLE, geometria e trefoli

## Calcolo e cache

Le famiglie Rara, Frequente e Quasi permanente restano parallele. All'interno
di ciascuna famiglia le combinazioni sono distribuite su un massimo di due
worker, limitati in funzione dei processori disponibili. Ogni worker utilizza
il proprio solver; geometria e materiali condividono la sezione preparata.
L'inizializzazione dei solver resta serializzata perché la triangolazione
interna della DLL non è thread-safe.

Esposizione, sensibilità dell'armatura, durata, aderenza, copriferro per le
fessure e interasse non appartengono alla chiave della cache tensionale.
Cambiare questi dati aggiorna la fessurazione senza ripetere l'equilibrio SLE.
Per decompressione/formazione delle fessure rimane necessaria l'analisi
ausiliaria della sezione omogeneizzata interamente reagente, distinta
dall'analisi SLE principale.

## Interasse delle barre tese

Campo vuoto: automatico. Un valore positivo inserito dall'utente prevale.
La selezione delle barre è fatta per combinazione, usando il piano di
deformazione della DLL e l'area efficace già utilizzata dalla fessurazione.
Per le sezioni circolari si misura l'arco fra barre consecutive sulla corona.
Per rettangolari e T si considerano le coppie consecutive delle file
orizzontali e verticali, evitando collegamenti che attraversano i vuoti della
T. Si prende il massimo delle distanze valide, anche fra file allineate:
quest'ultima scelta può essere conservativa nelle disposizioni multistrato.
Non si sostituisce la spaziatura massima con il minimo del vicino più prossimo.
Se non esistono coppie idonee si richiede il dato manuale.

Il risultato riporta interasse e provenienza. La formula di apertura delle
fessure preesistente non è stata modificata. Riferimento: Circolare 21 gennaio
2019 n. 7, § C4.1.2.2.4.5 e figura C4.1.11.

Il contratto `ITensionBarSpacing`, la classe `TensionBarSpacing` e il punto di
iniezione `Ntc2018Checks.SpacingCalculator` separano la geometria dalla UI.
Un futuro adattatore Checker potrà sostituire questo servizio, convertendo
la geometria in ingresso nei tipi Geometry della libreria.

## Geometria e grafica

- Gli identificativi longitudinali sono B01, B02, … (senza troncamento oltre 99).
- La fila all'intradosso dell'ala della T ha numero, diametro e offset
  intradosso–asse barra; numero zero mantiene i vecchi fogli senza aggiunte.
  È distribuita sull'intera larghezza dell'ala. Copriferro, spessore e
  sovrapposizioni vengono controllati.
- Per le staffe circolari sono disponibili bracci interni paralleli e staffe
  chiuse interne sovrapposte, ruotabili. Nel primo schema uno/due bracci
  aggiuntivi corrispondono a tre/quattro braccia nella direzione scelta.
  Nel secondo schema il numero indica le staffe chiuse aggiuntive.
  Il disegno è indicativo: ancoraggi, piegature e il modello resistente a
  taglio circolare restano da validare.
- Rosso indica compressione negativa, blu trazione positiva.
- Le scale di utilizzo distinguono cinque fasce: fino a 0,50; 0,50–0,70;
  0,70–0,90; 0,90–1,00; oltre 1,00. Legende verticali.
- “Tutti i punti resistenti” è indipendente dalla selezione delle azioni:
  rispetta i filtri della tabella e le righe visibili. “Resistenze” nasconde
  tutti i punti resistenti.

## Trefoli

Riferimento implementativo: CheckerUI, ModuleConcreteSection,
`MainViewModel.AddTendon`, tabella Tendons e pannello TendonMaterialView.
Un cavo con n trefoli viene rappresentato con Ø equivalente = Ø singolo × √n.
Il diametro singolo è quello equivalente all'area metallica, non il diametro
esterno nominale. La tabella conserva diametro equivalente del cavo, area
totale, coordinate, σp0 e proprietà del materiale; area e diametro si
aggiornano reciprocamente.

Ogni cavo può avere un materiale distinto e una legge elastoplastica o
incrudente. Il catalogo materiali è salvato nel foglio. Applicare un materiale
agisce sul cavo selezionato e sul predefinito dei nuovi inserimenti.
I vecchi record con area e proprietà esplicite restano leggibili.
L'interfaccia non aggiunge nuove verifiche CAP: apertura delle fessure con
trefoli e identificazione SLE dei trefoli con σp0 nullo conservano i limiti
espliciti già presenti.

## Verifica

Suite numerica: `dotnet run --project X.Verifiche -c Release -- --checker`.
Test mirato WPF: `dotnet run --project X.Desktop -c Release -- --smoke-ca-features verifiche_ca_features`.
Quest'ultimo confronta seriale/parallelo lineare e non lineare, controlla
l'identità dei risultati al cambio ambiente, le nuove geometrie, le staffe,
i materiali distinti dei trefoli e il round trip JSON.
