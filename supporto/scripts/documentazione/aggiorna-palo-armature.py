"""Advance the two global guides to Rev17, preserving the preceding documents."""
from pathlib import Path
import hashlib,json,shutil,subprocess,sys
ROOT=Path(__file__).resolve().parents[3];SUP=ROOT/'supporto';ARCH=SUP/'SUPERATI/palo-armature-rev17-20261005'
paths=[SUP/f'docs/guida-{k}-anthea.{e}' for k in ('pratica','teorica') for e in ('md','pdf')]
paths+=list((SUP/'documentazione/Guide_ANTHEA').glob('*Rev16.*'))
paths += [SUP/'README.md',SUP/'README.pdf',SUP/'installer/Indice-guide.md',SUP/'installer/Indice-guide.pdf']
registry=[]
for p in paths:
    if not p.exists():continue
    dest=ARCH/p.relative_to(SUP);dest.parent.mkdir(parents=True,exist_ok=True)
    if not dest.exists():shutil.copy2(p,dest)
    registry.append(dict(origine=str(p.relative_to(ROOT)),archivio=str(dest.relative_to(ROOT)),motivo='Rev17: parametri per riga, N e verifiche lungo il palo, armature e distinta preliminare',sostituzione=str(p.relative_to(ROOT)).replace('Rev16','Rev17'),sha256=hashlib.sha256(dest.read_bytes()).hexdigest()))
