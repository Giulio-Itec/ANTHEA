"""Attesi indipendenti degli esempi delle pagine dei metodi di Checker (refactoring F2.8, decisione E3 del 15/10/2026).

    py -3 tests/ConcreteLibraryAdapter.Checks/attesi/pagine_metodi.py          scrive pagine-metodi.json accanto allo script
    py -3 tests/ConcreteLibraryAdapter.Checks/attesi/pagine_metodi.py --check  rigenera in memoria e confronta con pagine-metodi.json

Solo libreria standard di Python. Nessun valore viene da ANTHEA o da GPCChecker.Concrete: i dati sono quelli degli esempi delle
pagine (Checker/docs/metodi, §10) e dei casi AN/SO della campagna del 25/9; i risultati vengono dalle formule della norma scritte
qui. Ogni valore stampato nella pagina è ricontrollato con il numero di cifre della pagina (assert): un ricalcolo diverso dalla
pagina ferma lo script.

- ca.ancoraggi §10, casi A-G, ramo NTC (l'adattatore di ANTHEA usa il profilo NTC 2018): η2 (A.1), fbd (A.2) con αct = 1, lb,rqd
  (A.3), α6 (A.4), lbd = max(lb,rqd; 20Ø; 150 mm) (A.5), l0 = max(α6 lb,rqd; 0,3 α6 lb,rqd; 20Ø; 200 mm) e interferro ≤ 4Ø (A.6),
  esito (A.9). I valori solo Eurocodice della pagina (lb,min del caso A, l0 del caso C) non si applicano al profilo NTC.
- AN-01/02 e SO-01/02: casi della campagna di validazione del 25/9 (supporto/SUPERATI/test/ValidazioneCA20260925/reference_extra.py,
  letto in sola lettura per i dati), C30/37, ricalcolati qui in forma chiusa con le stesse formule.
- ca.dettagli §10, profilo NTC: trave 300 × 500 (interferro (D.1), copriferro (D.2)-(D.3), As,min e As,max (D.5), staffe (D.6)) e
  pilastro 400 × 400 (D.8), con l'armatura massima nella giunzione (D.10). L'interasse del pilastro segue la regola dichiarata del
  programma (riquadro D-2, voce R12: barre allineate anche attraverso il nucleo), come la colonna «a mano» della pagina.
- ca.momento-curvatura §10: R300x500, C30/37, 3Ø20 + 2Ø16, N = 0, lembo superiore compresso, NTC 2018. Sezione netta (σs − σc) As,
  parabola-rettangolo esatta (M.2), acciaio elastico-perfettamente plastico; punto limite a εcu (o εud) e primo snervamento a εyd.
  In più, per lo stesso caso, il lembo inferiore compresso (2Ø16 tese), calcolato qui con le stesse equazioni (la pagina non lo
  riporta). Le tolleranze verso il programma sono quelle delle cause dichiarate dalla pagina (§4 e §10): ramo parabolico tabellato a
  8 tratti, mesh e residuo su N; lo scarto della tabella è calcolato qui (campo 'tabella') e la tolleranza lo contiene con margine.
"""
import hashlib
import json
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUTPUT = HERE / 'pagine-metodi.json'


def page(value, printed, decimals, what):
    """Il ricalcolo coincide con il numero stampato nella pagina, arrotondato alle sue cifre."""
    assert abs(value - printed) <= 0.5 * 10 ** -decimals + 1e-12 * abs(printed), (what, value, printed)


def fctm(fck):
    """NTC [11.2.3a] fino a C50/60."""
    return 0.30 * fck ** (2 / 3)


# ---------------------------------------------------------------- ca.ancoraggi, ramo NTC
def anchorage(phi, sigma, fctk, gamma_c, good, available, lap, percent, clear):
    eta1 = 1.0 if good else 0.7
    eta2 = 1.0 if phi <= 32 else (132 - phi) / 100                      # (A.1)
    fbd = 2.25 * eta1 * eta2 * fctk / gamma_c                           # (A.2), αct = 1
    basic = phi * sigma / (4 * fbd)                                     # (A.3)
    alpha6 = min(1.5, max(1.0, math.sqrt(percent / 25))) if lap else 1.0  # (A.4)
    if lap:
        required = max(alpha6 * basic, 0.3 * alpha6 * basic, 20 * phi, 200.0)  # (A.6)
        limit = 4 * phi
        clear_ok = clear <= limit
    else:
        required = max(basic, 20 * phi, 150.0)                         # (A.5)
        limit = None
        clear_ok = True
    length_ok = available >= required                                   # (A.9)
    return dict(Eta1=eta1, Eta2=eta2, Fbd=fbd, BasicLength=basic, Alpha6=alpha6, RequiredLength=required,
                MaximumLapClearDistance=limit, LengthPassed=length_ok, LapClearDistancePassed=clear_ok,
                Passed=length_ok and clear_ok)


