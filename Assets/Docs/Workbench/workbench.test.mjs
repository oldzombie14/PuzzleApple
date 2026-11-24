import {chapterSpaces,moveSpace,reorderSpace,spaceEdges} from './levels.mjs';
import {ruleSources,readSourceSection} from './principles.mjs';
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {seed,clone,resolve,reconcile,prepareSave,submit,stale,sourceBasis,restoreDesign,validate,tempId,canonical} from './model.mjs';
import {upgradeDesign,dropGrammarSlot} from './design.mjs';
import {mergeWordClassDraft} from './model.mjs';
import {baseline,brief,makeBatch,makeItem,publish,outdated} from './workflow.mjs';
import {createWorkbench} from './server.mjs';
const dir=path.dirname(fileURLToPath(import.meta.url)),root=path.resolve(dir,'../../..');
const snapshot=JSON.parse(fs.readFileSync(path.join(dir,'implementation.json'),'utf8'));
const fresh=()=>seed(snapshot);
test('V5 candidate recognition matches every exported Unity two/three word combination',()=>{
  const d=fresh();const byKey=new Map(d.words.map(w=>[w.key,w.id]));let accepted=0;
  for(const row of snapshot.combinations){const result=resolve(d,row.words.split(' ').map(k=>byKey.get(k)));assert.equal(result.accepted,row.accepted,row.words);if(result.accepted)accepted++;}
  assert.equal(accepted,24);assert.equal(d.sentences.length,26);
});
test('stable sentence IDs and notes survive template retirement, restore and renaming',()=>{
  const d=fresh(),s=d.sentences.find(s=>!s.deferred);s.notes='人工说明';const id=s.id;
  const incoming=clone(d);incoming.grammars.forEach(g=>g.archived=true);incoming.exceptions.forEach(e=>e.archived=true);
  const archived=prepareSave(incoming,d).doc;assert.equal(archived.sentences.find(x=>x.id===id).active,false);
  const restored=prepareSave(restoreDesign(d,archived),archived).doc;
  assert.equal(restored.sentences.find(x=>x.id===id).notes,'人工说明');assert.equal(restored.sentences.find(x=>x.id===id).active,true);
  restored.words[0].name='新的名字';reconcile(restored);assert.equal(restored.sentences.find(x=>x.id===id).notes,'人工说明');
});
test('failed saves consume no IDs; draft references remap together',()=>{
  const d=fresh(),next=clone(d),c=clone(d.cases[0]);c.id=tempId();c.name='第二情境';next.cases.push(c);
  const req=submit(next,{collection:'cases',id:c.id},'实现这个情境');const saved=prepareSave(next,d);
  const savedCase=saved.map[c.id],savedRequest=saved.doc.requests.find(x=>x.id===saved.map[req.id]);
  assert.ok(savedCase.startsWith('C-'));assert.equal(savedRequest.source.id,savedCase);assert.equal(savedRequest.basis.record.id,savedCase);
  next.entities[0].spaces=['missing'];assert.throws(()=>prepareSave(next,d));assert.equal(d.counters.cases,24);
});
test('task basis changes block new acceptance, resubmission merges open work, passed work creates new ID',()=>{
  let d=fresh();const c=d.cases[0],source={collection:'cases',id:c.id};let r=submit(d,source,'调整表现','一批');d=prepareSave(d,fresh()).doc;r=d.requests[0];
  r.implementation='已调整';r.validation='实际流程通过';r.status='review';validate(d);
  d.cases[0].behavior='新的表现';assert.equal(stale(d,r),true);assert.throws(()=>validate(d));
  const id=r.id;submit(d,source,'更新要求','一批');assert.equal(d.requests.length,1);assert.equal(d.requests[0].id,id);assert.equal(stale(d,r),false);
  r.status='passed';validate(d);submit(d,source,'另一次改动','一批');assert.equal(d.requests.length,2);
});
test('unrelated records do not invalidate a case review basis',()=>{
  const d=fresh(),r=submit(d,{collection:'cases',id:d.cases[0].id},'修改表现');d.spaces[0].description='无关联空间';assert.equal(stale(d,r),false);d.words.find(w=>w.id===d.sentences.find(s=>s.id===d.cases[0].sentenceId).wordIds[0]).meaning='改变含义';assert.equal(stale(d,r),true);
});
test('linked capability changes invalidate requirements and archived words retire exceptions',()=>{
  const d=fresh(),c=d.cases[0];c.entityIds=[d.entities[0].id];const r=submit(d,{collection:'cases',id:c.id},'按能力执行');d.properties.find(p=>p.id===d.entities[0].properties[0]).description='新的资格定义';assert.equal(stale(d,r),true);
  const w=d.words.find(w=>w.key==='world');w.archived=true;reconcile(d);assert.equal(d.sentences.filter(s=>s.active&&s.wordIds.includes(w.id)).length,0);
});
test('restoring designs keeps task status, round history and higher allocated IDs',()=>{
  const old=fresh(),current=clone(old);submit(current,{collection:'cases',id:current.cases[0].id},'任务');current.requests[0].status='working';current.rounds=[{number:1,items:{}}];current.review.number=2;
  const restored=restoreDesign(old,current);assert.equal(restored.requests[0].status,'working');assert.deepEqual(restored.rounds,current.rounds);assert.equal(restored.review.number,2);
});
test('HTTP persistence, revision conflicts, frozen rounds, invalid data, origin protection and restart',async t=>{
  const temp=fs.mkdtempSync(path.join(root,'Temp/workbench-test-'));let server=createWorkbench({dataDirectory:temp});await new Promise(r=>server.listen(0,'127.0.0.1',r));
  t.after(()=>server.close());let base='http://127.0.0.1:'+server.address().port;
  const get=async p=>(await fetch(base+'/api/'+p)).json();let s=await get('state');const post=async(p,input,token=s.token,origin=base)=>fetch(base+'/api/'+p,{method:'POST',headers:{Origin:origin,'X-Workbench-Token':s.csrf,'Content-Type':'application/json'},body:JSON.stringify({baseToken:token,...input})});
  const initial=s.token;s.doc.cases[0].expected='门打开后轮廓可见';s.doc.review.items[s.doc.cases[0].id]={verdict:'pass',comment:'通过',basis:sourceBasis(s.doc,{collection:'cases',id:s.doc.cases[0].id})};
  let response=await post('save',{doc:s.doc,note:'验收依据'});assert.equal(response.status,200);s=await response.json();
  response=await post('save',{doc:s.doc},initial);assert.equal(response.status,409);
  response=await post('save',{doc:s.doc},s.token,'http://evil.example');assert.equal(response.status,403);
  const invalid=clone(s.doc);invalid.cases[0].entityIds=['unknown'];response=await post('save',{doc:invalid});assert.equal(response.status,400);assert.equal((await get('state')).token,s.token);
  response=await post('round',{note:'首轮完成'});assert.equal(response.status,200);s=await response.json();assert.equal(s.doc.rounds.length,1);assert.equal(s.doc.review.number,2);assert.equal(s.doc.review.items[s.doc.cases[0].id].verdict,'skip');
  const frozen=canonical(s.doc.rounds[0]);s.doc.rounds[0].note='篡改旧轮次';s.doc.cases[0].expected='之后的新要求';response=await post('save',{doc:s.doc});assert.equal(response.status,200);s=await response.json();assert.equal(canonical(s.doc.rounds[0]),frozen);
  response=await post('round',{});assert.equal(response.status,400); // carried skip is stale
  const histories=await get('history');assert.ok(histories.length>=3);const saved=s.token;
  await new Promise(r=>server.close(r));server=createWorkbench({dataDirectory:temp});await new Promise(r=>server.listen(0,'127.0.0.1',r));base='http://127.0.0.1:'+server.address().port;assert.equal((await get('state')).token,saved);
});

