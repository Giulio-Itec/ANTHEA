# Materiali integrato in ANTHEA

Scheda importata da Desktop/Materiali (sorgenti della versione ATECAP del 23 settembre 2026). Accesso: Moduli singoli → Materiali → Calcestruzzo → Apri. La scheda si apre nella finestra principale di ANTHEA, con Home e Riprendi. È disponibile anche nei Progetti, comprese sottosezioni e trascinamento. Salva e riapri conservano classe, esposizioni, dati di copriferro/aderenza, scelte e designazione del cemento, anche con campi numerici incompleti. Ogni scheda mantiene dati indipendenti. Il report Word non è ancora disponibile.

Il progetto X.Materiali è una libreria WPF inclusa nella compilazione e pubblicazione di X.Desktop. Stili e immagini sono incorporati; non occorre la cartella originale sul Desktop.

Verifica integrata: ANTHEA.exe --smoke-materials <cartella-output>. Include i controlli originali della scheda e catalogo, apertura interna, ripresa, modifica, salvataggio singolo e in progetto, spostamento, riapertura e input incompleti.

## Documentazione della versione importata

# ANTHEA · Materiali
Modulo autonomo Windows WPF / .NET 8 per il calcestruzzo.

Avvio: doppio clic su **Avvia Materiali.cmd**. Richiede .NET Desktop Runtime 8.
La versione aggiornata è in app-classe-minima; una finestra già aperta della vecchia versione va chiusa e riaperta tramite questo collegamento.

## Colonne
- Proprietà meccaniche: classe, resistenze, modulo elastico e deformazioni, con la libreria Checker già usata da ANTHEA.
- Esposizione e copriferro: selezione multipla X0/XC/XD/XS/XF/XA, vita utile 50/100 anni, diametro barre, Dmax, tolleranza, getto, superfici irregolari e abrasione. Classe strutturale e passaggi di calcolo espliciti.
- Composizione: cemento, acqua totale/assorbita/efficace, a/c, aggregati, aggiunte, additivi, aria, consistenza S1-S5 e cloruri. Confronti con i limiti raccomandati di F.1, con avviso fuori dalle ipotesi di riferimento.

Le modifiche valgono nella sessione aperta; salvataggio e riapertura di schede materiali non ancora implementati.

## Riferimenti e applicabilità
Vedere RIFERIMENTI.md. Le edizioni usate sono quelle dei PDF forniti: EN 1992-1-1:2004 e UNI EN 206-1:2006.
I risultati non attestano la conformità complessiva alle norme nazionali o alle edizioni successive.

## Sorgenti e verifiche
Sorgenti in src. Compila.cmd pubblica in app-classe-minima. Verifica.cmd esegue controlli numerici, aggiornamenti UI e acquisizioni a 1500/1200 px.
Esito: app-classe-minima/verifica.txt. Anteprime: app-classe-minima/anteprima-*.png.
La precedente versione resta in app; il precedente sorgente principale è conservato in src/App.xaml.cs.pre-esposizione.bak.


Interfaccia compatta in tre colonne affiancate con scorrimento verticale indipendente. Classe del calcestruzzo nella prima colonna. Nelle finestre strette lo scorrimento orizzontale mantiene accessibili tutte le colonne.



Il criterio predefinito per il copriferro è ora NTC 2018 + Circolare 7/2019; EC2 2004 rimane selezionabile.
XF e XA possono determinare da sole il copriferro nel criterio NTC. Sono disponibili tipo di elemento, Cmin pertinente facoltativo e controllo qualità che include i copriferri.



## Composizione automatica

Cemento, acqua totale, aria e abbassamento di riferimento dispongono dell'opzione **Auto**, attiva inizialmente. La modifica del testo passa il singolo campo in modalità manuale: il valore non viene sovrascritto quando cambiano esposizione o altre scelte. Riattivare Auto per ricalcolarlo.

- Cemento: massimo dei minimi F.1 delle esposizioni selezionate. Se l'acqua è manuale, viene proposto anche il cemento necessario a rispettare a/c massimo, arrotondato in eccesso al kg/m³.
- Acqua totale: cemento × a/c massimo + acqua assorbita. Il valore è un limite proposto, non il fabbisogno d'acqua della miscela; è arrotondato per difetto a 0,001 kg/m³.
- Aria: minimo tabellare dove presente; altrimenti il campo resta vuoto.
- Abbassamento di riferimento: punto medio della classe S1–S4. Per S5 viene proposta la soglia inferiore di 220 mm. Il valore non è una misura di prova.

Le proposte cemento/acqua/aria vengono sospese se le esposizioni non sono valide o se si esce dalle ipotesi del riferimento F.1: CEM I, Dmax 20–32 mm, vita utile 50 anni. Con X0 non vengono inventati minimi assenti. I dati mancanti o non validi cancellano le proposte dipendenti, mantenendo le modifiche manuali.

