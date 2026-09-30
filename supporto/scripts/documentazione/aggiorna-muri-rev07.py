"""Archive replaced documentation and align the wall guides with revision 07."""
from pathlib import Path
import json
import hashlib
import shutil

ROOT = Path(__file__).resolve().parents[3]
SUPPORT = ROOT / "supporto"
ARCHIVE = SUPPORT / "SUPERATI/muri-completamento-rev07-20260930"
ARCHIVE.mkdir(parents=True, exist_ok=True)
items = []
paths = [SUPPORT / "docs" / (stem + ext) for stem in ("guida-pratica-anthea", "guida-teorica-anthea", "muri-sostegno") for ext in (".md", ".pdf")]
paths += [SUPPORT / "documentazione/Guide_ANTHEA" / f"ANTHEA_Guida_{kind}_ITEC_Rev06{ext}" for kind in ("pratica", "teorica") for ext in (".docx", ".pdf")]
paths += [SUPPORT / "README.md", SUPPORT / "README.pdf"]
for source in paths:
    target = ARCHIVE / source.relative_to(SUPPORT)
    assert source.resolve().is_relative_to(SUPPORT.resolve())
    assert target.resolve().is_relative_to(ARCHIVE.resolve())
    if source.exists() and not target.exists():
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        items.append(dict(original=str(source.relative_to(ROOT)), archived=str(target.relative_to(ROOT)), sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                          reason="Sostituito dalla revisione 07 su portanza sismica cedimenti spostamenti e armature",
                          replacement=str(source.relative_to(ROOT)).replace("Rev06", "Rev07")))
        if "Rev06" in source.name:
            source.unlink()
registry = ARCHIVE / "registro.json"
if items:
    registry.write_text(json.dumps(items, ensure_ascii=False, indent=2), encoding="utf-8")