def anchorage_cases():
    fyd = 450 / 1.15
    fctk25 = 0.7 * fctm(25)
    page(fyd, 391.30, 2, 'fyd'); page(fctm(25), 2.5650, 4, 'fctm C25'); page(fctk25, 1.7955, 4, 'fctk C25')
    cases = []

    def add(case_id, source, phi, sigma, fctk, fck, good, available, lap, percent, clear, entry=True):
        r = anchorage(phi, sigma, fctk, 1.5, good, available, lap, percent, clear)
        cases.append(dict(id=case_id, fonte=source, dati=dict(diametro=phi, sigma=sigma, fctk005=fctk, fck=fck, gamma_c=1.5, aderenza_buona=good,
                                                              lunghezza=available, sovrapposizione=lap, percentuale=percent, interferro=clear),
                          ingresso_completo=entry, atteso=r))
        return r

    src = 'ca.ancoraggi §10'
    # Caso A: ancoraggio con ldisp = 600 mm.
    a = add('A', src + ', caso A', 16, fyd, fctk25, 25, True, 600, False, 100, 0)
    page(a['Fbd'], 2.69321, 5, 'A fbd'); page(a['BasicLength'], 581.171, 3, 'A lb,rqd'); assert a['RequiredLength'] == a['BasicLength'] and a['Passed']
    # Caso B: sovrapposizione del 100%, s = 30 mm, ldisp = 900 mm.
    b = add('B', src + ', caso B', 16, fyd, fctk25, 25, True, 900, True, 100, 30)
    page(b['Alpha6'], 1.5, 1, 'B α6'); page(b['RequiredLength'], 871.757, 3, 'B l0'); assert b['MaximumLapClearDistance'] == 64 and b['Passed']
    # Caso C: come B con s = 70 mm: interferro NTC non soddisfatto (70 > 64).
    c = add('C', src + ', caso C', 16, fyd, fctk25, 25, True, 900, True, 100, 70)
    assert c['LengthPassed'] and not c['LapClearDistancePassed'] and not c['Passed']
    # Caso D: aderenza non buona, ancoraggio con ldisp = 600 mm.
    d = add('D', src + ', caso D', 16, fyd, fctk25, 25, False, 600, False, 100, 0)
    page(d['Fbd'], 1.88525, 5, 'D fbd'); page(d['BasicLength'], 830.245, 3, 'D lb,rqd'); assert not d['Passed']
    # Caso E: sovrapposizione del 50% (ldisp e s come nel caso B: la pagina dà solo α6 e l0).
    e = add('E', src + ', caso E (ldisp e s del caso B)', 16, fyd, fctk25, 25, True, 900, True, 50, 30)
    page(e['Alpha6'], 1.41421, 5, 'E α6'); page(e['RequiredLength'], 821.900, 3, 'E l0'); assert e['Passed']
    # Caso F: Ø8, σsd = 100 MPa, sovrapposizione del 100%: minimo di 200 mm (riquadro A-1, voce R13); ldisp = 200 mm (al limite), s = 20 mm.
    f = add('F', src + ', caso F (ldisp = 200 mm al limite, s = 20 mm)', 8, 100.0, fctk25, 25, True, 200, True, 100, 20)
    page(f['BasicLength'], 74.26, 2, 'F lb,rqd'); page(f['Alpha6'] * f['BasicLength'], 111.39, 2, 'F α6 lb,rqd'); assert f['RequiredLength'] == 200 and f['Passed']
    # Caso G: Ø40 (η2 = 0,92), σsd = fyd; nel ramo NTC lbd = max(lb,rqd; 800; 150) = lb,rqd; ldisp = 1600 mm.
    g = add('G', src + ', caso G (ramo NTC, ldisp = 1600 mm)', 40, fyd, fctk25, 25, True, 1600, False, 100, 0)
    page(g['Eta2'], 0.92, 2, 'G η2'); page(g['Fbd'], 2.4778, 4, 'G fbd'); page(g['BasicLength'], 1579.27, 2, 'G lb,rqd'); assert g['Passed']

    # AN-01/02, SO-01/02 (reference_extra.py righe 69-73): C30/37, j = 1: Ø20, σ = 400 MPa (> fyd), aderenza buona, ρ1 = 25%,
    # ldisp = lreq arrotondata ai 10 mm superiori, s = 20 mm; j = 2: Ø16, σ = 300 MPa, aderenza non buona, ρ1 = 100%, ldisp = 300 mm, s = 70 mm.
    fctk30 = 0.7 * fctm(30)
    for lap in (False, True):
        for j in (1, 2):
            phi, sigma, good, percent, clear = (20, 400.0, True, 25, 20) if j == 1 else (16, 300.0, False, 100, 70)
            probe = anchorage(phi, sigma, fctk30, 1.5, good, 1e9, lap, percent, clear)
            available = math.ceil(probe['RequiredLength'] / 10) * 10 if j == 1 else 300
            case_id = ('SO' if lap else 'AN') + '-%02d' % j
            r = add(case_id, 'campagna del 25/9, reference_extra.py, caso ' + case_id, phi, sigma, fctk30, 30, good, available, lap, percent, clear,
                    entry=sigma <= fyd)
            assert r['Passed'] == (j == 1), case_id
    return cases


