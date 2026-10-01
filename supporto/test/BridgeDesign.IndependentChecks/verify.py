"""Independent oracle: stiffness FEM and a closed-form bill of quantities.

Does not load ANTHEA assemblies, call its solver, or use the reference website.
The controlled cost case has imposed circular piers and direct footings so its
bill of quantities can be derived algebraically without automatic-size loops.
"""
import json, math, sys
from pathlib import Path
import numpy as np
from fractions import Fraction

folder = Path(sys.argv[1])
observed = json.loads((folder / 'osservati.json').read_text(encoding='utf-8-sig'))
checks = 0
errors = {'reaction_kN': 0., 'moment_kNm': 0., 'shear_kN': 0., 'deflection_mm': 0., 'cost_eur': 0., 'carbon_t': 0.}
def near(actual, expected, category, rtol=2e-8, atol=1e-7):
    global checks
    checks += 1
    delta = abs(actual - expected)
    errors[category] = max(errors[category], delta)
    assert delta <= atol + rtol * max(abs(actual), abs(expected)), (category, actual, expected, delta)

def fem(lengths, q, ei):
    count = len(lengths); matrix = np.zeros((2*(count+1), 2*(count+1))); load = np.zeros(2*(count+1)); elements=[]
    for j, length in enumerate(lengths):
        k = ei / length**3 * np.array([[12,6*length,-12,6*length], [6*length,4*length**2,-6*length,2*length**2], [-12,-6*length,12,-6*length], [6*length,2*length**2,-6*length,4*length**2]])
        f = q * np.array([length/2,length**2/12,length/2,-length**2/12]); ids=np.arange(2*j, 2*j+4)
        matrix[np.ix_(ids,ids)] += k; load[ids] += f; elements.append((length,k,f,ids))
    free = np.arange(1, 2*(count+1), 2); u=np.zeros(len(load))
    u[free] = np.linalg.solve(matrix[np.ix_(free,free)], load[free])
    reactions = -(matrix @ u - load)[::2]
    return reactions, [(length, k@u[ids]-f, u[ids]) for length,k,f,ids in elements]

for case in observed['beams']:
    lengths, q, ei = case['spans'], case['q'], case['ei']
    if case['continuous']:
        reactions, elements = fem(lengths,q,ei)
    else:
        reactions=np.zeros(len(lengths)+1); elements=[]
        for j,length in enumerate(lengths):
            reaction, element=fem([length],q,ei); reactions[j:j+2]+=reaction; elements+=element
    for actual, expected in zip(case['Reactions'], reactions): near(actual, expected, 'reaction_kN')
    offsets=np.r_[0,np.cumsum(lengths)]
    for point in case['Stations']:
        j=point['Span']-1; length,end,u=elements[j]; x=point['X']-offsets[j]; t=x/length
        # Hermite interpolation plus the exact uniform-load bubble. Its nodal
        # displacements/rotations vanish, so it does not change the FEM solve.
        v=(1-3*t*t+2*t**3)*u[0]+length*(t-2*t*t+t**3)*u[1]+(3*t*t-2*t**3)*u[2]+length*(-t*t+t**3)*u[3]+q*x*x*(length-x)**2/(24*ei)
        moment=end[1]-end[0]*x-q*x*x/2
        shear=-end[0]-q*x
        near(point['Moment'],moment,'moment_kNm')
        near(point['Shear'],shear,'shear_kN')
        near(point['DeflectionMm'],v*1000,'deflection_mm')

def bill(n, depth):
    # SI dimensions explicitly fixed in the independently defined benchmark.
    length=120.; width=11.3; height=12.; diameter=2.; footing=6.
    deck=width*depth*length; pier_h=height-depth; cap=width*1.5*1.4
    pier=math.pi*diameter**2/4*pier_h+cap; abutment=width*(7.*1.+3.)
    sub=(n-1)*pier+2*abutment; found=(n-1)*footing**2*1.+2*footing*width*1.
    rebar=(140*deck+150*sub+120*found)/1000
    forms_deck=width*length
    forms_sub=(n-1)*(math.pi*diameter*pier_h+width*1.5+2*(width+1.5)*1.4)+2*2*(width+1)*7
    forms_found=(n-1)*4*footing*1.+2*2*(footing+width)*1.
    direct=260*deck+240*(sub+found)+1660*rebar+50*(forms_deck+forms_sub+forms_found)+5000*4*n+2400*width*(n+1)+360*2*length+32*(width-1)*length
    total=direct*1.12*1.15; carbon=((deck+sub+found)*.320+rebar*1.4)*1.15
    return dict(n=n, depth=depth, cost=total, carbon=carbon, concrete=deck+sub+found, rebar=rebar)

reference=[]
for n in range(8,17):
    natural=max(Fraction(35,100),Fraction(120,n)/18*Fraction(11,10))
    for factor in [Fraction(1),Fraction(11,10),Fraction(12,10)]:
        depth=float(natural) if factor==1 else math.ceil(natural*factor*20)/20
        reference.append(bill(n,depth))
unique={(r['n'],round(r['depth'],10)):r for r in reference}
reference=sorted(unique.values(),key=lambda r:(r['cost'],r['carbon']))
solutions=observed['solutions']
assert len(solutions)==len(reference)
for actual, expected in zip(solutions,reference):
    result=actual['result']; assert len(result['Spans'])==expected['n']
    assert abs(result['Depth']-expected['depth'])<1e-10, (actual['Rank'], result['Depth'], expected['depth'])
    near(result['TotalCost'],expected['cost'],'cost_eur')
    near(result['Carbon'],expected['carbon'],'carbon_t')
    assert abs(result['Concrete']-expected['concrete'])<1e-7
    assert abs(result['Rebar']-expected['rebar'])<1e-7
    dominated=any(o['cost']<=expected['cost'] and o['carbon']<=expected['carbon'] and (o['cost']<expected['cost'] or o['carbon']<expected['carbon']) for o in reference)
    assert actual['Pareto']==(not dominated)

summary={'status':'PASS','beam_cases':len(observed['beams']),'controlled_grid':len(reference), 'numeric_comparisons':checks,'absolute_max_errors':errors,'independent_best':reference[0],
 'scope':'Verification of the implemented ideal equations and discrete optimum in one prescribed family. No normative or experimental validation of bridge design rules.'}
(folder/'independent-summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
print(json.dumps(summary,indent=2))
