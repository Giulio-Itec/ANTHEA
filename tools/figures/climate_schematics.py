"""Original engineering schematics; no normative artwork is copied.

Matplotlib is needed only to regenerate PNG/SVG assets, never at runtime.
Dimensions are symbolic: these figures explain conventions, not project geometry.
"""
from pathlib import Path
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, Polygon, Arc

OUT = Path(__file__).resolve().parents[2] / 'src/ANTHEA.Desktop/Assets/Wiki'
INK, BLUE, ORANGE = '#203348', '#1976a3', '#c36522'
plt.rcParams.update({'font.family': 'DejaVu Sans', 'font.size': 11, 'text.color': INK, 'svg.fonttype': 'none'})

def canvas(title, rows=1, cols=1, height=4.8):
    fig, axes = plt.subplots(rows, cols, figsize=(12, height), squeeze=False)
    fig.suptitle(title, x=.04, ha='left', fontsize=17, fontweight='bold', color=INK)
    for ax in axes.flat:
        ax.set(xlim=(-1, 11), ylim=(-1.3, 6.4)); ax.axis('off')
    fig.subplots_adjust(left=.04, right=.96, top=.84, bottom=.13, wspace=.20, hspace=.38)
    return fig, axes

def text(ax,x,y,s,**kw): ax.text(x,y,s,ha=kw.pop('ha','center'),va='center',**kw)
def line(ax,x,y,**kw): ax.plot(x,y,color=kw.pop('color',INK),lw=kw.pop('lw',1.8),**kw)
def arrow(ax,x,y,dx,dy,color=BLUE): ax.annotate('',(x+dx,y+dy),(x,y),arrowprops=dict(arrowstyle='->',color=color,lw=1.7))
def dim(ax,x1,y1,x2,y2,s):
    ax.annotate('',(x1,y1),(x2,y2),arrowprops=dict(arrowstyle='<->',color=INK,lw=.8))
    text(ax,(x1+x2)/2,(y1+y2)/2-.35,s,fontsize=10)
def save(fig,name,note):
    fig.text(.04,.035,note,fontsize=9,color=INK)
    OUT.mkdir(parents=True,exist_ok=True)
    for suffix in ('png','svg'): fig.savefig(OUT/f'clima-{name}.{suffix}',dpi=170,facecolor='white')
    plt.close(fig)

def snow():
    fig,axes=canvas('Neve · geometrie e distribuzioni sulla proiezione orizzontale',2,3,8)
    titles=['Piana','Falda singola','Due falde','Falde multiple','Cilindrica','Copertura inferiore presso salto']
    shapes=[([0,10],[1,1]),([0,10],[1,3.5]),([0,5,10],[1,3.5,1]),([0,2.5,5,7.5,10],[1,3.5,1,3.5,1]),(np.linspace(0,10,80),1+2.5*np.sin(np.linspace(0,np.pi,80))),([0,4,4,10],[3.7,3.7,1,1])]
    for i,(ax,title,(x,y)) in enumerate(zip(axes.flat,titles,shapes)):
        ax.set_title(title,fontsize=12,color=INK,pad=10);line(ax,x,y)
        if i<3:
            for xx in np.linspace(.5,9.5,10):
                yy=np.interp(xx,x,y);arrow(ax,xx,yy+1.2,0,-1)
            text(ax,5,-.15,'s = μ · Ce · Ct · sk',color=BLUE)
            if i==2:text(ax,5,5.7,'Uniforme + due casi sbilanciati',fontsize=9)
        elif i==3:
            line(ax,[2.5,5,7.5],[4.1,5.6,4.1],color=BLUE)
            for xx in np.linspace(2.8,7.2,9):arrow(ax,xx,np.interp(xx,[2.5,5,7.5],[4.1,5.6,4.1]),0,-.7)
            text(ax,5,-.15,'Accumulo nella valle; μ₂ al colmo del carico',fontsize=9)
        elif i==4:
            line(ax,[0,2.5,5,7.5,10],[4,4.8,4,5.6,4],color=BLUE);line(ax,[0,10],[4,4],lw=.7,ls='--');text(ax,5,-.15,'Due accumuli speculari; μ₃ di picco',fontsize=9)
            dim(ax,0,-.65,10,-.65,'b');dim(ax,5,1,5,3.5,'f')
        else:
            line(ax,[4,4,10],[1,5.3,2],color=BLUE);line(ax,[4,10],[2,2],color=BLUE,ls='--')
            for xx in np.linspace(4.5,9.5,8):arrow(ax,xx,np.interp(xx,[4,10],[5.3,2]),0,-.7)
            dim(ax,4,-.3,10,-.3,'b₂; estensione effettiva ≤ lₛ')
            text(ax,1.7,5.4,'h: salto; b₁: tetto superiore',fontsize=9)
    save(fig,'neve','Schemi qualitativi originali. Frecce verticali; valori e casi completi nelle tabelle. NTC §3.4; EN 1991-1-3 §5.')

