"""One-time, reviewed conversion of the Rev09 display equations to LaTeX.
Stops if the source differs: never infer fractions or subscripts from prose.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[3]
LATEX = r"""
f_{cd} = \frac{\alpha_{cc} f_{ck}}{\gamma_c}
f_{yd} = \frac{f_{yk}}{\gamma_s}
\varepsilon_{yd} = \frac{f_{yd}}{E_s}
c_{\min} = \max(10; c_{bond}; c_{dur}) + c_{rugosita} + c_{abrasione}
c_{nom} = \max(c_{\min}+\Delta c_{dev}; c_{controterra})
A_b = \frac{\pi D^2}{4}
u = \pi D
\gamma' = \max(0; \gamma_{sat}-9{,}81)
\sigma'_v(z) = \int \gamma'(z)\,dz
\tau_s = c' + K\mu\sigma'_v
R_s = \sum_j \pi D L_j\tau_{sj}
R_{b,\mathrm{drenata}} = A_b\sigma'_v N_q
R_{b,\mathrm{non\ drenata}} = A_b(N_c C_u+\sigma_v)
\log_{10}(N_q) = 1+\frac{\varphi-\varphi_{10}}{\varphi_{100}-\varphi_{10}}
R_{d,C} = \eta_C\min\left[\frac{R_{s,medio}/\gamma_s+R_{b,medio}/\gamma_b}{\xi_3};
\frac{R_{s,\min}/\gamma_s+R_{b,\min}/\gamma_b}{\xi_4}\right]
R_{d,T} = \eta_T\min\left[\frac{R_{s,medio}}{\xi_3\gamma_t};\frac{R_{s,\min}}{\xi_4\gamma_t}\right]
E_{d,C} = N_C+\gamma_{G,sfav}W
E_{d,T} = \max(0; N_T-\gamma_{G,fav}W)
\eta = 1-\frac{\arctan(D/s_x)}{90}\frac{n_x-1}{n_x}
-\frac{\arctan(D/s_y)}{90}\frac{n_y-1}{n_y}
D_s = \alpha D
R_s = \sum_j\pi D_s L_j\tau_j
A_{s,tubo} = \frac{\pi(D_e^2-D_i^2)}{4}
q = \frac{9{,}81\cdot7850 A_{s,tubo}}{1000}+\gamma_{boiacca}A_{boiacca}
W = q s\cos\theta
p(z) = 9 C_u D\quad\mathrm{per}\ z\geq1{,}5D\ \mathrm{nei\ coesivi}
p(z) = 3 K_p D\sigma'_v(z)\quad\mathrm{nei\ granulari}
K_p = \frac{1+\sin\varphi}{1-\sin\varphi}
Q(z) = \int_0^z p(s)\,ds
S(z) = \int_0^z s p(s)\,ds
A(z) = z Q(z)-S(z)
V(z) = H-Q(z)
M(z) = M_0+H z-A(z)
R_k = \min\left(\frac{H_{u,medio}}{\xi_3};\frac{H_{u,\min}}{\xi_4}\right)
R_d = \frac{\eta R_k}{1{,}3}
A = \frac{\pi(D_e^2-D_i^2)}{4}
I = \frac{\pi(D_e^4-D_i^4)}{64}
W_{el} = \frac{2I}{D_e}
W_{pl} = \frac{D_e^3-D_i^3}{6}
N_{pl} = \frac{A f_y}{\gamma_{M0}}
M_{pl} = \frac{W_{pl}f_y}{\gamma_{M0}}
M_y(N) = M_{pl}\left(1-\frac{|N|}{N_{pl}}\right)
\varepsilon(x,y) = \varepsilon_0+k_x x+k_y y
N = \int_{A_c}\sigma_c\,dA+\sum A_s\sigma_s+\sum A_p\sigma_p
\rho_{eff} = \frac{A_{s,eff}}{A_{c,eff}}
\alpha_e = \frac{E_s}{E_{cm}}
\Delta\sigma = \frac{k_t f_{ctm}(1+\alpha_e\rho_{eff})}{\rho_{eff}}
\Delta\varepsilon = \max\left[\frac{\sigma_s-\Delta\sigma}{E_s};\frac{0{,}60\sigma_s}{E_s}\right]
\Delta s_{m,vicino} = \frac{3{,}4c+0{,}425 k_1 k_2\varphi_{eq}/\rho_{eff}}{1{,}70}
s_{lim} = 5(c+\varphi_{eq}/2)
\Delta s_{m,distante} = 0{,}75(h-x)
w_k = \max(0;1{,}70\Delta s_m\Delta\varepsilon)
k = \min\left[2;1+\sqrt{\frac{200}{d}}\right]
\rho = \min\left[0{,}02;\frac{A_{sl}}{b_w d}\right]
\sigma_{cp} = \min\left[-\frac{1000N}{A_c};0{,}2f_{cd}\right]
V_{Rd} = \max\left[\frac{0{,}18k\sqrt[3]{100\rho f_{ck}}}{\gamma_c}+0{,}15\sigma_{cp};
0{,}035 k^{1{,}5}\sqrt{f_{ck}}+0{,}15\sigma_{cp}\right]\frac{b_w d}{1000}
V_{Rsd} = \frac{z(A_{sw}/s)f_{yd}(\cot\alpha+\cot\theta)\sin\alpha}{1000}
V_{Rcd} = \frac{z b_w\alpha_c\,0{,}5f_{cd}(\cot\alpha+\cot\theta)}{(1+\cot^2\theta)1000}
V_{Rd} = \min(V_{Rsd};V_{Rcd})
T_{Rcd} = \frac{2A_k t\,0{,}5 f_{cd}\cot\theta}{(1+\cot^2\theta)10^6}
T_{Rsd} = \frac{2A_k(A_{sw}/s)f_{yd}\cot\theta}{10^6}
T_{Rld} = \frac{2A_k(A_{sl,disp}/u_k)f_{yd}}{\cot\theta\,10^6}
T_{Rd} = \min(T_{Rcd};T_{Rsd};T_{Rld})
n_0 = \frac{E_a}{E_{cm}}
n = n_0(1+\psi_L\varphi)
\varphi = \frac{n/n_0-1}{\psi_L}
\alpha = \arctan\left(\frac{\delta}{h_w}\right)
\ell_w = \sqrt{h_w^2+\delta^2} = \frac{h_w}{\cos\alpha}
t_{w,h} = \frac{t_w}{\cos\alpha}
s_{bottom} = s_{top}-2\delta
b_{interno} = s_{bottom}-t_{w,h}
b_{sbalzo} = \frac{b_b-s_{bottom}-t_{w,h}}{2}
t_{w,eq} = \frac{n_w t_w}{\cos\alpha}
b_{t,eq} = n_f b_t
A_w = n_w t_w\ell_w = t_{w,eq}h_w
A_s = n_f b_t t_t+n_w t_w\ell_w+b_b t_b
y_G = \frac{\sum_i A_i y_i}{\sum_i A_i}
I_x = \sum_i\left[I_{xi}+A_i(y_i-y_G)^2\right]
M_c = M_0+N y_G
\sigma(y) = \frac{N}{A_s}-\frac{M_c(y-y_G)}{I_x}
M_{x0} = M_x-\frac{N y_N}{1000}
N_{eq} = E_{c,eff}A_c\Delta\varepsilon_{cs}
\Delta\sigma_{c,propria} = -E_{c,eff}\Delta\varepsilon_{cs}
\tau_{cr} = \frac{k_{\tau}\pi^2 E}{12(1-\nu^2)}\left(\frac{t_w}{h_w}\right)^2
\lambda_w = \sqrt{\frac{f_y}{\sqrt{3}\tau_{cr}}}
V_{pl,Rd} = \frac{h_w t_w f_y}{\sqrt{3}\gamma_{M0}}
V_{bw,Rd} = \frac{\chi_w h_w t_w f_y}{\sqrt{3}\gamma_{M1}}
V_{Rd} = \min(V_{pl,Rd};V_{bw,Rd})
V_{lamiera} = \frac{V}{n_w\cos\alpha}
V_{Rd,verticale} = n_w\cos\alpha\,V_{Rd,lamiera}
\tau_{media} = \frac{V}{n_w h_w t_w}
P_{Rd} = \frac{\min\left[0{,}8f_u\pi d^2/4;0{,}29\alpha d^2\sqrt{f_{ck}E_{cm}}\right]}{\gamma_V}
q = \sum_i\left(\frac{V_i S_i}{I_i}+\Delta q_i\right)
P_{Ed} = \frac{|q|\,passo}{n_{pioli}}
P_{Ed} = \frac{|q|\,passo}{n_f n_{pioli}}
P_{Ed} = \frac{(|q|/n_f+|q_T|)\,passo}{n_{pioli}}
q = \frac{T}{2A_0}
A_0 = \frac{(b_{sup}+b_{inf})h_0}{2}
J = \frac{4A_0^2}{\sum_i(\ell_i/t_i)}
n_G = \frac{n(1+\nu_c)}{1+\nu_a}
\tau_T = \frac{q}{t}
V_{lamiera} = \frac{V}{n_w\cos\alpha}+q\ell_w
V_{eq} = n_w\cos\alpha\,V_{lamiera}
\tau_b = \frac{q}{t_b}+\frac{V S_{fondo}}{I t_b}
\eta_1+(2\eta_3-1)^2\leq1
q\cot\theta\leq\max(0;-\sigma_{c,media})h_c+\sum_i\frac{A_{s,i}}{s_i}\left[f_{yd}-\max(0;\sigma_{s,i})\right]
\tau_D = \frac{T}{2A_0 t_D}
\Delta R = \frac{T}{e_b}
\sum_i\ell_i V_i = 0
\int\omega t\,ds = \int\omega x t\,ds = \int\omega y t\,ds = 0
I_{Dw} = \int\omega^2 t\,ds
E I_{Dw}\psi''''+K\psi = p
\sigma_{dw} = E\omega\psi''
m = m_1\psi
I_{Dw} = \frac{t(b+h)b^2 h^2}{96}
K = \frac{24}{b/D_h+h/D_v}
K_D = G t b h
K_D = \frac{2E A b^2 h^2}{L^3}
\sigma_{eq} = \sqrt{\sigma_x^2+\sigma_z^2+|\sigma_x\sigma_z|+3\tau^2}
W = n_c b_c+2\,banchina+spartitraffico+2\,barriera
d = \max\left[d_{\min};\frac{L_{\max}}{r}k\right]
E_c = 22000\left(\frac{f_c+8}{10}\right)^{0{,}3}\ \mathrm{MPa}
n = \frac{200000}{E_c}
y_G = \frac{\sum_i n_i A_i y_i}{\sum_i n_i A_i}
I_{eq} = \sum_i n_i\left[I_i+A_i(y_i-y_G)^2\right]
g = 25A_{cls}+\frac{9{,}81m_{acciaio}}{L}+g_2 W+8n_{barriere}
q_{servizio} = g+q_{traffico}W
q_{fattorizzato} = \gamma_G g+\gamma_Q q_{traffico}W
L_1 M_0+2(L_1+L_2)M_1+L_2 M_2
= -\frac{q(L_1^3+L_2^3)}{4}
R_L = \frac{qL}{2}+\frac{M_R-M_L}{L}
M(x) = M_L+R_L x-\frac{qx^2}{2}
V(x) = R_L-qx
R_{palo} = \pi D L q_s+\frac{\pi D^2 q_b}{4}
C_{diretto} = \sum_i Q_i p_i
C_{totale} = C_{diretto}(1+oneri/100)(1+imprevisti/100)
CO_2 = \left[\frac{V_{cls}f_{cls}}{1000}+\sum m_{acciaio}f_{acciaio}\right](1+cantiere/100)
y_{fondo,i} = y_{superficie}-\sum_{j=1}^i h_j
\eta = \frac{\gamma_R}{F}
W = n_c\cdot b_c+2b+m+2b_b
d = \max\left(d_{\min};\frac{k L_{\max}}{r}\right)
A_a = 2b_f t_f+t_w(h-2t_f)
\ell_w = \sqrt{h_w^2+(h_w s/4)^2}
y_G = \frac{\sum_j n_j A_j y_j}{\sum_j n_j A_j}
I_{eq} = \sum_j n_j\left[I_j+A_j(y_j-y_G)^2\right]
G = 25A_c+\frac{9{,}81m_a}{L}+g_2 W+8n_b
Q = q_{traffico}\cdot W
q_s = G+Q
q_d = \gamma_G G+\gamma_Q Q
M_l a+2M_i(a+b)+M_r b = -\frac{q(a^3+b^3)}{4}
R_l = \frac{ql}{2}+\frac{M_r-M_l}{l}
V(x) = R_l-qx
M(x) = M_l+R_l x-\frac{qx^2}{2}
A_{req} = \frac{\max(0;R+25V_{pulvino})}{300f_{c,sub}-25H}
R_{pal} = \pi D L_p q_{s,palo}+\frac{\pi D^2 q_b}{4}
A = \max\left(0{,}00001;\frac{|N|}{\sigma_{rif}}\right)
C_{diretto} = \sum_j Q_j p_j
C_{totale} = C_{diretto}(1+oneri/100)(1+imprevisti/100)
E_{CO2} = \left(\frac{V_{cls}f_{cls}}{1000}+\sum_s m_s f_s\right)(1+cantiere/100)
S = \frac{0{,}5C}{\max(1;C_{\min})}+\frac{0{,}5E}{\max(10^{-9};E_{\min})}
""".strip().splitlines()

PILOT = {
    'kₐ = EA / L': r'k_a = \frac{EA}{L}',
    'δ = NL / EA': r'\delta = \frac{NL}{EA}',
    'κ = M / EI': r'\kappa = \frac{M}{EI}',
    'σ = M / W': r'\sigma = \frac{M}{W}',
    'γ = V / (GAₛ)': r'\gamma = \frac{V}{GA_s}',
    'θ = TL / (GJ)': r'\theta = \frac{TL}{GJ}',
    'Mₘₐₓ = qL² / 8 = 25 × 8² / 8 = 200 kNm': r'M_{\max} = \frac{qL^2}{8} = \frac{25\cdot8^2}{8} = 200\ \mathrm{kN\,m}',
    'Vₐₚₚ = qL / 2 = 100 kN': r'V_{app} = \frac{qL}{2} = 100\ \mathrm{kN}',
    'N꜀ᵣ = π²EI / L₀²': r'N_{cr} = \frac{\pi^2 EI}{L_0^2}',
}

def convert():
    path = ROOT / 'supporto/docs/guida-teorica-anthea.md'
    text = path.read_text(encoding='utf-8')
    old = re.findall(r'^\$\$ (.+)$', text, re.M)
    assert len(old) == len(LATEX) == 162, (len(old), len(LATEX))
    iterator = iter(LATEX)
    text = re.sub(r'^\$\$ (.+)$', lambda m: '$$ ' + next(iterator), text, flags=re.M)
    # Rejoin the four equations split over continuation lines in the old manual.
    text = re.sub(r'\n\$\$ (?=\\frac\{R_|-\\frac\{\\arctan|0\{,\}035|= -\\frac\{q\(L_1)', ' ', text)
    # Use Markdown math fences, keeping each equation independently renderable.
    text = re.sub(r'(?:^\$\$ .+\n)+', lambda m: '```math\n'+re.sub(r'^\$\$ ', '', m[0], flags=re.M)+'```\n', text, flags=re.M)
    for before, after in PILOT.items():
        assert before in text, before
        text = text.replace(before, after)
    text = text.replace('```formula', '```math')
    path.write_text(text, encoding='utf-8')
    print('Formule LaTeX convertite: 158 storiche e 9 del pilota')

if __name__ == '__main__': convert()
