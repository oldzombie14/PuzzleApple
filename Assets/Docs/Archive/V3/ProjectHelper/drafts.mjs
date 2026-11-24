// Draft keys only connect editor controls. Permanent identities are assigned on save.
export const isDraft=id=>typeof id==='string'&&id.startsWith('draft-');
const collection={word:'words',part:'parts',grammar:'grammars',relation:'relations'};
export function persisted(d,kind,id){return kind==='sense'?d.words.some(w=>w.senses.some(s=>s.id===id)):(d[collection[kind]]||[]).some(r=>r.id===id);}
export function removeAuthored(d,saved,kind,id){
  const records=kind==='sense'?d.words.find(w=>w.senses.some(s=>s.id===id))?.senses:d[collection[kind]],r=records?.find(r=>r.id===id);if(!r)return;
  if(persisted(saved,kind,id)){r.archived=true;return;}
  const senseIds=kind==='word'?r.senses.map(s=>s.id):kind==='sense'?[id]:[];
  if(senseIds.length&&d.grammars.some(g=>g.slots.some(s=>s.kind==='sense'&&senseIds.includes(s.ref))||g.exceptions.some(x=>senseIds.includes(x.senseId))))throw Error('这个草稿词义仍被语法引用，请先移除相关槽位或条件说明。');
  if(kind==='part'&&(d.words.some(w=>w.senses.some(s=>s.posId===id))||d.grammars.some(g=>g.slots.some(s=>s.kind==='pos'&&s.ref===id))))throw Error('这个草稿词性仍被引用，请先调整相关词义或槽位。');
  if((d.tasks||[]).some(t=>t.sourceId===id))throw Error('已有开发事项引用该内容，请先完成提交或恢复此前草稿。');
  records.splice(records.indexOf(r),1);
}
export function finalizeDrafts(input,saved,uuid=()=>crypto.randomUUID()){
  let d=structuredClone(input);const ids={};
  for(const [kind,key] of Object.entries(collection))d[key]=d[key].filter(r=>!r.archived||persisted(saved,kind,r.id));
  for(const w of d.words)w.senses=w.senses.filter(s=>!s.archived||persisted(saved,'sense',s.id));
  let nextG=Math.max(saved.nextGrammar||1,...saved.grammars.map(g=>Number(g.id.slice(2))+1));
  let nextR=Math.max(1,...saved.relations.map(r=>Number(r.id.slice(2))+1));
  for(const g of d.grammars)if(!isDraft(g.id))nextG=Math.max(nextG,Number(g.id.slice(2))+1);
  for(const r of d.relations)if(!isDraft(r.id))nextR=Math.max(nextR,Number(r.id.slice(2))+1);
  const allocate=(r,p)=>{if(isDraft(r.id))ids[r.id]=p+'-'+uuid();};
  for(const p of d.parts)allocate(p,'p');for(const w of d.words){allocate(w,'w');for(const s of w.senses)allocate(s,'s');}
  for(const g of d.grammars)if(isDraft(g.id))ids[g.id]='G-'+String(nextG++).padStart(4,'0');
  for(const r of d.relations)if(isDraft(r.id))ids[r.id]='R-'+String(nextR++).padStart(4,'0');
  const refs=new Set(['id','ref','wordId','senseId','posId','slotId','sourceId','senseIds','grammarIds','sentenceIds']);
  function remap(value,key=''){if(typeof value==='string')return refs.has(key)?ids[value]||value:value;if(Array.isArray(value))return value.map(v=>remap(v,key));if(value&&typeof value==='object')return Object.fromEntries(Object.entries(value).map(([k,v])=>[k,remap(v,k)]));return value;}
  d=remap(d);d.nextGrammar=nextG;return {design:d,idMap:ids};
}