(ARCH/'registro.json').write_text(json.dumps(registry,ensure_ascii=False,indent=2),encoding='utf-8')
practical='''### Parametri nella riga dello strato

La tabella iniziale contiene anche i parametri della risposta orizzontale. Strato e terreno rimangono riconoscibili durante lo scorrimento orizzontale interno. Nella stessa riga si scelgono addensamento o categoria, determinazione e autore; minimo e massimo della fonte sono in sola lettura, il valore adottato è modificabile. Info della riga riporta origine, condizioni, bibliografia e motivazione dell'eventuale override. Non esiste più il pannello di modifica sotto la stratigrafia.

Per un nuovo terreno granulare la modalità iniziale è A γ/1,35. Sciolto usa A 100–300, media 200; Medio 300–1000, media 650; Denso 1000–3000, media 2000. A è adimensionale. I valori consigliati nel libro restano separati: 200, 600 e 1500. La modalità nh direttamente da tabella 14.5 mostra nella riga i valori non immerso / immerso e usa la falda condivisa; nh* abilita un override esplicito in kN/m³ con motivazione. Il valore adottato da una tabella in N/cm³ viene convertito in Checker.

Per i coesivi scegliere categoria e singolo autore della tabella 14.6. Le categorie filtrano le righe bibliografiche; una categoria cambiata richiede una teoria pertinente prima del calcolo. Per l'argilla n.c. Reese–Matlock il valore iniziale è 1,85 N/cm³, media di 0,2 e 3,5. Inserire 1,0 registra una scelta manuale. Argilla sovraconsolidata consente kh costante o k distribuito assegnato. Le modalità manuali restano disponibili. I valori manuali non sono sovrascritti dal ricalcolo; se cambia la fonte occorre scegliere se mantenerli o adottare la nuova media. Una media ancora intatta si aggiorna alla nuova riga.

### Sforzo normale e lettura dei diagrammi

Il peso unitario adottato è nei dati iniziali della sezione. Il valore iniziale del c.a. è 25 kN/m³ e include già l'acciaio. Per CHS si usa il peso dell'anello di acciaio; l'iniezione è esclusa inizialmente (peso unitario zero) e può essere inclusa esplicitamente sull'area restante. Il peso unitario e N in testa devono essere coerenti con il livello delle azioni usate per la verifica. Non vengono applicati automaticamente coefficienti alle azioni.

I pannelli sono ordinati: profilo, k, y, θ, q, N, V, M, tratti. La scala verticale e la selezione della quota sono comuni. N è positivo a compressione e cresce del peso tra testa e quota considerata. Non sono modellati trasferimento assiale al terreno, galleggiamento o secondo ordine. I passi degli assi seguono valori 1, 2, 5 per potenze di dieci; i valori selezionati conservano la precisione numerica. Alle interfacce il clic ripetuto legge i due lati.

### Tratti e verifica delle sezioni

Il primo utilizzo e gli archivi senza tratti hanno un solo tratto collegato alla sezione principale di Broms. Le quote sono misurate dalla testa; una fine vuota indica la punta. Dividi aggiunge un confine interno, Unisci conserva l'armatura del tratto precedente. La partizione deve coprire il palo senza vuoti. I tratti successivi possono restare collegati oppure avere quantità e diametri delle barre, diametro e passo delle staffe personalizzati. Geometria e materiali restano comuni. La modifica della sezione principale aggiorna soltanto i tratti collegati.

Confermare nelle ipotesi la natura delle azioni di progetto e il modello di taglio circolare equivalente, con z/d assegnato. Le verifiche impiegano N, V e M alla stessa quota, includendo interfacce, confini dei tratti, campioni interni ed estremi dei polinomi FEM. Il riepilogo mostra la sezione critica e la sezione trasversale segue la quota selezionata. Gli esiti distinguono non verificabile, non soddisfatto, parziale e soddisfatto N–M–V con dettagli ancora da completare. Il verificatore c.a. non si applica ai CHS.

MRd positivo e negativo sono calcolati al valore locale di N e sovrapposti a MEd con linea tratteggiata. Sono capacità nominali della sezione; non certificano l'ancoraggio o il confinamento dei giunti. Fuori dal dominio resistente non viene disegnata una resistenza valida. La capacità ultima di Broms non è utilizzata come azione del FEM o delle sezioni.

### Proposte e vista delle armature

Proponi suddivisione usa classi di utilizzo N–M–V di ampiezza 0,25 e lunghezze esecutive assegnate, riservando max(lbd,l0)+a_l allo sviluppo. La lunghezza commerciale delle barre è un input distinto dalla lunghezza massima della gabbia. È un criterio numerico preliminare, non una prescrizione normativa o un'ottimizzazione globale. La proposta si applica con un comando esplicito che archivia i tratti precedenti. Dimensiona esamina il catalogo di quantità di barre e passi staffe inserito nel dialogo, mantenendo i diametri assegnati: Checker seleziona la prima disposizione che soddisfa tutte le sezioni del tratto. Per il primo tratto l'applicazione aggiorna la sezione principale e i tratti collegati; per gli altri crea una personalizzazione.

L'opzione minimi pali controlla il sottoinsieme NTC 2018 §7.2.5: As almeno 0,3% Ac, staffe di almeno 8 mm e passo non superiore a 8 diametri longitudinali. Non attiva una verifica sismica completa né definisce le zone dissipative. Il dimensionamento può non trovare una soluzione nel catalogo assegnato: ampliare consapevolmente il catalogo o modificare la sezione.

La vista finale deriva dai risultati: quantità, diametri, estremi delle barre, tratti teorici, sviluppo, sovrapposizioni, staffe e copriferro. Barre uguali attraversano più tratti senza giunti artificiali. La distinta delle barre longitudinali usa tagli entro la lunghezza massima assegnata e include le sovrapposizioni. I giunti sono raggruppati; percentuali di sovrapposizione inferiori al 100% richiedono ancora la distribuzione effettiva dei giunti. Le forme di chiusura delle staffe, lo sfalsamento, il confinamento e l'esecutivo non sono certificati: la vista è etichettata preliminare. Gli sviluppi disponibili oltre testa e punta sono input espliciti, inizialmente nulli.

Salvataggio, JSON, CSV e report conservano parametri e origine, modello di N, azioni concomitanti, collegamenti, verifiche, resistenze, sviluppi e distinta preliminare. Un cambiamento invalida subito risultati e armature, bloccando l'esportazione fino al ricalcolo. Le prove dirette e di interfaccia sono in supporto/test/ElasticPile.Checks e ElasticPile.UiChecks; le evidenze della revisione sono in supporto/artefatti/palo-armature.

'''
p=SUP/'docs/guida-pratica-anthea.md';s=p.read_text(encoding='utf-8-sig')
start=s.index('### Parametri degli strati',s.index('## Risposta elastica del palo orizzontale'));end=s.index('### Esempio riproducibile e limiti',start)
s=s[:start]+practical+s[end:]
s=s.replace('curve p-y, carico assiale, secondo ordine','curve p-y, trasferimento assiale al terreno, secondo ordine');p.write_text(s,encoding='utf-8')
theory='''### Sforzo normale e verifica per ascissa

Checker aggiunge N ai risultati FEM e alle azioni strutturate per sezione. Con x dalla testa verso la punta e compressione positiva:

$$ N(x)=N_0+\\int_0^x w(s)\\,ds=N_0+w x

Il modello corrente ha geometria e materiali comuni lungo il palo e peso w costante. Per c.a. w=γca πD²/4; γca include l'armatura, che non viene sommata nuovamente. Per il CHS, Ast=πt(De−t) e w=γs Ast+γiniezione(πDgeo²/4−Ast), dopo conversione in metri. Il valore zero del peso dell'iniezione la esclude esplicitamente. I componenti del peso non vengono dedotti da EJ. Non sono introdotti resistenza assiale del terreno, spinta idrostatica o rigidezza geometrica.

PileSegments valida la copertura completa, gli identificativi univoci e le quote crescenti. I confini entrano nella mesh senza spostare interfacce o azzerare la profondità geotecnica. La sezione iniziale è un riferimento ai dati comuni; un tratto personalizzato contiene soltanto le differenze di armatura.

PileReinforcement, in GPCChecker.Concrete, orchestra SectionSolver a N costante, SectionShearCalculator e AnchorageCalculator. N positivo del palo viene convertito nel segno negativo e nei newton del solutore di sezione; M da kNm a Nmm. MRd è cercato nei due versi a ciascun N effettivo. Una soluzione con residuo assiale eccessivo, verso errato o momento trasversale non trascurabile non produce una resistenza valida. I rapporti usano azioni concomitanti e tutti i campioni disponibili, inclusi gli estremi FEM; non si combinano massimi indipendenti.

Per il taglio si conserva il verificatore NTC esistente. L'adattamento circolare esplicito usa bw=D e d dal baricentro delle barre del semicerchio teso; si adotta il minore d dei due versi. Staffe chiuse a 90° hanno due bracci; z/d è assegnato e deve essere confermato dall'utente. Non si trasferiscono automaticamente ai pali le riduzioni specifiche delle pile da ponte. Il risultato soddisfatto N–M–V attesta soltanto il perimetro dichiarato; SLE, instabilità, sisma, duttilità e dettagli completi sono separati.

### Continuità delle barre e proposta costruttiva

Le lunghezze di ancoraggio e sovrapposizione richiamano il motore Checker già validato, con barre ad aderenza migliorata, σsd=fyd e nessuna riduzione favorevole di forma o confinamento (α1…α5=1). La resistenza a trazione usata per l'aderenza è limitata a C60/75. Buona aderenza è una scelta esplicita. I risultati conservano lbd, l0, percentuale assegnata e controllo della distanza libera.

Per la proposta si aggiunge lo spostamento del diagramma delle forze di trazione a_l=z cotθ/2 per staffe ortogonali (EN1992-1-1 §9.2.1.3, presentazione JRC Arrieta 2011, diapositiva 33). Se il taglio non è confermato si usa il limite superiore cotθ=2,5 a fini preliminari. L'estensione teorica delle barre oltre il confine del tratto è max(lbd,l0)+a_l, limitata agli sviluppi fisicamente disponibili. Questa convenzione conservativa non sostituisce l'analisi locale del nodo testa o della punta e non certifica l'armatura di confinamento.

Tratti contigui con identica disposizione delle barre formano un unico gruppo continuo. I tagli sono ripartiti rispettando la lunghezza commerciale assegnata e sovrapposizioni esplicite; la distinta conteggia la lunghezza fisica, inclusi i tratti sovrapposti. La resistenza nominale non somma la doppia armatura del giunto. La disposizione raggruppata dei giunti non soddisfa automaticamente una percentuale di sovrapposizione inferiore al 100%: il risultato resta preliminare e segnala la necessità di sfalsamento. Le quote teoriche, effettive, lbd, l0 e a_l sono grandezze distinte.

Il controllo opzionale dei minimi pali è limitato a NTC 2018 §7.2.5, testo della Gazzetta Ufficiale del 20 febbraio 2018, p.213 (pagina 7 del PDF del capitolo 7): As≥0,003Ac, φst≥8 mm e s≤8φL. Le ulteriori prescrizioni per zone dissipative e duttilità non sono attivate da questo controllo. La proposta automatica dei tratti usa variazioni delle classi di utilizzo di ampiezza 0,25 e vincoli di lunghezza, riservando max(lbd,l0)+a_l allo sviluppo: è una scelta software dichiarata. La lunghezza commerciale dei tagli è assegnata separatamente. La ricerca discreta delle armature richiama gli stessi verificatori per ogni candidato e per tutte le ascisse, con quantità e passi forniti dall'utente.

### Fonti e validazione dell'estensione strutturale

Fonti consultate il 5 ottobre 2026: DM 17 gennaio 2018, NTC, Gazzetta Ufficiale, capitolo 4 (§4.1.2.3.5, §4.1.6.1.4) e capitolo 7 (§7.2.5); José M. Arrieta, Eurocode 2 Background and Applications, workshop JRC Bruxelles 20–21 ottobre 2011, diapositive 7–8, 16 e 33. Il materiale JRC è un supporto formativo, non una nuova edizione della norma. Le fonti Viggiani e le distinzioni PDF/pagina stampata riportate sopra restano invariate.

[NTC nella Gazzetta Ufficiale](https://www.gazzettaufficiale.it/eli/id/2018/02/20/18A00716/sg) · [Materiale formativo JRC](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/05_EC2WS_Arrieta_Detailing.pdf)

I test diretti dell'estensione controllano pesi su aree disgiunte, carico assiale nullo e invertito, azioni concomitanti, confini esatti della mesh, continuità dei tratti, MRd nei due versi e dipendenza da N, rifiuto dei casi fuori dominio, minimi selezionati, ancoraggi, sovrapposizioni e distinta. Riferimento indipendente per l'aderenza: φ16, σsd=400 MPa, fctk05=2 MPa, γc=1,5 e buona aderenza danno fbd=3 MPa, lbd=533,333 mm e l0=800 mm con tutte le barre sovrapposte. Restano attivi i confronti analitici FEM e Viggiani già documentati. Evidenze in supporto/artefatti/palo-armature.

'''
p=SUP/'docs/guida-teorica-anthea.md';s=p.read_text(encoding='utf-8-sig')
s=s.replace('Checker restituisce SectionDemands per ascissa e lato, con V e M con segno, riferimento a geometria e materiale e riferimenti agli estremi. Questi dati alimenteranno le verifiche di resistenza lungo il palo. In questa revisione non sono calcolati coefficienti di utilizzo né esiti normativi di sezione.','Checker restituisce SectionDemands per ascissa e lato, con N, V e M concomitanti, riferimento a geometria e materiale e riferimenti agli estremi. Questi dati alimentano i verificatori di sezione descritti di seguito.')
pos=s.index('### Discretizzazione e recupero delle sollecitazioni',s.index('## Risposta elastica del palo orizzontale'));s=s[:pos]+theory+s[pos:];p.write_text(s,encoding='utf-8')
for kind in ('pratica','teorica'):
    p=SUP/f'docs/guida-{kind}-anthea.md';s=p.read_text(encoding='utf-8').replace('revisione documentale 16','revisione documentale 17');p.write_text(s,encoding='utf-8')
