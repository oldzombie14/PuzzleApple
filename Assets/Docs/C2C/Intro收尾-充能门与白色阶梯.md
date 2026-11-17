# C2C｜Intro 收尾：语法修正、充能门、白色阶梯

> Claude → Codex（Astra）实操文档。2026-11-17，已按用户逐条拍板的结果整理。
> 本文只描述设计与实现方案，不代表已实现。开工前先读 `AGENTS.md`、`Assets/Docs/README.md`、`Assets/Docs/V5.md`（尤其「句子系统结构」「语法与演出接入」「灯群地面与下降」「开阔场景加载」）。
> 标记：**【已定】** 用户已确认；**【建议】** Claude 的方案，可按实际效果微调并在结果里说明；**【待确认】** 未答前按「默认」实施，保持可配置、易回退。
> 本文按**批次**组织，执行顺序见 [00-执行计划.md](00-执行计划.md)：本文批次 A／B／C 对应计划中的批次 1／2／3；拖拽实例化是批次 4，见 [拖拽实例化.md](拖拽实例化.md)。

---

## 0. 总览

**目标流程**：房间 3（飘灯房）远端墙上有一扇凹进墙里的充能门 → 玩家用 `light move door` 把灯聚到门框边，三条充能条充满 → 用 `on door` 让门向上升起 → 门后向上的楼梯 → 在楼梯中点附近渐入纯白空间 → 顶端小台上的立体 light 符号 → 点击点亮 → 强光白闪 → 慢慢回到开阔地。之后再走这扇门只走普通路段，不再出现纯白世界。

**批次与依赖**

| 批次 | 内容 | 依赖 |
|---|---|---|
| A 语法底层 | A1 move 词序统一；A2 句子种类（所有指向型 A move B 改为一次性）；A3 作用范围改为「所在空间 + 视野方向空间 + 距离上限，成立时锁定」；A4 门按空间取目标、门不可自由移动；A5 floor 收集规则 | 无 |
| B 充能门 | B1 门与门龛几何、去掉壁画和地洞；B2 充能计算；B3 聚集停靠；B4 门的控制、失力下落、敲晕重生 | A |
| C 白色阶梯与过场 | C1 楼梯与白色渐入；C2 小台与 light 符号点亮；C3 白闪回到开阔地；C4 一次性状态与回访 | B |
| D 拖拽实例化 | 见独立文档 | A3（区域判断） |

**两类句子（全文依据，摘自现有语法）**
- **持续句**（`SentenceKind.Sustained`）：生效期间一直作用；生效时首次带星涟漪，之后在当前空间实际受该句影响期间循环泛星；拆句暂停当前进度，重组后继续。
- **一次性句**（`SentenceKind.Command`）：触发时反馈一次涟漪，有可见变化时带星，否则为弱涟漪，**完成后归回词库**；拆句暂停当前过渡；完成后效果保留，但句子已不再生效。
- 当前场合没有可见变化时，只给无星弱涟漪，句子正常显示；冲突句显示弱涟漪并变淡。

---

## 批次 A：语法底层

### A1 move 词序统一【已定】

V5 的 move 句一律 **move 在中间**（A move B）。旧词序**作废**，判为不成立（划线），不保留为同义句。

| 规则文件 | 当前 id／词序 | 改为 |
|---|---|---|
| LightFloorMove | `light.move.floor` | ✅ 已正确，不改 |
| LightDoorMove | `light.door.move`／light door move | `light.move.door`／light move door |
| LightLightMove | `light.light.move` | `light.move.light` |
| DoorFloorMove | `door.floor.move` | `door.move.floor` |
| DoorLightMove | `door.light.move` | `door.move.light` |
| FloorDoorMove | `floor.door.move` | `floor.move.door` |
| FloorLightMove | `floor.light.move` | `floor.move.light` |
| LightMove／DoorMove／FloorMove、MoveOn／NoMove | 不涉及方向 | 不改 |

