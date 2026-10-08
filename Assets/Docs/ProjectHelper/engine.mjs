import {validateTasks} from './workflow.mjs';
// Shared documentation model. Stable identities never depend on labels or list order.
export const STATES={draft:'待确认',confirmed:'已确认',observed:'旧记录：当前实现'};
export const MODES={inherit:'沿用语法',supplement:'补充细节',replace:'采用特殊行为',forbid:'禁止此组合',unresolved:'待设计'};
export const canonical=x=>Array.isArray(x)?'['+x.map(canonical).join(',')+']':x&&typeof x==='object'?'{'+Object.keys(x).sort().map(k=>JSON.stringify(k)+':'+canonical(x[k])).join(',')+'}':JSON.stringify(x);
export const numberId=(prefix,n)=>prefix+'-'+String(n).padStart(4,'0');
export function senseIndex(d,archived=false){const parts=new Map(d.parts.filter(p=>archived||!p.archived).map(p=>[p.id,p])),result=new Map();for(const w of d.words)for(const s of w.senses)if(archived||(!w.archived&&!s.archived&&parts.has(s.posId)))result.set(s.id,{...s,wordId:w.id,symbol:w.symbol,code:w.code,part:parts.get(s.posId)?.name||'未分配词性'});return result;}
export function affected(d,type,id){if(type==='part')return {senses:d.words.flatMap(w=>w.senses).filter(s=>!s.archived&&s.posId===id).length,grammars:d.grammars.filter(g=>!g.archived&&g.slots.some(s=>s.kind==='pos'&&s.ref===id)).map(g=>g.id)};const ids=type==='word'?d.words.find(w=>w.id===id)?.senses.map(s=>s.id)||[]:[id];return {sentences:Object.values(d.sentences).filter(r=>r.senseIds.some(s=>ids.includes(s))).length,grammars:d.grammars.filter(g=>!g.archived&&g.slots.some(s=>s.kind==='sense'&&ids.includes(s.ref))).map(g=>g.id)};}
export function eligibleExceptions(g,r){return g.exceptions.filter(e=>!e.slotId||!e.senseId||r.senseIds[g.slots.findIndex(s=>s.id===e.slotId)]===e.senseId);}
export function fingerprint(d,r){const ix=senseIndex(d,true);return canonical({senses:r.senseIds.map(id=>{const s=ix.get(id);return s?{id,text:s.text,posId:s.posId,notes:s.notes,archived:s.archived,part:s.part}:id;}),grammars:r.grammarIds.map(id=>d.grammars.find(g=>g.id===id)).filter(Boolean).map(g=>({id:g.id,slots:g.slots,constraints:g.constraints,logic:g.logic,lifecycle:g.lifecycle,notes:g.notes,status:g.status,exceptions:eligibleExceptions(g,r)})),relations:d.relations.filter(c=>!c.archived&&c.sentenceIds.includes(r.id)),active:r.active});}
export function recordState(d,r){if(r.note?.reviewedFingerprint&&r.note.reviewedFingerprint!==fingerprint(d,r))return 'stale';return r.note?.reviewedFingerprint?r.note.status:(r.note?.mode!=='inherit'||r.note?.effect||r.note?.notes?'draft':'inherited');}
export const STATE_LABEL={stale:'需复核',draft:'标注草稿',confirmed:'标注已确认',inherited:'沿用语法',observed:'旧记录：当前实现'};

