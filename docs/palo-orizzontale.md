# Palo singolo: capacità portante orizzontale

## Stato e campo di applicazione

Modulo `geo_palo_orizzontale`, motore `Broms-ANTHEA-1`, C#/.NET 8 senza DLL GPC.
La schermata riprende i sette pannelli del palo verticale: dati, modello,
coefficienti, verifica, stratigrafie, profilo, momento plastico/resistente.
I diagrammi sono accessibili da **Diagrammi e dettagli**.

Implementati e confrontati con soluzioni analitiche: terreno omogeneo granulare
drenato o coesivo non drenato; testa libera/impedita; corto, intermedio e lungo
ove applicabili. Estensione a strati della stessa famiglia e falda interna:
**sperimentale**, verificata per equilibrio e recupero dell'omogeneo, senza
validazione indipendente su stratificazioni reali.

Esclusi: sequenze miste coesivo/granulare, granulare c–φ, testa impedita fuori
dal piano campagna, momento indipendente da H, spostamenti, gruppi, ciclicità,
taglio, secondo ordine, precompressione. Il percorso normativo NTC/EC2 resta
incompleto; nessuna conformità automatica.

## Inventario delle fonti

| Materiale | Uso effettivo |
| --- | --- |
| Viggiani, *Fondazioni*, PDF fornito, 274 pagine a due facciate | Fonte principale: pp. stampate 400–415, PDF205–212 (conteggio da 1); formule controllate visivamente. |
| Lancellotta, *Geotecnica*, seconda edizione, PDF fornito, 270 pagine | Consultazione indice e ricerca nel testo OCR; non individuata una trattazione operativa di Broms equivalente a Viggiani. Nessuna formula implementata attribuita a questo testo. |
| PileChecker, `Checker/PileCalculator.cs`, righe 256–385 e 610–678 | Algoritmo orizzontale e ricerca della profondità analizzati criticamente. |
| PileChecker, `Test/PileHorizontalBearingCapacityTest.cs` | Otto casi omogenei, senza fonte indipendente dei valori attesi. Nessun test multistrato. |
| NTC, Circolare, EN1997 | Testi non allegati: nessun coefficiente attribuito automaticamente a tali norme. |

I libri restano su X:. Le scansioni locali di verifica non vengono distribuite.
Viggiani non ha restituito testo ricercabile con l'estrattore disponibile: la
trascrizione è stata controllata mediante lettura visiva, non presentata come OCR.
Riferimenti originali identificati: Broms (1964), *Lateral Resistance of Piles in
Cohesive Soils*, DOI 10.1061/JSFEAQ.0000611, e *Lateral Resistance of Piles in
Cohesionless Soils*, DOI 10.1061/JSFEAQ.0000614. Implementazione riferita alle
equazioni di Viggiani sotto elencate.

## Specifica meccanica e convenzioni

Palo circolare, verticale, D diametro, L lunghezza infissa. z=0 al piano campagna,
z positivo verso il basso. H positiva applicata a z=−e, e≥0. N positiva a
compressione, costante mentre H cresce. Testa impedita: forza e vincolo a z=0,
e=0. La capacità della struttura di vincolo di sviluppare il momento necessario
non è verificata. My costante lungo il fusto e uguale nei due versi.

Si assume disponibile la rotazione plastica richiesta; la duttilità non è
verificata. HEd serve al confronto finale, non determina Hu. Il momento
applicato è H·e. Un `momento_applicato` indipendente non nullo viene rifiutato.

p(z), in kN/m, positiva se opposta a H, è una forza distribuita, non una pressione
in kPa. Leggi limite (Viggiani p.400, PDF205):

- Coesivo: p_lim=0 per z<1,5D; p_lim=9CuD al di sotto. La zona nulla è riferita
  al piano campagna, non ripetuta per strato.