new = """
## Portanza sismica cedimenti spostamenti e armature Rev07

La revisione 07 aggiunge i calcoli dei punti 2, 3 e 5 nel campo dichiarato: inerzia del terreno nella portanza sismica; cedimenti e spostamenti; dettagli e predimensionamento delle armature e modelli strutturali per gravità. Rimangono le due schede Input e Verifiche. Il report Word contiene input, ipotesi, coefficienti, risultati e distinta delle barre; una quinta figura mostra le armature della sezione.

### Portanza sismica

In Terreno aprire Portanza sismica. Con Da sito si usa ah/g=ag/g·Ss·St, prima della riduzione β del muro; av/g=±0,5ah/g. In alternativa assegnare entrambe le accelerazioni. Il fattore γRD è modificabile: 1 per sabbia medio densa, 1,15 per sabbia sciolta asciutta. Non è un valore ricavato automaticamente dal solo angolo di attrito.

Si applica EN 1998-5:2004 allegato F alla fondazione nastriforme su terreno granulare asciutto, omogeneo e con base ruvida. Nmax=0,5γ(1−av/g)B²Nγ, con Nγ=2(Nq−1)tanφd. Si trascura il contributo favorevole del ricoprimento. N, V e M sono normalizzati con γRD·γR; F=γRD·ah/(g tanφd). Il γR della combinazione è applicato separatamente e dichiarato nella relazione.

Il dominio usa a=c=0,92; b=d=1,25; e=0,41; f=0,32; m=0,96; k=1; k′=0,39; cT=1,14; cM=c′M=1,01; β=2,90; γ=2,80. La somma dei termini di interazione deve essere ≤1, con 0<N̄<(1−0,96F)^0,39. La capacità è cercata lungo il raggio N,V,M: il tasso η è l’inverso del moltiplicatore limite, non il valore della funzione di interazione. Non si applicano una seconda volta larghezza efficace e fattori di inclinazione.

In Verifiche scegliere una combinazione SISMA e Portanza sismica nel riepilogo. Sono leggibili Nmax, F, N̄, V̄, M̄, limite verticale, interazione, tasso ed esito. Un’accelerazione mancante, un terreno fuori campo o una risultante non ammissibile restano esplicitamente non verificati.

### Cedimenti e spostamenti di esercizio

In Terreno attivare Calcola cedimenti finali e inserire, a partire dal piano di posa, nome, spessore e modulo edometrico M di ciascuno strato. M è espresso in kPa: per esempio 30 MPa corrispondono a 30000 kPa. Non viene dedotto da φ o riempito con un valore presunto. La pressione del terreno rimosso è il carico geostatico eliminato con lo scavo, da valutare nel modello scelto; zero è una scelta esplicita.

Si integra s=∫Δσz/M dz con tensioni Boussinesq di una striscia infinita e pressione di contatto lineare. Il calcolo è ripetuto a valle, al centro e a monte. La profondità deve arrivare a Δσz≤10% del carico netto oppure a un substrato rigido documentato. Viene controllata anche la convergenza numerica. Profili insufficienti non producono un esito favorevole né uno spostamento totale valido.

È un cedimento finale con moduli costanti assegnati: non ricostruisce tempi di consolidazione, OCR, scarico e ricarico, variazione di M con le tensioni o degrado ciclico. La rotazione θ=(smonte−svalle)/B deriva dal profilo libero; non è una soluzione accoppiata della fondazione rigida.

Per Calcola spostamenti in testa servono anche la rigidezza orizzontale di fondazione K per metro di muro, in kN/m², e il limite scelto. Il fusto in c.a. usa curvature delle sezioni fessurate GPC con viscosità assegnata. La doppia integrazione fornisce u del fusto con base fissa; la stima disaccoppiata totale è utesta=ufusto+H/K−θHmuro. Il termine di rotazione conserva il segno. Le curvature mancanti impediscono il risultato. La gravità usa il modello elastico del materiale nel campo senza trazione.

Limiti iniziali modificabili: 25 mm per cedimento, 0,002 rad per rotazione e 20 mm per spostamento in testa. Sono valori di avvio da valutare per l’opera, non limiti normativi universali. In Verifiche scegliere Cedimenti e spostamenti per la tabella per combinazione, i contributi degli strati e le curvature.

### Spostamenti permanenti Newmark

In Azioni aprire Spostamenti permanenti e aggiungere una storia. Scegliere SLD o SLV, inserire ky/g, fattore di scala e limite di spostamento. ky/g è la soglia di inizio scorrimento del muro, da ricavare da un’analisi di equilibrio: non coincide con ag/g e non viene dedotta automaticamente dal coefficiente kh.

Importare un CSV a due colonne separate da punto e virgola: tempo in secondi e accelerazione verso valle in g. È ammessa una prima riga t;a_g e il separatore decimale italiano. I tempi devono essere crescenti. Confermare che storia e scala siano compatibili con sito e stato limite. I campioni restano salvati nel file del muro.

Il blocco rigido scorre in una sola direzione. L’integrazione dei tratti lineari di a(t)−ky·g tiene conto degli attraversamenti della soglia, dell’arresto e della coda finale a terreno fermo. Wood è escluso perché presuppone un muro vincolato. Il risultato riguarda ciascuna storia; la scelta e la conformità normativa dell’insieme degli accelerogrammi devono essere documentate. Lo SLD non viene ricavato dal solo ag/g SLV.

### Armature e comando Calcola armature

In Geometria si possono mantenere le facce simmetriche o assegnare due armature indipendenti. La prima faccia è monte nel fusto e inferiore nelle solette; la seconda è valle nel fusto e superiore nelle solette. Rimangono disponibili le due zone verticali separate da h₁.

Ogni zona contiene barre principali, diametro e passo delle secondarie, lunghezza di ancoraggio, sovrapposizione e mandrino. Zero nelle lunghezze significa calcolo automatico, non lunghezza nulla. Il pannello dei dettagli espone aggregato, aderenza, vita nominale, tolleranza del copriferro e collegamenti della giunzione.

Calcola armature cerca diametri e numeri interi di barre entro i limiti impostati. Ogni candidato viene controllato con GPC a N–M, a taglio e in SLE; la proposta usa armature simmetriche per zona, poi modificabili. L’area stimata dalla flessione serve soltanto a scartare candidati impossibili. La verifica finale include i dettagli: una sezione resistente può avere una piega o una giunzione che non entra. In tal caso l’esito lo segnala e può occorrere aumentare lo spessore. La ricerca è interrompibile. Premere Applica proposta per sostituire le barre inserite; prima di applicare restano conservate.

Il predimensionamento usa ancoraggi a fyd e nessuna riduzione favorevole dei coefficienti di forma o confinamento. fbd deriva dalle proprietà GPC e dalle condizioni di aderenza. Le giunzioni sono alla stessa quota, quindi lo schema richiede il 100% delle barre giuntate e numeri compatibili nelle due zone. Si controllano lunghezza comune, interferro tra coppie, ingombro, area e passo dei collegamenti. Il mandrino considera anche la pressione nel calcestruzzo all’interno della piega.

In Vista dei risultati scegliere Armature: si vedono i percorsi delle barre, le pieghe, la fascia di sovrapposizione e le marche. Le barre giuntate sono affiancate lungo lo sviluppo del muro; le proiezioni sono leggermente distanziate sul disegno per leggibilità. Dettagli armature riporta la distinta, fbd, lunghezze richieste e usate, mandrini, quantità e tutti i controlli. I pesi sono stime per metro comprensive di ancoraggi, giunzioni e secondarie. Restano da definire il disegno esecutivo, i giunti di costruzione, i bordi lungo il muro, le interferenze tridimensionali e gli sfridi: la vista non è una distinta di officina.

### Gravità in calcestruzzo o muratura

In Materiali scegliere Calcestruzzo non armato oppure Muratura. Il primo usa fck e proprietà GPC, con compressione e taglio NTC 4.1.11 e fct1d=0,85 fctk,0.05/γc. Per muratura occorrono fk, fvk0, limite caratteristico a taglio, γM, fattore di confidenza e modulo elastico; non si possono usare automaticamente le resistenze del calcestruzzo.

Il fusto è una mensola libera: lunghezza efficace almeno 2H, imperfezione almeno H/200, rigidezza EI minima e amplificazione 1/(1−N/Ncr). La verifica rimane nel campo senza trazione e N<0,8Ncr. Se queste condizioni non sono soddisfatte serve un modello non lineare e l’esito non è dichiarato favorevole. Per muratura si controllano blocco compresso 0,85fk/(γM·FC) e scorrimento dei giunti; la resistenza a trazione è nulla. Le mensole di fondazione dello stesso materiale sono controllate anche a trazione, quindi una mensola in muratura può richiedere una diversa soluzione costruttiva.

La modalità Resistenze assegnate conserva la compatibilità con i file precedenti e i relativi controlli elastici; non diventa automaticamente una verifica normativa completa.

### Esempio ripercorribile e rapporto

Aprire supporto/artefatti/muri-completamento-20260930/interfaccia-finale/esempio-completo.anthea. Il modello dimostrativo ha H=3 m, B=3 m, due zone di armatura, terreno deformabile di spessore 25 m con M=30000 kPa, sisma da sito e una storia triangolare sintetica. Questi dati servono a riprodurre il test e non descrivono un sito reale. La storia sintetica non è un accelerogramma normativamente qualificato.

Nella stessa cartella sono presenti relazione Word e PDF, figure della sezione e risultati JSON. Il rapporto CONTROLLO.md e PDF nella cartella principale dell’attività descrive test, correzioni e limiti. I confronti MAX rimangono sospesi: nessuna delle nuove funzioni è dichiarata validata contro MAX 16.

I nuovi motori ShallowFoundationSeismic, FoundationSettlement e NewmarkSliding sono separati in X.Calculations/Geotechnics; l’adattatore del muro è RetainingWall.Serviceability. Geometria delle barre e predimensionamento sono separati dall’interfaccia. Materiali, equilibrio e tensioni delle sezioni riutilizzano GPC. Sono candidati per una successiva estrazione nelle librerie GPC; nessun repository GPC esterno è stato modificato.

Fonti: [JRC Eurocode 8 Worked Examples](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/EC8_Seismic_Design_of_Buildings-Worked_examples.pdf), §4.8; [JRC Eurocode 2 Detailing](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/05_EC2WS_Arrieta_Detailing.pdf); [USGS Newmark](https://pubs.usgs.gov/sir/2007/5196/sir2007-5196_text.pdf); NTC 2018 §§4.1.11 e 7.8.2.2.3; USACE EM 1110-1-1905, 2025.
"""

