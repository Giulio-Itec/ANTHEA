# Attesi indipendenti della prova 4d di ConcreteLibraryAdapter.Checks (ClsTesoRisposte.cs): «CLS teso nelle risposte» del M–χ della
# sezione c.a. (opzione StressAnalysisConcreteTension = TensionStiffening di GPCChecker.Concrete, 16/10/2026). Modello a fibre scritto
# qui, nessun valore letto dal codice di ANTHEA o delle librerie; impostazione del modello indipendente dell'agente della libreria
# (scratchpad agent-concrete-tension/ref_section_ts.py), con il momento resistente a N = 0 per individuare i punti della curva.
# Eseguire con: python -I cls_teso_risposte.py (numpy richiesto; pochi secondi).
#
# Sezione: rettangolo 300 × 500, C25/30, 4Ø16 con il centro a 46 mm dai bordi (copriferro 30 + staffa Ø8 + 8: y = ±204, 2 barre per
# strato), N = 0, flessione attorno a x (θ = 0). Leggi:
# - CLS compresso: parabola-rettangolo di progetto NTC 2018 §4.1.2.1.2.1, fcd = 0,85·25/1,5, εc2 = 2 ‰, εcu = 3,5 ‰, ramo parabolico
#   analitico σ = fcd (1 − (1 − ε/εc2)²), legge predefinita di GPCModel 5.21 / GPCChecker.Concrete 0.20 (registro AG-CA-02, 24/10/2026);
#   con --tabellare il ramo tabulato con 9 punti fra 0 e εc2 e interpolazione lineare delle tabelle dei materiali europei di Model fino a
#   GPCModel 5.20 (pagina ca.momento-curvatura §4), che dava gli attesi fino al 23/10/2026: conservato per la tracciabilità;
# - CLS teso, «Trascurato»: σ = 0 (NTC §4.1.2.3.4.1, EN 1992-1-1 6.1(2)P);
# - CLS teso, «Tension stiffening»: Collins e Mitchell (1991) con valori medi, σ = Ecm ε fino a εcr = fctm/Ecm, poi σ = fctm/(1 + √(500 ε)),
#   Ecm = 22000 (fcm/10)^0,3, fcm = fck + 8, fctm = 0,30 fck^(2/3) (EN 1992-1-1 prospetto 3.1);
# - acciaio B450C elastico-perfettamente plastico, fyd = 450/1,15, Es = 200 GPa, εud = 0,9 · 75 ‰;
# - le barre sottraggono il calcestruzzo che occupano (sezione netta).
# Piano ε(y) = ε0 − χ y, y verso l'alto dal baricentro, trazione positiva: con χ > 0 il lembo inferiore è teso; M = −Σ σ y dA > 0.
# Il momento resistente MRd (N = 0) viene dal piano di rottura (εcu al lembo compresso o εud nelle barre tese) con il CLS teso nullo, come
# il dominio della libreria, che non legge l'opzione: il punto limite della curva è lo stesso con le due scelte.
# Equilibrio per bisezione annidata (ε0 per N = 0 a χ dato, poi χ per M): robusta al salto della legge a εcr (da fctm a 0,82 fctm).
import sys, math
import numpy as np
sys.stdout.reconfigure(encoding="utf-8")

b, h, fck = 300.0, 500.0, 25.0
fcm = fck + 8; Ecm = 22000 * (fcm / 10) ** 0.3; fctm = 0.30 * fck ** (2 / 3); ecr = fctm / Ecm
fcd = 0.85 * fck / 1.5; ec2, ecu = 0.002, 0.0035
Es, fyd, eud = 200000.0, 450 / 1.15, 0.9 * 0.075
As16 = math.pi * 16 ** 2 / 4
bars = [(-204.0, 2 * As16), (204.0, 2 * As16)]
tab_e = np.array([ec2 / 8 * k for k in range(9)])
tab_s = fcd * (1 - (1 - tab_e / ec2) ** 2)
TABULATED = "--tabellare" in sys.argv[1:]
print("Ramo parabolico: " + ("tabulato a 9 punti (storico, GPCModel 5.20)" if TABULATED else "analitico (predefinito da GPCModel 5.21)"))


def parabola(c):
    return np.interp(c, tab_e, tab_s) if TABULATED else fcd * (1 - (1 - np.clip(c, 0, ec2) / ec2) ** 2)

NS = 20000
ys = -h / 2 + h * (np.arange(NS) + 0.5) / NS
dA = b * h / NS

def sig_c(e, stiffening):
    e = np.asarray(e, dtype=float)
    c = np.clip(-e, 0, None)
    comp = np.where(c >= ec2, np.where(c <= ecu * (1 + 1e-12), fcd, 0.0), parabola(c))
    s = np.where(e < 0, -comp, 0.0)
    if stiffening:
        t = np.clip(e, 0, None)
        s = np.where(e > 0, np.where(t <= ecr, Ecm * t, fctm / (1 + np.sqrt(500 * t))), s)
    return s

