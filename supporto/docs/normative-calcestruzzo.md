# Calcestruzzo ordinario — normative e verifiche di sezione

Aggiornamento: 28 settembre 2026. Il selettore propone NTC 2018, Model Code 2010,
EN 1992-1-1 e le varianti UNI, DIN, DS e NS. CNR-DT 204 e TR34 restano leggibili
negli archivi storici, ma non sono proposti per nuovi calcoli: FRC e pavimentazioni
sono esclusi da questa attività su indicazione del progettista.

## Implementazioni

Le classi GPC.Model.Standards restano la sorgente dei coefficienti. Domini,
tensioni e piani di deformazione usano GPC Checker; geometrie e baricentri
usano GPC Geometry. Le nuove formule sono in ANTHEA.Calculations, indipendente
 da WPF ed esportabile con il progetto già predisposto.

| Profilo | Taglio senza / con staffe | Apertura delle fessure |
| --- | --- | --- |
| NTC 2018 | §§4.1.2.3.5.1–2; formulazione esistente | NTC e Circolare 2019; frequente e quasi permanente |
| EN 1992-1-1 | Prima generazione, §§6.2.2–3 | §7.3.4; quasi permanente |
| UNI EN 1992-1-1 | DM 31/07/2012: ν nazionale; αcw=1 per c.a. non precompresso | Formule EC2 e criteri ambientali italiani |
| DIN EN 1992-1-1 | CRd,c, vmin, ν1, z e intervallo di cotθ nazionali | Altezza efficace e distanza tra fessure DIN; kt=0,4 |
| DS EN 1992-1-1 | DK NA:2024; vmin e ν1 nazionali, cotθ≤2 cautelativo, duttilità almeno B | k3 e area efficace DK; sistemi fine e grossolano in trazione |
| NS EN 1992-1-1 | NA:2010: granulometria, trazione assiale, limite C60 a taglio | XD3/XS3 in frequente; kc=1 senza incremento favorevole |
| Model Code 2010 | Livello II: εx da N–M–V, Asl e granulometria | Lunghezza di trasferimento; durata breve/lunga; wlim assegnato |

DIN usa i parametri di prima generazione riscontrati nella documentazione NA:2011
e nel benchmark SOFiSTiK; il materiale GPC dichiara NA:2013-04. Questa distinzione
è intenzionale: non è un'attestazione di copertura di ogni aggiornamento
 dell'annesso. NS è NA:2010. Non sono implementati EC2:2023, Model Code 2020
 o il progetto di aggiornamento delle appendici italiane.

Predefiniti corretti nell'adattatore: UNI αcc=0,85; DIN αct=0,85;
NS αct=0,85 e εud/εuk=0,4. DS usa γc=1,45 e γs=1,20 della DLL corrente.
I coefficienti salvati restano assegnazioni dell'utente: consultare il confronto
con i predefiniti e usare il ripristino per aggiornare un vecchio foglio.

## Dati condivisi e aggiornamento

- Le combinazioni di taglio conservano nome, N, Mx, My, Vx, Vy, T anche in
  archivio, Excel e report. I vecchi file restano leggibili; momenti assenti
  vengono inizializzati a zero nell'interfaccia.
- N negativo a compressione. Mx è associato a Vy, My a Vx nel livello II MC.
  Le azioni sono già combinate/coefficientate: non vengono moltiplicate ancora.
  γc e γs operano sulle resistenze.
- dg è condiviso con Dettagli → Copriferro e durabilità; modificarlo invalida il
  taglio. Δe MC è distinto per asse e visibile solo per quel modello. Asl
  automatica richiede conferma dell'ancoraggio, anche con staffe nel Model Code;
  cambiare la geometria revoca la conferma.
- wlim è comune alle SLE. Vuoto: criterio del codice; MC e classi ambientali
  non tabulate richiedono un valore esplicito. NTC/UNI conservano i propri
  criteri e ignorano l'override. Modificare wlim aggiorna la verifica senza
  ricostruire l'equilibrio tensionale invariato.

## Geometrie

Rettangolo, T, cerchio, rettangolo cavo e cerchio cavo sono collegati a taglio
 e fessurazione. Nel taglio automatico si impiegano larghezza resistente minima
 e armatura efficace nei due versi. I fori sono sottratti. Per i cerchi occorre
scegliere il modello: le riduzioni per pile NTC non vengono trasferite agli altri
codici, che richiedono z/d assegnato e una schematizzazione verificata.

wk richiede analisi lineare con CLS teso escluso. Le fasce efficaci sono tagli
 geometrici della sezione reale, verificati separatamente senza sommare aree di
 facce diverse. La trazione uniforme/disuniforme è inclusa. DS usa il baricentro
 della fascia per il sistema fine e controlla anche il sistema grossolano, con
 intera area tesa e fattore 0,5 DK.

Le superfici interne hanno fasce di parete o anello limitate a metà spessore.
Nel rettangolo si limita anche l'estensione tangenziale alla faccia del foro:
le barre esterne agli angoli non diventano armatura della parete interna.
Un foro compresso non richiede verifica di apertura; una superficie tesa senza
armatura efficace/interasse definito lascia il controllo incompleto. Un
superamento noto resta negativo. Report e vista mostrano lo stesso inviluppo.
L'estensione a pareti/anelli è una schematizzazione geometrica del modulo,
esplicitata nelle tracce; non è una validazione sperimentale di tutte le cavità.

## Campo ancora non coperto

Il modulo non costituisce una verifica normativa completa. Restano esclusi:

- taglio CAP con componenti dei cavi e apertura con aderenza dei trefoli;
- torsione accoppiata e dettagli/ancoraggi nazionali diversi dal modello NTC
  disponibile; torsione automatica della T;
- armatura minima per deformazioni imposte, fatica, fuoco, gerarchia sismica,
  punzonamento, appoggi, perdite e secondo ordine;
- interazione generale Vx–Vy: le resistenze sono controllate separatamente;
- wk non lineare, barre lisce MC/DIN, durabilità nazionale completa e incremento
  favorevole del limite NS legato al copriferro;
- normative di seconda generazione e FRC.

Per fonti, confronti indipendenti e comandi vedere
[validazione delle normative CA](ca-normative-validazione.md).