- Granulare: p_lim=3KpDσ′v; Kp=(1+sinφ′)/(1−sinφ′). Nell'omogeneo σ′v=γ_eff z.
- Estensione ANTHEA: σ′v integrata dagli strati sovrastanti, con γ sopra falda,
  γsat−9,81 sotto. σ′v continua alle interfacce; p_lim può saltare se cambia
  Kp/Cu. Non si mediano parametri e non si sommano capacità di singoli strati.
  Falda interna nel granulare attiva automaticamente la modalità sperimentale.

Q(z)=∫₀ᶻ p_lim(s)ds; S(z)=∫₀ᶻ s p_lim(s)ds; A(z)=zQ(z)−S(z).
Integrali analitici per tratti costanti/lineari. V=H−∫p;
M=M0+Hz−∫(z−s)p(s)ds. V e M continui alle interfacce ordinarie.

### Coesivo

z_f soddisfa Q(z_f)=H; nell'omogeneo z_f=1,5D+H/(9CuD). Nel tratto inferiore
[z_f,t], +p_lim fino a b, −p_lim dopo b, con Q(b)=[Q(z_f)+Q(t)]/2: risultante
nulla. Coppia C(z_f,t)=S(t)+S(z_f)−2S(b), pari a 9CuD(t−z_f)²/4 nell'omogeneo.
L'estensione di tale coppia a strati è una scelta ANTHEA sperimentale.

| Vincolo / meccanismo | Equilibrio | Viggiani, pagine stampate / PDF |
| --- | --- | --- |
| Libera corto | He+S(z_f)=C(z_f,L) | eq.13.23–13.26, pp.400–402 / 205–206 |
| Libera lungo | He+S(z_f)=My; C(z_f,t)=My, t≤L | eq.13.27–13.29, p.403 / 206 |
| Impedita corto | H=Q(L), M0=−S(L), S(L)≤My | eq.13.30–13.32, pp.405–407 / 207–208 |
| Impedita intermedio | −My+S(z_f)=C(z_f,L), M0=−My | eq.13.33–13.35, p.407 / 208 |
| Impedita lungo | S(z_f)=2My, M0=−My, C(z_f,t)=My | eq.13.36, pp.407–408 / 208–209 |

### Granulare

| Vincolo / meccanismo | Equilibrio | Viggiani, pagine stampate / PDF |
| --- | --- | --- |
| Libera corto | H=A(L)/(L+e), M0=He | eq.13.37–13.38, p.410 / 210 |
| Libera lungo | He+S(z_f)=My, Q(z_f)=H | eq.13.39–13.43, pp.410–411 / 210 |
| Impedita corto | H=Q(L), M0=−S(L) | eq.13.44–13.45, p.413 / 211 |
| Impedita intermedio | H=[My+A(L)]/L, M0=−My | eq.13.46, p.413 / 211 |
| Impedita lungo | S(z_f)=2My, M0=−My | eq.13.47, p.415 / 212 |

Corto/intermedio: F=Q(L)−H concentrata al piede, diretta come H, secondo la
semplificazione di p.409, PDF209. Registrata separatamente, produce un salto
nel taglio e non viene mascherata come pressione distribuita finita.

Nel lungo si completa il diagramma prolungando +p_lim fino alla radice t≥z_f
di M0+Ht−A(t)=0, con F=Q(t)−H a t. È un completamento **idealizzato e non
univoco**, scelto da ANTHEA per esplicitare l'equilibrio sotto la cerniera;
non è un'analisi elastoplastica dell'interazione. La capacità è fissata dal
tratto superiore secondo Broms. Non si impone un limite di pressione locale
alla F concentrata: non è una verifica puntuale del terreno.

### Scelta e controlli

Si sceglie il minimo dei candidati attivabili, senza soglie empiriche L/D.
Le alternative che superano il primo limite non vengono dichiarate
contemporaneamente ammissibili. Si controllano t≤L, |M|max≤My, equilibrio
finale di forze e momenti. Nessuna soluzione apparentemente valida in caso di errore.

