"""Attesi indipendenti delle sezioni in forma chiusa (refactoring F2.1, docs/refactoring/f2.1-banco.md).

    py -3 tests/ConcreteBench.Checks/attesi/genera_attesi.py          scrive attesi.json accanto allo script
    py -3 tests/ConcreteBench.Checks/attesi/genera_attesi.py --check  rigenera in memoria e confronta con attesi.json

Solo libreria standard di Python. Nessun valore viene da ANTHEA o da GPCChecker.Concrete: gli ingressi (geometria,
materiali, azioni) sono scritti qui, i risultati vengono dalle formule sotto, con due controlli interni:
- rettangoli: le integrazioni sui poligoni coincidono con le formule chiuse dei manuali (flessione semplice elastica
  fessurata con l'equazione di secondo grado dell'asse neutro; stress-block 0,8x · ηfcd; parabola-rettangolo con
  ψ = 17/21 e δ = 99/238);
- poligoni regolari che approssimano il cerchio: integrali esatti sul poligono (teorema di Green), confrontati per
  informazione con le formule del segmento circolare del cerchio vero.

Convenzioni (le stesse dichiarate dal solutore di sezione, non i suoi risultati):
- mm, MPa, N, N·mm nei calcoli; azioni in kN e kNm nel JSON; compressione negativa;
- contorni centrati nell'origine (baricentro della sezione lorda), asse y verso l'alto, flessione attorno all'asse x;
  "superiore" = lembo y massimo compresso, "inferiore" = lembo y minimo compresso;
- calcestruzzo spostato dalle barre sottratto: barra compressa con (σs − σc(ε)) As;
- SLE: analisi lineare, calcestruzzo teso escluso, φ = 0, n = Es / Ecm con Ecm = 22000 ((fck + 8)/10)^0,3
  (EN 1992-1-1 prospetto 3.1); limiti NTC 2018 §4.1.2.2.5: 0,60 fck (rara), 0,45 fck (quasi permanente), 0,80 fyk;
- SLU: acciaio elastico-perfettamente plastico ±fyd = fyk/γs; calcestruzzo fcd = αcc fck/γc; stress-block
  (EN 1992-1-1 3.1.7(3)): σ = η fcd per ε ≤ −(1 − λ) εcu3, λ = 0,8 e η = 1 fino a C50/60; parabola-rettangolo
  (3.1.7(1)): σ = fcd [1 − (1 − ε/εc2)^2] fino a εc2 = 2 ‰, poi fcd fino a εcu2 = 3,5 ‰; rottura lato calcestruzzo
  (ε = −εcu al lembo compresso) controllata: deformazione massima delle barre tese ≤ 5 %, sotto εud = 0,9 εuk = 6,75 %
  di B450C (εuk = 7,5 %, passato anche alle sezioni di ANTHEA).
"""
import hashlib
import json
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUTPUT = HERE / 'attesi.json'

# ---------------------------------------------------------------- geometria esatta dei poligoni
GAUSS3 = [(0.5 - math.sqrt(15) / 10, 5 / 18), (0.5, 8 / 18), (0.5 + math.sqrt(15) / 10, 5 / 18)]


def area_sign(poly):
    return 0.5 * sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1] for i in range(len(poly)))


def ccw(poly):
    return poly if area_sign(poly) > 0 else list(reversed(poly))


def clip(poly, level, keep_above=True):
    """Sutherland-Hodgman con il semipiano y >= level (o y <= level)."""
    inside = (lambda p: p[1] >= level) if keep_above else (lambda p: p[1] <= level)
    out = []
    n = len(poly)
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        ia, ib = inside(a), inside(b)
        if ia:
            out.append(a)
        if ia != ib:
            t = (level - a[1]) / (b[1] - a[1])
            out.append((a[0] + t * (b[0] - a[0]), level))
    return out