**改动范围**
- `GameData/V5/Opening/Rules/*.asset`：调整 `words` 顺序与 `id`；文件名可不改（`LightFloorMove.asset` 先例），如重命名须保留 `.meta`／GUID，保证 `OpeningCatalog` 引用不断。
- 同步按 id 引用规则的地方：`SentencePresentation.cs` 默认 shot 表（约第 32、45–47 行）、`GameData/V5/Resources/V5/SentencePresentation.asset`、`Editor/*Checks.cs` 中的拼句与 id 断言（全工程搜索旧 id 字符串和旧词序拼句）。
- `CognitionSignal` 枚举名是内部名，不改。

**验收**
- [x] 六条新词序均成立并按原效果生效；旧词序划线不成立。
- [x] 句子历史显示新词序；镜头 shot 正常触发。
- [x] V5 的运行代码、资产及镜头配置无残留旧 id。V4 历史版本保留原规则；验收输入刻意保留旧顺序，用于验证划线。

**回归**：SentenceRuntimeChecks、ObservationChecks、SentenceHistoryChecks、FloorImpactChecks、OutdoorChecks 中与 move 相关的用例。

### A2 句子种类：指向型 A move B 全部改为一次性【已定】

- **所有指向型 A move B 句都是有完成状态的一次性句**（`SentenceKind.Command`）：触发时反馈一次涟漪，物体到达目标后**完成，归回词库**；效果保留，物体停在到达处。
- 不带方向的 `light move`、`door move`、`floor move` 仍是持续句，不变。
- 执行中拆句：停在当前位置（暂停），重组后继续；执行中灯被关掉：暂停，重新亮起后继续。
- 同一 `.motion` 通道的排他规则不变，但一次性句只在执行期间占用通道，完成归库后释放。

| 句子 | 完成条件【建议】 | 完成后 |
|---|---|---|
| light move door | 作用范围内所有亮灯到达门边停靠位（或被阻挡无法再前进） | 灯停靠在门边轻微漂浮 |
| light move floor | 所有亮灯贴地（沿用 `FloorDirectedMotion.IsArrived`） | 灯停在地面 |
| light move light | 每盏亮灯都已靠近邻灯到停止距离（成团） | 灯停在团中 |
| door move light／door move floor | —（A4：室内外都无效） | — |
| floor move light／floor move door | 室内无效；室外地面升起到位（沿用 `WorldSentenceEffects.CanRise` 的终点） | 地面保持升起 |

- **需要新增「停靠」状态**：现在 `RoomLightGathering`／`FloorDirectedMotion` 只在 `HasEffect(...)` 时驱动灯；句子归库后 `DriftingLight` 会回到示范漂移或自主漂移。要为每盏灯记录「已停靠」（门边、地面、成团），停靠中的灯不执行示范漂移和自主漂移，直到有新的运动句作用于它。
- **关灯后仍停着【已定】**：停靠的灯被 `off light` 熄灭后保持停靠，不按「完全熄灭时自主漂移」离开；对充能门而言，灯灭了就不计充能，能量会掉。
- 开阔地（`OutdoorWorld`／`LightSentenceEffects`）和 Chapter I（`ChapterRoom`）中的同名句也按一次性句处理，各自实现完成判定与停靠；开阔地的灯在室外原本的漂浮表现可作为停靠后的 idle。
- 同步修改：对应规则资源的 `kind` 改为 Command；`SentenceRuntime` 完成后归库（`FinishCommand`）需要各效果上报完成；V5.md 中「light move floor ……拆句暂停，重组恢复」等持续句描述改为一次性句描述。

**验收**
- [x] 每条指向型句触发时反馈一次，完成后归回词库，物体停在终点。
- [x] 执行中拆句暂停、重组继续；关灯暂停、开灯继续。
- [x] 完成后灯不再自己漂走，关灯后也仍停着；组 `light move` 后灯才离开。
- [x] 室内、开阔地、Chapter I 中表现一致。

