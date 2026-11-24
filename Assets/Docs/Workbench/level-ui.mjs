import {tempId} from './model.mjs';
import {chapterSpaces,edgeKinds,endNames,moveSpace,reorderSpace,spaceEdges,spaceKinds} from './levels.mjs';

export function levelUI({data,esc,button,field,select,requestBox,technical,openDialog,mark,render,showArchived}){
  const find=(k,id)=>data()[k].find(x=>x.id===id);
  const ref=id=>esc(id?.startsWith('draft-')?'待分配编号':id);
  const href=(k,id)=>'#capabilities/'+k+(id?'/'+id:'');
  const pageLink=(k,row)=>'<a href="'+href(k,esc(row.id))+'"><strong>'+ref(row.id)+'</strong> '+esc(row.name)+'</a>';
  const visible=r=>showArchived()||!r.archived;
  const dataId=id=>'data-id="'+esc(id)+'"';
  const newButton=(kind,id='')=>button('level-new',kind==='chapters'?'新增大关':'新增空间','data-kind="'+kind+'" '+dataId(id),'primary');
  const archives=()=>'<label class="check"><input type="checkbox" id="show-archived" '+(showArchived()?'checked':'')+'>含归档</label>';
  const archive=(k,r)=>button('archive',r.archived?'恢复条目':'归档','data-k="'+k+'" '+dataId(r.id),'quiet');
  const muted=r=>r.archived?'<small>已归档</small>':'';
  const back='<a href="#capabilities/chapters">全部大关</a>';
  function index(){
    const d=data();
    return '<div class="toolbar"><h2>大关</h2><div class="actions">'+archives()+newButton('chapters')+'</div></div><div class="chapter-index">'+
      d.chapters.filter(visible).map(c=>'<a class="chapter-row" href="'+href('chapters',esc(c.id))+'"><strong>'+ref(c.id)+'</strong><span>'+esc(c.name)+'</span><small>'+chapterSpaces(d,c,showArchived()).length+' 个空间 '+muted(c)+'</small><span aria-hidden="true">→</span></a>').join('')+'</div>';
  }
  function spaceCard(s,chapter,index,rows){
    const d=data();
    const edges=spaceEdges(d,s.id).filter(e=>e.kind!=='open'||e.progressLock);
    const membrane=edges.some(e=>e.kind==='membrane'&&e.to===s.id);
    const chips=edges.map(e=>{
      const side=s.id===e.from?e.fromEnd:e.toEnd;
      const tags=[endNames[side],e.name||edgeKinds[e.kind]];
      if(e.progressLock)tags.push('进度锁');
      return '<span class="edge-chip">'+tags.map(esc).join(' · ')+'</span>';
    }).join('');
    const attr=dataId(s.id)+' data-chapter="'+esc(chapter.id)+'"';
    return '<article class="space-card" data-space-card="'+esc(s.id)+'"><a class="space-open" href="'+href('spaces',esc(s.id))+'"><div class="space-type">'+esc(spaceKinds[s.kind])+(membrane?' · 薄膜空间':'')+muted(s)+'</div><h3>'+ref(s.id)+'</h3><p>'+esc(s.name)+'</p><div class="edge-chips">'+chips+'</div></a>'+
      '<div class="card-order"><button type="button" draggable="true" data-space-drag="'+esc(s.id)+'" aria-label="拖动排列 '+ref(s.id)+'" title="拖动排列">⠿</button>'+
      button('space-order','←',attr+' data-target="'+esc(rows[index-1]?.id??'')+'" aria-label="前移 '+ref(s.id)+'" '+(index?'':'disabled'))+
      button('space-order','→',attr+' data-target="'+esc(rows[index+1]?.id??'')+'" aria-label="后移 '+ref(s.id)+'" '+(index===rows.length-1?'disabled':''))+'</div></article>';
  }
  function chapter(c){
    const rows=chapterSpaces(data(),c,showArchived());
    return '<div class="crumb">'+back+' / '+ref(c.id)+'</div><div class="toolbar"><div class="level-heading"><h2>'+ref(c.id)+'</h2><span>'+esc(c.name)+'</span>'+muted(c)+'</div><div class="actions">'+archives()+(c.archived?'':newButton('spaces',c.id))+'</div></div>'+
      '<div class="space-grid">'+rows.map((s,i)=>spaceCard(s,c,i,rows)).join('')+'</div>'+
      (rows.length?'':'<p class="empty">这个大关还没有空间。</p>')+
      '<details class="panel level-settings"><summary>编辑大关</summary>'+field('chapters',c,'name','大关名称')+field('chapters',c,'description','关卡设计',{area:true})+archive('chapters',c)+'</details>'+requestBox('chapters',c);
  }
  function edgeTitle(e,s){
    const other=find('spaces',e.from===s.id?e.to:e.from);
    return '<div class="edge-row">'+pageLink('boundaries',e)+'<small>'+esc(endNames[e.from===s.id?e.fromEnd:e.toEnd])+' · '+esc(edgeKinds[e.kind])+(e.progressLock?' · 进度锁':'')+muted(e)+'</small><span>连接 '+pageLink('spaces',other)+'</span></div>';
  }
  function space(s){
    const d=data(),c=find('chapters',s.chapterId),objects=d.entities.filter(e=>visible(e)&&e.spaces.includes(s.id));
    const edges=spaceEdges(d,s.id,showArchived());
    const options=d.chapters.filter(row=>!row.archived||row.id===s.chapterId).map(row=>'<option value="'+esc(row.id)+'" '+(row===c?'selected':'')+'>'+ref(row.id)+' · '+esc(row.name)+'</option>').join('');
    return '<div class="crumb">'+back+' / '+pageLink('chapters',c)+' / '+ref(s.id)+'</div><section class="panel"><div class="panel-head"><div class="level-heading"><h2>'+ref(s.id)+'</h2><span>'+esc(s.name)+'</span>'+muted(s)+'</div>'+archive('spaces',s)+'</div>'+
      '<div class="two">'+field('spaces',s,'name','空间名称')+select('spaces',s,'kind','空间类型',spaceKinds)+'</div>'+
      field('spaces',s,'description','空间设计与流程',{area:true})+
      '<div class="panel-head section-label"><h3>门与边界</h3>'+button('edge-new','添加边界',dataId(s.id))+'</div>'+
      (edges.map(e=>edgeTitle(e,s)).join('')||'<p class="muted">尚未登记边界。</p>')+
      '<div class="panel-head section-label"><h3>本空间对象</h3>'+button('space-object','添加对象',dataId(s.id))+'</div>'+
      (objects.map(e=>'<div class="rule-link">'+pageLink('entities',e)+'<small>'+esc(e.properties.map(id=>find('properties',id)?.name).join('、'))+'</small></div>').join('')||'<p class="muted">尚未登记对象。</p>')+
      '<details><summary>所属大关与开发关联</summary><label class="field"><span>所属大关</span><select aria-label="所属大关" data-space-parent="'+esc(s.id)+'">'+options+'</select></label>'+esc(s.designSource||'')+technical(s.source)+'</details>'+requestBox('spaces',s)+'</section>';
  }
  function boundary(e){
    const d=data(),from=find('spaces',e.from),to=find('spaces',e.to),choices=Object.fromEntries(d.spaces.filter(s=>!s.archived||s===from||s===to).map(s=>[s.id,(s.id.startsWith('draft-')?'新空间':s.id)+' · '+s.name]));
    const directions=(key,label)=>select('boundaries',e,key,label,Object.fromEntries(Object.entries(choices).filter(([id])=>id!==e[key==='from'?'to':'from'])));
    return '<div class="crumb">'+back+' / '+pageLink('chapters',find('chapters',from.chapterId))+' / '+pageLink('spaces',from)+'</div><section class="panel"><div class="panel-head"><div class="level-heading"><h2>'+ref(e.id)+'</h2><span>'+esc(e.name)+'</span>'+muted(e)+'</div>'+archive('boundaries',e)+'</div>'+
      field('boundaries',e,'name','边界名称')+'<div class="two">'+select('boundaries',e,'kind','边界类型',edgeKinds)+'<label class="check"><input type="checkbox" data-edge-lock="'+esc(e.id)+'" '+(e.progressLock?'checked':'')+'>进度锁</label></div>'+
      '<div class="two"><div>'+directions('from','起端空间')+select('boundaries',e,'fromEnd','起端的位置',endNames)+'</div><div>'+directions('to',e.kind==='membrane'?'内侧空间':'另一端空间')+select('boundaries',e,'toEnd','另一端的位置',endNames)+'</div></div>'+
      '<div class="actions">'+pageLink('spaces',from)+' ↔ '+pageLink('spaces',to)+'</div>'+requestBox('boundaries',e)+'</section>';
  }
  function page(r){
    if(!r.id)return index();
    const row=find(r.collection,r.id);
    if(!row)return '<p class="empty">条目不存在。'+back+'</p>';
    if(r.collection==='chapters')return chapter(row);
    return r.collection==='spaces'?space(row):boundary(row);
  }
  const inputName='<label class="field"><span>名称</span><input id="level-name" autofocus></label>';
  function action(a,el){
    const {id,kind}=el.dataset,d=data();
    if(a==='level-new'){
      openDialog(kind==='chapters'?'新增大关':'新增空间',inputName+(kind==='spaces'?'<label class="field"><span>类型</span><select id="level-kind" aria-label="类型">'+Object.entries(spaceKinds).map(([k,v])=>'<option value="'+k+'">'+v+'</option>').join('')+'</select></label>':'')+button('level-create','创建','data-kind="'+kind+'" '+dataId(id),'primary'));
    }else if(a==='level-create'){
      const row={id:tempId(),archived:false,name:document.querySelector('#level-name').value.trim(),description:''};
      if(kind==='chapters')row.spaceOrder=[];
      else {row.kind=document.querySelector('#level-kind').value;moveSpace(d,row,find('chapters',id));}
      d[kind].push(row);mark();document.querySelector('#dialog').close();location.hash=href(kind,row.id);
    }else if(a==='edge-new'){
      const candidates=d.spaces.filter(s=>!s.archived&&s.id!==id);
      if(!candidates.length)throw Error('请先创建另一个可连接的空间。');
      openDialog('添加边界',inputName+'<label class="field"><span>连接到空间</span><select id="edge-peer" aria-label="连接到空间">'+candidates.map(s=>'<option value="'+esc(s.id)+'">'+ref(s.id)+' · '+esc(s.name)+'</option>').join('')+'</select></label>'+button('edge-create','创建',dataId(id),'primary'));
    }else if(a==='edge-create'){
      const row={id:tempId(),archived:false,name:document.querySelector('#level-name').value.trim(),from:id,to:document.querySelector('#edge-peer').value,kind:'open',progressLock:false,fromEnd:'exit',toEnd:'entrance'};
      d.boundaries.push(row);mark();document.querySelector('#dialog').close();location.hash=href('boundaries',row.id);
    }else if(a==='space-order'){
      if(reorderSpace(d,find('chapters',el.dataset.chapter),id,el.dataset.target)){mark();render();}
    }else return false;
    return true;
  }
  function change(el){
    if(el.dataset.spaceParent)moveSpace(data(),find('spaces',el.dataset.spaceParent),find('chapters',el.value));
    else if(el.dataset.edgeLock)find('boundaries',el.dataset.edgeLock).progressLock=el.checked;
    else return false;
    mark();render();return true;
  }
  function drop(source,target){
    const s=find('spaces',source),other=find('spaces',target);
    if(s&&other&&other.chapterId===s.chapterId&&reorderSpace(data(),find('chapters',s.chapterId),source,target)){mark();render();}
  }
  return {page,action,change,drop};
}
