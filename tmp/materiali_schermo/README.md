# ANTHEA · Materiali
Modulo autonomo Windows WPF / .NET 8 per il calcestruzzo.

Avvio: doppio clic su **Avvia Materiali.cmd**. Richiede .NET Desktop Runtime 8.
La versione aggiornata è in app-schermo; una finestra già aperta della vecchia versione va chiusa e riaperta tramite questo collegamento.

## Colonne
- Proprietà meccaniche: classe, resistenze, modulo elastico e deformazioni, con la libreria Checker già usata da ANTHEA.
- Esposizione e copriferro: selezione multipla X0/XC/XD/XS/XF/XA, vita utile 50/100 anni, diametro barre, Dmax, tolleranza, getto, superfici irregolari e abrasione. Classe strutturale e passaggi di calcolo espliciti.
- Composizione: cemento, acqua totale/assorbita/efficace, a/c, aggregati, aggiunte, additivi, aria, consistenza S1-S5 e cloruri. Confronti con i limiti raccomandati di F.1, con avviso fuori dalle ipotesi di riferimento.

Le modifiche valgono nella sessione aperta; salvataggio e riapertura di schede materiali non ancora implementati.

## Riferimenti e applicabilità
Vedere RIFERIMENTI.md. Le edizioni usate sono quelle dei PDF forniti: EN 1992-1-1:2004 e UNI EN 206-1:2006.
I risultati non attestano la conformità complessiva alle norme nazionali o alle edizioni successive.

## Sorgenti e verifiche
Sorgenti in src. Compila.cmd pubblica in app-schermo. Verifica.cmd esegue controlli numerici, aggiornamenti UI e acquisizioni a 1500/1200 px.
Esito: app-schermo/verifica.txt. Anteprime: app-schermo/anteprima-*.png.
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
Per la verifica sul monitor e sulla scala correnti: eseguire app-schermo\Materiali.exe --check-display. Il risultato viene scritto in app-schermo\verifica-schermo.txt. La prova non modifica l'ingrandimento di Windows.
