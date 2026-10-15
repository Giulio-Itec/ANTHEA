"""Preserve Rev45 and prepare Rev46 with the existing global guide pipeline."""
from pathlib import Path
import hashlib
import json
import shutil

ROOT = Path(__file__).resolve().parents[2]
ARCH = ROOT / 'supporto/SUPERATI/stratigrafia-rev46-20261015'
ART = ROOT / 'supporto/artefatti/guide_anthea_itec_rev46'
ART.mkdir(parents=True, exist_ok=True)
practical = '''## Stratigrafia del terreno

Il modulo autonomo Stratigrafia del terreno si apre dal catalogo Geotecnica e può essere aggiunto come scheda a un progetto. Gestisce un profilo di strati orizzontali; non esegue verifiche geotecniche. In questa versione il collegamento ai moduli di verifica non è ancora attivo.

Assegnare nome, descrizione e, se disponibile, quota altimetrica del piano campagna. Le profondità sono espresse in metri e crescono verso il basso a partire dal piano campagna. La quota altimetrica è un dato distinto e facoltativo.

Inserire gli strati dall’alto verso il basso con + Strato. Duplica crea uno strato indipendente; Elimina conserva almeno uno strato; le frecce cambiano l’ordine. Selezionare uno strato nell’elenco o nel profilo per modificarne nome, descrizione litologica, colore e spessore. Solo l’ultimo strato può essere esteso indefinitamente: per duplicarlo o spostarlo occorre prima renderlo finito e assegnargli lo spessore corretto. Un nuovo strato viene inserito prima dello strato indefinito.

Per ciascuno strato sono disponibili γ e γsat in kN/m³, φ′ in gradi, c′ e cu in kPa. Lasciare vuoti i parametri non disponibili: zero è un valore distinto. Registrare provenienza, qualificazione e note; se i singoli parametri hanno qualificazioni diverse, specificarlo nelle note. Il programma non attribuisce valori in base alla litologia e non applica coefficienti parziali.

Per la falda scegliere Non nota, Non considerata oppure Assegnata. Nel terzo caso inserire la profondità, nulla al piano campagna o positiva al di sotto. La rappresentazione ammette un solo livello; non descrive falde sospese o condizioni artesiane. Un valore eventualmente conservato nel campo profondità è inattivo quando la falda non è assegnata.

Salva conserva il documento autonomo o il progetto. I parametri incompleti generano avvisi e non impediscono il salvataggio; geometria invalida, numeri non validi e falda assegnata senza profondità devono essere corretti. Il grafico evidenzia lo strato selezionato e la falda; la profondità visualizzata di uno strato indefinito non ne rappresenta il limite fisico. Report Word produce la scheda dei dati in DOCX e PDF affiancati, anche senza calcoli.

'''
theory = '''## Modello della stratigrafia del terreno

Il modulo stratigrafico rappresenta una colonna di strati orizzontali, contigui e omogenei nei dati assegnati. Gli spessori positivi sono la sola definizione della geometria: il limite superiore del primo strato è a profondità zero e i limiti successivi derivano dalla somma degli spessori precedenti. Non sono ammessi vuoti o sovrapposizioni. L’ultimo strato può essere indefinito; questa scelta costituisce un’ipotesi di modellazione e non estende la profondità effettivamente indagata.

Il riferimento delle profondità è il piano campagna, con verso positivo discendente e unità metro. L’eventuale quota altimetrica del piano campagna resta distinta. La falda assegnata è un singolo livello al piano campagna o al di sotto; Non nota esprime assenza di informazione, Non considerata una scelta esplicita del modello. Questa versione non calcola pressioni interstiziali, tensioni geostatiche, resistenze o cedimenti.

I dati efficaci φ′ e c′ e la resistenza non drenata cu possono coesistere. Il successivo modulo di verifica dovrà scegliere le condizioni di analisi e richiedere i parametri necessari. Non vengono effettuate conversioni fra resistenza drenata e non drenata né correlazioni automatiche con la descrizione litologica.

I pesi dell’unità di volume sono espressi in kN/m³, le resistenze in kPa e l’angolo in gradi. Un campo vuoto significa dato non disponibile, non zero. I pesi assegnati devono essere positivi; resistenze e angolo non negativi, con angolo minore di 90°. Questi sono controlli del contratto dati, non prescrizioni normative. Il caso γsat minore di γ genera un avviso per il controllo dei dati e delle condizioni di misura.

La qualificazione inserita dall’utente documenta l’origine interpretativa dei valori e non costituisce una trasformazione automatica in valori caratteristici o di progetto. Non vengono applicati coefficienti parziali. La gestione di moduli di deformabilità, permeabilità, acquiferi multipli e collegamenti ai motori di verifica è rinviata a sviluppi successivi.

'''
entries = []
paths = [ROOT / f'supporto/docs/guida-{kind}-anthea.{ext}' for kind in ('pratica', 'teorica') for ext in ('md', 'pdf')]
paths += list((ROOT / 'supporto/documentazione/Guide_ANTHEA').glob('*Rev45.*'))
paths += [ROOT / rel for rel in ('supporto/documentazione/Guide_ANTHEA/README.md', 'supporto/documentazione/Guide_ANTHEA/README.pdf', 'supporto/installer/Indice-guide.md', 'supporto/installer/Indice-guide.pdf', 'supporto/README.md', 'supporto/README.pdf', 'supporto/installer/README.md', 'supporto/installer/README.pdf')]
for source in paths:
    if not source.exists(): continue
    relative = source.relative_to(ROOT)
    target = ARCH / relative
    if not target.exists():
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    entries.append({'origine': relative.as_posix(), 'archivio': target.relative_to(ROOT).as_posix(),
                    'sha256': hashlib.sha256(target.read_bytes()).hexdigest(), 'sostituzione': 'Guide globali Rev46'})
