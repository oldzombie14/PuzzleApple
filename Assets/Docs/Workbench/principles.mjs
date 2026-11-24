// References are limited to current project documents. The viewer reads the cited section on demand.
export const ruleSources = [
  {id:'visual',title:'画面与材质',file:'V5.md',heading:'已确认要求',paragraphs:['**全局避免','启动即进入']},
  {id:'semantics',title:'符号、语法与对象性质',file:'V5.md',heading:'句子系统结构',paragraphs:['原则：','- 语法入口','- `SentenceGrammarResolver`']},
  {id:'period',title:'句号、草稿与执行锁定',file:'V5.md',heading:'句号生效设计'},
  {id:'scope',title:'章节、进度锁与薄膜',file:'V5.md',heading:'已确认要求',paragraphs:['**句子的作用范围']},
  {id:'boundaries',title:'边界的指定与 Intro 进度层',file:'C2C/生效范围-进度锁与薄膜门.md',heading:'2. 我的理解与默认处理【建议 + 待确认】'},
  {id:'objects',title:'参与对象、局部冲突与反馈',file:'C2C/句子系统架构-QA.md',heading:'已确认的玩家行为'},
  {id:'command',title:'一次性命令的空间上限',file:'C2C/句子系统架构-QA.md',heading:'命令范围策略'},
  {id:'resume',title:'暂停与继续',file:'C2C/句号生效机制.md',heading:'暂停与继续：全局方案 A'},
  {id:'feedback',title:'可见变化与演出反馈',file:'V5.md',heading:'已确认要求',paragraphs:['一次性句子只在触发时']},
  {id:'camera',title:'语句镜头',file:'V5.md',heading:'语法与演出接入',paragraphs:['SentencePresentation 在反馈后']},
  {id:'observe',title:'收词与观察',file:'V5.md',heading:'行为观察与持续句子',paragraphs:['灯群移动时','已确认：已习得后']},
  {id:'history',title:'句子历史',file:'V5.md',heading:'句子历史',paragraphs:['已确认：']},
];
export const designSummary = `画面与材质
保留柔光、星芒和局部强光，控制大片过曝。普通墙地面以白色粗糙哑光为基底，暗度由照明形成。章节的特殊材质与演出按各自设计处理。

符号与对象
符号可兼任不同词类。语法给出共同含义，对象按自身性质、当前状态和能力响应。同一句移动命令可以让灯漂移，也可以让轨道平台沿轨道行走。

句号与执行
拼词形成草稿，点击句号才判定生效、冲突、反馈和历史。一次性命令有可执行变化时才锁定全部新句判定，结束后整句归库。期间仍可收词、拖放和编辑其他句子。持续句可拆词或取走句号暂停，恢复时保留运动方向、相位与进度。

章节与边界
默认覆盖当前章，在已解锁进度与当前薄膜区域的交集中选取对象。首次解锁永久保留；带门的进度边界完全关严时隔断两侧。薄膜内外隔离，穿越时重算。跨章清空组句区。light world 与 no move 有明确的全局例外。

命令与持续效果
一次性开关与定向命令保留下令时的空间上限，并检查当前连通性；执行期间，范围内新增或刚满足条件的对象可以加入。持续句随当前范围和对象性质更新。

共存与反馈
同效果可共存。有效句被占用，或一次性命令当前无法执行时，给一次弱反馈并保留低透明度草稿。条件恢复后各句号独立提示，玩家重新点击才执行。语法不通会划线；语法成立但无可见变化给弱反馈，有可见变化给带星反馈。已确认的逐物体优先关系与新增性质组合，仍须按各自的实现进度验收。

镜头与观察
语句演出通常保持当前视角；只有当前空间内确实发生变化的关键机关才请求转向。展示目标不缩小执行范围。普通词汇可直接收集，同时可观察动作的目标区分短按收词与长按观察。

句子历史
历史可与组句区同时浏览，成立与不成立的句子保留各自的视觉标记。关闭面板时先收起历史。`;

export function readSourceSection(markdown,source){
  const lines=markdown.replace(/\r/g,'').split('\n');
  const start=lines.findIndex(l=>/^#{1,6} /.test(l)&&l.replace(/^#+ /,'').trim()===source.heading);
  if(start<0)throw Error('当前文档中的小节已更名，请核对来源。');
  const depth=lines[start].indexOf(' ');
  let end=start+1;
  while(end<lines.length&&!new RegExp('^#{1,'+depth+'} ').test(lines[end]))end++;
  const section=lines.slice(start+1,end).join('\n').trim();
  if(!source.paragraphs)return section;
  const blocks=section.split(/\n\s*\n/).flatMap(block=>block.startsWith('- ')?block.split(/\n(?=- )/):[block]);
  return source.paragraphs.map(prefix=>{
    const found=blocks.find(p=>p.startsWith(prefix));
    if(!found)throw Error('来源段落已变化，请核对 '+source.file+' 的「'+source.heading+'」。');
    return found;
  }).join('\n\n');
}
