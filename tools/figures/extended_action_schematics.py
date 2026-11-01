"""Original diagrams for hydraulic and subsequent Actions sheets."""
from climate_schematics import canvas, text, line, arrow, dim, save, INK, BLUE, ORANGE, Rectangle


def hydraulics():
    fig, axes = canvas('Acqua · pressioni nette e resistenza della corrente', 1, 3)
    ax = axes[0, 0]
    ax.add_patch(Rectangle((5, 0), .6, 5, fc='#dce5eb', ec=INK))
    line(ax, [0, 4.9], [4, 4], color=BLUE)
    line(ax, [5.7, 10], [2, 2], color=ORANGE)
    for y in (0.5, 1.5, 2.5, 3.5):
        arrow(ax, 5-(4-y), y, 4-y, 0)
    for y in (.5, 1.5):
        arrow(ax, 5.6+(2-y), y, -(2-y), 0, ORANGE)
    text(ax, 5, 5.8, 'Parete · livelli indipendenti', fontsize=10)
    text(ax, 5, -0.8, 'pnet(z) = psin(z) − pdes(z)', fontsize=9)
    ax = axes[0, 1]
    ax.add_patch(Rectangle((0, 2), 10, .8, fc='#dce5eb', ec=INK))
    for x in (1, 3, 5, 7, 9):
        arrow(ax, x, .5, 0, 1.4)
        arrow(ax, x, 4.2, 0, -1.3, ORANGE)
    dim(ax, 0, -.1, 10, -.1, 'L; larghezza fuori piano B')
    text(ax, 5, 5.8, 'Platea · sottospinta e acqua sopra', fontsize=10)
    text(ax, 5, -0.8, 'Forza positiva verso l’alto', fontsize=9)
    ax = axes[0, 2]
    ax.add_patch(Rectangle((7, 0), 1, 5, fc='#dce5eb', ec=INK))
    line(ax, [0, 10], [4.3, 4.3], color=BLUE)
    for y in (.6, 1.6, 2.6, 3.6):
        arrow(ax, 2, y, .8+y, 0)
    text(ax, 5, 5.8, 'Pila · corrente v(z) con segno', fontsize=10)
    text(ax, 5, -.8, 'p(z) = ½ ρ Cd v(z) |v(z)|', fontsize=9)
    save(fig, 'idraulica', 'Schemi qualitativi originali. Scenari alternativi; Cd e profili da studio documentato. Non comprende filtrazione, erosione o urti da detriti.')


def construction():
    fig, axes = canvas('Esecuzione · attivazione dei carichi e area di getto', 1, 2)
    ax = axes[0, 0]
    for y, x0, x1, label, colour in [(4, 0, 5, 'Getto · calcestruzzo fresco e casseri', BLUE), (2, 5, 10, 'Disarmo · peso permanente', ORANGE)]:
        ax.add_patch(Rectangle((x0, y), x1-x0, .5, fc=colour))
        text(ax, 5, y-.7, label, fontsize=10)
    line(ax, [0, 10], [0, 0])
    for x, label in [(0, '0'), (5, '14'), (10, '28 giorni')]:
        line(ax, [x, x], [-.15, .15]); text(ax, x, -.5, label)
    text(ax, 5, 5.8, 'Eventi all’inizio di ciascuna fase')
    ax = axes[0, 1]
    ax.add_patch(Rectangle((0, 0), 10, 5, fc='#eaf3f7', ec=INK))
    ax.add_patch(Rectangle((1, 1), 3, 3, fc='#f4d5b9', ec=ORANGE, lw=2))
    text(ax, 2.5, 2.5, 'Area\ndi lavoro', fontsize=10)
    text(ax, 7, 3.4, 'Peso getto\n+ casseri', fontsize=10)
    text(ax, 7, 1.6, '+ carico di lavoro\nesterno', fontsize=10)
    text(ax, 5, 5.8, 'Pianta · incremento locale senza duplicazioni')
    save(fig, 'fasi-costruttive', 'Schema originale. Le fasi definiscono le azioni e i tempi; vincoli, puntellazione, redistribuzione e verifiche della struttura richiedono un modello dedicato.')