(ARCH / 'registro.json').write_text(json.dumps({'motivo': 'Introduzione del modulo autonomo Stratigrafia', 'files': entries}, ensure_ascii=False, indent=2), encoding='utf-8')
for kind, chapter, anchor in [('pratica', practical, '## Avvio e scelta del modulo'), ('teorica', theory, '## Architettura del calcolo e convenzioni')]:
    path = ROOT / f'supporto/docs/guida-{kind}-anthea.md'
    text = path.read_text(encoding='utf-8').replace('revisione documentale 45', 'revisione documentale 46')
    if chapter.splitlines()[0] not in text: text = text.replace(anchor, chapter + anchor, 1)
    path.write_text(text, encoding='utf-8')
builder = ROOT / 'supporto/scripts/Build-AntheaGuides-Itec.py'
builder.write_text(builder.read_text(encoding='utf-8').replace("REVISION = '45'", "REVISION = '46'").replace("REVISION_NOTE = 'INSTALLAZIONE SENZA NUMERO DI VERSIONE'", "REVISION_NOTE = 'MODULO AUTONOMO STRATIGRAFIA'"), encoding='utf-8')
(ART / 'artifact.md').write_text('''# Guide globali ANTHEA Rev46

Aggiornamento delle guide esistenti con il modulo autonomo Stratigrafia del terreno.
Conservare il modello ITEC e la pipeline Build/Render/Finalize. Nessuna modifica al template originale.
Le revisioni precedenti sono conservate con percorsi relativi e impronte in supporto/SUPERATI/stratigrafia-rev46-20261015.
''', encoding='utf-8')
index = '# ANTHEA Indice delle guide globali\n\nITEC Engineering · Revisione 46 · 15 ottobre 2026\n\n'
for kind in ('pratica', 'teorica'):
    index += f'## Guida {kind} ANTHEA\n\n[Word Rev46](../documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev46.docx) · [PDF Rev46](../documentazione/Guide_ANTHEA/ANTHEA_Guida_{kind}_ITEC_Rev46.pdf)\n\n'
    index += '\n'.join('- ' + line[3:] for line in (ROOT / f'supporto/docs/guida-{kind}-anthea.md').read_text(encoding='utf-8').splitlines() if line.startswith('## ')) + '\n\n'
(ROOT / 'supporto/installer/Indice-guide.md').write_text(index, encoding='utf-8')
readme = ROOT / 'supporto/documentazione/Guide_ANTHEA/README.md'
text = readme.read_text(encoding='utf-8')
# Only the current edition references change; preserve the historical revision descriptions.
head, history = text.split('Le revisioni precedenti', 1)
text = head.replace('Rev45', 'Rev46') + 'Le revisioni precedenti' + history
if 'stratigrafia-rev46' not in text:
    text += '\nLa Rev45 è conservata in `supporto/SUPERATI/stratigrafia-rev46-20261015/`. La Rev46 introduce il modulo autonomo Stratigrafia del terreno; il collegamento ai motori geotecnici resta successivo.\n'
readme.write_text(text, encoding='utf-8')
print('Rev46 preparata; Rev45 conservata.')
editorial_path = ROOT / 'src/ANTHEA.Wiki/Content/editorial.json'
editorial = json.loads(editorial_path.read_text(encoding='utf-8'))
for kind, title, key, area in [
    ('guide', 'Stratigrafia del terreno', 'guida-stratigrafia', 'Moduli singoli'),
    ('theory', 'Modello della stratigrafia del terreno', 'modello-stratigrafia', 'Fondazioni e geotecnica')]:
    editorial[kind + ':' + title] = dict(area=area, id='/wiki/' + ('guide/moduli/' if kind == 'guide' else 'manuale/geotecnica/') + key,
        modules=['geo_stratigrafia'], related=['modello-stratigrafia' if kind == 'guide' else 'guida-stratigrafia'],
        key=key, chapterId='geotecnica', status='reviewed', level='introductory', prerequisites=[], references=[],
        title=title, order=99, summary='Strati orizzontali, dati geotecnici e falda nel modulo autonomo di ANTHEA.')
editorial_path.write_text(json.dumps(editorial, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
for rel in ('supporto/README.md', 'supporto/installer/README.md'):
    path = ROOT / rel
    path.write_text(path.read_text(encoding='utf-8').replace('Rev45', 'Rev46'), encoding='utf-8')
