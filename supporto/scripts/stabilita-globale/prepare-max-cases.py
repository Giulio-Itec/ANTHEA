"""Prepare independent native input copies; every copy MUST be reopened/recalculated in MAX.

Only round-tripped input records are changed. This never synthesizes MAX results.
"""
from pathlib import Path
import struct
import json

root = Path(__file__).resolve().parents[2] / 'artefatti/stabilita-globale/confronti-max'
source = root / 'input-base-verificato.dat'
data = source.read_bytes()

def unique(blob, values):
    pattern = struct.pack('<' + 'd' * len(values), *values)
    start = blob.find(pattern)
    if start < 0 or blob.find(pattern, start + 1) >= 0:
        raise RuntimeError(f'Record assente o ambiguo: {values}')
    return start

geo1 = unique(data, [3, 3.5, .25, 0, 0, 0, 0, 0, 1, .5])
geo2 = unique(data, [3, .25, 3.5, 0])
soil = unique(data, [1800, 2000, 30, 20, 0, 0])
print(dict(geometry1=geo1, geometry2=geo2, soil=soil))

# Geometry, density and friction records confirmed in MAX; cohesion is checked
# explicitly in the soil dialog before admitting the corresponding comparisons.
variants = [
    ('02-mensola-alta', dict(height=5., free=5.5)),
    ('03-gravita-rettangolare', dict(top=1.5)),
    ('04-attrito-22', dict(phi=22., delta=22.*2/3)),
    ('05-coesione-10', dict(cohesion=10./.00980665)),
    ('06-attrito-38', dict(phi=38., delta=38.*2/3)),
    ('07-peso-terreno-20', dict(gamma=20./.00980665, gamma_sat=2100.)),
    ('08-paramento-50', dict(top=.5)),
    ('09-mensola-bassa', dict(height=2., free=2.5)),
    ('10-coesione-20', dict(cohesion=20./.00980665)),
]
cases = [dict(id=name, **(dict(height=3., free=3.5, top=.25, phi=30., gamma=1800., gamma_sat=2000., delta=20., cohesion=0., profile_length=40.) | changes)) for name, changes in variants]
for case in cases:
    target = root / case['id']
    target.mkdir(exist_ok=False)
    output = bytearray(data)
    for relative, key in [(0, 'height'), (8, 'free'), (16, 'top')]:
        struct.pack_into('<d', output, geo1 + relative, case[key])
    for relative, key in [(0, 'height'), (8, 'top'), (16, 'free')]:
        struct.pack_into('<d', output, geo2 + relative, case[key])
    assert struct.unpack_from('<d', output, geo2 + 56)[0] == 8.
    struct.pack_into('<d', output, geo2 + 56, case['profile_length'])
    for relative, key in [(0, 'gamma'), (8, 'gamma_sat'), (16, 'phi'), (24, 'delta'), (32, 'cohesion')]:
        struct.pack_into('<d', output, soil + relative, case[key])
    (target / 'dati.dat').write_bytes(output)
    (target / 'accelerogrammi.bin').write_bytes(b'\0' * 4)
    path = str(target).encode('cp1252')
    target.with_suffix('.mrt').write_bytes(struct.pack('<i', len(path)) + path + b'\0' * 4)
    (target / 'preparazione-input.json').write_text(json.dumps(dict(status='INPUT DA CONTROLLARE E RICALCOLARE IN MAX', **case), indent=2), encoding='utf-8')
    print(target.with_suffix('.mrt'))
