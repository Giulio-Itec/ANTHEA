"""Controlli di integrità della campagna e convergenza dell'integrazione indipendente."""
import json, hashlib, contextlib, io
from pathlib import Path
with contextlib.redirect_stdout(io.StringIO()):
    import reference_base as rb
repo=Path(__file__).resolve().parents[3]
art=repo/'supporto/artefatti/validazione_ca_2026_09_25'
base=json.loads((art/'reference.json').read_text(encoding='utf8'))['cases']
extra=json.loads((art/'extra_reference.json').read_text(encoding='utf8'))['cases']
actual=sum([json.loads((art/n).read_text(encoding='utf8')) for n in ['actual_base.json','actual_extra.json']],[])
assert len(base+extra)==len(actual)==88
assert {v['id'] for v in base+extra}=={v['id'] for v in actual}
assert not any('error' in v or 'Error' in v for v in actual)
coverage=json.loads((art/'coverage.json').read_text(encoding='utf8'))
assert all(len(ids)>=2 for rows in coverage.values() for ids in rows.values())
diffs={}
for v in base:
    if 'plane' not in v['reference']:continue
    args=(v['section'],v['reference']['plane'],v['law'])
    a,b=rb.response(*args,order=6),rb.response(*args,order=10)
    diffs[v['id']]=max(abs(a[k]-b[k]) for k in ['N','Mx','My'])
assert max(diffs.values())<1e-8
files=list((repo/'lib/Checker').glob('*.dll'))
files+=list((repo/'X.Core').glob('*.cs'))+list((repo/'X.Materiali').glob('*.cs'))
files+=list(Path(__file__).parent.glob('*.py'))+list(Path(__file__).parent.glob('*.cs'))
files+=list(art.glob('*.json'))
manifest={str(p.relative_to(repo)):hashlib.sha256(p.read_bytes()).hexdigest() for p in files if p.name!='campaign_integrity.json'}
report=dict(cases=88,groups=len(coverage['groups']),detail_checks=len(coverage['details']),minimum_examples_per_check=2,quadrature_max_difference=max(diffs.values()),sha256=manifest)
(art/'campaign_integrity.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print({k:v for k,v in report.items() if k!='sha256'})
