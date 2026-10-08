# Banco F2.1: esito dei casi vicini alla soglia e delle sezioni in forma chiusa

Voci di F2.1 del [piano](../../docs/refactoring/piano.md) «casi vicini alla soglia» e «test con sezioni in forma chiusa»
([f2.1-banco.md](../../docs/refactoring/f2.1-banco.md)). Nessun codice di calcolo modificato.

- Codice: branch `refactoring/f2-1-soglie` da main 98a21d4; `lib/Checker` S2 (GPCChecker.Concrete 0.0.17.0, Checker 0d7ba50b);
  runtime .NET 8.0.31; attesi `attesi/attesi.json` SHA-256 883007AA…BF7C.
- Corsa: `dotnet tests\ConcreteBench.Checks\bin\Release\net8.0\ConcreteBench.Checks.dll <cartella>` (circa 15 s) →
  PASS, 3393 controlli, 8 divergenze registrate (`divergenze.json`). Due corse danno `misura.json` identico byte per byte.
- Attesi: `py -3 tests\ConcreteBench.Checks\attesi\genera_attesi.py` (`--check` rigenera e confronta), solo libreria standard.

## Motori confrontati

| Grandezza | Legacy di X.Calculations | GPCChecker.Concrete (S2), chiamata diretta |
| --- | --- | --- |
| tensioni SLE e limiti | `CheckerSection.Stress` (limiti di `DescribeStress`) | `SectionCheckerModelCode2010.GetTensionAnalysisResult` + `StressLimitCheck.Evaluate` |
| fessurazione | `Ntc2018Checks.Cracking` | `SectionCrackCheck.Evaluate` (`SectionCrackInput` come nei `CrackMigrationTests`) |
| resistenza SLU a N assegnato | `SectionMomentResistance.Calculate` | `SectionSolver.CalculateDomainPoint` |
| verifica SLU di una combinazione | `CheckerDomain3D.CheckMany` (percorso di `ConcreteAnalysisSession.Domain`) | `CalculateDomainPoint` + `FailureDomainPoint.CalculateWorkingRatio` |
| taglio | `ConcreteCodeChecks.Shear` (motore `Legacy` dell'adattatore) | `SectionShearCalculator` (motore `Library` di `ConcreteShearTorsionAdapter`) |

Ingressi comuni: la sezione del Model di `CheckerSection.PrepareModel` (geometria, barre e materiali dal JSON del modulo) e la
norma di `ConcreteStandards.Effective`; forze in N e N·mm negli assi locali del modulo, controllate uguali a quelle di
`CheckerSection.Force`. Il resto del percorso della libreria non passa dal legacy.

Progetto nuovo (`tests/ConcreteBench.Checks`) e non `tests/ConcreteLibraryAdapter.Checks`: quella suite prova l'adattatore di
taglio e torsione con un contratto proprio (interruttore, `misura.json` confrontato byte per byte fra snapshot di
`lib/Checker`, F2.4-F2.6) e il lavoro parallelo di F2.7 tocca lo stesso ambito; qui si confrontano motori non ancora collegati
(SLE, fessurazione, SLU) e attesi esterni, con una registrazione delle divergenze che non deve cambiare quella suite.

## A. Sezioni in forma chiusa

Attesi indipendenti (Python): sezione elastica fessurata (n = Es/Ecm, φ = 0, calcestruzzo teso escluso, calcestruzzo spostato
dalle barre compresse sottratto) in flessione semplice e composta, sezioni interamente compresse e interamente tese; resistenza
SLU a N assegnato con stress-block (0,8 x, η fcd) e parabola-rettangolo, rottura lato calcestruzzo. Sezioni: rettangoli
300 × 500 con armatura asimmetrica (3Ø20 + 2Ø16) e semplice (3Ø20), 400 × 600 con 4 + 4Ø20; poligoni regolari di 72 lati
(D 600, 8Ø20) e 144 lati (D 800, 12Ø16), come il contorno circolare di ANTHEA. Per i rettangoli lo script controlla le sue
integrazioni con le formule chiuse dei manuali; per i poligoni riporta lo scarto dal cerchio vero (area compressa a parità di x:
da −0,20 a −0,30 % con 72 lati, −0,06 % con 144).

Controllo a mano di uno stato: R2 (400 × 600, 4 + 4Ø20 a 50 mm, C35/45, n = 200000/34077 = 5,869) in flessione semplice,
200 x² + (n − 1) As' (x − 50) = n As (550 − x) con As = As' = 1256,6 mm² → 200 x² + 13 494 x − 4 362 290 = 0, x = 117,76 mm
(attesi.json: 117,757 mm; motori: yn entro 2e-5 h).

Convenzione di segno ricavata e poi imposta a tutti i casi: Mx positivo di ANTHEA comprime il lembo superiore (y massimo).

Tolleranze fissate prima delle misure; scarti massimi misurati (relativi alla scala della grandezza):

| Grandezza | Stati | Tolleranza | Scarto massimo | Dove |
| --- | ---: | ---: | ---: | --- |
| σc,min (lembo compresso) | 19 | 1e-3 | 9,2e-5 | C1, flessione semplice: −15,1822 contro −15,1836 MPa |
| σs per barra | 19 | 1e-3 | 9,7e-5 | C2, presso-flessione: 40,6139 contro 40,6098 MPa |
| asse neutro yn (su h) | 19 | 1e-3 | 2,1e-5 | C1: 165,934 contro 165,921 mm |
| rapporto SLE rara (0,60 fck; 0,80 fyk) | 19 | 1e-3 | 7,8e-5 | C1 |
| rapporto SLE quasi permanente (0,45 fck) | 19 | 1e-3 | 1,0e-4 | C1 |
| MRd stress-block | 17 | 5e-3 | 1,8e-3 | R1 armatura superiore tesa, N = 0: 69,606 contro 69,731 kNm |
| MRd parabola-rettangolo | 8 | 5e-3 | 3,4e-4 | R1, N = −500 kN: 232,124 contro 232,204 kNm |

Legacy e libreria danno gli stessi numeri (entro 1e-9) in ogni stato: stesso solutore. Gli scarti dagli attesi sono quelli del
solutore di sezione (integrazione con punti di Gauss su una mesh fissa, non tagliata lungo l'asse neutro né lungo il salto dello
stress-block; parabola discretizzata; tolleranza della ricerca del punto resistente su N). Lo scarto massimo dello stress-block
coincide con il caso in cui il punto resistente ha |NRd − N| = 0,57 kN con N = 0.

## B. Casi vicini alla soglia

Per ogni ramo la soglia è trovata per bisezione sul legacy; i due motori sono valutati a λ*(1 ± 1e-4), λ*(1 ± 1e-6),
λ*(1 ± 1e-9) e λ*: stessi esiti e numeri entro 1e-9 in tutti i punti; il ramo atteso è controllato ai due lati a ± 1e-4.

| Prova | Soglia | Norme | Punti |
| --- | --- | --- | ---: |
| B1 rapporto tensionale ≈ 1 | calcestruzzo (rara, quasi permanente), acciaio (rara), getti sottili NTC (0,8), analisi non lineare | NTC, EN, MC2010, DIN | 70 |
| B2 wk ≈ wlim | flessione semplice di R3, XC3 (MC2010 con wlim di progetto 0,3 mm) | 7 ordinarie | 56 |
| B3 εsm − εcm | minimo βmin σs/Es contro valore calcolato | NTC, EN, MC2010, DS | 32 |
| B4 interasse | s = 5 (c + Ø/2) = 250 mm, salto fra (7.11) e (7.14) | NTC, EN, UNI, DIN, NS | 35 |
| B5 hc,eff | 2,5 (h − d) = (h − x)/3 (N = −55,49 kN) | NTC | 7 |
| B6 barre fuori da Ac,eff | limite superiore (7.14) (N = −693,98 kN) | NTC | 7 |
| B7 asse neutro alle barre tese | wk = 0 con asse neutro nel copriferro (N = −1000,01 kN) | NTC | 7 |
| B8 asse neutro al lembo teso | sezione interamente compressa (N = −1226,51 kN) | NTC | 7 |
| B9 asse neutro al lembo compresso | da flessione (k2 = 0,5) a trazione su tutta la sezione (N = +288,02 kN) | NTC, EN | 14 |
| B10 decompressione e formazione | σct,max = 0 (XD1, quasi permanente) e fctm/1,2 (XD3, frequente), armature sensibili | NTC | 14 |
| B11 SLU η ≈ 1 | dominio 3D del modulo contro la libreria, R1 (N = −500 kN) e C1 (N = −800 kN), Mx+ e Mx− | NTC | 28 |
| B12 taglio | η ≈ 1 con e senza staffe; vmin contro CRd,c; tetti di k (d = 200 mm), ρl (0,02), σcp (0,2 fcd); cot θ massimo; αc NTC a 0,25, 0,5 e 1 fcd | 7 ordinarie | 350 |

Esiti della fessurazione nei 179 punti: 82 apertura entro limite, 56 oltre limite, 8 senza barre in Ac,eff, 8 interamente tese,
7 asse neutro nel copriferro, 4 sezioni interamente compresse, 7 decompressione, 7 formazione delle fessure.

## Divergenze

Registrate in `divergenze.json` (la suite le riporta e passa); non corrette, da far decidere.

### S-1: punto resistente con N fuori dalla tolleranza del legacy (SLU)

Sezione C2 (poligono di 144 lati, D 800, 12Ø16 su r = 330, C40/50, NTC, stress-block), N = −1500 kN, direzione Mx+.
`SectionSolver.CalculateDomainPoint` restituisce NRd = −1502,0504 kN, MxRd = 707,0166 kNm, MyRd = −0,4157 kNm. Lo scarto su N
(2,05 kN) supera la tolleranza di `SectionMomentResistance` (max[1 kN; 1e-6 |N|] = 1 kN), che scarta il punto: il modulo
mostra «Soluzione non coerente con N e direzione assegnati.» invece della resistenza. Nella direzione Mx− la stessa sezione dà
la resistenza (−706,415 kNm, |NRd − N| = 0,05 kN). Atteso indipendente: 706,486 kNm; la libreria è a +0,075 %, entro la
tolleranza del banco.

- Motori: la libreria dà un valore, il legacy (adattatore) nessuno. Scarto su N negli altri 24 casi: fino a 0,57 kN (R1, N = 0).
- Effetto: chiamanti di `SectionMomentResistance` (resistenza rapida del modulo sezione, `ConcreteQuickResistance.cs:38`;
  sezione del palo orizzontale, `HorizontalConcreteSection.cs:22`, che rifiuta il calcolo; verifica N–M dei muri,
  `RetainingWall.Structures.cs:94`, che registra «GPC: resistenza non convergente o N fuori dominio»). La verifica delle
  combinazioni (`CheckerDomain3D.CheckMany`) non ha questo controllo.
- Da decidere: tolleranza della ricerca su N nella libreria, oppure tolleranza del controllo dell'adattatore riferita alla
  scala della sezione.
- **Decisione dell'utente (8/10)**: «tolleranza di convergenza che dipende dal legame costitutivo del materiale. stress
  block tolleranza maggiore. metti nota che potrebbe essere meno preciso». La regola della tolleranza va nella libreria
  (S3 se pronta prima del commit delle versioni) e `SectionMomentResistance` la usa dopo l'aggiornamento di `lib/Checker`;
  la nota sulla minore precisione dello stress block va nella documentazione della libreria e nella pagina del metodo, e
  nelle guide quando riparte la traccia W.

### T-1: VEd uguale a VRd senza staffe, esito opposto per 1 ulp (taglio)

Per le norme NTC 2018, EN, UNI, DIN, DS e NS senza armatura a taglio, con VEd uguale alla resistenza calcolata dal legacy
(sezione 300 × 500, d = 450 mm, Asl = 942,5 mm², C30/37, N = −100 kN): legacy VRd = 87,92520607962011 kN, η = 1, «Resistenza
sufficiente»; libreria VRd = 87,9252060796201 kN (1 ulp in meno), η = 1,0000000000000002, «Resistenza insufficiente» (DIN:
72,82100506635008 contro 72,82100506635007 kN). Con le staffe e con MC2010 nessuna differenza.

- Causa: le ultime cifre diverse già dichiarate nel registro F2-1 (unità N nella libreria, radice cubica), che qui cadono
  sull'uguaglianza esatta.
- Effetto: dal passo F2.6 il modulo calcola il taglio con la libreria, quindi un'azione esattamente uguale alla resistenza
  senza staffe ora dà «insufficiente». F2-1 dice «nessun esito diverso»: la voce va completata con questo caso limite, oppure
  il confronto η ≤ 1 va reso indipendente dall'ultima cifra. Decisione del coordinatore o dell'utente.
- **Decisione (8/10)**: l'utente ha lasciato la scelta al coordinatore («fai come credi meglio»). Si accetta e si registra
  nella voce F2-1, senza tolleranze sul verdetto: il caso ha misura nulla e una tolleranza sposterebbe il confine della
  verifica. La divergenza resta in `divergenze.json` come caso noto.

## Osservazioni (non divergenze)

- La soluzione elastica fessurata della libreria ha precisione relativa di circa 1e-6: il rapporto a λ = 1/rapporto(1) scarta
  fino a 5,6e-6 da 1 (B1, MC2010 su C1); fra 1 kNm e 52 kNm σs non è proporzionale entro 2,5e-4 (B3), quindi a carichi piccoli
  l'errore relativo cresce. I rami sono controllati a ± 1e-4 per questo motivo.
- Simmetria del dominio: C1 (doppiamente simmetrica) dà Mx+ = 341,7323 e Mx− = −341,7268 kNm (1,6e-5); |MyRd|/|MxRd| fino a
  5,9e-4 nelle sezioni simmetriche.

## Non coperto

Flessione attorno a y e deviata, sezioni a T e cave, trefoli, viscosità (φ > 0) negli attesi, torsione, dettagli, ancoraggi e
M-χ vicino alle soglie; il taglio è confrontato al livello delle formule (`ShearInput`), non del modulo.
