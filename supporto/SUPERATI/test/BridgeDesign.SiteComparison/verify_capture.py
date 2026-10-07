"""Independent integrity and visible-output consistency audit of the acquired corpus."""
import collections
import hashlib
import json
import re
from compare import OUT, FAMILIES, number, records

cases = records(OUT / 'site-cases.jsonl')
assert len(cases) == 1000
assert len({c['id'] for c in cases}) == len({c['inputHash'] for c in cases}) == 1000
assert set(c['family'] for c in cases) == set(FAMILIES)
assert all(n == 125 for n in collections.Counter(c['family'] for c in cases).values())
assert collections.Counter(c['variation'] for c in cases) == dict(automatic=600, manual_depth=160, manual_spans=120, material_strength=120)
warnings = []; clipped = []; effective_hashes = set()
for c in cases:
    o = c['outputs']; f = {s['id']: s['value'] for s in c['fields']}
    assert c['settlement']['elapsedMs'] >= 650 and c['settlement']['stableMs'] >= 180
    assert f['cur'] == 'EUR' and c['source'] == 'https://thebridgeeng.com/design'
    fingerprint = {'family': c['family'], 'fields': c['fields'], 'section': c['section'], 'auto': [o['aN'], o['aD'], o['aPL']]}
    digest = hashlib.sha256(json.dumps(fingerprint, ensure_ascii=False, separators=(',', ':')).encode()).hexdigest()
    assert digest == c['inputHash'], (c['id'], 'input hash mismatch')
    raw = {k: v for k, v in c.items() if k not in ('id','source','capturedAt','variation','familyOrdinal','inputHash','domHash')}
    digest = hashlib.sha256(json.dumps(raw, ensure_ascii=False, separators=(',', ':')).encode()).hexdigest()
    assert digest == c['domHash'], (c['id'], 'DOM hash mismatch')
    effective = dict(f)
    for key in ('L', 'H', 'OW', 'LN', 'N', 'PL'):
        effective[key] = float(o['v' + key])
        if float(f[key]) != effective[key]: clipped.append({'id': c['id'], 'control': key, 'slider': f[key], 'adopted': o['v' + key]})
    effective_hashes.add(hashlib.sha256(json.dumps({'family': c['family'], 'inputs': effective, 'section': c['section'], 'auto': fingerprint['auto']}, sort_keys=True, ensure_ascii=False).encode()).hexdigest())
    width = float(f['LN']) * 3.65 + 3 + 1 + (1.6 if int(f['LN']) >= 4 else 0)
    assert abs(width - float(o['vW'])) < .0051, (c['id'], 'site width convention mismatch')
    if c['variation'] == 'manual_depth': assert 'manual' in o['aD']
    if c['variation'] == 'manual_spans': assert 'manual' in o['aN']
    if c['variation'] == 'material_strength': assert f['fcG'] != 'auto'
    # Cross-check stable animated cards against independently visible outputs.
    if abs(number(o['tDep'], True) - float(o['vD'])) > .010001:
        warnings.append({'id': c['id'], 'kind': 'site_depth_display_inconsistency', 'card': o['tDep'], 'control': o['vD']})
    carbon = number(o['tCO2'], True); intensity = number(o['tCO2S'], True); area = float(o['vL']) * width
    if abs(carbon * 1000 / area - intensity) > .5001 + .5001 * 1000 / area:
        warnings.append({'id': c['id'], 'kind': 'site_carbon_display_inconsistency', 'card': o['tCO2'], 'intensity': o['tCO2S']})
    assert any(row[0].startswith('Approximate total') for row in c['rows'])
    lengths = [float(v.strip()) for v in o['spanTxt'].split('spans: ')[1].split(' m')[0].split('+')]
    assert abs(sum(lengths) - float(o['vL'])) <= len(lengths) * .05001, (c['id'], 'span total disagrees with adopted length')

assert len(effective_hashes) == 1000, 'Duplicated effective configuration'
summary = {'status': 'PASS', 'cases': len(cases), 'inputHashesVerified': len(cases), 'domHashesVerified': len(cases), 'uniqueEffectiveConfigurations': len(effective_hashes), 'clippedSliders': clipped,
           'familyCoverage': dict(collections.Counter(c['family'] for c in cases)), 'variations': dict(collections.Counter(c['variation'] for c in cases)),
           'visibleOutputConsistencyWarnings': warnings}
expected_screenshots = {f"{c['id']}-{c['family']}-{c['variation']}.png" for c in cases if c['familyOrdinal'] in (1, 76, 96, 111)}
screen_root = (OUT / 'screenshots').resolve()
pilot_root = (OUT / 'pilot-screenshots-excluded').resolve()
assert screen_root.parent == OUT.resolve() and pilot_root.parent == OUT.resolve()
for path in screen_root.glob('*.png'):
    if path.name not in expected_screenshots:
        pilot_root.mkdir(exist_ok=True)
        destination = pilot_root / path.name
        assert path.resolve().parent == screen_root and destination.resolve().parent == pilot_root
        path.rename(destination)
assert all((screen_root / name).is_file() for name in expected_screenshots)
summary['screenshots'] = {name: hashlib.sha256((screen_root / name).read_bytes()).hexdigest() for name in sorted(expected_screenshots)}
(OUT / 'integrity-audit.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'status': 'PASS', 'cases': len(cases), 'hashesVerified': 2000, 'siteDisplayWarnings': len(warnings)}))
