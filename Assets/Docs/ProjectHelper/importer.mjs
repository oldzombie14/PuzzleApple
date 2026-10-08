// Read-only adapter for this project's Unity text assets, deliberately not a general YAML parser.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const roles=['Reference','Predicate','Modifier','Transform','Relation'];
export const digest=s=>crypto.createHash('sha256').update(s).digest('hex');
function walk(dir){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(dir,e.name)):[path.join(dir,e.name)]);}
function scalar(text,key){
  const raw=text.match(new RegExp('^  '+key+':(.*)$','m'))?.[1]?.trim()||'';
  if(raw.startsWith('"'))return JSON.parse(raw);
  if(raw.startsWith("'"))return raw.slice(1,-1).replaceAll("''", "'");
  return raw;
}
function refs(text,key){
  const body=text.match(new RegExp('^  '+key+':[^\\r\\n]*\\r?\\n((?:  -[^\\r\\n]*\\r?\\n)*)','m'))?.[1]||'';
  return [...body.matchAll(/guid: ([a-f0-9]{32})/g)].map(m=>m[1]);
}
export function importCatalog(root){
  const catalogPath='Assets/GameData/V3/Opening/OpeningCatalog.asset';
  const paths=walk(path.join(root,'Assets/GameData/V3')).filter(p=>p.endsWith('.asset.meta'));
  const map=new Map(paths.map(p=>[fs.readFileSync(p,'utf8').match(/^guid: (\w+)/m)?.[1],p.slice(0,-5)]));
  const sources=new Map();
  const read=p=>{const txt=fs.readFileSync(p,'utf8');sources.set(path.relative(root,p).replaceAll('\\','/'),digest(txt));return txt;};
  const resolve=guid=>{const p=map.get(guid);if(!p)throw Error('V3 引用缺失：'+guid);read(p+'.meta');return p;};
  const text=read(path.join(root,catalogPath));
  const wordByGuid=new Map();
  const words=refs(text,'words').map(guid=>{
    const p=resolve(guid),t=read(p),id=scalar(t,'id');wordByGuid.set(guid,id);
    const senses=[...t.matchAll(/  - meaning: (.+)\r?\n    role: (\d+)/g)].map(m=>({meaning:m[1].trim(),role:roles[Number(m[2])]}));
    if(!id||!senses.length||senses.some(s=>!s.role))throw Error('无法读取词义：'+p);
    return {id,displayText:scalar(t,'displayText'),guid,senses,symbolGuid:t.match(/^  symbol:.*guid: ([a-f0-9]{32})/m)?.[1]||'',path:path.relative(root,p).replaceAll('\\','/')};
  });
  if(new Set(words.map(w=>w.id)).size!==words.length)throw Error('词库存在重复 ID。');
  const rules=refs(text,'rules').map(guid=>{
    const p=resolve(guid),t=read(p);
    const ids=refs(t,'words').map(g=>wordByGuid.get(g));
    if(ids.length<2||ids.some(w=>!w))throw Error('规则包含未登记的词：'+p);
    return {id:scalar(t,'id'),words:ids,effect:Number(scalar(t,'effect')),trigger:Number(scalar(t,'trigger')),exclusionKey:scalar(t,'exclusionKey'),path:path.relative(root,p).replaceAll('\\','/')};
  });
  const conflicts=refs(text,'conflicts').map(guid=>{const p=resolve(guid),t=read(p);return {id:scalar(t,'id'),first:Number(scalar(t,'first')),second:Number(scalar(t,'second')),earliestWins:scalar(t,'earliestWins')==='1'};});
  if(!words.length||!rules.length)throw Error('V3 目录的词或规则列表为空，可能不是受支持的 Unity 文本格式；保留旧快照。');
  const provenance=Object.fromEntries([...sources].sort(([a],[b])=>a.localeCompare(b)));
  return {schemaVersion:1,catalogPath,sourceHash:digest(JSON.stringify(provenance)),sources:provenance,words,rules,conflicts};
}