Bisezione con massimo 100 iterazioni, tolleranza sulla coordinata scalata con
max(1,L), default 1e−8; intervallo ammesso 1e−12…1e−5. Controlli finali di
equilibrio 1e−5 sulle scale max(1,Q(L)) e max(1,My,Q(L)L). Diagrammi fino a
20.000 intervalli, con nodi aggiunti a strati, falda, cerniere, inversioni ed
estremi. Il passo grafico non cambia la capacità. φ′<60° è un controllo d'input
del software, non una prescrizione normativa.

## Momento della sezione

Origini esplicite: `Sezione c.a.` o `Manuale`. La seconda richiede valore e
natura/provenienza. Il pulsante di calcolo non sovrascrive il valore manuale.
N assegnata deve essere coerente con la fonte manuale. Il numero di barre nel
calcolo automatico deve essere pari (4–512), per mantenere la simmetria nel
piano di flessione senza introdurre un momento ortogonale non richiesto.

Calcolo automatico: leggi materiali di `SezioneCA`, sezione circolare, armature
uniformi. Diametro e N provengono dai dati del palo. Raggio barre:
D/2−copriferro esterno staffa−diametro staffa−diametro barra/2.
fcd=αcc fck/γc; fyd=fyk/γs, coefficienti espliciti. Unità mm/N/MPa, uscita kN/kNm.
Piano di flessione Mx, barre orientate come nel disegno; nessuna ricerca della
direzione più debole. Nessuna verifica della duttilità dedotta dal solo My.

ε(y)=εcu(y−R+x)/x; CLS parabola-rettangolo senza trazione; acciaio elastico
perfettamente plastico. Sottrazione del CLS sostituito dalle barre. Si risolve
ΣF=N e si calcola ΣFy. Campo ristretto all'asse neutro interno (0<x<D), per
evitare l'estensione impropria al dominio tutto compresso; fuori campo serve My
manuale. Mesh polari 28×96 e 56×192, adottata la seconda; scarto esposto e
risultato rifiutato oltre 2%. Questa è una soglia numerica, non di sicurezza.
Equilibrio assiale con 65 bisezioni. Test indipendente con quadratura Simpson
per strisce orizzontali, distinta dalla mesh polare.

## Capacità e normativa

Hu per sondaggio; minimo governante. Questo minimo non viene chiamato
automaticamente resistenza caratteristica. HEd/Hu è un rapporto meccanico.
Rk = min(media(Hu)/ξ3; min(Hu)/ξ4); Rd = Rk/1,3, applicati automaticamente.
ξ3 e ξ4 provengono da Calcolo.Verticali, come nel palo verticale; il numero
di verticali indagate è scelto dall'utente, non dedotto dal numero di schede.
I file precedenti senza questa selezione usano una verticale (ξ3 = ξ4 = 1,70);
i vecchi divisori manuali e il relativo interruttore non sono più applicati.
Le chiavi risultato con suffisso _manuale sono mantenute per compatibilità degli export.
Non sono implementate le combinazioni delle azioni: inserire HEd di progetto.
Occorre anche verificare la natura di My e i fattori già
applicati ai materiali. La relazione dichiara sempre verifica normativa incompleta.

## Confronto critico con PileChecker

### Efficienza della palificata

Due opzioni: Manuale (0 < η ≤ 1, default 1), oppure Reese & Van Impe (foglio).
La seconda riproduce il file SMath fornito “Portanza orizzontale palo incastrato
in testa in terreno incoerente - [Viggiani cfr.13.2.5].sm”, revisione 37.
Interassi anteriore, posteriore, sinistro e destro in metri, riferiti alla direzione H.
I contributi diretti sono min(1; 0,7(s_ant/D)^0,26), min(1; 0,48(s_post/D)^0,38)
e min(1; 0,64(s_lato/D)^0,34) sui due lati.
Per ogni diagonale β = atan(s_lato/s_longitudinale) e il contributo è
sqrt(η_longitudinale² cos²β + η_lato² sin²β). η è il prodotto degli otto contributi.
Rd = η Rk / 1,3; Hu, diagrammi del palo singolo e Rk non sono ridotti.
Lo schema implica quattro vicini allineati e quattro diagonali, non una geometria
arbitraria. Come avverte il foglio, maglie fitte possono richiedere altri pali
interferenti. Il programma non determina automaticamente queste interferenze.
Le note del foglio sugli interassi non introducono ulteriori soglie nelle formule:
si riproduce il min(1; ...) effettivamente presente nelle espressioni SMath.

