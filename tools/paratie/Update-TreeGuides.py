"""One-time Rev56 preparation; preserve the superseded documents byte for byte."""
from pathlib import Path
import hashlib
import json
import shutil

ROOT = Path(__file__).resolve().parents[2]
ARCHIVE = ROOT / "supporto/SUPERATI/paratie-albero-rev56-20261020"
if ARCHIVE.exists():
    raise SystemExit("Archive exists: inspect before repeating this preparation.")
paths = [
    *(f"supporto/docs/guida-{kind}-anthea.{ext}" for kind in ("pratica", "teorica") for ext in ("md", "pdf")),
    *(f"supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev55.{ext}" for kind in ("pratica", "teorica") for ext in ("docx", "pdf")),
    *(f"{base}.{ext}" for base in ("supporto/README", "supporto/installer/README", "supporto/installer/Indice-guide", "supporto/documentazione/Guide_ANTHEA/README") for ext in ("md", "pdf")),
    "supporto/scripts/Build-AntheaGuides-Itec.py", "supporto/scripts/Render-AntheaGuides-Itec.ps1",
]
def write(path, text):
    path.write_bytes(text.replace("\r\n", "\n").replace("\n", "\r\n").encode("utf-8"))
records = []
for relative in paths:
    source, target = ROOT / relative, ARCHIVE / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, target)
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    assert hashlib.sha256(target.read_bytes()).hexdigest() == digest
    records.append(dict(origine=relative, sha256=digest, motivo="Sostituito dalla Rev56: albero Paratie e capsula con armature per tratti."))
write(ARCHIVE / "registro.json", json.dumps(records, ensure_ascii=False, indent=2) + "\n")
write(ARCHIVE / "README.md", "# Originali sostituiti dalla Rev56\n\n18 file conservati senza modifiche; provenienza e SHA-256 in registro.json.\n")

practice = """### Navigare nell’albero di lavoro

L’**Albero di lavoro** sul lato sinistro raccoglie i dati presenti nel foglio: parete e sezioni, terreni, sostegni e relative verifiche, casi e fasi, falda e filtrazione. I gruppi mostrano le righe effettivamente inserite. Passando il puntatore su una voce si leggono i valori e le unità dei suoi campi.

Selezionare una riga per aprirla nell’editor. Se la riga appartiene a una fase, si aggiornano anche il caso e la fase del disegno. Le fasi con lo stesso nome in casi diversi rimangono separate. Sotto ciascuna fase compaiono sostegni presenti, carichi e verifiche riferiti a quella fase. Le righe con fase mancante, inesistente o duplicata sono raccolte in **Riferimenti di fase da completare**: selezionarle per correggere i dati. Un caso citato dalle righe ma assente dalle definizioni è indicato come **non definito**.

Il pulsante **‹** richiude il pannello in una barra sottile; **›** lo riapre. Trascinare il separatore per cambiarne la larghezza. I rami si possono espandere o richiudere singolarmente; modificare o riordinare una riga conserva il ramo e la sua apertura. L’albero si aggiorna anche dopo aggiunte, eliminazioni e riapertura dei dati. La navigazione non modifica gli input né avvia il calcolo.

I nodi **Falda e filtrazione** e **Ricerca dell’infissione** aprono i rispettivi parametri. Il pannello è disponibile anche nelle altre schede del modulo e nei documenti in sola lettura; in questi ultimi gli editor mantengono il blocco delle modifiche.

"""
path = ROOT / "supporto/docs/guida-pratica-anthea.md"
text = path.read_text(encoding="utf-8").replace("revisione documentale 55", "revisione documentale 56")
assert text.count("### Definire il modello e le unità") == 1
text = text.replace("### Definire il modello e le unità", practice + "### Definire il modello e le unità")
text = text.replace("| Capsula | Due semicirconferenze unite da tratti rettilinei; barre radiali rispetto al centro |",
    "| Capsula | Numero e diametro distinti per i due archi e per i due rettilinei; disponibile anche la disposizione radiale precedente |")
