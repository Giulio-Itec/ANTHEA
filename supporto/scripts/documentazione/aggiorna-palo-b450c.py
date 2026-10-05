"""Rev22: acciaio B450C predefinito nei nuovi fogli del palo."""
from pathlib import Path
import hashlib, json, shutil, subprocess, sys

root = Path(__file__).resolve().parents[3]
support = root / 'supporto'
archive = support / 'SUPERATI/palo-b450c-rev22-20261005'
paths = [support / f'docs/guida-{kind}-anthea.{ext}' for kind in ('pratica', 'teorica') for ext in ('md', 'pdf')]
paths += list((support / 'documentazione/Guide_ANTHEA').glob('*Rev21.*'))
paths += [support / name for name in ('README.md', 'README.pdf', 'installer/Indice-guide.md', 'installer/Indice-guide.pdf')]
records = []
for path in paths:
    destination = archive / path.relative_to(support)
    destination.parent.mkdir(parents=True, exist_ok=True)
    if not destination.exists():
        shutil.copy2(path, destination)
    records.append(dict(origine=str(path.relative_to(root)), archivio=str(destination.relative_to(root)),
                        motivo='Acciaio B450C predefinito e distinta ferri visibile nel palo',
                        sostituzione=str(path.relative_to(root)).replace('Rev21', 'Rev22'),
                        sha256=hashlib.sha256(destination.read_bytes()).hexdigest()))
(archive / 'registro.json').write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding='utf-8')

for kind in ('pratica', 'teorica'):
    path = support / f'docs/guida-{kind}-anthea.md'
    content = path.read_text(encoding='utf-8-sig').replace('revisione documentale 21', 'revisione documentale 22')
    if kind == 'pratica':
        marker = 'scegliere i materiali dalle tendine Calcestruzzo e Acciaio.'
        content = content if 'Nei nuovi fogli del palo è preselezionato B450C' in content else content.replace(marker, marker + ' Nei nuovi fogli del palo è preselezionato B450C con tutte le proprietà del catalogo, come nel modulo della sezione in c.a.; i materiali personalizzati degli archivi esistenti rimangono conservati.')
    else:
        marker = 'Il riferimento GPC Engine nel palo orizzontale identifica il motore delle librerie di calcolo;'
        content = content if 'I nuovi fogli del palo inizializzano' in content else content.replace(marker, 'I nuovi fogli del palo inizializzano l’acciaio B450C dal catalogo comune alla sezione in c.a., includendo l’intero legame costitutivo. La riapertura di archivi precedenti conserva invece i parametri salvati: la sola resistenza fyk di 450 MPa non identifica automaticamente tutte le proprietà di B450C.\n\n' + marker)
    if kind == 'pratica' and 'Il pulsante Distinta ferri di ogni tratto' not in content:
        target = 'La distinta di ciascun tratto indica'
        content = content.replace(target, 'Il pulsante Distinta ferri di ogni tratto apre la distinta e la seleziona nel riepilogo a destra. Barre e staffe sono visibili inizialmente; la scelta di richiudere il pannello viene conservata durante il ricalcolo. I testi si aggiornano anche mantenendo il cursore in un campo. Quando la distinta non può essere prodotta, la vista delle armature e il tratto mostrano il motivo specifico, senza un riquadro bianco o un’attesa permanente. ' + target)
    path.write_text(content, encoding='utf-8')

for name in ('README.md', 'installer/Indice-guide.md'):
    path = support / name
    path.write_text(path.read_text(encoding='utf-8-sig').replace('Rev21', 'Rev22').replace('Revisione 21', 'Revisione 22'), encoding='utf-8')
path = support / 'scripts/Build-AntheaGuides-Itec.py'
path.write_text(path.read_text(encoding='utf-8').replace("REVISION = '21'", "REVISION = '22'").replace("REVISION_NOTE = 'PALO ETICHETTE MATERIALI E GPC ENGINE'", "REVISION_NOTE = 'ACCIAIO B450C E DISTINTA FERRI DEL PALO'").replace("REVISION_NOTE = 'ACCIAIO B450C PREDEFINITO NEL PALO'", "REVISION_NOTE = 'ACCIAIO B450C E DISTINTA FERRI DEL PALO'"), encoding='utf-8')
path = support / 'scripts/Render-AntheaGuides-Itec.ps1'
path.write_text(path.read_text(encoding='utf-8-sig').replace("$Revision = '21'", "$Revision = '22'"), encoding='utf-8-sig')

subprocess.run([sys.executable, str(support / 'scripts/wiki/build-wiki-index.py')], check=True)
art = support / 'artefatti/guide_anthea_itec_rev22'
art.mkdir(parents=True, exist_ok=True)
(art / 'artifact.md').write_text('Rev22 delle guide globali sul modello ITEC: acciaio B450C predefinito nel modulo palo orizzontale. Conservare contenuti della Rev21, stile e struttura del modello.', encoding='utf-8')
subprocess.run([sys.executable, str(support / 'scripts/Build-AntheaGuides-Itec.py')], check=True)
for name in ('README.md', 'installer/Indice-guide.md'):
    subprocess.run([sys.executable, str(support / 'scripts/documentazione/markdown-pdf.py'), str(support / name)], check=True)
