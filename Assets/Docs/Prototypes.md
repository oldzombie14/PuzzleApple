# V4 编辑与资源

## 构建与上传

- 发布场景显式使用 `P4`、`V4_NextRoom`，不包含旧 P1。两版使用 PC 渲染质量及深度纹理；Web 使用 WebGL 2、Gzip 与解压回退。V4 UI 文本引用内嵌 Handodle 字体，避免 Web 依赖系统字体。门聚合透明材质加入 PlayerSettings 预加载，保留运行时透明变体。
- `Build/` 位于 Unity 项目文件夹的上一层。本次输出为 `Web/2026-10-25-01` 与 `Win/2026-10-25-01`，对应 ZIP 放在各自 Web／Win 目录。Web ZIP 根目录含 `index.html`，可在 itch.io 选 HTML 项目并勾选浏览器运行；建议 1280×720、允许全屏。Windows ZIP 解压后启动 `PuzzleApple.exe`，保留同目录数据与 DLL。
- 本次 Web 导出后的 `index.html` 已替换为随嵌入区域缩放的简洁加载页，像素倍率设为 1；后续 Unity 默认模板重建会覆盖此页，需保留或重新应用其自适应设置。网页版需通过 HTTP(S) 打开，不直接双击 HTML。

## 临时验收工具

- `2026-10-24-01.vbs` 启动本地 HTML 编辑器，自动保存到同目录 `2026-10-24-01.json`，无需选择或连接文件。网页是验收记录，不直接写回 Unity 参数。
- 句子列使用词资源引用的原始游戏符号显示，不加中英文释义；页面采用黑白灰配色。后续开发可能继续使用 HTML 表格并支持新增句式；新增句式功能尚未实现。
- 第 1 轮起按“提交本轮 → 开始下一轮”记录。`history` 保存各轮完整快照；新轮继承表现与镜头设置；上一轮“通过”或“暂不复测”的条目默认“暂不复测”，其余默认“待测”，意见清空，状态仍可手动修改，原轮记录不可覆盖。`legacySnapshot` 保留升级前原始填写内容，不视作已提交轮次。
- 镜头只在涟漪带星且有演出时适用。`cameraIntent` 记录可编辑的大方向：观察对象手写、多对象选择说明、通用取景方式、转向方式、是否跟随和补充说明；特殊条件取景用自定义说明。选项允许组合，提示不阻止保存；实施前必须结合具体条件和修改意见核对，遇到真实冲突再向用户确认，不把选项直接当成可执行配置。
- 数值参数只读，来自当前 IntroShots 资源；修改数值的要求写在右侧意见中。每轮提交由服务端读取并保存 `history[].cameraParameters` 快照，历史显示提交时参数，旧轮未记录时明确标注。旧 `camera` 字段中的填写值保留为旧版记录，不视为已应用的游戏参数。转向角度上限已从代码及资源移除。
- 每句可“添加一种情况”，选择通用或手写条件；条件留空按通用展示，各情况分别保存表现、反馈及镜头。`rows[].cases` 为新增情况记录；旧记录未覆盖，历史可切换情况只读查看。条件是验收描述，不自动执行游戏规则；同句多条条件有重叠时需在描述中明确适用范围。
- 每种情况可勾选“冲突时变淡”并填写“冲突原因”，分别保存为 `conflictDim` 与 `conflictReason`；与句子涟漪反馈独立，可组合记录，随本轮自动保存及归入历史。第 1 轮已将游戏内冲突反馈接为弱涟漪与变淡，不播放星芒或演出。
- `OpeningLight.Brightness`／`CurrentState` 为实际亮度及开／关／中间状态，`TargetOn` 为目标，`TransitionEnabled` 控制继续渐变。拆句调用 HoldCurrentState；灯和环境保留连续值，动态加载灯共享全局亮度。门的开关结果保留，ThresholdMist.Visibility 单独保留渐变值。`StateRetentionChecks.Run()` 在全新 P4 Play 检查中途拆句、反向操作、冲突接管、门与雾及演出中返回编辑。