def moment(poly, k):
    """Integrale esatto di y^k sul poligono (antiorario): ∮ x y^k dy, Gauss a 3 punti esatto fino al grado 5."""
    if len(poly) < 3:
        return 0.0
    total = 0.0
    n = len(poly)
    for i in range(n):
        x0, y0 = poly[i]
        x1, y1 = poly[(i + 1) % n]
        dy = y1 - y0
        if dy == 0:
            continue
        total += dy * sum(w * (x0 + t * (x1 - x0)) * (y0 + t * dy) ** k for t, w in GAUSS3)
    return total


def slab(poly, low, high):
    """Parte del poligono con low <= y <= high."""
    part = clip(poly, low, True) if low is not None else poly
    return clip(part, high, False) if high is not None and len(part) >= 3 else part


def mirror(section):
    """Sezione specchiata rispetto all'asse x: il lembo inferiore diventa superiore."""
    return dict(section, outline=ccw([(x, -y) for x, y in section['outline']]), bars=[(x, -y, d) for x, y, d in section['bars']])


def rectangle(b, h, bars):
    return dict(shape='Rettangolare', b=b, h=h, outline=ccw([(-b / 2, -h / 2), (b / 2, -h / 2), (b / 2, h / 2), (-b / 2, h / 2)]), bars=bars)


def circle(diameter, sides, bars):
    r = diameter / 2
    # Vertici come il contorno di ANTHEA (SezioneCA): angoli 2πi/n da 0, con n multiplo di 4 i vertici stanno sugli assi.
    outline = [(r * math.cos(2 * math.pi * i / sides), r * math.sin(2 * math.pi * i / sides)) for i in range(sides)]
    return dict(shape='Circolare', D=diameter, sides=sides, outline=ccw(outline), bars=bars)


def ring(count, radius, diameter, start_deg):
    return [(radius * math.cos(math.radians(start_deg + 360 * i / count)), radius * math.sin(math.radians(start_deg + 360 * i / count)), diameter) for i in range(count)]


def bar_area(d):
    return math.pi * d * d / 4


# ---------------------------------------------------------------- materiali
def ecm(fck):
    return 22000 * ((fck + 8) / 10) ** 0.3


def fctm(fck):
    return 0.3 * fck ** (2 / 3) if fck <= 50 else 2.12 * math.log(1 + (fck + 8) / 10)


# ---------------------------------------------------------------- SLE: sezione elastica fessurata
def elastic(section, fck, es, n_kn, m_knm):
    """Piano ε(y) = −κ (y − yn) con κ > 0: la deformazione decresce verso l'alto, il lembo superiore è il più compresso
    (o il meno teso). M positivo comprime il lembo superiore (M = −∫σ y dA). None se l'azione non ha soluzione con κ > 0
    (allora ce l'ha la sezione specchiata, vedi elastic_action)."""
    ec = ecm(fck)
    poly = section['outline']
    ys = [p[1] for p in poly]
    top, bottom = max(ys), min(ys)
    h = top - bottom
    bars = [(x, y, bar_area(d)) for x, y, d in section['bars']]
    big_n, big_m = n_kn * 1e3, m_knm * 1e6

    def forces(yn):
        comp = clip(poly, yn, True) if yn < top else []
        a0, a1, a2 = moment(comp, 0), moment(comp, 1), moment(comp, 2)
        fn = -ec * (a1 - yn * a0)
        fm = ec * (a2 - yn * a1)
        for x, y, a in bars:
            eff = es - (ec if y > yn else 0.0)
            fn -= eff * (y - yn) * a
            fm += eff * (y - yn) * y * a
        return fn, fm

    def g(yn):
        fn, fm = forces(yn)
        return fn * big_m - fm * big_n

    # Griglia di yn da 60 h sotto a 60 h sopra la sezione (passo 0,02 h), poi bisezione su ogni cambio di segno.
    grid = [bottom - 60 * h + i * (121 * h) / 6050 for i in range(6051)]
    roots = []
    prev = g(grid[0])
    for i in range(1, len(grid)):
        cur = g(grid[i])
        if prev == 0 or (prev < 0) != (cur < 0):
            lo, hi = grid[i - 1], grid[i]
            glo = g(lo)
            for _ in range(200):
                mid = 0.5 * (lo + hi)
                gm = g(mid)
                if (gm < 0) == (glo < 0):
                    lo, glo = mid, gm
                else:
                    hi = mid
                if hi - lo <= 1e-13 * max(1.0, abs(mid)):
                    break
            yn = 0.5 * (lo + hi)
            fn, fm = forces(yn)
            if fm != 0 and big_m / fm > 0:
                roots.append(yn)
        prev = cur
    if not roots:
        return None
    if len(roots) != 1:
        raise RuntimeError('elastica: %d soluzioni con κ > 0' % len(roots))
    yn = roots[0]
    fn, fm = forces(yn)
    kappa = big_m / fm
    # Verifica dell'equilibrio: N = κ FN.
    assert abs(kappa * fn - big_n) <= 1e-9 * max(abs(big_n), abs(big_m) / h, 1.0), (kappa * fn, big_n)

    def strain(y):
        return -kappa * (y - yn)

    sigma_top = ec * strain(top) if strain(top) < 0 else 0.0
    regime = 'parzializzata' if bottom < yn < top else ('interamente compressa' if yn <= bottom else 'interamente tesa')
    return dict(yn=yn, kappa=kappa, x=top - yn, regime=regime, sigma_c_min=sigma_top,
                sigma_s=[es * strain(y) for _, y, _ in bars], top=top, bottom=bottom)


