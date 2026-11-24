import {canonical,STATES,MODES} from './engine.mjs';
import {TASK_STATES} from './workflow.mjs';
const LABELS={title:'名称',name:'名称',text:'词义',code:'内部名称',symbol:'符号图片',notes:'备注',logic:'规则说明',lifecycle:'撤销后的行为',condition:'适用条件',effect:'具体效果',source:'来源',status:'旧设计分类',mode:'处理方式',evidence:'依据',designPending:'待设计提醒',archived:'已移除',enabled:'启用展开',slots:'句式结构',constraints:'组合条件',exceptions:'共同条件说明',senses:'词义',posId:'词性',kind:'类型',ref:'匹配对象',slotId:'原适用位置',senseId:'原限定词义',engineMeaning:'引擎词义',grammarIds:'匹配语法',active:'在当前展开范围',request:'开发需求',version:'所属版本',events:'处理记录',basis:'提交时的设计',updatedAt:'更新时间',createdAt:'创建时间',at:'时间',note:'说明',result:'预期结果',order:'成立顺序',sentenceIds:'关联句子',sourceKind:'来源类型',sourceId:'来源编号',reviewedFingerprint:'已复核的依据',reviewedAt:'复核时间'};
function leaves(a,b,path=[],result=[]){if(canonical(a)===canonical(b))return result;
  if(a&&b&&typeof a==='object'&&typeof b==='object'&&!Array.isArray(a)&&!Array.isArray(b)){for(const k of new Set([...Object.keys(a),...Object.keys(b)])){if(k==='authoredBaseline')continue;leaves(a[k],b[k],[...path,k],result);}}
  else if(Array.isArray(a)&&Array.isArray(b)&&[...a,...b].every(x=>x&&typeof x==='object'&&x.id)){const am=new Map(a.map(x=>[x.id,x])),bm=new Map(b.map(x=>[x.id,x]));for(const id of new Set([...am.keys(),...bm.keys()]))leaves(am.get(id),bm.get(id),[...path,id],result);}
  else result.push({path,field:path.map(k=>LABELS[k]||k).join(' / '),before:a,after:b});return result;
}
function rows(doc){const out=new Map();if(!doc)return out;
  for(const [key,label] of [['words','词'],['parts','词性'],['grammars','语法'],['relations','句间关系'],['tasks','开发事项']])for(const r of doc[key]||[])out.set(r.id,{id:r.id,kind:key,title:label+' · '+(r.title||r.name||(r.senses?.map(s=>s.text).join(' · '))||r.code||r.id),record:r});
  for(const r of Object.values(doc.sentences||{}))out.set(r.id,{id:r.id,kind:'sentence',title:r.tokens?.map(t=>t.text).join(' · ')||r.id,record:{...r.note,active:r.active,grammarIds:r.grammarIds,senseIds:r.senseIds}});
  out.set('$document',{id:'$document',kind:'document',title:'文档信息',record:Object.fromEntries(Object.entries(doc).filter(([k])=>!['words','parts','grammars','relations','tasks','sentences','nextSentence','nextGrammar','nextTask','schemaVersion'].includes(k)))});return out;
}
export function documentDiff(before,after,ref=''){
  const a=rows(before),b=rows(after),relevant=new Set(ref?[ref]:[]);
  if(ref.startsWith('S-'))for(const doc of [before,after]){const r=Object.values(doc?.sentences||{}).find(r=>r.id===ref);if(!r)continue;for(const id of r.grammarIds||[])relevant.add(id);for(const w of doc.words||[])if(w.senses.some(s=>r.senseIds.includes(s.id))){relevant.add(w.id);for(const s of w.senses)if(r.senseIds.includes(s.id))relevant.add(s.posId);}for(const c of doc.relations||[])if(c.sentenceIds.includes(ref))relevant.add(c.id);}
  const result=[];for(const id of new Set([...a.keys(),...b.keys()])){if(ref&&!relevant.has(id))continue;const old=a.get(id),next=b.get(id),fields=leaves(old?.record,next?.record);if(fields.length)result.push({...next||old,record:undefined,related:!!ref&&id!==ref,fields});}return result;
}
export function historyValue(value,path,kind){if(value===undefined)return '（此版本不存在）';if(value==='')return '（空白）';if(typeof value==='boolean')return value?'是':'否';const key=path.at(-1);if(key==='mode')return MODES[value]||value;if(key==='status')return (kind==='tasks'?TASK_STATES:STATES)[value]||value;if(key==='reviewedFingerprint')return '复核依据已记录（展开原始数据可查看）';return typeof value==='object'?JSON.stringify(value,null,2):String(value);}
