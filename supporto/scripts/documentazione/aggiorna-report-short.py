from pathlib import Path
import json, shutil, subprocess, sys
root=Path(__file__).resolve().parents[3]
support=root/'supporto'
archive=support/'SUPERATI/report-short-rev28-20261005'
archive.mkdir(parents=True,exist_ok=True)
records=[]
for relative in ['docs/guida-pratica-anthea.md','docs/guida-pratica-anthea.pdf','docs/guida-teorica-anthea.md','docs/guida-teorica-anthea.pdf','README.md','README.pdf','installer/Indice-guide.md','installer/Indice-guide.pdf']:
    source=support/relative;dest=archive/relative;dest.parent.mkdir(parents=True,exist_ok=True)
    if not dest.exists(): shutil.copy2(source,dest)
    records.append({'origine':relative,'archivio':str(dest.relative_to(support)),'motivo':'Sostituita dalla revisione 28 con Report short CLS','revisione_sostitutiva':'28'})
practical='''### Report short della sezione in calcestruzzo armato

Accanto a Report Word, il pulsante Report short esporta il foglio corrente in Word e PDF omonimi, entro due pagine. Attendere il completamento del calcolo automatico. Il comando richiede Microsoft Word installato: lo usa in background per verificare la paginazione e produrre il PDF. Il report completo rimane disponibile dal pulsante precedente.

La prima pagina raccoglie geometria, coordinate delle armature o definizione degli anelli regolari, staffe, materiali, coefficienti e opzioni di calcolo. La seconda contiene una combinazione governante per ciascuna verifica: SLU N–Mx–My, SLV N–Mx–My, taglio, tensioni rara, fessurazione frequente, tensioni quasi permanente e fessurazione quasi permanente. Azioni, resistenze o limiti, tasso ed esito sono racchiusi in due tabelle.

La scelta considera tutte le combinazioni inserite, comprese quelle nascoste nei grafici. Tensioni e fessurazione possono avere combinazioni governanti diverse. Per il taglio sono riportate entrambe le direzioni della stessa combinazione. Una famiglia senza azioni è indicata come non richiesta; risultati mancanti o privi di tasso numerico producono un esito incompleto. I criteri senza rapporto numerico, come la decompressione, richiedono il report completo.

Il report short non comprende torsione e interazioni, dettagli costruttivi, ancoraggi, domini 2D e momento–curvatura. Se dati geometrici molto estesi o nomi lunghi impediscono di rispettare due pagine, l'esportazione viene fermata senza tagliare dati: abbreviare i nomi o utilizzare il report completo. Il file di calcolo resta il riferimento riapribile.

'''
theory='''### Selezione delle verifiche nel report short

Il report short è una rappresentazione dei risultati correnti, senza nuovi calcoli resistenti. Per ogni famiglia sceglie il massimo tasso di lavoro numerico finito non arrotondato, mantenendo concomitanti azioni e risultati della combinazione selezionata. La selezione è distinta per SLU N–Mx–My, SLV N–Mx–My, taglio, tensioni rara, fessurazione frequente, tensioni quasi permanente e fessurazione quasi permanente.

Per N–Mx–My usa il tasso e il punto resistente del dominio 3D con il criterio salvato. Per il taglio usa il massimo dei rapporti nelle direzioni x e y e riporta entrambe le direzioni della medesima combinazione. Il tasso tensionale raro considera il massimo fra calcestruzzo compresso e acciaio in valore assoluto, confrontati con i rispettivi limiti; quello quasi permanente riguarda il calcestruzzo. Per la fessurazione usa wk/wlim e conserva distanza fra fessure, differenza di deformazioni e aree efficaci quando disponibili nella traccia. Il calcolo come limite superiore senza barre efficaci è esplicitamente riconoscibile.

Il numero di combinazioni con tasso disponibile viene confrontato con le azioni della famiglia. Un risultato mancante non viene trasformato in zero né escluso dalla valutazione di completezza: il massimo disponibile può essere mostrato, ma la riga rimane incompleta. Criteri non esprimibili con un rapporto, comprese le verifiche di decompressione, non vengono ordinati artificiosamente. Una famiglia priva di azioni è distinta da una verifica incompleta. Gli arrotondamenti riguardano soltanto la stampa.

La sintesi copre esclusivamente le sette famiglie riportate. Non costituisce attestazione di conformità per torsione, dettagli costruttivi, ancoraggi o altre verifiche del modulo. Il limite di due pagine viene controllato sull'impaginazione effettiva del documento Word prima del salvataggio dei due formati; non si riduce il contenuto tecnico per superare tale controllo.

'''
for kind,addition,marker in [('pratica',practical,'### Leggere asse neutro, mappe e report'),('teorica',theory,'### Apertura delle fessure')]:
    path=support/f'docs/guida-{kind}-anthea.md';text=path.read_text(encoding='utf-8-sig')
    if addition.splitlines()[0] not in text:
        assert marker in text;text=text.replace(marker,addition+marker,1)
    text=text.replace('revisione documentale 27','revisione documentale 28')
    path.write_text(text,encoding='utf-8')
for relative in ['README.md','installer/Indice-guide.md']:
    p=support/relative;t=p.read_text(encoding='utf-8-sig').replace('Rev27','Rev28').replace('Revisione 27','Revisione 28')
    p.write_text(t,encoding='utf-8')
p=support/'scripts/Build-AntheaGuides-Itec.py';t=p.read_text(encoding='utf-8').replace("REVISION = '27'","REVISION = '28'").replace("REVISION_NOTE = 'MATERIALI E DISTINTA FERRI DEI MURI'","REVISION_NOTE = 'REPORT SHORT DELLE SEZIONI IN CALCESTRUZZO'");p.write_text(t,encoding='utf-8')
p=support/'scripts/Render-AntheaGuides-Itec.ps1';p.write_text(p.read_text(encoding='utf-8-sig').replace("$Revision = '27'","$Revision = '28'"),encoding='utf-8-sig')
art=support/'artefatti/guide_anthea_itec_rev28';art.mkdir(parents=True,exist_ok=True)
(art/'artifact.md').write_text('Integrare Report short CLS nelle due guide globali, conservando il modello ITEC, contenuti e formule esistenti. Documentare selezione, completezza, ambito, Word e PDF e limite di due pagine.',encoding='utf-8')
(archive/'registro.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
subprocess.run([sys.executable,str(support/'scripts/Build-AntheaGuides-Itec.py')],check=True)
for relative in ['README.md','installer/Indice-guide.md']:
    subprocess.run([sys.executable,str(support/'scripts/documentazione/markdown-pdf.py'),str(support/relative)],check=True)
