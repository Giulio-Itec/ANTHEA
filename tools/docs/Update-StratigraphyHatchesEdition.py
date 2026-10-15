"""Preserve the preceding Rev46 draft and document descriptive soil families."""
from pathlib import Path
import hashlib
import json
import shutil

ROOT = Path(__file__).resolve().parents[2]
ARCH = ROOT / 'supporto/SUPERATI/stratigrafia-rev46-prima-retini-20261015'
paths = [ROOT / f'supporto/docs/guida-{kind}-anthea.{ext}' for kind in ('pratica', 'teorica') for ext in ('md', 'pdf')]
paths += list((ROOT / 'supporto/documentazione/Guide_ANTHEA').glob('*Rev46.*'))
entries = []
for source in paths:
    target = ARCH / source.relative_to(ROOT)
    if not target.exists():
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    entries.append({'origine': source.relative_to(ROOT).as_posix(), 'archivio': target.relative_to(ROOT).as_posix(),
                    'sha256': hashlib.sha256(target.read_bytes()).hexdigest()})
(ARCH / 'registro.json').write_text(json.dumps({'motivo': 'Bozza Rev46 sostituita dalla Rev46 con famiglie di terreno e retini, prima del rilascio', 'files': entries}, ensure_ascii=False, indent=2), encoding='utf-8')
practical = '''### Famiglie di terreno e retini

Il menu Famiglia prevalente propone Non classificato, Riporto, Ghiaia, Sabbia, Limo, Argilla, Terreno organico, Roccia e Altro. Per terreni misti scegliere la componente prevalente e conservare la denominazione completa: ad esempio Sabbia per una sabbia limosa. Il colore resta modificabile separatamente.

Il profilo usa punti per Sabbia, ciottoli per Ghiaia, tratti orizzontali brevi per Limo e continui per Argilla, crocette per Riporto, simboli vegetali per Terreno organico, tratti diagonali per Roccia e piccoli segni a più per Altro. Non classificato conserva il solo colore. I retini distinguono le famiglie anche senza colore e mantengono dimensione costante sullo schermo. Il nome della famiglia appare accanto allo strato e nella scheda dati. Sono convenzioni grafiche di ANTHEA: scegliere una famiglia non assegna parametri, falda o spessore.

L’importatore AI propone la famiglia solo quando la componente prevalente è riconoscibile nella descrizione della relazione, lasciando Non classificato nei casi incerti. Il menu permette di correggere la proposta in anteprima; non è una classificazione granulometrica automatica.

'''
theory = '''### Significato delle famiglie grafiche

La famiglia prevalente è un attributo descrittivo dello strato, usato per il retino e per riconoscere il materiale nel profilo. Le famiglie ammesse comprendono riporto, ghiaia, sabbia, limo, argilla, terreno organico e roccia, oltre ai casi non classificato e altro. La descrizione libera conserva componenti secondarie, stato, alterazione e altre particolarità. Una sabbia limosa può quindi usare il retino Sabbia senza perdere la descrizione della miscela.

I retini sono convenzioni grafiche proprie di ANTHEA; non rappresentano una classificazione granulometrica normativa e non sostituiscono prove o criteri di classificazione. Il loro passo è espresso in dimensioni di visualizzazione e non descrive la dimensione reale dei grani o delle discontinuità. Cambiare famiglia o colore non modifica alcun parametro geotecnico.

'''
for kind, addition, anchor in [('pratica', practical, '### Valori medi e minimi'), ('teorica', theory, '### Insiemi di parametri')]:
    path = ROOT / f'supporto/docs/guida-{kind}-anthea.md'
    text = path.read_text(encoding='utf-8')
    if addition.splitlines()[0] not in text:
        text = text.replace(anchor, addition + anchor, 1)
    path.write_text(text, encoding='utf-8')
print('Guide aggiornate con famiglie e retini; bozza precedente conservata.')
