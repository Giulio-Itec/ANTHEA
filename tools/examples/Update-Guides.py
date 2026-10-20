"""One-time Rev55 preparation; preserve the superseded documents byte for byte."""
from pathlib import Path
import hashlib
import json
import shutil

ROOT = Path(__file__).resolve().parents[2]
ARCHIVE = ROOT / "supporto/SUPERATI/esempi-rev55-20261020"
if ARCHIVE.exists():
    raise SystemExit("Archive exists: inspect before repeating this preparation.")
paths = [
    *(f"supporto/docs/guida-{kind}-anthea.{ext}" for kind in ("pratica", "teorica") for ext in ("md", "pdf")),
    *(f"supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev54.{ext}" for kind in ("pratica", "teorica") for ext in ("docx", "pdf")),
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
    records.append(dict(origine=relative, sha256=digest, motivo="Sostituito dalla Rev55: catalogo esempi e distribuzione."))
write(ARCHIVE / "registro.json", json.dumps(records, ensure_ascii=False, indent=2) + "\n")
write(ARCHIVE / "README.md", "# Originali sostituiti dalla Rev55\n\n18 file conservati senza modifiche; provenienza e SHA-256 in registro.json.\n")

practice = """### Esempi organizzati per modulo

Aprire **Esempi** dalla barra laterale oppure **File → Apri esempio…**. La colonna sinistra raggruppa i file per modulo; quella destra mostra i casi disponibili. Selezionare un caso e leggere la descrizione prima di premere **Apri una copia**. Il catalogo comprende almeno due casi per ognuno dei quindici moduli, tre casi Spettri con isolamento e pendolo, nove esempi di paratie e dodici modelli geometrici da wizard per le sezioni in calcestruzzo armato.

Il documento aperto è una copia indipendente senza percorso di salvataggio. **Salva** richiede una destinazione nella cartella dei propri lavori. Anche il doppio clic su un esempio installato apre una copia; il programma impedisce di salvare dentro la cartella degli esempi distribuiti. Prima di sostituire un documento modificato viene chiesta la conferma consueta.

Gli esempi di calcolo illustrano gli input e i risultati del relativo modulo: un controllo non soddisfatto resta visibile e non viene trasformato in un esito positivo. Le stratigrafie sono esempi di dati. I casi **Wizard** sono modelli geometrici con barre e staffe schematiche: completare le azioni, i parametri manuali del taglio e le verifiche dei dettagli secondo le indicazioni del capitolo sulle sezioni. Non sono progetti esecutivi.

Nell’installer il componente **Esempi per tutti i moduli** è selezionato inizialmente e può essere escluso. I file sono raccolti nella cartella **Esempi**, suddivisi per modulo, accanto al programma; il menu Start contiene il collegamento alla cartella. Se il componente è assente, la pagina indica di rieseguire l’installer selezionandolo. Gli esempi e le due guide sono utilizzabili senza connessione.

"""
path = ROOT / "supporto/docs/guida-pratica-anthea.md"
text = path.read_text(encoding="utf-8").replace("revisione documentale 54", "revisione documentale 55")
assert text.count("### Leggere e compilare i campi") == 1
write(path, text.replace("### Leggere e compilare i campi", practice + "### Leggere e compilare i campi"))
path = ROOT / "supporto/docs/guida-teorica-anthea.md"
write(path, path.read_text(encoding="utf-8").replace("revisione documentale 54", "revisione documentale 55"))
for relative in ("supporto/README.md", "supporto/installer/README.md", "supporto/installer/Indice-guide.md", "supporto/documentazione/Guide_ANTHEA/README.md"):
    path = ROOT / relative
    text = path.read_text(encoding="utf-8")
    if relative.endswith("Guide_ANTHEA/README.md"):
        before, after = text.split("La Rev54 integra", 1)
        text = before.replace("Rev54", "Rev55") + "La Rev55 aggiunge il catalogo Esempi, l’apertura come copia e il componente dell’installer. La teoria resta invariata. Gli originali Rev54 sono conservati in `supporto/SUPERATI/esempi-rev55-20261020/`.\n\nLa Rev54 integra" + after
    else:
        text = text.replace("Rev54", "Rev55").replace("Revisione 54", "Revisione 55")
    if relative == "supporto/installer/README.md":
        text = text.replace("Componenti selezionabili: collegamento", "Componenti selezionabili: esempi per tutti i moduli (selezionati inizialmente), collegamento")
        text = text.replace("aggiornando comunque guide e collegamenti", "aggiornando comunque guide, esempi e collegamenti")
        text = text.replace("## Documentazione distribuita", """## Esempi distribuiti

Il componente **Esempi per tutti i moduli** installa 52 file nella cartella `Esempi`, suddivisi per modulo, e il collegamento Esempi nel menu Start. Dentro ANTHEA si consultano dalla pagina **Esempi** e si aprono come copie senza percorso; il primo salvataggio richiede una destinazione personale. Il componente è facoltativo, selezionato per impostazione iniziale.

La fonte unica è `supporto/esempi/distribuzione/catalogo.json`, con percorso, modulo, descrizione, provenienza e SHA-256 di ciascun file. `Esempi.ps1` verifica integrità e percorsi prima della build e genera elenchi espliciti per installazione e rimozione. I file sono versionati insieme al catalogo. I casi geometrici da wizard indicano nella descrizione le verifiche ancora da completare.

`tests/Installer/Test-Examples.ps1` collauda la sezione reale NSIS in una destinazione isolata, inclusi collegamento, integrità e rimozione che conserva i file personali. Il runner lo esegue con `-Installer`.

## Documentazione distribuita""")
    write(path, text)
path = ROOT / "supporto/scripts/Build-AntheaGuides-Itec.py"
write(path, path.read_text(encoding="utf-8").replace("REVISION = '54'", "REVISION = '55'").replace("SEZIONI: SALVATAGGIO E WIZARD", "ESEMPI PER TUTTI I MODULI"))
path = ROOT / "supporto/scripts/Render-AntheaGuides-Itec.ps1"
write(path, path.read_text(encoding="utf-8").replace("$Revision = '54'", "$Revision = '55'"))
# Originals are archived and verified above; remove only the four superseded current editions.
for kind in ("pratica", "teorica"):
    for ext in ("docx", "pdf"):
        path = ROOT / f"supporto/documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev54.{ext}"
        assert path.read_bytes() == (ARCHIVE / path.relative_to(ROOT)).read_bytes()
        path.unlink()
art = ROOT / "supporto/artefatti/guide_anthea_itec_rev55"
art.mkdir(parents=True, exist_ok=True)
write(art / "artifact.md", "# Guide ANTHEA Rev55\n\nAggiornamento dei due volumi globali: catalogo esempi, copie di lavoro e componente installer. Teoria e formule invariate. Conservare tutte le parti del modello ITEC e verificare PDF e indici.\n")
print("Prepared Rev55; preserved", len(records), "predecessors.")
