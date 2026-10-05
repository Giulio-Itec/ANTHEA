"""Rev21: etichette materiali e riferimento al motore nel palo orizzontale."""
from pathlib import Path
import hashlib, json, shutil, subprocess, sys

root = Path(__file__).resolve().parents[3]
support = root / 'supporto'
archive = support / 'SUPERATI/palo-engine-rev21-20261005'
paths = [support / f'docs/guida-{kind}-anthea.{ext}' for kind in ('pratica', 'teorica') for ext in ('md', 'pdf')]
paths += list((support / 'documentazione/Guide_ANTHEA').glob('*Rev20.*'))
paths += [support / name for name in ('README.md', 'README.pdf', 'installer/Indice-guide.md', 'installer/Indice-guide.pdf')]
records = []
for path in paths:
    destination = archive / path.relative_to(support)
    destination.parent.mkdir(parents=True, exist_ok=True)
    if not destination.exists():
        shutil.copy2(path, destination)
    records.append(dict(origine=str(path.relative_to(root)), archivio=str(destination.relative_to(root)),
                        motivo='Etichette materiali e rimando GPC Engine nel palo orizzontale',
                        sostituzione=str(path.relative_to(root)).replace('Rev20', 'Rev21'),
                        sha256=hashlib.sha256(destination.read_bytes()).hexdigest()))
(archive / 'registro.json').write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding='utf-8')

for kind in ('pratica', 'teorica'):
    path = support / f'docs/guida-{kind}-anthea.md'
    content = path.read_text(encoding='utf-8-sig').replace('revisione documentale 20', 'revisione documentale 21')
    if kind == 'pratica':
        content = content.replace('scegliere calcestruzzo e acciaio dalle tendine del catalogo GPC Model.',
            'scegliere i materiali dalle tendine Calcestruzzo e Acciaio. Il riferimento Motore di calcolo: GPC Engine è in alto a destra del modulo, come nella scheda della sezione in c.a., ed è visibile per entrambe le analisi.')
    elif 'Il riferimento GPC Engine nel palo orizzontale' not in content:
        marker = '### Separazione fra modello e interfaccia\n'
        content = content.replace(marker, marker + '\nIl riferimento GPC Engine nel palo orizzontale identifica il motore delle librerie di calcolo; la presentazione dei materiali usa i nomi Calcestruzzo e Acciaio. Formulazioni, parametri e risultati rimangono quelli descritti nelle sezioni teoriche del modulo.\n')
    path.write_text(content, encoding='utf-8')

for name in ('README.md', 'installer/Indice-guide.md'):
    path = support / name
    path.write_text(path.read_text(encoding='utf-8-sig').replace('Rev20', 'Rev21').replace('Revisione 20', 'Revisione 21'), encoding='utf-8')
path = support / 'scripts/Build-AntheaGuides-Itec.py'
path.write_text(path.read_text(encoding='utf-8').replace("REVISION = '20'", "REVISION = '21'").replace("REVISION_NOTE = 'ASPETTI CHIARO SCURO E MOLTO SCURO'", "REVISION_NOTE = 'PALO ETICHETTE MATERIALI E GPC ENGINE'"), encoding='utf-8')
path = support / 'scripts/Render-AntheaGuides-Itec.ps1'
path.write_text(path.read_text(encoding='utf-8-sig').replace("$Revision = '20'", "$Revision = '21'"), encoding='utf-8-sig')

subprocess.run([sys.executable, str(support / 'scripts/wiki/build-wiki-index.py')], check=True)
art = support / 'artefatti/guide_anthea_itec_rev21'
art.mkdir(parents=True, exist_ok=True)
(art / 'artifact.md').write_text('Rev21 delle guide globali sul modello ITEC: nomi materiali semplificati e riferimento GPC Engine nel modulo palo orizzontale. Conservare contenuti della Rev20, stile e struttura del modello.', encoding='utf-8')
subprocess.run([sys.executable, str(support / 'scripts/Build-AntheaGuides-Itec.py')], check=True)
for name in ('README.md', 'installer/Indice-guide.md'):
    subprocess.run([sys.executable, str(support / 'scripts/documentazione/markdown-pdf.py'), str(support / name)], check=True)
