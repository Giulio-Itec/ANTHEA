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


if __name__ == '__main__':
    hydraulics()
