// A submitted request is the unit of development, independent of design approval.
export const TASK_STATES={queued:'待处理',working:'开发中',review:'待你验收',passed:'已通过',rework:'需要修改',deferred:'暂不处理'};
export const SOURCE_TYPES={word:'词',part:'词性',grammar:'语法',sentence:'句子'};
export function sourceRecord(d,kind,id){return kind==='sentence'?Object.values(d.sentences).find(r=>r.id===id):(d[{word:'words',part:'parts',grammar:'grammars'}[kind]]||[]).find(r=>r.id===id);}
function clean(value){return JSON.parse(JSON.stringify(value,(k,v)=>['reviewedFingerprint','reviewedAt','acknowledge','authoredBaseline','createdAt'].includes(k)?undefined:v));}
export function taskSnapshot(d,kind,id){
  const source=sourceRecord(d,kind,id);if(!source)throw Error('开发事项的来源不存在。');
  if(kind==='sentence')return clean({source,words:d.words.filter(w=>w.senses.some(s=>source.senseIds.includes(s.id))),grammars:d.grammars.filter(g=>source.grammarIds.includes(g.id)),relations:d.relations.filter(r=>!r.archived&&r.sentenceIds.includes(id))});
  if(kind==='grammar'){const senses=source.slots.filter(s=>s.kind==='sense').map(s=>s.ref),parts=source.slots.filter(s=>s.kind==='pos').map(s=>s.ref);return clean({source,words:d.words.filter(w=>w.senses.some(s=>senses.includes(s.id)||parts.includes(s.posId))),parts:d.parts.filter(p=>parts.includes(p.id))});}
  return clean({source});
}
export function taskOutdated(d,t){try{return JSON.stringify(taskSnapshot(d,t.sourceKind,t.sourceId))!==JSON.stringify(t.basis);}catch{return true;}}
export function submitTask(d,{sourceKind,sourceId,title,request,version='当前版本'},now=new Date().toISOString()){
  if(!title?.trim()||!request?.trim())throw Error('请填写事项名称和需要处理的内容。');
  const basis=taskSnapshot(d,sourceKind,sourceId);d.tasks??=[];
  let t=d.tasks.find(t=>!t.archived&&t.sourceKind===sourceKind&&t.sourceId===sourceId&&t.version===version&&!['passed','deferred'].includes(t.status));
  if(!t){const n=Math.max(d.nextTask||1,...d.tasks.map(t=>Number(t.id.slice(2))+1));d.nextTask=n+1;t={id:'D-'+String(n).padStart(4,'0'),sourceKind,sourceId,createdAt:now,events:[],archived:false};d.tasks.push(t);}
  Object.assign(t,{title:title.trim(),request:request.trim(),version:version.trim()||'当前版本',status:'queued',basis,updatedAt:now});
  t.events.push({at:now,status:'queued',note:'提交需求：'+t.request,basis});return t;
}
export function transitionTask(d,t,status,note='',now=new Date().toISOString()){
  if(!TASK_STATES[status])throw Error('开发状态无效。');
  if(['review','passed'].includes(status)&&taskOutdated(d,t))throw Error('来源设计已变化，请先重新提交需求，再继续验收。');
  if(status==='passed'&&t.status!=='review')throw Error('请先提交验收，再标记通过。');
  if(['review','rework'].includes(status)&&!note.trim())throw Error(status==='review'?'请填写完成内容及验证结果。':'请写明需要修改什么。');
  t.status=status;t.updatedAt=now;t.events.push({at:now,status,note:note.trim()});
}
export function validateTasks(d){
  if(d.tasks===undefined)return [];if(!Array.isArray(d.tasks))return ['开发事项必须是数组。'];
  const seen=new Set(),errors=[];for(const t of d.tasks){if(!t||!/^D-\d{4,}$/.test(t.id)||seen.has(t.id)){errors.push('开发事项编号无效或重复。');continue;}seen.add(t.id);
    if(!SOURCE_TYPES[t.sourceKind]||!sourceRecord(d,t.sourceKind,t.sourceId))errors.push(t.id+' 的来源不存在。');
    if(!TASK_STATES[t.status]||!Array.isArray(t.events)||!t.basis||typeof t.basis!=='object')errors.push(t.id+' 的进度记录无效。');
    for(const k of ['title','request','version'])if(typeof t[k]!=='string'||!t[k].trim())errors.push(t.id+' 缺少 '+k);
  }return errors;
}
