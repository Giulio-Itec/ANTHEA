# Attesi indipendenti delle correzioni del 15/10/2026 (Correzioni20261015.cs): formule di norma scritte qui, nessun valore letto dal
# codice di ANTHEA o delle librerie. Eseguire con: python -I correzioni_20261015.py
import sys, math
sys.stdout.reconfigure(encoding="utf-8")

# ---------------------------------------------------------------- TAG-1
# fib MC2010 livello II (7.3-16)-(7.3-21), senza staffe. Statica delle due corde: N (trazione positiva) applicata a e dalla metà di z
# verso la corda compressa dà alla corda tesa M/z + N(1/2 − e/z). Con Δe positiva verso il lembo teso (convenzione di ANTHEA dopo
# TAG-1) la formula è N(1/2 + Δe/z), quindi Δe = −e.
def mc2010(bw, d, asl, fck, gc, dg, N, M, V, De, Es=200000.0, lever=0.9):
    z = lever * d
    ex = max(0.0, (abs(M) * 1e6 / z + abs(V) * 1e3 + N * 1e3 * (0.5 + De / z)) / (2 * Es * asl))
    kdg = max(0.75, 32 / (16 + dg))
    kv = 0.4 / (1 + 1500 * ex) * 1300 / (1000 + kdg * z)
    return kv * min(8.0, math.sqrt(fck)) * z * bw / gc / 1e3, ex

asl = 4 * math.pi * 20 ** 2 / 4   # 4 Ø20 = 1256,637 mm²
for e_to_compressed in (50.0, -50.0, 0.0):
    De = -e_to_compressed
    v, ex = mc2010(300, 460, asl, 30, 1.5, 20, -500, 100, 100, De)
    print(f"TAG-1 risultante a {e_to_compressed:+} mm verso il lembo compresso (Δe = {De:+}): εx = {ex:.10e}  VRd,c = {v:.12f} kN")

# ---------------------------------------------------------------- TAG-2
# NTC 2018 [4.1.23] senza staffe: VRd = max{[0,18 k (100 ρ fck)^(1/3)/γc + 0,15 σcp] bw d; (vmin + 0,15 σcp) bw d},
# vmin = 0,035 k^1,5 fck^0,5, σcp = NEd/Ac ≤ 0,2 fcd. Sezione 600×800, bw = 600, d = 708, Asl = 4Ø20, NEd = 500 kN, VEd = 300 kN.
def vrd_ntc(fck, bw, d, asl, ned_kn, ac, gc, acc):
    fcd = acc * fck / gc
    k = min(2.0, 1 + math.sqrt(200 / d)); rho = min(0.02, asl / (bw * d)); scp = min(ned_kn * 1e3 / ac, 0.2 * fcd)
    main = (0.18 * k * (100 * rho * fck) ** (1 / 3) / gc + 0.15 * scp) * bw * d
    vmin = (0.035 * k ** 1.5 * math.sqrt(fck) + 0.15 * scp) * bw * d
    return max(main, vmin) / 1e3

for lab, gc, acc in (("ordinari", 1.5, 0.85), ("unitari", 1.0, 1.0)):
    v = vrd_ntc(35, 600, 708, 4 * math.pi * 20 ** 2 / 4, 500, 600 * 800, gc, acc)
    print(f"TAG-2 {lab}: VRd = {v:.12f} kN, η = {300 / v:.12f}")
# NTC 2018 [4.1.41]-[4.1.42]: TRsd = 2 Ak (As/s) fyd cot θ, TRld = 2 Ak (ΣAl/uk) fyd / cot θ. Asse delle barre a 70 + 10 + 12 = 92 mm:
# Ak = (600 − 184)(800 − 184) = 256 256 mm², uk = 2(416 + 616) = 2064 mm; staffe Ø10/200, ΣAl = 1000 mm², cot θ = 1,5.
A, um = 416.0 * 616.0, 2 * (416.0 + 616.0)
for fyd in (450.0, 450 / 1.15):
    trsd = 2 * A * math.pi * 10 ** 2 / 4 / 200 * fyd * 1.5 / 1e6; trld = 2 * A * 1000 / um * fyd / 1.5 / 1e6
    print(f"TAG-2 fyd = {fyd:.6f} MPa: TRsd = {trsd:.12f} kNm, TRld = {trld:.12f} kNm")