test('design migration preserves original IDs, restrictions, review and legacy request snapshots',()=>{
  const old=fresh();old.schemaVersion=1;
  for(const key of ['rules','wordClasses','batches','timeline'])delete old[key];
  old.requests=[{id:'D-0017',name:'门',request:'改变颜色',source:{collection:'words',id:'W-0001'},basis:{record:old.words[0]},status:'queued',implementation:'',validation:'',feedback:'',events:[],archived:false}];
  const next=upgradeDesign(clone(old));
  assert.deepEqual(next.grammars.map(g=>g.slots),old.grammars.map(g=>g.slots));
  assert.deepEqual(next.sentences,old.sentences);assert.deepEqual(next.rounds,old.rounds);
  assert.equal(next.batches[0].legacyId,'D-0017');assert.deepEqual(next.batches[0].items[0].basis[0].after,old.requests[0].basis);
  assert.deepEqual(upgradeDesign(clone(next)),next);
});
test('custom class and fixed symbol define a structure, draft grammars save without affecting recognition',()=>{
  const d=fresh(),custom={id:tempId(),name:'测试词类',archived:false};
  d.wordClasses.push(custom);
  const w=d.words.find(x=>x.key==='world'),move=d.words.find(x=>x.key==='move');
  w.classIds=[custom.id];
  const g={id:tempId(),name:'自定义结构',archived:false,designDraft:true,slots:[],intent:{action:'Move',kind:'Sustained',scope:'CurrentRegion',subject:-1,target:-1}};
  d.grammars.push(g);validate(d);
  g.slots=[{classes:0,classIds:[custom.id],words:[]},{classes:0,classIds:[],words:[move.id]}];
  assert.equal(resolve(d,[w.id,move.id]).accepted,false);
  g.designDraft=false;assert.equal(resolve(d,[w.id,move.id]).sourceId,g.id);
  custom.archived=true;assert.throws(()=>validate(d),/词类仍被使用/);
});
test('batch stages, partial acceptance and session archives remain distinct and immutable',()=>{
  const original=fresh(),d=clone(original),base=baseline(original,d);
  const batch=makeBatch();batch.name='范围与颜色';
  const item=batch.items[0];item.text='调整词义并同步关卡';item.refs=[{collection:'words',id:d.words[0].id},{collection:'spaces',id:d.spaces[0].id}];
  const other=makeItem();other.text='改变颜色';batch.items.push(other);d.batches.push(batch);
  const saved=prepareSave(d,original).doc;assert.equal(saved.timeline.length,0);
  const b=saved.batches[0],cmd=(action,extra={})=>publish(saved,{action,batchId:b.id,...extra},base);
  cmd('submit');const frozen=canonical(saved.timeline[0]);assert.equal(b.items[0].basis.length,2);
  cmd('progress',{summary:'完成第一项'});assert.equal(b.status,'working');assert.equal(saved.timeline.length,2);
  for(const i of b.items){i.implementation='已修改';i.validation='实际核对完成';}
  const w=saved.words[0];w.meaning='新要求';assert.ok(outdated(saved,b.items[0]));assert.throws(()=>cmd('review'),/依据/);
  cmd('revise');cmd('submit');cmd('review');
  cmd('decide',{decisions:{[item.id]:'pass'}});assert.equal(b.status,'review');
  b.items[1].feedback='再改一处';cmd('decide',{decisions:{[other.id]:'rework'}});assert.equal(b.status,'rework');
  cmd('review');cmd('decide',{decisions:{[other.id]:'pass'}});assert.equal(b.status,'passed');
  const session={action:'session',sessionKey:'同一会话',summary:'会话收尾',detail:'本批改动与自测'};
  publish(saved,session);assert.throws(()=>publish(saved,session),/已经归档/);
  assert.equal(canonical(saved.timeline[0]),frozen);assert.match(brief(saved,b),/改变颜色/);
  const incoming=clone(saved);incoming.timeline=[];
  assert.equal(prepareSave(incoming,saved).doc.timeline.length,saved.timeline.length);
  assert.equal(restoreDesign(original,saved).batches[0].status,'passed');
});