**回归**：FloorImpactChecks（light move floor 由持续改为一次性，相关断言更新）、OutdoorChecks、ChapterChecks、SentenceRuntimeChecks、ObservationChecks。

### A3 作用范围：所在空间 + 视野方向空间 + 距离上限，成立时锁定【已定，试用】

**规则**
- 一句话作用的空间 = **玩家所在空间** + **视野方向上、通过开放通路连通且看得见的空间**；超过距离上限的空间即使连通也不算。
- **开阔地是一整个空间【已定】**：玩家在开阔地控制东西时，默认整个开阔地都受作用，不按视野或距离切分。
- **判定时机**：持续句在成立那一刻判定一次并**锁定**，之后玩家转头或移动都不改变作用对象；一次性句在执行开始时判定（与现状一致）。
- **两边都看不见时**（例如在通道里侧身）：退回「较近的那一端空间」（与现有通道 on light 选侧规则一致）。
- **例子**（用户给出）：站在房间 2→3 的通道，面向房间 2、仍看得见白门时，可以控制白门；面向房间 3 时，控制房间 3 的门。
- **【建议】** 组句时，被选中的目标（门或灯）轻微高亮，让玩家提前知道这句会作用到哪里。

**实现建议**
- 空间单位沿用 `PassageJourney.LightSpace`（Chamber／FirstPath／SphereRoom／ReturnPath／FarRoom）并扩展。新增一个统一的范围解析，例如 `SentenceScope.Resolve(player)`，返回空间掩码：
  1. 玩家所在空间。
  2. 与之连通（沿用 `ConnectedLights()` 的通路开闭判断：入口显露、白门开度、教学房出口球是否堵住）的空间中，空间入口或代表点落在视野锥内（建议水平半角约 60°，可调）、且距离不超过 `maxScopeDistance`（建议 30 米，可调）的空间。
  3. 若第 2 步为空且玩家在通道内：取较近一端。
- `CognitionState.Group`（或 `SentenceRuntime` 已应用效果）记录该句锁定的空间掩码；各效果处理器与物体只响应「锁定了自己所在空间」的句子：
  - `MotionEffect.CanMove` 与各物体读取 `HasEffect` 的地方（`DriftingLight`／`RoomLightGathering`、`MoveTutorialRoom` 预置运动、`CorridorStars`、`ChamberLight`、白门往复 `UpdateDoorSwing`、`PassagePlatform`）改为带空间判断的查询，例如 `HasEffect(signal, space)`。
  - `LightStateEffect`、`DoorStateEffect` 用同一解析取目标。
  - 照亮句（light world／door／floor／light）：**【建议】** light world 保持全局（曝光是画面效果），其余三条按空间。
- 循环泛星只在「当前空间受该句影响」时播放：玩家离开锁定空间后星芒停止，这本身告诉玩家这句在这里不起作用。

**跨章节：句子清空【已定】**
- 玩家跨章节（Intro ↔ 开阔地 ↔ Chapter I，经室外白门、地洞／楼梯过场、金色画框、镜子）时，**组句区的所有句子清空**：词全部归回词库，相当于全部拆句。持续句停止，执行中的一次性句停在当前进度；不会跟随玩家进入新章节，也不会在新章节重新生效。
- 词库本身（已收集的词、快照、笔记）保留，与现状一致。
- 需要修改 V5.md「开阔场景与 Chapter I」中两处已确认描述：「本次运行往返保留词库……」保留；「持续运动句仍可随玩家进入新空间，拆句暂停」改为「跨章节时句子清空」；「穿框不会向另一端重发命令」随之失效，可删除。
- 实现：在 `OutdoorPassage` 的各个跨越入口（`Travel`、`CrossThreshold`、`LeaveChapter`、`EnterChapter`、批次 3 的白闪到达）统一调用一个清空方法（复用 CognitionState 现有的归库逻辑，按拆句处理，不播放消融），再 `ChangeSpace()`。

