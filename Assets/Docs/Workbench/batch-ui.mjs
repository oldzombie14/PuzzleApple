import {active,labels} from './model.mjs';
import {batchStates,brief,changedFields,designKeys,displayName,fieldName,makeBatch,makeItem,outdated,refKey,showValue} from './workflow.mjs';

export function batchUI({documentData,stateData,esc,button,badge,date,openDialog,render,mark,commit,sourceLink,downloadText}){
  const doc=documentData,state=stateData;
  const $=selector=>document.querySelector(selector);
  const current=id=>doc().batches.find(b=>b.id===id);
  const attr=(b,i)=>'data-batch="'+esc(b.id)+'"'+(i?' data-item="'+esc(i.id)+'"':'');
  const actionButton=(action,title,b,i,cls='')=>button(action,title,attr(b,i),cls);
  const head=(b)=>b.name||'未命名批次';
  const verdicts={todo:'待核对',pass:'通过',rework:'需修改'};
  function input(b,i,key,title,short=false){
    return '<label class="field"><span>'+title+'</span><textarea aria-label="'+esc(title)+'" '+attr(b,i)+' data-item-field="'+key+'" class="'+(short?'short':'')+'">'+esc(i[key])+'</textarea></label>';
  }
  function linkTitle(ref){
    const row=doc()[ref.collection]?.find(r=>r.id===ref.id);
    return labels[ref.collection]+' · '+(row?displayName(ref.collection,row):ref.id);
  }
  function referenceBlock(b,i){
    const refs=i.refs.map(ref=>'<a class="chip" href="'+esc(sourceLink(ref))+'">'+esc(linkTitle(ref))+'</a>').join('');
    return '<div class="actions">'+refs+(b.status==='draft'?actionButton('choose-refs','选择关联设计',b,i):'')+'</div>';
  }
  function basisDetail(item){
    if(!item.basis?.length)return '';
    return '<details><summary>提交时的设计依据</summary>'+item.basis.map(({ref,before,after})=>{
      const fields=changedFields(before,after.record);
      return '<h4>'+esc(linkTitle(ref))+'</h4>'+fields.map(k=>'<p><strong>'+esc(fieldName(k))+'</strong> '+esc(showValue(before?.[k]))+' → '+esc(showValue(after.record[k]))+'</p>').join('')+'<details><summary>完整依据及依赖</summary><pre>'+esc(showValue(after))+'</pre></details>';
    }).join('')+'</details>';
  }
  function itemView(b,i,index,review){
    let body='';
    if(b.status==='draft'){
      body=input(b,i,'text','开发要求')+input(b,i,'expected','验收条件（可选）',true)+referenceBlock(b,i);
    }else{
      body='<p class="prose">'+esc(i.text)+'</p>'+(i.expected?'<p class="thin">验收条件 · '+esc(i.expected)+'</p>':'')+referenceBlock(b,i)+basisDetail(i);
      if(i.verdict!=='pass'&&outdated(doc(),i))body+='<p class="notice">关联设计已有更新，可重新修订提交依据。</p>';
      if(review&&i.verdict!=='pass'&&b.status==='review'){
        body+='<p class="prose"><strong>实际改动</strong>　'+esc(i.implementation)+'</p><p class="prose"><strong>验证结果</strong>　'+esc(i.validation)+'</p>'+input(b,i,'feedback','验收反馈',true)+'<label class="field"><span>本次结论</span><select data-verdict="'+esc(i.id)+'"><option value="">本次未核对</option><option value="pass">通过</option><option value="rework">需修改</option></select></label>';
      }else if(!review&&['queued','working','rework'].includes(b.status)&&i.verdict!=='pass'){
        body+=input(b,i,'implementation','实际改动',true)+input(b,i,'validation','验证结果',true)+(i.feedback?'<p class="prose">验收反馈 · '+esc(i.feedback)+'</p>':'');
      }else{
        for(const [k,t] of [['implementation','实际改动'],['validation','验证结果'],['feedback','验收反馈']])if(i[k])body+='<p class="prose"><strong>'+t+'</strong>　'+esc(i[k])+'</p>';
      }
    }
    return '<article class="case"><div class="panel-head"><h3>需求 '+(index+1)+'</h3>'+(b.status==='draft'?actionButton('delete-item','移除',b,i):badge(verdicts[i.verdict]))+'</div>'+body+'</article>';
  }
  function panel(b,review){
    const drafting=b.status==='draft';
    let buttons='';
    if(review){
      if(b.status==='review')buttons=actionButton('decide','记录验收结论',b,null,'primary');
      buttons+=' <a href="#development/batches/'+b.id+'">打开开发批次</a>';
    }else{
      if(drafting)buttons=actionButton('add-item','添加需求',b)+actionButton('submit-batch','提交这批需求',b,null,'primary');
      if(['queued','working','rework'].includes(b.status))buttons+=actionButton('progress-dialog','记录阶段进展',b)+actionButton('send-review','提交验收',b,null,'primary');
      if(b.status==='review')buttons+='<a class="chip" href="#review/batches/'+b.id+'">前往验收 →</a>';
      if(!['draft','passed'].includes(b.status))buttons+=actionButton('revise','修订需求',b);
      if(['queued','working','review','rework'].includes(b.status))buttons+=actionButton('defer','暂缓',b);
    }
    const legacy=b.legacyId&&doc().requests.find(r=>r.id===b.legacyId);
    return '<section class="panel"><div class="panel-head"><div><small>'+esc(b.id.startsWith('draft-')?'新批次':b.id)+'</small><h2>'+esc(head(b))+'</h2></div>'+badge(batchStates[b.status])+'</div>'+
      (drafting?'<label class="field"><span>批次名称</span><input data-k="batches" data-field="name" data-id="'+b.id+'" value="'+esc(b.name)+'"></label>':'')+
      b.items.map((i,n)=>itemView(b,i,n,review)).join('')+'<div class="actions batch-actions">'+buttons+'</div>'+
      '<details><summary>开发交接文档</summary>'+actionButton('batch-md','导出 Markdown',b)+'<pre>'+esc(brief(doc(),b))+'</pre></details>'+
      (legacy?'<details><summary>迁移前的事项历史</summary><pre>'+esc(JSON.stringify(legacy.events,null,2))+'</pre></details>':'')+'</section>';
  }
  function page(r,review=false){
    let rows=active(doc().batches).slice().reverse();
    if(review)rows=rows.filter(b=>['review','passed','rework'].includes(b.status)||b.items.some(i=>i.verdict!=='todo'));
    const selected=rows.find(b=>b.id===r.id||b.legacyId===r.id)??rows[0];
    const left=rows.map(b=>'<a class="record '+(b===selected?'active':'')+'" href="#'+(review?'review':'development')+'/batches/'+b.id+'"><div class="record-title">'+esc(head(b))+'</div><small>'+esc(batchStates[b.status])+' · '+b.items.length+' 项需求</small></a>').join('');
    const old=review&&(doc().rounds.length||Object.keys(doc().review.items).length)?'<details><summary>旧版情境验收记录</summary>'+doc().rounds.map(x=>button('view-round','第 '+x.number+' 轮','data-number="'+x.number+'"')).join('')+'<pre>'+esc(JSON.stringify(doc().review,null,2))+'</pre></details>':'';
    return '<div class="toolbar"><p class="muted">'+(review?'按提交批次核对本次交付。':'把本次要一起开发的要求放入同一批次。')+'</p>'+(review?'':button('new-batch','新建批次','','primary'))+'</div>'+
      (selected?'<div class="split"><div class="record-list">'+left+'</div>'+panel(selected,review)+'</div>':'<section class="panel empty">'+(review?'暂无可验收的批次。':'尚无开发批次。可以先写一条文字需求。')+'</section>')+old;
  }
  function entryLine(e){
    return '<li><button class="timeline-link" data-action="view-entry" data-id="'+esc(e.id)+'"><span>'+esc(e.summary)+'</span><small>'+esc(date(e.at))+' · '+esc(e.author)+'</small></button></li>';
  }
  function timeline(){
    const entries=doc().timeline.slice().reverse();
    return '<section class="panel"><div class="panel-head"><h2>开发时间线</h2>'+button('session-dialog','归档已完成会话')+'</div>'+
      (entries.length?'<ol class="timeline">'+entries.slice(0,4).map(entryLine).join('')+'</ol>':'<p class="muted">尚无已发布的开发记录。</p>')+
      (entries.length>4?'<details><summary>更早的 '+(entries.length-4)+' 条</summary><ol class="timeline">'+entries.slice(4).map(entryLine).join('')+'</ol></details>':'')+'</section>';
  }
  function dialogField(id,title,multiline=false){
    return '<label class="field"><span>'+title+'</span>'+(multiline?'<textarea id="'+id+'"></textarea>':'<input id="'+id+'">')+'</label>';
  }
  function requestBox(k,r){
    const batches=doc().batches.filter(b=>!b.archived&&b.items.some(i=>i.refs.some(ref=>ref.collection===k&&ref.id===r.id)));
    return '<div class="request-box actions">'+button('attach-design','加入开发批次','data-k="'+esc(k)+'" data-id="'+esc(r.id)+'"')+batches.map(b=>'<a href="#development/batches/'+b.id+'">'+esc(head(b))+'</a>').join('')+'</div>';
  }
  function chooseRefs(b,i){
    let groups='';
    for(const k of designKeys){
      const refs=doc()[k].filter(r=>!r.archived||i.refs.some(ref=>ref.id===r.id)).map(r=>{
        const key=refKey({collection:k,id:r.id}),changed=changedFields(state().designBase[key],r);
        const checked=i.refs.some(ref=>ref.collection===k&&ref.id===r.id);
        return {changed,html:'<label class="check ref-choice"><input type="checkbox" data-choice="'+esc(key)+'" '+(checked?'checked':'')+'><span>'+esc(displayName(k,r))+'</span>'+(changed.length?'<small>已改 · '+esc(changed.map(fieldName).join('、'))+'</small>':'')+'</label>'};
      });
      groups+='<details '+(refs.some(x=>x.changed.length)?'open':'')+'><summary>'+labels[k]+' · '+refs.length+'</summary>'+refs.sort((a,b)=>Number(!!b.changed.length)-Number(!!a.changed.length)).map(x=>x.html).join('')+'</details>';
    }
    openDialog('关联本项需求的设计',groups+actionButton('apply-refs','确定关联',b,i,'primary'));
  }
  function viewEntry(id){
    const e=doc().timeline.find(x=>x.id===id);
    const fields=e.items?.map((i,n)=>'<article class="history-item"><h3>需求 '+(n+1)+' · '+esc(i.text)+'</h3>'+[i.expected,i.implementation,i.validation,i.feedback].filter(Boolean).map(x=>'<p class="prose">'+esc(x)+'</p>').join('')+basisDetail(i)+'</article>').join('')??'';
    openDialog(esc(e.summary),'<p class="meta">'+esc(date(e.at))+' · '+esc(e.author)+(e.status?' · '+esc(batchStates[e.status]):'')+'</p><p class="prose">'+esc(e.detail)+'</p>'+fields+(e.batchId?'<a data-action="close-dialog" href="#development/batches/'+e.batchId+'">打开关联批次</a>':''));
  }
  async function act(a,el){
    const b=current(el.dataset.batch),i=b?.items.find(x=>x.id===el.dataset.item);
    const finish=async(command)=>{await commit(command);if($('#dialog').open)$('#dialog').close();};
    if(a==='new-batch'){const batch=makeBatch();doc().batches.push(batch);mark();location.hash='development/batches/'+batch.id;return true;}
    if(a==='attach-design'){
      const ref={collection:el.dataset.k,id:el.dataset.id},drafts=doc().batches.filter(x=>x.status==='draft');
      const old=doc().requestDrafts?.[ref.id];
      openDialog('加入开发批次','<label class="field"><span>目标批次</span><select id="target-batch"><option value="">新建批次</option>'+drafts.map(x=>'<option value="'+x.id+'">'+esc(head(x))+'</option>').join('')+'</select></label><label class="field"><span>本项要求</span><textarea id="attach-text">'+esc(old?.text||'')+'</textarea></label>'+button('attach-confirm','加入草稿','data-k="'+ref.collection+'" data-id="'+ref.id+'"','primary'));return true;
    }
    if(a==='attach-confirm'){
      const t=$('#attach-text').value.trim();if(!t)throw Error('请填写本项开发要求。');
      const selected=current($('#target-batch').value),batch=selected??makeBatch();
      if(!selected)doc().batches.push(batch);
      let item=batch.items.find(x=>!x.text&&!x.refs.length);
      if(!item){item=makeItem();batch.items.push(item);}
      item.text=t;item.refs=[{collection:el.dataset.k,id:el.dataset.id}];
      if(doc().requestDrafts)delete doc().requestDrafts[el.dataset.id];
      mark();$('#dialog').close();location.hash='development/batches/'+batch.id;return true;
    }
    if(a==='session-dialog'){
      openDialog('归档已完成会话',dialogField('archive-name','本批改动摘要')+dialogField('archive-detail','实际改动、验证与待办',true)+dialogField('archive-author','记录人（可选）')+dialogField('session-key','会话编号或链接（可选）')+'<label class="field"><span>关联需求批次（可选）</span><select id="archive-batch"><option value="">独立会话</option>'+doc().batches.map(x=>'<option value="'+x.id+'">'+esc(head(x))+'</option>').join('')+'</select></label>'+button('session-publish','标记会话完成并归档','','primary'));return true;
    }
    if(a==='session-publish'){await finish({action:'session',summary:$('#archive-name').value,detail:$('#archive-detail').value,author:$('#archive-author').value,sessionKey:$('#session-key').value,batchId:$('#archive-batch').value});return true;}
    if(a==='view-entry'){viewEntry(el.dataset.id);return true;}
    if(!b)return false;
    if(a==='add-item')b.items.push(makeItem());
    else if(a==='delete-item'){if(!i.text||confirm('移除这条草稿需求？'))b.items=b.items.filter(x=>x!==i);}
    else if(a==='choose-refs'){chooseRefs(b,i);return true;}
    else if(a==='apply-refs'){i.refs=[...document.querySelectorAll('[data-choice]:checked')].map(e=>{const [collection,id]=e.dataset.choice.split('/');return {collection,id};});$('#dialog').close();}
    else if(a==='progress-dialog'){openDialog('记录阶段进展',dialogField('progress-summary','本次完成了什么')+dialogField('progress-detail','具体改动与待办（可选）',true)+dialogField('progress-author','记录人（可选）')+actionButton('progress-publish','发布进展',b,null,'primary'));return true;}
    else if(a==='progress-publish'){await finish({action:'progress',batchId:b.id,summary:$('#progress-summary').value,detail:$('#progress-detail').value,author:$('#progress-author').value});return true;}
    else if(a==='batch-md'){downloadText(brief(doc(),b),b.id+'-开发交接.md');return true;}
    else if(['submit-batch','send-review','decide','revise','defer'].includes(a)){
      const command={action:({'submit-batch':'submit','send-review':'review',decide:'decide',revise:'revise',defer:'defer'})[a],batchId:b.id};
      if(a==='decide')command.decisions=Object.fromEntries([...document.querySelectorAll('[data-verdict]')].map(e=>[e.dataset.verdict,e.value]));
      await finish(command);return true;
    }else return false;
    mark();render();return true;
  }
  function edit(el){
    if(!el.dataset.itemField)return false;
    const i=current(el.dataset.batch)?.items.find(x=>x.id===el.dataset.item);
    if(i){i[el.dataset.itemField]=el.value;mark();}return true;
  }
  return {page,timeline,requestBox,act,edit};
}
