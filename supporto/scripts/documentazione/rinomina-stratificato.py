"""Archive the previous guides and remove the old visible method name."""
from pathlib import Path
import json
import shutil

root = Path(__file__).resolve().parents[3]
support = root / 'supporto'
archive = support / 'SUPERATI/ridenominazione-stratificato-20260930'
documents = [Path('docs/palo-stratificato-pilechecker.md'), Path('docs/palo-orizzontale.md'), Path('README.md')]
changes = []
for relative in documents:
    source = support / relative
    destination = source.with_name('palo-stratificato.md') if relative.name == 'palo-stratificato-pilechecker.md' else source
    text = source.read_text(encoding='utf-8-sig')
    for suffix in ('.md', '.pdf'):
        original = source.with_suffix(suffix)
        saved = archive / relative.with_suffix(suffix)
        saved.parent.mkdir(parents=True, exist_ok=True)
        if original.exists() and not saved.exists():
            shutil.copy2(original, saved)
    replacements = {
        'palo-stratificato-pilechecker': 'palo-stratificato',
        'Stratificato (PileChecker)': 'Stratificato',
        "Sviluppo dell'approccio PileChecker": "Sviluppo dell'approccio stratificato",
        "approccio originario PileChecker": "approccio originario dell'utente",
        '**PileChecker dell\'utente**': '**Codice originario dell\'utente**',
        'Confronto critico con PileChecker': 'Confronto critico con il codice originario',
        'PileChecker,': "Codice originario dell'utente,",
        'Metodo stratificato da PileChecker': 'Metodo stratificato',
        'revisione 2, 30 settembre 2026.': 'revisione 3, 30 settembre 2026.',
    }
    for old, new in replacements.items():
        text = text.replace(old, new)
    if 'PileChecker' in text:
        raise ValueError(f'Unconverted visible text in {source}')
    destination.write_text(text, encoding='utf-8')
    if source != destination:
        # Originals are already preserved in the archive. Moving them also
        # removes the superseded names from the current documentation folder.
        for suffix in ('.md', '.pdf'):
            original = source.with_suffix(suffix)
            if original.exists():
                original.replace(archive / relative.with_suffix(suffix))
    changes.append({'origine': str(relative), 'revisione_sostitutiva': str(destination.relative_to(support))})
(archive / 'provenienza.json').write_text(json.dumps({'motivo': 'Rimozione della denominazione precedente dalle diciture pubbliche, su richiesta dell’utente.', 'documenti': changes}, ensure_ascii=False, indent=2), encoding='utf-8')
