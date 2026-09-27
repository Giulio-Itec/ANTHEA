// Loaded from cua_repl. Browser interaction is exclusively through the supplied
// supported tab.playwright API. evaluate only reads rendered DOM data.
import * as fs from 'node:fs/promises';
import { createHash } from 'node:crypto';

export const families = ['rc_slab', 'rc_tbeam', 'psc_i', 'psc_u', 'psc_box', 'psc_box_var', 'steel_i', 'steel_box'];
export const artifactDir = 'C:/Users/g.pacini/Documents/Repository/ANTHEA/supporto/artefatti/bridge_design_site_1000';
const hash = s => createHash('sha256').update(s).digest('hex');

export async function readCase(tab) {
  return await tab.playwright.evaluate(() => {
    const text = id => document.getElementById(id)?.textContent.trim() ?? null;
    const fields = Array.from(document.querySelectorAll('input[id],select[id]')).filter(e => e.id)
      .map(e => ({id:e.id,value:e.value,checked:e.checked,type:e.type}));
    const section = Array.from(document.querySelectorAll('#secParams .field')).map(e => ({
      label:e.querySelector('label')?.textContent.trim(), displayed:e.querySelector('b')?.textContent.trim(),
      value:e.querySelector('input')?.value, valueAttribute:e.querySelector('input')?.getAttribute('value'),
      min:e.querySelector('input')?.min, max:e.querySelector('input')?.max, step:e.querySelector('input')?.step,
      automatic:!!e.querySelector('.auto')
    }));
    const outputs = Object.fromEntries(['vL','vH','vOW','vLN','vW','vN','aN','vD','aD','aF','vPL','aPL',
      'spanTxt','secTxt','subTxt','warn','tCost','tCostS','tCO2','tCO2S','tDep','tDepS','tDur','tDurS'].map(id=>[id,text(id)]));
    const rows=Array.from(document.querySelectorAll('#qty tr')).map(r=>Array.from(r.querySelectorAll('th,td')).map(c=>c.textContent.trim()));
    const labels=Array.from(document.querySelectorAll('#labels text,#secLabels text')).map(e=>e.textContent.trim());
    return {family:document.querySelector('#cards .on')?.getAttribute('data-k'),fields,section,outputs,rows,labels};
  });
}

export async function initialize(tab) {
  await fs.mkdir(artifactDir+'/screenshots',{recursive:true});
  let saved=[];
  try { saved=(await fs.readFile(artifactDir+'/site-cases.jsonl','utf8')).trim().split('\n').filter(Boolean).map(s=>JSON.parse(s)); }
  catch(e) { if(e.code!=='ENOENT') throw e; }
  const counts=Object.fromEntries(families.map(f=>[f,saved.filter(r=>r.family===f).length]));
  return {saved,counts,seen:new Set(saved.map(r=>r.inputHash)),attempts:0};
}

export async function settledCase(tab) {
  const started=Date.now(); let previous='', stableSince=started, raw, reads=0;
  do {
    raw=await readCase(tab); reads++;
    const signature=JSON.stringify(raw);
    if(signature!==previous) { previous=signature; stableSince=Date.now(); }
    if(Date.now()-started>=650 && Date.now()-stableSince>=180)
      return {...raw,settlement:{elapsedMs:Date.now()-started,stableMs:Date.now()-stableSince,reads}};
  } while(Date.now()-started<5000);
  throw new Error('Site did not settle within five seconds');
}

// Each family: 75 native automatic cases, 20 manual depths, 15 manual span
// counts and 15 material changes. The site's own Random bridge creates the
// common site/layout combinations. Duplicate configurations do not count.
export async function collectBatch(tab,state,limit=40) {
  const start=state.saved.length; let attempts=0;
  while(state.saved.length-start<limit && state.saved.length<1000 && attempts<500) {
    attempts++; state.attempts++;
    await tab.playwright.getByRole('button',{name:'Random bridge ⚄',exact:true}).click();
    let raw=await readCase(tab);
    if(!families.includes(raw.family)) throw new Error('Unknown family '+raw.family);
    const ordinal=state.counts[raw.family];
    if(ordinal>=125) continue;
    let variation='automatic';
    if(ordinal>=75 && ordinal<95) {
      variation='manual_depth';
      await tab.playwright.getByRole('button',{name:'3 Section',exact:true}).click();
      // One 0.05 m keyboard step from the current slider, inside its bounds.
      await tab.playwright.locator('#D').press('ArrowRight');
      raw=await readCase(tab);
    } else if(ordinal>=95 && ordinal<110) {
      variation='manual_spans';
      await tab.playwright.getByRole('button',{name:'2 Layout',exact:true}).click();
      const n=Number(raw.fields.find(x=>x.id==='N').value);
      await tab.playwright.locator('#N').press(n>=12?'ArrowLeft':'ArrowRight');
      raw=await readCase(tab);
    } else if(ordinal>=110) {
      variation='material_strength';
      await tab.playwright.getByRole('button',{name:'3 Section',exact:true}).click();
      await tab.playwright.locator('#fcG').selectOption(['30','35','40','45','50','55','60'][(ordinal-110)%7]);
      raw=await readCase(tab);
    }
    raw=await settledCase(tab);
    if(raw.fields.find(x=>x.id==='cur')?.value!=='EUR') throw new Error('Currency changed');
    if(!raw.rows.some(r=>r[0]?.startsWith('Approximate total'))) throw new Error('Missing site total');
    if(!raw.outputs.tDep || !raw.outputs.tCO2 || !raw.outputs.tDur) throw new Error('Missing site results');
    const inputHash=hash(JSON.stringify({family:raw.family,fields:raw.fields,section:raw.section,auto:[raw.outputs.aN,raw.outputs.aD,raw.outputs.aPL]}));
    if(state.seen.has(inputHash)) continue;
    const id=String(state.saved.length+1).padStart(4,'0');
    const record={id,source:'https://thebridgeeng.com/design',capturedAt:new Date().toISOString(),variation,
      familyOrdinal:ordinal+1,inputHash,domHash:hash(JSON.stringify(raw)),...raw};
    await fs.appendFile(artifactDir+'/site-cases.jsonl',JSON.stringify(record)+'\n');
    state.saved.push(record); state.seen.add(inputHash); state.counts[raw.family]++;
    if([0,75,95,110].includes(ordinal)) {
      await fs.writeFile(artifactDir+`/screenshots/${id}-${raw.family}-${variation}.png`,await tab.screenshot({fullPage:false}));
    }
  }
  const progress={captured:state.saved.length,counts:state.counts,totalAttempts:state.attempts,latest:state.saved.at(-1)?.id};
  await fs.writeFile(artifactDir+'/capture-progress.json',JSON.stringify(progress,null,2));
  return progress;
}