p = SUPPORT / "docs/muri-sostegno.md"
s = p.read_text(encoding="utf-8-sig")
s = s.replace("Rev06", "Rev07")
s = s.replace("Le due facce restano simmetriche.", "Le facce possono essere simmetriche o indipendenti.")
s = s.replace("Quantità indicative prive di ancoraggi, sovrapposizioni e armatura secondaria.", "Con i dettagli attivi la quantità comprende ancoraggi, sovrapposizioni e armature secondarie.")
s = s.replace("portanza 1,2, con portanza sismica comunque non calcolata", "portanza 1,2, con inerzia del terreno nell’allegato F")
s = s.replace("Il percorso automatico riguarda lo SLV; gli spostamenti e lo SLD non vengono verificati.", "Il percorso automatico delle spinte riguarda lo SLV; per gli spostamenti SLD/SLV è disponibile il calcolo separato Newmark con accelerogrammi.")
s = s.replace("La portanza sismica resta da verificare: manca l'inerzia del terreno di fondazione nel relativo meccanismo.", "La portanza sismica comprende l’inerzia del terreno nel modello Annex F descritto in Rev07.")
s = s.replace("cedimenti, spostamenti, liquefazione, verifiche idrauliche e dettagli esecutivi restano esclusi.", "cedimenti e spostamenti richiedono l’attivazione e i dati descritti in Rev07; liquefazione, verifiche idrauliche e completamento esecutivo restano esterni.")
s = s.replace("Non è una verifica completa di pietrame o muratura.", "La modalità Resistenze assegnate non è una verifica completa di pietrame o muratura; i nuovi modelli CLS e Muratura sono descritti in Rev07.")
if "## Portanza sismica cedimenti spostamenti e armature Rev07" not in s:
    s += "\n" + new
