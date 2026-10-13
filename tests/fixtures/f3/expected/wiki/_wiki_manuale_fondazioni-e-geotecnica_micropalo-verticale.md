## Micropalo verticale

### Correlazione del bulbo

La resistenza laterale usa curve digitalizzate Bustamante Doix documentate nel materiale di riferimento di Viggiani, sezione 13.1.6. Le famiglie SG, AL, MC e R e le curve 1 IRS e 2 IGU individuano la correlazione applicabile. La grandezza p_l, in MPa, viene interpolata linearmente fra i punti della curva; l'ordinata viene convertita in kPa. Il codice rifiuta valori esterni al campo disponibile. Il campo dell'interfaccia è «Pressione p_i = p_l»: l'uguaglianza è un'assunzione del modulo e non dimostra che la pressione della pompa coincida fisicamente con il parametro geotecnico dell'abaco.

Il diametro del bulbo viene stimato come Ds = α D, dove D è il diametro di perforazione. La resistenza di ogni tratta attiva vale π Ds Lj τj. α rappresenta l'espansione convenzionale e va scelto in relazione a terreno e iniezione; non è un incremento di resistenza indipendente dalla geometria.

```math
D_s = \alpha D
R_s = \sum_j\pi D_s L_j\tau_j
```

Per esempio, D = 0,20 m, α = 1,30, lunghezza attiva 8 m e τ = 150 kPa producono Ds = 0,26 m e Rs = 980,18 kN prima dei coefficienti. Aumentare α del 10% aumenta linearmente la superficie resistente, a parità delle altre ipotesi. Il risultato resta condizionato alla validità della correlazione e della realizzazione del bulbo.

### Inclinazione punta e peso

Per un'inclinazione θ dalla verticale, una differenza di quota Δz corrisponde a una tratta lungo l'asse Δs = Δz/cos θ. La resistenza viene sommata lungo quell'asse. Non deve essere letta direttamente come componente verticale di una capacità di gruppo senza risolvere la geometria delle azioni.

La punta opzionale è una frazione assegnata del laterale; non deriva da una capacità portante indipendente. Per il peso si separano area del tubo e area di boiacca. Il diametro del tubo è distinto da quello del bulbo. La componente adottata nella procedura segue il fattore cos θ previsto dal modello.

```math
A_{s,tubo} = \frac{\pi(D_e^2-D_i^2)}{4}
q = \frac{9{,}81\cdot7850 A_{s,tubo}}{1000}+\gamma_{boiacca}A_{boiacca}
W = q s\cos\theta
```

Diametri e aree devono essere riportati in metri e metri quadrati nella formula del peso. Media, minimi, ξ, γ ed efficienze seguono la struttura del calcolo verticale. Instabilità del tubo, flessione, sfilamento del collegamento e trasferimento locale fra tubo e boiacca restano verifiche distinte.

