import {migrateLevels} from './levels.mjs';
import {designSummary} from './principles.mjs';

// Initial editorial content for existing V5 designs and new workspaces.
export const initialRules = [
  {id:'A-0001', name:'设计原则', description:'符号可兼任多个词类。按字形与稳定编号引用符号，含义可随用法变化。\n先说明语法的共同含义，再在关卡或具体句子中写特殊条件与表现。\n组句时解析，点击句号后生效。对象是否响应取决于性质、当前状态与作用范围。', origin:'V5.md · 句子系统结构'},
  {id:'A-0002', name:'Agent 工作规范', description:'开始 V5 工作时先读项目 AGENTS.md、全局规则和本批需求。\nUnity 脚本放 Assets/Scripts/，场景放 Assets/Scenes/，预制体放 Assets/Prefabs/，3D 素材放 Assets/ArtAssets-3D/。按用途在现有目录细分，沿用相邻资源命名；移动资源时保留 .meta 与 GUID。\n按本批明确的范围修改设计与实现。关卡初稿注明依据和待确认项；跨条目的设计变更在交接中列明。\n记录实际改动、验证结果和仍待验证的操作。验收结论由用户填写。用户标记会话完成后，将该批改动汇总一次归档。',origin:'AGENTS.md · 工作台协作流程'}
];
const areaDrafts = {
  Chamber:['Intro','从方盒开始学习组句与开盒。盒内灯响应作用范围内的开关灯命令。'],
  FirstPath:['Intro','白门连接运动教学房。玩家可以观察通道星辰，习得 light 和移动例句。'],
  SphereRoom:['Intro','大球初始熄灭并挡住出口。观察小球的示范，再用已经习得的移动句接管。'],
  ReturnPath:['Intro','轨道平台连接运动教学房与飘灯房。收集 floor 后可通过移动句驱动平台。'],
  FarRoom:['Intro','飘灯房初始只有远端充能门发光。房间解锁后，灯体参与光与运动的交互。'],
  Ascent:['Intro','通过充能门后的上升通道前往开阔地。'],
  Outdoor:['开阔地','白门返回 Intro；穿过画框进入 Chapter I。可在此处放置和回收自建灯。'],
  Gallery:['Chapter I','展开入口方盒进入主展廊。由画框进入不同房间，入口镜通回开阔地。'],
  Garden:['Chapter I','主展廊的画框通往花房。'],
  Vitruvian:['Chapter I','通过角镜调整反射光路，使光线到达人物两脚之间。']
};
const grammarDrafts = {
  'state.on':'开启作用范围内符合该符号开关性质的对象。对象按自身能力执行开启表现。',
  'state.off':'关闭作用范围内符合该符号开关性质的对象。对象按自身能力执行关闭表现。',
  'move.free':'符合主语性质和运动资格的对象，自行执行持续运动。',
  'move.directed':'主语朝目标移动。对象与目标的资格及具体运动方式取决于关卡。',
  illuminate:'提高目标类别的照明强度，由相应的灯、门、地面或世界表现。'
};
export function upgradeDesign(doc) {
  if(doc.schemaVersion===3)return doc;
  if(doc.schemaVersion===2){
    migrateLevels(doc);
    const principle=doc.rules.find(r=>r.id==='A-0001');
    if(principle&&principle.description===initialRules[0].description){
      principle.description=designSummary;
      principle.origin='当前 V5 主档、句号机制、句子系统架构问答与生效范围专题';
    }
    doc.schemaVersion=3;
    return doc;
  }
  if(doc.schemaVersion!==1)throw Error('不支持的工作台数据版本。');
  doc.wordClasses=[['状态',1],['名词',2],['动词',4]].map(([name,bit],i)=>({id:'T-000'+(i+1),name,bit,description:'',archived:false}));
  doc.rules=initialRules.map(r=>({...r,archived:false}));
  doc.batches=doc.requests.map((r,i)=>({
    id:'B-'+String(i+1).padStart(4,'0'),name:r.name||r.request,archived:r.archived,status:r.status,
    items:[{id:r.id,text:r.request,expected:'',refs:[r.source],basis:[{ref:r.source,after:r.basis}],implementation:r.implementation,validation:r.validation,feedback:r.feedback,verdict:r.status==='passed'?'pass':r.status==='rework'?'rework':'todo'}],
    legacyId:r.id
  }));
  doc.timeline=[];
  doc.counters={...doc.counters,wordClasses:3,rules:2,batches:doc.batches.length};
  const light=doc.words.find(w=>w.key==='light');
  if(light){
    light.propertyIds??=doc.properties.filter(p=>['Light','PotentialLight'].includes(p.source?.name)).map(p=>p.id);
    if(!light.meaning)light.meaning='开关用法关心对象能否发光，熄灭的灯仍可被点亮。按发光性质选取对象时则要看当前亮度。这个符号也可以在灯化语法中作为动作使用。';
  }
  const concepts={
    PotentialLight:'具备可开关的发光能力。对象完全熄灭时，仍然可以响应开灯。',
    Light:'当前亮度大于零的发光对象。该性质随对象状态变化。',
    Door:'能够承接门类语义的对象。白门可以绕轴旋转，充能门可以升降。',
    Emission:'控制发光强度。多个对象可共享一个渐变输出。',
    Hinge:'控制绕铰链旋转的开合动作。',
    Lift:'控制机关沿升降方向的开合动作。',
    Position:'承接对象的位置运动。实际运动方式由对象决定。'
  };
  for(const row of [...doc.properties,...doc.outputs]){
    if(!row.description&&concepts[row.source?.name])row.description=concepts[row.source.name];
  }
  for(const s of doc.spaces){
    const draft=areaDrafts[s.source?.name];
    if(draft){s.chapter??=draft[0];if(!s.description){s.description=draft[1];s.designSource='初稿依据：V5.md';}}
  }
  for(const g of doc.grammars){
    if(!g.behavior&&grammarDrafts[g.key]){g.behavior=grammarDrafts[g.key];g.designSource='初稿依据：V5.md · 句子系统结构';}
  }
  doc.schemaVersion=2;
  return upgradeDesign(doc);
}
export function classIds(d,row) {
  return row.classIds??d.wordClasses.filter(c=>c.bit && (row.classes & c.bit)).map(c=>c.id);
}
export function matchesSlot(d,slot,word) {
  const required=classIds(d,slot), actual=classIds(d,word);
  return (!required.length||required.some(id=>actual.includes(id)))&&(!slot.words.length||slot.words.includes(word.id));
}
export function classUsers(d,id){
  return [...d.words.filter(w=>classIds(d,w).includes(id)),...d.grammars.filter(g=>g.slots.some(s=>classIds(d,s).includes(id)))].filter(x=>!x.archived);
}