# ---------------------------------------------------------------- ca.dettagli, profilo NTC
def rect_cover(b, h, bar):
    x, y, phi = bar
    return min(b / 2 - abs(x), h / 2 - abs(y)) - phi / 2


def clear_spacing(bars):
    return min(math.hypot(p[0] - q[0], p[1] - q[1]) - (p[2] + q[2]) / 2 for i, p in enumerate(bars) for q in bars[i + 1:])


def cover_required(phi_b, cmin_dur, dev, aggregate):
    return max(10.0, cmin_dur, phi_b + (5.0 if aggregate > 32 else 0.0)) + 0.0 + dev   # (D.2), Δcadd = 0, cground = 0


def common_checks(b, h, bars, cnom, link_phi, cmin_dur, dev, aggregate):
    phis = [p[2] for p in bars]
    out = [dict(chiave='ClearSpacing', nome='Interferro minimo', effettivo=clear_spacing(bars), limite=max(20.0, max(phis), aggregate + 5), esito=True),
           dict(chiave='NominalCover', nome='Copriferro nominale', effettivo=cnom, limite=cover_required(link_phi, cmin_dur, dev, aggregate), esito=True),
           dict(chiave='BarCoverMargin', nome='Margine copriferro barre longitudinali',
                effettivo=min(rect_cover(b, h, p) - cover_required(p[2], cmin_dur, dev, aggregate) for p in bars), limite=0.0, esito=True)]
    for c in out:
        c['esito'] = c['effettivo'] >= c['limite']
    return out


def area(phi):
    return math.pi * phi * phi / 4


