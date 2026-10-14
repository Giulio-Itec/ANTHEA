"""Attesi indipendenti delle sezioni in c.a. di supporto/test/casi_confronto.json.

Ricostruzione in Python del programma originale (RCSectionEngine e sezione_elastica.py) da cui provengono gli attesi
delle sezioni rettangolari e circolari: fibre, legami di calcolo, curva di interazione campionata, fattore di carico,
metodo n fessurato, limite elastico e domini. Non importa nulla di ANTHEA e non legge risultati del C#.

La fedeltà al programma originale si prova sui suoi stessi risultati:
  --valida <casi.json>   confronta con gli attesi originali i casi senza «fonte_atteso» (rettangolari e circolari) e,
                         con --barre-laterali originale, i 30 casi a T del commit 51e1c114 (prima della rigenerazione);
  --confronta <casi.json> confronta i casi a T con «fonte_atteso» con la regola attuale delle barre laterali;
  --scrivi <casi.json>   sostituisce gli attesi dei casi a T con «fonte_atteso» con i valori di questo programma.

La regola attuale delle barre laterali delle sezioni a T (decisione di ANTHEA, ConcreteEnhancementChecks): n barre per
lato ripartite in parti uguali fra l'angolo inferiore e l'angolo superiore della staffa d'anima, l'ultima coppia
nell'ala. Il programma originale le ripartiva in n + 1 parti fino all'intradosso dell'ala.

Tolleranza dei confronti: la stessa di supporto/test/X.Verifiche (|Δ| <= 1e-8 + 1e-10·|atteso|), testi uguali.
"""
import argparse
import json
import math
import sys

TOL_ABS, TOL_REL = 1e-8, 1e-10


def fsum_neumaier(values):
    """Somma compensata (Neumaier), come sum() delle versioni Python recenti."""
    total, correction = 0.0, 0.0
    for value in values:
        nxt = total + value
        if abs(total) >= abs(value):
            correction += (total - nxt) + value
        else:
            correction += (value - nxt) + total
        total = nxt
    return total + correction


def number(value):
    if isinstance(value, bool):
        raise ValueError("booleano al posto di un numero")
    if isinstance(value, (int, float)):
        return float(value)
    return float(str(value).strip().replace(",", "."))


def flag(value):
    if isinstance(value, bool):
        return value
    return str(value).strip().lower() in ("true", "sì", "si", "1")


# Valori predefiniti del programma originale (180 lati del contorno circolare: i casi del 25/09 non li indicano).
DEFAULTS = {
    "shape": "Rettangolare", "diameter_mm": "1000", "circular_sides": "180", "width_mm": "600", "height_mm": "800",
    "flange_width_mm": "1200", "web_width_mm": "400", "flange_thickness_mm": "250", "cover_mm": "70",
    "longitudinal_bar_count": "16", "longitudinal_bar_diameter_mm": "24", "top_bar_count": "4", "top_bar_diameter_mm": "20",
    "bottom_bar_count": "6", "bottom_bar_diameter_mm": "24", "side_bar_count_per_side": "2", "side_bar_diameter_mm": "16",
    "transverse_bar_diameter_mm": "10", "transverse_spacing_mm": "200", "apply_pile_requirements": False,
    "dissipative_zone": False, "fck_mpa": "35", "fyk_mpa": "450", "alpha_cc": "0.85", "gamma_c": "1.50", "gamma_s": "1.15",
    "steel_modulus_mpa": "200000", "apply_minimum_eccentricity": False, "minimum_eccentricity_mm": "0",
}


def concrete_parameters(fck):
    """εc2, εcu e esponente della parabola (EN 1992-1-1, prospetto 3.1)."""
    if fck <= 50:
        return 0.002, 0.0035, 2.0
    ratio = (90 - fck) / 100
    return (2 + 0.085 * math.pow(fck - 50, 0.53)) / 1000, (2.6 + 35 * math.pow(ratio, 4)) / 1000, 1.4 + 23.4 * math.pow(ratio, 4)


