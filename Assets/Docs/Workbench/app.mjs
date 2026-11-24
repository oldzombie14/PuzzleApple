import {levelUI} from './level-ui.mjs';
import {ruleSources} from './principles.mjs';
import {upgradeDesign,classIds,classUsers,dropGrammarSlot} from './design.mjs';
import {brief} from './workflow.mjs';
import {batchUI} from './batch-ui.mjs';
import {collections,labels,statuses,actions,kinds,scopes,active,clone,tempId,canonical,resolve,reconcile,submit,sourceBasis,stale,diff} from './model.mjs';
const $=s=>document.querySelector(s),esc=x=>String(x??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const nav={overview:'工作概览',language:'符号与语法',sentences:'具体句子',capabilities:'关卡与对象',rules:'全局规则',development:'开发批次',review:'批次验收'};
const groups={language:['words','grammars','wordClasses','exceptions'],capabilities:['chapters','entities','properties','outputs'],sentences:['sentences','relations']};
let state,doc,dirty=false,query='',showArchived=false,filter='',busy=false,toastTimer,draftTimer,renderedHash='';
const draftKey='PuzzleApple.V5.workbench.draft.v1';
let route=()=>{const [page='overview',collection,id]=location.hash.slice(1).split('/');return {page:nav[page]?page:'overview',collection:collection??groups[page]?.[0],id};};
const find=(k,id)=>doc[k]?.find(r=>r.id===id);
const labelId=id=>id?.startsWith('draft-')?'未保存':id;
const name=(k,id)=>k==='words'?labelId(id):find(k,id)?.name??labelId(id);
function titleSentence(s){return s.wordIds.map(id=>name('words',id)).join(' · ');}
function glyph(id,big=false){const w=find('words',id);if(!w)return '';const original=state.snapshot.words.find(x=>x.key===w.key);return w.symbol||original?`<img class="glyph ${big?'big':''}" src="${esc(w.symbol||'/symbol/'+encodeURIComponent(w.key))}" alt="${esc(labelId(w.id))}" title="${esc(labelId(w.id))}">`:`<span class="glyph-fallback">${esc(labelId(w.id))}</span>`;}
const symbols=(ids,large=false)=>`<span class="glyphs ${large?'large':''}">${ids.map(id=>glyph(id)).join('')}<span class="period" aria-label="句号"></span></span>`;
const badge=(text,type='')=>`<span class="badge ${type}">${esc(text)}</span>`;
const button=(action,text,extra='',cls='')=>`<button data-action="${action}" ${extra} class="${cls}">${text}</button>`;
const attrs=(k,r,key)=>`data-k="${k}" data-id="${esc(r.id)}" data-field="${key}"`;
function field(k,r,key,label,options={}){const value=get(r,key)??'';return `<label class="field"><span>${label}</span>${options.area?`<textarea ${attrs(k,r,key)} placeholder="${esc(options.placeholder??'')}" class="${options.short?'short':''}">${esc(value)}</textarea>`:`<input ${attrs(k,r,key)} value="${esc(value)}" placeholder="${esc(options.placeholder??'')}">`}${options.help?`<span class="help">${options.help}</span>`:''}</label>`;}
function select(k,r,key,label,choices){return `<label class="field"><span>${label}</span><select aria-label="${esc(label)}" ${attrs(k,r,key)}>${Object.entries(choices).map(([v,t])=>`<option value="${esc(v)}" ${String(get(r,key))===v?'selected':''}>${esc(t)}</option>`).join('')}</select></label>`;}
function checks(k,r,key,label,items){return `<div class="field"><span>${label}</span><div class="checks">${items.map(x=>`<label class="check"><input type="checkbox" ${attrs(k,r,key)} data-ref="${esc(x.id)}" ${(get(r,key)??[]).includes(x.id)?'checked':''}>${['spaces','entities','properties','outputs'].includes(key)||key==='spaceIds'||key==='entityIds'?esc(labelId(x.id))+' · ':''}${esc(x.name)}</label>`).join('')||'<span class="muted">暂无条目</span>'}</div></div>`;}
function classes(k,r,key='classIds'){
  const owner=key==='classIds'?r:get(r,key.slice(0,-9));
  const selected=classIds(doc,owner);
  return '<div class="checks">'+active(doc.wordClasses).map(c=>'<label class="check"><input type="checkbox" '+attrs(k,r,key)+' data-class-id="'+esc(c.id)+'" '+(selected.includes(c.id)?'checked':'')+'>'+esc(c.name)+'</label>').join('')+'</div>';
}
const json=x=>`<pre>${esc(JSON.stringify(x,null,2))}</pre>`;
function get(x,key){return key.split('.').reduce((v,k)=>v?.[k],x);}
function set(x,key,value){const parts=key.split('.');for(const p of parts.slice(0,-1))x=x[p];x[parts.at(-1)]=value;}
function notify(text){if($('#dialog').open){let hint=$('#dialog-message');if(!hint){hint=document.createElement('p');hint.id='dialog-message';hint.setAttribute('role','status');$('#dialog-content').append(hint);}hint.textContent=text;}$('#toast').textContent=text;$('#toast').classList.add('visible');clearTimeout(toastTimer);toastTimer=setTimeout(()=>$('#toast').classList.remove('visible'),5200);}
function mark(){dirty=true;$('#save-status').textContent='未保存 · 草稿在此浏览器';clearTimeout(draftTimer);draftTimer=setTimeout(()=>{try{localStorage.setItem(draftKey,JSON.stringify({baseToken:state.token,doc,at:new Date().toISOString()}));}catch{notify('浏览器草稿未能保存，请保存到项目或导出。');}},250);}
async function api(path,body){const r=await fetch('/api/'+path,body?{method:'POST',headers:{'Content-Type':'application/json','X-Workbench-Token':state.csrf},body:JSON.stringify({baseToken:state.token,...body})}:{});const data=await r.json();if(!r.ok){if(r.status===409)$('#banner').innerHTML=`${esc(data.error)} ${button('export-draft','导出当前草稿')}${button('reload','读取项目版本')}`;throw Error(data.error);}return data;}
function accept(s){delete $('#banner').dataset.saveError;$('#banner').textContent='';state=s;doc=clone(s.doc);dirty=false;clearTimeout(draftTimer);localStorage.removeItem(draftKey);if(s.idMap){const r=route();if(s.idMap[r.id])history.replaceState(null,'','#'+[r.page,r.collection,s.idMap[r.id]].join('/'));}render();}
function saveFailure(error){
  const banner=$('#banner');
  if(!banner.querySelector('[data-action="reload"]'))banner.textContent='保存失败 · '+error.message;
  banner.dataset.saveError='true';
  $('#save-status').textContent='保存失败 · 草稿已保留';
}
async function save(note='保存设计'){
  if(busy)return false;
  busy=true;
  const controls=[$('main'),$('#dialog-content')];
  try{
    controls.forEach(el=>{el.inert=true;});
    $('#save-status').textContent='正在保存…';
    reconcile(doc);
    accept(await api('save',{doc,note}));
    notify('已保存到项目');return true;
  }catch(error){saveFailure(error);throw error;}
  finally{controls.forEach(el=>{el.inert=false;});busy=false;}
}
async function publishCommand(command){
  if(busy)throw Error('正在保存，请稍候。');
  busy=true;
  try{reconcile(doc);accept(await api('workflow',{doc,command}));notify('已发布到开发时间线');}
  finally{busy=false;}
}
function downloadText(value,filename){
  const url=URL.createObjectURL(new Blob([value],{type:'text/markdown;charset=utf-8'}));
  const a=document.createElement('a');a.href=url;a.download=filename;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
}
const batches=batchUI({documentData:()=>doc,stateData:()=>state,esc,button,badge,date,openDialog,render,mark,commit:publishCommand,sourceLink,downloadText});
const levels=levelUI({data:()=>doc,esc,button,field,select,requestBox,technical,openDialog,mark,render,showArchived:()=>showArchived});
function banner(){if(!$('#banner').dataset.saveError&&!$('#banner').querySelector('[data-action="reload"]'))$('#banner').textContent='';}
function render(){
  const expanded=renderedHash===location.hash?[...document.querySelectorAll('#workspace details')].flatMap((d,i)=>d.open?[i]:[]):[];
  const r=route();$('#nav').innerHTML=Object.entries(nav).map(([k,t])=>`<a href="#${k}" class="${r.page===k?'active':''}" ${r.page===k?'aria-current="page"':''}>${t}</a>`).join('');
  $('#heading').textContent=nav[r.page];$('#save-status').textContent=dirty?'未保存 · 草稿在此浏览器':'已保存 · 版本 '+doc.revision;banner();
  let content='';if(r.page==='overview')content=overview();else if(r.page==='rules')content=rulesPage(r);else if(['development','review'].includes(r.page))content=batches.page(r,r.page==='review');else content=editor(r);
  $('#workspace').innerHTML=content;
  const details=document.querySelectorAll('#workspace details');for(const i of expanded)if(details[i])details[i].open=true;renderedHash=location.hash;
}
function overview(){
  const shortcuts=[['language/words','符号',active(doc.words).length],['language/grammars','语法',active(doc.grammars).length],['sentences','具体句子',active(doc.sentences).filter(s=>s.active).length]];
  return '<div class="cards">'+shortcuts.map(([path,title,n])=>'<a class="shortcut" href="#'+path+'"><h2>'+title+'</h2><small>'+n+' 条</small><span aria-hidden="true">↗</span></a>').join('')+'</div>'+
    '<div class="overview-grid"><section class="panel"><div class="panel-head"><h2>全局规则</h2></div>'+doc.rules.map(rule=>'<a class="rule-link" href="#rules/rules/'+rule.id+'">'+esc(rule.name)+' <span>→</span></a>').join('')+'</section>'+batches.timeline()+'</div>'+
    '<details class="tools"><summary>开发工具</summary><p class="meta">Unity '+esc(state.snapshot.unityVersion)+' · 快照 '+date(state.snapshot.capturedAt)+(state.changedSources.length?' · '+state.changedSources.length+' 个相关源文件已变化，请重新导出快照。':'')+'</p>'+button('refresh','读取实现快照')+toolInventory()+'</details>';
}
function rulesPage(route){
  const selected=doc.rules.find(x=>x.id===route.id)??doc.rules[0];
  const tabs='<div class="tabs">'+doc.rules.map(r=>'<a class="'+(r===selected?'active':'')+'" href="#rules/rules/'+r.id+'">'+esc(r.name)+'</a>').join('')+'</div>';
  if(selected.id!=='A-0001')return tabs+'<section class="panel rules"><h2>'+esc(selected.name)+'</h2>'+field('rules',selected,'description','规则正文',{area:true})+requestBox('rules',selected)+'</section>';
  const paragraphs=selected.description.split(/\n\s*\n/).map(block=>{
    const [title,...body]=block.split('\n');
    return body.length?'<section class="principle"><h3>'+esc(title)+'</h3><p>'+esc(body.join('\n'))+'</p></section>':'<p class="prose">'+esc(block)+'</p>';
  }).join('');
  return tabs+'<section class="panel rules"><h2>设计原则</h2><div class="principle-list">'+paragraphs+'</div><details class="edit-principles"><summary>编辑原则正文</summary>'+field('rules',selected,'description','规则正文',{area:true})+'</details>'+
    '<details class="rule-references"><summary>查看当前项目文档依据</summary>'+ruleSources.map(source=>'<button data-action="read-rule-source" data-source="'+source.id+'"><strong>'+esc(source.title)+'</strong><small>'+esc(source.file)+' · '+esc(source.heading)+'</small></button>').join('')+'</details>'+requestBox('rules',selected)+'</section>';
}
function toolInventory(){return `<table><thead><tr><th>工具</th><th>用途与影响</th></tr></thead><tbody><tr><td>WorkbenchSnapshot.Export</td><td>只读 Unity；写入工作台实现快照。</td></tr><tr><td>V5 Checks</td><td>自动验证，按各自 Edit / Play 前提执行。</td></tr><tr><td>MoveMemoryLoopBaker / DoorMemoryBaker</td><td>重做词条记忆图，会覆盖对应 V5 图片资源。</td></tr><tr><td>SentenceRegionSetup</td><td>建立缺失资源、迁移范围；会写资产，仅开发时按需运行。</td></tr><tr><td>ColorMapTrial / ShaderStudy</td><td>独立美术试验；保留代码和场景，未纳入正式效果。</td></tr></tbody></table>`;}
function date(x){return new Date(x).toLocaleString('zh-CN',{hour12:false});}
function editor(r){
  if(r.page==='capabilities'&&['chapters','spaces','boundaries'].includes(r.collection)){
    const tabs='<div class="tabs">'+groups.capabilities.map(k=>'<a href="#capabilities/'+k+'" class="'+(k==='chapters'?'active':'')+'">'+labels[k]+'</a>').join('')+'</div>';
    return tabs+levels.page(r);
  }
  const tabs=groups[r.page];let k=tabs.includes(r.collection)?r.collection:tabs[0];let rows=doc[k].filter(x=>showArchived||!x.archived);
  if(k==='sentences'){rows.sort((a,b)=>Number(!!a.deferred)-Number(!!b.deferred));rows=rows.filter(s=>showArchived||s.active);if(filter==='missing')rows=rows.filter(s=>!actualRule(s));if(filter==='deferred')rows=rows.filter(s=>s.deferred);}
  if(k==='requests'&&filter)rows=rows.filter(x=>x.status===filter);
  if(query)rows=rows.filter(x=>(JSON.stringify(x)+(k==='sentences'?titleSentence(x):'')).toLowerCase().includes(query.toLowerCase()));
  let selected=rows.find(x=>x.id===r.id)??rows[0];
  return `
${tabs?`<div class="tabs">${tabs.map(t=>`<a href="#${r.page}/${t}" class="${k===t?'active':''}">${labels[t]}</a>`).join('')}</div>`:''}<div class="toolbar"><div class="row"><input type="search" id="search" aria-label="搜索条目" value="${esc(query)}" placeholder="搜索名称、内容或编号">${k==='requests'?`<select id="filter" aria-label="事项状态"><option value="">全部状态</option>${Object.entries(statuses).map(([v,t])=>`<option value="${v}" ${filter===v?'selected':''}>${t}</option>`).join('')}</select>`:k==='sentences'?`<select id="filter" aria-label="句子范围"><option value="">所有设计组合</option><option value="missing" ${filter==='missing'?'selected':''}>尚无执行绑定</option><option value="deferred" ${filter==='deferred'?'selected':''}>暂缓迁移</option></select>`:''}</div><div class="row"><label class="check"><input id="show-archived" type="checkbox" ${showArchived?'checked':''}>含归档</label>${k==='sentences'?button('compose','试组一句'):k==='requests'?'':button('add','新增'+(labels[k]),`data-k="${k}"`)}</div></div>
  <div class="split"><div class="record-list" aria-label="${labels[k]}列表">${rows.map(x=>record(k,x,r.page,selected)).join('')||'<div class="empty">暂无条目</div>'}</div><div>${selected?detail(k,selected):`<div class="panel empty">${k==='requests'?'从词、句型、对象或情境的详情中提交开发事项，系统会自动关联设计依据。':'新建条目，开始编辑。'}</div>`}</div></div>`;
}
function record(k,r,page,selected){const title=k==='sentences'?symbols(r.wordIds):k==='words'?`<span class="row">${glyph(r.id)}<span>${esc(labelId(r.id))}</span></span>`:page==='capabilities'?esc(labelId(r.id)):esc(r.name);let sub='';if(k==='sentences'){const result=resolve(doc,r.wordIds);sub=r.deferred?'暂缓迁移':!result.accepted?(result.reason??'不成立'):actualRule(r)?'已有执行绑定':'设计成立 · 尚无绑定';}else if(k==='requests')sub=statuses[r.status]+(stale(doc,r)?' · 依据已变化':'');return `<a class="record ${r.id===selected?.id?'active':''}" href="#${page}/${k}/${r.id}"><div class="record-title">${title}</div><span class="meta">${k==='words'?'':page==='capabilities'?esc(r.name):esc(labelId(r.id))}${sub?' · '+esc(sub):''}${r.archived?' · 已归档':''}</span></a>`;}
function head(k,r){return `<div class="panel-head"><div><p class="eyebrow">${esc(labelId(r.id))} / ${labels[k]}</p><h2>${esc(k==='sentences'?titleSentence(r):['words','entities','properties','outputs'].includes(k)?labelId(r.id):r.name)}</h2></div>${k==='sentences'?'':button('archive',r.archived?'恢复条目':'归档',`data-k="${k}" data-id="${r.id}"`,'quiet')}</div>${r.origin?'<p class="meta">初始来自实现参考，可编辑为设计要求；不代表已验收。</p>':''}`;}
function detail(k,r){let body='';if(k==='words')body=wordDetail(r);else if(k==='grammars')body=grammarDetail(r);else if(k==='exceptions')body=exceptionDetail(r);else if(k==='entities')body=entityDetail(r);else if(k==='wordClasses')body=classDetail(r);else if(['properties','outputs'].includes(k))body=field(k,r,'name','名称')+field(k,r,'description','定义与使用要求',{area:true})+technical(r.source);else if(k==='sentences')return sentenceDetail(r);else if(k==='relations')body=relationDetail(r);return `<section class="panel">${head(k,r)}${body}${requestBox(k,r)}</section>`;}
function technical(source){return source?`<details><summary>开发关联</summary>${json(source)}</details>`:'';}
function classDetail(c){
  const users=classUsers(doc,c.id);
  return field('wordClasses',c,'name','词类名称')+field('wordClasses',c,'description','用法说明',{area:true})+'<p class="section-label">使用此词类</p>'+users.map(r=>'<a class="chip" href="#language/'+(r.slots?'grammars':'words')+'/'+r.id+'">'+esc(r.slots?r.name:r.id)+'</a>').join('');
}
function wordDetail(w){
  const live=state.snapshot.words.find(x=>x.key===w.key);
  return '<div class="row subtle">'+glyph(w.id,true)+button('upload','更换字形','data-id="'+w.id+'"')+'<input id="upload-'+w.id+'" type="file" hidden accept="image/png,image/jpeg,image/webp" data-upload="'+w.id+'">'+(w.symbol?button('clear-symbol','恢复实现字形','data-id="'+w.id+'"'):'')+'</div>'+
    field('words',w,'meaning','含义备注',{area:true,placeholder:'可按不同用法分别说明。'})+
    '<div class="field"><span>词类 · 可多选</span>'+classes('words',w)+'<a class="meta" href="#language/wordClasses">管理词类 →</a></div>'+
    '<details><summary>开发关联</summary><p class="meta">对象性质用于说明这个符号在不同用法中能指向哪些场景对象。例如 light 用于开灯时指可发光的对象，用于 light move 时指正在发光的对象。</p>'+checks('words',w,'propertyIds','可指向的对象性质',active(doc.properties))+field('words',w,'key','实现映射键')+(live?json({asset:live.asset,classes:live.classes}):'暂无实现映射')+'</details>';
}
function intentForm(k,r,length){const slots={'-1':'无'};for(let i=0;i<length;i++)slots[i]='第 '+(i+1)+' 个词';return `<div class="two">${select(k,r,'intent.action','动作',actions)}${select(k,r,'intent.kind','生效方式',kinds)}${select(k,r,'intent.subject','主语来自',slots)}${select(k,r,'intent.target','目标来自',slots)}</div>${select(k,r,'intent.scope','作用范围',scopes)}`;}
function grammarDetail(g){
  const token=(type,id,content)=>'<span class="grammar-material" draggable="true" data-token-type="'+type+'" data-token-id="'+esc(id)+'">'+content+'</span>';
  const options=active(doc.words).map(w=>({...w,name:labelId(w.id)}));
  const gap=i=>'<span class="slot-gap" data-slot-drop="insert" data-index="'+i+'" aria-label="插入位置 '+(i+1)+'"></span>';
  const chips=g.slots.map((s,i)=>{
    const ids=classIds(doc,s),title=s.words.length===1?glyph(s.words[0])+' '+esc(labelId(s.words[0])):esc(ids.map(id=>name('wordClasses',id)).join(' / ')||'任意符号');
    return gap(i)+'<div class="structure-token" draggable="true" data-slot-index="'+i+'" data-slot-drop="replace" data-index="'+i+'"><strong>'+title+'</strong>'+(s.words.length>1?'<small>限定 '+s.words.length+' 个符号</small>':'')+'</div>';
  }).join('');
  const end='<span class="slot-end" data-slot-drop="insert" data-index="'+g.slots.length+'">'+(g.slots.length?'＋':'从下方拖入词类或符号')+'</span>';
  const advanced=g.slots.map((s,i)=>'<div class="slot"><strong>位置 '+(i+1)+'</strong>'+classes('grammars',g,'slots.'+i+'.classIds')+checks('grammars',g,'slots.'+i+'.words','限定符号（可选）',options)+'</div>').join('');
  return field('grammars',g,'name','语法名称')+'<div class="grammar-builder" data-grammar="'+esc(g.id)+'"><h3>句子结构</h3><p class="meta">拖到空隙插入，拖到卡片上替换；拖动已有卡片可换序。</p><div class="structure">'+chips+end+'</div>'+
    '<div class="slot-bin" data-slot-drop="remove" data-index="0">拖到这里移除</div>'+
    '<div class="palette"><span>词类</span>'+active(doc.wordClasses).map(c=>token('class',c.id,esc(c.name))).join('')+'</div><div class="palette"><span>符号</span>'+options.map(w=>token('word',w.id,glyph(w.id)+' '+esc(w.name))).join('')+'</div></div>'+
    (g.designDraft?button('enable-grammar','启用这条语法','data-id="'+g.id+'"','primary'):'')+
    '<h3 class="section-label">通用设计</h3>'+field('grammars',g,'behavior','共同含义与表现',{area:true})+field('grammars',g,'conditions','生效条件',{area:true,short:true})+field('grammars',g,'camera','共同镜头要求',{area:true,short:true})+field('grammars',g,'lifecycle','暂停、拆句与结束',{area:true,short:true})+
    '<details><summary>开发配置与适用限制</summary>'+intentForm('grammars',g,g.slots.length)+advanced+technical(state.snapshot.grammars.find(x=>x.key===g.key))+'</details>';
}
let grammarDrag=null,dragTarget=null;
function startGrammarDrag(el){
  const item=el.closest('[data-token-type],[data-slot-index]'),builder=item?.closest('[data-grammar]');
  if(!builder)return false;
  const type=item.dataset.tokenType,id=item.dataset.tokenId;
  const source=type?{slot:{classes:0,classIds:type==='class'?[id]:[],words:type==='word'?[id]:[]}}:{index:Number(item.dataset.slotIndex)};
  grammarDrag={builder,source};
  builder.classList.toggle('moving-slot',!type);
  return true;
}
function hoverGrammarDrop(el){
  const target=el?.closest('[data-slot-drop]');
  const valid=target&&grammarDrag?.builder.contains(target)?target:null;
  if(dragTarget!==valid){dragTarget?.classList.remove('drop-hover');valid?.classList.add('drop-hover');dragTarget=valid;}
  return valid;
}
function stopGrammarDrag(){
  grammarDrag?.builder.classList.remove('moving-slot');dragTarget?.classList.remove('drop-hover');
  grammarDrag=null;dragTarget=null;
}
function finishGrammarDrag(el){
  const target=hoverGrammarDrop(el),drag=grammarDrag;
  stopGrammarDrag();
  if(!target||!drag)return;
  const grammar=find('grammars',drag.builder.dataset.grammar);
  try{
    if(dropGrammarSlot(grammar,drag.source,target.dataset.slotDrop,Number(target.dataset.index))){mark();reconcile(doc);render();}
  }catch(error){notify(error.message);}
}
function exceptionDetail(e){return `${field('exceptions',e,'name','例外名称')}<div class="field"><span>精确词序</span>${wordSequence('exceptions',e,'words')}</div>${select('exceptions',e,'reject','处理方式',{'true':'此组合不成立','false':'改写此组合的动作'})}${!e.reject?intentForm('exceptions',e,e.words.length):''}${field('exceptions',e,'notes','原因与设计要求',{area:true})}`;}
function wordSequence(k,r,key){return `<div class="stack">${r[key].map((id,i)=>`<div class="row"><select aria-label="第 ${i+1} 个词" ${attrs(k,r,key+'.'+i)}>${active(doc.words).map(w=>`<option value="${w.id}" ${id===w.id?'selected':''}>${esc(labelId(w.id))}</option>`).join('')}</select>${r[key].length>2?button('sequence-remove','移除',`data-k="${k}" data-id="${r.id}" data-key="${key}" data-index="${i}"`):''}</div>`).join('')}${button('sequence-add','增加一个词',`data-k="${k}" data-id="${r.id}" data-key="${key}" ${r[key].length>=8?'disabled':''}`)}</div>`;}
function entityDetail(e){return `${field('entities',e,'name','对象名称')}${checks('entities',e,'properties','具备的性质',active(doc.properties))}${checks('entities',e,'outputs','可控制的输出端',active(doc.outputs))}${checks('entities',e,'spaces','所在或可影响的空间',active(doc.spaces))}${field('entities',e,'description','对象与控制关系',{area:true})}${field('entities',e,'conditions','资格、进度锁与特殊条件',{area:true,short:true})}${field('entities',e,'camera','局部镜头要求',{area:true,short:true})}${technical(e.source)}`;}
function actualRule(s){const keys=s.wordIds.map(id=>find('words',id)?.key);return state.snapshot.rules.find(r=>canonical(r.words)===canonical(keys));}
function intentText(i,s){if(!i)return '';return `${actions[i.action]} · ${i.subject>=0?name('words',s.wordIds[i.subject]):'无主语'}${i.target>=0?' · 目标：'+name('words',s.wordIds[i.target]):''}<br>${kinds[i.kind]} · ${scopes[i.scope]}`;}
function sentenceDetail(s){const parsed=resolve(doc,s.wordIds),current=actualRule(s),cs=doc.cases.filter(c=>c.sentenceId===s.id&&(showArchived||!c.archived)),source=[...doc.grammars,...doc.exceptions].find(x=>x.id===parsed.sourceId),relations=active(doc.relations).filter(r=>r.sentenceIds.includes(s.id));return `<section class="panel">${head('sentences',s)}${symbols(s.wordIds,true)}<div class="two"><div class="compare"><strong>设计侧解析</strong>${parsed.accepted?intentText(parsed.intent,s):esc(parsed.reason)}${source?`<br><a href="#language/${source.slots?'grammars':'exceptions'}/${source.id}">${esc(source.name)} · ${source.id}</a>`:''}</div><div class="compare"><strong>Unity 当前实现</strong>${current?`${esc(kinds[current.kind])} · ${esc(current.channel||'无独占通道')}<br>${badge(current.deferred?'兼容路径保留':'已有执行绑定','good')}<p class="meta">有绑定不代表所有情境已实现或验收。</p>`:badge('尚无执行绑定','warn')}</div></div>${s.deferred?'<div class="notice">mirror 按当前决策暂缓处理；这里保留现有记录。</div>':''}${source?.slots?`<details><summary>句型共同要求与实际演出参数</summary><div class="subtle"><p>共同表现：${esc(source.behavior||'待补充')}</p><p>生效条件：${esc(source.conditions||'待补充')}</p><p>暂停与结束：${esc(source.lifecycle||'待补充')}</p><p>镜头要求：${esc(source.camera||'待补充')}</p></div><p class="meta">Unity 当前共用演出时间：进入 ${state.snapshot.presentation.enter} 秒，停留 ${state.snapshot.presentation.hold} 秒，退出 ${state.snapshot.presentation.exit} 秒；黑边比例 ${state.snapshot.presentation.letterbox}。数值仅供核对。</p></details>`:''}${field('sentences',s,'notes','此句的补充说明',{area:true,short:true})}${requestBox('sentences',s)}<div class="panel-head section-label"><h3>具体情境 · ${cs.length}</h3>${button('add-case','添加情境',`data-id="${s.id}"`)}</div>${cs.map(c=>'<details><summary>'+esc(c.name)+'</summary>'+caseDetail(c)+'</details>').join('')||'<div class="empty">添加情境，描述不同对象、条件与预期表现。</div>'}<p class="section-label">相关句间关系</p>${relations.map(r=>`<p><a href="#sentences/relations/${r.id}">${esc(r.name)} · ${r.id}</a></p>`).join('')||'<p class="meta">尚未记录共存、冲突或先后关系。</p>'}${technical(current)}</section>`;}
function caseDetail(c){return `<article class="case"><div class="panel-head"><h3>${esc(c.name)} <small>${esc(labelId(c.id))}</small></h3>${button('archive',c.archived?'恢复情境':'归档情境',`data-k="cases" data-id="${c.id}"`,'quiet')}</div>${field('cases',c,'name','情境名称',{placeholder:'例如：白门尚未解锁时'})}${field('cases',c,'conditions','触发条件与操作顺序',{area:true,short:true,placeholder:'在哪个阶段、先做什么、对象处于什么状态？'})}<details><summary>关联对象与空间</summary>${checks('cases',c,'entityIds','涉及对象',active(doc.entities))}${checks('cases',c,'spaceIds','涉及空间',active(doc.spaces))}</details>${select('cases',c,'behaviorMode','此情境与共同要求的关系',{inherit:'完全继承',supplement:'补充说明',replace:'替换共同表现',forbid:'此情境不生效',unknown:'待确认'})}${field('cases',c,'behavior','表现要求',{area:true,placeholder:'玩家看到什么？哪些对象响应？没有可见变化时如何处理？'})}<div class="two">${select('cases',c,'feedbackKind','反馈形式',{'':'待明确',strong:'强反馈',weak:'弱反馈',invalid:'不成立／划线',conflict:'冲突／暗化',none:'无额外反馈'})}${field('cases',c,'feedback','反馈原因或补充')}</div>${field('cases',c,'camera','镜头要求',{area:true,short:true,placeholder:'保持当前视角，或说明关注对象、转向、跟随与结束恢复。'})}${field('cases',c,'lifecycle','暂停、拆句和结束时的表现',{area:true,short:true})}${field('cases',c,'expected','验收标准',{area:true,short:true,placeholder:'用可观察的结果描述通过条件。'})}${field('cases',c,'notes','备注',{area:true,short:true})}${requestBox('cases',c)}</article>`;}
function relationDetail(r){return `${field('relations',r,'name','关系名称')}${select('relations',r,'kind','关系类型',{coexist:'可以共存',conflict:'存在冲突',sequence:'先后影响',unknown:'待确认'})}${checks('relations',r,'sentenceIds','关联句子',active(doc.sentences).filter(s=>s.active).map(s=>({id:s.id,name:titleSentence(s)})))}${field('relations',r,'conditions','条件与顺序',{area:true,short:true})}${field('relations',r,'behavior','预期关系及理由',{area:true})}`;}
function requestBox(k,r){return batches.requestBox(k,r);}
function sourceLink(ref){if(ref.collection==='rules')return '#rules/rules/'+ref.id;if(ref.collection==='cases'){const c=find('cases',ref.id);return '#sentences/sentences/'+c?.sentenceId;}const page=ref.collection==='sentences'||ref.collection==='relations'?'sentences':['words','wordClasses','grammars','exceptions'].includes(ref.collection)?'language':'capabilities';return '#'+page+'/'+ref.collection+'/'+ref.id;}
function newRow(k){const common={id:tempId(),name:'新的'+labels[k],archived:false};const intent={action:'On',subject:1,target:-1,kind:'Command',scope:'CommandRegion'};const defaults={wordClasses:{description:''},words:{key:'new-'+doc.words.length,classes:2,meaning:'',symbol:''},grammars:{key:'new-'+doc.grammars.length,designDraft:true,slots:[],intent:{...intent,subject:-1},behavior:'',conditions:'',lifecycle:'',camera:''},exceptions:{key:'new-'+doc.exceptions.length,words:active(doc.words).slice(0,2).map(w=>w.id),reject:true,intent,notes:''},entities:{properties:[],outputs:[],spaces:[],description:'',conditions:''},properties:{description:''},outputs:{description:''},spaces:{description:''},relations:{kind:'unknown',sentenceIds:[],conditions:'',behavior:''}};return {...common,...defaults[k]};}
function openDialog(title,html){$('#dialog-content').innerHTML=`<div class="dialog-head"><h2>${title}</h2>${button('close-dialog','关闭')}</div>${html}`;if(!$('#dialog').open)$('#dialog').showModal();}
function download(data,filename){const u=URL.createObjectURL(new Blob([JSON.stringify(data,null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=u;a.download=filename;a.click();setTimeout(()=>URL.revokeObjectURL(u),1000);}
async function historyDialog(){const hs=await api('history');openDialog('版本历史',`<p class="muted">每次保存保留快照。恢复只更新设计；开发事项与验收历史继续保留。</p><label class="field thin"><span>给当前版本命名</span><input id="version-note" placeholder="例如：第一批情境要求"></label>${button('named-save','保存命名版本','','primary')}<div id="version-comparison"></div>${hs.map(h=>`<div class="history-item"><div class="row"><strong>${esc(h.note)}</strong><small>v${h.revision} · ${date(h.at)}</small></div><div class="actions">${button('compare-version','对照当前',`data-version="${h.id}"`)}${button('restore-version','恢复设计',`data-version="${h.id}"`)}</div></div>`).join('')}`);}
function compose(){openDialog('试组一句',`<p class="muted">选择词序，分别核对设计语法和当前执行绑定。</p><div class="two thin">${Array.from({length:8},(_,i)=>i).map(i=>`<label class="field"><span>第 ${i+1} 个词</span><select id="compose-${i}">${i>=2?'<option value="">不使用</option>':''}${active(doc.words).map(w=>`<option value="${w.id}">${esc(labelId(w.id))}</option>`).join('')}</select></label>`).join('')}</div>${button('check-compose','核对组合','','primary')}<div id="compose-result"></div>`);}
document.addEventListener('click',async event=>{const el=event.target.closest('[data-action]');if(!el)return;const a=el.dataset.action,k=el.dataset.k,id=el.dataset.id;try{
  if(levels.action(a,el)||await batches.act(a,el))return;
  if(a==='read-rule-source'){const source=await api('rule-source?id='+encodeURIComponent(el.dataset.source));openDialog(esc(source.heading),'<p class="meta">'+esc(source.file)+'</p><div class="source-prose">'+esc(source.text)+'</div>');return;}
  if(a==='enable-grammar'){const g=find('grammars',id);if(g.slots.length<2)throw Error('至少需要两个位置。');g.designDraft=false;reconcile(doc);mark();render();}
  else if(a==='space-object'){const obj=newRow('entities');obj.spaces=[id];doc.entities.push(obj);mark();location.hash='capabilities/entities/'+obj.id;}
  else if(a==='design-md')downloadText(brief(doc),'PuzzleApple-V5-设计文档.md');
  else if(a==='save')await save();
  else if(a==='close-dialog')$('#dialog').close();
  else if(a==='add'){const r=newRow(k);doc[k].push(r);mark();query='';location.hash=route().page+'/'+k+'/'+r.id;}
  else if(a==='archive'){const r=find(k,id);if(k==='chapters'&&!r.archived&&doc.spaces.some(s=>s.chapterId===id&&!s.archived))throw Error('请先移动或归档这个大关中的空间。');if(k==='wordClasses'&&!r.archived&&classUsers(doc,id).length)throw Error('请先调整使用该词类的符号和语法。');r.archived=!r.archived;reconcile(doc);mark();render();}
  else if(a==='upload')$('#upload-'+id).click();
  else if(a==='clear-symbol'){find('words',id).symbol='';mark();render();}
  else if(a==='sequence-add'||a==='sequence-remove'){const r=find(k,id),key=el.dataset.key;if(a==='sequence-add')r[key].push(active(doc.words)[0].id);else{const i=Number(el.dataset.index);r[key].splice(i,1);for(const f of ['subject','target']){if(r.intent[f]===i)r.intent[f]=-1;else if(r.intent[f]>i)r.intent[f]--;}}reconcile(doc);mark();render();}
  else if(a==='add-case'){doc.cases.push({id:tempId(),sentenceId:id,name:'新的情境',entityIds:[],spaceIds:[],conditions:'',behaviorMode:'supplement',behavior:'',feedbackKind:'',feedback:'',camera:'',lifecycle:'',expected:'',notes:'',archived:false});mark();render();}

  else if(a==='history')await historyDialog();
  else if(a==='named-save'){const note=$('#version-note').value.trim();if(!note)throw Error('请填写版本名称。');if(await save(note))await historyDialog();}
  else if(a==='compare-version'){const old=await api('version?id='+el.dataset.version);const changes=diff(old.doc,doc);$('#version-comparison').innerHTML=`<div class="subtle history-detail"><strong>所选版本与当前内容 · ${changes.length} 个条目有差异</strong>${changes.map(c=>`<div class="history-item"><p>${labels[c.collection]} · ${esc(c.name)} · ${c.id}</p><p class="field-changes">变化字段：${esc(c.fields.join('、'))}</p><details><summary>查看改前与改后</summary>${c.fields.map(f=>`<strong>${esc(f)}</strong><div class="two">${json(old.doc[c.collection].find(x=>x.id===c.id)?.[f])}${json(doc[c.collection].find(x=>x.id===c.id)?.[f])}</div>`).join('')}</details></div>`).join('')||'<p>没有设计差异。</p>'}</div>`;}
  else if(a==='restore-version'){if(dirty){notify('请先保存或导出当前草稿，再恢复历史设计。');return;}if(!confirm('恢复这个版本的设计？当前版本已有历史备份；开发与验收历史会保留。'))return;accept(await api('restore',{id:el.dataset.version}));$('#dialog').close();notify('已恢复设计，开发与验收历史已保留。');}
  else if(a==='export')openDialog('导出开发数据',`<p class="muted">使用稳定编号关联内容。设计主档用于备份；开发交接包另附待办事项、提交依据和实现快照。</p><div class="actions">${button('design-md','设计文档 · Markdown')}${button('export-draft','完整数据 · JSON')}${button('handoff','导出开发交接包','','primary')}</div>`);
  else if(a==='export-draft')download(doc,'PuzzleApple-V5-design.json');
  else if(a==='handoff'){if(dirty&&!(await save()))return;download(await api('handoff'),'PuzzleApple-V5-handoff.json');}
  else if(a==='refresh'){const fresh=await api('state');state.snapshot=fresh.snapshot;state.changedSources=fresh.changedSources;render();notify('已读取现有快照；重新导出 Unity 数据需由开发者调用导出工具。');}
  else if(a==='reload'){if(dirty&&!confirm('读取项目版本会替换当前浏览器草稿。已导出需要保留的草稿了吗？'))return;accept(await api('state'));}

  else if(a==='view-round'){const r=doc.rounds.find(x=>x.number===Number(el.dataset.number));openDialog('第 '+r.number+' 轮 · 冻结记录',`<p class="meta">${date(r.at)} · ${esc(r.note)}</p>${r.design.cases.filter(c=>!c.archived).map(c=>`<div class="history-item"><strong>${esc(c.name)} · ${c.id}</strong><p>${esc(c.expected||'未填写验收标准')}</p><p>${esc(({pass:'通过',todo:'待核对',rework:'需修改',skip:'跳过'})[r.items[c.id]?.verdict??'todo'])} · ${esc(r.items[c.id]?.comment??'')}</p><details><summary>当时的表现与镜头要求</summary>${json(c)}</details></div>`).join('')}<details><summary>当时的 Unity 时间参数</summary>${json(r.implementation.presentation)}</details>`);}
  else if(a==='compose')compose();
  else if(a==='check-compose'){const wordIds=Array.from({length:8},(_,i)=>i).map(i=>$('#compose-'+i).value).filter(Boolean),p=resolve(doc,wordIds),s=doc.sentences.find(s=>canonical(s.wordIds)===canonical(wordIds)),current=actualRule({wordIds});$('#compose-result').innerHTML=`<div class="compare">${symbols(wordIds,true)}<p>${p.accepted?'设计成立':esc(p.reason)}</p><p>${current?'Unity 有执行绑定':'Unity 尚无执行绑定'}</p>${s?`<a href="#sentences/sentences/${s.id}" data-action="close-dialog">打开句子详情</a>`:p.accepted?button('document-sentence','收录到具体句子',`data-words="${wordIds.join(',')}"`):''}</div>`;}
  else if(a==='document-sentence'){const wordIds=el.dataset.words.split(',');if(!resolve(doc,wordIds).accepted)throw Error('请先核对语法。');let sentence=doc.sentences.find(s=>canonical(s.wordIds)===canonical(wordIds));if(!sentence){sentence={id:tempId(),wordIds,archived:false,active:true,notes:''};doc.sentences.push(sentence);}mark();$('#dialog').close();location.hash='sentences/sentences/'+sentence.id;}
}catch(e){notify(e.message);}});
function updateInput(el){if(batches.edit(el))return;if(el.dataset.requestText||el.dataset.requestVersion){const id=el.dataset.requestText||el.dataset.requestVersion;doc.requestDrafts??={};doc.requestDrafts[id]??={text:'',version:''};doc.requestDrafts[id][el.dataset.requestText?'text':'version']=el.value;mark();return;}if(el.dataset.review){const id=el.dataset.review;const item=doc.review.items[id]??{verdict:'todo',comment:''};item[el.dataset.reviewField]=el.value;item.basis=sourceBasis(doc,{collection:'cases',id});doc.review.items[id]=item;mark();return;}if(!el.dataset.field)return;const r=find(el.dataset.k,el.dataset.id);if(!r)return;const key=el.dataset.field;let v=el.value;if(el.dataset.classId){
    const owner=key==='classIds'?r:get(r,key.slice(0,-9)),ids=new Set(classIds(doc,owner));
    el.checked?ids.add(el.dataset.classId):ids.delete(el.dataset.classId);v=[...ids];owner.classes=doc.wordClasses.filter(c=>ids.has(c.id)).reduce((n,c)=>n|(c.bit||0),0);
  }else if(el.dataset.bit){const bit=Number(el.dataset.bit);v=el.checked?get(r,key)|bit:get(r,key)&~bit;}else if(el.dataset.ref){const ids=new Set(get(r,key));el.checked?ids.add(el.dataset.ref):ids.delete(el.dataset.ref);v=[...ids];}else if(['intent.subject','intent.target'].includes(key))v=Number(v);else if(key==='reject')v=v==='true';set(r,key,v);mark();}
document.addEventListener('input',event=>{const el=event.target;if(el.id==='search'){const pos=el.selectionStart;query=el.value;render();$('#search').focus();$('#search').setSelectionRange(pos,pos);return;}if(el.tagName!=='SELECT'&&el.type!=='checkbox')updateInput(el);});
document.addEventListener('change',async event=>{const el=event.target;try{if(levels.change(el))return;if(el.id==='show-archived'){showArchived=el.checked;render();return;}if(el.id==='filter'){filter=el.value;render();return;}if(el.dataset.upload){const f=el.files[0];if(!f)return;if(f.size>1024*1024||!['image/png','image/jpeg','image/webp'].includes(f.type))throw Error('请使用不超过 1 MB 的 PNG、JPEG 或 WebP。');const reader=new FileReader();reader.onload=()=>{find('words',el.dataset.upload).symbol=reader.result;mark();render();};reader.readAsDataURL(f);return;}if(el.tagName==='SELECT'||el.type==='checkbox'){updateInput(el);if(el.dataset.field){reconcile(doc);render();}}}catch(e){notify(e.message);}});
document.addEventListener('dragstart',e=>{
  const space=e.target.closest('[data-space-drag]');
  if(space){e.dataTransfer.setData('application/x-puzzle-space',space.dataset.spaceDrag);return;}
  if(startGrammarDrag(e.target)){e.dataTransfer.effectAllowed=Number.isInteger(grammarDrag.source.index)?'move':'copy';e.dataTransfer.setData('text/plain','PuzzleApple 语法');}
});
document.addEventListener('dragover',e=>{
  if(grammarDrag){if(hoverGrammarDrop(e.target))e.preventDefault();}
  else if(e.target.closest('[data-space-card]'))e.preventDefault();
});
document.addEventListener('drop',e=>{
  if(grammarDrag){e.preventDefault();finishGrammarDrag(e.target);return;}
  const card=e.target.closest('[data-space-card]');
  if(card){e.preventDefault();levels.drop(e.dataTransfer.getData('application/x-puzzle-space'),card.dataset.spaceCard);}
});
document.addEventListener('dragend',stopGrammarDrag);
let touchDrag=null;
document.addEventListener('pointerdown',e=>{
  if(e.pointerType==='mouse'||!e.isPrimary||!e.target.closest('[data-token-type],[data-slot-index]'))return;
  touchDrag={id:e.pointerId,el:e.target,x:e.clientX,y:e.clientY};
  e.target.setPointerCapture(e.pointerId);
});
document.addEventListener('pointermove',e=>{
  if(touchDrag?.id!==e.pointerId)return;
  if(!grammarDrag&&Math.hypot(e.clientX-touchDrag.x,e.clientY-touchDrag.y)>6)startGrammarDrag(touchDrag.el);
  if(grammarDrag)hoverGrammarDrop(document.elementFromPoint(e.clientX,e.clientY));
});
document.addEventListener('pointerup',e=>{
  if(touchDrag?.id!==e.pointerId)return;
  touchDrag=null;if(grammarDrag)finishGrammarDrag(document.elementFromPoint(e.clientX,e.clientY));
});
document.addEventListener('pointercancel',e=>{if(touchDrag?.id===e.pointerId){touchDrag=null;stopGrammarDrag();}});
window.addEventListener('hashchange',()=>{query='';filter='';render();});
window.addEventListener('beforeunload',e=>{if(dirty){try{localStorage.setItem(draftKey,JSON.stringify({baseToken:state.token,doc}));}catch{}e.preventDefault();e.returnValue='';}});
window.addEventListener('storage',e=>{if(e.key===draftKey&&dirty)notify('另一个窗口的浏览器草稿有变化。当前内容仍保留；保存时会核对项目版本。');});
async function offerDraft(draft){
  let restored=null,reason='',merged=false;
  const same=draft.baseToken===state.token;
  if(same)restored=upgradeDesign(clone(draft.doc));
  else{
    try{
      const result=await api('recover-classes',{doc:draft.doc,draftToken:draft.baseToken});
      restored=result.doc;merged=true;$('#banner').textContent='';
    }catch(error){reason=error.message;$('#banner').textContent='';}
  }
  const message=merged?'找到了尚未保存的词类修改，可与当前项目一起保存。':restored?'草稿基于当前项目版本，可以继续编辑。':reason;
  openDialog('发现浏览器草稿','<p>'+esc(message)+'</p><div class="actions">'+
    '<button id="discard-draft">使用项目版本</button><button id="download-draft">导出旧草稿</button>'+
    (restored?'<button id="resume-draft" class="primary">'+(merged?'恢复并保存词类':'继续草稿')+'</button>':'')+'</div>');
  $('#discard-draft').onclick=()=>{localStorage.removeItem(draftKey);$('#dialog').close();};
  $('#download-draft').onclick=()=>download(draft.doc,'PuzzleApple-V5-unsaved.json');
  if(restored)$('#resume-draft').onclick=async()=>{
    doc=restored;mark();$('#dialog').close();render();
    if(merged)try{await save('恢复并保存词类');}catch(error){notify(error.message);}
  };
}
try{
  state=await api('state');doc=clone(state.doc);const saved=localStorage.getItem(draftKey);render();
  if(saved)await offerDraft(JSON.parse(saved));
}catch(e){$('#workspace').innerHTML='<div class="panel error">工作台未能载入：'+esc(e.message)+'</div>';}
