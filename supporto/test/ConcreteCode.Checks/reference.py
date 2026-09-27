"""Independent oracle: execute the public fib StructuralCodes functions, not ANTHEA.
Source files must be downloaded under supporto/artefatti/ca-normative/fonti.
The JSON records their SHA256; expected values are never generated from C#.
Only AST function declarations and standard-library imports are loaded: interpolation
functions requiring scipy are not used. Mathematical function bodies remain unchanged.
"""
import ast
import hashlib
import json
import math
import sys
import urllib.request
from pathlib import Path

root = Path(__file__).resolve().parents[3]
sources = root / 'supporto/artefatti/ca-normative/fonti'
revision = '3e9c3f5cffb0c28e083257346006c7eac02384fd'
paths = {
    'ec_shear.py': 'ec2_2004/shear.py',
    'ec_cracks.py': 'ec2_2004/_section_7_3_crack_control.py',
    'mc_shear.py': 'mc2010/_concrete_shear.py',
}
urls = {name: f'https://raw.githubusercontent.com/fib-international/structuralcodes/{revision}/structuralcodes/codes/{path}' for name, path in paths.items()}
if '--download' in sys.argv:
    sources.mkdir(parents=True, exist_ok=True)
    for name, url in urls.items():
        (sources / name).write_bytes(urllib.request.urlopen(url, timeout=30).read())
def load(name):
    path = sources / name
    tree = ast.parse(path.read_text(encoding='utf-8'))
    tree.body = [n for n in tree.body if isinstance(n, ast.FunctionDef) or
                 isinstance(n, (ast.Import, ast.ImportFrom)) and
                 all(a.name in ('math', 'typing', 'warnings', 'pi', 'sin', 'tan') for a in n.names)]
    scope = {}
    exec(compile(tree, str(path), 'exec'), scope)
    return scope

ec, mc, cracks = load('ec_shear.py'), load('mc_shear.py'), load('ec_cracks.py')
shear = []
for fck in (20, 35, 70, 80):
    for n in (-400, 0, 100):
        for reinforced in (False, True):
            p = dict(Standard='EN 1992-1-1', N=n, V=140., M=80., Area=300000., Bw=300., D=550.,
                     Asl=2500., Fck=fck, Fcd=.85*fck/1.5, Fyd=500/1.15, GammaC=1.5, Es=200000.,
                     Asw=157.08 if reinforced else 0., Spacing=150., Alpha=90., CotTheta=1.6,
                     LeverFactor=.9, Aggregate=20., AxialEccentricity=0.)
            if reinforced:
                rs = ec['VRds'](p['Asw'],150,495,math.degrees(math.atan(1/1.6)),500)/1000
                # alpha_cw=1 for ordinary non-prestressed RC: pass NEd=0 to this helper.
                rc = ec['VRdmax'](300,495,fck,math.degrees(math.atan(1/1.6)),0,300000,p['Fcd'])/1000
                rd = min(rs,rc)
            else:
                rd = max(0, ec['VRdc'](fck,550,2500,300,-n*1000,300000,p['Fcd'])/1000)
                rs = rc = None
            shear.append(dict(input=p, expected=rd, steel=rs, concrete=rc))
            p = p | dict(Standard='Model Code 2010')
            loads = mc['create_load_dict'](80e6,140e3,n*1000,0)
            if reinforced:
                rs = mc['v_rds'](157.08,150,495,500,math.degrees(math.atan(1/1.6)))/1000
                rc = mc['v_rd_max_approx2'](fck,300,math.degrees(math.atan(1/1.6)),495,200000,2500,loads)/1000
                rd = min(rs,rc)
            else:
                rd = mc['v_rdc_approx2'](fck,495,300,20,200000,2500,loads)/1000
                rs = rc = None
            shear.append(dict(input=p,expected=rd,steel=rs,concrete=rc))

widths=[]
for sigma in (50,200,350):
    for short in (True,False):
        for ribbed in (True,False):
            for spacing in (100,500):
                p=[sigma,200000,33000,3.2,.015,20,35,spacing,350,short,ribbed,.5]
                eps=cracks['eps_sm_eps_cm'](sigma,200000/33000,.015,.6 if short else .4,3.2,200000)
                sr=cracks['sr_max_close'](35,20,.015,.8 if ribbed else 1.6,.5,3.4,.425) if spacing<=225 else cracks['sr_max_far'](500,150)
                widths.append(dict(input=p,expected=cracks['wk'](sr,eps)))
result=dict(source='fib-international/structuralcodes (public functions, source hashes below)',
            revision=revision, urls=urls,
            hashes={n:hashlib.sha256((sources/n).read_bytes()).hexdigest() for n in ('ec_shear.py','mc_shear.py','ec_cracks.py')},
            shear=shear,cracks=widths)
Path(__file__).with_name('reference.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(f'{len(shear)} shear + {len(widths)} crack independent reference cases')