- **Chapter I 内部（本轮采用 A3-Q2 默认）**：默认主展廊与花房按两个空间处理（与现有「花房与走廊分别响应灯光开关」一致），套用同一视野 + 距离规则。

**验收**
- [x] 在房间 3 组 `light move door`：只作用房间 3 的灯和充能门；第一段通道白门不受影响。
- [x] 在房间 2→3 通道面向房间 2 且看得见白门时组 on door：作用白门；面向房间 3：作用充能门；侧身两边都看不见：作用较近一端。
- [x] 持续句成立后转身、走到另一个空间，作用对象不变；离开锁定空间后循环泛星停止，回来后恢复。
- [x] 视野中很远但连通的空间不受影响（距离上限生效）。
- [x] 在开阔地组句，整个开阔地受作用。
- [x] 跨章节（白门、地洞／楼梯、画框、镜子）后组句区为空，词全部在词库；离开前生效的持续句已停止。
- [x] Chapter I 展廊与花房按 A3-Q2 默认。

**回归**：ChamberChecks、JourneyChecks、PassageChecks、SentenceRuntimeChecks、ObservationChecks、SurfaceArtChecks、OutdoorChecks 中涉及范围的用例；预计部分旧断言需按新规则更新，在结果中逐条说明。

### A4 门按空间取目标；门不能自由移动【已定】

- `DoorStateEffect.Indoor` 现在在室内任何位置都返回白门；`MotionEffect` 对 `door move` 不看空间。改为用 A3 的范围解析取门：房间 3 → 充能门；第一段通道、教学房、以及在 2→3 通道面向房间 2 且看得见白门时 → 白门。
- `door move light`、`door move floor`：**室内外都无效**（门有固定转轴锚点和固定动向，不能自由移动），只出无星弱涟漪。需要移除 `OutdoorWorld.CanMove` 里 `DoorLightMove`、`DoorWorldMove` 的分支及 `Outdoor/DoorSentenceEffects` 中对应的追灯／落地行为，并更新 OutdoorChecks。
- `floor move light`、`floor move door`：室内无效；室外保持现状。

**验收**
- [x] 房间 3 的 on／off door 和 door move 不再驱动第一段通道白门。
- [x] door move light／door move floor 在室内和开阔地都只有弱涟漪，室外门不再追灯或落地。

### A5 floor 收集规则【已定】

- floor 快照只取可移动方地（`PassagePlatform`）；确认其它来源不会写入 floor 快照。
- 未从可移动方地收集 floor 前，其它地面不显示 floor 收集符，点击不收词。
- 收集后，所有房间的地面（大方盒、教学房、房间 3、通道路面、Chapter I、花房、开阔地）都显示 floor 收集符，点击给已知词反馈（0.9 秒提示、不锁移动）。
- 实现：`PassageJourney.WordAt`、`ChamberLight` 点击路由、`OutdoorWorld.LookedAtWord`、`ChapterRoom` 中对普通地面返回 floor 前加 `board.State.Knows("floor")` 判断；`PassagePlatform` 不受限制。

**验收**
- [x] 新开局：先瞄房间地面无 floor 收集符；收集方地后，各房间地面都有。

---

## 批次 B：充能门（依赖批次 A）

### B1 门、门龛与房间改动

**【已定】** 门在房间 3 墙上；**没有把手**；**向上移动打开**；**凹进墙面**，像洞穴里的门；不要壁画，提示词写在门上；不用地洞。

