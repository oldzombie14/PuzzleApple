import {finalizeDrafts} from './drafts.mjs';
import fs from 'node:fs';
import path from 'node:path';
import http from 'node:http';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {importCatalog,digest} from './importer.mjs';
import {Compiler,validate,changes,canonical,syncChanges} from './engine.mjs';
export const directory=path.dirname(fileURLToPath(import.meta.url));
export function atomicWrite(file,text){const temp=file+'.'+crypto.randomUUID()+'.tmp';try{fs.writeFileSync(temp,text,{encoding:'utf8',flag:'wx'});fs.renameSync(temp,file);}finally{if(fs.existsSync(temp))fs.unlinkSync(temp);}}
const json=x=>JSON.stringify(x,null,2)+'\n';
// Restore authoring content, retaining newer identities in the archive and every allocated sentence ID.
export function restoreDesign(old,current){const d=structuredClone(old);for(const field of ['parts','words','grammars','relations'])for(const item of current[field]){const prior=d[field].find(x=>x.id===item.id);if(!prior)d[field].push({...structuredClone(item),archived:true});else if(field==='words')for(const s of item.senses)if(!prior.senses.some(x=>x.id===s.id))prior.senses.push({...structuredClone(s),archived:true});}d.tasks=structuredClone(current.tasks||[]);d.nextTask=current.nextTask||1;d.nextGrammar=Math.max(d.nextGrammar,current.nextGrammar);d.nextSentence=Math.max(d.nextSentence,current.nextSentence);for(const [key,r] of Object.entries(current.sentences))if(!d.sentences[key])d.sentences[key]=structuredClone(r);return d;}
export function createWorkbench({dataDirectory=directory,root=path.resolve(directory,'../../..')}={}){
  const target=path.join(dataDirectory,'design.json'),historyDir=path.join(dataDirectory,'.history'),symbols=path.join(dataDirectory,'symbols'),snapshotPath=path.join(dataDirectory,'catalog.snapshot.json');
  fs.mkdirSync(historyDir,{recursive:true});fs.mkdirSync(symbols,{recursive:true});
  const token=crypto.randomBytes(24).toString('hex'),compiler=new Compiler();
  const read=()=>{const raw=fs.readFileSync(target,'utf8');return {design:JSON.parse(raw),revision:digest(raw)};};
  const historyPath=rev=>path.join(historyDir,rev+'.json');
  function retain(state,label,delta=[]){if(!fs.existsSync(historyPath(state.revision)))atomicWrite(historyPath(state.revision),json({revision:state.revision,at:new Date().toISOString(),label,changes:delta,design:state.design}));}
  retain(read(),'新版初始设计');compiler.compile(read().design);
  const timelinePath=path.join(historyDir,'timeline.json');
  if(!fs.existsSync(timelinePath)){const versions=fs.readdirSync(historyDir).filter(n=>/^[a-f0-9]{64}\.json$/.test(n)).map(n=>JSON.parse(fs.readFileSync(path.join(historyDir,n),'utf8'))).sort((a,b)=>a.at.localeCompare(b.at));atomicWrite(timelinePath,json(versions.map((h,i)=>({eventId:h.revision,revision:h.revision,parentRevision:versions[i-1]?.revision||null,inferredParent:i>0,at:h.at,label:h.label,changes:h.changes}))));}
  const releasesPath=path.join(historyDir,'releases.json');
  const releases=()=>fs.existsSync(releasesPath)?JSON.parse(fs.readFileSync(releasesPath,'utf8')):[];
  const timeline=()=>JSON.parse(fs.readFileSync(timelinePath,'utf8'));
  function recordSave(revision,parentRevision,label,delta){const entries=timeline();entries.push({eventId:crypto.randomUUID(),revision,parentRevision,at:new Date().toISOString(),label,changes:delta});atomicWrite(timelinePath,json(entries));}
  if(timeline().at(-1)?.revision!==read().revision)recordSave(read().revision,timeline().at(-1)?.revision||null,'发现项目中的外部修改',[]);
  function commit(candidate,current,label){
    const finalized=finalizeDrafts(candidate,current.design);candidate=finalized.design;
    // Absence must never silently delete referenced authoring identities. UI uses archives.
    for(const field of ['parts','words','grammars','relations'])for(const r of current.design[field])if(!candidate[field]?.some(x=>x.id===r.id))throw Error('请通过归档移除 '+r.id+'；保存不能直接丢弃已有记录。');
    for(const w of current.design.words){const next=candidate.words.find(x=>x.id===w.id);for(const s of w.senses)if(!next.senses.some(x=>x.id===s.id))throw Error('词义 '+s.id+' 仍需保留历史标识，请使用移除／归档。');}
    for(const t of current.design.tasks||[]){const next=candidate.tasks?.find(x=>x.id===t.id);if(!next||next.sourceId!==t.sourceId||next.sourceKind!==t.sourceKind)throw Error('开发事项不能删除或改绑来源：'+t.id);}
    if(candidate.tasks?.length||current.design.nextTask)candidate.nextTask=Math.max(candidate.nextTask||1,current.design.nextTask||1,...(candidate.tasks||[]).map(t=>Number(t.id.slice(2))+1));
    if(!current.design.tasks&&!candidate.tasks?.length)delete candidate.tasks;
    const errors=validate(candidate);if(errors.length)throw Error(errors.join('\n'));
    const result=compiler.compile(candidate,current.design),delta=changes(current.design,result.design),raw=json(result.design),revision=digest(raw);
    retain(current,'保存前版本');retain({design:result.design,revision},label,delta);
    atomicWrite(target,raw);if(revision!==current.revision)recordSave(revision,current.revision,label,delta);return {...result,idMap:finalized.idMap,revision,changes:delta,sync:syncChanges(result.design)};
  }
  const files={'/':'index.html','/index.html':'index.html','/app.mjs':'app.mjs','/engine.mjs':'engine.mjs','/workflow.mjs':'workflow.mjs','/history.mjs':'history.mjs','/drafts.mjs':'drafts.mjs','/styles.css':'styles.css'};
  const mime={'.html':'text/html; charset=utf-8','.mjs':'text/javascript; charset=utf-8','.css':'text/css; charset=utf-8','.png':'image/png','.jpg':'image/jpeg','.webp':'image/webp'};
  const server=http.createServer(async(req,res)=>{
    const send=(status,value)=>{res.writeHead(status,{'Content-Type':'application/json; charset=utf-8'});res.end(JSON.stringify(value));};
    res.setHeader('Cache-Control','no-store');res.setHeader('X-Content-Type-Options','nosniff');
    res.setHeader('Content-Security-Policy',"default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' blob:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'");
    try{
      const host='127.0.0.1:'+server.address().port;if(req.headers.host!==host)return send(403,{error:'仅接受本机地址。'});
      const url=new URL(req.url,'http://'+host);
      if(req.method==='GET'){
        let file=files[url.pathname]?path.join(directory,files[url.pathname]):/^\/symbols\/[a-f0-9]{64}\.(png|jpg|webp)$/.test(url.pathname)?path.join(symbols,path.basename(url.pathname)):null;
        if(file){if(!fs.existsSync(file))return send(404,{error:'图片不存在。'});res.writeHead(200,{'Content-Type':mime[path.extname(file)]});return res.end(fs.readFileSync(file));}
        if(url.pathname==='/api/state'){const state=read(),compiled=compiler.compile(state.design);return send(200,{...compiled,revision:state.revision,token,snapshot:JSON.parse(fs.readFileSync(snapshotPath,'utf8')),sync:syncChanges(state.design)});}
        if(url.pathname==='/api/releases')return send(200,releases());
        if(url.pathname==='/api/history')return send(200,timeline().reverse());
        if(/^\/api\/history\/[a-f0-9]{64}$/.test(url.pathname)){const p=historyPath(url.pathname.split('/').pop());return fs.existsSync(p)?send(200,JSON.parse(fs.readFileSync(p,'utf8'))):send(404,{error:'版本不存在。'});}
        if(url.pathname==='/api/reference'){const d=read().design,id=url.searchParams.get('id'),r=Object.values(d.sentences).find(s=>s.id===id);return r?send(200,{sentence:r,grammars:d.grammars.filter(g=>r.grammarIds.includes(g.id)),relations:d.relations.filter(c=>!c.archived&&c.sentenceIds.includes(id))}):send(404,{error:'编号不存在。'});}
        return send(404,{error:'没有此入口。'});
      }
      if(req.method!=='POST')return send(405,{error:'不支持此方法。'});
      if(req.headers.origin!=='http://'+host||req.headers['x-workbench-token']!==token)return send(403,{error:'页面已过期，请导出草稿后重新载入。'});
      const chunks=[];let size=0;for await(const c of req){size+=c.length;if(size>40*1024*1024)return send(413,{error:'请求超过 40 MB。'});chunks.push(c);}
      const bytes=Buffer.concat(chunks);
      if(url.pathname==='/api/symbol'){
        if(bytes.length>2*1024*1024)return send(413,{error:'符号图片不能超过 2 MB。'});
        const extension=bytes.subarray(0,8).equals(Buffer.from([137,80,78,71,13,10,26,10]))?'png':bytes[0]===255&&bytes[1]===216&&bytes[2]===255?'jpg':bytes.toString('ascii',0,4)==='RIFF'&&bytes.toString('ascii',8,12)==='WEBP'?'webp':null;
        if(!extension)return send(400,{error:'请上传 PNG、JPEG 或 WebP 图片。'});
        const name=digest(bytes)+'.'+extension;if(!fs.existsSync(path.join(symbols,name)))fs.writeFileSync(path.join(symbols,name),bytes,{flag:'wx'});return send(200,{symbol:'/symbols/'+name});
      }
      const payload=JSON.parse(bytes.toString('utf8')),current=read();if(payload.revision!==current.revision)return send(409,{error:'项目设计已在别处更新。当前草稿保留，请导出后重新载入，避免覆盖。'});
      if(url.pathname==='/api/releases'){
        const name=String(payload.name||'').trim(),notes=String(payload.notes||'').trim(),list=releases();if(!name||name.length>120)return send(400,{error:'请输入不超过 120 字的版本名称。'});if(list.some(r=>r.name.toLocaleLowerCase()===name.toLocaleLowerCase()))return send(400,{error:'版本名称已存在，请使用不同名称。'});
        retain(current,'命名版本');const release={id:crypto.randomUUID(),name,notes,revision:current.revision,createdAt:new Date().toISOString()};list.unshift(release);atomicWrite(releasesPath,json(list));return send(200,{release,releases:list});
      }
      if(url.pathname==='/api/save')return send(200,commit(payload.design,current,payload.label||'保存设计'));
      if(url.pathname==='/api/restore'){
        if(!/^[a-f0-9]{64}$/.test(payload.target||''))return send(400,{error:'历史版本无效。'});
        const old=JSON.parse(fs.readFileSync(historyPath(payload.target),'utf8'));return send(200,commit(restoreDesign(old.design,current.design),current,'恢复版本 '+old.at));
      }
      if(url.pathname==='/api/source'){const snapshot=importCatalog(root);atomicWrite(snapshotPath,json(snapshot));return send(200,{snapshot});}
      return send(404,{error:'没有此入口。'});
    }catch(err){return send(400,{error:err.message});}
  });return server;
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){const server=createWorkbench();server.on('error',e=>{console.error(e.message);process.exitCode=1;});server.listen(Number(process.env.PUZZLEAPPLE_WORKBENCH_PORT||4317),'127.0.0.1',()=>console.log('V3 language workbench: http://127.0.0.1:'+server.address().port));}
