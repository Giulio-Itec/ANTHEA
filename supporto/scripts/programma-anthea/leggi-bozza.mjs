import fs from 'node:fs/promises';
import {FileBlob,SpreadsheetFile} from '@oai/artifact-tool';
const dir='supporto/artefatti/programma-anthea-20261006';
const w=await SpreadsheetFile.importXlsx(await FileBlob.load(`${dir}/bozza-originale.xlsx`));
console.log((await w.inspect({kind:'sheet',include:'id,name',maxChars:1600})).ndjson);
const p=await w.render({sheetName:'Calcestruzzo',range:'A1:D10',scale:1.5,format:'png'});
await fs.writeFile(`${dir}/bozza-calcestruzzo.png`,new Uint8Array(await p.arrayBuffer()));