按需阅读。行为要求只见 [当前设计](GameDesign.md)，开发状态只见 [进度](Progress.md)。

## 入口与编辑位置

- 场景：`Assets/Scenes/V4/P4.unity`。Build Settings 以 P4 为入口，另含 V4_NextRoom（加法加载的下一关空房间）；旧 P1 条目保留。旧 Prototypes 菜单含过时场景名，不用它定位当前场景。
- 脚本、Prefab、规则在 `Assets/Scripts/V4/`、`Assets/Prefabs/V4/`、`Assets/GameData/V4/Opening/`；其它资源按类别置于对应 V4 目录。
- 退出确认框：`WorldEntry.ShowExitConfirmation` 使用原生 Canvas／Image／Button，并从当前词库取 positive／negative 的符号。`SentenceEffectManager` 记录已请求的退出句组，取消后不因无关编辑重复请求。
- 菜单与游戏内退出：`Prefabs/V4/WorldEntry.prefab` 与场景 World Entry。菜单颜色在 Enter World／Exit 的 Symbol Button / Colors；入场后的颜色由 WorldEntry 的 In-world exit palette 管理，Day Exit Color／Day Exit Highlight 调白天默认与悬停色，Exit Saturation 调整体饱和度，Ground Look Angles 调朝下提亮角度。夜色取 NightEnvironment.nightFogColor，Night Exit Lift／Night Highlight Lift 调正常与悬停亮度。
- 面板：`Prefabs/V4/UI/V4Interface.prefab` 及同目录控件模板；场景 `V4Interface/Opening HUD` 编辑眼睛、准星与收词符号。OpeningFlow 绑定流程，Player 独立控制器调整行走学习。场景 Sentence Effects 上的 SentenceEffectManager 转发已仲裁的句子效果，LightSentenceEffects／DoorSentenceEffects／WorldSentenceEffects 负责各域行为。
- 重复交互：OpeningFlow 的 Known Eye Alpha、Known Symbol Alpha、Known Symbol Duration、Known Symbol Scale（起始／结束倍率）调整淡眼睛与居中放大淡出反馈；按同类词是否已收集判断。
- Ground 点击地面由 OpeningFlow 识别 Sentence Effects / WorldSentenceEffects.ground，收词动画结束入库，保存首次静态快照。GroundCollectionChecks.Run() 检查真实射线收词及重复点击。规则与镜头资源保留原 GUID 和历史文件名，运行时词 ID 为 ground；CognitionSignal 的 World 移动枚举保留序列化值。
- 词与规则：`GameData/V4/Opening/OpeningCatalog.asset` 及 Words／Rules。
- 灯：场景 `Opening Light Field/OpeningLightField` 调整生成；`Lamp State/OpeningLight` 保留全局昼夜与共享外观来源，不显示大测试 cube。运行时小灯复用原生 Renderer、Rigidbody、Collider 和 LightMotion，忽略玩家碰撞，共用一份运行时材质，最近的有限小灯启用无阴影点光。
- 分布参数：Seed、Cell Size、Lamps Per Cell、Scale Range（立方体边长）、Clearance Range（底部离地高度）在重新进入 Play 后对全场一致生效；已有记录不会因调参数而洗牌。Spawn Radius、Recycle Buffer、Max Active、Spawn Budget、Refresh Interval、Move Speed 和点光预算支持运行时调整。选中管理器显示加载／回收范围线；默认场景只保存管理器，进入 Play 才生成实体。
- 环境：`Night Environment` 集中昼夜参数，可用 Preview Night；`Cloud Layer/CloudLightCookie` 调整真实高度、贴图、移动和全局雾；Direction Delay 控制方向响应延迟，Direction Smoothing 控制转向耗时系数（越小越快），两者由普通行走云跟随与 Ground move 共用；云平面与方向光 Cookie 共用 `Textures/V4/CloudOpacity.asset`。
- 画面：WorldPictureStyle 与 `Materials/V4/WorldPicture.mat` 控制世界画面处理；UI 独立。参数以资源为准，不重复记录试调历史。
- 门：`Prefabs/V4/FishThreshold.prefab`，ThresholdDoor 调降落／落地／交互，ThresholdFish 管鱼群；子节点 Landing mist 的 ThresholdMist 调整雾透明度、流速与淡入，材质为 Materials/V4/ThresholdMist.mat。DoorStateEffects 控制 on/off 发光与鱼暂停，雾读取落地且 on 的通行状态；DoorPassage 检查实际跨门面并加载下一关。
- 无快照标记：WordLibrary 的 Empty Snapshot 与 `Memory print/No snapshot/Corner to corner` 原生 Image，SnapshotPlaceholder 适配框尺寸。
- 快照：WordLibrary 首次收灯／门时保存世界相机画面；Move Memory 引用 `GameData/V4/Opening/MoveMemory.asset` 和 `Textures/V4/MoveMemory.png` 帧图集。`MoveMemoryRecorder.Run()` 在全新 P4 Play 中使用实际首次行走逻辑重新录制，不注入输入设备；日常不必重录。
- 句子表现：Sentence Effects / IntroPresentation 的 Shots 绑定 `GameData/V4/Opening/IntroShots/` 下有世界表现的完整句式的独立资源（Rule 绑定具体规则）。LightMove 调【灯 move】，WorldMove 调【Ground move】，LightWorldMove 调【灯 Ground move】；每份配置可调 Aim（Focus／Pitch Only／Adaptive Light Ground／Pitch To Target）、Target（含 Light Group／Horizon）、Target Offset（世界坐标偏移）、Angle Offset（目标朝向的俯仰／左右偏移，X 负值抬头）、Enter／Hold／Exit Duration、Letterbox Height。Follow Target 控制轻微跟随，Snap Above Angle 控制大角度瞬切，Target Viewport Y 控制画面高度，Elevated Group Height 与 Light Group Radius 控制灯群取景。Pitch Only 的 Angle Offset X 为相对当前视角的 pitch，忽略 yaw；Adaptive 的同一偏移仅用于低处灯群。Sky 目标同时使用 Sky Minimum Elevation 与 Target Viewport Y，保留更高的当前仰角；Horizon 根据视场角和 Target Viewport Y 取景。Height Based Pitch Speed 按灯群高度在 Low／High Pitch Speed 间插值。所有镜头转向只插值 yaw/pitch，roll 固定为 0。Exit Duration 仅控制黑边收起，不再回转；正常结束与中断均由 FirstPersonController.AdoptCurrentView 接续当前朝向。未绑定配置直接应用效果，不播放 cutscene；无表现和退出句无配置。SentenceFeedback 控制演出前的涟漪和星芒，仍完整展示后才收面板；黑边到位后应用效果。星芒引用 `Prefabs/V4/UI/SentenceSparkle.prefab` 与 `Textures/V4/SentenceSparkle.png`，使用第 31 层独立相机渲染到 UI；该层不供世界物体使用。IntroPresentationSetup 仅增量绑定，不重建 UI。
- 悬空提示：Sentence Effects / SkyInscription 入场时在玩家背后显现；调 Fade Duration、Viewing Distance、Elevation、Angular Width 和昼夜颜色。NightEnvironment 等 SkyInscription 淡出后才开始变暗。Progress Key 暂仅作为兼容标识，不读写 PlayerPrefs；Play 中按 X 或组件右键“清除本地天空提示完成记录”可清除本次提示记录；运行时立即重建未划线的初始提示。材质 `Materials/V4/SkyInscription.mat` 只取符号 Alpha，保证白天暗色、夜晚浅色。
- 日常编辑原生组件、场景和 Prefab，不重跑初次搭建脚本覆盖手调布局。

