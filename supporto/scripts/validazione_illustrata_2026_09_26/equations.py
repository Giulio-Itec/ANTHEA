"""LaTeX source and native Office Math, retaining the arithmetic in Rev02."""
import re,sys
from pathlib import Path
from lxml import etree as E
R=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(R/'supporto/artefatti/validazione_illustrata_2026_09_26/runtime'))
from latex2mathml.converter import convert
transform=E.XSLT(E.parse('C:/Program Files/Microsoft Office/root/Office16/MML2OMML.XSL'))
M='http://schemas.openxmlformats.org/officeDocument/2006/math'
sup='⁰¹²³⁴⁵⁶⁷⁸⁹⁻⁺'; supmap=str.maketrans(sup,'0123456789-+')
sub='₀₁₂₃₄₅₆₇₈₉ᵢᵣ';submap=str.maketrans(sub,'0123456789ir')
symbols={'ε':r'\varepsilon','φ':r'\varphi','σ':r'\sigma','α':r'\alpha','γ':r'\gamma','ρ':r'\rho','κ':r'\kappa','χ':r'\chi','θ':r'\theta','π':r'\pi','η':r'\eta','ψ':r'\psi','ξ':r'\xi','Δ':r'\Delta','Σ':r'\sum','∫':r'\int','×':r'\times','·':r'\cdot','≤':r'\le','≥':r'\ge','±':r'\pm','∞':r'\infty','⇒':r'\Rightarrow','∈':r'\in','‰':r'\text{‰}','µ':r'\mu','Ø':r'\varnothing','√':r'\sqrt'}
vars={'sc':r'\sigma_c','ss':r'\sigma_s','As':r'A_s','Ac':r'A_c','Cc':r'C_c','Fi':r'F_i','Mx':r'M_x','My':r'M_y','Mcx':r'M_{cx}','Mcy':r'M_{cy}','Ecm':r'E_{cm}','Es':r'E_s','Ea':r'E_a','Ec':r'E_c','fcd':r'f_{cd}','fyd':r'f_{yd}','fck':r'f_{ck}','fyk':r'f_{yk}','fctm':r'f_{ctm}','eps':r'\varepsilon','yc':r'y_c','xn':r'x_n','wk':r'w_k','bw':r'b_w','VRcd':r'V_{Rcd}','VRsd':r'V_{Rsd}','VRd':r'V_{Rd}','TRcd':r'T_{Rcd}','TRsd':r'T_{Rsd}','TRld':r'T_{Rld}','TRd':r'T_{Rd}','Asw':r'A_{sw}','Ak':r'A_k','uk':r'u_k','M0':r'M_0','Mc':r'M_c','e0':r'\varepsilon_0','emax':r'e_{max}','ei':r'e_i','fbd':r'f_{bd}','fct':r'f_{ct}','fy':r'f_y','fu':r'f_u','Et':r'E_t','Ac,eff':r'A_{c,eff}','As,eff':r'A_{s,eff}','hc,eff':r'h_{c,eff}','lb,rqd':r'l_{b,rqd}','lbd':r'l_{bd}','As,min':r'A_{s,min}','As,max':r'A_{s,max}'}
def tex(s):
    s=s.strip().rstrip('.').replace('−','-').replace('–','-')
    s=re.sub(r'(\d+(?:[,.]\d+)?)[eE]([+-]?\d+)',lambda m:m[1]+'×10'+str(int(m[2])).translate(str.maketrans('0123456789-','⁰¹²³⁴⁵⁶⁷⁸⁹⁻')),s)
    # Numeric punctuation is kept, with a narrow thousands separator not introduced.
    s=re.sub(r'(?<=\d),(?=\d)',lambda m:'@DEC@',s)
    tokens=re.findall(r'@DEC@|\d+(?:\.\d+)?|[A-Za-zÀ-ÿ]+(?:[0-9]+)?(?:,(?:eff|min|max|rqd))?|[⁰¹²³⁴⁵⁶⁷⁸⁹⁻⁺]+|[₀₁₂₃₄₅₆₇₈₉ᵢᵣ]+|\s+|.',s)
    out=[]
    for t in tokens:
        if t=='@DEC@':out.append('{,}')
        elif t.isspace():out.append(' ')
        elif all(c in sup for c in t):out.append('^{'+t.translate(supmap)+'}')
        elif all(c in sub for c in t):out.append('_{'+t.translate(submap)+'}')
        elif t in symbols:out.append(symbols[t]+' ')
        elif t in vars:out.append(vars[t]+' ')
        elif t in ['min','max','sin','cos','tan','cot','arctan']:out.append('\\'+t+' ')
        elif t in ['mm','MPa','kN','kNm','Nmm']:out.append(r'\,\mathrm{'+t+'}')
        elif re.fullmatch('[A-Za-zÀ-ÿ]+[0-9]*',t) and len(t)>2:out.append(r'\text{ '+t+' }')
        elif t=='%':out.append(r'\%')
        elif t=='_':out.append('_')
        else:out.append(t)
    value=''.join(out)
    value=re.sub(r'\^\(([^()]*)\)',r'^{\1}',value)
    value=re.sub(r'\^(\d+(?:\{,\}\d+)?)',r'^{\1}',value)
    number=r'\d+(?:\{,\}\d+)?'
    value=re.sub(r'(?<![\w}])('+number+r')/('+number+r')(?![\w{^])',lambda m:r'\frac{'+m[1]+'}{'+m[2]+'}',value)
    return value
