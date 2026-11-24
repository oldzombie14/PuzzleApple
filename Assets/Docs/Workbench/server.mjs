import {ruleSources,readSourceSection} from './principles.mjs';
import {upgradeDesign} from './design.mjs';
import {baseline,publish,brief} from './workflow.mjs';
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {seed,prepareSave,mergeWordClassDraft,restoreDesign,sourceBasis,clone,diff,labels} from './model.mjs';
const directory=path.dirname(fileURLToPath(import.meta.url));
const hash=s=>crypto.createHash('sha256').update(s).digest('hex');
export function createWorkbench({dataDirectory=directory,root=path.resolve(directory,'../../..'),snapshotPath=path.join(directory,'implementation.json')}={}) {
  const secret=crypto.randomBytes(24).toString('hex'),file=path.join(dataDirectory,'design.json'),history=path.join(dataDirectory,'.history');
  fs.mkdirSync(history,{recursive:true});
  const snapshot=()=>JSON.parse(fs.readFileSync(snapshotPath,'utf8'));
  function atomic(target,value){const temp=target+'.'+crypto.randomUUID()+'.tmp';try{fs.writeFileSync(temp,JSON.stringify(value,null,2)+'\n',{flag:'wx'});fs.renameSync(temp,target);}finally{if(fs.existsSync(temp))fs.unlinkSync(temp);}}
  function version(doc,note){const id=String(doc.revision).padStart(6,'0')+'-'+crypto.randomUUID()+'.json';fs.writeFileSync(path.join(history,id),JSON.stringify({id,at:new Date().toISOString(),note,doc},null,2)+'\n',{flag:'wx'});return id;}
  if(!fs.existsSync(file)){const d=seed(snapshot());version(d,'从 V5 实现建立初始参考；尚未验收');atomic(file,d);}
  const recentlyRead=new Map();
  function read(){
    const content=fs.readFileSync(file,'utf8'),token=hash(content);
    const doc=upgradeDesign(JSON.parse(content));
    if(!recentlyRead.has(token)){
      recentlyRead.set(token,doc);
      if(recentlyRead.size>8)recentlyRead.delete(recentlyRead.keys().next().value);
    }
    return {doc,token};
  }
  function draftBase(token,revision){
    if(recentlyRead.has(token))return recentlyRead.get(token);
    if(!Number.isSafeInteger(revision)||revision<0)return null;
    const prefix=String(revision).padStart(6,'0')+'-';
    for(const id of fs.readdirSync(history).filter(id=>id.startsWith(prefix)&&id.endsWith('.json'))){
      const old=historic(id).doc;
      if(hash(JSON.stringify(old,null,2)+'\n')===token)return upgradeDesign(old);
    }
    return null;
  }
  const firstVersion=fs.readdirSync(history).filter(x=>x.endsWith('.json')).sort()[0];
  const firstDesign=firstVersion?upgradeDesign(historic(firstVersion).doc):read().doc;
  function state(){const s=snapshot();const changed=s.sources.filter(x=>{try{return hash(fs.readFileSync(path.join(root,x.path)))!==x.sha256;}catch{return true;}}).map(x=>x.path);const current=read();return {...current,designBase:baseline(firstDesign,current.doc),snapshot:s,changedSources:changed,csrf:secret,app:'PuzzleApple-V5-Workbench'};}
  function historic(id){if(!/^\d{6,}-[0-9a-f-]{36}\.json$/.test(id))throw Error('无效版本编号。');return JSON.parse(fs.readFileSync(path.join(history,id),'utf8'));}
  function commit(doc,note){version(doc,note||'保存设计');atomic(file,doc);}
  const staticFiles={'/':'index.html','/index.html':'index.html','/app.mjs':'app.mjs','/model.mjs':'model.mjs','/design.mjs':'design.mjs','/workflow.mjs':'workflow.mjs','/batch-ui.mjs':'batch-ui.mjs','/styles.css':'styles.css','/levels.mjs':'levels.mjs','/level-ui.mjs':'level-ui.mjs','/principles.mjs':'principles.mjs'};
  const server=http.createServer(async(req,res)=>{
    function send(code,value,type='application/json; charset=utf-8'){res.writeHead(code,{'Content-Type':type,'Cache-Control':'no-store','X-Content-Type-Options':'nosniff','Referrer-Policy':'no-referrer','Content-Security-Policy':"default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; connect-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'none'"});res.end(type.startsWith('application/json')?JSON.stringify(value):value);}
    try{
      const origin='http://'+req.headers.host;
      if(!/^127\.0\.0\.1:\d+$/.test(req.headers.host??''))return send(403,{error:'仅允许本机访问。'});
      const url=new URL(req.url,origin),p=url.pathname;
      if(req.method==='GET'){
        if(p==='/api/rule-source'){
          const source=ruleSources.find(s=>s.id===url.searchParams.get('id'));
          if(!source)return send(404,{error:'找不到规则来源。'});
          const raw=fs.readFileSync(path.join(root,'Assets/Docs',source.file),'utf8');
          return send(200,{file:source.file,heading:source.heading,text:readSourceSection(raw,source)});
        }
        if(p==='/api/state')return send(200,state());
        if(p==='/api/history')return send(200,fs.readdirSync(history).filter(x=>x.endsWith('.json')).map(id=>{const h=historic(id);return {id,at:h.at,note:h.note,revision:h.doc.revision};}).sort((a,b)=>b.revision-a.revision||b.at.localeCompare(a.at)));
        if(p==='/api/version'){const h=historic(url.searchParams.get('id'));h.doc=upgradeDesign(h.doc);return send(200,{...h,diff:diff(h.doc,read().doc)});}
        if(p==='/api/brief'){
          const d=read().doc,id=url.searchParams.get('batch'),batch=id&&d.batches.find(b=>b.id===id);
          if(id&&!batch)return send(404,{error:'找不到批次。'});
          return send(200,brief(d,batch),'text/markdown; charset=utf-8');
        }
        if(p==='/api/export')return send(200,read().doc);
        if(p==='/api/handoff'){const d=read().doc;return send(200,{format:'puzzleapple-v5-handoff',schemaVersion:1,revision:d.revision,rules:d.rules,batches:d.batches.filter(b=>!b.archived&&b.status!=='passed'),requests:d.requests.filter(r=>!r.archived&&r.status!=='passed').map(r=>({...r,currentBasis:sourceBasis(d,r.source)})),design:d,implementation:snapshot()});}
        if(p.startsWith('/symbol/')){const key=decodeURIComponent(p.slice(8)),w=snapshot().words.find(w=>w.key===key);if(!w||!w.symbol.startsWith('Assets/Sprites/V5/')||path.extname(w.symbol).toLowerCase()!=='.png')return send(404,{error:'找不到字形。'});return send(200,fs.readFileSync(path.join(root,w.symbol)),'image/png');}
        if(staticFiles[p]){const target=staticFiles[p];return send(200,fs.readFileSync(path.join(directory,target)),target.endsWith('.mjs')?'text/javascript; charset=utf-8':target.endsWith('.css')?'text/css; charset=utf-8':'text/html; charset=utf-8');}
        return send(404,{error:'页面不存在。'});
      }
      if(req.method!=='POST')return send(405,{error:'不支持此操作。'});
      if(req.headers.origin!==origin||req.headers['x-workbench-token']!==secret)return send(403,{error:'请从工作台页面提交。'});
      if(!(req.headers['content-type']??'').startsWith('application/json'))return send(415,{error:'需要 JSON。'});
      let raw='',bytes=0;for await(const chunk of req){bytes+=chunk.length;if(bytes>20*1024*1024)return send(413,{error:'数据超过 20 MB。请缩小上传字形或拆分历史。'});raw+=chunk;}
      const input=JSON.parse(raw),current=read();
      if(p==='/api/recover-classes'||input.baseToken!==current.token){
        if(p!=='/api/save'&&p!=='/api/recover-classes')return send(409,{error:'项目已更新，请核对当前版本。'});
        const token=p==='/api/recover-classes'?input.draftToken:input.baseToken;
        const base=draftBase(token,input.doc?.revision);
        let merged;
        try{merged=mergeWordClassDraft(base,input.doc,current.doc);}catch(error){return send(409,{error:error.message});}
        if(!merged)return send(409,{error:'项目版本已变化，无法自动合并这份草稿。请先导出草稿，再核对项目版本。'});
        if(p==='/api/recover-classes'){
          if(input.baseToken!==current.token)return send(409,{error:'项目又有更新，请刷新后恢复草稿。'});
          return send(200,{doc:merged});
        }
        input.doc=merged;
      }
      if(p==='/api/stop'){send(200,{stopped:true});server.close();return;}
      if(p==='/api/workflow'){
        const result=prepareSave(input.doc,current.doc);
        const command={...input.command,batchId:result.map[input.command?.batchId]??input.command?.batchId};
        const entry=publish(result.doc,command,baseline(firstDesign,current.doc));
        commit(result.doc,entry.summary);
        return send(200,{...state(),idMap:result.map});
      }
      if(p==='/api/save'){
        const result=prepareSave(input.doc,current.doc);commit(result.doc,input.note);return send(200,{...state(),idMap:result.map});
      }
      if(p==='/api/round'){
        const d=clone(current.doc),items=clone(d.review.items);
        for(const [id,item]of Object.entries(items)){const c=d.cases.find(c=>c.id===id);if(!c)throw Error('验收情境不存在。');if(item.verdict!=='todo'&&JSON.stringify(item.basis)!==JSON.stringify(sourceBasis(d,{collection:'cases',id})))throw Error('有情境在核对后发生变化，请重新核对结论。');}
        d.rounds.push({number:d.review.number,at:new Date().toISOString(),note:input.note||'',items,design:Object.fromEntries(Object.keys(labels).filter(k=>k!=='requests').map(k=>[k,clone(d[k])])),implementation:snapshot()});
        d.review={number:d.review.number+1,items:Object.fromEntries(Object.entries(items).map(([id,item])=>[id,{verdict:['pass','skip'].includes(item.verdict)?'skip':'todo',comment:'',basis:sourceBasis(d,{collection:'cases',id})}]))};
        d.revision++;commit(d,'提交第 '+(d.review.number-1)+' 轮验收');return send(200,state());
      }
      if(p==='/api/restore'){
        const old=historic(input.id),restored=restoreDesign(upgradeDesign(old.doc),current.doc),result=prepareSave(restored,current.doc);
        commit(result.doc,'恢复设计至 '+old.note+'；保留开发与验收历史');return send(200,state());
      }
      return send(404,{error:'操作不存在。'});
    }catch(error){send(400,{error:error.message});}
  });
  return server;
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
  const port=Number(process.env.PUZZLE_WORKBENCH_PORT??4320);
  const server=createWorkbench({dataDirectory:process.env.PUZZLE_WORKBENCH_DATA||directory});
  server.listen(port,'127.0.0.1',()=>console.log('PuzzleApple V5 工作台 http://127.0.0.1:'+port));
  server.on('error',e=>{console.error(e.message);process.exitCode=1;});
}