# ---------------------------------------------------------------- ARM-1 (supporto/test/ConcreteDesign.Checks)
# NTC 2018 §4.1.2.3.5.2 [4.1.27]-[4.1.29], staffe a 90° a due bracci, N = 0 (αc = 1), f'cd = 0,5 fcd, 1 ≤ cot θ ≤ 2,5 con cot θ
# all'eguaglianza VRsd = VRcd. C35/45 (fcd = 0,85 · 35/1,5), B450C (fyd = 450/1,15). z = 0,9 d (rettangolo) o z/d assegnato (cerchio).
fcd, fyd = 0.85 * 35 / 1.5, 450 / 1.15
def vrd_staffe(bw, d, phi_st, s, lever=0.9, legs=2):
    z = lever * d
    ks = z * legs * math.pi * phi_st ** 2 / 4 / s * fyd / 1e3       # VRsd = ks cot θ
    kc = z * bw * 0.5 * fcd / 1e3                                     # VRcd = kc cot θ/(1 + cot² θ)
    cot = min(max(math.sqrt(max(kc / ks - 1, 0)), 1.0), 2.5)
    return min(ks * cot, kc * cot / (1 + cot * cot))
# Rettangolo 400×600, parametri manuali bw = 250, d = 540; d delle proposte (600 − 40 − Øst − Øl/2) ≥ 540, quindi min = 540.
for phi in (8, 10):
    for s in (100, 150):
        v = vrd_staffe(250, 540, phi, s)
        print(f"ARM-1 rettangolo bw 250 d 540 staffe Ø{phi}/{s}: VRd = {v:.12f} kN (Vy = 500: η = {500 / v:.12f}; Vy = 600: η = {600 / v:.12f})")
# Cerchio D = 600, copriferro 40: d = R + r · media(|sin θ|) delle barre del semicerchio teso, r = R − c − Øst − Øl/2, barre a θ = 2πk/n.
def d_circle(n, phi_l, phi_st, D=600.0, c=40.0):
    R = D / 2; r = R - c - phi_st - phi_l / 2
    low = [-math.sin(2 * math.pi * k / n) for k in range(n) if math.sin(2 * math.pi * k / n) < -1e-9]
    return R + r * sum(low) / len(low)
d0, d1 = d_circle(16, 24, 8), d_circle(24, 24, 10)
v = vrd_staffe(600, d1, 10, 100, lever=0.75)
print(f"ARM-1 cerchio: d(16Ø24, Ø8) = {d0:.12f} mm (manuale); d(24Ø24, Ø10) = {d1:.12f} mm; VRd(z = 0,75 d, Ø10/100) = {v:.12f} kN; η(300) = {300 / v:.12f}")

# ---------------------------------------------------------------- ARM-2 (tests/ANTHEA.Application.Tests)
# NTC 2018 §4.1.2.3.4: parabola-rettangolo (εc2 = 2 ‰, εcu = 3,5 ‰), fcd = 0,85 · 35/1,5, B450C elastico-perfettamente plastico
# fyd = 450/1,15, N = 0, flessione retta per integrazione a fibre. Trave 400×600, copriferro 40, staffe Ø8: barre Ø16 con il centro a
# 40 + 8 + 8 = 56 mm dai bordi, distribuite uniformemente su ogni faccia fra gli spigoli (x = ±144 mm). 4Ø16 a y = +244, 3Ø16 a y = −244.
def mrd_rect(bars, compressed_top, B=400.0, H=600.0, nf=6000):
    ec2, ecu, Es = 0.002, 0.0035, 200000.0
    sc = lambda e: 0.0 if e <= 0 else (fcd * (1 - (1 - e / ec2) ** 2) if e < ec2 else fcd)
    top = H / 2
    def forces(c):
        N = M = 0.0; dz = H / nf
        for i in range(nf):
            s = top - (i + 0.5) * dz; e = ecu * (c - (top - s)) / c; f = sc(e) * B * dz; N += f; M += f * s
        for y, a in bars:
            s = y if compressed_top else -y; e = ecu * (c - (top - s)) / c
            f = (max(-fyd, min(fyd, Es * e)) - (sc(e) if e > 0 else 0.0)) * a; N += f; M += f * s
        return N, M
    lo, hi = 1.0, H
    for _ in range(100):
        mid = 0.5 * (lo + hi)
        if forces(mid)[0] > 0: hi = mid
        else: lo = mid
    return forces(0.5 * (lo + hi))[1] / 1e6