## 旧资源依赖（无需读取旧设计）

旧资源保留原位，目录版本名不意味着继承玩法。

- Ground 另复用 `Sprites/v2_260929/Light.png`；V4 直接复用 `Sprites/V3/` 中 Apple、Door、I、Move、Negative、Positive、red；`Fonts/V3/PlayerNotes.fontsettings`、`Materials/V3/MemoryPrint.mat`、`UI/V3/SaveSquare.png`。
- 初次迁移工具 OpeningSetup 引用 V3Interface／V3Presentation，WorldEntrySetup 引用旧符号路径；这些工具依赖不是采用旧规则的依据。
- General 及以上资产按共享资源处理；V4 专属修改前核对影响，必要时复制至 V4 再改引用，保留 `.meta` 和 GUID。
- ProjectHelper 原位保留。其旧来源字符串中的 GameDesign.md／Progress.md 指整理前内容，现位于 `Archive/` 同名文件；未改设计数据、审核指纹和历史。仅用户要求追溯 V3 时使用。

## 验证

- GrammarChecks.Run() 为无场景副作用的语法与整句冲突检查。SentenceRuntimeChecks.Run()、GatheringChecks.Run()、PassageChecks.Run() 分别在新的 P4 Play 中检查效果、聚集与目标选择、实际角色跨门和空房间；各读 Result。不注入输入设备，结束后退出 Play 恢复场景。
- SentenceEffectsSetup 与 NextRoomSetup 为本次增量配置工具，日常只编辑原生组件／资源；不重跑初次开场搭建工具。

