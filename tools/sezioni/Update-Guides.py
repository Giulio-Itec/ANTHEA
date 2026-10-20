"""Prepare the combined section-persistence / template revision, preserving its predecessors."""
from pathlib import Path
import hashlib
import json
import shutil

ROOT = Path(__file__).resolve().parents[2]
ARCHIVE = ROOT / "supporto/SUPERATI/sezioni-rev54-20261020"
ART = ROOT / "supporto/artefatti/guide_anthea_itec_rev54"

paths = [
    *(f"supporto/docs/guida-{kind}-anthea.{ext}" for kind in ("pratica", "teorica") for ext in ("md", "pdf")),
    *(f"supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev53.{ext}" for kind in ("pratica", "teorica") for ext in ("docx", "pdf")),
    *(f"{base}.{ext}" for base in ("supporto/README", "supporto/installer/README", "supporto/installer/Indice-guide", "supporto/documentazione/Guide_ANTHEA/README") for ext in ("md", "pdf")),
    "supporto/scripts/Build-AntheaGuides-Itec.py", "supporto/scripts/Render-AntheaGuides-Itec.ps1",
]

def write(path, text):
    path.write_bytes(text.replace("\r\n", "\n").replace("\n", "\r\n").encode("utf-8"))

if ARCHIVE.exists():
    raise SystemExit("Archive already exists; inspect before repeating this one-time preparation.")
manifest = []
for relative in paths:
    source, target = ROOT / relative, ARCHIVE / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, target)
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    assert hashlib.sha256(target.read_bytes()).hexdigest() == digest
    manifest.append({"origine": relative, "sha256": digest, "motivo": "Sostituzione con Rev54: persistenza e wizard sezioni c.a."})