class Section:
    """Sezione a fibre: mm, MPa, kN, kNm; compressione positiva; Mx = ΣF·y, My = −ΣF·x."""

    def __init__(self, parameters, radial=28, angular=96, side_rule="attuale"):
        p = dict(DEFAULTS)
        p.update(parameters)
        self.p = p
        shape = str(p["shape"]).strip().lower().replace("-", " ")
        self.shape = {"circolare": "Circolare", "rettangolare": "Rettangolare", "a t": "A T", "t": "A T"}.get(shape, p["shape"])
        if self.shape not in ("Circolare", "Rettangolare", "A T"):
            raise ValueError("forma non gestita: " + str(p["shape"]))
        if flag(p.get("foro_presente", False)) or p.get("barre_manuali"):
            raise ValueError("fori e barre manuali non gestiti da questa ricostruzione")
        self.side_rule = side_rule
        self.fcd = self.v("alpha_cc") * self.v("fck_mpa") / self.v("gamma_c")
        self.fyd = self.v("fyk_mpa") / self.v("gamma_s")
        self.es = self.v("steel_modulus_mpa")
        self.eps_c2, self.eps_cu, self.parabola_n = concrete_parameters(self.v("fck_mpa"))
        self.radius = self.v("diameter_mm") / 2 if self.shape == "Circolare" else 0.0
        self.bar_radius = 0.0
        self.centroid_y = 0.0
        self.outline, self.fibers, self.bars = [], [], []
        self.circular_sides = int(number(p["circular_sides"]))
        self._geometry(max(12, radial), max(36, angular))
        xs = [q[0] for q in self.outline]
        ys = [q[1] for q in self.outline]
        self.width = max(xs) - min(xs)
        self.height = max(ys) - min(ys)
        self._bars()
        self.area_steel = fsum_neumaier(b[2] for b in self.bars)

    def v(self, key):
        if key == "transverse_bar_diameter_mm" and str(self.p.get("staffe_presenti", "Sì")) == "No":
            return 0.0
        return number(self.p[key])

    # ---------------------------------------------------------------- geometria
    def _rectangle_fibers(self, xmin, xmax, ymin, ymax, nx, ny, shift=0.0):
        dx, dy = (xmax - xmin) / nx, (ymax - ymin) / ny
        area = dx * dy
        for iy in range(ny):
            for ix in range(nx):
                self.fibers.append((xmin + (ix + 0.5) * dx, ymin + (iy + 0.5) * dy - shift, area))

    def _geometry(self, nr, na):
        if self.shape == "Circolare":
            r_out, sides = self.radius, self.circular_sides
            for i in range(sides):
                self.outline.append([r_out * math.cos(2 * math.pi * i / sides), r_out * math.sin(2 * math.pi * i / sides)])
            for ir in range(nr):
                r1, r2 = r_out * ir / nr, r_out * (ir + 1) / nr
                r = math.sqrt((r1 * r1 + r2 * r2) / 2)
                area = math.pi * (r2 * r2 - r1 * r1) / na
                for ia in range(na):
                    angle = 2 * math.pi * (ia + 0.5) / na
                    self.fibers.append((r * math.cos(angle), r * math.sin(angle), area))
            self.area_concrete = math.pi * r_out * r_out
            return
        h = self.v("height_mm")
        nx, ny = max(16, 2 * nr), max(16, na // 2)
        if self.shape == "Rettangolare":
            b = self.v("width_mm")
            self.outline.extend([[-b / 2, -h / 2], [b / 2, -h / 2], [b / 2, h / 2], [-b / 2, h / 2]])
            self._rectangle_fibers(-b / 2, b / 2, -h / 2, h / 2, nx, ny)
            self.area_concrete = b * h
            return
        bf, bw, hf = self.v("flange_width_mm"), self.v("web_width_mm"), self.v("flange_thickness_mm")
        af, hw = bf * hf, h - hf
        aw = bw * hw
        yf, yw = h / 2 - hf / 2, -h / 2 + hw / 2
        self.centroid_y = cy = (af * yf + aw * yw) / (af + aw)
        raw = [[-bf / 2, h / 2], [bf / 2, h / 2], [bf / 2, h / 2 - hf], [bw / 2, h / 2 - hf], [bw / 2, -h / 2], [-bw / 2, -h / 2],
               [-bw / 2, h / 2 - hf], [-bf / 2, h / 2 - hf]]
        self.outline.extend([[q[0], q[1] - cy] for q in raw])
        self._rectangle_fibers(-bf / 2, bf / 2, h / 2 - hf, h / 2, nx, max(6, round(ny * hf / h)), cy)
        self._rectangle_fibers(-bw / 2, bw / 2, -h / 2, h / 2 - hf, max(12, round(nx * bw / bf)), max(8, round(ny * hw / h)), cy)
        self.area_concrete = af + aw

    def _layer(self, count, half_width, y, diameter):
        if half_width <= 0:
            raise ValueError("strato senza spazio")
        area = math.pi * diameter * diameter / 4
        for i in range(count):
            self.bars.append((-half_width + 2 * half_width * i / (count - 1), y, area, diameter))

    def _bars(self):
        cover, transverse = self.v("cover_mm"), self.v("transverse_bar_diameter_mm")
        if self.shape == "Circolare":
            diameter = self.v("longitudinal_bar_diameter_mm")
            area = math.pi * diameter * diameter / 4
            count = int(self.v("longitudinal_bar_count"))
            self.bar_radius = self.radius - cover - transverse - diameter / 2
            for i in range(count):
                self.bars.append((self.bar_radius * math.cos(2 * math.pi * i / count), self.bar_radius * math.sin(2 * math.pi * i / count), area, diameter))
            if flag(self.p.get("second_inner_enabled", False)):
                d = self.v("second_inner_diameter")
                r = self.bar_radius - (diameter + d) / 2 - self.v("second_inner_gap")
                n = int(self.v("second_inner_count"))
                for i in range(n):
                    self.bars.append((r * math.cos(2 * math.pi * i / n), r * math.sin(2 * math.pi * i / n), math.pi * d * d / 4, d))
            return
        rect = self.shape == "Rettangolare"
        h, tp, bp = self.v("height_mm"), self.v("top_bar_diameter_mm"), self.v("bottom_bar_diameter_mm")
        tw = (self.v("width_mm") if rect else self.v("flange_width_mm")) / 2 - cover - transverse - tp / 2
        half_side = (self.v("width_mm") if rect else self.v("web_width_mm")) / 2
        bw = half_side - cover - transverse - bp / 2
        side_top = h / 2 if rect else h / 2 - self.v("flange_thickness_mm")
        ty = h / 2 - cover - transverse - tp / 2 - self.centroid_y
        by = -h / 2 + cover + transverse + bp / 2 - self.centroid_y
        self._layer(int(self.v("top_bar_count")), tw, ty, tp)
        self._layer(int(self.v("bottom_bar_count")), bw, by, bp)
        if rect:
            for layer in ("top", "bottom"):
                if flag(self.p.get("second_" + layer + "_enabled", False)):
                    d, gap = self.v("second_" + layer + "_diameter"), self.v("second_" + layer + "_gap")
                    y = ty - (tp + d) / 2 - gap if layer == "top" else by + (bp + d) / 2 + gap
                    self._layer(int(self.v("second_" + layer + "_count")), self.v("width_mm") / 2 - cover - transverse - d / 2, y, d)
        if self.shape == "A T" and number(self.p.get("flange_bottom_count", 0)) > 0:
            raise ValueError("fila all'intradosso dell'ala non gestita")
        sides = int(self.v("side_bar_count_per_side"))
        if sides == 0:
            return
        phi = self.v("side_bar_diameter_mm")
        offset = cover + transverse + phi / 2
        sx = half_side - offset
        low = -h / 2 + offset - self.centroid_y
        tee_now = self.shape == "A T" and self.side_rule == "attuale"
        # Regola attuale delle sezioni a T: fino all'angolo superiore della staffa d'anima, n parti; altrimenti n + 1 parti
        # fino al lembo superiore (rettangolo) o all'intradosso dell'ala (regola del programma originale per la T).
        high = (h / 2 if tee_now else side_top) - offset - self.centroid_y
        parts = sides if tee_now else sides + 1
        area = math.pi * phi * phi / 4
        for i in range(sides):
            y = low + (i + 1) * (high - low) / parts
            self.bars.append((-sx, y, area, phi))
            self.bars.append((sx, y, area, phi))

    # ---------------------------------------------------------------- materiali
    def concrete_stress(self, strain):
        if strain <= 0:
            return 0.0
        if strain < self.eps_c2:
            return self.fcd * (1 - math.pow(1 - min(max(strain / self.eps_c2, 0.0), 1.0), self.parabola_n))
        return self.fcd

    def steel_stress(self, strain):
        return min(max(self.es * strain, -self.fyd), self.fyd)

    def pure_compression(self):
        return (self.fcd * (self.area_concrete - self.area_steel) + self.fyd * self.area_steel) / 1000

    def pure_tension(self):
        return -self.fyd * self.area_steel / 1000

    # ---------------------------------------------------------------- SLU
    def supports(self, direction):
        if self.shape == "Circolare":
            return -self.radius, self.radius
        s, c = math.sin(direction), math.cos(direction)
        coords = [q[1] * c - q[0] * s for q in self.outline]
        return min(coords), max(coords)

    def profile(self, neutral_depth, direction):
        s, c = math.sin(direction), math.cos(direction)
        low, high = self.supports(direction)
        section_depth = max(high - low, 1e-9)
        depth = max(neutral_depth, section_depth * 1e-8)
        neutral = high - depth
        scale = self.eps_cu / depth
        axial = moment = 0.0
        for x, y, a in self.fibers:
            u = y * c - x * s
            force = self.concrete_stress(scale * (u - neutral)) * a
            axial += force
            moment += force * u
        for x, y, a, _ in self.bars:
            u = y * c - x * s
            strain = scale * (u - neutral)
            force = (self.steel_stress(strain) - self.concrete_stress(strain)) * a
            axial += force
            moment += force * u
        return axial / 1000, max(moment / 1e6, 0.0)

    def interaction(self, direction, count=321):
        points = [(self.pure_tension(), 0.0, 0.0)]
        low, high = self.supports(direction)
        log_min, log_max = math.log((high - low) * 0.0005), math.log((high - low) * 1000)
        for i in range(count):
            depth = math.exp(log_min + i / max(count - 1, 1) * (log_max - log_min))
            n, m = self.profile(depth, direction)
            points.append((n, m, depth))
        points.append((self.pure_compression(), 0.0, None))
        return points

    @staticmethod
    def capacity(curve, target):
        best = depth = None
        for i in range(1, len(curve)):
            a, b = curve[i - 1], curve[i]
            n1, n2 = a[0], b[0]
            if target < min(n1, n2) - 1e-9 or target > max(n1, n2) + 1e-9:
                continue
            if abs(n2 - n1) < 1e-12:
                m, x = max(a[1], b[1]), a[2]
            else:
                t = (target - n1) / (n2 - n1)
                m = a[1] + t * (b[1] - a[1])
                x = a[2] + t * (b[2] - a[2]) if a[2] is not None and b[2] is not None else (a[2] if a[2] is not None else b[2])
            m = max(m, 0.0)
            if best is None or m > best:
                best, depth = m, x
        return best, depth

    @classmethod
    def inside(cls, curve, n, m):
        cap = cls.capacity(curve, n)[0]
        return cap is not None and m <= cap + 1e-8

    @classmethod
    def load_factor(cls, curve, n, m):
        if abs(n) < 1e-12 and abs(m) < 1e-12:
            return math.inf
        low, high = 0.0, 1.0
        for _ in range(48):
            if not cls.inside(curve, high * n, high * m):
                break
            low, high = high, high * 2
        else:
            return math.inf
        for _ in range(64):
            mid = (low + high) / 2
            if cls.inside(curve, mid * n, mid * m):
                low = mid
            else:
                high = mid
        return low

    def analyze(self, axial, mx, my):
        demand = math.hypot(mx, my)
        direction = math.atan2(my, mx) if demand > 1e-12 else 0.0
        effective = demand
        warnings = []
        if flag(self.p.get("apply_minimum_eccentricity", False)):
            raise ValueError("eccentricità minima non gestita")
        curve = self.interaction(direction)
        resistance, depth = self.capacity(curve, axial)
        factor = self.load_factor(curve, axial, effective)
        ratio = 0.0 if math.isinf(factor) else 1 / max(factor, 1e-15)
        if flag(self.p.get("apply_pile_requirements", False)):
            raise ValueError("requisiti dei pali non gestiti")
        ok = self.inside(curve, axial, effective)
        axial_ok = self.pure_tension() - 1e-9 <= axial <= self.pure_compression() + 1e-9
        reinforcement = self.area_steel / self.area_concrete
        spacing = math.inf
        for i in range(len(self.bars)):
            for j in range(i + 1, len(self.bars)):
                a, b = self.bars[i], self.bars[j]
                spacing = min(spacing, math.hypot(a[0] - b[0], a[1] - b[1]) - (a[3] + b[3]) / 2)
        # Controlli nel formato del programma originale (non confrontati da X.Verifiche).
        checks = [
            ["Pressoflessione SLU", ok, f"eta = {ratio:.3f}; lambda = " + ("infinito" if math.isinf(factor) else f"{factor:.3f}")],
            ["Campo di resistenza assiale", axial_ok, f"{self.pure_tension():.1f} <= NEd <= {self.pure_compression():.1f} kN"],
            ["Rapporto geometrico di armatura", True, f"rho = {100 * reinforcement:.3f}% (solo informativo)"],
            ["Spaziatura libera indicativa", spacing > 0, f"distanza libera minima = {spacing:.1f} mm"],
        ]
        if self.v("fck_mpa") > 50:
            warnings.append("Calcestruzzo ad alta resistenza: deformazioni ed esponente del diagramma adeguati automaticamente.")
        warnings.append("Verifica di sezione del primo ordine: effetti del secondo ordine, taglio, fessurazione e interazione terreno-palo non inclusi.")
        return {
            "fcd_mpa": self.fcd, "fyd_mpa": self.fyd, "concrete_area_mm2": self.area_concrete, "steel_area_mm2": self.area_steel,
            "reinforcement_ratio": reinforcement, "bar_radius_mm": self.bar_radius,
            "section_outline": self.outline, "bars": [list(b) for b in self.bars], "section_width_mm": self.width,
            "section_height_mm": self.height, "minimum_steel_ratio": 0.0, "maximum_axial_kn": self.pure_compression(),
            "minimum_axial_kn": self.pure_tension(), "demand_moment_knm": demand, "effective_moment_knm": effective,
            "resistance_moment_knm": resistance if resistance is not None else 0.0, "utilization": ratio,
            "load_factor": factor if math.isfinite(factor) else "Infinity", "direction_rad": direction,
            "neutral_axis_depth_mm": depth,
            "interaction_curve": [{"axial_force_kn": n, "moment_knm": m, "neutral_axis_depth_mm": x} for n, m, x in curve],
            "checks": checks, "warnings": warnings, "passed": ok and axial_ok,
        }


# -------------------------------------------------------------------- metodo n fessurato (sezione_elastica.py)
def solve3(matrix, rhs):
    a = [[matrix[i][j] for j in range(3)] + [rhs[i]] for i in range(3)]
    scale = max(abs(matrix[i][j]) for i in range(3) for j in range(3))
    for i in range(3):
        pivot = i
        for j in range(i + 1, 3):
            if abs(a[j][i]) > abs(a[pivot][i]):
                pivot = j
        if abs(a[pivot][i]) <= max(scale * 1e-13, 1e-30):
            raise ValueError("Sezione elastica singolare")
        a[i], a[pivot] = a[pivot], a[i]
        divisor = a[i][i]
        a[i] = [value / divisor for value in a[i]]
        for j in range(3):
            if j != i:
                factor = a[j][i]
                a[j] = [a[j][k] - factor * a[i][k] for k in range(4)]
    return [a[0][3], a[1][3], a[2][3]]


def elastic_state(section, n, axial, mx, my):
    if not all(math.isfinite(v) for v in (n, axial, mx, my)) or n <= 0:
        raise ValueError("input non validi")
    length = max(section.width, section.height)
    target = [axial * 1000, mx * 1e6 / length, my * 1e6 / length]
    points = [(a, (1.0, y / length, -x / length), False) for x, y, a in section.fibers]
    points += [(a, (1.0, y / length, -x / length), True) for x, y, a, _ in section.bars]

    def response(q, uncracked=False):
        force = [0.0, 0.0, 0.0]
        stiffness = [[0.0] * 3 for _ in range(3)]
        for area, u, steel in points:
            stress = fsum_neumaier(q[k] * u[k] for k in range(3))
            active = stress > 0 or uncracked
            factor = (n - (1 if active else 0)) if steel else (1 if active else 0)
            for i in range(3):
                force[i] += area * factor * stress * u[i]
                for j in range(3):
                    stiffness[i][j] += area * factor * u[i] * u[j]
        return force, stiffness

    q = solve3(response([0.0, 0.0, 0.0], True)[1], target)
    norm = max(1.0, math.sqrt(fsum_neumaier(v * v for v in target)))
    error = 0.0
    for _ in range(80):
        force, stiffness = response(q)
        residual = [target[i] - force[i] for i in range(3)]
        error = math.sqrt(fsum_neumaier(v * v for v in residual)) / norm
        if error < 1e-9:
            break
        delta = solve3(stiffness, residual)
        for step in range(20):
            candidate = [q[i] + delta[i] * math.pow(0.5, step) for i in range(3)]
            trial = response(candidate)[0]
            trial_error = math.sqrt(fsum_neumaier((target[i] - trial[i]) * (target[i] - trial[i]) for i in range(3))) / norm
            if trial_error < error:
                q = candidate
                break
        else:
            raise ValueError("mancata convergenza")
    else:
        raise ValueError("superato il limite di 80 iterazioni")
    steel = [n * fsum_neumaier(q[k] * u[k] for k in range(3)) for _, u, s in points if s]
    concrete = max(0.0, max(q[0] + q[1] * p[1] / length - q[2] * p[0] / length for p in section.outline))
    return {"piano": q, "lunghezza_mm": length, "sigma_cls": concrete, "sigma_acciaio": max((abs(v) for v in steel), default=0.0),
            "tensioni_barre": steel, "residuo_relativo": error}


def elastic_limit(section, n, axial, direction):
    def state(moment):
        return elastic_state(section, n, axial, moment * math.cos(direction), moment * math.sin(direction))
    baseline = state(0.0)
    if baseline["sigma_acciaio"] >= section.fyd:
        return 0.0, baseline
    low, high = 0.0, max(1.0, section.fyd * section.area_steel * section.height / 1e6)
    for _ in range(32):
        if state(high)["sigma_acciaio"] >= section.fyd:
            break
        high *= 2
    else:
        raise ValueError("limite elastico non individuato")
    for _ in range(42):
        middle = (low + high) / 2
        if state(middle)["sigma_acciaio"] < section.fyd:
            low = middle
        else:
            high = middle
    return low, state(low)


# -------------------------------------------------------------------- domini
def profiles(section, mode="Plastico", n=15.0, angle=0.0, samples=121):
    sx, cy = math.sin(angle), math.cos(angle)
    lo, hi = section.supports(angle)
    depth = hi - lo
    elastic = mode == "Elastico"
    concrete = [(y * cy - x * sx, x, y, a) for x, y, a in section.fibers]
    bars = [(y * cy - x * sx, x, y, a) for x, y, a, _ in section.bars]

    def response(neutral):
        if elastic:
            maximum = max(abs(b[0] - neutral) for b in bars)
            factor = section.fyd / (n * maximum)
            def c_(u): return max(0.0, (u - neutral) * factor)
            def s_(u): return n * (u - neutral) * factor
        else:
            factor = section.eps_cu / max(hi - neutral, depth * 1e-8)
            def c_(u): return section.concrete_stress((u - neutral) * factor)
            def s_(u): return section.steel_stress((u - neutral) * factor)
        nn = mx = my = 0.0
        for u, x, y, a in concrete:
            force = c_(u) * a
            nn += force
            mx += force * y
            my -= force * x
        for u, x, y, a in bars:
            force = (s_(u) - c_(u)) * a
            nn += force
            mx += force * y
            my -= force * x
        return [nn / 1000, mx / 1e6, my / 1e6]

    points = []
    if elastic:
        for i in range(samples):
            points.append(response(hi + depth * math.pow(10, 3 - 6.0 * i / (samples - 1))))
    else:
        points.append([section.pure_tension(), 0, 0])  # zeri interi, come nel file del programma originale
    for i in range(samples):
        points.append(response(hi - depth * math.exp(math.log(0.0005) + i / (samples - 1) * math.log(1000 / 0.0005))))
    if mode == "Plastico":
        points.append([section.pure_compression(), 0, 0])
    return points


def domain_nm(section, mode, n):
    first = [[q[1], q[0]] for q in profiles(section, mode, n, 0.0)]
    second = [[q[1], q[0]] for q in reversed(profiles(section, mode, n, math.pi))]
    return first + second


def domain_mm(section, mode, n, axial, angles=36):
    contour = []
    for i in range(angles):
        angle = 2 * math.pi * i / angles
        curve = profiles(section, mode, n, angle)
        candidates = []
        for j in range(1, len(curve)):
            p, q = curve[j - 1], curve[j]
            if min(p[0], q[0]) <= axial <= max(p[0], q[0]) and abs(q[0] - p[0]) > 1e-12:
                t = (axial - p[0]) / (q[0] - p[0])
                candidates.append([p[1] + t * (q[1] - p[1]), p[2] + t * (q[2] - p[2])])
        if candidates:
            contour.append(max(candidates, key=lambda c: c[0] * math.cos(angle) + c[1] * math.sin(angle)))
    return contour


# -------------------------------------------------------------------- casi
SECTION_KINDS = ("sezione", "elastica", "resistenza_elastica", "dominio")


def evaluate(case, side_rule):
    kind, data = case["tipo"], case["input"]
    get = lambda key, default: number(data[key]) if key in data and data[key] is not None else default
    if kind == "dominio":
        section = Section(data["parametri"], 12, 36, side_rule)
        mode, n = data["mode"], get("coeff_n", 15.0)
        return {"nm": domain_nm(section, mode, n), "mm": domain_mm(section, mode, n, get("n", 0.0), 12)}
    section = Section(data["parametri"], int(get("nr", 28)), int(get("na", 96)), side_rule)
    if kind == "sezione":
        return section.analyze(get("n", 0.0), get("mx", 0.0), get("my", 0.0))
    if kind == "elastica":
        return elastic_state(section, get("coeff_n", 15.0), get("n", 0.0), get("mx", 0.0), get("my", 0.0))
    if kind == "resistenza_elastica":
        moment, state = elastic_limit(section, get("coeff_n", 15.0), get("n", 0.0), get("direction", 0.0))
        return {"moment": moment, "state": state}
    raise ValueError("tipo non gestito: " + kind)


class Stats:
    def __init__(self):
        self.numbers = 0
        self.max_abs = 0.0
        self.max_path = ""


def compare(expected, actual, path, stats, errors, checks=False):
    """Stesse regole di supporto/test/X.Verifiche: campi dell'atteso (esclusi «checks», salvo checks=True), liste di uguale
    lunghezza. Con checks=True anche i campi in più del risultato sono un errore."""
    if expected is None or actual is None:
        if expected is not None or actual is not None:
            errors.append(path + ": null diverso")
        return
    if isinstance(expected, dict):
        if not isinstance(actual, dict):
            errors.append(path + ": oggetto atteso")
            return
        if checks and set(actual) - set(expected):
            errors.append(path + ": campi in più " + ", ".join(sorted(set(actual) - set(expected))))
        for key, value in expected.items():
            if key == "checks" and not checks:
                continue
            if key not in actual:
                errors.append(path + "." + key + ": campo assente")
                continue
            compare(value, actual[key], path + "." + key, stats, errors, checks)
        return
    if isinstance(expected, list):
        if not isinstance(actual, list) or len(expected) != len(actual):
            errors.append(path + ": lunghezza diversa")
            return
        for i, (a, b) in enumerate(zip(expected, actual)):
            compare(a, b, f"{path}[{i}]", stats, errors, checks)
        return
    numeric = lambda v: isinstance(v, (int, float)) and not isinstance(v, bool)
    if numeric(expected) and numeric(actual):
        stats.numbers += 1
        delta = abs(expected - actual)
        if delta > stats.max_abs:
            stats.max_abs, stats.max_path = delta, path
        if not math.isfinite(actual) or delta > TOL_ABS + TOL_REL * abs(expected):
            errors.append(f"{path}: atteso={expected!r}; Python={actual!r}; delta={delta!r}")
        return
    if json.dumps(expected, ensure_ascii=False) != json.dumps(actual, ensure_ascii=False):
        errors.append(f"{path}: valore diverso: {expected!r} / {actual!r}")


def load(path):
    with open(path, "rb") as handle:
        return json.loads(handle.read().decode("utf-8"))


def run_comparison(cases, side_rule, selector, checks=False):
    stats, failures, count = Stats(), [], 0
    for case in cases:
        if case.get("tipo") not in SECTION_KINDS or case.get("atteso_errore") or not selector(case):
            continue
        errors = []
        try:
            compare(case["atteso"], evaluate(case, side_rule), case["nome"], stats, errors, checks)
        except ValueError as exc:
            errors.append(case["nome"] + ": " + str(exc))
        count += 1
        if errors:
            failures.append((case["nome"], errors))
    return count, stats, failures


def report(title, count, stats, failures):
    print(f"{title}: {count} casi, {stats.numbers} numeri, scarto assoluto massimo {stats.max_abs:.3e} ({stats.max_path}); "
          f"{len(failures)} casi diversi")
    for name, errors in failures[:20]:
        print("  DIVERSO " + name + ": " + "; ".join(errors[:3]) + (f" (+{len(errors) - 3})" if len(errors) > 3 else ""))
    return 0 if not failures and count else 1


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--valida", metavar="CASI")
    parser.add_argument("--barre-laterali", choices=("attuale", "originale"), default="attuale")
    parser.add_argument("--confronta", metavar="CASI")
    parser.add_argument("--scrivi", metavar="CASI")
    parser.add_argument("--provenienza", default="")
    args = parser.parse_args(argv)
    status = 0
    if args.valida:
        cases = load(args.valida)
        regenerated = lambda c: "fonte_atteso" in c
        if args.barre_laterali == "originale":
            count, stats, failures = run_comparison(cases, "originale", lambda c: c["input"]["parametri"].get("shape") == "A T", checks=True)
            status |= report("Sezioni a T, attesi originali (barre laterali del programma originale; con checks e senza campi in più)",
                             count, stats, failures)
        count, stats, failures = run_comparison(cases, "attuale", lambda c: not regenerated(c) and c["input"]["parametri"].get("shape") != "A T",
                                                checks=True)
        status |= report("Sezioni rettangolari e circolari, attesi originali (con checks e senza campi in più)", count, stats, failures)
    if args.confronta:
        cases = load(args.confronta)
        count, stats, failures = run_comparison(cases, "attuale", lambda c: "fonte_atteso" in c)
        status |= report("Sezioni a T con «fonte_atteso», regola attuale delle barre laterali", count, stats, failures)
    if args.scrivi:
        raw = open(args.scrivi, "rb").read()
        cases = json.loads(raw.decode("utf-8"))
        if json.dumps(cases, ensure_ascii=False, separators=(",", ":")).encode("utf-8") != raw:
            raise SystemExit("Il file non si riscrive identico: nessuna modifica.")
        if not args.provenienza:
            raise SystemExit("Indicare --provenienza.")
        changed = 0
        for case in cases:
            if case.get("tipo") in SECTION_KINDS and "fonte_atteso" in case:
                case["atteso"] = evaluate(case, "attuale")
                case["fonte_atteso"] = args.provenienza
                changed += 1
        with open(args.scrivi, "wb") as handle:
            handle.write(json.dumps(cases, ensure_ascii=False, separators=(",", ":")).encode("utf-8"))
        print(f"{changed} casi riscritti con gli attesi di questo programma.")
    return status


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