def omml(latex):
    node=transform(E.fromstring(convert(latex).encode())).getroot()
    if node.tag=='{'+M+'}oMathPara':node=node.find('{'+M+'}oMath')
    if node is None:raise ValueError(latex)
    return node
# Carefully authored general equations, linked from the examples.
GENERAL=[
('equilibrio_ca','Equilibrio della sezione in calcestruzzo armato',r'e(x,y)=a+bx+cy',r'F_i=\frac{A_{s,i}(\sigma_{s,i}-\sigma_{c,i})}{1000}',r'N=-\left(\frac{\int_{A_c}\sigma_c\,dA}{1000}+\sum_i F_i\right)',r'M_x=\frac{\int_{A_c}\sigma_c y\,dA}{10^6}+\frac{\sum_iF_i y_i}{1000}',r'M_y=-\frac{\int_{A_c}\sigma_c x\,dA}{10^6}-\frac{\sum_iF_i x_i}{1000}'),
('materiali','Resistenze di progetto e legami costitutivi',r'f_{cd}=\frac{\alpha_{cc}f_{ck}}{\gamma_c},\qquad f_{yd}=\frac{f_{yk}}{\gamma_s}',r'\sigma_c=f_{cd}\left[2\frac{e}{\varepsilon_{c2}}-\left(\frac{e}{\varepsilon_{c2}}\right)^2\right]\quad(0\le e\le\varepsilon_{c2})',r'\sigma_s=\max(-f_{yd},\min(E_s e,f_{yd}))'),
('geometria','Proprietà geometriche',r'A=\sum_i A_i,\qquad y_G=\frac{\sum_i A_i y_i}{A}',r'I_G=\sum_i\left(I_i+A_i(y_i-y_G)^2\right)'),
('ancoraggio','Ancoraggio rettilineo e sovrapposizione',r'f_{bd}=2.25\eta_1\eta_2\frac{f_{ctk,0.05}}{\gamma_c}',r'l_{b,rqd}=\frac{\varnothing}{4}\frac{|\sigma_{sd}|}{f_{bd}}',r'l_{bd}=\max(l_{b,rqd},l_{b,min}),\qquad l_0=\max(\alpha_6 l_{b,rqd},l_{0,min})'),
('fessure','Distanza e apertura delle fessure',r'\rho_{p,eff}=\frac{A_{s,eff}}{A_{c,eff}}',r'\Delta\varepsilon=\max\left(\frac{\sigma_s-k_t f_{ct,eff}(1+\alpha_e\rho_{p,eff})/\rho_{p,eff}}{E_s},\frac{0.6\sigma_s}{E_s}\right)',r'\Delta s_{m,v}=\frac{3.4c+0.8k_2(0.425)\varnothing/\rho_{p,eff}}{1.7}',r'w_k=1.7\,\Delta s_m\,\Delta\varepsilon'),
('taglio','Taglio con armatura trasversale',r'V_{Rsd}=z\frac{A_{sw}}s f_{yd}(\cot\alpha+\cot\theta)\sin\alpha',r'V_{Rcd}=z b_w\alpha_c(0.5f_{cd})\frac{\cot\alpha+\cot\theta}{1+\cot^2\theta}',r'V_{Rd}=\min(V_{Rsd},V_{Rcd})'),
('torsione','Torsione e interazione con il taglio',r'T_{Rcd}=2A_k t(0.5f_{cd})\frac{\cot\theta}{1+\cot^2\theta}',r'T_{Rsd}=2A_k\frac{A_{sta}}s f_{yd}\cot\theta,\qquad T_{Rld}=2A_k\frac{A_{s,l}}{u_k}\frac{f_{yd}}{\cot\theta}',r'\eta_c=\frac{|T|}{T_{Rcd}}+\frac{|V_x|}{V_{Rcd,x}}+\frac{|V_y|}{V_{Rcd,y}}'),
('ponti','Equilibrio e storia della sezione composta',r'\varepsilon_{mecc,i}=\varepsilon_0-\kappa y_i-\varepsilon_{getto,i}-\varepsilon_{imposta,i}',r'N=\sum_i\sigma_i A_i,\qquad M_0=-\sum_i\sigma_i A_i y_i',r'M(y_r)=M_0+Ny_r,\qquad n=\frac{E_a}{E_{cm}}(1+\psi_L\varphi)'),
('navier','Soluzione elastica e curve di risposta',r'\kappa=\frac{M_0+Ny_G}{EI_G},\qquad\varepsilon_0=\frac{N}{EA}+\kappa y_G',r'\sigma(y)=E(\varepsilon_0-\kappa y),\qquad M_G=EI_G\kappa',r'N=EA\varepsilon\quad(\kappa=0),\qquad\varepsilon_p=\varepsilon-\frac{f_y}{E}'),
('fasi','Sistema incrementale delle fasi composte',r'A=\sum_iE_iA_i,\quad B=-\sum_iE_iA_i y_i,\quad D=\sum_iE_i(I_i+A_i y_i^2)',r'n=\Delta N+E_cA_c\varepsilon_{cs},\qquad m=\Delta M_0-E_cA_cy_c\varepsilon_{cs}',r'\Delta\varepsilon_0=\frac{nD-Bm}{AD-B^2},\qquad\Delta\kappa=\frac{Am-Bn}{AD-B^2}')]
