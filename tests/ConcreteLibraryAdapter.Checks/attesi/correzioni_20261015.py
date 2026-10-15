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
