"""Replay saved public UI observations through the real C# engine; no site solver is copied."""
import collections
import copy
import csv
import hashlib
import json
import re
import statistics
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'supporto/artefatti/bridge_design_site_1000'
FAMILIES = dict(rc_slab='slab', rc_tbeam='tee', psc_i='psc_i', psc_u='psc_u', psc_box='psc_box', psc_box_var='fcm', steel_i='steel_i', steel_box='steel_box')
GROUPS = {'Superstructure': 'Impalcato', 'Substructure': 'Sottostrutture', 'Foundations': 'Fondazioni', 'Finishes': 'Finiture'}
ITEMS = {
    ('Superstructure', 'Concrete (girders + deck)'): 'Calcestruzzo',
    ('Superstructure', 'Reinforcement'): 'Armatura ordinaria',
    ('Superstructure', 'PT strand'): 'Precompressione',
    ('Superstructure', 'Structural steel'): 'Carpenteria metallica',
    ('Superstructure', 'Formwork'): 'Casseforme equivalenti',
    ('Substructure', 'Concrete (piers, caps, abutments)'): 'Calcestruzzo pile e spalle',
    ('Substructure', 'Reinforcement'): 'Armatura pile e spalle',
    ('Foundations', 'Concrete (footings / pile caps)'): 'Calcestruzzo plinti',
    ('Foundations', 'Reinforcement'): 'Armatura plinti e pali',
    ('Foundations', 'Bored piles'): 'Pali trivellati',
    ('Finishes', 'Bearings'): "Apparecchi d'appoggio",
    ('Finishes', 'Expansion joints'): 'Giunti di dilatazione',
    ('Finishes', 'Barriers'): 'Barriere',
    ('Finishes', 'Wearing surface'): 'Pavimentazione',
}
SECTIONS = {'Deck slab t': 'slab', 'Girder spacing': 'spacing', 'Web width': 'web', 'Web thickness': 'web',
            'Bottom slab t': 'bottom', 'Bottom width / W': 'bottom_ratio', 'Cells': 'cells', 'Boxes': 'boxes',
            'Flange width': 'flange_width', 'Flange thickness': 'flange_mm', 'Web plate t': 'web_mm',
            'Web slope': 'web_slope', 'Haunch': 'haunch', 'U top width': 'u_top', 'U bottom width': 'u_bottom'}

def records(path):
    return [json.loads(s) for s in path.read_text(encoding='utf-8-sig').splitlines() if s.strip()]

def number(text, italian=False):
    token = re.search(r'-?[\d.,]+', text).group()
    return float(token.replace('.', '').replace(',', '.') if italian else token)

