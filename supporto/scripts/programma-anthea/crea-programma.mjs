import fs from 'node:fs/promises';
import {Workbook,SpreadsheetFile} from '@oai/artifact-tool';
const art='supporto/artefatti/programma-anthea-20261006';
const dest='supporto/documentazione/Programma_ANTHEA/ANTHEA_Programma_sviluppo_2026-10-06';
const d=JSON.parse(await fs.readFile(`${art}/contenuti.json`,'utf8'));
const w=Workbook.create();
const names=['Sintesi','Tempi','Attivita','Milestone','Test e feedback','Stato attuale','BIM','Opzioni','Bozza e fonti'];
const sheets=Object.fromEntries(names.map(n=>[n,w.worksheets.add(n)]));
const navy='#203D59',blue='#EEF3F8',amber='#FFF3D2',ink='#243746';
const col=n=>{let s='';for(n++;n;n=Math.floor((n-1)/26))s=String.fromCharCode(65+(n-1)%26)+s;return s};
function init(name,title,widths,last){const s=sheets[name];s.showGridLines=false;s.getRange(`A1:${col(widths.length-1)}${last}`).format.font={name:'Arial',size:11,color:ink};s.getRange(`A1:${col(widths.length-1)}${last}`).format.verticalAlignment='top';widths.forEach((v,i)=>s.getRange(`${col(i)}1:${col(i)}${last}`).format.columnWidth=v);s.getRange('A2').values=[[title]];s.getRange('A2').format.font={size:16,bold:true,color:navy};s.getRange('A2').format.rowHeight=30;s.getRange(`A3:${col(widths.length-1)}3`).format.borders={bottom:{style:'thin',color:navy}};return s;}
function table(s,row,headers,data,widthEnd=null){let end=col(headers.length-1);s.getRange(`A${row}:${end}${row}`).values=[headers];s.getRange(`A${row}:${end}${row}`).format={fill:navy,font:{name:'Arial',size:11,bold:true,color:'#FFFFFF'},wrapText:true,rowHeight:34,verticalAlignment:'center',horizontalAlignment:'center'};if(data.length)s.getRange(`A${row+1}:${end}${row+data.length}`).values=data;s.getRange(`A${row+1}:${end}${row+data.length}`).format.wrapText=true;for(let i=0;i<data.length;i++){const r=s.getRange(`A${row+1+i}:${end}${row+1+i}`);r.format.rowHeight=76;if(i%2===0)r.format.fill=blue;}return row+data.length;}
function formula(s,cell,value){s.getRange(cell).formulas=[[value]];}
function value(s,cell,v){s.getRange(cell).values=[[v]];}
const a=init('Attivita','Attività residue · stime con AI, test e documentazione inclusi',[10,9,18,30,62,16,9,9,9,9,22,58,20,35],d.tasks.length+6);
value(a,'A4','Stime in giornate-persona. Celle ambra modificabili. Le attività presenti sono conteggiate soltanto per le estensioni residue.');
table(a,5,['ID','Fase','Area','Consegna','Perimetro','Origine','Priorità','Min gg','Base gg','Max gg','Dipendenze','Criterio di accettazione','Test','Riferimento'],d.tasks.map(t=>[t.id,t.phase,t.area,t.title,t.scope,t.status,t.priority,t.lo,t.base,t.hi,t.deps,t.accept,t.test,t.source]));
a.freezePanes.freezeRows(5);a.freezePanes.freezeColumns(4);a.getRange(`H6:J${d.tasks.length+5}`).format.fill=amber;a.getRange(`H6:J${d.tasks.length+5}`).setNumberFormat('0');a.dataValidations.add({range:`H6:J${d.tasks.length+5}`,rule:{type:'whole',operator:'between',formula1:0,formula2:500}});
a.getRange(`G6:G${d.tasks.length+5}`).dataValidation={rule:{type:'list',values:['P0','P1','P2']}};
const t=init('Tempi','Due scenari · una persona con AI',[9,35,10,10,10,12,12,14,14,12,14,14,12,12,12,12],48);t.tabColor=navy;
table(t,5,['Parametro','Valore'],[['Avvio ipotetico',new Date('2026-10-12T00:00:00Z')],['Gg/settimana pieno',5],['Gg/settimana parziale',2],['Margine imprevisti',.2]]);
t.getRange('A6:A9').format.columnWidth=24;t.getRange('A6:B9').format.rowHeight=24;t.getRange('B6:B9').format.fill=amber;t.getRange('B6').setNumberFormat('dd/mm/yyyy');t.getRange('B7:B8').setNumberFormat('0.0');t.getRange('B9').setNumberFormat('0%');t.dataValidations.add({range:'B7:B8',rule:{type:'decimal',operator:'between',formula1:.1,formula2:5}});t.dataValidations.add({range:'B9',rule:{type:'decimal',operator:'between',formula1:0,formula2:1}});
value(t,'D6','Date indicative. Fasi sequenziali, nessun parallelismo con una sola persona.');value(t,'D7','Settimane arrotondate per fase. Ferie, festività e attese extra non incluse.');value(t,'D8','Intervalli minimo/massimo = stime, non probabilità. Ricalibrare dopo M00.');
table(t,12,['Fase','Consegna','Min gg','Base gg','Max gg','Base + margine','Sett. pieno','Inizio pieno','Fine pieno','Sett. 2 gg','Inizio 2 gg','Fine 2 gg','Min sett. pieno','Max sett. pieno','Min sett. 2 gg','Max sett. 2 gg'],d.phases.map(p=>[p[0],p[1],...Array(14).fill(null)]));
const lastA=d.tasks.length+5,lastT=d.phases.length+12;
d.phases.forEach((p,i)=>{const r=i+13;for(const [c,ac]of[['C','H'],['D','I'],['E','J']])formula(t,`${c}${r}`,`=SUMIFS('Attivita'!$${ac}$6:$${ac}$${lastA},'Attivita'!$B$6:$B$${lastA},A${r})`);formula(t,`F${r}`,`=D${r}*(1+$B$9)`);formula(t,`G${r}`,`=ROUNDUP(F${r}/$B$7,0)`);formula(t,`H${r}`,i===0?'=$B$6':`=I${r-1}+1`);formula(t,`I${r}`,`=H${r}+7*G${r}-1`);formula(t,`J${r}`,`=ROUNDUP(F${r}/$B$8,0)`);formula(t,`K${r}`,i===0?'=$B$6':`=L${r-1}+1`);formula(t,`L${r}`,`=K${r}+7*J${r}-1`);for(const[c,eff,cap]of[['M','C',7],['N','E',7],['O','C',8],['P','E',8]])formula(t,`${c}${r}`,`=ROUNDUP(${eff}${r}*(1+$B$9)/$B$${cap},0)`);});
for(const c of ['H','I','K','L'])t.getRange(`${c}13:${c}${lastT}`).setNumberFormat('dd/mm/yyyy');t.getRange(`C13:G${lastT}`).setNumberFormat('0.0');t.getRange(`J13:J${lastT}`).setNumberFormat('0');t.getRange(`M13:P${lastT}`).setNumberFormat('0');t.getRange(`A13:P${lastT}`).format.rowHeight=42;
const total=lastT+1;value(t,`B${total}`,'TOTALE PROGRAMMA');for(const c of ['C','D','E','F','G','J','M','N','O','P'])formula(t,`${c}${total}`,`=SUM(${c}13:${c}${lastT})`);formula(t,`I${total}`,`=I${lastT}`);formula(t,`L${total}`,`=L${lastT}`);t.getRange(`I${total}`).setNumberFormat('dd/mm/yyyy');t.getRange(`L${total}`).setNumberFormat('dd/mm/yyyy');t.getRange(`A${total}:P${total}`).format.font.bold=true;
t.freezePanes.freezeRows(12);t.freezePanes.freezeColumns(2);
const summary=init('Sintesi','ANTHEA · programma di sviluppo',[31,20,20,21,21,38],41);summary.tabColor=navy;
value(summary,'A4','Ricognizione al 06/10/2026 · proposta di sequenza e tempi residui');
table(summary,6,['Perimetro','Pieno: settimane','2 gg: settimane','Fine pieno','Fine 2 gg','Consegna'],[
['Primo rilascio esteso',null,null,null,null,'M03 · nucleo, CLS e geotecnica'],['Rilascio funzionale integrato',null,null,null,null,'M08 · azioni, acciaio, misto e CAP'],['Programma con entrambi i BIM',null,null,null,null,'MB4 · openBIM, Revit e Tekla']]);
for(const[r,end]of[[7,16],[8,21],[9,25]]){formula(summary,`B${r}`,`=SUM('Tempi'!G13:G${end})`);formula(summary,`C${r}`,`=SUM('Tempi'!J13:J${end})`);formula(summary,`D${r}`,`='Tempi'!I${end}`);formula(summary,`E${r}`,`='Tempi'!L${end}`);}summary.getRange('D7:E9').setNumberFormat('dd/mm/yyyy');summary.getRange('A7:F9').format.rowHeight=50;
table(summary,12,['Stima complessiva','Min','Base','Max'],[['Giornate prima del margine',null,null,null],['Settimane a tempo pieno',null,null,null],['Settimane a 2 gg/settimana',null,null,null]]);
for(const [r,cols]of[[13,['C','D','E']],[14,['M','G','N']],[15,['O','J','P']]])cols.forEach((c,i)=>formula(summary,`${col(i+1)}${r}`,`='Tempi'!${c}${total}`));summary.getRange('A13:D15').format.rowHeight=30;
table(summary,18,['Decisione','Indicazione'],[['Primo obiettivo','Consolidare e chiudere M00 prima di aggiungere ampiezza. Alla prima demo correggere le stime con i tempi effettivi.'],['Priorità','M03 produce un primo pacchetto usabile. M08 chiude il programma funzionale. Il BIM viene sviluppato dopo, con entrambi i connettori.'],['Già disponibile',`${d.inventory.length} voci ricognite nel foglio Stato attuale. Presente non significa copertura universale né nuova validazione.`],['Non incluso','Fessurazione non lineare, pali p-y, solutore FEM generale, CDE e altri specialistici restano nel foglio Opzioni.'],['Ritmo di feedback','Demo ogni 10 giornate di lavoro; verifica tecnica a ogni milestone. Registro modificabile in Test e feedback.'],['Interpretazione dei tempi','Stime assistite da AI, non velocità storica misurata. Nessun coefficiente automatico di accelerazione; ricalibrare sulla baseline.']]);
summary.getRange('B19:B24').format.columnWidth=20; // Summary narrative spans a separate right-hand notes area below.
// Wider narrative block without merging independent data columns.
summary.getRange('B19:B24').clear({applyTo:'contents'});
const notes=[['M00 prima di ampliare il perimetro: usare il primo ciclo per calibrare le stime.'],['M03 = primo rilascio esteso; M08 = funzionale integrato; MB4 = BIM Revit + Tekla.'],[`${d.inventory.length} voci ricognite; le funzioni presenti non vengono stimate da ricostruire.`],['Opzioni escluse: non lineare CA, pali p-y, FEM generale e BIM/CDE avanzato.'],['Demo ogni 10 giornate di lavoro e accettazione tecnica a ogni milestone.'],['L’AI è già inclusa nelle stime; il programma non assume un fattore di velocità misurato.']];
summary.getRange('B19:B24').values=notes;summary.getRange('B19:F24').format.wrapText=false;summary.getRange('A19:F24').format.rowHeight=28;summary.getRange('B19:F24').format.fill='#FFFFFF';summary.getRange('A19:A24').format.wrapText=true;
value(summary,'A27','Ipotesi di piano');summary.getRange('A27').format.font.bold=true;
d.assumptions.forEach((x,i)=>{value(summary,`A${29+i}`,`${i+1}.`);value(summary,`B${29+i}`,x);summary.getRange(`B${29+i}:F${29+i}`).merge();summary.getRange(`B${29+i}:F${29+i}`).format.wrapText=true;summary.getRange(`A${29+i}:F${29+i}`).format.rowHeight=52;});
const m=init('Milestone','Milestone e decisioni di passaggio',[10,31,54,18,18,38,20,38],19);table(m,5,['ID','Consegna','Criterio per chiudere','Fine pieno','Fine 2 gg','Test/feedback','Stato','Decisione del referente'],d.phases.map((p,i)=>[p[2],p[1],p[3],null,null,'Demo + test di fase + registro difetti','Da avviare','']));
d.phases.forEach((p,i)=>{formula(m,`D${i+6}`,`='Tempi'!I${i+13}`);formula(m,`E${i+6}`,`='Tempi'!L${i+13}`)});m.getRange('D6:E18').setNumberFormat('dd/mm/yyyy');m.getRange('G6:H18').format.fill=amber;m.getRange('G6:G18').dataValidation={rule:{type:'list',values:['Da avviare','In corso','In revisione','Accettata','Riaperta']}};m.freezePanes.freezeRows(5);
const test=init('Test e feedback','Piano dei test e registro del feedback',[12,28,34,28,65,66,50,29],44);table(test,5,['ID','Quando','Chi','Tipo','Prova da eseguire','Accettazione','Evidenza','Fasi'],d.tests);test.freezePanes.freezeRows(5);test.getRange('A6:H21').format.rowHeight=104;
value(test,'A24','Regola di chiusura: test completati, difetti prioritari risolti e decisione esplicita del referente tecnico.');
table(test,27,['ID feedback','Data','Attività / milestone','Osservazione','Priorità / impatto','Decisione / responsabile','Stato','Criterio / data chiusura'],Array.from({length:10},(_,i)=>[`FB${String(i+1).padStart(2,'0')}`,'','','','','','Da esaminare','']));test.getRange('B28:H37').format.fill=amber;test.getRange('B28:B37').setNumberFormat('dd/mm/yyyy');test.getRange('G28:G37').dataValidation={rule:{type:'list',values:['Da esaminare','Accettato nel piano','Da correggere','Rinviato','Chiuso']}};
const st=init('Stato attuale','Quanto è già disponibile al 06/10/2026',[10,20,33,23,70,65,55,20],30);table(st,5,['ID','Area','Funzione','Stato','Disponibile','Limite / seguito','Evidenza','Rimando'],d.inventory);st.freezePanes.freezeRows(5);st.freezePanes.freezeColumns(3);
const b=init('BIM','Proposta BIM · openBIM, Revit e Tekla',[12,22,38,102,22,35],23);table(b,5,['ID','Tema','Scelta proposta','Implementazione / limite','Attività','Fonte'],d.bim);b.freezePanes.freezeRows(5);b.getRange('A6:F19').format.rowHeight=88;
const o=init('Opzioni','Ampliamenti esclusi dal calendario principale',[10,40,95,12,12,12,18],14);table(o,5,['ID','Opzione','Perimetro da decidere','Min gg','Base gg','Max gg','Prerequisito'],d.options.map(r=>r.map(v=>v===null?'Da stimare':v)));o.freezePanes.freezeRows(5);o.getRange('A6:G10').format.rowHeight=90;value(o,'A12','Stime preliminari con AI, prima del margine. Non sommate al programma. O05 richiede un capitolato separato.');
const src=init('Bozza e fonti','Tracciabilità della bozza e fonti',[24,10,78,25,35],58);table(src,5,['Foglio originale','Riga','Voce originale','Stato ricognito','Copertura / attività'],d.original);src.freezePanes.freezeRows(5);src.getRange(`A6:E${5+d.original.length}`).format.rowHeight=46;const sr=d.original.length+8;table(src,sr,['ID','Tipo','Documento / contenuto','Nota','Riferimento'],d.sources.map(r=>[r[0],r[1],r[2],r[3],r[4]]));src.getRange(`A${sr+1}:E${sr+d.sources.length}`).format.rowHeight=112;
for(const sheet of [a,m,st]){const end=sheet===a?lastA:sheet===m?18:28;const c=sheet===a?'F':sheet===m?'G':'D';sheet.getRange(`${c}6:${c}${end}`).conditionalFormats.add('containsText',{text:'Parziale',format:{fill:'#FFF3D2'}});sheet.getRange(`${c}6:${c}${end}`).conditionalFormats.add('containsText',{text:'Non riscontrato',format:{fill:'#FCE4DF'}});}
w.recalculate();
for(const [s,range,name] of [[a,`A5:N${lastA}`,'AttivitaProgramma'],[st,'A5:H28','Ricognizione'],[test,'A5:H21','PianoTest'],[src,`A5:E${5+d.original.length}`,'TracciabilitaBozza']]){const tbl=s.tables.add(range,true,name);tbl.style='TableStyleMedium2';tbl.showFilterButton=true;}
a.getRange(`A6:N${lastA}`).format.rowHeight=62;
st.getRange('A6:H28').format.rowHeight=62;
test.getRange('A6:H21').format.rowHeight=66;
b.getRange('A6:F19').format.rowHeight=66;
o.getRange('A6:G10').format.rowHeight=70;
m.getRange('A6:H18').format.rowHeight=56;
const basePart=Number(summary.getRange('C9').values[0][0]);
value(t,'B8',1);w.recalculate();
if(Number(summary.getRange('C9').values[0][0])<=basePart)throw new Error('Capacity does not update scenario');
value(t,'B8',2);
const finalEffort=Number(a.getRange(`I${lastA}`).values[0][0]);
const totalEffort=Number(summary.getRange('C13').values[0][0]);
value(a,`I${lastA}`,finalEffort+2);w.recalculate();
if(Number(summary.getRange('C13').values[0][0])!==totalEffort+2)throw new Error('Later activity does not update total');
value(a,`I${lastA}`,finalEffort);w.recalculate();
console.log((await w.inspect({kind:'region',sheetId:'Sintesi',range:'A6:F15',maxChars:3000,tableMaxRows:10,tableMaxCols:6})).ndjson);
console.log((await w.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#NUM!|#SPILL!',options:{useRegex:true,maxResults:30},maxChars:1500})).ndjson);
const snapshot={};for(const name of names){const s=sheets[name];snapshot[name]=s.getUsedRange().values;}
await fs.writeFile(`${art}/valori-calcolati.json`,JSON.stringify(snapshot,null,2));
const output=await SpreadsheetFile.exportXlsx(w);await output.save(`${dest}.xlsx`);
for(const [name,range]of [['Sintesi','A1:F16'],['Tempi','A12:J18'],['Attivita','A1:J8'],['Milestone','A1:H8'],['Test e feedback','A1:H8'],['Stato attuale','A1:H8'],['BIM','A1:F8'],['Opzioni','A1:G9'],['Bozza e fonti','A1:E10'],['Sintesi','A18:F36'],['Bozza e fonti','A49:E54']]){try{const p=await w.render({sheetName:name,range,scale:1,format:'png'});await fs.writeFile(`${art}/excel-${name.replaceAll(' ','-')}-${range.replace(':','_')}.png`,new Uint8Array(await p.arrayBuffer()));}catch(e){console.log('RENDER',name,e.message)}}
console.log(`SALVATO ${dest}.xlsx`);