**【建议】**
- 新组件 `Scripts/V5/IntroEnding/ChargeDoor.cs`，挂在 P5 场景配置对象上，引用 `PassageJourney`；在 `journey.Build` 之后于 `journey.Root` 下生成门龛、门扇、三条充能槽和门后楼梯入口。房间墙体由 `PassageJourney.Build` 用 `RoomBlock` 生成，远端墙需参照 `Door wall`／`Door lintel` 的分块方式开洞，并在门楣上方预留门扇升起的空腔。
- **位置**：房间 3 远端墙（正对入口）水平居中（【待确认】B1-Q1）；门洞净宽高沿用 3.6×3.8 米，门底与地面齐平。
- **门龛**：一层倒 U 形浅凹口，墙面到门扇深 0.30 米，左右和顶部斜面内收 0.20 米，底部地面齐平连续；与远墙共用白色哑光材质和房间补光。房灯渐亮、熄灭和门扇升降时，门龛随远墙一起明暗，充能细线沿左、顶、右三边延伸；不加岩石贴图或颜色。
- **门扇**：整块约 0.4 米厚的白色石板，无把手。正面居中浅刻 light、move、door 三个符号（与新词序一致），沿用壁画的 Blender 正交投影烘焙法线 + 凹槽遮蔽方法（`ArtSource/V5/GravityMural/build_mural.py` 流程），符号随门扇升降。
- **去掉壁画**：`FloorImpactSurface` 增加 `showMural`（默认 false），关闭壁画取样，不再回退加载 `V5/Murals/GravityMuralNormal`。烘焙脚本与源文件保留；不再使用的贴图按 AGENTS.md 清理原则核实引用后再处理。
- **停用地洞**：`FloorDirectedMotion` 增加 `openHoleWhenGrounded`（默认 false）；`light move floor` 照常落地但不开洞。`OutdoorPassage` 的地洞窗口与 `TransferLamp` 保留不触发。
- 门扇与门龛在 `journey.WordAt` 返回 `door`。

**验收**
- [x] 房间 3 远端墙：单层倒 U 形凹口、无把手石板门、门上浅刻三个符号、三条空充能槽；房间里没有壁画；全部灯落地也不开洞。
- [x] 门关闭时不可通过；瞄准门显示 door 已知词反馈。

**回归**：FloorImpactChecks（开洞、坠落、壁画相关步骤改为验证「不开洞／无壁画」，不得直接删除）、OutdoorChecks（调用 `OpenHole()` 的部分改为新路线或标为过时并说明）。

### B2 充能计算

**【已定】** 左、右、上三条；亮灯靠近上涨、远离衰减；衰减本身是提示；**充满后能量会掉**；light light、light door 作为 bonus 加快充能并让颜色偏橙红；off world 关盒时暂停。

**【建议】**
- 每条独立计算，只计**正在发光**的灯：灯到该条线段距离 `d < chargeRadius`（默认 2.2 米）贡献 `w = brightness × (1 − d/chargeRadius)`；`charge += Σw × chargeRate × dt`，`Σw=0` 时 `charge −= decayRate × dt`（默认 0.12/秒）。默认参数下需要较多灯贴近才能充满（用户要求：为了开门时的视觉效果）。
- 三条同时 ≥1 为「已充能」：整圈充能槽填满发亮，门上三枚刻字渐亮；**门不自动打开**。
- 能量会掉的情形：灯被关掉；灯被 `light move`、`light move floor` 等运动句带离门边。（灯停靠后不会自己散开，见 A2。）
- **颜色**：默认微暖白；`light light` 或 `light door` 生效时 `chargeRate` 乘以加成系数（建议各 ×1.5，可调），充能光越亮越偏向橙红色。
- 冻结：`no move`（`Time.timeScale=0`）自然冻结；off world 关盒期间一切暂停，只保留 idle 漂浮动画（灯的停靠漂浮、符号浮动）继续播放。
- 填充表现：按三处储能总量，从左下向上、沿顶边向右、再沿右边向下连续填充，衰减时原路回退；空槽为细黑描边凹槽，填充为 HDR 自发光并参与 Bloom。

