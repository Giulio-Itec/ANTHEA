"""Independent rational statics and integrated curvature for two wall histories.

No GPC/ANTHEA imports. Internal units N, mm, Nmm, radians. Positive x is
downward from the head, N is compression, rotation is du/dx. The cantilever
is fixed at x=L. M=H*x-C; EI*u''=M with u(L)=u'(L)=0.
Creates only a new file, or checks the frozen fixture without modifying it.
"""
from fractions import Fraction as F
from pathlib import Path
import argparse
import json


def expected():
    length = F(6000)
    rigidity = F(200000000000000)
    cases = []
    for name, force, couple, normal in [('C1', 10000, 0, 100000), ('C2', -6000, 12000000, -30000)]:
        rows = []
        for x in map(F, range(0, 6001, 500)):
            displacement = force / rigidity * (length**3 / 3 - x * length**2 / 2 + x**3 / 6) - couple / rigidity * (length - x)**2 / 2
            rotation = force / rigidity * (-length**2 / 2 + x**2 / 2) + couple / rigidity * (length - x)
            rows.append(dict(z=float(-x), u=float(displacement), rotation=float(rotation), N=normal, V=force, M=float(force * x - couple)))
        cases.append(dict(id=name, rows=rows))
    return dict(method='Equilibrio e integrazione razionale di M/EI; nessun motore numerico', length_mm=int(length), EI_Nmm2=int(rigidity), cases=cases)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    values = expected()
    if args.check:
        assert json.loads(args.output.read_text(encoding='utf-8')) == values, 'Frozen independent fixture differs'
        print('PASS: 26 independent stations unchanged')
    else:
        with args.output.open('x', encoding='utf-8', newline='\r\n') as stream:
            json.dump(values, stream, ensure_ascii=False, indent=2)