`externalForce` ed `eccentricity` non intervengono nel suo metodo orizzontale.
My proviene da GPC Concrete con conversione Nmm→kNm. Le capacità lunghe sono
sommate per strato riutilizzando My; γsat è usato senza la quota di falda,
mentre σv cresce con γ efficace. Il tratto 1,5D è sottratto dalla lunghezza
residua senza una correzione coerente dello spessore del primo strato.

Confronto riproducibile del solo candidato lungo coesivo: D=1 m, Cu=50 kPa,
My=450 kNm. L'espressione del riferimento
(-13,5+sqrt(182,25+36My/(CuD³)))CuD² restituisce 450 kN per uno strato;
sommandola per due strati identici restituisce 900 kN prima dei fattori.
ANTHEA recupera 450 kN per testa impedita, indipendentemente dalle interfacce.
Non è presentato come confronto completo con l'eseguibile.

La DLL GPC Concrete non è presente al HintPath del progetto fornito. I valori
524/476/491/441/520 kN dei test dipendono da un My esterno non riportato:
non dichiarati riprodotti, nessuna calibrazione per farli coincidere.
Il confronto eseguibile completo resta da fare con dipendenze disponibili.

## Prove riproducibili

`X.Verifiche/HorizontalChecks.cs`: D=1 m, Cu=50 kPa oppure φ′=30°,
γ=18 kN/m³ (Kp=3), e=0.

| Terreno / vincolo / meccanismo | L [m] | My [kNm] | Hu attesa [kN] |
| --- | ---: | ---: | ---: |
| Coesivo libera corto | 2,5+√8 | 10000 | 450 |
| Coesivo libera lungo | 10 | 900 | 450 |
| Coesivo impedita corto | 2,5 | 10000 | 450 |
| Coesivo impedita intermedio | 5,5 | 1800 | 900 |
| Coesivo impedita lungo | 10 | 450 | 450 |
| Granulare libera corto | 2 | 1000 | 108 |
| Granulare libera lungo | 10 | 432 | 324 |
| Granulare impedita corto | 2 | 1000 | 324 |
| Granulare impedita intermedio | 2 | 108 | 162 |
| Granulare impedita lungo | 10 | 216 | 324 |

Tolleranza relativa scalata 1e−6 (più larga della bisezione, non percentuale
di progetto). Residui benchmark ≤0,001 kN/kNm. Sezione a strisce: tolleranza
0,4% tra quadrature diverse. Ulteriori test: eccentricità, falda, interfacce
fittizie, passo grafico, input fuori campo, equilibrio, limiti My, fattori,
immutabilità input, roundtrip, CSV e XML della relazione.

## Avvio e file

`Avvia ANTHEA.cmd` → Moduli singoli → Palo → Capacità portante orizzontale.
`Compila.cmd` pubblica la versione, `Verifica.cmd` esegue tutta la suite.

```powershell
dotnet X.Desktop/bin/Release/net8.0-windows/ANTHEA.dll --smoke-horizontal verifiche_orizzontale_ui
dotnet run --project X.Verifiche -c Release -- --calcola esempi/palo_orizzontale.json verifiche_orizzontale_risultato.json
```

Lo smoke crea schermate e file di verifica, poi chiude l'app. Usare sempre una
cartella nuova. Contenitore archivio versione 1, dati `versione_orizzontale: 1`.
I risultati non vengono riusati all'apertura: è necessario ricalcolare.
