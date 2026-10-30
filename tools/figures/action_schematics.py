"""Original diagrams for the additional Actions sheets. Uses the common climate figure style."""
from climate_schematics import canvas, text, line, arrow, dim, save, INK, BLUE, ORANGE, Rectangle, Polygon
import numpy as np

def building():
    fig,axs=canvas('Carichi d’edificio · permanenti, distribuiti e locali',1,2)
    ax=axs[0,0]
    for bottom,height,label,colour in [(1,1.2,'G1 · soletta','#cbd6df'),(2.2,.5,'G2 · massetto','#e3ceb5'),(2.7,.3,'G2 · pavimento','#b7c7b5')]:
        ax.add_patch(Rectangle((0,bottom),10,height,fc=colour,ec=INK));text(ax,5,bottom+height/2,label,fontsize=10)
    for x in np.linspace(.5,9.5,10):arrow(ax,x,4.7,0,-1.4)
    text(ax,5,5.4,'qk · sovraccarico distribuito',color=BLUE)
    text(ax,5,0,'g = spessore × peso specifico')
    ax=axs[0,1];line(ax,[0,10],[1,1]);arrow(ax,5,4.6,0,-3.3,ORANGE);text(ax,5,5.4,'Qk · verifica locale alternativa',color=ORANGE)
    ax.add_patch(Rectangle((4.3,.9),1.4,.2,fc=ORANGE));dim(ax,4.3,.2,5.7,.2,'impronta')
    text(ax,5,-.8,'F / G: due impronte, carico Qk per ruota',fontsize=10)
    save(fig,'carichi','NTC §3.1; EN 1991-1-1. Partizioni: NTC → G2; EN, mobili → aggiunta a Q. Riduzioni per area e per piani alternative.')

def traffic():
    fig,axs=canvas('Traffico stradale · corsie convenzionali e tandem',1,2)
    ax=axs[0,0]
    for i,(y,width) in enumerate([(0,3),(3,3),(6,3),(9,1)]):
        ax.add_patch(Rectangle((y,0),width,5,fc=['#dbeaf3','#e7edf2','#edf1f5','#f4e6ce'][i],ec=INK))
        text(ax,y+width/2,5.5,['1','2','3','R'][i])
    for y in [1.5,4.5,7.5]:
        for yy in [y-.65,y+.65]:
            for x in [1.5,3]:ax.add_patch(Rectangle((yy-.2,x-.2),.4,.4,fc=ORANGE,ec=INK))
    text(ax,5,-.8,'Numerare per gravosità; R = area rimanente',fontsize=10)
    ax=axs[0,1];line(ax,[0,10],[1,1])
    for x in [3,7]:arrow(ax,x,4.2,0,-3,ORANGE);text(ax,x,4.8,'Qasse',color=ORANGE)
    dim(ax,3,.2,7,.2,'1,20 m')
    text(ax,5,5.7,'Tandem: due assi, due ruote per asse')
    arrow(ax,1,2.1,6,0);text(ax,5,2.8,'Frenatura / accelerazione ±')
    save(fig,'traffico','Schema qualitativo originale. LM1: impronta ruota 0,40 × 0,40 m. LM2/schema 2 è un caso locale alternativo, non un terzo asse del tandem.')

def distribution():
    fig,axs=canvas('Ripartizione trasversale · due modelli meccanici',1,2)
    for j,ax in enumerate(axs.flat):
        ax.set_title(['Traverso rigido · Courbon','Traverso deformabile · Fourier'][j],fontsize=12)
        line(ax,[0,10],[3,3],lw=3)
        for i,x in enumerate([1,5,9]):
            line(ax,[x,x],[1.2,3]);text(ax,x,.6,'EI'+str(i+1));arrow(ax,x,1.5,0,1)
        arrow(ax,3,5.5,0,-2.3,ORANGE);text(ax,3,5.8,'P')
        if j==0:
            line(ax,[0,10],[1.8,2.4],color=BLUE,ls='--');text(ax,5,-.5,'Spostamenti trasversali su una retta',fontsize=10)
        else:
            xx=np.linspace(0,10,80);line(ax,xx,2.3-.6*np.exp(-((xx-3)/2)**2),color=BLUE,ls='--')
            text(ax,5,-.5,'kv = EI λ⁴; kθ = GJ λ²; λ = nπ/L',fontsize=10)
    save(fig,'ripartizione','Travi prismatiche in semplice appoggio. Quote di momento nella sezione x: 100 Mi/ΣMi. Pianta e linee d’influenza del progetto nel foglio.')

def impact():
    fig,axs=canvas('Urti veicolari · forze equivalenti e punti di applicazione',1,2)
    ax=axs[0,0];ax.add_patch(Rectangle((5,0),1.5,5,fc='#dce5eb',ec=INK));line(ax,[0,10],[0,0]);arrow(ax,0,2,4.8,0,ORANGE);text(ax,2,2.7,'Fd,x');dim(ax,8,0,8,2,'h')
    text(ax,5,5.7,'Piedritto');text(ax,5,-.8,'Fd,y alternativo, non simultaneo a Fd,x',fontsize=10)
    ax=axs[0,1];ax.add_patch(Rectangle((2,4),8,1,fc='#dce5eb',ec=INK));line(ax,[0,10],[0,0]);dim(ax,9,0,9,4,'franco')
    arrow(ax,0,4.5,1.8,0,ORANGE);arrow(ax,2,2.5,5,1.2,BLUE);text(ax,4,1.6,'Intradosso: 10° verso l’alto')
    text(ax,5,5.7,'Sovrastruttura');text(ax,5,-.8,'Forza base NTC diversa da EN Tab. 4.2',fontsize=10)
    save(fig,'urti','Ad è un’azione eccezionale di progetto. La riduzione per franco usa la geometria futura netta. Barriere e ancoraggi richiedono un modello distinto.')

def combinations():
    fig,axs=canvas('Combinazioni · sorgenti, dominanti e alternative',1,2)
    for j,ax in enumerate(axs.flat):
        labels=['G1 / G2','Q dominante','Q accompagnatrici'] if j==0 else ['Caso vento +','Caso vento −','Presenza / assenza']
        for i,label in enumerate(labels):
            y=4.7-i*2;ax.add_patch(Rectangle((.3,y-.6),9.4,1.2,fc='#eaf1f5',ec=INK));text(ax,5,y,label)
        if j==0:
            text(ax,5,5.9,'γG · G + γQ · Q1 + Σ γQi · ψ0i · Qi',fontsize=10)
        else:
            text(ax,5,5.9,'Esclusione reciproca dei casi incompatibili',fontsize=10)
    save(fig,'combinazioni','Correlazione mantiene insieme le parti della stessa sorgente. Gli effetti si sommano solo per modelli lineari e per una medesima grandezza con segno.')

if __name__=='__main__':
    building();traffic();distribution();impact();combinations()