def wind_walls():
    fig,axs=canvas('Vento sugli edifici · dimensioni e convenzioni',1,2)
    ax=axs[0,0];ax.set_title('Pianta · b ortogonale al vento; d parallela',fontsize=11)
    ax.add_patch(Rectangle((1,1),8,4,fc='#eaf3f7',ec=INK,lw=1.8))
    arrow(ax,-.8,3,1.5,0);text(ax,-.3,3.8,'vento',fontsize=10)
    text(ax,1.4,3,'D');text(ax,8.6,3,'E')
    for x,s in [(1.7,'A'),(3.8,'B'),(7,'C')]:text(ax,x,5.4,s);text(ax,x,.6,s)
    dim(ax,1,-.2,9,-.2,'d');dim(ax,10,1,10,5,'b')
    ax=axs[0,1];ax.set_title('Sezione · pressione normale alla superficie',fontsize=11)
    line(ax,[1,1,5,9,9],[0,2.8,4.3,2.8,0]);line(ax,[0,10],[0,0],lw=.8)
    arrow(ax,-.5,1.8,1.3,0);text(ax,0,3.1,'pe > 0',color=BLUE)
    arrow(ax,8,3.3,.9,.9,ORANGE);text(ax,8.8,5,'pe < 0',color=ORANGE)
    arrow(ax,4,1.6,-2,0,ORANGE);arrow(ax,6,1.6,2,0,ORANGE);text(ax,5,1,'pi > 0: verso l’esterno',fontsize=9)
    dim(ax,10,0,10,4.3,'h');text(ax,5,-.8,'pnet = pe − pi; + verso la superficie',fontsize=10)
    save(fig,'vento-edifici','Pareti laterali: A fino a e/5, B fino a e, C oltre e; e = min(b, 2h), troncamento alla profondità d. EN 1991-1-4 §7.2.2.')

def roof_zones(kind):
    titles={'piana':'Copertura piana','mono':'Falda singola','doppia':'Due falde'}
    fig,axs=canvas('Vento · '+titles[kind]+' · zone in pianta',1,2)
    for j,ax in enumerate(axs.flat):
        ax.set_title(('θ = 0° / 180°' if kind=='mono' else 'θ = 0°') if j==0 else ('Stessa suddivisione ruotando b e d' if kind=='piana' else 'θ = 90°'),fontsize=11)
        ax.add_patch(Rectangle((1,0),8,5,fc='#edf4f7',ec=INK,lw=1.8));arrow(ax,-.8,2.5,1.5,0)
        line(ax,[2,2],[0,5]);line(ax,[1,2],[1,1]);line(ax,[1,2],[4,4])
        text(ax,1.5,.5,'F');text(ax,1.5,2.5,'G');text(ax,1.5,4.5,'F')
        if kind=='doppia' and j==0:
            line(ax,[5,5],[0,5],ls='--');line(ax,[6,6],[0,5]);text(ax,3.5,2.5,'H');text(ax,5.5,2.5,'J');text(ax,7.5,2.5,'I');text(ax,5,5.5,'colmo',fontsize=10)
            dim(ax,5,-.5,6,-.5,'e/10')
        elif kind=='mono' and j==0:text(ax,5.5,2.5,'H')
        else:
            line(ax,[4.5,4.5],[0,5]);text(ax,3.3,2.5,'H');text(ax,6.8,2.5,'I');dim(ax,1,5.6,4.5,5.6,'e/2')
            if kind=='doppia':
                line(ax,[1,9],[2.5,2.5],ls='--');text(ax,7.4,2.8,'colmo',fontsize=9)
                for t in ax.texts:
                    if t.get_text() in ('G','H','I'):t.set_y(1.8)
            if kind=='mono':text(ax,1.5,4.5,'Fup',fontsize=9,bbox=dict(fc='white',ec='none'));text(ax,1.5,.5,'Flow',fontsize=9,bbox=dict(fc='white',ec='none'))
        dim(ax,1,-.5,2,-.5,'e/10');dim(ax,10,0,10,5,'b');text(ax,5,-1.1,'d →; e = min(b, 2h)',fontsize=10)
    save(fig,'vento-'+kind,'Schema simbolico originale: tagliare le zone ai bordi effettivi. Estremità F: e/4; mono θ=90°: Fup alla gronda alta. EN 1991-1-4 §7.2.')