def rectangle_bending_closed_form(b, h, bars, fck, es, m_knm):
    """Flessione semplice, lembo superiore compresso: b x²/2 + (n − 1) As' (x − d') = n As (d − x) (barre compresse sopra l'asse neutro)."""
    n = es / ecm(fck)
    top = h / 2
    # Divide le barre per lato con l'asse neutro di prova; iterazione sul solo insieme delle barre compresse.
    compressed = set()
    for _ in range(10):
        a2 = b / 2
        a1 = 0.0
        a0 = 0.0
        for i, (x, y, d) in enumerate(bars):
            a = bar_area(d)
            depth = top - y
            factor = (n - 1) if i in compressed else n
            # factor·As·(x − depth) contribuisce con il segno: compresse (x − d') > 0, tese (x − d) < 0
            a1 += factor * a
            a0 -= factor * a * depth
        x = (-a1 + math.sqrt(a1 * a1 - 4 * a2 * a0)) / (2 * a2)
        new = {i for i, (_, y, _) in enumerate(bars) if top - y < x}
        if new == compressed:
            break
        compressed = new
    inertia = b * x ** 3 / 3
    for i, (_, y, d) in enumerate(bars):
        depth = top - y
        factor = (n - 1) if i in compressed else n
        inertia += factor * bar_area(d) * (x - depth) ** 2
    m = m_knm * 1e6
    sigma_c = -m * x / inertia
    sigma_s = [n * m * ((top - y) - x) / inertia for _, y, _ in bars]
    return dict(x=x, sigma_c_min=sigma_c, sigma_s=sigma_s)


