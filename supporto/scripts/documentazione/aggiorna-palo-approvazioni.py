"""Rev30: approvazioni, esiti e rappresentazione dell'armatura trasversale del palo."""
from pathlib import Path
import hashlib,json,shutil,subprocess,sys
root=Path(__file__).resolve().parents[3];support=root/'supporto'
archive=support/'SUPERATI/palo-approvazioni-rev30-20261006'
builder=support/'scripts/Build-AntheaGuides-Itec.py'
assert "REVISION = '29'" in builder.read_text(encoding='utf-8'), 'Rileggere la revisione corrente prima di applicare'
paths=[support/x for x in ['docs/guida-pratica-anthea.md','docs/guida-pratica-anthea.pdf','docs/guida-teorica-anthea.md','docs/guida-teorica-anthea.pdf','README.md','README.pdf','installer/Indice-guide.md','installer/Indice-guide.pdf']]
paths+=list((support/'documentazione/Guide_ANTHEA').glob('*Rev29.*'))
records=[]
for p in paths:
    dest=archive/p.relative_to(root);dest.parent.mkdir(parents=True,exist_ok=True)
    if not dest.exists():shutil.copy2(p,dest)
    records.append(dict(origine=str(p.relative_to(root)),archivio=str(dest.relative_to(root)),sha256=hashlib.sha256(dest.read_bytes()).hexdigest(),motivo='Sostituita dalla revisione 30: ipotesi, esiti e staffe/spirali del palo',revisione_sostitutiva=30))