def sig_s(e): return max(-fyd, min(fyd, Es * e))

def forces(e0, chi, stiffening):
    s = sig_c(e0 - chi * ys, stiffening)
    n, m = float((s * dA).sum()), float(-(s * ys * dA).sum())
    for y, a in bars:
        e = e0 - chi * y; sb = sig_s(e) - float(sig_c(e, stiffening)); n += sb * a; m -= sb * a * y
    return n, m

def e0_for(chi, stiffening):
    lo, hi = -0.01, 0.07
    for _ in range(55):
        mid = 0.5 * (lo + hi)
        if forces(mid, chi, stiffening)[0] > 0: hi = mid
        else: lo = mid
    return 0.5 * (lo + hi)

def response(M, stiffening):
    """χ (1/mm) per il momento M (Nmm) a N = 0, con il controllo di monotonia di M(χ) sulla griglia di partenza."""
    grid = np.geomspace(1e-8, 3e-5, 40)
    ms = [forces(e0_for(c, stiffening), c, stiffening)[1] for c in grid]
    assert all(m2 > m1 for m1, m2 in zip(ms, ms[1:]) if m2 <= M * 1.2), "M(χ) non monotono"
    k = next(i for i, m in enumerate(ms) if m >= M)
    lo, hi = grid[k - 1], grid[k]
    for _ in range(55):
        mid = 0.5 * (lo + hi)
        if forces(e0_for(mid, stiffening), mid, stiffening)[1] > M: hi = mid
        else: lo = mid
    chi = 0.5 * (lo + hi); e0 = e0_for(chi, stiffening)
    return e0, chi, max(abs(e0 - chi * y) for y, _ in bars)

def limit_moment():
    """MRd a N = 0: piano di rottura con l'asse neutro a profondità x dal lembo superiore compresso, CLS teso nullo."""
    d = h / 2 + 204
    def plane(x):
        chi = ecu / x
        if chi * (d - x) > eud: chi = eud / (d - x)
        return -chi * x + chi * h / 2, chi   # ε0 al baricentro (lembo superiore a −χ x), χ
    lo, hi = 1.0, 400.0
    for _ in range(80):
        mid = 0.5 * (lo + hi)
        if forces(*plane(mid), False)[0] > 0: lo = mid
        else: hi = mid
    e0, chi = plane(0.5 * (lo + hi))
    return forces(e0, chi, False)[1], e0, chi, 0.5 * (lo + hi)

mrd, e0u, chiu, xu = limit_moment()
print(f"Ecm = {Ecm:.4f} MPa, fctm = {fctm:.6f} MPa, εcr = {ecr:.6e}; MRd(N = 0) = {mrd / 1e6:.4f} kNm, x = {xu:.3f} mm, "
      f"χu = {chiu * 1e3:.6e} 1/m, ε0,u = {e0u * 1e3:.5f} ‰")
for frac in (0.3, 0.6):
    M = frac * mrd
    for label, ts in (("Trascurato", False), ("Tension stiffening", True)):
        e0, chi, es = response(M, ts)
        _, _, esp = response(M * 1.001, ts)
        print(f"{frac:.1f} MRd = {M / 1e6:.4f} kNm, {label}: χ = {chi * 1e3:.6e} 1/m, |εs|max = {es * 1e3:.5f} ‰, ε0 = {e0 * 1e3:.5f} ‰; "
              f"d ln εs / d ln M = {(esp / es - 1) / 0.001:.3f}")
for label, ts in (("Trascurato", False), ("Tension stiffening", True)):
    print(f"M = 40,2 kNm, {label}: |εs|max = {response(40.2e6, ts)[2] * 1e3:.4f} ‰ (pagina ca.momento-curvatura, riquadro M-3: 0,17 ‰ e 1,20 ‰)")

# Primo snervamento delle barre tese (εs = fyd/Es) sul ramo di risposta: con il tension stiffening My supera MRd, quindi nessun punto
# della curva prima del limite è snervato.
ey = fyd / Es
for label, ts in (("Trascurato", False), ("Tension stiffening", True)):
    lo, hi = 1e-8, 3e-5
    for _ in range(55):
        mid = 0.5 * (lo + hi)
        if e0_for(mid, ts) + mid * 204 > ey: hi = mid
        else: lo = mid
    chi = 0.5 * (lo + hi)
    print(f"Primo snervamento, {label}: My = {forces(e0_for(chi, ts), chi, ts)[1] / 1e6:.4f} kNm, χy = {chi * 1e3:.6e} 1/m (MRd = {mrd / 1e6:.4f} kNm)")