# ---------------------------------------------------------------- SLU: resistenza a rottura con N assegnato
def ultimate(section, fck, fyk, es, alpha_cc, gamma_c, gamma_s, diagram, n_kn):
    """Lembo superiore compresso con ε = −εcu; x dall'equilibrio alla traslazione; MRd attorno al baricentro (positivo)."""
    if fck > 50:
        raise ValueError('solo fck <= 50')
    fcd = alpha_cc * fck / gamma_c
    fyd = fyk / gamma_s
    ecu, ec2, lam, eta = 3.5e-3, 2e-3, 0.8, 1.0
    poly = section['outline']
    ys = [p[1] for p in poly]
    top, bottom = max(ys), min(ys)
    h = top - bottom
    bars = [(x, y, bar_area(d)) for x, y, d in section['bars']]
    target = n_kn * 1e3

    def concrete_stress(eps):
        if eps >= 0:
            return 0.0
        if diagram == 'Stress block':
            return -eta * fcd if eps <= -(1 - lam) * ecu else 0.0
        if -eps >= ec2:
            return -fcd
        return -fcd * (1 - (1 - (-eps) / ec2) ** 2)

    def resultant(x):
        yn = top - x
        k = ecu / x
        n = m = 0.0
        if diagram == 'Stress block':
            block = clip(poly, top - lam * x, True)
            a0, a1 = moment(block, 0), moment(block, 1)
            n += -eta * fcd * a0
            m += eta * fcd * a1
        else:
            y2 = yn + x * ec2 / ecu
            # tratto parabolico: σ(y) = −fcd [2u − u²], u = (y − yn) k / εc2
            c = k / ec2
            part = slab(poly, max(yn, bottom), min(y2, top)) if yn < top else []
            # 2u − u² = 2c(y − yn) − c²(y − yn)² = p0 + p1 y + p2 y²
            p0 = -2 * c * yn - c * c * yn * yn
            p1 = 2 * c + 2 * c * c * yn
            p2 = -c * c
            mom = [moment(part, i) for i in range(4)]
            n += -fcd * (p0 * mom[0] + p1 * mom[1] + p2 * mom[2])
            m += fcd * (p0 * mom[1] + p1 * mom[2] + p2 * mom[3])
            rect = clip(poly, y2, True) if y2 < top else []
            n += -fcd * moment(rect, 0)
            m += fcd * moment(rect, 1)
        strains = []
        for _, y, a in bars:
            eps = -k * (y - yn)
            strains.append(eps)
            s = max(-fyd, min(fyd, es * eps)) - concrete_stress(eps)
            n += s * a
            m -= s * a * y
        return n, m, strains

    lo, hi = 1e-6 * h, 10 * h
    flo = resultant(lo)[0] - target
    for _ in range(300):
        mid = 0.5 * (lo + hi)
        fm = resultant(mid)[0] - target
        if (fm < 0) == (flo < 0):
            lo, flo = mid, fm
        else:
            hi = mid
        if hi - lo <= 1e-14 * h:
            break
    x = 0.5 * (lo + hi)
    n, m, strains = resultant(x)
    assert abs(n - target) <= 1e-6 * max(1.0, abs(target), fyd * sum(a for _, _, a in bars) * 1e-3)
    if max(strains) > 0.05:
        raise RuntimeError('rottura non governata dal calcestruzzo: εs = %g' % max(strains))
    if diagram == 'Stress block' and lam * x > h:
        raise RuntimeError('stress-block oltre la sezione')
    return dict(x=x, MRd=m / 1e6, eps_s_max=max(strains), fcd=fcd, fyd=fyd)


def rectangle_ultimate_closed_form(b, h, bars, fck, fyk, es, alpha_cc, gamma_c, gamma_s, diagram, n_kn):
    """Rettangolo: risultante del calcestruzzo in forma chiusa (stress-block 0,8 x ηfcd a 0,4 x; parabola-rettangolo
    17/21 b x fcd a 99/238 x dal lembo); x per bisezione dell'equilibrio con le barre elastico-plastiche."""
    fcd = alpha_cc * fck / gamma_c
    fyd = fyk / gamma_s
    ecu = 3.5e-3
    top = h / 2
    psi, delta = (0.8, 0.4) if diagram == 'Stress block' else (17 / 21, 99 / 238)

    def resultant(x):
        k = ecu / x
        cc = psi * b * x * fcd
        n = -cc
        m = cc * (top - delta * x)
        for _, y, d in bars:
            eps = -k * (y - (top - x))
            if diagram == 'Stress block':
                sc = -fcd if eps <= -(1 - 0.8) * ecu else 0.0
            else:
                e = -eps
                sc = 0.0 if eps >= 0 else (-fcd if e >= 2e-3 else -fcd * (1 - (1 - e / 2e-3) ** 2))
            s = max(-fyd, min(fyd, es * eps)) - sc
            n += s * bar_area(d)
            m -= s * bar_area(d) * y
        return n, m

    target = n_kn * 1e3
    lo, hi = 1e-6 * h, min(h, h / 0.8) if diagram == 'Stress block' else h
    flo = resultant(lo)[0] - target
    for _ in range(300):
        mid = 0.5 * (lo + hi)
        fm = resultant(mid)[0] - target
        if (fm < 0) == (flo < 0):
            lo, flo = mid, fm
        else:
            hi = mid
        if hi - lo <= 1e-14 * h:
            break
    x = 0.5 * (lo + hi)
    return dict(x=x, MRd=resultant(x)[1] / 1e6)