// Palette tokens can be inserted or substituted. Moving an existing slot carries
// its constraints and its semantic bindings with it.
export function dropGrammarSlot(grammar,source,operation,index){
  const slots=grammar.slots;
  const existing=Number.isInteger(source.index);
  const slot=existing?slots[source.index]:source.slot;
  if(!slot||!Number.isInteger(index)||index<0||index>slots.length)return false;
  const bindings=['subject','target'].map(key=>slots[grammar.intent[key]]);
  if(operation==='remove'){
    if(!existing)return false;
    if(!grammar.designDraft&&slots.length<=2)throw Error('启用的语法至少需要两个位置；可以直接拖入素材替换。');
    slots.splice(source.index,1);
  }else if(operation==='replace'&&!existing){
    if(index>=slots.length)return false;
    slots[index]=slot;
    return true;
  }else if(operation==='insert'||(operation==='replace'&&existing&&index<slots.length)){
    if(existing){
      if(operation==='insert'&&source.index<index)index--;
      if(index===source.index)return false;
      slots.splice(source.index,1);
    }else if(slots.length>=8)throw Error('语法最多支持八个位置。');
    slots.splice(index,0,slot);
  }else return false;
  [grammar.intent.subject,grammar.intent.target]=bindings.map(bound=>slots.indexOf(bound));
  return true;
}
