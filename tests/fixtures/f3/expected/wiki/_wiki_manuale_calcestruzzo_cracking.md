## Fessurazione del calcestruzzo armato

Il controllo di apertura delle fessure è una verifica di esercizio. Una sezione può soddisfare la resistenza ultima e avere fessure incompatibili con il requisito assegnato. Il calcolo richiede tensioni di esercizio, geometria dell'armatura e zona efficace di calcestruzzo teso; non basta conoscere il momento resistente.

### Meccanismo e campo

Alla fessura il contributo teso del calcestruzzo è interrotto; l'aderenza trasferisce forza fra acciaio e calcestruzzo nei tratti adiacenti. Perciò la deformazione media dell'acciaio fra le fessure non coincide necessariamente con la deformazione nella sezione fessurata. Il tension stiffening rappresenta questa collaborazione media.

![Figura 04.1 — Tirante fessurato: due fessure attraversano la zona tesa e l'armatura continua trasferisce forza per aderenza. La distanza tra fessure e l'apertura sono grandezze diverse.](../../X.Desktop/Assets/Wiki/crack-transfer.svg)

Le relazioni qui riportate riguardano la formulazione classica di EN 1992-1-1 illustrata da Walraven nel materiale JRC del 2008, per armatura ordinaria aderente, senza precompressione e con barre ravvicinate. È una relazione normativa empirico-meccanica, non una legge universale né una dichiarazione sull'edizione applicabile oggi al singolo progetto.

### Relazioni e simboli

```math
w_k=s_{r,max}(\varepsilon_{sm}-\varepsilon_{cm})
\rho_{eff}=\frac{A_s}{A_{c,eff}},\quad\alpha_e=\frac{E_s}{E_{cm}}
\Delta\varepsilon=\max\left(\frac{\sigma_s-k_t\frac{f_{ct,eff}}{\rho_{eff}}(1+\alpha_e\rho_{eff})}{E_s},\frac{0{,}6\sigma_s}{E_s}\right)
s_{r,max}=3{,}4c+0{,}425k_1k_2\frac{\phi}{\rho_{eff}}
```

| Simbolo | Definizione | Unità |
| --- | --- | --- |
| $w_k$ | Apertura caratteristica calcolata | mm |
| $s_{r,max}$ | Distanza massima convenzionale fra fessure | mm |
| $\Delta\varepsilon$ | Differenza delle deformazioni medie | adimensionale |
| $\sigma_s$ | Tensione di trazione nell'acciaio, sezione fessurata | MPa |
| $E_s,E_{cm}$ | Moduli elastici di acciaio e calcestruzzo | MPa |
| $f_{ct,eff}$ | Resistenza media a trazione al tempo di fessurazione | MPa |
| $A_s,A_{c,eff}$ | Area di armatura e area efficace tesa | mm² |
| $c,\phi$ | Copriferro dell'armatura considerata e diametro | mm |
| $k_t,k_1,k_2$ | Coefficienti di durata, aderenza e distribuzione deformativa | adimensionali |

Il copriferro della formula va riferito alle barre che controllano la fessura: non sostituire senza controllo il copriferro netto alla staffa impostato nella UI. L'area efficace non è l'intera sezione né tutta la zona geometricamente tesa. Per flessione semplice della sezione rettangolare, la definizione classica limita l'altezza efficace con il minimo dei tre contributi seguenti, dove $d$ è l'altezza utile e $x$ la profondità dell'asse neutro dal bordo compresso:

```math
h_{c,eff}=\min\left(2{,}5(h-d),\frac{h-x}{3},\frac{h}{2}\right)
```

### Procedura per un caso ordinario

Identifica combinazione SLE, esposizione e requisito di apertura dal quadro normativo del progetto. Risolvi la sezione fessurata e determina l'armatura efficace. Controlla che l'interasse delle barre non superi $5(c+\phi/2)$ prima di usare la relazione di distanza qui mostrata. Per flessione semplice usa $k_2=0{,}5$, per trazione uniforme $k_2=1$; la trazione eccentrica con la sezione interamente tesa richiede la distribuzione delle deformazioni, $k_2=(\varepsilon_1+\varepsilon_2)/(2\varepsilon_1)$. Con l'asse neutro interno alla sezione, anche in presso- o tensoflessione, la sezione è inflessa e $k_2=0{,}5$, come in ANTHEA; sulle fasce interne dei fori ANTHEA usa invece la distribuzione locale delle deformazioni della fascia. Per barre ad aderenza migliorata $k_1=0{,}8$. Il coefficiente $k_t$ è 0,6 a breve e 0,4 a lunga durata nella formulazione citata.

### Esempio numerico

Dati assegnati: $\sigma_s=200\,\mathrm{MPa}$, $E_s=200000\,\mathrm{MPa}$, $E_{cm}=33000\,\mathrm{MPa}$, $f_{ct,eff}=2{,}9\,\mathrm{MPa}$, $\rho_{eff}=0{,}02$, $\phi=16\,\mathrm{mm}$, $c=30\,\mathrm{mm}$. Barre ad aderenza migliorata, flessione semplice, lunga durata e interasse 150 mm. L'area efficace è qui un dato assunto: l'esempio verifica le relazioni di apertura, non una sezione completa.

```math
\alpha_e=6{,}0606
\Delta\sigma=0{,}4\frac{2{,}9}{0{,}02}(1+6{,}0606\cdot0{,}02)=65{,}0303\,\mathrm{MPa}
\Delta\varepsilon=\max(0{,}00067485;0{,}00060000)=0{,}00067485
s_{r,max}=3{,}4\cdot30+0{,}425\cdot0{,}8\cdot0{,}5\frac{16}{0{,}02}=238\,\mathrm{mm}
w_k=238\cdot0{,}00067485=0{,}1606\,\mathrm{mm}
```

La soglia d'interasse è 190 mm: il ramo adottato è coerente con i 150 mm assegnati. Per un requisito ipotetico di 0,30 mm il valore calcolato sarebbe inferiore al limite; quel limite è un dato d'esempio, non una prescrizione valida per ogni ambiente. Il controllo non copre fessure da ritiro impedito, gradienti termici o dettagli non rappresentati.

### Limiti e confronto con il software

Armature rade, trazione integrale, sezioni non rettangolari, tendini e fasi iniziali richiedono le rispettive regole. Non estendere la formula ravvicinata a tutti questi casi. Nel motore Anthea esistono scelte implementative specifiche per regioni efficaci e ramo distante: la guida teorica della sezione le documenta separatamente, senza attribuirle automaticamente al testo normativo.

Apri [la guida del modulo](wiki:guida-sezione-ca) per le convenzioni degli input. Conserva nel report combinazione, parametri effettivi e ramo usato; un singolo numero di apertura non rende il risultato riproducibile.