def true_circle_segment(diameter, depth):
    """Cerchio vero: area e momento statico (rispetto al centro) del segmento di altezza 'depth' dal lembo."""
    r = diameter / 2
    c = r - depth
    theta = math.acos(max(-1.0, min(1.0, c / r)))
    area = r * r * (theta - math.sin(theta) * math.cos(theta))
    static = 2 / 3 * r ** 3 * math.sin(theta) ** 3
    return area, static


# ---------------------------------------------------------------- casi
STEEL = dict(fyk=450.0, Es=200000.0)
NTC = dict(normativa='NTC 2018', alpha_cc=0.85, gamma_c=1.5, gamma_s=1.15)
EN = dict(normativa='EN 1992-1-1', alpha_cc=1.0, gamma_c=1.5, gamma_s=1.15)

SECTIONS = {
    # 300 × 500, 3Ø20 a 50 mm dal lembo inferiore, 2Ø16 a 45 mm dal superiore: armatura asimmetrica.
    'R1': rectangle(300, 500, [(-100, -200, 20), (0, -200, 20), (100, -200, 20), (-100, 205, 16), (100, 205, 16)]),
    # 400 × 600, 4Ø20 per lembo a 50 mm: armatura simmetrica.
    'R2': rectangle(400, 600, [(x, y, 20) for y in (-250, 250) for x in (-150, -50, 50, 150)]),
    # 300 × 500 con le sole 3Ø20 inferiori: armatura semplice.
    'R3': rectangle(300, 500, [(-100, -200, 20), (0, -200, 20), (100, -200, 20)]),
    # Cerchio D = 600 come poligono di 72 lati, 8Ø20 su r = 240 a 22,5° + k·45°.
    'C1': circle(600, 72, ring(8, 240, 20, 22.5)),
    # Cerchio D = 800 come poligono di 144 lati, 12Ø16 su r = 330 a 15° + k·30°.
    'C2': circle(800, 144, ring(12, 330, 16, 15)),
}

# (id, sezione, fck, N kN, |M| kNm, segni di M): '+' comprime il lembo superiore, '−' quello inferiore.
SLE_CASES = [
    ('SLE-R1-flessione', 'R1', 30, 0, 100, '+-'), ('SLE-R1-presso', 'R1', 30, -300, 120, '+-'), ('SLE-R1-compressa', 'R1', 30, -1500, 20, '+-'),
    ('SLE-R1-tensoflessione', 'R1', 30, 200, 10, '+-'), ('SLE-R2-flessione', 'R2', 35, 0, 200, '+-'), ('SLE-R2-presso', 'R2', 35, -800, 150, '+-'),
    ('SLE-R3-flessione', 'R3', 25, 0, 80, '+'), ('SLE-C1-flessione', 'C1', 30, 0, 120, '+-'), ('SLE-C1-presso', 'C1', 30, -500, 100, '+-'),
    ('SLE-C2-presso', 'C2', 40, -1000, 250, '+-'),
]
# (id, sezione, fck, norma, diagramma, N kN, segni di M)
SLU_CASES = [
    ('SLU-R1-SB-N0', 'R1', 30, NTC, 'Stress block', 0, '+-'), ('SLU-R1-SB-N500', 'R1', 30, NTC, 'Stress block', -500, '+-'),
    ('SLU-R2-SB-N0', 'R2', 35, NTC, 'Stress block', 0, '+-'), ('SLU-R2-SB-N1000', 'R2', 35, NTC, 'Stress block', -1000, '+-'),
    ('SLU-R2-SB-EN-N0', 'R2', 35, EN, 'Stress block', 0, '+-'), ('SLU-R3-SB-N0', 'R3', 25, NTC, 'Stress block', 0, '+'),
    ('SLU-C1-SB-N0', 'C1', 30, NTC, 'Stress block', 0, '+-'), ('SLU-C1-SB-N800', 'C1', 30, NTC, 'Stress block', -800, '+-'),
    ('SLU-C2-SB-N1500', 'C2', 40, NTC, 'Stress block', -1500, '+-'),
    ('SLU-R2-PR-N0', 'R2', 35, NTC, 'Parabola-rettangolo', 0, '+-'), ('SLU-R2-PR-N1000', 'R2', 35, NTC, 'Parabola-rettangolo', -1000, '+-'),
    ('SLU-R1-PR-N500', 'R1', 30, NTC, 'Parabola-rettangolo', -500, '+-'), ('SLU-C1-PR-N800', 'C1', 30, NTC, 'Parabola-rettangolo', -800, '+-'),
]


