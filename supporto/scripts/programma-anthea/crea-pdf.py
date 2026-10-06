from pathlib import Path
import json,math,datetime,html
from reportlab.platypus import SimpleDocTemplate,Paragraph,Spacer,Table,TableStyle,PageBreak,KeepTogether
from reportlab.lib.styles import getSampleStyleSheet,ParagraphStyle
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont

root=Path(__file__).resolve().parents[3];art=root/'supporto/artefatti/programma-anthea-20261006'
d=json.loads((art/'contenuti.json').read_text(encoding='utf-8'))
dest=root/'supporto/documentazione/Programma_ANTHEA/ANTHEA_Programma_sviluppo_2026-10-06.pdf'
for name,file in [('Arial','arial.ttf'),('Arial-Bold','arialbd.ttf')]:pdfmetrics.registerFont(TTFont(name,'C:/Windows/Fonts/'+file))
pdfmetrics.registerFontFamily('Arial',normal='Arial',bold='Arial-Bold',italic='Arial',boldItalic='Arial-Bold')
navy=colors.HexColor('#203D59');light=colors.HexColor('#EEF3F8');grey=colors.HexColor('#526475')
styles=getSampleStyleSheet()
for key in ['Normal','BodyText']:styles[key].fontName='Arial';styles[key].fontSize=10;styles[key].leading=14;styles[key].spaceAfter=7
styles.add(ParagraphStyle('TitleA',fontName='Arial-Bold',fontSize=26,leading=31,textColor=navy,spaceAfter=18))
styles.add(ParagraphStyle('HeadA',fontName='Arial-Bold',fontSize=17,leading=21,textColor=navy,spaceAfter=12))
styles.add(ParagraphStyle('SubA',fontName='Arial-Bold',fontSize=11,leading=15,textColor=navy,spaceBefore=9,spaceAfter=5))
styles.add(ParagraphStyle('SmallA',fontName='Arial',fontSize=8.5,leading=11,spaceAfter=4))
styles.add(ParagraphStyle('TH',fontName='Arial-Bold',fontSize=9,leading=12,textColor=colors.white))
styles.add(ParagraphStyle('Cell',fontName='Arial',fontSize=9,leading=12,spaceAfter=0))
story=[];W=A4[0]-88
def clean(x):return str(x).replace('–','-').replace('—','-').replace('‑','-')
def p(x,style='BodyText'):return Paragraph(html.escape(clean(x)).replace('\n','<br/>'),styles[style])
def text(x,style='BodyText'):story.append(p(x,style))
def title(x):text(x,'HeadA')
def table(headers,rows,widths):
    t=Table([[p(x,'TH') for x in headers]]+[[p(x,'Cell') for x in r] for r in rows],colWidths=[W*v/sum(widths) for v in widths],repeatRows=1,hAlign='LEFT')
    t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),navy),('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),7),('RIGHTPADDING',(0,0),(-1,-1),7),('TOPPADDING',(0,0),(-1,-1),7),('BOTTOMPADDING',(0,0),(-1,-1),7),('ROWBACKGROUNDS',(0,1),(-1,-1),[light,colors.white]),('LINEBELOW',(0,0),(-1,0),.5,navy)]))
    story.append(t);story.append(Spacer(1,10))
def page():story.append(PageBreak())
start=datetime.date.fromisoformat(d['start']);plans=[];cf=cp=0
for ph in d['phases']:
    items=[t for t in d['tasks'] if t['phase']==ph[0]];lo,base,hi=[sum(t[k] for t in items) for k in ('lo','base','hi')]
    wf=math.ceil(base*1.2/5);wp=math.ceil(base*1.2/2);cf+=wf;cp+=wp
    plans.append(dict(id=ph[0],name=ph[1],milestone=ph[2],gate=ph[3],lo=lo,base=base,hi=hi,wf=wf,wp=wp,cf=cf,cp=cp,full=start+datetime.timedelta(days=7*cf-1),part=start+datetime.timedelta(days=7*cp-1)))
def date(x):return x.strftime('%d/%m/%Y')
totals={k:sum(t[k] for t in d['tasks']) for k in ('lo','base','hi')}
text('ANTHEA\nProgramma di sviluppo','TitleA')
text('Ricognizione, rilasci, milestone e integrazione BIM','SubA')
text('Proposta del 6 ottobre 2026 · Revisione 01')
text('Il programma sviluppa tutti i filoni della bozza 00-Programma.xlsx e aggiunge le funzioni già realizzate, le attività residue, i criteri di accettazione e una proposta finale per Revit e Tekla. Il file Excel allegato è modificabile; questo PDF ne presenta gli stessi contenuti in forma di lettura.')
table(['Passaggio','Tempo pieno','2 giorni/settimana'],[[r['milestone']+' · '+label,str(r['cf'])+' settimane\n'+date(r['full']),str(r['cp'])+' settimane\n'+date(r['part'])] for r,label in [(plans[3],'primo rilascio esteso'),(plans[8],'rilascio funzionale'),(plans[-1],'BIM Revit e Tekla')]], [2.2,1,1])
text('Il piano centrale stima 275 giornate-persona residue, prima del margine del 20%. Le fasi sono sequenziali: 72 settimane complessive a tempo pieno e 171 settimane a due giorni/settimana. Gli intervalli minimo/massimo sono rispettivamente 50-110 e 116-261 settimane, con arrotondamento per fase. Il primo pacchetto utilizzabile arriva prima del completamento del programma.')
text('Priorità proposta','SubA')
text('Chiudere prima affidabilità, dati comuni e casi pilota. Poi completare CLS/geotecnica, quindi azioni, acciaio, misto e CAP. Realizzare infine il nucleo BIM e i due connettori, riutilizzando gli stessi contratti. A ogni milestone il referente può approvare, correggere o ridurre il perimetro successivo.')
text('Indice del programma','SubA')
text('1. Ipotesi e calendario · 2. Stato attuale · 3. Attività e milestone · 4. Test e feedback · 5. Proposta BIM · 6. Opzioni escluse · 7. Tracciabilità e fonti. Gli ID S, T, M, TST e BIM corrispondono a quelli dell’Excel.')
page();title('1. Ipotesi e calendario')
for i,x in enumerate(d['assumptions'],1):text(f'{i}. {x}')
text('Una baseline misurata prima degli ampliamenti','SubA')
text('Le stime presuppongono riuso del codice e delle librerie attuali, AI per implementazione e test, requisiti concordati e accesso ai casi di confronto. Il primo ciclo serve a confrontare giornate previste/effettive. Se la capacità reale o il perimetro cambiano, aggiornare le stime in Attivita e i parametri in Tempi: le date di Sintesi e Milestone si ricalcolano.')
page();title('Calendario centrale per fase')
table(['Fase / milestone','Gg base','Sett. pieno','Fine pieno','Sett. 2 gg','Fine 2 gg'],[[r['id']+' / '+r['milestone']+'\n'+r['name'],r['base'],r['wf'],date(r['full']),r['wp'],date(r['part'])]for r in plans],[3,0.7,.85,1.25,.85,1.25])
text('Le date rappresentano la fine della settimana della fase, non una scadenza contrattuale. L’avvio del 12/10/2026 è un’ipotesi modificabile. I calendari non includono ferie, festività o attese aggiuntive del revisore. I tempi BIM riguardano entrambi i connettori, sviluppati in sequenza.','SmallA')
page();title('2. Stato attuale di ANTHEA')
text('Presente = implementato/documentato nel perimetro descritto. Parziale = riuso o funzione incompleta rispetto alla bozza. Non riscontrato = nessun modulo operativo corrispondente nel catalogo e nei percorsi consultati. La ricognizione non sostituisce il collaudo e non ha rieseguito integralmente tutte le suite.')
for row in d['inventory']:
    id,area,name,status,ready,limit,evidence,follow=row
    block=[p(f'{id} · {area} · {name}','SubA'),p(f'{status}. {ready}'),p(f'Da completare / limite: {limit}'),p(f'Evidenza: {evidence}. Seguito: {follow}.','SmallA')]
    story.append(KeepTogether(block))
page();title('3. Attività residue e milestone')
text('48 attività. Le stime comprendono il lavoro assistito da AI e i test di modulo. I valori minimo/base/massimo sono giornate-persona prima del margine. La baseline è sequenziale; le dipendenze elencate sono tecniche e non autorizzano sovrapposizioni di lavoro con una sola persona.')
text('Per accettare ogni milestone: consegna disponibile, test pertinenti completati, documentazione coerente, nessun difetto bloccante/grave aperto nel perimetro e decisione del referente. I difetti minori possono essere rinviati soltanto con una motivazione e un’attività assegnata.')
text('Le attività P2 rimangono incluse nel calendario centrale, ma vanno confermate prima della fase. Le opzioni O01-O05 sono escluse. La chiusura delle attività già presenti riguarda consolidamento e integrazione, non una riscrittura automatica.')
for ph in plans:
    page();title(ph['id']+' · '+ph['name'])
    table(['Milestone','Pieno','2 giorni/settimana','Stima gg min/base/max'],[[ph['milestone'],f"{ph['wf']} settimane\n{date(ph['full'])}",f"{ph['wp']} settimane\n{date(ph['part'])}",f"{ph['lo']} / {ph['base']} / {ph['hi']}"]],[.8,1.2,1.2,1.2])
    text('Criterio di chiusura: '+ph['gate'])
    for a in [t for t in d['tasks'] if t['phase']==ph['id']]:
        block=[p(f"{a['id']} · {a['title']} · {a['priority']}",'SubA'),p(a['scope']),p('Accettazione: '+a['accept']),p(f"{a['lo']} / {a['base']} / {a['hi']} gg · {a['status']} · Dipende da {a['deps']} · Test {a['test']}",'SmallA')]
        story.append(KeepTogether(block))
page();title('4. Test, feedback e accettazione')
text('Ogni modifica passa prima dal controllo dello sviluppatore. La demo si svolge ogni 10 giornate effettive di sviluppo: circa ogni due settimane a tempo pieno e ogni cinque settimane con due giorni/settimana. Gli incontri di milestone si aggiungono quando la consegna è pronta, senza attendere la demo periodica.')
text('Il referente tecnico fornisce i casi reali e la decisione sui risultati. Per la fase BIM partecipa anche un modellatore esperto del relativo software. Il tempo dei revisori è esterno alla capacità di sviluppo; pianificare 30-45 minuti per la demo e 60-90 minuti per il gate, oltre alla verifica indipendente dei casi.')
for x in d['tests']:
    id,when,who,kind,scope,accept,evidence,ph=x
    story.append(KeepTogether([p(f'{id} · {kind}','SubA'),p(f'Quando: {when}. Referenti: {who}.'),p(scope),p('Accettazione: '+accept),p(f'Evidenza: {evidence} Fasi: {ph}.','SmallA')]))
page();title('Gestione delle osservazioni e degli imprevisti')
text('Il foglio Test e feedback contiene un registro compilabile con ID, data, attività/milestone, osservazione, impatto, decisione, responsabile, stato e criterio di chiusura. Ogni osservazione deve terminare con una correzione verificata, un rinvio motivato o una modifica approvata del perimetro.')
table(['Evento','Decisione prevista'],[
['Risultato numerico incoerente','Bloccare il rilascio della funzione coinvolta, conservare il caso e confrontare motore, dati e convenzioni.'],
['Prestazioni peggiori della baseline','Misurare sullo stesso hardware. Spiegare la regressione e concordare il limite accettabile prima di estendere i casi.'],
['Aggiornamento delle DLL','Congelare la nuova versione, eseguire regressioni e aggiornare il manifest prima della distribuzione.'],
['Nuovo requisito durante la fase','Stimare l’attività, definirne la priorità e sostituire o spostare lavoro. Non consumare implicitamente il margine.'],
['Ritardo nei dati o nel feedback','Registrare l’attesa. Le milestone dipendenti slittano; non dichiarare la consegna accettata.'],
['Compatibilità Revit/Tekla/IFC','Prototipo sui modelli campione prima delle automazioni. Separare oggetti supportati, conversioni esplicite ed esclusioni.'],
['Norma o metodo non disponibili','Concordare fonte/versione e campo d’uso prima del calcolo; controlli specialistici fuori campo diventano opzioni dedicate.']],[1.2,3])
text('Misura del progresso','SubA')
text('Usare milestone accettate, casi pilota completati e difetti risolti. Evitare percentuali globali ricavate dal solo numero di funzioni: i moduli hanno dimensioni diverse. Dopo M00 confrontare per ciascuna attività le giornate effettive con la stima base e rivedere le fasi rimanenti.')
page();title('5. Possibile implementazione BIM')
text('Proposta: un contratto dati comune di ANTHEA, uno scambio openBIM e due adattatori separati per Revit e Tekla. Il programma non modifica automaticamente il modello costruttivo in base a un esito numerico: l’utente vede la proposta, controlla ciò che cambia e approva la restituzione.')
table(['Passo','Flusso proposto'],[
['1 · Leggi','Revit / Tekla / IFC → selezione elementi e versione → associazione geometrie, materiali, armature e identificativi.'],
['2 · Controlla','Validazione dei requisiti, trasformazione delle unità e assi, anteprima e segnalazione dei dati mancanti.'],
['3 · Calcola','ANTHEA riceve azioni da un’analisi identificata, applica ipotesi e verifica. Salva esiti, combinazioni e versione del motore.'],
['4 · Confronta','Presenta alternative di armatura, differenze geometriche e proprietà modificate con report associato.'],
['5 · Restituisci','Aggiornamento confermato in Revit/Tekla, proprietà di esito, report e osservazioni BCF.'],
['6 · Rivedi','Una modifica rilevante invalida i risultati collegati e apre una nuova revisione, senza perdere la precedente.']],[.8,3.2])
text('IFC descrive lo scambio del modello; IDS esprime requisiti informativi; BCF comunica osservazioni associate agli oggetti. La scelta di versioni, oggetti supportati e proprietà va verificata sui due modelli pilota. Le API Revit e Tekla consentono adattatori specifici; i riferimenti ufficiali sono F07-F12. Le scelte di flusso e sicurezza delle modifiche sono proposte progettuali, non funzionalità già esistenti di ANTHEA.')
for row in d['bim']:
    id,theme,choice,scope,tasks,source=row
    story.append(KeepTogether([p(f'{id} · {theme}: {choice}','SubA'),p(scope),p(f'Attività: {tasks}. Riferimento: {source}.','SmallA')]))
title('6. Opzioni escluse dal piano centrale')
text('Queste estensioni non sono perse, ma richiedono una decisione separata. Le stime sono preliminari, prima del margine, e non sono sommate ai tempi o alle milestone del programma principale.')
for id,name,scope,lo,base,hi,dep in d['options']:
    text(f'{id} · {name}','SubA');text(scope);text(('Stima min/base/max: '+f'{lo}/{base}/{hi} giornate.' if lo is not None else 'Stima da formulare dopo definizione del capitolato.')+' Prerequisito: '+dep+'.')
text('Decisioni necessarie prima di partire','SubA')
text('Confermare l’ordine delle fasi e il primo progetto pilota; individuare i revisori; concordare normativa/versione per ciascun nuovo modulo; verificare le DLL disponibili; concordare le versioni Revit/Tekla, licenze e modelli campione; decidere quali attività P2 mantenere nel primo programma. L’avvio proposto non costituisce approvazione automatica di questi punti.')
page();title('7. Tracciabilità della bozza originale')
text('Tutte le 32 righe valorizzate sono ricondotte allo stato corrente o a un’attività, comprese le voci gerarchiche del foglio Acciaio. Il foglio CAP vuoto costituisce la trentatreesima voce di tracciabilità. Correzioni ortografiche nei titoli del programma non cambiano il significato della bozza.')
table(['Foglio / riga','Voce originale','Stato','Rimando'],[[r[0]+f' / {r[1]}',r[2],r[3],r[4]] for r in d['original']],[1.05,2.45,1.1,1])
page();title('Fonti e metodo della ricognizione')
for id,kind,name,note,ref in d['sources']:
    story.append(KeepTogether([p(f'{id} · {kind} · {name}','SubA'),p(note),p(ref,'SmallA')]))
text('I riferimenti al codice sono evidenze interne della presenza e del campo delle funzioni. Le stime e la sequenza sono proposte di pianificazione. Le fonti BIM sostengono i ruoli degli standard e delle API; non attestano la compatibilità di un futuro connettore ANTHEA, che dovrà essere provata nelle milestone MB1-MB4.','SmallA')
def footer(c,doc):
    c.saveState();c.setStrokeColor(navy);c.setLineWidth(.4);c.line(44,40,A4[0]-44,40);c.setFont('Arial',8);c.setFillColor(grey);c.drawString(44,27,'ANTHEA · Programma di sviluppo · 06/10/2026 · Rev. 01');c.drawRightString(A4[0]-44,27,str(doc.page));c.restoreState()
doc=SimpleDocTemplate(str(dest),pagesize=A4,rightMargin=44,leftMargin=44,topMargin=42,bottomMargin=55,title='ANTHEA - Programma di sviluppo',author='ITEC')
doc.build(story,onFirstPage=footer,onLaterPages=footer)
(art/'piano-calcolato.json').write_text(json.dumps(plans,ensure_ascii=False,indent=2,default=str),encoding='utf-8')
print(dest)
