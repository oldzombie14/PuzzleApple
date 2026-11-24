import {canonical,clone,labels,sourceBasis,statuses,tempId} from './model.mjs';
import {ruleSources} from './principles.mjs';
import {classIds} from './design.mjs';

export const batchStates={draft:'草稿',...statuses};
export const designKeys=Object.keys(labels).filter(k=>!['requests','batches'].includes(k));
export const refKey=ref=>ref.collection+'/'+ref.id;
export const displayName=(k,r)=>['chapters','spaces','boundaries','entities','properties','outputs'].includes(k)?r.id+' · '+r.name:k==='words'?r.id:k==='sentences'?r.wordIds.join(' · '):r.name||r.id;
export const makeItem=()=>({id:crypto.randomUUID(),text:'',expected:'',refs:[],basis:[],implementation:'',validation:'',feedback:'',verdict:'todo'});
export const makeBatch=()=>({id:tempId(),name:'',status:'draft',archived:false,items:[makeItem()]});
export function baseline(initial,doc){
  const rows={};
  for(const k of designKeys)for(const row of initial[k])rows[refKey({collection:k,id:row.id})]=row;
  for(const entry of doc.timeline)if(entry.kind==='submit')for(const item of entry.items)for(const link of item.basis??[])rows[refKey(link.ref)]=link.after.record;
  return rows;
}
export function changedFields(before,after){
  return Object.keys({...before,...after}).filter(k=>canonical(before?.[k])!==canonical(after?.[k]));
}
function capture(d,item,base){
  return item.refs.map(ref=>({ref:clone(ref),before:clone(base[refKey(ref)]??null),after:sourceBasis(d,ref)}));
}
export function outdated(d,item){
  return (item.basis??[]).some(link=>canonical(link.after)!==canonical(sourceBasis(d,link.ref)));
}
const trim=value=>typeof value==='string'?value.trim():'';
export function publish(d,command,base={}) {
  const b=d.batches.find(x=>x.id===command.batchId);
  const {action}=command;
  if(action!=='session'&&!b)throw Error('找不到批次。');
  let summary=trim(command.summary),detail=trim(command.detail);
  const at=new Date().toISOString();
  const entry={id:crypto.randomUUID(),at,kind:action,batchId:b?.id??null,batchName:b?.name??'',author:trim(command.author)||'工作台用户'};
  if(action==='session'){
    if(!summary||!detail)throw Error('请填写会话标题和本批改动。');
    if(command.batchId&&!b)throw Error('关联批次不存在。');
    const key=trim(command.sessionKey);
    if(key&&d.timeline.some(e=>e.kind==='session'&&e.sessionKey===key))throw Error('这个会话已经归档。');
    entry.sessionKey=key;
  }else if(action==='submit'){
    if(b.status!=='draft'||!b.name.trim()||!b.items.length||b.items.some(i=>!i.text.trim()))throw Error('请填写批次名称及每项需求。');
    const previous=d.timeline.findLast(e=>e.batchId===b.id&&e.kind==='submit');
    for(const i of b.items){
      const was=previous?.items.find(x=>x.id===i.id);
      const unchanged=i.verdict==='pass'&&was&&was.text===i.text&&was.expected===i.expected&&canonical(was.refs)===canonical(i.refs)&&!outdated(d,i);
      i.basis=capture(d,i,base);
      if(!unchanged)i.verdict='todo';
    }
    b.submissionId=entry.id;b.submittedRules=clone(d.rules);
    b.status='queued';summary='提交需求 · '+b.name;
  }else if(action==='progress'){
    if(!['queued','working','rework'].includes(b.status)||!summary)throw Error('请为开发中的批次填写阶段摘要。');
    b.status='working';
  }else if(action==='review'){
    if(!['queued','working','rework'].includes(b.status))throw Error('当前批次无法送验。');
    for(const i of b.items.filter(i=>i.verdict!=='pass')){
      if(outdated(d,i))throw Error('关联设计已变化，请先修订提交依据。');
      if(!i.implementation.trim()||!i.validation.trim())throw Error('送验前请为每项需求填写实际改动与验证结果。');
      i.verdict='todo';
    }
    b.status='review';summary='提交验收 · '+b.name;
  }else if(action==='decide'){
    if(b.status!=='review')throw Error('请在待验收批次中记录结论。');
    const decisions=command.decisions??{};
    const pending=b.items.filter(i=>i.verdict==='todo'&&decisions[i.id]);
    if(!pending.length)throw Error('请选择本次已核对的需求。');
    for(const i of pending){
      const result=decisions[i.id];
      if(!['pass','rework'].includes(result))throw Error('验收结论无效。');
      if(result==='pass'&&outdated(d,i))throw Error('关联设计已变化，请先修订提交依据。');
      if(result==='rework'&&!i.feedback.trim())throw Error('请说明需要返工的内容。');
      i.verdict=result;
    }
    if(b.items.every(i=>i.verdict==='pass'))b.status='passed';
    else if(b.items.some(i=>i.verdict==='rework'))b.status='rework';
    summary=(b.status==='passed'?'验收通过':b.status==='rework'?'验收需修改':'阶段验收')+' · '+b.name;
    detail=pending.map(i=>i.text+' — '+(i.verdict==='pass'?'通过':'需修改')+(i.feedback?'，'+i.feedback:'')).join('\n');
  }else if(action==='revise'){
    if(['draft','passed'].includes(b.status))throw Error('当前批次无法修订。');
    b.status='draft';summary='重新修订需求 · '+b.name;
  }else if(action==='defer'){
    if(!['queued','working','rework','review'].includes(b.status))throw Error('当前批次无法暂缓。');
    b.status='deferred';summary='暂缓 · '+b.name;
  }else throw Error('未知的归档操作。');
  entry.summary=summary;entry.detail=detail;
  if(b){entry.status=b.status;if(action!=='session'){
      entry.submissionId=b.submissionId;
      if(action==='submit'){entry.rules=clone(b.submittedRules);entry.items=clone(b.items);}
      else entry.items=b.items.map(({basis,...item})=>clone(item));
    }}
  d.timeline.push(entry);
  return entry;
}
const fieldNames={chapterId:'所属大关',spaceOrder:'空间排列',from:'起端空间',to:'另一端空间',fromEnd:'起端位置',toEnd:'另一端位置',progressLock:'进度锁',kind:'类型',name:'名称',meaning:'含义备注',description:'定义',behavior:'共同表现',classIds:'词类',classes:'词类映射',slots:'语法结构',conditions:'条件',lifecycle:'暂停与结束',camera:'镜头',notes:'备注',intent:'执行意图',archived:'归档状态',symbol:'字形',properties:'性质',outputs:'输出端',spaces:'空间',chapter:'所属关卡',expected:'验收条件',words:'符号',wordIds:'词序',designSource:'初稿依据'};
export const fieldName=k=>fieldNames[k]??k;
export function showValue(value){
  if(value==null||value==='')return '（空）';
  if(typeof value==='string')return value.startsWith('data:image/')?'（自定义字形）':value;
  return typeof value==='object'?JSON.stringify(value,(k,v)=>typeof v==='string'&&v.startsWith('data:image/')?'（自定义字形）':v):String(value);
}
function recordLines(k,row,d){
  const lines=[];
  const className=id=>d.wordClasses.find(c=>c.id===id)?.name??id;
  if(k==='words')lines.push('词类：'+classIds(d,row).map(className).join('、'));
  if(k==='grammars')lines.push('结构：'+row.slots.map(slot=>{
    const classes=classIds(d,slot).map(className).join(' / ');
    return '['+(slot.words.length===1?slot.words[0]:(classes||'任意符号')+(slot.words.length?'，限定 '+slot.words.join('、'):''))+']';
  }).join(' '));
  for(const [key,value] of Object.entries(row)){
    if(['id','source','origin','archived','symbol','name','classes','classIds','active','slots'].includes(key)||value==null||value===''||Array.isArray(value)&&!value.length)continue;
    lines.push(fieldName(key)+'：'+(Array.isArray(value)?value.join('、'):showValue(value)));
  }
  return lines;
}
export function brief(d,b=null){
  const sections=['# '+(b?(b.name||'未命名批次')+' · '+b.id:'V5 设计文档'),''];
  for(const r of d.rules)sections.push('## '+r.name,r.description,'');
  sections.push('设计原则的文档依据',...ruleSources.map(s=>'- '+s.title+' · Assets/Docs/'+s.file+' / '+s.heading),'');
  if(b){
    if(b.submittedRules&&canonical(b.submittedRules)!==canonical(d.rules))sections.push('## 提交时的全局规则（之后有更新）',...b.submittedRules.map(r=>r.name+'\n'+r.description),'');
    sections.push('## 本批需求',batchStates[b.status],'');
    b.items.forEach((i,n)=>{
      sections.push('### '+(n+1)+'. '+i.text);
      if(i.expected)sections.push('验收条件：'+i.expected);
      for(const ref of i.refs){
        const saved=i.basis.find(x=>refKey(x.ref)===refKey(ref));
        const basis=saved?.after??sourceBasis(d,ref),row=basis.record;
        sections.push('- '+labels[ref.collection]+' '+row.id+' '+displayName(ref.collection,row));
        if(saved)for(const k of changedFields(saved.before,row))sections.push('  - '+fieldName(k)+'：'+showValue(saved.before?.[k])+' → '+showValue(row[k]));
        sections.push(...recordLines(ref.collection,row,d).map(line=>'  '+line));
        for(const [key,values] of Object.entries(basis)){
          if(key==='record'||!Array.isArray(values)||!values.length)continue;
          const names=values.map(x=>x.record??x).map(x=>x.id+(x.name&&key!=='words'?' '+x.name:''));
          sections.push('  '+(labels[key]??({objects:'关卡对象',wordClasses:'词类'})[key]??key)+'：'+names.join('、'));
        }
        if(outdated(d,i))sections.push('  当前设计已更新，请核对提交依据。');
      }
      if(i.implementation)sections.push('实际改动：'+i.implementation);
      if(i.validation)sections.push('开发验证：'+i.validation);
      if(i.feedback)sections.push('验收反馈：'+i.feedback);
      sections.push('');
    });
  }else{
    for(const k of designKeys.filter(k=>k!=='rules')){
      sections.push('## '+labels[k]);
      for(const row of d[k].filter(r=>!r.archived&&(k!=='sentences'||r.active))){
        sections.push('### '+row.id+' '+(row.name&&k!=='words'?row.name:''));
        sections.push(...recordLines(k,row,d));
        sections.push('');
      }
    }
  }
  return sections.join('\n');
}