def section_json(name):
    s = SECTIONS[name]
    out = dict(forma=s['shape'], barre=[[x, y, d] for x, y, d in s['bars']])
    if s['shape'] == 'Rettangolare':
        out.update(b_mm=s['b'], h_mm=s['h'])
    else:
        out.update(D_mm=s['D'], lati=s['sides'])
    return out


def elastic_action(section, fck, es, n_kn, m_knm):
    """Stato elastico fessurato dell'azione (N, M) con M positivo che comprime il lembo superiore: la soluzione ha la deformazione
    minima al lembo superiore (sezione data) o a quello inferiore (sezione specchiata, M cambiato di segno); una sola delle due."""
    up = elastic(section, fck, es, n_kn, m_knm)
    down = elastic(mirror(section), fck, es, n_kn, -m_knm)
    if (up is None) == (down is None):
        raise RuntimeError('elastica: soluzione non unica o assente')
    if up is not None:
        return dict(up, lembo='superiore', yn_sezione=up['yn'])
    return dict(down, lembo='inferiore', yn_sezione=-down['yn'])


def build():
    sle = []
    for case_id, name, fck, n_kn, m_knm, signs in SLE_CASES:
        s = SECTIONS[name]
        es = STEEL['Es']
        entry = dict(id=case_id, sezione=name, fck=fck, fyk=STEEL['fyk'], Es=es, **NTC, N_kN=n_kn,
                     Ecm=ecm(fck), n=es / ecm(fck), limiti=dict(sigma_c_rara=-0.6 * fck, sigma_c_qp=-0.45 * fck, sigma_s=0.8 * STEEL['fyk']), azioni=[])
        for sign in signs:
            m = m_knm if sign == '+' else -m_knm
            r = elastic_action(s, fck, es, n_kn, m)
            sigma_s = r['sigma_s']
            ratio_rara = max(abs(r['sigma_c_min']) / (0.6 * fck), max(abs(v) for v in sigma_s) / (0.8 * STEEL['fyk']))
            item = dict(M_kNm=m, lembo_meno_deformato=r['lembo'], regime=r['regime'], x_mm=r['x'], yn_mm=r['yn_sezione'],
                        sigma_c_min=r['sigma_c_min'], sigma_s=sigma_s, rapporto_rara=ratio_rara, rapporto_qp=abs(r['sigma_c_min']) / (0.45 * fck))
            # Controllo interno: flessione semplice dei rettangoli con la formula chiusa.
            if s['shape'] == 'Rettangolare' and n_kn == 0:
                sec = s if sign == '+' else mirror(s)
                cf = rectangle_bending_closed_form(s['b'], s['h'], sec['bars'], fck, es, m_knm)
                assert abs(cf['x'] - r['x']) <= 1e-9 * s['h'], (case_id, sign, cf['x'], r['x'])
                assert abs(cf['sigma_c_min'] - r['sigma_c_min']) <= 1e-9 * abs(cf['sigma_c_min'])
                assert all(abs(a - b) <= 1e-9 * max(1, abs(a)) for a, b in zip(cf['sigma_s'], sigma_s))
                item['forma_chiusa'] = 'x da b x²/2 + (n − 1) As\' (x − d\') = n As (d − x); σc = M x / Icr, σs = n M (d − x) / Icr'
            entry['azioni'].append(item)
        sle.append(entry)

    slu = []
    for case_id, name, fck, code, diagram, n_kn, signs in SLU_CASES:
        s = SECTIONS[name]
        entry = dict(id=case_id, sezione=name, fck=fck, fyk=STEEL['fyk'], Es=STEEL['Es'], **code, diagramma=diagram, N_kN=n_kn, azioni=[])
        for sign in signs:
            sec = s if sign == '+' else mirror(s)
            r = ultimate(sec, fck, STEEL['fyk'], STEEL['Es'], code['alpha_cc'], code['gamma_c'], code['gamma_s'], diagram, n_kn)
            item = dict(segno=sign, lembo_compresso='superiore' if sign == '+' else 'inferiore', x_mm=r['x'], MRd_kNm=r['MRd'] if sign == '+' else -r['MRd'],
                        eps_s_max=r['eps_s_max'])
            if s['shape'] == 'Rettangolare':
                cf = rectangle_ultimate_closed_form(s['b'], s['h'], sec['bars'], fck, STEEL['fyk'], STEEL['Es'], code['alpha_cc'], code['gamma_c'], code['gamma_s'], diagram, n_kn)
                assert abs(cf['x'] - r['x']) <= 1e-8 * s['h'], (case_id, sign, cf['x'], r['x'])
                assert abs(cf['MRd'] - r['MRd']) <= 1e-9 * abs(cf['MRd']), (case_id, sign, cf['MRd'], r['MRd'])
                item['forma_chiusa'] = 'stress-block 0,8 b x ηfcd a 0,4 x' if diagram == 'Stress block' else 'parabola-rettangolo 17/21 b x fcd a 99/238 x'
            elif diagram == 'Stress block':
                # Informativo: area compressa a parità di x sul cerchio vero (segmento circolare): scarto dovuto al poligono.
                top = max(p[1] for p in sec['outline'])
                area_p = moment(clip(sec['outline'], top - 0.8 * r['x'], True), 0)
                area_c, _ = true_circle_segment(s['D'], 0.8 * r['x'])
                item['cerchio_vero'] = dict(nota='area compressa a parità di x: poligono di %d lati / cerchio vero − 1' % s['sides'], scarto=area_p / area_c - 1)
            entry['azioni'].append(item)
        slu.append(entry)
    return dict(
        strumento='genera_attesi.py',
        descrizione='Attesi indipendenti delle sezioni in forma chiusa del banco F2.1 (ConcreteBench.Checks): SLE elastica fessurata e SLU con stress-block e parabola-rettangolo.',
        convenzioni=__doc__.split('Convenzioni')[1].strip(),
        sezioni={k: section_json(k) for k in SECTIONS},
        sle=sle, slu=slu)


def main():
    data = build()
    text = json.dumps(data, indent=1, ensure_ascii=False) + '\n'
    if '--check' in sys.argv:
        old = OUTPUT.read_text(encoding='utf-8') if OUTPUT.exists() else ''
        if old != text:
            print('attesi.json diverso dalla rigenerazione')
            return 1
        print('attesi.json coincide con la rigenerazione (%d SLE, %d SLU)' % (len(data['sle']), len(data['slu'])))
        return 0
    OUTPUT.write_text(text, encoding='utf-8', newline='\n')
    print('%d casi SLE e %d SLU -> %s (SHA-256 %s)' % (len(data['sle']), len(data['slu']), OUTPUT, hashlib.sha256(text.encode('utf-8')).hexdigest()))
    return 0


if __name__ == '__main__':
    sys.exit(main())
