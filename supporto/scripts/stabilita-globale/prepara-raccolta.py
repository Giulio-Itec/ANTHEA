"""Index the saved ANTHEA models; external MAX evidence is tracked separately."""
from pathlib import Path
import json
import sys

root = Path(sys.argv[1]).resolve()
manifest = root / 'indice-confronti.json'
cases = json.loads(manifest.read_text(encoding='utf-8-sig'))
assert len(cases) == 10
assert (root / 'stato-esecuzione.txt').read_text().startswith('PASS')
for case in cases:
    case['MAX'] = 'Vedere stato-max.json e il rapporto di confronto: questo indice contiene risultati ANTHEA.'
    guide = root / 'esempi' / case['Id'] / 'riproduzione.md'
    text = guide.read_text(encoding='utf-8-sig')
    text = text.replace('NON ESEGUITO: questa sessione non espone il controllo delle app Windows. Nessun file MAX è stato creato e nessun risultato MAX è attribuito a questi dati.', 'Questa cartella contiene il modello e i risultati ANTHEA. Lo stato del confronto indipendente MAX è documentato in stato-max.json e nel rapporto della raccolta.')
    guide.write_text(text, encoding='utf-8')
manifest.write_text(json.dumps(cases, ensure_ascii=False, indent=2), encoding='utf-8')
rows = ['# Dieci esempi ANTHEA — stabilità globale', '',
        'Aprire i modelli con ANTHEA, quindi Input → Terreno → Stabilità globale. Il profilo, gli strati, la falda, i parametri di ricerca e le combinazioni sono salvati nel modello. In Verifiche → Stabilità globale si consultano cerchio critico e conci.', '',
        'Ogni cartella contiene il modello `.anthea`, i risultati JSON, tutti i conci CSV, la relazione Word e le istruzioni per ripetere il calcolo.', '',
        '**I controlli interni non sostituiscono il confronto con MAX.** Consultare [stato del confronto](stato-max.json) e [rapporto Word](Rapporto-controllo-stabilita-globale.docx).', '',
        '| Caso | Modello | Descrizione |', '|---|---|---|']
for c in cases:
    rows.append(f"| {c['Id']} | [Apri modello](esempi/{c['Id']}/modello.anthea) | {c['Description']} |")
rows += ['', 'Il caso non drenato segnala una discretizzazione non convergente e non produce un esito favorevole. I casi non soddisfatti restano esplicitamente tali: la raccolta serve a controllare il comportamento del motore, non a certificare dieci muri verificati.', '',
         'Rigenerazione: `dotnet run --project supporto/test/GlobalStability.Checks -c Release -- <cartella output nuova>`. Il marcatore `stato-esecuzione.txt` deve riportare PASS; il solo codice di uscita del processo non prova che tutti gli export siano stati completati.']
(root / 'README.md').write_text('\n'.join(rows) + '\n', encoding='utf-8')
print(root / 'README.md')
