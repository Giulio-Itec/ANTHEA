## Calcestruzzo armature e copriferro

### Resistenze e diagrammi

Per il calcestruzzo il passaggio dalla resistenza caratteristica a quella di progetto segue il coefficiente αcc e il parziale γc selezionati. Per l'armatura la resistenza di progetto deriva da fy e γs. I diagrammi possono essere di catalogo o personalizzati; l'uso di una classe nominale non autorizza a ignorare il diagramma effettivamente selezionato.

```math
f_{cd} = \frac{\alpha_{cc} f_{ck}}{\gamma_c}
f_{yd} = \frac{f_{yk}}{\gamma_s}
\varepsilon_{yd} = \frac{f_{yd}}{E_s}
```

Con fck = 30 MPa, αcc = 0,85 e γc = 1,50 si ottiene fcd = 17 MPa. Con fyk = 450 MPa e γs = 1,15 si ottiene fyd = 391,30 MPa. Assumendo Es = 200000 MPa, la deformazione elastica a fyd è 0,0019565, cioè 1,9565 per mille. Sono esempi dei passaggi algebrici, non una scelta universale dei coefficienti.

Il catalogo delle armature comprende classi attuali e storiche. I dati nominali delle classi storiche non sostituiscono prove e valutazioni di un materiale esistente. A5 e deformazione ultima del diagramma hanno definizioni diverse. Quando il motore richiede una deformazione ultima, essa deve provenire da una scelta documentata e coerente con il modello costitutivo.

### Esposizioni e requisiti del materiale

Il catalogo della durabilità comprende 18 esposizioni. Le prescrizioni combinate vengono ricavate assumendo il massimo dei requisiti minimi di resistenza, il minimo dei rapporti acqua cemento massimi e il massimo dei contenuti minimi di cemento pertinenti. Classi minime, rapporti acqua cemento massimi e contenuti minimi di cemento sono quelli del prospetto 5 della UNI 11104:2016, richiamata dalle NTC 2018 al §11.2.11. L'edizione 2025 della UNI 11104 non è adottata: i suoi valori saranno considerati solo dopo il riscontro sul testo della norma. Descrizioni ed esempi delle classi mostrati nella scheda Materiali sono testi di ANTHEA basati sulla classificazione del prospetto 1 della UNI EN 206.

| Esposizione | Classe minima implementata |
| --- | --- |
| X0 | C12/15 |
| XC1 e XC2 | C25/30 |
| XC3 | C30/37 |
| XC4 | C32/40 |
| XD1 | C30/37 |
| XD2 | C32/40 |
| XD3 | C35/45 |
| XS1 | C32/40 |
| XS2 e XS3 | C35/45 |
| XF1 | C32/40 |
| XF2 e XF3 | C25/30 |
| XF4 | C30/37 |
| XA1 | C30/37 |
| XA2 | C32/40 |
| XA3 | C35/45 |

L'aria inglobata per XF2, XF3 e XF4 dipende anche dalla dimensione dell'aggregato. L'implementazione documenta il 4% per dimensioni superiori a 20 mm e il 5% nell'intervallo 12–16 mm; gli altri intervalli non devono essere interpolati tacitamente. La proposta Cl 0,4 per armatura ordinaria non deriva semplicemente dal massimo codice di esposizione. Il prospetto aiuta a formulare requisiti, senza calcolare una miscela industriale completa.

### Copriferro minimo e nominale

Il minimo deve soddisfare aderenza, durabilità e condizioni aggiuntive. Il nominale comprende il margine esecutivo e gli eventuali limiti per la superficie di getto. Nel ramo implementato, il requisito di aderenza parte dal diametro e aumenta di 5 mm quando l'aggregato supera 32 mm. Rugosità e abrasione aggiungono i contributi assegnati.

```math
c_{\min} = \max(10; c_{bond}; c_{dur}) + c_{rugosita} + c_{abrasione}
c_{nom} = \max(c_{\min}+\Delta c_{dev}; c_{controterra})
```

Il ramo EC2 determina la classe strutturale secondo esposizione, vita e opzioni ammesse, poi consulta la tabella di durabilità. Le esposizioni che non definiscono da sole quel requisito richiedono un'associazione pertinente, anziché essere trasformate in una classe equivalente arbitraria.

Il ramo NTC utilizza tre livelli di severità. Il valore tabellare di base è (15 mm per elemento a piastra, 20 mm negli altri casi) più 10 mm per livello di severità; si aggiungono 5 mm se fck è inferiore a C0 = 35 + 5 × severità. La vita di 100 anni aggiunge 10 mm, una resistenza sotto il minimo pertinente aggiunge 5 mm e l'opzione di qualità del copriferro riduce di 5 mm. Il minimo pertinente può essere assegnato nel campo ammesso dal motore e non coincide necessariamente con tutte le prescrizioni di composizione.

Come esempio del ramo NTC, XF2, fck = 30 MPa, elemento non a piastra, vita 50 anni, barra 16 mm, aggregato 20 mm e Δcdev = 10 mm danno 45 mm nominali senza riduzione di qualità. Se si passa a vita 100 anni, lo stesso caso dà 55 mm. I valori sono esempi riproducibili del codice NtcCover, utili per controllare l'input.