p.write_text(s, encoding="utf-8")

for kind in ("pratica", "teorica"):
    p = SUPPORT / f"docs/guida-{kind}-anthea.md"
    s = p.read_text(encoding="utf-8-sig").replace("revisione documentale 06", "revisione documentale 07")
    s = s.replace("La revisione 06 allinea il", "La revisione 07 mantiene l’allineamento del")
    s = s.replace("Cedimenti, spostamenti, verifiche idrauliche, liquefazione e dettagli esecutivi richiedono analisi dedicate:", "Cedimenti e spostamenti sono disponibili nei modelli separati descritti in Rev07; verifiche idrauliche, liquefazione e completamento esecutivo richiedono analisi dedicate:")
    s = s.replace("Restano esclusi i meccanismi non circolari, la stabilità generale del versante, cedimenti e liquefazione.", "Nel motore Bishop restano esclusi i meccanismi non circolari, la stabilità generale del versante e la liquefazione. I cedimenti sono ora trattati da un motore separato.")
    if "## Portanza sismica cedimenti spostamenti e armature Rev07" not in s:
        s += "\n" + new
    p.write_text(s, encoding="utf-8")

p = ROOT / "supporto/scripts/Build-AntheaGuides-Itec.py"
s = p.read_text(encoding="utf-8-sig").replace("REVISION = '06'", "REVISION = '07'").replace("REVISION_NOTE = 'STABILITÀ GLOBALE GUIDATA'", "REVISION_NOTE = 'MURI SISMA SLE E ARMATURE'")
p.write_text(s, encoding="utf-8")
p = SUPPORT / "README.md"
s = p.read_text(encoding="utf-8-sig").replace("Rev06", "Rev07")
if "muri-completamento-20260930" not in s:
    s += "\nPortanza sismica, cedimenti, Newmark e armature: [rapporto aggiornamento](artefatti/muri-completamento-20260930/CONTROLLO.md), anche PDF; [esempio salvato](artefatti/muri-completamento-20260930/interfaccia-finale/esempio-completo.anthea), relazione Word e PDF nella stessa cartella. Nessuna nuova prova MAX.\n"
p.write_text(s, encoding="utf-8")
p = SUPPORT / "SUPERATI/README.md"
s = p.read_text(encoding="utf-8-sig")
if "## Completamento muri Rev07" not in s:
    s += "\n## Completamento muri Rev07\n\nLe guide generali Rev06 e i documenti sostituiti sono conservati in muri-completamento-rev07-20260930, con struttura relativa e registro.json. La revisione corrente è Rev07, su portanza sismica, cedimenti, spostamenti e armature. I modelli e le evidenze MAX non sono stati spostati.\n"
p.write_text(s, encoding="utf-8")
print("Guide aggiornate; archiviati", len(items), "documenti")