def mapped(case, mode, defaults):
    d = copy.deepcopy(defaults)
    i = d['input']; f = {x['id']: x['value'] for x in case['fields']}; o = case['outputs']
    i['family'] = FAMILIES[case['family']]
    for source, target in [('L', 'length'), ('H', 'height'), ('OW', 'obstacle_width'), ('LN', 'lanes'), ('fcS', 'fc_sub')]:
        i[target] = float(o['v' + source]) if 'v' + source in o else float(f[source])
    # Site carriageway convention, independently checked against vW in every case.
    i['median'] = 1.6 if i['lanes'] >= 4 else 0
    i['obstacle'] = {'none': 'Nessuno', 'river': 'Fiume', 'road': 'Strada / ferrovia'}[f['obs']]
    i['soil'] = {'rock': 'Roccia', 'dense': 'Sabbia / ghiaia densa', 'medium': 'Terreno medio', 'soft': 'Argilla soffice'}[f['soil']]
    i['pier'] = {'multi_column': 'Telaio a colonne', 'single_round': 'Colonna circolare', 'wall': 'Setto', 'hammerhead': 'Testa a martello'}[f['pier']]
    i['foundation'] = {'auto': 'Automatica', 'spread': 'Plinto diretto', 'bored_pile': 'Pali Ø 1,0 m', 'bored_pile15': 'Pali Ø 1,5 m'}[f['found']]
    i['continuous'] = f['cont'] == 'continuous'
    i['start_pier'] = f['endS'] == 'pier'; i['end_pier'] = f['endE'] == 'pier'
    i['spans'] = float(f['N']) if mode == 'resolved' or 'manual' in o['aN'] else 0
    i['depth'] = number(o['vD']) if mode == 'resolved' else float(f['D']) if 'manual' in o['aD'] else 0
    # FCM D on the site is the mean of pier/midspan depths, unlike ANTHEA's midspan d.
    if case['family'] == 'psc_box_var' and (mode == 'resolved' or 'manual' in o['aD']):
        i['depth'] = float(re.search(r'/ mid ([\d.]+)', o['secTxt']).group(1))
    i['pile_length'] = float(f['PL']) if mode == 'resolved' or 'manual' in o['aPL'] else 0
    # ANTHEA has no automatic fc mode. Use the explicitly displayed adopted strength.
    i['fc'] = float(re.search(r'f′c (\d+)', o['secTxt']).group(1))
    notes = ['fc automatico sito risolto nel valore visualizzato: ANTHEA non ha auto-fc.'] if f['fcG'] == 'auto' else []
    if mode == 'resolved':
        for s in case['section']:
            keys = [k for k in SECTIONS if s['label'].startswith(k)]
            if not keys:
                notes.append('Parametro sito senza corrispondenza: ' + s['label']); continue
            key = SECTIONS[keys[0]]
            value = float(s['valueAttribute'])
            # DOM values are metres, including plate thickness sliders (labels show mm).
            i[key] = value * 1000 if key in ('flange_mm', 'web_mm') else value
        if f['found'] == 'auto':
            i['foundation'] = 'Plinto diretto' if 'spread' in o['aF'] else 'Pali Ø 1,5 m' if 'Ø1.5' in o['subTxt'] else 'Pali Ø 1,0 m'
    if case['family'] == 'psc_i': notes.append('Profilo AASHTO prefabbricato del sito non rappresentato dal profilo rettangolare equivalente ANTHEA.')
    if case['family'] == 'psc_box_var': notes.append('Legge di variazione altezza FCM e rapporto mezzeria/pila diversi; non assimilati.')
    return {'id': case['id'], 'mode': mode, 'data': d, 'mappingNotes': notes}

def prepare():
    cases = records(OUT / 'site-cases.jsonl'); defaults = json.loads((OUT / 'engine-defaults.json').read_text(encoding='utf-8-sig'))
    assert len({c['inputHash'] for c in cases}) == len(cases), 'Duplicate inputs'
    assert all(c.get('settlement', {}).get('stableMs', 0) >= 180 for c in cases), 'Unsettled capture'
    with (OUT / 'engine-inputs.jsonl').open('w', encoding='utf-8') as file:
        for c in cases:
            for mode in ('native', 'resolved'):
                file.write(json.dumps(mapped(c, mode, defaults), ensure_ascii=False) + '\n')
    print(f'{len(cases)} site cases, {2 * len(cases)} actual-engine inputs written.')

