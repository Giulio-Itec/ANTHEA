# Attesi indipendenti dell'integrazione c.a. delle correzioni del 15/10/2026 (CorrezioniCa20261015.cs, prova 4c): formule di norma e un
# modello a fibre scritti qui, nessun valore letto dal codice di ANTHEA o delle librerie. Eseguire con: python -I correzioni_ca_20261015.py
# (numpy richiesto; la griglia a fibre impiega circa un minuto).
import sys, math
import numpy as np
sys.stdout.reconfigure(encoding="utf-8")

# ---------------------------------------------------------------- modello a fibre SLU (NTC 2018 §4.1.2.3.4)
# Rettangolo 300×500 C25/30, B450C, copriferro 30 e staffe Ø8: barre Ø16 con il centro a 46 mm dai bordi (x = ±104, y = ±204).
# Parabola-rettangolo fcd = 0,85·25/1,5, εc2 = 2 ‰, εcu = 3,5 ‰, CLS non reagente a trazione; acciaio elastico-perfettamente plastico
# fyd = 450/1,15, Es = 200 GPa, εud = 0,9·75 ‰ = 67,5 ‰. Compressione negativa; momenti Σσ·y e Σσ·x (segni del riferimento interno: si
# confrontano i moduli e la direzione). Piani di rottura per i campi A/B/C.
B, H = 300.0, 500.0
fcd = 0.85 * 25 / 1.5
ec2, ecu = 0.002, 0.0035
fyd, Es, eud = 450 / 1.15, 200000.0, 0.0675
A16, A20 = math.pi * 16 ** 2 / 4, math.pi * 20 ** 2 / 4
SYM = [(sx * 104.0, sy * 204.0, A16) for sx in (-1, 1) for sy in (-1, 1)]
# 2Ø16 in alto (centro a 46 mm), 3Ø20 in basso (centro a 30 + 8 + 10 = 48 mm), x = ±102 e 0 per le Ø20.
ASYM = [(-104.0, 204.0, A16), (104.0, 204.0, A16), (-102.0, -202.0, A20), (0.0, -202.0, A20), (102.0, -202.0, A20)]
n = 300
xs = (np.arange(n) + 0.5) / n * B - B / 2
ys = (np.arange(n) + 0.5) / n * H - H / 2
X, Y = np.meshgrid(xs, ys)
dA = (B / n) * (H / n)

def sig_c(e):
    c = np.clip(-e, 0, None)
    return -np.where(c > 0, np.where(c < ec2, fcd * (1 - (1 - c / ec2) ** 2), fcd), 0.0)

def sig_s(e):
    return max(-fyd, min(fyd, Es * e))

def plane(beta, c, bars):
    """Piano di rottura con la compressione dal lato di u = (cos β, sin β), asse neutro a profondità c dal lembo più compresso."""
    u = (math.cos(beta), math.sin(beta))
    tc = [px * u[0] + py * u[1] for px in (-B / 2, B / 2) for py in (-H / 2, H / 2)]
    tmax, tmin = max(tc), min(tc); h = tmax - tmin
    ds = tmax - min(bx * u[0] + by * u[1] for bx, by, _ in bars)
    if c >= h:
        d0 = (1 - ec2 / ecu) * h; kappa = ec2 / (c - d0); top = -ec2 - kappa * d0
    else:
        kappa = ecu / c; top = -ecu
        if top + kappa * ds > eud:
            kappa = eud / (ds - c); top = -kappa * c
    return u, tmax, top, kappa

def forces(beta, c, bars):
    u, tmax, top, kappa = plane(beta, c, bars)
    eps = lambda t: top + kappa * (tmax - t)
    sc = sig_c(eps(X * u[0] + Y * u[1]))
    N, Mx, My = (sc * dA).sum(), (sc * Y * dA).sum(), (sc * X * dA).sum()
    for bx, by, a in bars:
        e = eps(bx * u[0] + by * u[1]); s = sig_s(e) - float(sig_c(np.array(e)))
        N += s * a; Mx += s * a * by; My += s * a * bx
    return N, Mx, My

def at_eccentricity(beta, ecc_m, bars):
    """Punto del meridiano con |M|/(−N) = e (m) per l'asse neutro di direzione β."""
    f = lambda c: math.hypot(*forces(beta, c, bars)[1:]) / 1e6 - ecc_m * (-forces(beta, c, bars)[0] / 1e3)
    lo, hi = 5.0, 2000.0
    for _ in range(70):
        mid = 0.5 * (lo + hi)
        if f(mid) * f(lo) > 0: lo = mid
        else: hi = mid
    return forces(beta, 0.5 * (lo + hi), bars)