**验收**
- [x] 不用句子时，灯随机飘过门边，对应条短暂上涨又回落。
- [x] 灯聚齐后几秒内三条充满、刻字渐亮，门不自动开。
- [x] 关灯或用 light move 把灯带走，充能衰减。
- [x] light light／light door 生效时充能更快、光色偏橙红。
- [x] no move、off world 期间充能不变；off world 期间 idle 漂浮继续。

### B3 聚集与停靠（light move door）

**【已定】** 沿用现有聚集逻辑，加漂浮，聚在门框外缘，不挡玩家；房间 3 的镜头基本对准门；站在房间 2→3 通道时也允许（按 A3 的视野规则）；第一段通道白门附近使用时，星芒朝白门聚集。

**【建议】**
- `RoomLightGathering` 增加门目标接口（如 `IGatherTarget`），`ChargeDoor` 实现；目标由 A3 的范围解析决定。
- 槽位：三条充能条拼成 U 形折线，按灯的稳定 id 等距分配；目标点 = 折线点 + 墙法线 0.6 米（房间一侧）+ 向门洞外侧 0.5 米，不进入门龛和门洞。保留 `DirectedMotion.ApproachStep` 排队避让与地面夹取。
- 到达后进入「停靠」（A2），叠加小幅漂浮（沿墙面 0.08–0.15 米、0.25–0.4 Hz、按 id 错相）。全部到达 → 句子完成归库。
- 充能门门洞对灯封住（同 `DriftingLight` 现有入口封口），灯不会飘上楼梯。
- 镜头：`SentencePresentation` 中 `light.move.door` 的 shot 在房间 3 对准充能门。
- **第一段通道星芒聚向白门**：`PassageJourney.CorridorMotion` 现在只响应 `light move`；为 `CorridorStars` 新增朝白门门框周围聚集的运动，沿用星体自己的移动与闪烁表现。同为一次性句，全部到位后归库。

**验收**
- [x] 房间 3：灯沿 U 形停在门框外、轻微漂浮，玩家可正面走进门洞；镜头对准门；完成后句子归库。
- [x] 通道 2→3 面向房间 3 组句：同上。
- [x] 第一段通道白门附近组句：星芒朝白门聚集，完成后归库。
- [x] 门开着时灯不飘进楼梯。

### B4 门的控制、失力下落、敲晕重生

**【已定】**
- **充能完成前无法控制门**：on door／off door／door move 对充能门只出无星弱涟漪，充能条短暂闪一下。
- 已充能后：`on door` 门向上升起（收进门楣上方墙内）；`off door` 落下；`door move` 在关闭↔完全升起之间往复，表现同第一段通道白门（on／off 与往复共存时先完成开关动作再恢复往复，拆句停在当前高度，重组继续）。玩家可以趁门升起时穿过。
- **失力下落**：门开着（或升起中）时能量掉到不满，门失去支撑，**先有一点前摇**，再加速落下。
- **敲晕**：门下落时玩家站在门洞下，玩家被敲晕：仰头 + 与落入深渊一致的黑屏过渡，然后重生。
- **重生位置**：按玩家身体中心在门的哪一侧决定，在那一侧重生（楼梯侧或房间侧）。
- **句子状态按类型**：持续句保持生效（例如把 light move door 换成了 light move 导致能量掉，light move 继续生效）；一次性句完成即结束（例如 off door 让门落下，门关上后这句已完成归库）。

**【建议】**
- 门的升降：上升约 2.2 秒缓起缓停；正常下落（off door、往复）约 1.6 秒；失力下落：前摇约 0.4 秒（充能条闪烁、门扇轻微下沉约 3 厘米并带很小的抖动、播放吃力的低频音效钩子 `strainClip`），之后按重力加速落下（约 0.5 秒落到底），落地时地面双圈涟漪 + 方形粒子（沿用灯撞地表现）和 `slamClip`。
- 敲晕判定：门扇下沿碰到玩家胶囊体顶部时触发（正常下落和失力下落都适用）。门继续落到底。
- 敲晕表现：锁移动与视角 → 视角在约 0.3 秒内向上仰约 35° → 复用可移动方地坠落深渊的「视野渐暗、全黑重生、从落点淡入」过渡 → 在对应一侧的重生点（房间侧：门前 2 米面向门；楼梯侧：第一级平台面向楼梯上方）淡入。
- 敲晕不重置任何句子和谜题状态（灯、充能、门位置保持）。
- 门的升降与往复受 `no move` 冻结；off world 时暂停。