def detailing_cases():
    fck, fyk = 25, 450
    fyd = fyk / 1.15
    f_ctm = fctm(fck)
    beam_bars = [(-97, -197, 20), (0, -197, 20), (97, -197, 20), (-100, 200, 14), (100, 200, 14)]
    b, h, cnom, link, spacing, legs = 300, 500, 35, 8, 200, 2
    ac = b * h
    checks = common_checks(b, h, beam_bars, cnom, link, 25, 10, 20)
    rho = max(0.26 * f_ctm / fyk, 0.0013)
    page(rho, 0.0014820, 7, 'ρmin')
    ast = legs * area(link) * 1000 / spacing
    page(ast, 502.65, 2, 'Ast/s')
    for face, label, zone, depth in (('Bottom', 'inferiore', [p for p in beam_bars if p[1] < 0], None), ('Top', 'superiore', [p for p in beam_bars if p[1] > 0], None)):
        a_s = sum(area(p[2]) for p in zone)
        ybar = sum(area(p[2]) * p[1] for p in zone) / a_s
        d = h / 2 - ybar if face == 'Bottom' else ybar + h / 2          # (D.4)
        checks.append(dict(chiave='MinimumTension:' + face, nome='As,min ' + label, effettivo=a_s, limite=rho * b * d, esito=True, d=d))
        checks.append(dict(chiave='MaximumTension:' + face, nome='As,max ' + label, effettivo=a_s, limite=0.04 * ac, esito=True))
        checks.append(dict(chiave='MinimumLinks:' + face, nome='Staffe minime · ' + label, effettivo=ast, limite=1.5 * b, esito=True))
        checks.append(dict(chiave='LinkSpacing:' + face, nome='Passo staffe · ' + label, effettivo=spacing, limite=min(1000 / 3, 0.8 * d), esito=True))
    checks.append(dict(chiave='CompressionBarRestraint', nome='Trattenimento barre compresse', effettivo=spacing, limite=15 * min(p[2] for p in beam_bars), esito=True))
    for c in checks:
        c['esito'] = c['effettivo'] <= c['limite'] if c['chiave'].split(':')[0] in ('MaximumTension', 'LinkSpacing', 'CompressionBarRestraint') else c['effettivo'] >= c['limite']
    by = {c['chiave']: c for c in checks}
    page(by['ClearSpacing']['effettivo'], 77, 0, 'interferro'); page(by['ClearSpacing']['limite'], 25, 0, 'interferro limite')
    page(by['NominalCover']['limite'], 35, 0, 'cnom richiesto'); page(by['BarCoverMargin']['effettivo'], 8, 0, 'margine')
    page(by['MinimumTension:Bottom']['d'], 447, 0, 'd inf'); page(by['MinimumTension:Bottom']['limite'], 198.73, 2, 'As,min inf')
    page(by['MinimumTension:Bottom']['effettivo'], 942.48, 2, 'As inf'); page(by['MinimumTension:Top']['d'], 450, 0, 'd sup')
    page(by['MinimumTension:Top']['limite'], 200.07, 2, 'As,min sup'); page(by['MinimumTension:Top']['effettivo'], 307.88, 2, 'As sup')
    page(by['MaximumTension:Bottom']['limite'], 6000, 0, 'As,max'); page(by['LinkSpacing:Bottom']['limite'], 333.33, 2, 'passo NTC')
    page(by['CompressionBarRestraint']['limite'], 210, 0, '15Ø')
    assert all(c['esito'] for c in checks)
    beam = dict(id='trave', fonte='ca.dettagli §10, trave 300 × 500 (profilo NTC)', b=b, h=h, fck=fck, fyk=fyk, fctm=f_ctm, barre=[list(p) for p in beam_bars],
                cnom=cnom, staffe=dict(diametro=link, passo=spacing, braccia=legs), NEd_kN=0, aggregato=20, cmin_dur=25, delta_c=10, sovrapposizione=False,
                controlli=checks)

    col_bars = [(x, y, 16) for y in (149, 0, -149) for x in (-149, 0, 149) if not (x == 0 and y == 0)]
    bc, hc, ned, col_spacing = 400, 400, 2000.0, 250
    acc = bc * hc
    a_s = sum(area(p[2]) for p in col_bars)
    col = common_checks(bc, hc, col_bars, cnom, link, 25, 10, 20)

    def aligned_spacing(bars):
        """Massima distanza fra barre adiacenti sulla stessa orizzontale o verticale con il punto medio nel calcestruzzo (riquadro D-2)."""
        best = 0.0
        for axis in (0, 1):
            lines = {}
            for p in bars:
                lines.setdefault(p[1 - axis], []).append(p[axis])
            for coord, values in lines.items():
                values.sort()
                for u, v in zip(values, values[1:]):
                    mid = [0.0, 0.0]; mid[axis] = (u + v) / 2; mid[1 - axis] = coord
                    if abs(mid[0]) < bc / 2 and abs(mid[1]) < hc / 2:
                        best = max(best, v - u)
        return best

    phis = [p[2] for p in col_bars]
    col += [dict(chiave='LongitudinalDiameter', nome='Diametro longitudinale', effettivo=min(phis), limite=12.0),
            dict(chiave='LongitudinalSpacing', nome='Interasse longitudinale', effettivo=aligned_spacing(col_bars), limite=300.0),
            dict(chiave='MinimumLongitudinal', nome='Armatura longitudinale minima', effettivo=a_s, limite=max(0.10 * ned * 1000 / fyd, 0.003 * acc)),
            dict(chiave='MaximumLongitudinal', nome='Armatura longitudinale massima', effettivo=a_s, limite=0.04 * acc),
            dict(chiave='LinkDiameter', nome='Diametro staffe', effettivo=float(link), limite=max(6.0, max(phis) / 4)),
            dict(chiave='LinkSpacing', nome='Passo staffe', effettivo=float(col_spacing), limite=min(250.0, 12 * min(phis)))]
    for c in col[3:]:
        c['esito'] = c['effettivo'] <= c['limite'] if c['chiave'] in ('LongitudinalSpacing', 'MaximumLongitudinal', 'LinkSpacing') else c['effettivo'] >= c['limite']
    by = {c['chiave']: c for c in col}
    page(a_s, 1608.50, 2, 'As pilastro'); page(by['MinimumLongitudinal']['limite'], 511.11, 2, 'As,min pilastro')
    page(by['LinkSpacing']['limite'], 192, 0, 'passo pilastro'); page(by['LongitudinalSpacing']['effettivo'], 298, 0, 'interasse pilastro')
    assert not by['LinkSpacing']['esito'] and all(c['esito'] for c in col if c['chiave'] != 'LinkSpacing')
    column = dict(id='pilastro', fonte='ca.dettagli §10, pilastro 400 × 400 (profilo NTC)', b=bc, h=hc, fck=fck, fyk=fyk, fctm=f_ctm, barre=[list(p) for p in col_bars],
                  cnom=cnom, staffe=dict(diametro=link, passo=col_spacing, braccia=0), NEd_kN=ned, aggregato=20, cmin_dur=25, delta_c=10, sovrapposizione=False,
                  controlli=col)
    # In giunzione: il limite 0,08 Ac (D.10) e il passo NTC invariato (la riduzione 0,6 è solo della famiglia Eurocodice, riquadro D-3).
    lap_checks = [dict(by['LinkSpacing']), dict(chiave='MaximumAtLap', nome='Armatura massima nella giunzione', effettivo=a_s, limite=0.08 * acc, esito=a_s <= 0.08 * acc)]
    page(lap_checks[-1]['limite'], 12800, 0, 'As,max giunzione')
    column_lap = dict(column, id='pilastro-giunzione', fonte='ca.dettagli §10, pilastro 400 × 400 in zona di sovrapposizione (riquadro D-4, voce R13)',
                      sovrapposizione=True, controlli=lap_checks)
    return [beam, column, column_lap]