def analyze():
    cases = records(OUT / 'site-cases.jsonl'); actual = records(OUT / 'engine-results.jsonl')
    results = {(a['id'], a['mode']): a for a in actual}
    assert len(results) == 2 * len(cases) == len(actual)
    measurements = []; verdicts = []; missing = []
    for c in cases:
        o = c['outputs']; f = {x['id']: x['value'] for x in c['fields']}
        sq = {}; group = ''
        for row in c['rows']:
            if len(row) == 1: group = row[0]
            elif len(row) == 4 and row[0] != 'Item':
                if (group, row[0]) in ITEMS:
                    sq[(GROUPS[group], ITEMS[group, row[0]])] = (number(row[1], True), row[2])
                else: missing.append({'id': c['id'], 'group': group, 'item': row[0], 'qty': row[1], 'unit': row[2], 'cost': row[3]})
        for mode in ('native', 'resolved'):
            a = results[c['id'], mode]
            if a['status'] != 'calculated':
                verdicts.append(dict(id=c['id'], family=c['family'], variation=c['variation'], mode=mode, status='NONFINITE' if a['status'] == 'nonfinite' else 'REJECTED', compared=0, matched=0, error=a['error'])); continue
            r = a['result']; start = len(measurements)
            def measure(metric, expected, value, tolerance, unit, category='geometry'):
                measurements.append(dict(id=c['id'], family=c['family'], variation=c['variation'], mode=mode, category=category, metric=metric, site=expected, anthea=value, delta=value-expected,
                                         relative_percent=(value/expected-1)*100 if expected else None, tolerance=tolerance, unit=unit, match=abs(value-expected) <= tolerance))
            measure('Larghezza', number(o['vW']), r['Width'], .0051, 'm')
            if c['family'] == 'psc_box_var':
                pier, mid = map(float, re.search(r'pier ([\d.]+) / mid ([\d.]+)', o['secTxt']).groups())
                measure('Altezza in campata', mid, r['Depth'], .0051, 'm')
                measure('Altezza sulle pile FCM', pier, r['PierDepth'], .0051, 'm')
            else:
                measure('Altezza in campata', number(o['vD']), r['Depth'], .0051, 'm')
            measure('Numero campate', float(f['N']), len(r['Spans']), 0, 'n.')
            # Keep one comparable span-layout metric even when the counts differ.
            site_spans = [float(s.strip()) for s in o['spanTxt'].split('spans: ')[1].split(' m')[0].split('+')]
            measure('Luce massima', max(site_spans), max(r['Spans']), .051, 'm')
            if len(site_spans) == len(r['Spans']):
                for k, (s, v) in enumerate(zip(site_spans, r['Spans'])): measure(f'Luce {k+1}', s, v, .051, 'm')
            girders = int(re.search(r'· (\d+) (?:girder|box|slab)', o['secTxt']).group(1))
            measure('Elementi longitudinali', girders, r['Girders'], 0, 'n.')
            measure('Lunghezza pali', float(f['PL']) if 'bored' in o['subTxt'] else 0, r['PileLength'], .001, 'm')
            # UI depth card is animated; vD is the non-animated adopted dimension.
            measure('CO2', number(o['tCO2'], True), r['Carbon'], .5001, 'tCO2e', 'environment')
            measure('Durata', number(o['tDur']), r['Duration'], 0, 'mesi', 'schedule')
            total = next(row[-1] for row in c['rows'] if row[0].startswith('Approximate total'))
            measure('Costo totale (listini diversi)', number(total, True), r['TotalCost'], .5001, 'EUR', 'economic')
            aq = {(q['Group'], 'Pali trivellati' if q['Item'].startswith('Pali trivellati') else q['Item']): q['Amount'] for q in r['Quantities']}
            for key in sorted(set(sq) | set(aq)):
                val, unit = sq.get(key, (0, next((q['Unit'] for q in r['Quantities'] if q['Group'] == key[0] and q['Item'].startswith(key[1])), '')))
                measure('/'.join(key), val, aq.get(key, 0), .5001 if unit not in ('ea', 'cad') else 0, unit, 'quantity')
            subset = measurements[start:]; matched = sum(v['match'] for v in subset)
            verdicts.append(dict(id=c['id'], family=c['family'], variation=c['variation'], mode=mode, status='MATCH' if matched == len(subset) else 'DIFFERENT', compared=len(subset), matched=matched, error=''))
    for filename, rows in [('measurements.csv', measurements), ('case-verdicts.csv', verdicts), ('unmapped-site-items.csv', missing)]:
        with (OUT / filename).open('w', newline='', encoding='utf-8-sig') as file:
            writer = csv.DictWriter(file, fieldnames=list(rows[0])); writer.writeheader(); writer.writerows(rows)
    by_mode = {mode: dict(collections.Counter(v['status'] for v in verdicts if v['mode'] == mode)) for mode in ('native', 'resolved')}
    coverage = {'family': dict(collections.Counter(c['family'] for c in cases)), 'variation': dict(collections.Counter(c['variation'] for c in cases))}
    for key in ('L', 'H', 'LN', 'obs', 'soil', 'cont', 'endS', 'endE', 'pier', 'found', 'fcG'):
        coverage[key] = dict(collections.Counter(next(f['value'] for f in c['fields'] if f['id'] == key) for c in cases))
    summary = {'siteCases': len(cases), 'uniqueInputs': len({c['inputHash'] for c in cases}), 'engineRuns': len(actual), 'modes': by_mode, 'coverage': coverage,
               'firstCapture': cases[0]['capturedAt'], 'lastCapture': cases[-1]['capturedAt'], 'measurements': len(measurements),
               'sha256': {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in [OUT/'site-cases.jsonl', OUT/'engine-inputs.jsonl', OUT/'engine-results.jsonl']}}
    (OUT / 'summary.json').write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding='utf-8')
    lines = ['# Bridge Design — confronto con 1.000 casi del sito', '', f"Casi reali unici: **{len(cases)}**. Esecuzioni del motore ANTHEA: **{len(actual)}**. Fonte: https://thebridgeeng.com/design.",
             '', '## Esito', '', '| Modalità | Calcolati con differenze | Corrispondenti su tutti gli indicatori | Rifiutati dal motore |', '|---|---:|---:|---:|']
    for mode, counts in by_mode.items(): lines.append(f"| {mode} | {counts.get('DIFFERENT',0)} | {counts.get('MATCH',0)} | {counts.get('REJECTED',0)} |")
    if (OUT / 'engine-results-before-fix.jsonl').exists():
        previous = records(OUT / 'engine-results-before-fix.jsonl')
        transitions = collections.Counter(f"{p['status']} -> {results[p['id'], p['mode']]['status']}" for p in previous)
        issue = {'before': dict(collections.Counter(p['status'] for p in previous)), 'after': dict(collections.Counter(p['status'] for p in actual)), 'transitions': dict(transitions),
                 'nonfiniteCasesBefore': [{'id': p['id'], 'mode': p['mode']} for p in previous if p['status'] == 'nonfinite']}
        common = [(p, results[p['id'], p['mode']]) for p in previous if p['status'] == 'calculated' and results[p['id'], p['mode']]['status'] == 'calculated']
        for before, after in common:
            assert {k:v for k,v in before['result'].items() if k != 'Warnings'} == {k:v for k,v in after['result'].items() if k != 'Warnings'}, (before['id'], 'numerical change outside uplift diagnosis')
        issue['unchangedNumericalResultsOnCommonCalculatedCases'] = len(common)
        (OUT / 'uplift-fix-audit.json').write_text(json.dumps(issue, indent=2, ensure_ascii=False), encoding='utf-8')
        lines += ['', '## Problema numerico individuato e corretto', '',
                  'Il caso live 0137 ha evidenziato un assiale di fondazione negativo dopo lo spostamento della pila per evitare l’ostacolo: la radice quadrata del predimensionamento del plinto produceva NaN. Il motore ora rifiuta esplicitamente fondazioni con assiale nullo o di sollevamento e segnala reazioni verso l’alto. La ricerca esclude gli appoggi che richiedono dispositivi antisollevamento.',
                  f"Sul corpus completo, il motore precedente produceva **{issue['before'].get('nonfinite',0)} risultati non finiti** nelle 2.000 valutazioni; quello aggiornato ne produce **{issue['after'].get('nonfinite',0)}**. La correzione diagnostica il limite del modello, non simula una verifica di fondazioni in trazione.",
                  'Il runner congelato e la sorgente iniziale sono conservati in baseline-runner e BridgeConcept.Calculation.before.cs; engine-results-before-fix.jsonl contiene tutte le esecuzioni precedenti. uplift-fix-audit.json riepiloga le transizioni. Gli indicatori riportati qui sotto si riferiscono al motore aggiornato.']
    lines += ['', '**Questa campagna non certifica equivalenza con il sito.** Mille casi sono un campione, non tutte le combinazioni possibili. Anche una corrispondenza completa dei soli indicatori confrontati non comprenderebbe le funzioni assenti in ANTHEA.',
              '', '## Metodo riproducibile', '',
              'Acquisizione live dalla UI pubblica tramite il pulsante Random bridge, con 125 casi per famiglia: 75 automatici, 20 variazioni manuali di altezza, 15 variazioni del numero di campate, 15 variazioni della resistenza del calcestruzzo. Nessun solver del sito scaricato o eseguito offline. I valori animati vengono letti dopo almeno 650 ms e 180 ms di DOM invariato. Il campione pilota non assestato è escluso e conservato separatamente.',
              'Ogni riga di site-cases.jsonl conserva controlli, dimensioni di sezione, risultati, computo completo, data, hash e numero di letture di assestamento. Duplicati esclusi tramite hash degli input. Le schermate sono esempi per famiglia e modalità; la prova completa dei 1.000 casi è nel JSONL.',
              '', '- **native**: stessi dati del sito, stessi vincoli e modalità automatica dove supportata. Resistenza automatica risolta nel valore visualizzato perché ANTHEA non ha auto-fc. Listino e coefficienti ANTHEA conservati.',
              '- **resolved**: stesso caso, con numero di campate, altezza, fondazione e dimensioni di sezione supportate impostate secondo i valori adottati dal sito. Serve a isolare le differenze successive al predimensionamento. Le dimensioni copiate non sono una prova indipendente di parità degli automatismi.',
              '- Spartitraffico 0 m per 2 corsie, 1,6 m da 4 corsie: convenzione geometrica osservata e controllata con la larghezza del sito. Restano 3,65 m/corsia, banchine 1,5 m e barriere 0,5 m per lato.',
              '- Per le dimensioni automatiche si legge il valore DOM adottato, non il valore del cursore arrotondato al passo. Quote in m, spessori metallici convertiti in mm; quantità e costi con separatori italiani.',
              '- Il generatore casuale del sito può produrre lunghezze fuori dal range 20–400 m del cursore: in 100 casi la proprietà value del cursore è limitata, mentre vL, disegno e computo adottano un’altra lunghezza. La traduzione usa la lunghezza effettivamente visualizzata in vL. Il dato originale del cursore resta conservato; integrity-audit.json documenta questi casi.',
              '- Nel cassone FCM, D del sito è la media delle quote pier e mid. Il confronto usa la quota mid per l’altezza in campata ANTHEA e confronta separatamente la quota pier. Nei casi FCM manuali, anche native risolve la quota mid visualizzata: i due controlli D non hanno lo stesso significato. La legge di variazione longitudinale rimane una differenza di modello.',
              '- Tolleranze determinate dalla risoluzione visualizzata: 0,0051 m sulle quote a due decimali; 0,051 m sulle luci a un decimale; 0,5001 sulle quantità e CO2 arrotondate all’intero; conteggi esatti. Il costo usa il totale del computo in EUR (tolleranza 0,5001 EUR), non la scheda abbreviata in milioni.',
              '', '## Cosa non è equivalente', '',
              'Il sito include profili prefabbricati AASHTO, regole diverse per campate e altezza, pile e fondazioni, durata, fattori ambientali e voci di montaggio/opere provvisionali. ANTHEA usa una geometria parametrica propria, carichi uniformi equivalenti e un listino EUR modificabile. Il costo è pertanto un confronto diagnostico tra modelli e listini diversi, non un test isolato di una formula di somma. Le voci senza corrispondenza sono conservate in unmapped-site-items.csv. Il Detailed check AASHTO del sito non è riprodotto e non è oggetto della campagna.',
              '', '## Indicatori (automatico ANTHEA)', '', '| Indicatore | Confronti | Entro tolleranza | Scostamento relativo mediano assoluto |', '|---|---:|---:|---:|']
    names = ['Larghezza', 'Altezza in campata', 'Altezza sulle pile FCM', 'Numero campate', 'Luce massima', 'Elementi longitudinali', 'Lunghezza pali', 'Impalcato/Calcestruzzo', 'Impalcato/Carpenteria metallica', 'Fondazioni/Calcestruzzo plinti', 'CO2', 'Durata', 'Costo totale (listini diversi)']
    for name in names:
        subset = [v for v in measurements if v['mode'] == 'native' and v['metric'] == name]
        errors = [abs(v['relative_percent']) for v in subset if v['relative_percent'] is not None]
        lines.append(f"| {name} | {len(subset)} | {sum(v['match'] for v in subset)} | {statistics.median(errors):.2f}% |" if errors else f'| {name} | {len(subset)} | — | — |')
    lines += ['', '## Copertura', '', '```json', json.dumps(coverage, ensure_ascii=False, indent=2), '```', '', '## Ripetizione del confronto', '',
              '```powershell', 'python supporto/test/BridgeDesign.SiteComparison/compare.py prepare', 'dotnet run --project supporto/test/BridgeDesign.SiteComparison -c Release -- --run supporto/artefatti/bridge_design_site_1000/engine-inputs.jsonl supporto/artefatti/bridge_design_site_1000/engine-results.jsonl', 'python supporto/test/BridgeDesign.SiteComparison/compare.py analyze', '```', '',
              'engine-hashes-before.json identifica il motore iniziale; engine-hashes-after.json identifica le sorgenti aggiornate. summary.json riporta gli hash del dataset e dei risultati. case-verdicts.csv contiene tutti gli esiti, inclusi i rifiuti con motivazione; measurements.csv tutti gli scostamenti numerici. Un’esecuzione senza eccezioni non equivale a una corrispondenza col sito.']
    (OUT / 'REPORT.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print(json.dumps({k: summary[k] for k in ('siteCases','uniqueInputs','engineRuns','modes','measurements')}, ensure_ascii=False))

if __name__ == '__main__':
    {'prepare': prepare, 'analyze': analyze}[sys.argv[1]]()