Questi valori non costituiscono una ricetta qualificata e non garantiscono classe di resistenza e lavorabilità. Aggregati, aggiunte/additivi, prodotti commerciali e classe di cloruri richiedono informazioni specifiche e restano manuali. Non si deduce il fabbisogno d'acqua dalla sola classe di consistenza o resistenza.


Cmin pertinente: ora visibile e compilato automaticamente dalla soglia della tabella C4.1.IV già usata dal motore (25 / 30 / 35 MPa, ambiente più gravoso selezionato). La modifica manuale viene conservata; Auto ripristina il valore tabellare. Questo campo non coincide necessariamente con la classe minima della miscela riportata in F.1.



## Finestra e ingrandimento Windows
Il modulo si apre massimizzato sull'area utile del monitor, lasciando visibile la barra delle applicazioni. Non impone limiti massimi fissi ricavati dallo schermo principale. Il manifest PerMonitorV2 e l'adattamento al monitor corrente gestiscono le variazioni DPI; in modalità finestra le dimensioni minime e la posizione vengono contenute nell'area utile disponibile. I tre pannelli mantengono le proprie barre di scorrimento.
Per la verifica sul monitor e sulla scala correnti: eseguire app-classe-minima\Materiali.exe --check-display. Il risultato viene scritto in app-classe-minima\verifica-schermo.txt. La prova non modifica l'ingrandimento di Windows.


## Selezione dell'esposizione
Menu a tendina con tutte le 18 classi, descrizione a destra trascritta dalla colonna Descrizione dell'ambiente del prospetto 1, pagina 7, del documento ATECAP 2020 fornito (2020_atecap_corretta_prescrizione_cls.pdf). Sotto compare l'aggressività della classe scelta secondo NTC 2018, tabella 4.1.III. Le esposizioni concomitanti restano disponibili nella sezione richiudibile; per combinazioni compare anche l'aggressività complessiva più gravosa. Cambiando classe dal menu si riparte da una sola esposizione. Copriferro, Cmin e composizione continuano ad aggiornarsi automaticamente. I limiti della miscela F.1 esistenti non sono stati sostituiti dai valori UNI 11104 del nuovo documento.

La descrizione a destra del menu include ora anche tutti gli esempi informativi del prospetto 1 ATECAP (pagina 7), aggiornati per ciascuna delle 18 classi. Descrizione ed esempi sono separati da etichette e visibili senza aprire sezioni aggiuntive.


## Classe minima del calcestruzzo (aggiornamento)
La precedente riga Cmin pertinente è sostituita da una classe Cxx/XX non modificabile, senza Auto e senza unità MPa. La classe proviene dal prospetto 5 UNI 11104 riprodotto a pagina 19 del PDF ATECAP 2020 fornito. Questa indicazione sostituisce le precedenti istruzioni del README sul Cmin manuale/tabellare 25/30/35.
X0 C12/15; XC1 e XC2 C25/30; XC3 C30/37; XC4 C32/40; XS1 C32/40; XS2 e XS3 C35/45; XD1 C30/37; XD2 C32/40; XD3 C35/45; XF1 C32/40; XF2 e XF3 C25/30; XF4 C30/37; XA1 C30/37; XA2 C32/40; XA3 C35/45.
Per esposizioni concomitanti viene adottato il massimo fck. Il corrispondente fck è passato al calcolo del copriferro come Cmin pertinente; il controllo della classe del materiale e gli altri requisiti restano necessari. Per dati incompatibili il valore non viene mostrato.
Le classi XF2/XF3/XF4 sono quelle tabellari con aria inglobata; l'alternativa senza aria richiede le prove della nota a. XF1 riporta il valore ordinario senza aria aggiunta: l'alternativa aerata della nota b necessita di una specifica dedicata. Le note sono mostrate accanto al risultato. I confronti della composizione con l'appendice F.1 preesistente restano distinti e identificati come tali.

## Composizione ATECAP (23 settembre 2026)
Scheda semplificata: rapporto A/C massimo e cemento minimo dal prospetto 5, p. 19 del vademecum ATECAP 2020. Per esposizioni concomitanti si applicano i limiti più restrittivi. Campi automatici in sola lettura; Dmax ripreso dal copriferro. Mantenuti consistenza al getto e definizione del cemento. Rimossi dalla scheda dosaggi di produzione, acqua, aggiunte, additivi e confronti F.1.
Cl 0,40 è una proposta per armatura ordinaria dagli esempi pp. 43–50, da confermare nella prescrizione, non dedotta dall’esposizione.
Aria XF2/XF3/XF4: 4% per Dupper >20 mm; 5% per 12–16 mm come esempio della nota a; negli altri intervalli fino a 20 mm il minimo resta da definire. Per le altre classi il prospetto non prescrive un minimo. Evidenziati i requisiti aggiuntivi per XF, XS, XA e la validità dei riferimenti per 50 anni.
Verificate tutte le 18 esposizioni, combinazioni, Dmax e input invalidi; controllati i layout a 1200 e 1500 px. Avvia Materiali.cmd usa app-atecap.