old = "I dettagli automatici e la ricerca automatica delle armature delle sezioni usuali non vengono applicati a queste disposizioni."
assert text.count(old) == 1
text = text.replace(old, "Per la capsula impostata come **Pilastro** sono disponibili i dettagli generali NTC, con interasse misurato lungo il contorno delle barre. Per le altre tipologie avanzate i dettagli automatici restano fuori campo; la ricerca automatica delle armature non si applica alle disposizioni avanzate.")
marker = "### Input geometria e unità"
assert text.count(marker) == 1
text = text.replace(marker, """### Armature della capsula

Scegliere **Curve e rettilinei separati**. Il numero per semicirconferenza comprende le due barre agli estremi di tangenza; il numero per rettilineo comprende soltanto le barre interne. I conteggi valgono per ciascuno dei due archi e per ciascuno dei due rettilinei: 17 barre per arco e 8 interne per rettilineo producono 50 barre complessive. Diametri delle barre curve e rettilinee sono indipendenti. Le barre curve sono radiali rispetto al centro della rispettiva semicirconferenza.

La dimensione maggiore orienta la capsula: b = 5000 mm e h = 2500 mm danno due semicirconferenze laterali e due rettilinei orizzontali. Per b = h usare la sezione circolare. Le capsule già salvate conservano **Radiale dal centro (precedente)**; aprire il wizard non cambia automaticamente le loro armature. Il passaggio alla nuova disposizione richiede la scelta del modo e **Applica sezione**.

Nella scheda dei dettagli selezionare **Pilastro**. Sono disponibili i controlli generali su armatura longitudinale, interassi, copriferro e staffe; una barra spostata manualmente fuori dal percorso lascia l'interasse da verificare, mantenendo gli altri controlli. La staffa perimetrale disegnata non conferma il trattenimento delle barre: progettare e controllare anche le legature. Il confinamento sismico specifico della pila non è verificato da questi controlli generali. Le grandezze resistenti e i bracci delle staffe per il taglio restano da assegnare nelle due direzioni.

""" + marker)
write(path, text)
path = ROOT / "supporto/docs/guida-teorica-anthea.md"
text = path.read_text(encoding="utf-8").replace("revisione documentale 55", "revisione documentale 56")
text = text.replace("Ellisse, capsula e anelli cavi usano", "Ellisse, capsula nella disposizione radiale precedente e anelli cavi usano")
text = text.replace("dettagli automatici e ottimizzazione delle armature restano fuori campo.",
    "l'ottimizzazione delle armature resta fuori campo. Per la capsula come pilastro sono disponibili i controlli generali descritti di seguito.")
marker = "Il modello nativo salvato conserva materiali, geometria e armature discrete."
assert text.count(marker) == 1
text = text.replace(marker, """La capsula con armature separate usa n_c barre per ciascuna semicirconferenza, estremi di tangenza inclusi, e n_r barre interne per ciascun rettilineo. Il totale è 2n_c + 2n_r. Gli angoli delle barre curve sono equidistanti rispetto al centro del relativo arco; gli assi intersecano il contorno arretrato in funzione del diametro delle barre curve. Per i rettilinei, lunghi L = |b − h|, le ascisse interne sono suddivise in n_r + 1 intervalli; l'ordinata rispetta il copriferro e il diametro delle barre rettilinee. Diametri diversi possono quindi produrre piccoli raccordi tra gli assi delle barre nelle zone di tangenza.

Nei dettagli del pilastro l'interasse massimo viene misurato mediante le ascisse curvilinee delle barre su un percorso chiuso. Si considerano tutti gli intervalli consecutivi, incluso quello di chiusura; i tratti curvi seguono la discretizzazione poligonale. Non si collegano come barre adiacenti quelle appartenenti ai lati opposti. Il limite NTC 2018 §4.1.6.1.2 e gli altri controlli generali già implementati restano invariati. Barre manuali fuori dal percorso non consentono il riconoscimento automatico dell'interasse. Il metodo non dimostra trattenimento con legature, ancoraggi o confinamento sismico delle pile: queste verifiche sono distinte.

""" + marker)
write(path, text)
for relative in ("supporto/README.md", "supporto/installer/README.md", "supporto/installer/Indice-guide.md", "supporto/documentazione/Guide_ANTHEA/README.md"):
    path = ROOT / relative
    text = path.read_text(encoding="utf-8")
    if relative.endswith("Guide_ANTHEA/README.md"):
        before, after = text.split("La Rev55 aggiunge", 1)
        text = before.replace("Rev55", "Rev56") + "La Rev56 documenta l’albero di lavoro richiudibile di Paratie e le armature separate della capsula, con dettagli generali del pilastro e limiti del riconoscimento automatico. Gli originali Rev55 sono conservati in `supporto/SUPERATI/paratie-albero-rev56-20261020/`.\n\nLa Rev55 aggiunge" + after
    else:
        text = text.replace("Rev55", "Rev56").replace("Revisione 55", "Revisione 56")
    write(path, text)
path = ROOT / "supporto/scripts/Build-AntheaGuides-Itec.py"
write(path, path.read_text(encoding="utf-8").replace("REVISION = '55'", "REVISION = '56'").replace("ESEMPI PER TUTTI I MODULI", "ALBERO PARATIE E ARMATURE CAPSULA"))
path = ROOT / "supporto/scripts/Render-AntheaGuides-Itec.ps1"
write(path, path.read_text(encoding="utf-8").replace("$Revision = '55'", "$Revision = '56'"))
# Originals are archived and verified above; remove only the four superseded current editions.
for kind in ("pratica", "teorica"):
    for ext in ("docx", "pdf"):
        path = ROOT / f"supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev55.{ext}"
        assert path.read_bytes() == (ARCHIVE / path.relative_to(ROOT)).read_bytes()
        path.unlink()
art = ROOT / "supporto/artefatti/guide_anthea_itec_rev56"
art.mkdir(parents=True, exist_ok=True)
write(art / "artifact.md", "# Guide ANTHEA Rev56\n\nAggiornamento dei due volumi globali: albero di lavoro Paratie e armature della capsula per tratti con dettagli del pilastro. Conservare tutte le parti del modello ITEC e verificare PDF e indici.\n")
print("Prepared Rev56; preserved", len(records), "predecessors.")
