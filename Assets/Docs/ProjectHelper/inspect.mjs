import {TASK_STATES,taskOutdated,taskSnapshot} from './workflow.mjs';
// Usage: node Assets/Docs/ProjectHelper/inspect.mjs S-0042 [S-0087 ...]
import fs from 'node:fs';
import {recordState,senseIndex,eligibleExceptions} from './engine.mjs';
const design=JSON.parse(fs.readFileSync(new URL('./design.json',import.meta.url),'utf8'));
const ids=process.argv.slice(2),ix=senseIndex(design,true);
const meaning=id=>{const s=ix.get(id),w=design.words.find(w=>w.id===s?.wordId);const source=w?.source?{guid:w.source.guid,path:w.source.path,engineId:w.source.engineId}:null;return {...s,source};};
if(!ids.length){console.log(JSON.stringify((design.tasks||[]).filter(t=>!t.archived).map(t=>({id:t.id,title:t.title,version:t.version,status:TASK_STATES[t.status],source:t.sourceId,outdated:taskOutdated(design,t)})),null,2));}
for(const id of ids){
  const task=(design.tasks||[]).find(t=>t.id===id);if(task){console.log(JSON.stringify({...task,statusLabel:TASK_STATES[task.status],outdated:taskOutdated(design,task),currentDesign:taskSnapshot(design,task.sourceKind,task.sourceId)},null,2));continue;}
  const r=Object.values(design.sentences).find(r=>r.id===id),g=design.grammars.find(g=>g.id===id);
  if(g){console.log(JSON.stringify(g,null,2));continue;}
  if(!r){console.error('Unknown ID: '+id);process.exitCode=1;continue;}
  const note={...r.note};delete note.reviewedFingerprint;
  console.log(JSON.stringify({id:r.id,active:r.active,meanings:r.senseIds.map(meaning),review:recordState(design,r),note,grammars:design.grammars.filter(g=>r.grammarIds.includes(g.id)).map(g=>({...g,exceptions:eligibleExceptions(g,r)})),relations:design.relations.filter(c=>!c.archived&&c.sentenceIds.includes(id))},null,2));
}