# ---------------------------------------------------------------- ca.momento-curvatura
EPS_C2, EPS_CU = 0.002, 0.0035


def concrete_stress(e, fcd, table=False):
    """Parabola-rettangolo (M.2), compressione positiva; con table=True il ramo parabolico interpolato su 9 punti fra 0 e εc2 (pagina §4)."""
    if e <= 0:
        return 0.0
    if e <= EPS_C2:
        if table:
            k = min(int(e / (EPS_C2 / 8)), 7)
            e0, e1 = k * EPS_C2 / 8, (k + 1) * EPS_C2 / 8
            s0, s1 = concrete_stress(e0, fcd), concrete_stress(e1, fcd)
            return s0 + (s1 - s0) * (e - e0) / (e1 - e0)
        return fcd * (1 - (1 - e / EPS_C2) ** 2)
    return fcd if e <= EPS_CU * (1 + 1e-12) else 0.0


def gauss(f, a, b, n=40):
    """Gauss-Legendre a 5 punti su n sottointervalli (esatto per polinomi di grado ≤ 9 in ogni sottointervallo)."""
    nodes = [(-0.9061798459386640, 0.2369268850561891), (-0.5384693101056831, 0.4786286704993665), (0.0, 0.5688888888888889),
             (0.5384693101056831, 0.4786286704993665), (0.9061798459386640, 0.2369268850561891)]
    h = (b - a) / n
    total = 0.0
    for i in range(n):
        lo = a + i * h
        total += sum(w * f(lo + h / 2 * (1 + t)) for t, w in nodes) * h / 2
    return total


