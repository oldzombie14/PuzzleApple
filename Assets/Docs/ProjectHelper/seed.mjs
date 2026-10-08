import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {importCatalog,digest} from './importer.mjs';
import {Compiler,canonical,fingerprint} from './engine.mjs';
const here=path.dirname(fileURLToPath(import.meta.url));
const labels={self:'玩家本人',essence:'本质化',mirror:'镜子',reflect:'镜像化',move:'移动',door:'门',positive:'正状态',negative:'负状态',equal:'等同',balance:'天平',apple:'苹果',analyzer:'解析器',red:'红色化',round:'圆形化'};
const roles={Reference:'p-noun',Predicate:'p-verb',Modifier:'p-state',Transform:'p-operation',Relation:'p-relation'};
const source='Assets/Docs/GameDesign.md：V3 当前已确认设计；广义适用范围待审核。';
export function makeSeed(snapshot,symbols={}){
  let d={schemaVersion:2,title:'V3 语言设计',nextGrammar:10,nextSentence:1,parts:[['p-noun','名词','指称对象或玩家。'],['p-verb','动词','表示动作。'],['p-state','状态词','正状态、负状态；名称可修改。'],['p-operation','化词','暂定分类：本质化、镜像化、红色化、圆形化。词义名称独立于词性。'],['p-relation','关系词','等同等关系；名称可修改。']].map(([id,name,notes])=>({id,name,notes,archived:false})),words:[],grammars:[],sentences:{},relations:[]};
  d.words=snapshot.words.map(w=>{const word={id:'w-'+w.guid,code:w.id,symbol:symbols[w.id]||'',notes:'从 V3 SO 导入；词义说明与词性可编辑。',archived:false,senses:w.senses.map(s=>({id:'s-'+w.guid+'-'+s.meaning,text:labels[s.meaning]||s.meaning,posId:roles[s.role],notes:'',archived:false,engineMeaning:s.meaning})),source:{guid:w.guid,path:w.path,engineId:w.id}};word.source.authoredBaseline=canonical({code:word.code,symbol:word.symbol,senses:word.senses,archived:word.archived});return word;});
  const sense=(code,meaning)=>d.words.find(w=>w.code===code).senses.find(s=>s.engineMeaning===meaning).id;
  const pos=(id,ref)=>({id,kind:'pos',ref}),fixed=(id,code,meaning)=>({id,kind:'sense',ref:sense(code,meaning)});
  const grammar=(id,title,slots,logic,lifecycle='',exceptions=[])=>({id,title,slots,logic,lifecycle,exceptions,constraints:[],notes:'可修改结构及默认逻辑。当前扩展到所有匹配词义的范围仍需你确认。',source,status:'draft',enabled:true,archived:false});
  d.grammars=[
    grammar('G-0001','自由移动',[pos('a','p-noun'),fixed('b','move','move')],'默认：非玩家实体做自由运动（项目称布朗运动），仅真实碰撞时按法线反射，不定时随机转向。','拆句停止在当前位置，不自动复位。',[{id:'player-exception',title:'玩家实体',mode:'replace',condition:'响应实体是玩家，包括通过苹果身份响应。不能只根据句面中的词判断。',effect:'沿当前面向自动前进，不反弹。',slotId:'',senseId:''},{id:'door-unresolved',title:'门的移动细节',mode:'unresolved',condition:'主体词义为门；具体运动及与开关机制的关系尚未设计。',effect:'不能从已登记推断已有完整执行行为。',slotId:'a',senseId:sense('door','door')}]),
    grammar('G-0002','来与去',[pos('a','p-noun'),pos('b','p-state'),fixed('c','move','move')],'正状态：生效时投射一次终点。负状态：持续跟随玩家前方位置。','拆句停止在当前位置。'),
    grammar('G-0003','移动到目标',[pos('a','p-noun'),fixed('b','move','move'),pos('c','p-noun')],'按主体有效身份找到响应实体，前往目标指定的位置。目的地需定义落点、容量和分配规则；缺少细节时待设计。','撤销指令不自动回原位，实际占位应在实体离开后释放。'),
    grammar('G-0004','镜像化',[fixed('a','mirror','reflect'),pos('b','p-noun')],'依对象的镜像参照生成持续同步的镜像；重复同句不叠加。玩家为分屏，只有一个物理主体。','拆句移除副本，保留本体姿态及有效移动。'),
    grammar('G-0005','本质化',[fixed('a','i','essence'),pos('b','p-noun')],'恢复原位、原本身份并去除镜像。与同对象镜像互斥，先成立句优先。当前对象重置在首次有效或冲突解除时执行一次。','仍有有效移动时，复位后继续移动；颜色按有效句重新计算。'),
    grammar('G-0006','红色化',[fixed('a','red','red'),pos('b','p-noun')],'让目标指定的可染色部分变红。具体材质范围在句子中补充，不假定所有 Renderer 都染色。','拆句恢复原色；原初化存在时维持原色，解除后按剩余有效句计算。'),
    grammar('G-0007','圆形化',[fixed('a','round','round'),pos('b','p-noun')],'圆形化的对象支持与具体形变逐项设计。','待设计。'),
    grammar('G-0008','设置状态',[pos('a','p-state'),pos('b','p-noun')],'按状态词义改变目标状态。当前明确实例是正状态开门、负状态关门；其它目标待设计。','具体目标决定撤销后的行为。'),
    grammar('G-0009','等同关系',[pos('a','p-noun'),fixed('b','equal','equal'),pos('c','p-noun')],'当前已知实例：玩家增加苹果身份，可响应苹果相关移动。其它等同关系、自反和传递含义待设计，不能推断数学等式性质。','已知玩家苹果身份撤销时不复位位置。')
  ];
  d=new Compiler().compile(d).design;
  const row=(tokens)=>Object.values(d.sentences).find(r=>r.senseIds.join('|')===tokens.map(([a,b])=>sense(a,b)).join('|'));
  const annotate=(tokens,effect,mode='supplement',status='confirmed')=>{const r=row(tokens);r.note={...r.note,mode,status,effect,evidence:source};r.note.reviewedFingerprint=fingerprint(d,r);return r.id;};
  annotate([['red','red'],['equal','balance']],'天平整体材质红色化，拆句恢复。');
  annotate([['red','red'],['mirror','mirror']],'只改变镜框颜色；镜面继续反射，镜像副本同步。');
  annotate([['red','red'],['analyzer','analyzer']],'只改变解析器白色外壳，屏幕不变。');
  annotate([['round','round'],['equal','balance']],'句式成立，但不产生形变。','replace');
  const appleBalance=annotate([['apple','apple'],['move','move'],['equal','balance']],'当前实现：优先沿用实体已占托盘，否则分配最近空盘。先升到盘沿之上，再水平对齐后下降。两盘、镜像配对与实际占用需要共同处理。','supplement','draft');
  const analyzer=annotate([['apple','apple'],['move','move'],['analyzer','analyzer']],'句子成立时按距解析器的位置竞争唯一名额，只有获选苹果移动；过程中不改选，拆句重组后重新分配。实际离开才释放台面。');
  const playerBalance=row([['i','self'],['move','move'],['equal','balance']]).id,identity=row([['i','self'],['equal','equal'],['apple','apple']]).id;
  d.relations=[{id:'R-0001',title:'玩家与苹果分别落盘',kind:'coexist',status:'confirmed',sentenceIds:[identity,playerBalance,appleBalance],condition:'玩家获得苹果身份，天平存在可用位置。',order:'三句可同时保留；不同成立顺序都需验证。',result:'不新增整句互斥；按实际实体分配托盘。',notes:'依据 Assets/Docs/Progress.md：2026-10-07 大厅开灯、落盘与通关过场。',archived:false},{id:'R-0002',title:'多个苹果竞争解析器',kind:'capacity',status:'confirmed',sentenceIds:[analyzer],condition:'多个苹果，解析器只有一个名额。',order:'在句子成立时分配。',result:'仅获选苹果移动，其余保持原位。',notes:'容量限制不等于句式矛盾。',archived:false}];
  for(const r of Object.values(d.sentences))if(r.note.reviewedFingerprint)r.note.reviewedFingerprint=fingerprint(d,r);
  return d;
}
export function initialize(directory=here,root=path.resolve(here,'../../..')){
  const target=path.join(directory,'design.json');if(fs.existsSync(target)&&JSON.parse(fs.readFileSync(target,'utf8')).schemaVersion===2)return;
  const snapshot=importCatalog(root),symbols={};fs.mkdirSync(path.join(directory,'symbols'),{recursive:true});
  const spriteDir=path.join(root,'Assets/Sprites/V3');
  const sprites=new Map(fs.readdirSync(spriteDir).filter(n=>n.endsWith('.png.meta')).map(n=>[fs.readFileSync(path.join(spriteDir,n),'utf8').match(/^guid: (\w+)/m)?.[1],path.join(spriteDir,n.slice(0,-5))]));
  for(const w of snapshot.words)if(sprites.has(w.symbolGuid)){const bytes=fs.readFileSync(sprites.get(w.symbolGuid)),name=digest(bytes)+'.png';fs.writeFileSync(path.join(directory,'symbols',name),bytes);symbols[w.id]='/symbols/'+name;}
  fs.writeFileSync(target,JSON.stringify(makeSeed(snapshot,symbols),null,2)+'\n');fs.writeFileSync(path.join(directory,'catalog.snapshot.json'),JSON.stringify(snapshot,null,2)+'\n');
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url))initialize();