write(ARCHIVE / "registro.json", json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
write(ARCHIVE / "README.md", "# Originali sostituiti dalla Rev54\n\n18 file conservati senza modifiche. Origine, motivo e SHA-256 in `registro.json`.\n")
ART.mkdir(parents=True, exist_ok=True)
write(ART / "artifact.md", "# Guide ANTHEA Rev54\n\nModifica delle guide globali esistenti sul modello ITEC invariato. Aggiunte: persistenza nativa GPC e dodici tipologie di sezioni, armature longitudinali e schemi di staffatura con limiti espliciti. Preservare formule, immagini, stili, intestazioni e parti opache. Due DOCX e relativi PDF, indici aggiornati; controllo visivo delle pagine variate e audit dell'intero documento.\n")

practice = """### Salvataggio e riapertura della sezione

Il documento conserva il modello della sezione con geometria, materiali, barre e trefoli, insieme ai parametri necessari a modificarlo. Le combinazioni e le impostazioni delle verifiche rimangono nel foglio; le mesh e i risultati temporanei vengono ricalcolati. Una modifica della ricetta geometrica o dei trefoli richiede l'aggiornamento del modello.

I documenti precedenti restano apribili e vengono convertiti al nuovo formato al salvataggio. Conserva una copia se devi riaprirli con una versione precedente di ANTHEA. I dati incompleti si salvano come bozza: il foglio mantiene i valori digitati e il motivo per cui non può ancora costruire una sezione valida. Anche le revisioni storiche conservano la propria sezione.

### Wizard delle tipologie e delle armature

Nel pannello di controllo apri **Wizard tipologie / modifica disposizione…**. Scegli la tipologia, assegna dimensioni e armature e controlla l'anteprima affiancata. **Applica sezione** valida anche la compatibilità con i trefoli già presenti nel foglio. **Annulla** lascia invariati i dati. Per modificare una disposizione tipologica riapri lo stesso wizard; le coordinate delle barre restano modificabili nella tabella.

| Tipologia | Disposizione iniziale |
| --- | --- |
| Rettangolare | Due file superiori e due inferiori; numero, diametro e interferro delle seconde file indipendenti |
| Rettangolare cava | Barre sui contorni esterno e interno; staffe nelle quattro pareti |
| Circolare | Due anelli con numero, diametro e interferro radiale assegnati |
| Circolare cava | Anello esterno e interno rispetto al foro; due staffe circolari |
| A T | Barre superiori, inferiori e laterali; staffe di anima e ala |
| A I | Ali superiore e inferiore anche diverse; armatura perimetrale e staffe distinte di anima e ali |
| Ellittica | Barre a incrementi angolari uguali, sul contorno interno utile |
| Capsula | Due semicirconferenze unite da tratti rettilinei; barre radiali rispetto al centro |
| A L | Due bracci ortogonali; armatura perimetrale e staffe dei singoli bracci |
| A C | Canale aperto, anima e due ali sullo stesso lato |
| Trapezoidale | Basi parallele centrate; barre perimetrali e staffa aderente |
| Poligonale regolare | Da 3 a 32 lati; diametro circoscritto, barre perimetrali e staffa aderente |

Per le armature perimetrali assegna il passo massimo tra gli assi; il generatore inserisce le barre agli spigoli e suddivide i lati. Per le disposizioni radiali assegna il numero di barre. Il copriferro è netto, misurato alla superficie esterna della staffa. Il disegno verifica la collocazione geometrica; i tratti di staffa sono schematici e non rappresentano ganci e raggi di piegatura esecutivi.

Le nuove tipologie e le disposizioni cave a doppio contorno richiedono **parametri manuali del taglio**: assegnare bw, d e, quando richiesto, Asl, oltre ai bracci efficaci nelle due direzioni. Il wizard svuota i valori della sezione precedente. Ripartizione del taglio tra pareti, ancoraggio e trattenimento delle barre vanno verificati esplicitamente. I dettagli automatici e la ricerca automatica delle armature delle sezioni usuali non vengono applicati a queste disposizioni. Il modello torsionale automatico rimane limitato a rettangolo e cerchio; un profilo aperto richiede un modello dedicato.

"""
theory = """### Geometrie tipologiche e costruzione delle armature

I wizard aggiungono sezioni a I, L e C aperta, ellittiche, a capsula, trapezoidali e poligonali regolari, oltre alle disposizioni a doppia fila o doppio anello e ai due contorni delle sezioni cave. Per i profili aperti il calcestruzzo segue il contorno reale: l'inviluppo rettangolare non è una regione resistente. L'origine delle nuove geometrie coincide con il baricentro del calcestruzzo.

L'asse delle barre viene costruito sul contorno arretrato di copriferro netto, diametro della staffa e raggio della barra. Presso un foro l'arretramento è diretto verso il calcestruzzo. Sugli spigoli vengono mantenute barre di vertice, mentre ogni lato viene suddiviso rispettando il passo massimo assegnato. Ellisse, capsula e anelli cavi usano intersezioni tra raggi a incrementi angolari uguali e contorno utile: l'interasse lungo la curva non è necessariamente costante.

La capsula è composta da due semicirconferenze e due tratti rettilinei, con estremità lungo la dimensione maggiore. La sezione a C è aperta, con anima e ali dallo stesso lato; la I ammette larghezze e spessori diversi delle due ali. Il poligono regolare è definito mediante numero di lati e diametro circoscritto. Le curve vengono discretizzate in poligoni dal modello geometrico; la verifica della discretizzazione resta distinta da quella degli equilibri della sezione.

Le staffe seguono percorsi contenuti nel calcestruzzo. Nei profili composti da pareti rettangolari vengono proposti anelli distinti per anima e ali o per le quattro pareti cave. Il controllo geometrico del copriferro non dimostra il corretto ancoraggio, la piegatura, il trattenimento di ogni barra o la ripartizione del taglio. I percorsi disegnati non determinano automaticamente i bracci efficaci del modello resistente. Per le nuove disposizioni tali quantità e i parametri bw, d e Asl devono essere assegnati e giustificati; dettagli automatici e ottimizzazione delle armature restano fuori campo.

Il modello nativo salvato conserva materiali, geometria e armature discrete. La ricetta editabile e gli input delle verifiche restano separati. Le mesh non sono archiviate come risultati permanenti: vengono ricostruite dal motore. Una bozza incompleta non contiene una precedente sezione valida, che potrebbe risultare incoerente con gli input mostrati.

"""
for kind, addition, anchor in (
    ("pratica", practice, "### Input geometria e unità"),
    ("teorica", theory, "### Geometria e deformazioni piane"),
):
    path = ROOT / f"supporto/docs/guida-{kind}-anthea.md"
    text = path.read_text(encoding="utf-8").replace("revisione documentale 53", "revisione documentale 54")
    assert text.count(anchor) == 1
    text = text.replace(anchor, addition + anchor)
    if kind == "pratica":
        text = text.replace("Circolare, Rettangolare, A T; la sezione generica richiede un contorno definito", "Sezioni usuali e dodici tipologie da wizard; contorno e armatura devono essere compatibili")
    else:
        text = text.replace("Sono disponibili sezioni rettangolari, circolari e a T, con fori centrali per le forme supportate.", "Sono disponibili le sezioni usuali e le tipologie guidate descritte sopra, con fori centrali per le forme supportate.")
    write(path, text)

for relative in ("supporto/README.md", "supporto/installer/README.md", "supporto/installer/Indice-guide.md", "supporto/documentazione/Guide_ANTHEA/README.md"):
    path = ROOT / relative
    text = path.read_text(encoding="utf-8")
    if relative.endswith("Guide_ANTHEA/README.md"):
        before, after = text.split("La Rev53 aggiunge", 1)
        text = before.replace("Rev53", "Rev54") + "La Rev54 integra salvataggio nativo GPC delle sezioni e dodici tipologie da wizard, con limiti delle staffature e del taglio espliciti. Gli originali Rev53 sono in `supporto/SUPERATI/sezioni-rev54-20261020/`.\n\nLa Rev53 aggiunge" + after
    else:
        text = text.replace("Rev53", "Rev54").replace("Revisione 53", "Revisione 54")
    write(path, text)
path = ROOT / "supporto/scripts/Build-AntheaGuides-Itec.py"
write(path, path.read_text(encoding="utf-8").replace("REVISION = '53'", "REVISION = '54'").replace("MODELLAZIONE GRAFICA DELLE PARATIE", "SEZIONI: SALVATAGGIO E WIZARD"))
path = ROOT / "supporto/scripts/Render-AntheaGuides-Itec.ps1"
write(path, path.read_text(encoding="utf-8").replace("$Revision = '53'", "$Revision = '54'"))
print("Prepared Rev54; preserved and hashed", len(manifest), "predecessors.")
