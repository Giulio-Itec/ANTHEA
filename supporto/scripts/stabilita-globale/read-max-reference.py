"""Read native saved results, cross-check against the real MAX report, prepare comparison input."""
from pathlib import Path
import importlib.util
import json
import math
import re
import struct
import sys

spec = importlib.util.spec_from_file_location('rtf_text', Path(__file__).with_name('rtf-text.py'))
rtf = importlib.util.module_from_spec(spec); spec.loader.exec_module(rtf)
folder = Path(sys.argv[1]).resolve()
report = folder / '_relazione/Relazione.rtf'
text = rtf.extract(report.read_text(encoding='cp1252'))
(folder / 'relazione-max.txt').write_text(text, encoding='utf-8')
match = re.search(r'2 - GEO \(A2-M2-R2\)\t(-?[\d.]+); ([\d.]+)\t([\d.]+)\t\s*([\d.]+)', text)
if not match: raise ValueError('Risultato globale non trovato nella relazione MAX')
xc, yc, rr, ff = map(float, match.groups())
blob = (folder / 'dati.dat').read_bytes()
candidates = []
for i in range(len(blob)-56):
    r, f, zero, x, y, right, left = struct.unpack_from('<7d', blob, i)
    if abs(r-rr)<.00501 and abs(f-ff)<.000501 and x==xc and y==yc and left<x<right and zero==0:
        candidates.append((i,r,f,x,y,left,right))
if len(candidates)!=1: raise ValueError(f'Record risultato ambiguo: {candidates}')
offset,r,f,x,y,left,right=candidates[0]
native=json.loads((folder / 'preparazione-input.json').read_text(encoding='utf-8'))
height=native['height']+.5
# Exact intersection with the same horizontal ground. Native bounds are retained
# separately: MAX shifts/truncates its first and last slices near the intersections.
right_exact=x+math.sqrt(r*r-y*y)-1e-8
left_exact=x-math.sqrt(r*r-(y+height)**2)
reference=dict(model='modello.anthea',max_model='../'+folder.name+'.mrt',max_evidence='_relazione/Relazione.rtf',max_version='MAX 16',combination='Globale A2–M2–R2 1',max_value=f,max_value_kind='F',max_slices=25,
    circle=dict(x=x,y=y,radius=r,left=left_exact,right=right_exact,translation_x=1+native['top'],translation_y=height),
    native_record=dict(offset=offset,left=left,right=right,report_F=ff,report_R=rr))
# Read the 25 actual printed MAX slice rows (W, Qy, Qf, b, alpha, phi, c, u).
section=text.split('Dettagli strisce verifiche stabilità')[1].split('Combinazione n° 2 - GEO (A2-M2-R2)')[1]
rows=[]
for line in section.splitlines():
    fields=line.split('\t')
    if len(fields)>=9 and fields[0].isdigit() and 1<=int(fields[0])<=25:
        w,qy,qf=map(float,fields[1:4]); width=float(fields[4].split(' - ')[-1]); alpha,phi,c,u=map(float,fields[5:9])
        rows.append(dict(index=int(fields[0]),weight=w,vertical=qy+qf,width=width,alpha=alpha,phi=phi,cohesion=c,u=u))
    if len(rows)==25: break
if len(rows)!=25: raise ValueError('Conci MAX mancanti')
reference['native_printed_slices']=rows
(folder / 'riferimento-max.json').write_text(json.dumps(reference,ensure_ascii=False,indent=2),encoding='utf-8')
print(folder.name, f, x, y, r)