a16 = math.pi * 16 ** 2 / 4
bars = [(244.0, a16)] * 4 + [(-244.0, a16)] * 3
m = mrd_rect(bars, compressed_top=False)   # 4Ø16 tesi in alto, 3Ø16 compressi in basso
print(f"ARM-2 sup. 4Ø16 / inf. 3Ø16, lembo superiore teso: MRd = {m:.6f} kNm, η(150) = {150 / m:.6f}")

# ---------------------------------------------------------------- SLU-3 (supporto/test/X.Verifiche, --ca-module)
# NTC 2018 §4.1.2.1.2: parabola-rettangolo (εc2 = 2 ‰, εcu = 3,5 ‰, fcd = 0,85 · 35/1,5), acciaio elastico-perfettamente plastico
# (fyd = 450/1,15, εud = 0,9 · 7,5 %), N = 0, sezione netta. Cerchio D 1600 come poligono inscritto di 32 lati, 16Ø24 sul raggio
# R − 70 − 10 − 12 (copriferro 70, staffe Ø10). Rottura per εcu al lembo o εud nella barra più tesa.
def circle_mrd(D, sides=32, cover=70, stir=10, nb=16, phi=24, ntop=40000):
    R = D / 2; eud = 0.9 * 0.075
    verts = [(R * math.cos(2 * math.pi * i / sides), R * math.sin(2 * math.pi * i / sides)) for i in range(sides)]
    rb = R - cover - stir - phi / 2
    bars = [(rb * math.sin(2 * math.pi * i / nb), math.pi * phi ** 2 / 4) for i in range(nb)]
    def width(y):
        xs = []
        for i in range(sides):
            (x1, y1), (x2, y2) = verts[i], verts[(i + 1) % sides]
            if (y1 - y) * (y2 - y) < 0: xs.append(x1 + (y - y1) / (y2 - y1) * (x2 - x1))
        xs.sort(); return sum(xs[k + 1] - xs[k] for k in range(0, len(xs) - 1, 2))
    sc = lambda e: 0.0 if e <= 0 else (fcd * (1 - (1 - e / 0.002) ** 2) if e < 0.002 else fcd)
    def forces(x, n):
        k = min(0.0035 / x, eud / (R - x + rb) if R - x + rb > 0 else float("inf"))
        eps = lambda y: k * (y - (R - x)); N = M = 0.0; h = x / n
        for i in range(n):
            y = R - x + (i + 0.5) * h; f = sc(eps(y)) * width(y) * h; N += f; M += f * y
        for y, a in bars:
            e = eps(y); f = (max(-fyd, min(fyd, 200000.0 * e)) - sc(e)) * a; N += f; M += f * y
        return N, M, k
    lo, hi = 1.0, 0.999 * D
    for _ in range(80):
        mid = (lo + hi) / 2
        if forces(mid, 4000)[0] > 0: hi = mid
        else: lo = mid
    N, M, k = forces((lo + hi) / 2, ntop)
    return M / 1e6, k * 1000
m, chi = circle_mrd(1600)
print(f"SLU-3 D 1600, 16Ø24, N = 0: MRd = {m:.3f} kNm, χu = {chi:.6f} 1/m")