def section_response(x, kappa, b, h, bars, fcd, fyd, es, table=False):
    """Risultanti della sezione rettangolare con il lembo compresso a profondità 0, asse neutro a profondità x, curvatura kappa.
    bars: (profondità dal lembo compresso, area). Ritorna N (compressione positiva) e M rispetto a metà altezza (positivo se comprime il
    lembo considerato). Il calcestruzzo è integrato in ε con i nodi sui punti angolosi (0, εc2), quindi in modo esatto sulla parabola."""
    top = kappa * x
    breaks = [0.0] + [v for v in ([EPS_C2] if not table else [k * EPS_C2 / 8 for k in range(1, 9)]) if v < top] + [top]
    nc = mc = 0.0
    for lo, hi in zip(breaks, breaks[1:]):
        nc += b / kappa * gauss(lambda e: concrete_stress(e, fcd, table), lo, hi, 4)
        # profondità della fibra di deformazione e: z = x − e/κ; braccio rispetto a metà altezza: h/2 − z
        mc += b / kappa * gauss(lambda e: concrete_stress(e, fcd, table) * (h / 2 - (x - e / kappa)), lo, hi, 4)
    n, m = nc, mc
    strains = []
    for z, a_s in bars:
        e = kappa * (x - z)
        steel = math.copysign(min(es * abs(e), fyd), e)
        net = steel - concrete_stress(e, fcd, table)                    # sezione netta: barre interne al calcestruzzo
        n += net * a_s
        m += net * a_s * (h / 2 - z)
        strains.append(e)
    return n, m, strains


def bisect(f, lo, hi, tol=1e-12):
    flo = f(lo)
    assert flo * f(hi) < 0
    for _ in range(200):
        mid = (lo + hi) / 2
        fm = f(mid)
        if flo * fm <= 0:
            hi = mid
        else:
            lo, flo = mid, fm
        if hi - lo < tol:
            break
    return (lo + hi) / 2


def curvature_case(b, h, bars, fcd, fyd, es, eps_ud, table=False):
    """Punto limite a N = 0 (calcestruzzo a εcu o acciaio a εud) e primo snervamento (max |εs| = εyd)."""
    eyd = fyd / es
    deepest = max(z for z, _ in bars)

    def limit_plane(x):
        kappa = EPS_CU / x
        steel = max(abs(kappa * (x - z)) for z, _ in bars)
        if steel > eps_ud:                                            # rottura lato acciaio: pivot sulla barra più tesa
            kappa = eps_ud / (deepest - x)
        return kappa

    x_u = bisect(lambda x: section_response(x, limit_plane(x), b, h, bars, fcd, fyd, es, table)[0], 1e-6, h)
    k_u = limit_plane(x_u)
    n_u, m_u, s_u = section_response(x_u, k_u, b, h, bars, fcd, fyd, es, table)

    def yield_kappa(x):
        return eyd / max(abs(x - z) for z, _ in bars)

    x_y = bisect(lambda x: section_response(x, yield_kappa(x), b, h, bars, fcd, fyd, es, table)[0], 1e-6, h)
    k_y = yield_kappa(x_y)
    n_y, m_y, s_y = section_response(x_y, k_y, b, h, bars, fcd, fyd, es, table)
    assert abs(n_u) < 1e-6 and abs(n_y) < 1e-6
    return dict(x_limite=x_u, Mr_kNm=m_u / 1e6, chi_u_1_m=k_u * 1000, eps_c_limite=k_u * x_u * 1000, eps_s_limite=max(abs(e) for e in s_u) * 1000,
                x_snervamento=x_y, My_kNm=m_y / 1e6, chi_y_1_m=k_y * 1000, eps_c_snervamento=k_y * x_y * 1000, eps_s_snervamento=max(abs(e) for e in s_y) * 1000)