**【待确认】**
- B4-Q1：失力落下后，充能重新充满时门是否自动回到之前的开启状态？（默认：不自动，仍需 on door）

**验收**
- [x] 未充能时三种门句都无效，只有弱涟漪和条闪。
- [x] 已充能 on door／off door／door move 表现正确，可趁升起穿过。
- [x] 门开着时把灯带走：前摇后加速落下；玩家在门下会被敲晕并在身体所在一侧重生。
- [x] off door 落下时站在门下同样被敲晕；重生后 off door 已归库，持续句仍生效。

---

## 批次 C：白色阶梯与过场（依赖批次 B）

### C1 楼梯与纯白渐入

**【已定】** 门后向上的楼梯；在楼梯中点附近渐入纯白空间；楼梯白色 + 黑线描边。

**【建议】**
- 楼梯从门洞向外直线上行，宽 4 米，踏高 0.25、踏深 0.45，共 64 级，总升高 16 米，保持原踏步坡度。碰撞用斜面（走起来顺），视觉为踏步；每级踏步为白色，沿棱边按统一的屏幕线宽描黑线，相邻面共用一条描边；无扶手、无侧墙，悬在黑色虚空里。
- 基准高度 `whiteStartHeight` 为 0 时按全程 40% 至 60% 渐变，中点半白，可配置（【待确认】C1-Q1）。
- `WhiteAscent` 组件按玩家沿楼梯的实际行程驱动：相机背景黑→白、雾色→白并略增密度、环境光提高、楼梯纯白模式、房间外壳淡出；下楼可逆；统一持有并恢复这些设置。
- 上段淡入极淡的地平线。楼梯两侧与上方的菱形星芒在各自位置轻轻浮动。黑底白星，白阶和星芒周围保留轻微柔光，短按白星可收集 light；白底黑星，只作景物。每级踏步的棱边在白底上保持清晰。

**验收**
- [x] 上楼经过中段时在等效爬升 3.2 米内平滑变白，楼梯只剩黑描边；下楼可逆；白空间里没有发灰的表面。

### C2 小台与 light 符号

**【已定】** 顶端小台，立体 light 符号，点击点亮，有神圣／天堂感。

**【建议】**
- 小台约 3×3 米、高 0.6 米；中央小方柱，符号悬浮其上缓慢自转、上下浮动。符号由 `ArtSource/V5/Symbols` 的 light 原图在 Blender 挤出（厚约 0.12 米、高约 0.9 米），导出到 `ArtAssets-3D/V5/`；未点亮时白色 + 黑描边。
- 瞄准显示交互提示；左键短按触发，经 `ChamberLight` 点击路由接入，不收词、不开面板。
- 点亮演出约 4 秒：锁定玩家 → 吸气式微暗 → 符号描边由黑变白、HDR 自发光升至约 6、强 Bloom、转向玩家 → 两圈涟漪从立体符号中心向外扩散，每圈带出几颗黑色菱形星芒，32 颗白色菱形微粒（`CorridorDiamond` 同形）缓慢上升。镜头保持玩家点击时的朝向。音效钩子：`igniteClip`（建议与组句反馈 E5 同音高）、`swellClip`、`flashClip`。

**【待确认】** C2-Q1：点亮除了演出，是否带来能力？（默认：只是演出与过场）

### C3 白闪回到开阔地

**【已定】** 像眼睛被强光刺到，再慢慢加载回开阔地。