- OpeningChecks.Run(false/true) 分别覆盖先走与先点灯，读取 OpeningChecks.Result；完整测试状态见进度。
- LightFieldChecks.Run() 在新的 P4 Play 中检查小灯收词射线、玩家穿过、面板开关时移动、开关与移动共存、拆句、区域回收与返回恢复，读取 LightFieldChecks.Result；不注入键鼠设备，不等同完整人工操作验收。
- 覆盖本次变化涉及的实际玩家路径、先后顺序、共存、拆句及碰撞；规则成立或测试数量不能代替行为、画面与手感验证。
- `IntroPresentationChecks.Run()` 在全新 P4 Play 检查入场、首次快照、预录片段、UI 拖词回调、句子反馈、冲突接管、拆句不演出和镜头恢复；通过 Unity EventSystem 调用实际拖拽处理，不代表完整人工鼠标验收。
- IntroCameraChecks.Run() 在全新 P4 Play 检查 pitch 不偏 yaw、门对准与轻跟随、低／高灯群取景、门在身后及最终朝向保留、控制器接续；取景截图写入 Temp/V4Camera。
- SkyInscriptionChecks.Run() 在全新 P4 Play 检查入场立即在背后显现、仅本次运行的完成记录、方位与大小、提示替换和先淡字后入夜；不修改玩家真实完成记录。
- 测试工具异常与产品缺陷分开处理；未复现的用户问题保留未解决状态。

- ReviewRoundChecks.Run() 在全新 P4 Play 检查视野内最低灯、初始开启门落地通行、门／灯阻挡抬升、聚类速度、三种移动冲突的弱涟漪与变淡、不演出，以及提示不写入持久记录。

- 第 2 轮实现定位：ThresholdDoor.Release Landing Tolerance 调自由移动拆句贴地容错；门在物理帧持续水平朝向玩家，DoorPassage 在进入触发区时记录穿越平面，避免转向改变跨门判定。DoorStateEffects.Fade Duration 调门灯渐变；OpeningLight 与 NightEnvironment 的 Transition Duration 调灯／环境过渡。WorldSentenceEffects.Feedback Proximity 调近地弱反馈范围；DoorSentenceEffects.TargetClearance 使用门框整体范围判断近灯。ReviewRoundTwoChecks.Run() 检查本轮新增行为，PassageChecks.Run() 检查转向门的实际穿越。

- 第 3 轮：DoorAssembly 使用原生 ParticleSystem 与临时透明材质实现约 2.4 秒的表面粒子聚合，完成后恢复原材质并清理临时对象。OpeningFlow.RememberDoor 使用独立正交相机和临时图层 30 拍摄 640×640 正面门快照，调用后立即恢复图层；实体尺寸不变。NightEnvironment.MatchedStarSpeed 将当前云漂移速度换算成同取景中心的星空角速度。ReviewRoundThreeChecks.Run() 验证本轮反馈顺序、起步／加载、显现与快照，并输出 Temp/V4Review 供画面检查。