// Cache depends on matching inputs only. Image and prose edits never re-enumerate.
export class Compiler{
  constructor({limit=100000}={}){this.limit=limit;this.cache=new Map();}
  compile(input,previous=null){
    const d=structuredClone(input),issues=[],ix=senseIndex(d),byPart=new Map(),old=previous?.sentences||{};
    for(const s of ix.values()){if(!byPart.has(s.posId))byPart.set(s.posId,[]);byPart.get(s.posId).push(s.id);}
    for(const [key,r] of Object.entries(old)){if(d.sentences[key]&&d.sentences[key].id!==r.id)throw Error('句子编号不允许改写：'+r.id);if(!d.sentences[key])d.sentences[key]=structuredClone(r);}
    const oldIds=new Map(Object.entries(old).map(([k,r])=>[r.id,k]));
    for(const [key,r] of Object.entries(d.sentences))if(oldIds.has(r.id)&&oldIds.get(r.id)!==key)throw Error('句子编号不能用于另一句义：'+r.id);
    d.nextSentence=Math.max(d.nextSentence||1,previous?.nextSentence||1,...Object.values(d.sentences).map(r=>Number(r.id.slice(2))+1));
    d.nextGrammar=Math.max(d.nextGrammar||1,previous?.nextGrammar||1,...d.grammars.map(g=>Number(g.id.slice(2))+1));
    for(const r of Object.values(d.sentences)){r.active=false;r.grammarIds=[];}
    let recomputed=0,reused=0,attempted=0;
    for(const g of d.grammars.filter(g=>!g.archived&&g.enabled)){
      const choices=g.slots.map(s=>s.kind==='pos'?(byPart.get(s.ref)||[]):ix.has(s.ref)?[s.ref]:[]),signature=canonical({slots:g.slots,constraints:g.constraints,choices});
      let products=this.cache.get(g.id)?.signature===signature?this.cache.get(g.id).products:null;
      if(products)reused++;
      else{const size=choices.reduce((a,c)=>a*c.length,1);if(size>this.limit)throw Error(g.id+' 的候选积为 '+size.toLocaleString()+'，超过 '+this.limit.toLocaleString()+' 上限。请收窄槽位或停用语法；未截断结果。');products=[[]];for(const c of choices)products=products.flatMap(p=>c.map(id=>[...p,id]));products=products.filter(ids=>g.constraints.every(c=>{const a=g.slots.findIndex(s=>s.id===c.left),b=g.slots.findIndex(s=>s.id===c.right);return c.kind==='differentWord'?ix.get(ids[a]).wordId!==ix.get(ids[b]).wordId:ids[a]!==ids[b];}));this.cache.set(g.id,{signature,products});recomputed++;}
      attempted+=products.length;if(attempted>this.limit)throw Error('展开超过总量上限，请缩小启用语法范围。原文件未改变。');
      if(!products.length)issues.push({type:'empty',ref:g.id,message:'没有匹配组合。检查已删除的词性、词义或限制条件。'});
      for(const ids of products){const key=JSON.stringify(ids);let r=d.sentences[key];if(!r)r=d.sentences[key]={id:numberId('S',d.nextSentence++),senseIds:ids,grammarIds:[],note:{mode:'inherit',status:'draft',effect:'',condition:'',lifecycle:'',notes:'',evidence:''},createdAt:new Date().toISOString()};r.active=true;r.grammarIds.push(g.id);r.tokens=ids.map(id=>{const s=ix.get(id);return {senseId:id,wordId:s.wordId,text:s.text,symbol:s.symbol,part:s.part};});}
    }
    for(const r of Object.values(d.sentences)){
      if(r.active&&r.grammarIds.length>1){const gs=r.grammarIds.map(id=>d.grammars.find(g=>g.id===id));if(new Set(gs.map(g=>canonical([g.logic,g.lifecycle]))).size>1)issues.push({type:'overlap',ref:r.id,message:'匹配多条语法且默认逻辑不同，需人工检查。'});}
      if(r.note?.acknowledge){r.note.reviewedFingerprint=fingerprint(d,r);r.note.reviewedAt=new Date().toISOString();delete r.note.acknowledge;}
      if(recordState(d,r)==='stale')issues.push({type:'stale',ref:r.id,message:'相关依据已变化，原笔记已保留，等待复核。'});
    }
    return {design:d,issues,stats:{recomputed,reused,active:Object.values(d.sentences).filter(r=>r.active).length,retired:Object.values(d.sentences).filter(r=>!r.active).length}};
  }
}
export function validate(d,{allowDrafts=false}={}){
  const errors=[];if(d?.schemaVersion!==2)return ['需要第 2 版设计文件。'];
  for(const k of ['parts','words','grammars','relations'])if(!Array.isArray(d[k]))errors.push(k+' 必须是数组。');
  if(!d.sentences||Array.isArray(d.sentences)||typeof d.sentences!=='object')errors.push('缺少稳定句子登记表。');if(errors.length)return errors;
  const unique=(rows,label)=>{const seen=new Set();for(const r of rows){if(!r||typeof r.id!=='string'||!/^[a-zA-Z0-9_-]+$/.test(r.id)||seen.has(r.id)){errors.push(label+' 含缺失或重复标识。');continue;}seen.add(r.id);}return seen;};
  const parts=unique(d.parts,'词性');unique(d.words,'词');unique(d.grammars,'语法');unique(d.relations,'关联');if(errors.length)return errors;
  const senses=unique(d.words.flatMap(w=>Array.isArray(w.senses)?w.senses:[]),'词义');
  for(const p of d.parts)if(typeof p.name!=='string'||!p.name.trim())errors.push('词性需要名称。');
  for(const w of d.words){if(!Array.isArray(w.senses)){errors.push('词义应为数组。');continue;}if(w.symbol&&!/^\/symbols\/[a-f0-9]{64}\.(png|jpg|webp)$/.test(w.symbol))errors.push('符号应使用已上传的图片。');for(const s of w.senses){if(!s)continue;if(typeof s.text!=='string'||!s.text.trim())errors.push('词义不能为空。');if(!parts.has(s.posId))errors.push('词义引用不存在的词性。');}}
  for(const g of d.grammars){
    if((!/^G-\d{4,}$/.test(g.id)&&!(allowDrafts&&/^draft-G-[a-z0-9-]+$/.test(g.id)))||typeof g.title!=='string'||!g.title.trim())errors.push('语法需有有效编号与名称。');
    if(!Array.isArray(g.slots)||g.slots.length<1||g.slots.length>8){errors.push(g.id+' 应有 1–8 个槽位。');continue;}
    const slots=unique(g.slots,'槽位');for(const s of g.slots)if(s&&(!['pos','sense'].includes(s.kind)||!(s.kind==='pos'?parts:senses).has(s.ref)))errors.push(g.id+' 槽位引用不存在。');
    if(!Array.isArray(g.constraints)||!Array.isArray(g.exceptions)){errors.push('语法缺少限制或例外数组。');continue;}
    for(const c of g.constraints)if(!c||!['differentSense','differentWord'].includes(c.kind)||!slots.has(c.left)||!slots.has(c.right)||c.left===c.right)errors.push('语法限制位置无效。');
    for(const x of g.exceptions)if(!x||!MODES[x.mode]||(x.slotId&&!slots.has(x.slotId))||(x.senseId&&!senses.has(x.senseId)))errors.push('语法例外引用无效。');
  }
  const ids=new Set();for(const [key,r] of Object.entries(d.sentences)){if(!r||!/^S-\d{4,}$/.test(r.id)||ids.has(r.id)){errors.push('句子编号缺失或重复。');continue;}ids.add(r.id);if(!Array.isArray(r.senseIds)||key!==JSON.stringify(r.senseIds)||r.senseIds.some(id=>!senses.has(id)))errors.push('句子词义被改写或删除，请使用归档。');if(!r.note||!MODES[r.note.mode])errors.push('句子标注方式无效。');}
  for(const c of d.relations)if(!Array.isArray(c.sentenceIds)||c.sentenceIds.some(id=>!ids.has(id)))errors.push('关联引用不存在的句子编号。');
  for(const [kind,records,fields] of [['语法',d.grammars,['logic','lifecycle','notes','source']],['词义',d.words.flatMap(w=>w.senses||[]),['notes']],['关联',d.relations,['title','condition','order','result','notes']],['标注',Object.values(d.sentences).map(r=>r?.note),['effect','condition','lifecycle','notes','evidence']]])for(const r of records)if(r)for(const field of fields)if(r[field]!==undefined&&typeof r[field]!=='string')errors.push(kind+' '+field+' 必须是文本。');return [...new Set([...errors,...validateTasks(d)])];
}
export function changes(before,after){const result=[];for(const [field,label] of [['parts','词性'],['words','词'],['grammars','语法'],['relations','关联'],['tasks','开发事项']]){const old=new Map((before?.[field]||[]).map(x=>[x.id,x]));for(const r of after[field]||[])if(canonical(old.get(r.id))!==canonical(r))result.push({kind:field,id:r.id,summary:label+' '+(r.title||r.name||r.code||r.id)+(old.has(r.id)?' 已修改':' 已新增')});}for(const [key,r] of Object.entries(after.sentences))if(canonical(before?.sentences[key]?.note)!==canonical(r.note)&&before?.sentences[key])result.push({kind:'sentence',id:r.id,summary:r.id+' 标注已修改'});return result;}
export function syncChanges(d){return d.words.filter(w=>!w.source?.authoredBaseline||canonical({code:w.code,symbol:w.symbol,senses:w.senses,archived:w.archived})!==w.source.authoredBaseline).map(w=>({id:w.id,code:w.code,path:w.source?.path||'',kind:w.source?(w.archived?'请求移除':'设计有变化'):'待建立 SO 映射'}));}
