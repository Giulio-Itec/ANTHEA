# Pressione unica negli abachi — ipotesi autorizzata p_l = p_i

Modifica D esplicitamente approvata dall'utente: la pressione d'iniezione generale p_i viene usata come p_l negli abachi Bustamante–Doix. È un'ipotesi progettuale, non l'identificazione delle due grandezze nel testo di Viggiani.

Eliminato l'input p_l dalla stratigrafia e dai nuovi file. I valori locali nei vecchi file sono ignorati. La pressione generale aggiorna subito α/s visualizzate (α rimane il valore adottato) e tutte le resistenze. Restano i limiti degli abachi, senza estrapolazione.

Report, didascalie e ascissa dei grafici dichiarano p_l = p_i. Eliminato il confronto fra due pressioni indipendenti, che sotto questa ipotesi sarebbe tautologico. Per strati con precedenti p_l diversi da p_i, le resistenze cambiano. Peso CHS e scheda palo invariati.

Verifiche: 35 test, conto manuale con pressione uniforme, influenza di p_i sulle curve, irrilevanza dei p_l locali precedenti; GUI in 12 combinazioni e aggiornamento s al variare di p_i; grafico Word controllato visivamente.

# Revisione CHS e IRS su tutta la lunghezza — 10 settembre 2026

Le indicazioni storiche sotto riportate sui primi 5 m IGU sono superate nell'implementazione, su esplicita richiesta dell'utente: ora il tipo scelto vale per tutta la zona iniettata. È una scelta distinta dalla raccomandazione del libro, indicata nei report. La stratigrafia mostra un solo α e s.

Il peso considera CHS pienamente riempito e calcestruzzo esterno, entro Db, senza sottospinta. Catalogo geometrico: Tata Steel Celsius, https://www.tatasteel.com/media/14622/celsius-overview-brochure-all.pdf ; masse nominali ricalcolate con 7850 kg/m³. Dati in catalogo_chs.csv. p_i è unico; p_l resta per strato e continua a governare la lettura dell'abaco.

# Micropali — Bustamante–Doix secondo Viggiani

Fonte: PDF fornito dall'utente, C. Viggiani, Fondazioni, §13.1.6, pp. 392–396 (pagine PDF 201–203). Edizione non identificata dalla scansione; il file abachi.json conserva l'impronta SHA256 del PDF. Le immagini riproducono le pagine consultate per uso di verifica.

## Metodo implementato

Rs = Σ π × α × Db × Ls × s (eq. 13.21). α è adimensionale; Db e Ls in m; p_l in MPa; s in kPa. I valori s sono calcolati dal motore, non ricavati dal testo arrotondato nell'interfaccia.

Si inserisce direttamente la pressione limite Ménard p_l. Le scale ausiliarie NSPT del libro non vengono convertite automaticamente. Famiglie: SG per sabbie/ghiaie, AL per limo/argilla, MC per marne e calcari indicati in tabella, R per roccia alterata/fratturata. IRS usa la curva 1, IGU la curva 2. Per R si adotta il limite inferiore indicato dalla tab. 13.13.

I punti in abachi.json sono letture approssimate della scansione: interpolazione lineare tra punti, senza estrapolazione. Esclusi i tratti tratteggiati AL. Confronto_abachi.png sovrappone le curve adottate (rosso IRS, blu IGU) agli abachi; la scansione ha una lieve deformazione prospettica. I dati non hanno la precisione di formule analitiche pubblicate.

Gli α iniziali sono i minimi della tabella 13.12; sono editabili entro l'intervallo del terreno selezionato. La scelta effettiva resta visibile e deve rispecchiare l'esecuzione.

Quando zb=0, i primi 5 m sono calcolati come IGU anche con IRS selezionato. La lunghezza utile inferiore ai 4 m raccomandati (al netto dei primi 5 m in questo caso) produce un avviso. Condizioni esecutive di p. 392: IRS p_i ≥ p_l; IGU 0,5 p_l ≤ p_i ≤ p_l. Verificare portate e quantità minime di miscela della tab. 13.12 sulle pagine allegate.

Punta: resta facoltativa; attivandola, percentuale iniziale 15% di Rs, modificabile tra 0 e 15%. È la prima opzione descritta a p. 396. La formula alternativa con k_p non è implementata. Nessun contributo di punta a trazione. Peso proprio calcolato sul diametro nominale Db; α riguarda il diametro efficace di aderenza. Coefficienti di progetto e gestione del peso/falda restano quelli della scheda preesistente, separati dal modello di resistenza limite.

## Compatibilità

FHWA eliminato dal motore e dall'interfaccia. I vecchi fogli restano apribili, ma richiedono terreno, tipo IGU/IRS, p_l e α appropriati. Le aderenze FHWA non vengono reinterpretate come α né come s. Confrontare e validare i nuovi risultati prima dell'uso progettuale.

## Controlli

24 test automatici: regressione del palo, conti manuali BD multistrato, IGU/IRS, primi 5 m, domini, α e unità, input non modificati, report ogni 0,50 m. GUI: 12 combinazioni di modulo, risoluzione e scala caratteri, controlli curve e salvataggio/riapertura. È una verifica software; la digitalizzazione degli abachi richiede revisione ingegneristica.