test('v2 chapters migrate by authored name without overwriting edited principles or publishing history',()=>{
  const original=fresh(),old=clone(original);
  old.schemaVersion=2;delete old.chapters;delete old.boundaries;
  const names=new Map(original.chapters.map(c=>[c.id,c.name]));
  for(const s of old.spaces){s.chapter=names.get(s.chapterId);delete s.chapterId;delete s.kind;}
  old.rules[0].description='保留设计者手写原则';
  old.spaces[0].chapter='自定义大关';
  const migrated=upgradeDesign(old);
  assert.equal(migrated.rules[0].description,'保留设计者手写原则');
  assert.equal(migrated.chapters.find(c=>c.id===migrated.spaces[0].chapterId).name,'自定义大关');
  assert.deepEqual(migrated.spaces.map(s=>s.id),original.spaces.map(s=>s.id));
  assert.deepEqual(migrated.timeline,original.timeline);
  assert.equal(original.chapters.length,3);assert.equal(original.boundaries.filter(e=>e.progressLock).length,4);
  const far=original.spaces.find(s=>s.source.name==='FarRoom');
  const doors=spaceEdges(original,far.id);
  assert.equal(doors.filter(e=>e.kind==='door').length,2);
  for(const edge of doors)assert.ok(spaceEdges(original,edge.from===far.id?edge.to:edge.from).includes(edge));
  assert.equal(original.boundaries.filter(e=>e.kind==='membrane').length,2);
});
test('chapter layout, cross-chapter moves and linked design references survive a save and restore',()=>{
  const original=fresh(),d=clone(original),c=d.chapters[0],spaces=chapterSpaces(d,c);
  const edges=clone(d.boundaries);
  assert.ok(reorderSpace(d,c,spaces[0].id,spaces[2].id));
  assert.equal(chapterSpaces(d,c)[2].id,spaces[0].id);
  c.name='新章名';spaces[0].name='新空间名';
  const newChapter={id:tempId(),name:'下一关',archived:false,description:'',spaceOrder:[]};d.chapters.push(newChapter);
  moveSpace(d,spaces[0],newChapter);
  const saved=prepareSave(d,original),id=saved.map[newChapter.id];
  assert.equal(saved.doc.spaces.find(s=>s.id===spaces[0].id).chapterId,id);
  assert.deepEqual(saved.doc.boundaries,edges);assert.ok(sourceBasis(saved.doc,{collection:'chapters',id}).spaces.some(s=>s.name==='新空间名'));
  const restored=prepareSave(restoreDesign(original,saved.doc),saved.doc).doc;
  assert.ok(restored.chapters.find(c=>c.id===id).archived);assert.deepEqual(restored.boundaries,edges);
});
test('all visible design source references resolve against current documents',()=>{
  for(const source of ruleSources){
    const file=path.resolve(root,'Assets/Docs',source.file);
    assert.ok(!file.includes('Archive'));
    assert.ok(readSourceSection(fs.readFileSync(file,'utf8'),source).trim().length,source.id);
  }
});