(archive/'registro.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
practical='''### Approvazione delle ipotesi ed esito delle armature

In Tratti di armatura, Approva tutto · ipotesi conferma in un solo passaggio azioni di progetto, modello di taglio circolare, trattenimento delle barre compresse, dettagli delle estremità e buona aderenza. I valori numerici, le armature, i coefficienti e i vincoli della proposta restano quelli assegnati. Il pulsante Approva tutto · sisma, disponibile quando la modalità sismica è attiva, conferma anche le ipotesi ordinarie, la provenienza sismica di N e V e il momento elastico non ridotto con N concomitante. Le singole conferme rimangono modificabili. Il comando registra una scelta dell'utente e avvia il ricalcolo delle verifiche; non sostituisce un esito negativo con uno positivo.

Il riepilogo a destra indica quanti tratti sono non verificati, incompleti o hanno tutti i controlli eseguiti soddisfatti. Selezionando un tratto, il riquadro rosso segnala controlli non soddisfatti; quello ambra indica dati o verifiche da completare; quello verde vale esclusivamente per i controlli eseguiti. Le verifiche fuori dal perimetro sono elencate separatamente. Ancoraggi insufficienti, giunti non utilizzabili, resistenze mancanti e controlli sismici non soddisfatti restano riconoscibili anche dopo Approva tutto. Cambiare un input invalida subito gli esiti; la risposta FEM viene riutilizzata quando i dati fisici non cambiano.

NTC §7.2.5 apre una finestra interna ad ANTHEA con la sintesi dei criteri e una seconda scheda contenente la pagina originale consultata, PDF 217 / stampata 213. Zoom e scorrimento consentono di leggerla anche senza Internet. Non viene aperto un browser.

L'Armatura trasversale principale può essere Staffe singole, Spirale oppure Da definire. I tratti collegati ereditano la scelta; i tratti personalizzati possono scegliere una tipologia diversa nella propria riga. Gli archivi conservano le scelte precedenti; per nuovi dati il tipo iniziale è Staffe singole. Diametro e passo rimangono quelli della sezione o del tratto. La spirale è rappresentata in elevazione e nella vista laterale; la distinta riporta la marca SP, il numero di spire, il passo effettivo e la lunghezza geometrica. Le staffe hanno marca S e quantità di anelli. Le note riportano anche i diametri, il passo massimo e le quote.

La lunghezza geometrica non comprende ganci, chiusure, ancoraggi, giunti o sfridi e non è una lunghezza di taglio esecutiva. Il verificatore attuale del taglio usa staffe singole: selezionando Spirale il taglio resta non verificato e il dimensionamento resistente automatico non viene eseguito. Nella zona dissipativa di testa il controllo NTC delle staffe singole non è soddisfatto da una spirale. Disegno, verifica e dettagli costruttivi rimangono distinti.

'''
theory='''### Geometria delle staffe e delle spirali e significato delle approvazioni

La tipologia trasversale è condivisa dai tratti ancora collegati; un tratto personalizzato può modificarla. Checker calcola all'asse il raggio r = D/2 − c − φt/2, con c copriferro esterno e diametro φt dell'armatura trasversale. Per le staffe circolari, le quote derivano dalla suddivisione uniforme del tratto con passo non superiore a quello assegnato. Un confine interno appartiene al tratto successivo; l'ultimo comprende la punta. La lunghezza geometrica degli anelli è n · 2πr.

Per la spirale si assume, solo ai fini della geometria nominale, un numero intero di spire pari all'arrotondamento per eccesso di L/pmax; il passo effettivo è L/n. La lunghezza dell'elica è la diagonale del rettangolo ottenuto sviluppando il cilindro, con lati n · 2πr e L. Le unità sono convertite in Checker. La proiezione laterale campiona ciascuna spira in 24 intervalli e distingue metà visibile e nascosta. Il campionamento grafico è indipendente dalla mesh FEM. Ganci, chiusure, ancoraggi delle estremità e giunzioni della spirale non sono dedotti dalla sola geometria: la quantità calcolata non è una distinta esecutiva di taglio.

Non è introdotta un'equivalenza resistente tra spirale e staffe singole. La selezione Spirale disabilita il risultato a taglio del modello corrente a due bracci chiusi; le sollecitazioni FEM e il dominio N–M mantengono le ipotesi esistenti. La verifica sismica di testa continua a richiedere staffe singole. I test indipendenti comprendono 30 anelli di diametro all'asse 900 mm, una spirale di 30 spire su 6 m, sviluppo piano dell'elica, quote di confine e passo inferiore al massimo assegnato.

Approva tutto è un'operazione sugli input dichiarativi: conferma le ipotesi indicate dal pulsante, conserva le grandezze numeriche e registra l'origine della scelta. Non modifica resistenze, domanda o risultati delle verifiche. Il riepilogo è derivato dagli esiti correnti e distingue non soddisfatto, incompleto, soddisfatto nel perimetro eseguito e verifiche escluse. I risultati obsoleti non restano presentati come validi durante il ricalcolo. La fonte normativa interna è la riproduzione della pagina NTC già consultata, non una nuova formulazione normativa.

'''
for kind,addition,marker in [('pratica',practical,'### Controlli sismici della testa del palo'),('teorica',theory,'### Zona dissipativa di testa secondo NTC 2018')]:
    p=support/f'docs/guida-{kind}-anthea.md';text=p.read_text(encoding='utf-8-sig');assert marker in text
    text=text.replace(marker,addition+marker,1).replace('revisione documentale 29','revisione documentale 30')
    text=text.replace('La fonte è apribile dal pulsante dedicato: NTC 2018 §7.2.5, Gazzetta Ufficiale, pagina PDF 217, stampata 213.', 'Il pulsante NTC apre la finestra interna con sintesi e pagina originale: NTC 2018 §7.2.5, Gazzetta Ufficiale, pagina PDF 217, stampata 213.')
    p.write_text(text,encoding='utf-8')
for relative in ['README.md','installer/Indice-guide.md']:
    p=support/relative;text=p.read_text(encoding='utf-8-sig').replace('Rev29','Rev30').replace('Revisione 29','Revisione 30')
    text+='\n- Revisione 30: approvazione delle ipotesi del palo, esiti distinti, fonte NTC interna e rappresentazione di staffe e spirali.\n'
    p.write_text(text,encoding='utf-8')
builder.write_text(builder.read_text(encoding='utf-8').replace("REVISION = '29'","REVISION = '30'").replace("REVISION_NOTE = 'BIBLIOTECA TECNICA E AIUTI CONTESTUALI'","REVISION_NOTE = 'IPOTESI ESITI E ARMATURE TRASVERSALI DEL PALO'"),encoding='utf-8')
p=support/'scripts/Render-AntheaGuides-Itec.ps1';p.write_text(p.read_text(encoding='utf-8-sig').replace("$Revision = '29'","$Revision = '30'"),encoding='utf-8-sig')
art=support/'artefatti/guide_anthea_itec_rev30';art.mkdir(parents=True,exist_ok=True)
(art/'artifact.md').write_text('Aggiornare localmente le due guide mantenendo modello ITEC, biblioteca e formule. Documentare Approva tutto, esiti, NTC interna, staffe e spirali e limiti del modello resistente.',encoding='utf-8')
subprocess.run([sys.executable,str(builder)],check=True)
for relative in ['README.md','installer/Indice-guide.md']:subprocess.run([sys.executable,str(support/'scripts/documentazione/markdown-pdf.py'),str(support/relative)],check=True)
subprocess.run([sys.executable,str(support/'scripts/wiki/build-wiki-index.py')],check=True)
