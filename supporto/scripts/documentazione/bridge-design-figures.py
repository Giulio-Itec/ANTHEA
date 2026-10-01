"""Figures from recorded solver outputs; no fabricated illustrative results."""
from pathlib import Path
import json, sys
root=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(root/'supporto/artefatti/bridge-design-guide-20260930/plot-runtime'))
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np

root=Path(__file__).resolve().parents[3]
artifact=root/'supporto/artefatti/bridge-design-guide-20260930'
out=root/'supporto/documentazione/Bridge_Design/figure'; out.mkdir(parents=True,exist_ok=True)
cases=json.loads((artifact/'esempi/esempi.json').read_text(encoding='utf-8-sig'))
case=cases[0]
plt.rcParams.update({'font.family':'DejaVu Sans','font.size':10,'axes.spines.top':False,'axes.spines.right':False,'axes.titleweight':'bold'})
colors={'slab':'#333333','tee':'#88754a','psc_i':'#326487','psc_u':'#55a3aa','psc_box':'#153f63','fcm':'#5d436d','steel_i':'#bd6336','steel_box':'#856048','filler_beam':'#678451','orthotropic':'#ca9351'}
names={'slab':'Soletta c.a.','tee':'Travi T c.a.','psc_i':'Travi I c.a.p.','psc_u':'Travi U c.a.p.','psc_box':'Cassone c.a.p.','fcm':'Cassone variabile','steel_i':'Travi I miste','steel_box':'Cassone misto','filler_beam':'Travi incorporate','orthotropic':'Ortotropo'}
fig,ax=plt.subplots(figsize=(8,4.6),layout='constrained')
solutions=case['solutions']
for family in dict.fromkeys(s['Trial']['Family'] for s in solutions):
    rows=[s for s in solutions if s['Trial']['Family']==family]
    ax.scatter([s['Trial']['Cost']/1e6 for s in rows],[s['Trial']['Carbon'] for s in rows],s=30,color=colors.get(family),label=names.get(family,family),alpha=.8)
pareto=[s for s in solutions if s['Pareto']]
ax.scatter([s['Trial']['Cost']/1e6 for s in pareto],[s['Trial']['Carbon'] for s in pareto],s=105,facecolors='none',edgecolors='black',linewidths=1.2)
best=case['best']; baseline=case['baseline']
ax.scatter(best['TotalCost']/1e6,best['Carbon'],marker='*',s=220,color='#b07a27',edgecolors='black',label='Ottimo esplorato',zorder=4)
ax.scatter(baseline['TotalCost']/1e6,baseline['Carbon'],marker='x',s=80,color='black',label='Riferimento',zorder=4)
ax.set(xlabel='Costo totale indicativo [milioni di euro]',ylabel='CO₂ parziale [t]',title='69 geometrie distinte ammesse nella ricerca da 120 m')
ax.grid(alpha=.15);ax.legend(fontsize=8,ncol=2,loc='upper left');fig.savefig(out/'costo-co2.png',dpi=200);plt.close(fig)
fig,axes=plt.subplots(2,1,figsize=(8,4.6),sharex=True,layout='constrained')
trials=case['trials']; indexes=[t['Iteration'] for t in trials]
for ax,key,label,scale in [(axes[0],'Depth','Altezza [m]',1),(axes[1],'Cost','Costo [milioni di euro]',1e6)]:
    for t in trials:
        if t[key] is not None: ax.scatter(t['Iteration'],t[key]/scale,s=14,color=colors.get(t['Family'],'#153f63') if t['Exclusions']==[] else '#c8c8c8')
    ax.set_ylabel(label);ax.grid(alpha=.15)
values=[t['Cost']/1e6 if not t['Exclusions'] else np.inf for t in trials]
running=np.minimum.accumulate(values);running[~np.isfinite(running)]=np.nan
axes[1].plot(indexes,running,color='#111111',linewidth=1.2,label='Minimo ammesso progressivo');axes[1].legend(fontsize=8)
axes[0].set_title('Ordine di enumerazione e quote realmente valutate');axes[1].set_xlabel('Tentativo');fig.savefig(out/'traccia.png',dpi=200);plt.close(fig)
fig,ax=plt.subplots(figsize=(8,3.4),layout='constrained')
labels=['Riferimento','Ottimo 1–8 campate','Ottimo 5–8 campate','Prezzi al 80%','Prezzi al 120%']
values=[baseline['TotalCost'],best['TotalCost'],cases[3]['best']['TotalCost'],cases[5]['best']['TotalCost'],cases[6]['best']['TotalCost']]
bars=ax.barh(labels,[v/1e6 for v in values],color=['#81858a','#153f63','#b07a27','#527285','#527285']);ax.invert_yaxis()
for bar,value in zip(bars,values):ax.text(bar.get_width()+.025,bar.get_y()+bar.get_height()/2,f'{value/1e6:.3f}'.replace('.',','),va='center',fontsize=9)
ax.set_xlim(0,3.1);ax.set_xlabel('Costo totale indicativo [milioni di euro]');ax.set_title('Effetto di vincoli e scala dei prezzi');ax.grid(axis='x',alpha=.15)
fig.savefig(out/'scenari.png',dpi=200);plt.close(fig)
print(out)
