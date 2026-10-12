## Freccia controfreccia temperatura e appoggi dei ponti

La deformabilità influenza comfort, quote della pavimentazione, pendenze di drenaggio, giunti e funzionamento degli appoggi. Il superamento di una resistenza e l’incompatibilità di uno spostamento sono problemi distinti. Occorre inoltre separare la deformazione istantanea da viscosità, ritiro, assestamenti e sequenza costruttiva.

### Sensibilità alla luce

Per una trave prismatica semplicemente appoggiata, piccole deformazioni, EI costante e carico uniforme q, la freccia in mezzeria è:

```math
w_{max}=\frac{5qL^4}{384EI}
```

Con q = 30 kN/m, L = 30 m, E = 210 GPa e I = 0,30 m⁴ si ottengono circa 5,02 mm. Aumentando la luce del 10%, senza cambiare le altre grandezze, la freccia diventa circa 7,35 mm: cresce del 46,4% perché dipende da L alla quarta potenza. La formula non descrive automaticamente una trave composta a rigidezza variabile o una sezione fessurata.

### Movimento termico e schema dei vincoli

Una variazione uniforme della temperatura produce, in assenza di impedimenti:

```math
\Delta L=\alpha L\Delta T
```

Con α = 12 × 10⁻⁶ /K, L = 120 m e ΔT = 35 K, il movimento relativo fra estremità è 50,4 mm. Se il punto fisso è al centro e il comportamento è simmetrico, ciascuna estremità si muove di circa 25,2 mm rispetto a esso. Questo valore non comprende tolleranze, rotazioni, ritiro, viscosità e altre azioni da includere nel dimensionamento effettivo del giunto.

Nel caso ideale di impedimento assiale completo e risposta elastica, la tensione termica ha modulo EαΔT, pari a 88,2 MPa con E = 210 GPa. Un gradiente termico nello spessore introduce invece curvatura e possibili azioni da vincolo. Confondere temperatura uniforme e gradiente porta a errori di schema.

### Appoggi e costruzione

Un appoggio deve trasmettere le azioni previste e permettere i movimenti assegnati. Attrito, rotazione, spostamento, stabilità e sostituibilità vanno coordinati; il punto fisso influenza la distribuzione di frenatura e temperatura. Le quote di posa e la temperatura al montaggio determinano la posizione iniziale nel campo di escursione.

Le NTC 2018 §5.1.4.7 richiedono: «Le verifiche di sicurezza vanno svolte anche per le singole fasi di costruzione dell’opera». La fase di getto e quella di rimozione dei sostegni temporanei possono governare anche quando la configurazione definitiva è soddisfacente. La controfreccia compensa una quota attesa, non annulla lo stato tensionale.

In ANTHEA le fasi della sezione composta aiutano a organizzare azioni e rigidezze. L’esempio di Bridge Design va letto con i propri limiti: non rappresenta un progetto completo di appoggi o giunti. Fonti: NTC 2018, §§5.1.4.5 e 5.1.4.7; FHWA, esempio di progetto LRFD di un impalcato a travi metalliche, passo 6 sugli appoggi.

### Proseguire nella Wiki

- [Cos'è un ponte?](wiki:bridge)
- [Travi metalliche da ponte fra montaggio fatica e sezione composta](wiki:ponti-acciaio-fasi)

