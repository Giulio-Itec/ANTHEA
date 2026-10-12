## Cassoni sottili torsione uniforme e distorsione

Una sezione chiusa è efficiente nel trasmettere torsione perché può sviluppare un flusso tangenziale continuo lungo il contorno. Questa proprietà non elimina distorsione, instabilità locale delle lamiere o effetti di estremità. Occorre distinguere la torsione uniforme di Saint-Venant dalle deformazioni che cambiano la forma della sezione.

### Modello di una cella chiusa sottile

Per una cella unica, contorno chiuso, materiale elastico uniforme e torsione sufficientemente lontana dalle discontinuità, le relazioni di Bredt-Batho collegano momento torcente T, area racchiusa dalla linea media Am, spessore t e flusso q. L’integrale segue il perimetro della linea media.

```math
q=\frac{T}{2A_m}
\tau=\frac{q}{t}
J=\frac{4A_m^2}{\oint ds/t}
\frac{d\theta}{dx}=\frac{T}{GJ}
```

J è la costante torsionale, non il momento polare di inerzia utilizzabile indistintamente per ogni forma. Per celle multiple occorre imporre anche la compatibilità delle rotazioni e trattare i flussi sulle pareti condivise.

### Esempio con unità coerenti

Per un rettangolo sulla linea media di 4 × 2 m, spessore uniforme t = 20 mm, Am = 8 m² e perimetro 12 m. Con T = 1000 kNm e G = 80 GPa si ottengono q = 62,50 kN/m, τ = 3,125 MPa e J = 0,4267 m⁴. La rotazione unitaria è circa 2,93 × 10⁻⁵ rad/m; su 30 m con T costante vale circa 0,0504 gradi.

Non è la rotazione di un ponte reale: l’esempio trascura variazione di T, irrigidimenti, diaframmi, vincoli di estremità, variazioni di spessore e distorsione. Il valore contenuto della tensione tangenziale non dimostra che la lamiera sia stabile.

### Che cosa fanno diaframmi e irrigidimenti

Carichi eccentrici possono tendere a deformare il rettangolo trasversale in una forma romboidale. I diaframmi contrastano questa distorsione e distribuiscono le reazioni. Gli irrigidimenti longitudinali e trasversali governano campi locali di lamiera e lunghezze efficaci. Un elemento beam con sola rigidezza GJ non rappresenta tutti questi meccanismi; un modello a guscio richiede mesh, collegamenti e vincoli capaci di riprodurli.

Una curiosità utile: aprire una fessura longitudinale ideale nel contorno può ridurre drasticamente la rigidezza torsionale, anche se area di acciaio e ingombro cambiano poco. La chiusura del flusso è una proprietà topologica del percorso resistente, non soltanto una questione di quantità di materiale.

In ANTHEA questa trattazione è documentazione tecnica. I moduli attuali non forniscono una verifica completa di cassoni multicellulari, distorsione o instabilità delle piastre. Riferimento bibliografico: AISC e NSBA, Steel Bridge Design Handbook.

### Proseguire nella Wiki

- [Cos'è un ponte?](wiki:bridge)
- [Elementi Beam](wiki:beam)