def curvature_cases():
    fck, fyk, es = 30, 450, 200000
    fcd, fyd = 0.85 * fck / 1.5, fyk / 1.15
    eps_ud = 0.9 * 0.10
    b, h = 300, 500
    bottom = [(-100, -202, 20), (0, -202, 20), (100, -202, 20)]
    top = [(-100, 204, 16), (100, 204, 16)]
    bars = bottom + top
    out = []
    for face, depth in (('superiore', lambda y: h / 2 - y), ('inferiore', lambda y: h / 2 + y)):
        section_bars = [(depth(y), area(phi)) for _, y, phi in bars]
        exact = curvature_case(b, h, section_bars, fcd, fyd, es, eps_ud)
        tab = curvature_case(b, h, section_bars, fcd, fyd, es, eps_ud, table=True)
        if face == 'superiore':
            page(exact['x_limite'], 68.372, 3, 'x limite'); page(exact['Mr_kNm'] * 1e6, 154687729, -3, 'Mr')
            page(exact['chi_u_1_m'] / 1000, 5.11906e-5, 10, 'χu'); page(exact['eps_s_limite'] / 1000, 0.019638, 6, 'εs1 limite')
            page(exact['x_snervamento'], 151.315, 3, 'x snervamento'); page(exact['My_kNm'] * 1e6, 147535472, -3, 'My,1')
            page(exact['chi_y_1_m'] / 1000, 6.50688e-6, 11, 'χy,1'); page(exact['eps_c_snervamento'] / 1000, 0.00098459, 8, 'εc snervamento')
        out.append(dict(id='lembo-' + face, lembo_compresso=face,
                        fonte='ca.momento-curvatura §10' if face == 'superiore' else 'stesso caso di ca.momento-curvatura §10 con il lembo inferiore compresso (calcolato qui)',
                        b=b, h=h, barre=[list(p) for p in bars], fck=fck, fyk=fyk, Es=es, fu=fyk, eps_uk_permille=100, alpha_cc=0.85, gamma_c=1.5, gamma_s=1.15,
                        N_kN=0, passi=40, campionamento='Quadratico', bisezioni=12, tolleranza_N_kN=1,
                        atteso=exact, tabella={k: tab[k] / exact[k] - 1 for k in exact}))
    return out


# Tolleranze relative verso il programma: scarto del solo ramo tabellato (campo 'tabella', calcolato qui) più un margine per la mesh e il
# residuo su N ≤ 1 kN. La pagina ca.momento-curvatura §10 riporta per la libreria scarti dal calcolo a mano di −0,008% su Mr, +0,003% su
# My,1, −0,22% su χu, −0,26% su εs1, +0,13% su χy,1 e +0,38% su εc: tolti quelli del ramo tabellato restano al più 0,007% sui momenti e
# 0,14% su curvature e deformazioni. Margini: 5·10⁻⁴ sui momenti, 3·10⁻³ su curvature e deformazioni.
MARGINS = dict(Mr_kNm=5e-4, My_kNm=5e-4, chi_u_1_m=3e-3, chi_y_1_m=3e-3, eps_s_limite=3e-3, eps_c_snervamento=3e-3)


def build():
    curves = curvature_cases()
    for c in curves:
        c['tolleranze'] = {k: abs(c['tabella'][k]) + margin for k, margin in MARGINS.items()}
    return dict(
        strumento='pagine_metodi.py',
        descrizione='Attesi indipendenti degli esempi delle pagine dei metodi di Checker (ca.ancoraggi, ca.dettagli, ca.momento-curvatura §10) '
                    'e dei casi AN/SO della campagna del 25/9, per ConcreteLibraryAdapter.Checks (sezione 13, decisione E3 del 15/10/2026).',
        unita='mm, mm², MPa, kN, kNm; curvature in 1/m, deformazioni in ‰ (unità di ANTHEA)',
        ancoraggi=anchorage_cases(), dettagli=detailing_cases(), momento_curvatura=curves, margini_momento_curvatura=MARGINS)


def main():
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8')
    data = build()
    text = json.dumps(data, indent=1, ensure_ascii=False) + '\n'
    counts = (len(data['ancoraggi']), len(data['dettagli']), len(data['momento_curvatura']))
    if '--check' in sys.argv:
        old = OUTPUT.read_text(encoding='utf-8') if OUTPUT.exists() else ''
        if old != text:
            print('FAIL: pagine-metodi.json diverso dalla rigenerazione')
            return 1
        print('PASS · pagine-metodi.json coincide con la rigenerazione (%d ancoraggi, %d dettagli, %d curve M–χ)' % counts)
        return 0
    OUTPUT.write_text(text, encoding='utf-8', newline='\n')
    print('%d ancoraggi, %d dettagli, %d curve M–χ -> %s (SHA-256 %s)' % (counts + (OUTPUT, hashlib.sha256(text.encode('utf-8')).hexdigest())))
    return 0


if __name__ == '__main__':
    sys.exit(main())
