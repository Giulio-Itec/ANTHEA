# Paratie: albero di lavoro richiudibile

L'albero laterale rende navigabili i dati effettivamente presenti nel
foglio: impostazioni, parete e sezioni, terreni, sostegni e verifiche,
casi e fasi, falda e filtrazione, ricerca dell'infissione.
Selezionare una riga apre il suo editor; i dati di fase selezionano
anche caso e fase del disegno. Un nuovo clic sulla voce corrente o
Invio consente di tornare dall'area risultati all'editor.

Il pannello iniziale è largo 270 DIP, ridimensionabile fra 230 e 420;
chiuso occupa 38 DIP e riaperto conserva la larghezza scelta.
Ogni ramo conserva l'espansione quando una riga cambia o viene
riordinata. Le righe eliminate e i documenti ricaricati non conservano
riferimenti utilizzabili a dati precedenti.

Il ViewModel possiede nodi e comandi; WPF inoltra soltanto gli eventi.
Nessun dato ingegneristico è duplicato nei nodi. L'albero non avvia
calcoli, non modifica input o revisioni e rispetta gli editor in sola
lettura. Le 23 tabelle sono rappresentate senza perdita o duplicazione
di righe; una tabella futura non classificata rimane visibile.

Le fasi sono raggruppate per caso. Riferimenti a casi sconosciuti
rimangono evidenti; carichi e verifiche con fase assente, inesistente
o ambigua compaiono in «Riferimenti di fase da completare». Nessuna
attribuzione arbitraria a fasi con lo stesso nome.

La cattura WPF antecedente conserva i 69 controlli del modulo;
la cattura numerica antecedente conserva 37 calcoli di esempio,
incluse nove paratie. Cartella:
`supporto/artefatti/paratie-albero/prima/`.
Il primo collaudo integrato supera Presentation (3.523 controlli),
comprendendo 82 controlli aggiunti per l'albero.
WPF integrato: 79 PASS, inclusi dieci controlli nuovi. Schermate di
capsula e albero nei tre temi controllate, incluso il pannello chiuso.
I 37 calcoli di esempio prima/dopo, comprendenti nove esempi Paratie,
conservano esattamente 301.420 valori numerici e 97.689 testi.
Cambiano soltanto tre stringhe di versione e 18 campi impronta:
le 20 impronte nei risultati prima/dopo sono riprodotte separatamente
usando gli input archiviati e le versioni AB/AC del motore. Evidenze:
`supporto/artefatti/paratie-albero/confronto-calcoli.json` e `audit-impronte/`.

Guide globali Rev56 finalizzate, renderizzate e controllate: 264 pagine,
387 formule conservate e 18 originali archiviati con SHA-256.
Le 30 ricerche della Wiki restano identiche alla 1.12.0; tre articoli sono
aggiornati (uso Paratie, uso e teoria delle sezioni c.a.).
Il setup 1.12.0 pubblicato precede questa funzionalità; l'utente ha richiesto
di includerla nel nuovo installer 1.12.1.
