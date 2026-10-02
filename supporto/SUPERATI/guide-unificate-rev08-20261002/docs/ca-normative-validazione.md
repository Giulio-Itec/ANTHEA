# Validazione — taglio e fessurazione CA

Data: 28 settembre 2026. Sette profili normativi, cinque geometrie del catalogo.
[Matrice di copertura e parti ancora mancanti](guida-teorica-anthea.md).

## Confronti indipendenti

`supporto/test/ConcreteCode.Checks/reference.py` esegue funzioni Python pubblicate
da **fib StructuralCodes**, fissate al commit
`3e9c3f5cffb0c28e083257346006c7eac02384fd`. Non richiama ANTHEA e non ricava gli
attesi dal C#. Via AST carica solo funzioni e import standard necessari, lasciando
invariati i corpi matematici. Non usa le funzioni che richiedono SciPy.
URL e SHA256 dei sorgenti sono conservati in `reference.json`.

I 72 casi indipendenti comprendono 48 casi di taglio EC2/MC: quattro resistenze
fck (20, 35, 70, 80 MPa), N compresso/nullo/teso, con/senza staffe. I 24 casi di wk
EC2 variano durata, aderenza, interasse e tensione. Tolleranza C#: 10⁻⁹ moltiplicata
per max(1, |atteso|), numerica e non interpretabile come margine di sicurezza.

- [fib: taglio EC2](https://fib-international.github.io/structuralcodes/api/codes/ec2_2004/shear.html).
- [fib: fessurazione EC2](https://fib-international.github.io/structuralcodes/api/codes/ec2_2004/cracks.html).
- [fib: taglio MC2010](https://fib-international.github.io/structuralcodes/api/codes/mc2010/shear.html).

Il benchmark [SOFiSTiK DCE-EN6](https://docs.sofistik.com/2026/en/verification/_static/verification/pdf/dce-en6.pdf)
fornisce un confronto DIN esterno: b=300 mm, d=450 mm, z=384 mm, fcd=17 MPa,
ν1=0,75, VRd,max=734,4 kN per cotθ=1. Con VEd=343,25 kN il test rifiuta cotθ=2,
oltre il limite dipendente dal carico. Si confrontano questi valori locali;
non si dichiara riprodotto l'intero modello della trave SOFiSTiK.

Per DS, NS, UNI e fessurazione MC/DIN si usano confronti a formula chiusa,
controlli dei limiti e prove di integrazione. Non è disponibile un secondo
software indipendente per ogni variante. Fonti consultate:

- [Appendice italiana DM 31/07/2012, Allegato Eurocodice 2](https://www.gazzettaufficiale.it/atto/serie_generale/caricaArticolo?art.codiceRedazionale=13A02562&art.dataPubblicazioneGazzetta=2013-03-27&art.flagTipoArticolo=3&art.idArticolo=1&art.idGruppo=0&art.idSottoArticolo=1&art.idSottoArticolo1=10&art.progressivo=0&art.versione=1): consultate anche le immagini originali, pagine 82–85.
- [DS/EN 1992-1-1 DK NA:2024](https://www.bygningsreglementet.dk/media/rtdfjh4m/ds-en-1992-1-1-dk-na-2024_2024-07-01-a.pdf): documento normativo originale per taglio, k3 e sistemi di fessure.
- [SCIA: implementazione degli annessi](https://help.scia.net/19.1/en/krs/attachments/theory_na_en_1992_enu.pdf): riscontro DIN e NS; documentazione dell'implementatore, distinta dal testo originale degli annessi.
- [SCIA: NS NA:2010](https://help.scia.net/25.0/en/national_annexes/en1992/norway.htm): riscontro norvegese di combinazioni e coefficienti.
- [Terjesen et al., 2024](https://onlinelibrary.wiley.com/doi/full/10.1002/suco.202300367): confronto pubblicato dei modelli di fessurazione. Non sono stati riutilizzati i dati sperimentali dell'articolo per validare il modulo.

## Altre prove

Taglio: inversione V/M, carico nullo, ricerca automatica di cotθ confrontata con
scansione dell'intervallo, staffe inclinate, dati non finiti, granulometria e forte
trazione NS, αcw UNI del c.a. non precompresso.

Fessurazione: limite DIN della distanza tra fessure, barre distanti, durata,
coefficiente DS del copriferro, profondità efficace calcolabile a mano,
compressione totale, trazione uniforme, sistemi DS fine/grossolano, superfici
interne armate e coerenza tra inviluppo e riepilogo. Le geometrie vengono
esercitate con tutti i profili; sui cerchi il modello di taglio è esplicito.

WPF: persistenza Mx/My nel taglio (il precedente Commit li eliminava), modifica
Mx → aggiornamento εx per Vy nel MC, invalidazione da dg comune, scambio Excel,
salvataggio/riapertura, selezione DS, aggiornamento viste e report.

## Riproduzione

Da radice repository, con Python 3 e .NET 8:

```powershell
python supporto/test/ConcreteCode.Checks/reference.py --download
dotnet run --project supporto/test/ConcreteCode.Checks -c Release
dotnet run --project supporto/test/X.Verifiche -c Release -- --checker
dotnet run --project supporto/test/X.Verifiche -c Release -- --ca-module
dotnet build X.Desktop/X.Desktop.csproj -c Release
./supporto/scripts/Test-CalculationUi.ps1 -Suites ca-features,ca-extensions
```

Il JSON degli attesi è versionato; la suite C# non richiede rete né Python.
La rigenerazione scarica i tre sorgenti fissati in
`supporto/artefatti/ca-normative/fonti`. Controllare il diff prima di accettare
variazioni degli attesi. Log, schermate e report generati stanno sotto
`supporto/artefatti/ca-normative/` e non sono versionati.

| Gruppo | Risultato |
| --- | --- |
| Normative CA, riferimenti e geometrie | 434 controlli superati |
| Checker / Excel / estensioni / dati | 102 + 19 + 174 + 27 superati |
| Modulo CA ampliato | 78 superati |
| WPF funzionalità / estensioni | 143 + 94 superati |
| WPF foglio completo | 201 superati, attestazione finale presente |
| Compilazione integrata | Release: 0 errori, 0 avvisi |

Le prime prove isolate usano un'esportazione della base 91f1061 con i file CA
sovrapposti, separata dalle lavorazioni simultanee sul ponte. La compilazione
integrata verifica anche le DLL aggiornate successivamente nel repository.
I risultati dimostrano i confronti elencati; non attestano l'intero contenuto
degli annessi, né validazione sperimentale del comportamento reale delle fessure.
