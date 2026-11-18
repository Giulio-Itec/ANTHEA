import fs from 'node:fs/promises';
import { FileBlob, SpreadsheetFile } from '@oai/artifact-tool';
const source = process.argv[2];
const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(source));
const summary = await workbook.inspect({ kind: 'workbook,sheet,table', maxChars: 6000, tableMaxRows: 3, tableMaxCols: 6 });
console.log(summary.ndjson);
await fs.writeFile(source.replace(/\.xlsx$/i, '.inspect.jsonl'), summary.ndjson);