def bridge_wind():
    fig,axs=canvas('Vento sui ponti · azioni per unità di lunghezza',1,2)
    ax=axs[0,0];ax.set_title('Impalcato isolato · sezione trasversale',fontsize=11)
    ax.add_patch(Rectangle((1,1.5),7,1.1,fc='#e7edf2',ec=INK,lw=1.8));line(ax,[1,8],[4,4],ls='--',lw=1)
    arrow(ax,-.8,2,1.5,0);arrow(ax,4.5,2,4.5,0);text(ax,9,2.5,'+fx',color=BLUE)
    arrow(ax,4.5,2,0,3);text(ax,5,5,'+fy',color=BLUE)
    ax.add_patch(Arc((4.5,2),3,2.5,theta1=10,theta2=290,color=ORANGE,lw=1.8));arrow(ax,5, .8,.6,.2,ORANGE);text(ax,6.5,.4,'±mz',color=ORANGE)
    dim(ax,1,-.5,8,-.5,'d');dim(ax,10,1.5,10,4,'htot')
    text(ax,2.1,4.7,'arredi / traffico',fontsize=9)
    ax=axs[0,1];ax.set_title('Impalcati affiancati · entrambi i versi del vento',fontsize=11)
    for x in [0,6]:ax.add_patch(Rectangle((x,2),4,1,fc='#e7edf2',ec=INK,lw=1.8))
    dim(ax,0,.7,4,.7,'d₁');dim(ax,6,.7,10,.7,'d₂');dim(ax,4,3.7,6,3.7,'s');dim(ax,2,5,8,5,'DG: distanza dei centri')
    arrow(ax,3,1,4,0);arrow(ax,7,4.3,-4,0);text(ax,5,-.5,'Separare: impalcati, collegamenti e pile comuni',fontsize=9)
    save(fig,'vento-ponti','fx, fy [kN/m]; mz [kN·m/m]. Invertire indipendentemente fy e mz nei quattro casi. Sagoma e applicabilità: CNR-DT 207 R1/2018 §G.11.')

def thermal(bridge):
    title='Temperatura nei ponti · componenti e movimenti' if bridge else 'Temperatura negli edifici · profilo attraverso lo spessore'
    fig,axs=canvas(title,1,2)
    ax=axs[0,0];ax.set_title('Profilo lineare: uniforme + differenziale',fontsize=11)
    for x,vals,label in [(0,[1,1],'ΔTu'),(4,[-.8,.8],'ΔTM'),(8,[.2,1.8],'somma')]:
        line(ax,[x,x],[0,4],lw=.8,ls='--');line(ax,[x+vals[0],x+vals[1]],[0,4],color=ORANGE)
        arrow(ax,x,3.7,vals[1],0,ORANGE);arrow(ax,x,.3,vals[0],0,ORANGE);text(ax,x+.5,-.6,label)
    text(ax,5,5.3,'ΔTM = Testerna/sopra − Tinterna/sotto',fontsize=10)
    text(ax,5,4.7,'Linea tratteggiata: T0; ascissa: variazione T',fontsize=9)
    ax=axs[0,1]
    if bridge:
        ax.set_title('Movimenti liberi dal punto fisso',fontsize=11)
        line(ax,[0,10],[2,2],lw=3);ax.add_patch(Polygon([[0,2],[-.5,1],[.5,1]],fc='#e7edf2',ec=INK))
        ax.add_patch(Polygon([[9,2],[8.5,1],[9.5,1]],fc='#e7edf2',ec=INK));text(ax,0,.4,'fisso');text(ax,9,.4,'mobile')
        arrow(ax,9,3,1.2,0,ORANGE);arrow(ax,9,4,-1.2,0,BLUE);dim(ax,0,5,9,5,'L efficace');text(ax,5,-.8,'ΔL = α · L · ΔTu; κ = α · ΔTM / h',fontsize=11)
    else:
        ax.set_title('Strato omogeneo · regime stazionario',fontsize=11)
        ax.add_patch(Rectangle((4,0),2,4,fc='#e7edf2',ec=INK));line(ax,[1,4,6,9],[1,1.5,3.5,3.8],color=ORANGE)
        text(ax,1,4.8,'aria interna');text(ax,9,4.8,'aria esterna');text(ax,1,-.5,'Rsi');text(ax,5,-.5,'h/λ');text(ax,9,-.5,'Rse');text(ax,5,5.6,'Resistenze termiche in serie',fontsize=10)
    save(fig,'temperatura-ponti' if bridge else 'temperatura-edifici','Schemi originali non in scala. Segno positivo: riscaldamento / faccia superiore o esterna più calda. Le deformazioni non sono reazioni vincolari.')

if __name__=='__main__':
    snow();wind_walls()
    for kind in ('piana','mono','doppia'):roof_zones(kind)
    bridge_wind();thermal(False);thermal(True)
