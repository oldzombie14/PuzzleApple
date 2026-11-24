import {chapterSpaces,spaceEdges,validateLevels} from './levels.mjs';
import {upgradeDesign,classIds,classUsers,matchesSlot} from './design.mjs';
export const collections = {words:'W',grammars:'G',exceptions:'X',properties:'P',outputs:'O',spaces:'L',entities:'E',sentences:'S',cases:'C',relations:'R',requests:'D',wordClasses:'T',rules:'A',batches:'B',chapters:'CH',boundaries:'J'};
export const labels = {words:'符号',grammars:'语法',exceptions:'例外',properties:'性质',outputs:'输出端',spaces:'关卡空间',entities:'对象',sentences:'句子',cases:'情境',relations:'句间关系',requests:'原开发事项',wordClasses:'词类',rules:'全局规则',batches:'开发批次',chapters:'大关',boundaries:'空间边界'};
export const statuses = {queued:'待处理',working:'开发中',review:'待你验收',passed:'已通过',rework:'需要修改',deferred:'暂不处理'};
export const actions = {On:'开启',Off:'关闭',Move:'移动',Illuminate:'灯化',Freeze:'冻结运动',Legacy:'暂缓迁移'};
export const kinds = {Command:'一次性命令',Sustained:'持续效果',WorldFlow:'世界流程'};
export const scopes = {ExistingController:'现有控制器',CurrentRegion:'当前范围',CommandRegion:'命令开始时的范围',Global:'全局'};
export const clone = x => JSON.parse(JSON.stringify(x));
export const active = list => list.filter(x=>!x.archived);
export const tempId = ()=>'draft-'+globalThis.crypto.randomUUID();
export function canonical(x) { return JSON.stringify(sort(x)); }
function sort(x) { return Array.isArray(x)?x.map(sort):x&&typeof x==='object'?Object.fromEntries(Object.keys(x).sort().map(k=>[k,sort(x[k])])):x; }
const title = {positive:'开启',negative:'关闭',world:'世界',light:'光',door:'门',move:'移动',floor:'地面',mirror:'镜像'};
export function seed(snapshot) {
  const d={format:'puzzleapple-v5-workbench',schemaVersion:1,project:'PuzzleApple · V5',revision:0,counters:{},createdAt:new Date().toISOString(),review:{number:1,items:{}},rounds:[]};
  for(const k of Object.keys(collections))d[k]=[];
  function add(k,row){const n=(d.counters[k]??0)+1;d.counters[k]=n;const r={id:collections[k]+'-'+String(n).padStart(4,'0'),archived:false,...row};d[k].push(r);return r;}
  const words=new Map();
  snapshot.words.forEach(w=>words.set(w.key,add('words',{key:w.key,name:title[w.key]??w.name,classes:w.classes,meaning:'',symbol:'',source:w.asset,origin:'implementation'}).id));
  snapshot.grammars.forEach(g=>add('grammars',{key:g.key,name:({'state.on':'开启对象','state.off':'关闭对象','move.free':'对象自行移动','move.directed':'对象朝目标移动',illuminate:'让对象灯化'})[g.key]??g.key,slots:g.slots.map(s=>({...s,words:s.words.map(w=>words.get(w))})),intent:clone(g.intent),behavior:'',conditions:'',lifecycle:'',camera:'',origin:'implementation'}));
  const exceptionNames={'world.on':'开启世界流程','world.off':'关闭世界流程','move.on':'开启移动（现有空效果）','move.freeze':'冻结全局运动','illuminate.world':'全局灯化','unsupported.door.to.door':'门朝门移动：不成立','unsupported.floor.to.floor':'地面朝地面移动：不成立'};
  snapshot.exceptions.forEach(e=>add('exceptions',{...clone(e),name:exceptionNames[e.key]??e.key,words:e.words.map(w=>words.get(w)),notes:'',origin:'implementation'}));
  for(const k of ['properties','outputs','spaces'])snapshot[k].forEach(x=>add(k,{key:x.key,name:x.name,description:'',source:x,origin:'implementation'}));
  const propNames={PotentialLight:'可发光',Light:'正在发光',Door:'门'};
  const outputNames={Emission:'发光',Hinge:'铰链旋转',Lift:'升降',Position:'位置'};
  const spaceNames={Chamber:'盒内',FirstPath:'第一段通道',SphereRoom:'教学球房',ReturnPath:'返回通道',FarRoom:'远端灯房',Ascent:'上升通道',Outdoor:'室外',Gallery:'画廊',Garden:'花房',Vitruvian:'维特鲁威人房'};
  for(const [k,names]of [['properties',propNames],['outputs',outputNames],['spaces',spaceNames]])for(const r of d[k])r.name=names[r.name]??r.name;
  const ids=(k,names)=>d[k].filter(x=>names.includes(x.source.name)).map(x=>x.id);
  for(const [name,spaces,properties,outputs,description]of [
    ['盒内灯',['Chamber'],['PotentialLight','Light'],['Emission'],'发光资格随当前亮度变化。'],
    ['通道星群',['FirstPath','ReturnPath','Ascent'],['PotentialLight','Light'],['Emission','Position'],'各通道单独登记；星群共用渐变控制。'],
    ['教学房小球',['SphereRoom'],['PotentialLight','Light'],['Emission','Position'],'物理主体独立登记，共享房间发光输出。'],
    ['远端房间灯体',['FarRoom'],['PotentialLight','Light'],['Emission','Position'],'灯体独立登记，共享房间渐变。'],
    ['白色铰链门',['FirstPath','SphereRoom'],['Door'],['Hinge'],'门的进度锁与动作资格仍由现有控制器判断。'],
    ['返回通道入口门',['ReturnPath'],['Door'],['Hinge'],'具体控制范围由 ControlSpaces 提供。'],
    ['充能升降门',['FarRoom','Ascent'],['Door'],['Lift'],'开关和运动资格受充能状态控制。'],
    ['室外灯场',['Outdoor'],['PotentialLight','Light'],['Emission','Position'],'物理灯体独立登记，共享发光输出。'],
    ['室外门',['Outdoor'],['Door'],['Emission'],'门的出现与消失使用发光输出；运动另行登记。']
  ])add('entities',{name,spaces:ids('spaces',spaces),properties:ids('properties',properties),outputs:ids('outputs',outputs),description,conditions:'',source:{path:'Assets/Scripts/V5/Effects/SentenceSceneBindings.cs'},origin:'code-reference'});
  for(const r of snapshot.rules.filter(r=>r.deferred))add('sentences',{wordIds:r.words.map(w=>words.get(w)),notes:'mirror 暂不维护，保留当前实现。',deferred:true,active:true});
  upgradeDesign(d);
  reconcile(d);
  for(const s of active(d.sentences).filter(s=>s.active&&!s.deferred))add('cases',{sentenceId:s.id,name:'一般情境',conditions:'',entityIds:[],spaceIds:[],behaviorMode:'supplement',behavior:'',feedback:'',camera:'',lifecycle:'',expected:'',notes:'待补充情境要求，不代表已验收。'});
  return allocate(d).doc;
}
export function resolve(d,wordIds) {
  if(wordIds.some(id=>!d.words.some(w=>w.id===id&&!w.archived)))return {accepted:false,reason:'句子包含已归档或不存在的词'};
  const e=active(d.exceptions).find(x=>canonical(x.words)===canonical(wordIds));
  if(e)return {accepted:!e.reject,sourceId:e.id,source:'例外',intent:e.reject?null:e.intent,reason:e.reject?'此组合明确不成立':''};
  const matches=active(d.grammars).filter(g=>!g.designDraft&&g.slots.length===wordIds.length&&g.slots.every((slot,i)=>{const w=d.words.find(w=>w.id===wordIds[i]&&!w.archived);return w&&matchesSlot(d,slot,w);}));
  if(matches.length>1)return {accepted:false,reason:'多个句型同时匹配，需要消除歧义',ambiguous:true};
  if(matches.length)return {accepted:true,sourceId:matches[0].id,source:'句型',intent:matches[0].intent};
  if(d.sentences.some(s=>s.deferred&&!s.archived&&canonical(s.wordIds)===canonical(wordIds)))return {accepted:true,source:'暂缓迁移',intent:{action:'Legacy',kind:'Sustained',scope:'ExistingController',subject:-1,target:-1}};
  return {accepted:false,reason:'没有匹配的句型'};
}
export function candidates(d) {
  const found=new Map();
  function add(ids){found.set(ids.join('|'),ids);}
  for(const g of active(d.grammars)) {
    if(g.designDraft)continue;
    if(g.slots.length<2||g.slots.length>8)throw Error('句型需要 2–8 个位置。');
    const domains=g.slots.map(s=>active(d.words).filter(w=>matchesSlot(d,s,w)));
    // Large vocabularies remain authorable. Keep documented examples and resolve other sequences on demand.
    if(domains.reduce((n,domain)=>n*domain.length,1)>50000){
      for(const sentence of d.sentences)if(sentence.wordIds.length===domains.length&&domains.every((domain,i)=>domain.some(w=>w.id===sentence.wordIds[i])))add(sentence.wordIds);
      continue;
    }
    function visit(i,row){if(i===domains.length){add(row);return;}for(const w of domains[i])visit(i+1,[...row,w.id]);}visit(0,[]);
  }
  const available=ids=>ids.every(id=>d.words.some(w=>w.id===id&&!w.archived));
  for(const e of active(d.exceptions))if(available(e.words))add(e.words);
  for(const s of d.sentences.filter(s=>s.deferred&&!s.archived))if(available(s.wordIds))add(s.wordIds);
  return [...found.values()];
}
export function reconcile(d) {
  const byKey=new Map(d.sentences.map(s=>[s.wordIds.join('|'),s]));
  for(const s of d.sentences)s.active=false;
  for(const ids of candidates(d)){let s=byKey.get(ids.join('|'));if(!s){s={id:tempId(),wordIds:ids,notes:'',archived:false};d.sentences.push(s);}s.active=true;}
  return d;
}
export function allocate(doc) {
  const d=clone(doc),map={};
  for(const [k,prefix]of Object.entries(collections))for(const row of d[k]){
    if(row.id.startsWith('draft-')){const n=(d.counters[k]??0)+1;d.counters[k]=n;map[row.id]=prefix+'-'+String(n).padStart(4,'0');}
  }
  function remap(x){if(typeof x==='string')return map[x]??x;if(Array.isArray(x))return x.map(remap);if(x&&typeof x==='object')return Object.fromEntries(Object.entries(x).map(([k,v])=>[map[k]??k,remap(v)]));return x;}
  return {doc:remap(d),map};
}
export function sourceBasis(d,ref,includeCases=true) {
  const row=d[ref.collection]?.find(x=>x.id===ref.id);if(!row)return null;
  const base={record:clone(row)};
  if(ref.collection==='cases'){base.sentence=sourceBasis(d,{collection:'sentences',id:row.sentenceId},false);base.entities=row.entityIds.map(id=>sourceBasis(d,{collection:'entities',id}));base.spaces=d.spaces.filter(x=>row.spaceIds.includes(x.id));}
  if(ref.collection==='sentences'){base.words=d.words.filter(x=>row.wordIds.includes(x.id));base.grammar=resolve(d,row.wordIds);base.definition=[...d.grammars,...d.exceptions].find(x=>x.id===base.grammar.sourceId)??null;if(includeCases)base.cases=active(d.cases).filter(c=>c.sentenceId===row.id).map(c=>sourceBasis(d,{collection:'cases',id:c.id}));}
  if(ref.collection==='relations')base.sentences=row.sentenceIds.map(id=>sourceBasis(d,{collection:'sentences',id}));
  if(ref.collection==='entities'){for(const k of ['properties','outputs','spaces'])base[k]=d[k].filter(x=>row[k].includes(x.id));}
  if(ref.collection==='grammars')base.words=d.words.filter(x=>row.slots.some(s=>matchesSlot(d,s,x)));
  if(ref.collection==='words')base.properties=d.properties.filter(p=>row.propertyIds?.includes(p.id));
  if(ref.collection==='chapters'){
    base.spaces=chapterSpaces(d,row);
    const edges=new Map();
    for(const space of base.spaces)for(const edge of spaceEdges(d,space.id))edges.set(edge.id,edge);
    base.boundaries=[...edges.values()];
  }
  if(ref.collection==='boundaries')base.spaces=d.spaces.filter(s=>s.id===row.from||s.id===row.to).map(s=>({id:s.id,name:s.name,chapterId:s.chapterId}));
  if(ref.collection==='spaces'){
    base.boundaries=spaceEdges(d,row.id);
    base.chapter=d.chapters.filter(c=>c.id===row.chapterId).map(c=>({id:c.id,name:c.name}));
  }
  if(ref.collection==='spaces')base.objects=d.entities.filter(e=>!e.archived&&e.spaces.includes(row.id)).map(e=>({id:e.id,name:e.name,properties:e.properties,outputs:e.outputs}));
  if(ref.collection==='words'||ref.collection==='grammars'){
    const ids=ref.collection==='words'?classIds(d,row):row.slots.flatMap(s=>classIds(d,s));
    base.wordClasses=d.wordClasses.filter(c=>ids.includes(c.id));
  }
  return base;
}
export const stale = (d,r)=>canonical(r.basis)!==canonical(sourceBasis(d,r.source));
export function submit(d,source,text,version='') {
  if(!text.trim())throw Error('请先填写需要开发或修改的内容。');
  const basis=sourceBasis(d,source);if(!basis)throw Error('关联内容不存在。');
  let r=d.requests.find(r=>canonical(r.source)===canonical(source)&&!['passed','deferred'].includes(r.status)&&r.version===version);
  if(!r){r={id:tempId(),name:basis.record.name??'句子调整',source,version,implementation:'',validation:'',feedback:'',events:[],archived:false};d.requests.push(r);}
  r.events.push({at:new Date().toISOString(),type:'submit',request:text,basis:clone(basis)});r.request=text;r.basis=clone(basis);r.status='queued';return r;
}
export function validate(d,previous=null) {
  if(d.format!=='puzzleapple-v5-workbench'||d.schemaVersion!==3)throw Error('不支持的数据格式。');
  const seen=new Set();for(const k of Object.keys(collections)){if(!Array.isArray(d[k]))throw Error('缺少集合：'+k);for(const r of d[k]){if(typeof r.id!=='string'||seen.has(r.id))throw Error('编号缺失或重复。');seen.add(r.id);}}
  function refs(k,ids){if(!Array.isArray(ids)||ids.some(id=>!d[k].some(r=>r.id===id)))throw Error('无效关联：'+labels[k]);}
  for(const w of d.words){if(!Number.isInteger(w.classes)||w.classes<0||w.classes>7)throw Error('词类无效。');if(w.symbol&&!/^data:image\/(png|jpeg|webp);base64,[a-zA-Z0-9+/=]+$/.test(w.symbol))throw Error('字形只支持 PNG、JPEG、WebP。');}
  for(const w of d.words)if(w.propertyIds)refs('properties',w.propertyIds);
  for(const owner of [...d.words,...d.grammars.flatMap(g=>g.slots)])if(owner.classIds)refs('wordClasses',owner.classIds);
  const names=active(d.wordClasses).map(c=>c.name.trim());
  if(names.some(n=>!n)||new Set(names).size!==names.length)throw Error('词类名称不能为空或重复。');
  for(const c of d.wordClasses)if(c.archived&&classUsers(d,c.id).length)throw Error('词类仍被使用，请先调整关联符号或语法。');
  validateLevels(d);
  const itemKeys=new Set();
  for(const b of d.batches){
    if(!Array.isArray(b.items)||!['draft',...Object.keys(statuses)].includes(b.status))throw Error('批次数据无效。');
    for(const item of b.items){
      if(!item.id||itemKeys.has(item.id)||!Array.isArray(item.refs)||['text','expected','implementation','validation','feedback'].some(k=>typeof item[k]!=='string'))throw Error('需求编号或关联无效。');
      itemKeys.add(item.id);
      for(const ref of item.refs)if(!collections[ref.collection]||['batches','requests'].includes(ref.collection)||!d[ref.collection].some(x=>x.id===ref.id))throw Error('需求引用的设计不存在。');
    }
  }
  const keys=active(d.words).map(x=>x.key);if(keys.some(k=>!k)||new Set(keys).size!==keys.length)throw Error('词的语义键不能为空或重复。');
  const intent=(i,n)=>{if(!i||!actions[i.action]||!kinds[i.kind]||!scopes[i.scope]||![i.subject,i.target].every(x=>Number.isInteger(x)&&x>=-1&&x<n))throw Error('句型的动作、主语或目标设置无效。');};
  for(const g of d.grammars){if(!Array.isArray(g.slots)||(!g.designDraft&&g.slots.length<2)||g.slots.length>8)throw Error('句型需要 2–8 个位置。');for(const s of g.slots){refs('words',s.words);if(!Number.isInteger(s.classes)||s.classes<0||s.classes>7)throw Error('槽位词类无效。');}intent(g.intent,g.slots.length);}
  const ekeys=new Set();for(const e of d.exceptions){refs('words',e.words);if(e.words.length<2||e.words.length>8)throw Error('例外需要 2–8 个词。');if(!e.reject)intent(e.intent,e.words.length);if(!e.archived){const k=e.words.join('|');if(ekeys.has(k))throw Error('存在重复的例外词序。');ekeys.add(k);}}
  for(const e of d.entities)for(const k of ['properties','outputs','spaces'])refs(k,e[k]);
  for(const s of d.sentences)refs('words',s.wordIds);
  for(const c of d.cases){refs('sentences',[c.sentenceId]);refs('entities',c.entityIds);refs('spaces',c.spaceIds);}
  for(const r of d.relations)refs('sentences',r.sentenceIds);
  for(const r of d.requests){if(!statuses[r.status]||!sourceBasis(d,r.source))throw Error('开发事项的状态或来源无效。');if(['passed','review'].includes(r.status)&&stale(d,r)){const old=previous?.requests.find(x=>x.id===r.id);if(!old||old.status!==r.status||canonical(old.basis)!==canonical(r.basis))throw Error('设计已变化，请重新提交开发依据，再提交验收。');}if(['review','passed'].includes(r.status)&&(!r.implementation.trim()||!r.validation.trim()))throw Error('提交验收前请填写实现说明与验证结果。');}
  for(const item of Object.values(d.review.items)){if(!['todo','pass','rework','skip'].includes(item.verdict))throw Error('验收结论无效。');}
  candidates(d);return d;
}
export function prepareSave(incoming,previous) {
  if(incoming.schemaVersion!==3)throw Error('工作台已更新，请刷新后继续编辑。');
  const d=clone(incoming);d.counters=clone(previous.counters);
  for(const k of Object.keys(collections)){for(const old of previous[k])if(!d[k].some(r=>r.id===old.id))d[k].push({...clone(old),archived:true});for(const r of d[k])if(!r.id.startsWith('draft-')&&!previous[k].some(x=>x.id===r.id))throw Error('新条目必须使用草稿编号。');}
  // Only explicit workflow commands may publish history or change a submitted batch.
  if(d.rules.length!==previous.rules.length||d.rules.some((r,i)=>r.id!==previous.rules[i].id||r.archived||!r.description?.trim()))throw Error('请保留两份全局规则并填写正文。');
  d.timeline=clone(previous.timeline);
  for(const batch of d.batches){
    const old=previous.batches.find(b=>b.id===batch.id);
    if(!old){if(batch.status!=='draft')throw Error('新批次应先建立草稿。');continue;}
    if(old.status!==batch.status||old.archived!==batch.archived)throw Error('请使用批次的状态操作。');
    if(old.status==='draft')continue;
    const locked=b=>({...b,items:b.items.map(({implementation,validation,feedback,...item})=>item)});
    if(canonical(locked(old))!==canonical(locked(batch)))throw Error('已提交的需求须重新打开后修订。');
    for(const item of batch.items){
      const was=old.items.find(i=>i.id===item.id);
      if(was.verdict==='pass'&&canonical(was)!==canonical(item))throw Error('已验收的需求已冻结。');
    }
  }
  d.rounds=clone(previous.rounds);d.review.number=previous.review.number;
  for(const r of d.requests){const old=previous.requests.find(x=>x.id===r.id);if(old){if(canonical(r.events.slice(0,old.events.length))!==canonical(old.events))throw Error('不能改写开发事项的历史。');if(old.status!==r.status)r.events.push({at:new Date().toISOString(),type:'status',from:old.status,to:r.status,implementation:r.implementation,validation:r.validation,feedback:r.feedback});}}
  validate(d,previous);reconcile(d);const result=allocate(d);result.doc.revision=previous.revision+1;return result;
}
// A stale editor may carry only word-class edits. Never discard other draft changes
// or replace a class that another editor has independently changed.
export function mergeWordClassDraft(base,draft,current){
  if(!base||base.schemaVersion!==draft.schemaVersion||base.revision!==draft.revision)return null;
  const {wordClasses:original,...baseRest}=base;
  const {wordClasses:edited,...draftRest}=draft;
  if(!Array.isArray(edited)||canonical(baseRest)!==canonical(draftRest)||canonical(original)===canonical(edited))return null;
  const result=clone(current),byId=new Map(original.map(c=>[c.id,c]));
  if(original.some(c=>!edited.some(x=>x.id===c.id)))return null;
  for(const row of edited){
    const old=byId.get(row.id);
    if(canonical(old)===canonical(row))continue;
    const index=result.wordClasses.findIndex(c=>c.id===row.id),live=result.wordClasses[index];
    if(old&&canonical(live)!==canonical(old)&&canonical(live)!==canonical(row))throw Error('词类「'+(old.name||old.id)+'」在项目和草稿中均已修改。请先导出草稿，再核对项目版本。');
    if(!old&&(!row.id.startsWith('draft-')||live))return null;
    if(index<0)result.wordClasses.push(clone(row));
    else result.wordClasses[index]=clone(row);
  }
  return result;
}
export function restoreDesign(old,current){const d=clone(old);for(const k of Object.keys(collections))for(const row of current[k])if(!d[k].some(x=>x.id===row.id))d[k].push({...clone(row),archived:true});d.requests=clone(current.requests);d.batches=clone(current.batches);d.timeline=clone(current.timeline);d.review=clone(current.review);d.rounds=clone(current.rounds);d.counters=clone(current.counters);d.revision=current.revision;for(const c of d.chapters)c.spaceOrder=c.spaceOrder.filter(id=>d.spaces.some(s=>s.id===id&&s.chapterId===c.id));return d;}
export function diff(before,after){const out=[];for(const k of Object.keys(collections)){const ids=new Set([...before[k],...after[k]].map(x=>x.id));for(const id of ids){const a=before[k].find(x=>x.id===id),b=after[k].find(x=>x.id===id);if(canonical(a)===canonical(b))continue;const fields=Object.keys({...a,...b}).filter(f=>canonical(a?.[f])!==canonical(b?.[f]));out.push({collection:k,id,name:b?.name??a?.name??id,fields});}}return out;}