def eta(action, point):
    N, Mx, My = action
    return math.sqrt(N ** 2 + Mx ** 2 + My ** 2) / math.sqrt(sum((v / s) ** 2 for v, s in zip(point, (1e3, 1e6, 1e6))))

# ---------------------------------------------------------------- SLU-1: dominio 2D N–M con θ qualsiasi
# Azioni (N kN; Mx, My kNm). Flessione attorno a y (θ = 90°): asse neutro parallelo a y, β = 0; attorno a x (θ = 0°): β = 90°. Con la
# sezione doppiamente simmetrica il segno del momento non cambia il modulo (θ = 270° e 180° come 90° e 0°).
for label, action, beta in (("θ = 90°, N −1000, My 200", (-1000, 0, 200), 0.0), ("θ = 90°, N −1000, My 60", (-1000, 0, 60), 0.0),
                            ("θ = 0°, N −1000, Mx 250", (-1000, 250, 0), math.pi / 2)):
    e = math.hypot(action[1], action[2]) / -action[0]
    p = at_eccentricity(beta, e, SYM)
    print(f"SLU-1 {label}: R = ({p[0] / 1e3:.1f} kN; |M| {math.hypot(p[1], p[2]) / 1e6:.2f} kNm), η = {eta(action, p):.4f}")

# Flessione deviata: β tale che il momento resistente sia parallelo all'azione (sezione doppiamente simmetrica: compressione nel quadrante
# opposto, i momenti Σσ·y e Σσ·x hanno il segno opposto a quello dell'azione).
def oblique(action):
    e = math.hypot(action[1], action[2]) / -action[0]; target = math.atan2(action[2], action[1])
    g = lambda b: math.atan2(-at_eccentricity(b, e, SYM)[2], -at_eccentricity(b, e, SYM)[1]) - target
    lo, hi = 0.01, math.pi / 2 - 0.01
    for _ in range(45):
        mid = 0.5 * (lo + hi)
        if g(mid) * g(lo) > 0: lo = mid
        else: hi = mid
    return at_eccentricity(0.5 * (lo + hi), e, SYM)
for label, action in (("θ = 45°, N −1000, Mx = My = 120", (-1000, 120, 120)), ("θ = 30°, N −500, M 100", (-500, 100 * math.cos(math.pi / 6), 100 * math.sin(math.pi / 6)))):
    p = oblique(action)
    print(f"SLU-1 {label}: R = ({p[0] / 1e3:.1f} kN; {-p[1] / 1e6:.2f}; {-p[2] / 1e6:.2f} kNm), η = {eta(action, p):.4f}")

# Sezione 2Ø16/3Ø20, θ = 90°, N −1000, My 200: asse neutro ruotato finché Mx = 0 (momento solo attorno a y).
def asym_my_only(e=0.2):
    gx = lambda b: at_eccentricity(b, e, ASYM)[1]
    lo, hi = -0.2, 0.2
    for _ in range(45):
        mid = 0.5 * (lo + hi)
        if gx(mid) * gx(lo) > 0: lo = mid
        else: hi = mid
    return at_eccentricity(0.5 * (lo + hi), e, ASYM)
p = asym_my_only()
print(f"SLU-1 2Ø16/3Ø20 θ = 90°, N −1000, My 200: R = ({p[0] / 1e3:.1f} kN; Mx {p[1] / 1e6:.3f}; |My| {abs(p[2]) / 1e6:.2f} kNm), η = {eta((-1000, 0, 200), p):.4f}")

# ---------------------------------------------------------------- SLU-2: campo di N del dominio, forma chiusa
# Compressione uniforme a εc2 = 2 ‰ > εyd = 1,957 ‰: NRd,c = −[fcd (Ac − As) + As fyd]; trazione uniforme: NRd,t = As fyd.
As = 4 * A16
nrdc = -(fcd * (B * H - As) + As * fyd) / 1e3; nrdt = As * fyd / 1e3
print(f"SLU-2 NRd,c = {nrdc:.4f} kN, NRd,t = {nrdt:.4f} kN; |N/NRd| con N = −3000: {3000 / -nrdc:.6f}, N = +400: {400 / nrdt:.6f}")

# ---------------------------------------------------------------- SLU-4: ε0 del punto limite del M–χ al baricentro
# N = −1000 kN, θ = 0° (Mx, lembo superiore compresso): piano di rottura in campo B (εcu al lembo) con N = −1000; ε0 = ε a metà altezza.
lo, hi = 50.0, 499.0
fN = lambda c: forces(-math.pi / 2, c, SYM)[0] / 1e3 + 1000   # β = −90°: compressione dal lato −y nel riferimento interno (simmetrico)
for _ in range(70):
    mid = 0.5 * (lo + hi)
    if fN(mid) * fN(lo) > 0: lo = mid
    else: hi = mid