def time_effects():
    fig, axes = canvas('Calcestruzzo nel tempo · deformazione libera e vincolo assiale', 1, 2)
    ax = axes[0, 0]
    ax.add_patch(Rectangle((1, 2.2), 7, 1.1, fc='#dce5eb', ec=INK))
    line(ax, [1, 1], [1.5, 4]); dim(ax, 1, .9, 8, .9, 'L · configurazione priva di tensioni a tref')
    arrow(ax, 8, 2.75, 1.5, 0, BLUE)
    text(ax, 5, 5.5, 'Barra libera: N = P', fontsize=11)
    text(ax, 5, 4.4, 'ε = Δεcs + Σ Δσ J(t, ti)', fontsize=11)
    text(ax, 5, -.4, 'Carico a ti · salto elastico e sviluppo viscoso', fontsize=9)
    ax = axes[0, 1]
    ax.add_patch(Rectangle((0, 2.2), 5, 1.1, fc='#dce5eb', ec=INK))
    line(ax, [0, 0], [1.5, 4]); line(ax, [9.5, 9.5], [1.5, 4])
    line(ax, [5, 5.7, 6.1, 6.5, 6.9, 7.3, 7.7, 8.1, 8.5, 9.5], [2.75, 2.75, 3.2, 2.3, 3.2, 2.3, 3.2, 2.3, 2.75, 2.75], color=ORANGE)
    arrow(ax, 5, 3.8, 1.2, 0, BLUE);text(ax, 5.7, 4.3, 'P', fontsize=10)
    arrow(ax, 9.5, 4.4, .7, 0, ORANGE);text(ax, 9.5, 5.1, 'δ', fontsize=11)
    text(ax, 5, 5.8, 'Molla K o distanza imposta', fontsize=11)
    text(ax, 5, .5, 'N = P + K(δ − u) · u = L ε', fontsize=11)
    text(ax, 5, -.4, 'Distanza imposta: u = δ; libero: K = 0', fontsize=9)
    save(fig, 'effetti-differiti', 'Schema originale. Asta assiale integra a temperatura e umidità costanti. Le reazioni richiedono compatibilità e storia; la fessurazione non è simulata.')




def prestressing():
    fig, axes = canvas('Precompressione · profilo del cavo e stati successivi', 1, 2)
    ax = axes[0, 0]
    ax.add_patch(Rectangle((0, 1), 10, 3.8, fc='#edf1f4', ec=INK))
    x = [n/10 for n in range(101)]; y = [4-.09*v*(10-v) for v in x]
    line(ax, x, y, color=BLUE, lw=2.5)
    arrow(ax, .5, 4, -1.3, 0, ORANGE);text(ax, 1, 5.5, 'Martinetto', fontsize=10)
    line(ax, [0, 10], [3, 3], color='#7c8b99', ls='--')
    dim(ax, 0, .2, 10, .2, 'x · L orizzontale; s lungo il cavo')
    dim(ax, 5, 1.75, 5, 3, 'e')
    text(ax, 7.5, 5.5, 'z positivo verso l’alto', fontsize=10)
    text(ax, 5, -.6, 'σ(s) = σjack exp[−μ(θ + k s)]', fontsize=11)
    ax = axes[0, 1]
    for pos, width, label, colour in [(5, 10, 'Tiro al martinetto', BLUE), (3.9, 9, 'Dopo attrito e rientro', ORANGE), (2.8, 8, 'Dopo trasferimento · σpm0', '#218463'), (1.7, 6.7, 'Dopo perdite differite · σeff', '#C02672')]:
        ax.add_patch(Rectangle((0, pos-.3), width, .38, fc=colour))
        text(ax, 0, pos+.35, label, ha='left', fontsize=10)
    text(ax, 5, .3, 'Perdite differite accoppiate:', fontsize=10)
    text(ax, 5, -.4, 'ritiro + viscosità + rilassamento', fontsize=10)
    save(fig, 'precompressione', 'Schema originale qualitativo. Profilo piano, sezione costante e tensioni del cls da analisi strutturale. Il trasferimento a Concrete usa lo stato efficace senza duplicare N e M.')


if __name__ == '__main__':
    import sys
    choices = {'idraulica': hydraulics, 'fasi': construction, 'tempo': time_effects, 'precompressione': prestressing}
    for name in sys.argv[1:] or choices:
        choices[name]()