**【建议】**
- `WorldEntry` 下新增与 `blackBackground` 同级的白色全屏 Image。
- 时间线：白闪约 0.8 秒（白色 alpha 0→1，独立高优先级 Volume 的 `postExposure` 0→+4、Bloom 拉高）→ 全白保持 0.6 秒并切到室外平视落点 → 复原约 3 秒（白 1→0，曝光 +3→0），然后恢复控制。
- 把 `OutdoorPassage.Travel()` 中激活室外的部分提成共用方法（如 `ActivateOutdoor(position, rotation, velocity)`），供 `Travel()` 和新的 `ArriveFromAscent()` 共用；室外已由 `Prepare()` 预加载。
- 落点（【待确认】C3-Q1）：默认室外白门前方几米、背对白门、面向开阔地。

**验收**
- [x] 点亮后白闪，无黑帧无卡顿地到开阔地，以平视到达，约 3 秒恢复控制；室外持续句、灯、音乐状态与现有地洞路线出门后一致。

### C4 一次性状态与回访

**【已定】** 之后再进出这扇门，只加载普通路段，不再出现纯白世界。

**【建议】**
- 运行时状态对象（如 `IntroEndingState`，由 `WorldEntry` 持有，不用静态变量）：`AscentCompleted`。没有存档系统，只在本次运行内保留（【待确认】C4-Q1）。
- `AscentCompleted` 后：楼梯为黑色虚空中的白色楼梯，无渐白、无地平线、无小台演出；顶端为普通门槛，穿过时用 `CrossThreshold` 式短黑场（约 0.2 秒）到室外白门前（【待确认】C4-Q2：回访路线）。
- 从室外白门回来：`ReturnIndoors()` 的落点由地洞旁改为充能门房间侧门前、面向房间 3，过渡改为平视短黑场。
- 充能门在回访时仍遵守 B 批次的全部规则（能量会掉、需 on door 等）。

**验收**
- [x] 回访时没有纯白段落；往返多次一致；停止 Play 再开回到初始状态。

---

## 1. 待确认汇总

| 编号 | 问题 | 默认 |
|---|---|---|
| A3-Q2 | Chapter I 内主展廊与花房是否按两个空间处理？ | 是，套用视野 + 距离规则 |
| B1-Q1 | 充能门在哪面墙 | 远端墙居中 |
| B4-Q1 | 失力落下后重新充满，门是否自动重开 | 不自动，仍需 on door |
| C1-Q1 | 「方盒顶面」指房间 3 顶（8 米）还是大方盒顶（约 16 米） | 本次验收按楼梯行程的 50% 过渡，可调 |
| C1-Q2 | 高处美术（已定） | 极淡地平线、黑白切换的浮动星芒 |
| C2-Q1 | 点亮 light 符号是否带来能力 | 只是演出 |
| C3-Q1 | 白闪后开阔地的落点 | 室外白门前、面向开阔地 |
| C4-Q1 | 一次性状态是否跨重启保留 | 只在本次运行内 |
| C4-Q2 | 回访路线 | 楼梯照旧、无白色段，顶端短黑场到室外白门 |

---

## 2. 通用工程要求（每批都适用）

- 新脚本放 `Scripts/V5/IntroEnding/`（或按功能细分），新资源放各类别下的 V5 子目录；不改 V1–V4；不改 Build Settings；不保存验证截图。
- 每批新增或更新检查脚本（建议 `Editor/IntroEndingChecks.cs`，按批次分方法），在全新 P5 Play 中运行并读 Result。
- 受影响的旧检查按新规则更新，不为通过而删除或跳过；在结果中逐条说明。
- 每批完成后更新 `Assets/Docs/V5.md` 对应段落（删掉失效描述，如地洞出口、壁画、旧词序、旧作用范围），本 C2C 文档不作为长期设计来源。
- 未验证独立构建时在结果中说明。