u, tmax, top, kappa = plane(-math.pi / 2, 0.5 * (lo + hi), SYM)
print(f"SLU-4 N = −1000 kN, θ = 0: x = {0.5 * (lo + hi):.2f} mm, ε lembo = {top * 1e3:.4f} ‰, χ = {kappa * 1e6:.4f} 1/km, "
      f"ε0 al baricentro = {(top + kappa * tmax) * 1e3:.4f} ‰ (prima la riga limite riportava ε al vertice, −3,5 ‰)")

# ---------------------------------------------------------------- FES-2: copriferro c della formula dalle barre (EN 1992-1-1 7.3.4)
# Rettangolo 300×500 C30/37 (fctm = 0,30·30^(2/3), Ecm = 22000 (38/10)^0,3), 3Ø20 libere con il centro a 70 mm dal lembo teso (copriferro
# reale 60 mm), 2Ø16 a 50 mm dal lembo compresso; N = 0, Mx = 98 kNm, SLE QP, analisi lineare fessurata (barre compresse con n − 1),
# lunga durata kt = 0,4, k1 = 0,8, k2 = 0,5, k3 = 3,4, k4 = 0,425. Copriferro nominale 30 + 8 = 38 mm.
fck = 30.0; fctm = 0.30 * fck ** (2 / 3); Ecm = 22000 * ((fck + 8) / 10) ** 0.3; ne = Es / Ecm
def crack(c, centre=70.0, M=98.0, b=300.0, h=500.0):
    As = 3 * A20; d = h - centre; As2 = 2 * A16; d2 = 50.0
    # asse neutro: b x²/2 + (n−1) As2 (x − d2) − n As (d − x) = 0
    a2, a1, a0 = b / 2, (ne - 1) * As2 + ne * As, -(ne - 1) * As2 * d2 - ne * As * d
    x = (-a1 + math.sqrt(a1 * a1 - 4 * a2 * a0)) / (2 * a2)
    I = b * x ** 3 / 3 + (ne - 1) * As2 * (x - d2) ** 2 + ne * As * (d - x) ** 2
    ss = ne * M * 1e6 * (d - x) / I
    hc = min(2.5 * (h - d), (h - x) / 3, h / 2); rho = As / (b * hc)
    de = max((ss - 0.4 * fctm / rho * (1 + ne * rho)) / Es, 0.6 * ss / Es)
    sr = 3.4 * c + 0.8 * 0.5 * 0.425 * 20 / rho
    return x, ss, hc, rho, de, sr, sr * de
for c in (38.0, 60.0):
    x, ss, hc, rho, de, sr, wk = crack(c)
    print(f"FES-2 c = {c:.0f} mm: x = {x:.3f}, σs = {ss:.3f} MPa, hc,eff = {hc:.3f}, ρp,eff = {rho:.6f}, εsm − εcm = {de:.6e}, sr,max = {sr:.3f} mm, wk = {wk:.5f} mm")
# Barre al copriferro nominale (centro a 48 mm): c delle barre = 38 mm = nominale, wk invariato.
print(f"FES-2 barre a 48 mm: wk = {crack(38.0, centre=48.0)[6]:.5f} mm")

# ---------------------------------------------------------------- FES-1: copriferro proiettato a,c
# Asse neutro nel copriferro solo se Qmax − Q della barra più vicina ≤ a(c + Ø/2); gradiente normale alla faccia inferiore: a(d) = d.
# Barre laterali Ø12 a 200 mm dal lembo teso: a,c = 30 + 8 + 12/2 = 44 mm < 200 mm, e h − x ≈ 171 mm > 44 mm: zona tesa senza armatura.
print("FES-1 a,c = 44 mm (barre Ø12); distanza della barra più vicina 200 mm")

# ---------------------------------------------------------------- ARM-3: staffe della T sull'anima
# bf 1200, bw 300, h 700, copriferro 40, staffe Ø8/150 a due bracci, ganci 2 × 10 Ø: sviluppo 2[(300 − 2·44) + (700 − 2·44)] + 20·8.
dev = 2 * ((300 - 88) + (700 - 88)) + 20 * 8
print(f"ARM-3 sviluppo {dev:.0f} mm, staffe {math.pi * 8 ** 2 / 4 * 1e-6 * dev / 1000 * (1000 / 150) * 7850:.6f} kg/m (con l'ala: "
      f"{math.pi * 8 ** 2 / 4 * 1e-6 * (2 * ((1200 - 88) + (700 - 88)) + 160) / 1000 * (1000 / 150) * 7850:.6f} kg/m)")
