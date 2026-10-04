"""Record the scope and evidence of the completed article reviews, not certification."""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[3]
W=ROOT/'X.Desktop/Wiki'
articles=json.loads((W/'index.json').read_text(encoding='utf-8'))
reviews={}
def record(keys,scope,evidence,status='reviewed',open_points=None):
    for key in keys.split():
        assert any(a['key']==key for a in articles),key
        for p in evidence:
            if not p.startswith('https:'):assert (ROOT/p).exists(),p
        reviews[key]=dict(date='2026-10-04',status=status,scope=scope,evidence=evidence,openPoints=open_points or [])

record('''guida-avvio-e-scelta-del-modulo guida-progetti-e-gestione-del-lavoro
guida-salvataggio-e-report guida-percorso-completo-per-un-primo-progetto
guida-problemi-frequenti-e-controlli-finali''',
'Procedure confrontate con catalogo, archivio, gerarchia, revisioni, report e prove WPF. Corrette citazioni a documenti separati. Verificata immutabilità degli archivi storici e distinzione fra revisione dei dati e versione del motore.',
['X.Core/ProjectRevisions.cs','X.Core/ProjectSharedHierarchy.cs','X.Desktop/Wpf/ProjectReport.cs','supporto/test/Desktop/ProjectWorkspaceSmokeChecks.cs','supporto/test/Desktop/ProjectHierarchySmokeChecks.cs','supporto/test/Desktop/ProjectReportSmokeChecks.cs'])
record('guida-wiki-e-centro-della-conoscenza guida-tutorial-dal-modello-beam-alla-verifica-di-sezione guida-interpretazione-dei-risultati-e-controlli-indipendenti tracciabilita-e-riferimenti',
'Percorsi e limiti del lettore verificati in WPF. Tutorial: reazioni 100 kN, momento 200 kNm, azioni e geometria del foglio generato controllate. Archivio e vecchi indirizzi confrontati con le 85 origini.',
['supporto/test/Desktop/WikiChecks.cs','supporto/test/wiki-handbook-checks.py','X.Desktop/Wpf/WikiCatalog.cs'])
record('architettura-del-calcolo-e-convenzioni esempi-trasversali-e-lettura-critica load-path azioni-e-combinazioni-del-modello',
'Riviste convenzioni, conversioni dimensionali, equilibrio, simultaneità delle azioni e confini dei motori. Esempi aritmetici controllati; nessun coefficiente normativo assegnato universalmente.',
['supporto/test/CalculationLibrary.Checks/Program.cs','supporto/test/wiki-handbook-checks.py','X.Calculations/ModuleCatalog.cs','X.Core/SectionActionsExcel.cs'])
record('elementi-shell releases-e-connettivita dinamica-e-sisma-del-modello fasi-costruttive-e-percorso-dei-carichi',
'Revisione dei modelli didattici, unità delle risultanti, nodi, singolarità, oscillatore e fasi. Corretto superficie media; aggiunti esempi dimensionali e fonti primarie. Non attribuiti solutori generali ad Anthea.',
['https://www.scia.net/en/support/faq/scia-engineer/results/calculation-1d-and-2d-results','https://doc.comsol.com/6.4/doc/com.comsol.help.sme/sme_ug_modeling.05.071.html','https://interactivetextbooks.citg.tudelft.nl/computational-modelling/structural_linear/space_frame.html','https://ocw.mit.edu/courses/16-20-structural-mechanics-fall-2002/609687cf29516e13e864ff310af328a7_unit20.pdf','https://steelconstruction.info/topics/design/composite-construction/'])
record('guida-materiali-e-durabilita calcestruzzo-armature-e-copriferro',
'Riscontrati formule di progetto, copriferro e cataloghi implementati, combinazione delle esposizioni e proprietà condivise. Prove materiali WPF, libreria autonoma e ConcreteCode. I prospetti storici dichiarati non sono presentati come aggiornamento automatico delle norme.',
['supporto/test/Desktop/MaterialsSmokeChecks.cs','supporto/test/CalculationLibrary.Checks/Program.cs','X.Calculations/Materials/NtcCover.cs','X.Materiali/MixAutomation.cs'])
record('guida-palo-verticale guida-micropalo-verticale palo-verticale micropalo-verticale',
'Riscontrati input, unità, falda, rami drenato/non drenato, abaco parametrico e limiti. Confrontati casi salvati e controllati indipendentemente superficie laterale, peso e conversioni. Le correlazioni sono descritte come modelli implementati, non come parametri misurati o prescrizioni universali.',
['X.Calculations/Calcolo.cs','X.Calculations/Nq.cs','supporto/test/casi_confronto.json','supporto/test/wiki-handbook-checks.py'])
record('guida-pali-e-micropali-caricati-orizzontalmente capacita-orizzontale-con-broms',
'Rivisti equilibri dei meccanismi, pressioni per unità di lunghezza, segni, campo del momento automatico, anello CHS e interazione N–M. Superati 1080 controlli Broms, controlli CHS e 1645 stratificati; dichiarata natura sperimentale delle estensioni.',
['X.Calculations/PaloOrizzontale.cs','X.Calculations/MicropaloOrizzontale.cs','supporto/test/X.Verifiche/HorizontalChecks.cs','supporto/test/X.Verifiche/HorizontalStratifiedChecks.cs'])
record('guida-palificata-orizzontale',
'Procedura verificata rispetto a geometria, proiezioni, interassi rappresentativi e risultati. Superati 94 controlli numerici/UI. Il fattore Davisson resta distinto dalla media p-multiplier; le attribuzioni della teoria hanno un riscontro aperto separato.',
['X.Desktop/Wpf/HorizontalPileGroupWorkspace.cs','X.Calculations/HorizontalPileGroup.cs','supporto/test/HorizontalPileGroup.Checks/Program.cs'])
record('palificata-orizzontale',
'Formule implementate ed esempi numerici controllati; mantenuti distinti significato fisico, estrapolazioni e attribuzioni bibliografiche.',
['supporto/test/HorizontalPileGroup.Checks/Program.cs','X.Calculations/HorizontalPileGroup.cs'],
'qualified',['La tabella attribuita a Davisson proviene dalla specifica utente: resta da riscontrare direttamente nel lavoro originale. Le formule Rollins/FEMA richiedono completamento della verifica dei testi originali. I 94 controlli non dimostrano tale attribuzione.'])
record('guida-palo-elastico palo-elastico',
'Verificate dimensioni di kh, nh e k, segni, condizioni al contorno, integrazione Hermite, recupero equilibrato e convergenza. Superati 101 controlli, con riferimenti analitici e tabellati. L’edizione non identificabile della scansione Viggiani e la mancata consultazione degli articoli originali rimangono dichiarate, senza nuove attribuzioni.',
['X.Calculations/ElasticHorizontalPile.cs','X.Desktop/Wpf/ElasticPileWorkspace.cs','supporto/test/ElasticPile.Checks/Program.cs'])
record('sezione-in-calcestruzzo-armato',
'Rivisti piano delle deformazioni, domini, segni, rami fessurazione/taglio/torsione, fori, tendini e limiti. Esempio fessurazione ricalcolato; 434 controlli dei profili e 73 della libreria. Attribuzioni nazionali incerte restano nella matrice qualificata.',
['supporto/test/ConcreteCode.Checks/Program.cs','supporto/test/CalculationLibrary.Checks/Program.cs','X.Calculations/SectionNeutralAxis.cs','supporto/test/wiki-handbook-checks.py'])
record('guida-sezione-composta-da-ponte sezione-composta-da-ponte taglio-irrigidimenti-e-connessione-della-sezione-composta',
'Corretto l’ingresso Altezza totale H: 1855 mm per H inclinata e 1850 mm per cassoncino negli esempi con hw=1800 mm. Ricalcolati geometria, tensioni, torsione e distorsione con riferimenti indipendenti: 172+2957+189+191 controlli superati. Rivisti fasi, proprietà equivalenti, unità, limiti e distinzione fra comportamento implementato e prescrizione.',
['X.Calculations/BridgeSection.Shear.cs','X.Calculations/SectionNeutralAxis.cs','supporto/test/X.Verifiche/BridgeSectionChecks.cs','supporto/test/X.Verifiche/BridgeMethodChecks.cs','supporto/test/X.Verifiche/BridgeInclinedGuideChecks.cs','supporto/test/X.Verifiche/BridgePredalleChecks.cs'])
record('guida-bridge-design bridge-design',
'Rivisti input/output, famiglie, grandezze, analisi equivalente, griglia discreta, costi e CO2. Confronto indipendente: 100 travi e 26 alternative, 80133 confronti. Le regole geometriche empiriche e il predimensionamento non sono presentati come verifica completa del ponte.',
['X.Calculations/BridgeConcept.Calculation.cs','X.Calculations/BridgeConcept.Optimization.cs','supporto/test/BridgeDesign.IndependentChecks/Program.cs','supporto/test/BridgeDesign.IndependentChecks/verify.py'])
record('guida-muri-di-sostegno-con-stratigrafie-di-monte-e-valle guida-portanza-sismica-cedimenti-spostamenti-e-armature-rev07 muri-di-sostegno-e-stabilita-globale portanza-sismica-cedimenti-spostamenti-e-armature-rev07',
'Rivisti riferimenti geometrici, combinazioni, modelli separati, fattori F e gammaR, unità e segni degli spostamenti. Recuperato caso con esito sfavorevole. Superati 345 controlli dei muri. Distinte prove numeriche, applicabilità delle ipotesi e confronto MAX non disponibile.',
['X.Calculations/RetainingWall.Actions.cs','X.Calculations/RetainingWall.Serviceability.cs','supporto/test/RetainingWall.Checks/Program.cs','supporto/test/RetainingWall.Checks/SeismicSoilChecks.cs','supporto/test/RetainingWall.Checks/MaterialSharingChecks.cs'])
assert len(reviews)==39,len(reviews)
(ROOT/'supporto/docs/wiki-riscontri.json').write_text(json.dumps(reviews,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(len(reviews),'riscontri registrati')
