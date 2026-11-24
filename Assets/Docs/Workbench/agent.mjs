import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {upgradeDesign} from './design.mjs';
import {ruleSources} from './principles.mjs';
import {brief,batchStates} from './workflow.mjs';

const dir=path.dirname(fileURLToPath(import.meta.url));
const [mode='rules',arg]=process.argv.slice(2);
const doc=upgradeDesign(JSON.parse(fs.readFileSync(path.join(dir,'design.json'),'utf8')));
try{
  if(mode==='rules')console.log(doc.rules.map(r=>'## '+r.name+'\n'+r.description).join('\n\n')+'\n\n设计原则的项目依据\n'+ruleSources.map(s=>'- '+s.title+' · Assets/Docs/'+s.file+' / '+s.heading).join('\n'));
  else if(mode==='list')console.log(doc.batches.map(b=>b.id+'  '+batchStates[b.status]+'  '+b.name).join('\n')||'尚无开发批次。');
  else if(mode==='design')console.log(brief(doc));
  else if(mode==='batch'){
    const b=doc.batches.find(x=>x.id===arg);if(!b)throw Error('未找到批次 '+arg);
    console.log(brief(doc,b));
  }else if(mode==='record'){
    const command=JSON.parse(fs.readFileSync(arg,'utf8').replace(/^\uFEFF/,''));
    if(!['session','progress'].includes(command.action))throw Error('仅支持会话归档或阶段进展。');
    command.author=command.author?.trim()||'agent';
    const url='http://127.0.0.1:'+(process.env.PUZZLE_WORKBENCH_PORT||4320);
    const get=await fetch(url+'/api/state');if(!get.ok)throw Error('请先启动 V5 工作台。');
    const state=await get.json();
    if(state.app!=='PuzzleApple-V5-Workbench'||state.doc.schemaVersion!==3)throw Error('请启动新版 V5 工作台。');
    const result=await fetch(url+'/api/workflow',{method:'POST',headers:{Origin:url,'X-Workbench-Token':state.csrf,'Content-Type':'application/json'},body:JSON.stringify({baseToken:state.token,doc:state.doc,command})});
    const data=await result.json();if(!result.ok)throw Error(data.error);
    console.log(data.doc.timeline.at(-1).summary);
  }else throw Error('用法：agent.mjs rules | list | design | batch B-0001 | record <JSON 文件>');
}catch(error){console.error(error.cause?.code==='ECONNREFUSED'?'请先启动 V5 工作台。':error.message);process.exitCode=1;}
