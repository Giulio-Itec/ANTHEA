"""Rev23: tavola del palo con barre laterali quotate e distinta collegata."""
from pathlib import Path
import hashlib, json, shutil, subprocess, sys

root = Path(__file__).resolve().parents[3]
support = root / 'supporto'
archive = support / 'SUPERATI/palo-tavola-rev23-20261005'
paths = [support / f'docs/guida-{kind}-anthea.{ext}' for kind in ('pratica', 'teorica') for ext in ('md', 'pdf')]
paths += list((support / 'documentazione/Guide_ANTHEA').glob('*Rev22.*'))
paths += [support / name for name in ('README.md', 'README.pdf', 'installer/Indice-guide.md', 'installer/Indice-guide.pdf')]
records = []
for path in paths:
    destination = archive / path.relative_to(support)
    destination.parent.mkdir(parents=True, exist_ok=True)
    if not destination.exists():
        shutil.copy2(path, destination)
    records.append(dict(origine=str(path.relative_to(root)), archivio=str(destination.relative_to(root)),
                        motivo='Tavola armature in elevazione con barre laterali quotate',
                        sostituzione=str(path.relative_to(root)).replace('Rev22', 'Rev23'),
                        sha256=hashlib.sha256(destination.read_bytes()).hexdigest()))
(archive / 'registro.json').write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding='utf-8')
for kind in ('pratica', 'teorica'):
    path = support / f'docs/guida-{kind}-anthea.md'
    content = path.read_text(encoding='utf-8-sig').replace('revisione documentale 22', 'revisione documentale 23')
    if kind == 'pratica' and 'Apri tavola armature' not in content:
        marker = 'La vista finale deriva dai risultati:'
        content = content.replace(marker, 'Tavola armature e distinta ferri presenta un solo palo in elevazione a sinistra, le singole barre sviluppate lateralmente con marche B01, B02 e successive al centro e le sezioni trasversali dei tratti a destra. Tutte le quote si riferiscono alla testa e condividono la scala verticale; gli sviluppi esterni al modello restano visibili. I richiami S01, S02 e successivi identificano le zone di staffatura e le righe della distinta. La tabella inferiore collega marche, tratti, quantità, diametri, quote, lunghezze di taglio, barre commerciali e giunti. I gruppi longitudinali continui sono riportati una sola volta. Apri tavola armature apre la vista ingrandita con zoom ed esportazione PNG.\n\n' + marker)
    elif kind == 'teorica' and 'La tavola delle armature è una proiezione' not in content:
        marker = '### Separazione fra modello e interfaccia\n'
        content = content.replace(marker, marker + '\nLa tavola delle armature è una proiezione dei pezzi longitudinali, delle zone di staffatura e delle sezioni restituiti da Checker. Marche e coordinate grafiche non alterano quantità, quote o resistenze. Il modello attuale fornisce barre longitudinali rettilinee e posizioni nominali delle staffe: il disegno non aggiunge ganci o sagomature non calcolati. Le lunghezze di taglio delle staffe restano da definire e i dettagli costruttivi non completati sono segnalati. Le sezioni trasversali rappresentano l’armatura nominale del tratto, senza attribuire resistenza aggiuntiva alle barre sovrapposte.\n')
    if kind == 'pratica' and 'palo-12m-quattro-tratti.programma' not in content:
        content = content.replace('Salvataggio, JSON, CSV e report conservano parametri e origine, modello di N', '\nL’esempio apribile [Palo 12 m con quattro tratti](../esempi/palo-orizzontale-armature/palo-12m-quattro-tratti.programma) usa C35/45 e B450C, due strati granulari, quattro tratti e due gruppi longitudinali. Il [riepilogo degli input](../esempi/palo-orizzontale-armature/palo-12m-quattro-tratti.pdf) descrive dati assegnati, risultati e dettagli da completare.\n' + '\nSalvataggio, JSON, CSV e report conservano parametri e origine, modello di N')
    path.write_text(content, encoding='utf-8')
for name in ('README.md', 'installer/Indice-guide.md'):
    path = support / name
    path.write_text(path.read_text(encoding='utf-8-sig').replace('Rev22', 'Rev23').replace('Revisione 22', 'Revisione 23'), encoding='utf-8')
path = support / 'scripts/Build-AntheaGuides-Itec.py'
path.write_text(path.read_text(encoding='utf-8').replace("REVISION = '22'", "REVISION = '23'").replace("REVISION_NOTE = 'ACCIAIO B450C E DISTINTA FERRI DEL PALO'", "REVISION_NOTE = 'TAVOLA ARMATURE DEL PALO E BARRE LATERALI'"), encoding='utf-8')
path = support / 'scripts/Render-AntheaGuides-Itec.ps1'
path.write_text(path.read_text(encoding='utf-8-sig').replace("$Revision = '22'", "$Revision = '23'"), encoding='utf-8-sig')
subprocess.run([sys.executable, str(support / 'scripts/wiki/build-wiki-index.py')], check=True)
art = support / 'artefatti/guide_anthea_itec_rev23'
art.mkdir(parents=True, exist_ok=True)
(art / 'artifact.md').write_text('Rev23 delle due guide globali sul modello ITEC. Aggiornare la tavola delle armature del palo: elevazione, barre laterali quotate, sezioni e distinta. Conservare la documentazione precedente e il suo stile.', encoding='utf-8')
subprocess.run([sys.executable, str(support / 'scripts/Build-AntheaGuides-Itec.py')], check=True)
for name in ('README.md', 'installer/Indice-guide.md'):
    subprocess.run([sys.executable, str(support / 'scripts/documentazione/markdown-pdf.py'), str(support / name)], check=True)
