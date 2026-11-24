export const spaceKinds={room:'房间',passage:'通道',area:'区域'};
export const edgeKinds={open:'连通',door:'门',membrane:'薄膜门',portal:'跨章入口'};
export const endNames={entrance:'入口',exit:'出口',side:'侧面'};
const initialOrder=['Chamber','FirstPath','SphereRoom','ReturnPath','FarRoom','Ascent','Outdoor','Gallery','Garden','Vitruvian'];
// Only documented boundaries are seeded. Both ends refer to the same record.
const initialEdges=[
  ['Chamber','FirstPath','首段通道显现','open',true],
  ['FirstPath','SphereRoom','铰链白门','door',true],
  ['SphereRoom','ReturnPath','通往轨道平台','open',false],
  ['ReturnPath','FarRoom','飘灯房入口门','door',true],
  ['FarRoom','Ascent','充能门','door',true],
  ['Gallery','Garden','花房画框','membrane',false],
  ['Gallery','Vitruvian','维特鲁威人画框','membrane',false]
];
export function migrateLevels(d){
  const ordered=[...d.spaces].sort((a,b)=>{
    const index=s=>{const i=initialOrder.indexOf(s.source?.name);return i<0?initialOrder.length:i;};
    return index(a)-index(b);
  });
  d.chapters=[];d.boundaries=[];
  const numbered=(prefix,n)=>prefix+'-'+String(n).padStart(4,'0');
  const byName=new Map(),byAsset=new Map();
  for(const s of ordered){
    const name=s.chapter?.trim()||'未分组';
    let chapter=byName.get(name);
    if(!chapter){
      chapter={id:numbered('CH',byName.size+1),name,archived:false,description:'',spaceOrder:[]};
      byName.set(name,chapter);d.chapters.push(chapter);
    }
    chapter.spaceOrder.push(s.id);s.chapterId=chapter.id;delete s.chapter;
    const key=s.source?.name;
    s.kind=['FirstPath','ReturnPath','Ascent','Gallery'].includes(key)?'passage':key==='Outdoor'||!initialOrder.includes(key)?'area':'room';
    if(key)byAsset.set(key,s.id);
  }
  for(const [from,to,name,kind,progressLock] of initialEdges){
    if(!byAsset.has(from)||!byAsset.has(to))continue;
    d.boundaries.push({id:numbered('J',d.boundaries.length+1),name,kind,progressLock,archived:false,
      from:byAsset.get(from),to:byAsset.get(to),fromEnd:from==='Gallery'?'side':'exit',toEnd:'entrance'});
  }
  d.counters.chapters=d.chapters.length;d.counters.boundaries=d.boundaries.length;
}
export function chapterSpaces(d,c,includeArchived=false){
  const spaces=d.spaces.filter(s=>s.chapterId===c.id&&(includeArchived||!s.archived));
  const rank=new Map(c.spaceOrder.map((id,index)=>[id,index]));
  return spaces.sort((a,b)=>(rank.get(a.id)??Infinity)-(rank.get(b.id)??Infinity));
}
export function spaceEdges(d,id,includeArchived=false){
  const visible=new Set(d.spaces.filter(s=>includeArchived||!s.archived).map(s=>s.id));
  return d.boundaries.filter(e=>(includeArchived||!e.archived)&&(e.from===id||e.to===id)&&visible.has(e.from)&&visible.has(e.to));
}
export function moveSpace(d,space,targetChapter){
  const old=d.chapters.find(c=>c.id===space.chapterId);
  if(!targetChapter||targetChapter.archived)throw Error('请选择有效的大关。');
  if(targetChapter===old)return;
  if(old)old.spaceOrder=old.spaceOrder.filter(id=>id!==space.id);
  space.chapterId=targetChapter.id;targetChapter.spaceOrder.push(space.id);
}
export function reorderSpace(d,chapter,sourceId,targetId){
  const ids=chapterSpaces(d,chapter,true).map(s=>s.id);
  const start=ids.indexOf(sourceId),end=ids.indexOf(targetId);
  if(start<0||end<0||start===end)return false;
  ids.splice(start,1);ids.splice(end,0,sourceId);chapter.spaceOrder=ids;return true;
}
export function validateLevels(d){
  const chapters=new Map(d.chapters.map(c=>[c.id,c])),spaces=new Map(d.spaces.map(s=>[s.id,s]));
  for(const c of d.chapters){
    if(typeof c.name!=='string'||!Array.isArray(c.spaceOrder)||new Set(c.spaceOrder).size!==c.spaceOrder.length||c.spaceOrder.some(id=>spaces.get(id)?.chapterId!==c.id))throw Error('关卡布局引用了无效空间。');
  }
  for(const s of d.spaces){
    const c=chapters.get(s.chapterId);
    if(!c||(!s.archived&&c.archived)||!Object.hasOwn(spaceKinds,s.kind))throw Error('空间需要有效的大关和类型。');
  }
  for(const e of d.boundaries){
    if(!spaces.has(e.from)||!spaces.has(e.to)||e.from===e.to||!Object.hasOwn(edgeKinds,e.kind)||!Object.hasOwn(endNames,e.fromEnd)||!Object.hasOwn(endNames,e.toEnd)||typeof e.progressLock!=='boolean')throw Error('请核对边界的两个空间、端点与类型。');
  }
}