for p in (SUP/'README.md',SUP/'installer/Indice-guide.md'):
    s=p.read_text(encoding='utf-8-sig').replace('Rev16','Rev17').replace('Revisione 16','Revisione 17');p.write_text(s,encoding='utf-8')
p=SUP/'scripts/Build-AntheaGuides-Itec.py';s=p.read_text(encoding='utf-8').replace("REVISION = '16'","REVISION = '17'").replace("REVISION_NOTE = 'PALO ELASTICO DATI CONDIVISI E RIGIDEZZE'","REVISION_NOTE = 'PALO ELASTICO E VERIFICHE PER TRATTO'");p.write_text(s,encoding='utf-8')
p=SUP/'scripts/Render-AntheaGuides-Itec.ps1';s=p.read_text(encoding='utf-8-sig').replace("$Revision = '16'","$Revision = '17'");p.write_text(s,encoding='utf-8-sig')
art=SUP/'artefatti/guide_anthea_itec_rev17';art.mkdir(parents=True,exist_ok=True)
(art/'artifact.md').write_text('Revisione 17 delle due guide globali sul modello ITEC esistente. Parametri nella riga, N e verifiche per tratto, dettagli e distinta preliminari. Conservare stile, immagini, formule e parti del modello; verificare Word e PDF prima di archiviare Rev16.',encoding='utf-8')
subprocess.run([sys.executable,str(SUP/'scripts/wiki/build-wiki-index.py')],check=True)
subprocess.run([sys.executable,str(SUP/'scripts/Build-AntheaGuides-Itec.py')],check=True)
for p in (SUP/'README.md',SUP/'installer/Indice-guide.md'):subprocess.run([sys.executable,str(SUP/'scripts/documentazione/markdown-pdf.py'),str(p)],check=True)