test('stale class drafts preserve independent project edits and require review for overlapping edits',()=>{
  const base=fresh(),local=clone(base),remote=clone(base);
  const id=tempId();local.wordClasses[2].name='常规动词';local.wordClasses.push({id,name:'化动词',description:'',archived:false});
  remote.revision++;remote.rules[0].description='新的项目原则';
  const merged=mergeWordClassDraft(base,local,remote);
  const saved=prepareSave(merged,remote);assert.equal(saved.map[id],'T-0004');
  assert.equal(saved.doc.rules[0].description,remote.rules[0].description);assert.equal(saved.doc.wordClasses[2].name,'常规动词');
  remote.wordClasses[2].name='其他名称';assert.throws(()=>mergeWordClassDraft(base,local,remote),/均已修改/);
  local.words[0].meaning='同时编辑过的词义';assert.equal(mergeWordClassDraft(base,local,remote),null);
});
test('dragging grammar slots preserves constraints and the bindings of the original subject and target',()=>{
  const slot=id=>({classes:0,classIds:[],words:[id]});
  const [a,b,c]=['a','b','c'].map(slot),g={slots:[a,b,c],intent:{subject:0,target:2}};
  dropGrammarSlot(g,{index:2},'insert',0);assert.deepEqual(g.slots,[c,a,b]);assert.deepEqual(g.intent,{subject:1,target:0});
  const noun={classes:0,classIds:['T-0002'],words:[]};
  dropGrammarSlot(g,{slot:noun},'insert',1);assert.deepEqual(g.slots,[c,noun,a,b]);assert.deepEqual(g.intent,{subject:2,target:0});
  const fixed=slot('d');dropGrammarSlot(g,{slot:fixed},'replace',2);assert.equal(g.slots[g.intent.subject],fixed);
  dropGrammarSlot(g,{index:0},'remove',0);assert.deepEqual(g.intent,{subject:1,target:-1});
  dropGrammarSlot(g,{index:0},'remove',0);assert.throws(()=>dropGrammarSlot(g,{index:0},'remove',0),/两个位置/);
  assert.equal(dropGrammarSlot(g,{index:0},'insert',1),false);
  g.slots=Array.from({length:8},(_,i)=>slot(String(i)));assert.throws(()=>dropGrammarSlot(g,{slot:fixed},'insert',8),/八个位置/);
});
