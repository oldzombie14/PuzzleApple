# V4 编辑与资源

## 构建与上传

- 后续发布场景应显式使用 `P4_Systems`、`P4`、`ChapterI`、`ChapterI_Garden`，不包含旧 P1。两版使用 PC 渲染质量及深度纹理；Web 使用 WebGL 2、Gzip 与解压回退。V4 UI 文本引用内嵌 Handodle 字体，避免 Web 依赖系统字体。门聚合透明材质加入 PlayerSettings 预加载，保留运行时透明变体。
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

- 场景：`Assets/Scenes/V4/` 下的 P4、ChapterI 与 ChapterI_Garden；Build Settings 以 P4_Systems 为入口，旧 P1 条目保留。ChapterI 沿用原中转场景 GUID。旧 Prototypes 菜单含过时场景名，不用它定位当前场景。
- 脚本、Prefab、规则在 `Assets/Scripts/V4/`、`Assets/Prefabs/V4/`、`Assets/GameData/V4/Opening/`；其它资源按类别置于对应 V4 目录。
- 退出确认框：`WorldEntry.ShowExitConfirmation` 使用原生 Canvas／Image／Button，并从当前词库取 positive／negative 的符号。`SentenceEffectManager` 记录已请求的退出句组，取消后不因无关编辑重复请求。
- 常驻系统：P4_Systems 保存 Player、World picture style、EventSystem、World Entry、V4Interface 与 Session scene manager；Loading overlay 使用原生 Canvas/Image，右下角进度条可直接编辑。SessionSceneManager 处理加载、出生点、输入锁和卸载；门只提交请求。SceneEntry 的 Temporary Words 默认为空，填写场景临时词 ID 后离场统一清理。OpeningFlow 位于 P4 的 Intro flow，场景物体引用随内容卸载，WorldEntry 使用常驻 CognitionBoard，不依赖 Intro 存活。
- 菜单与游戏内退出：`Prefabs/V4/WorldEntry.prefab` 与场景 World Entry。菜单颜色在 Enter World／Exit 的 Symbol Button / Colors；入场后的颜色由 WorldEntry 的 In-world exit palette 管理，Day Exit Color／Day Exit Highlight 调白天默认与悬停色，Exit Saturation 调整体饱和度，Ground Look Angles 调朝下提亮角度。夜色取 NightEnvironment.nightFogColor，Night Exit Lift／Night Highlight Lift 调正常与悬停亮度。
- 面板：`Prefabs/V4/UI/V4Interface.prefab` 及同目录控件模板；场景 `V4Interface/Opening HUD` 编辑眼睛、准星与收词符号。OpeningFlow 绑定流程，Player 独立控制器调整行走学习。场景 Sentence Effects 上的 SentenceEffectManager 转发已仲裁的句子效果，LightSentenceEffects／DoorSentenceEffects／WorldSentenceEffects 负责各域行为。
- 重复交互：OpeningFlow 的 Known Eye Alpha、Known Symbol Alpha、Known Symbol Duration、Known Symbol Scale（起始／结束倍率）调整淡眼睛与居中放大淡出反馈；按同类词是否已收集判断。
- 收词范围：OpeningFlow.Interaction Distance 是普通实体的统一入口；ThresholdDoor.Collection Distance 为门的显式特例。太阳由 OpeningFlow 读取当前天空材质的 `_SunDirection`、`_Night`，按 DaySky／NightSky 的圆盘外沿点积 `.9995` 识别，共用 light 的收词与重复提示；瞄准太阳时把实体遮挡射线延伸到世界相机远裁剪面。若修改 shader 的太阳圆盘尺寸，需同步此识别阈值。
- Ground 点击地面由 OpeningFlow 识别 Sentence Effects / WorldSentenceEffects.ground，收词动画结束入库，保存首次静态快照。GroundCollectionChecks.Run() 检查真实射线收词及重复点击。规则与镜头资源保留原 GUID 和历史文件名，运行时词 ID 为 ground；CognitionSignal 的 World 移动枚举保留序列化值。
- 词与规则：`GameData/V4/Opening/OpeningCatalog.asset` 及 Words／Rules。
- 灯：场景 `Opening Light Field/OpeningLightField` 调整生成；`Lamp State/OpeningLight` 保留全局昼夜与共享外观来源，不显示大测试 cube。运行时小灯复用原生 Renderer、Rigidbody、Collider 和 LightMotion，忽略玩家碰撞，共用一份运行时材质，最近的有限小灯启用无阴影点光。
- 分布参数：Seed、Cell Size、Lamps Per Cell、Scale Range（立方体边长）、Clearance Range（底部离地高度）在重新进入 Play 后对全场一致生效；已有记录不会因调参数而洗牌。Spawn Radius、Recycle Buffer、Max Active、Spawn Budget、Refresh Interval、Move Speed 和点光预算支持运行时调整。选中管理器显示加载／回收范围线；默认场景只保存管理器，进入 Play 才生成实体。Reveal Duration 与 Reveal Interval 控制加载淡入时长和逐灯间隔（当前淡入 0.65 秒）；点光照明名额切换另以 0.35 秒平滑过渡；淡入共用透明材质及逐灯属性块，完成后恢复全局不透明材质。
- 环境：`Night Environment` 集中昼夜参数，可用 Preview Night；`Cloud Layer/CloudLightCookie` 调整真实高度、贴图、移动和全局雾；Direction Delay 控制方向响应延迟，Direction Smoothing 控制转向耗时系数（越小越快），两者由普通行走云跟随与 Ground move 共用；云平面与方向光 Cookie 共用 `Textures/V4/CloudOpacity.asset`。
- 画面：WorldPictureStyle 与 `Materials/V4/WorldPicture.mat` 控制世界画面处理；UI 独立。参数以资源为准，不重复记录试调历史。
- 门：`Prefabs/V4/FishThreshold.prefab`，ThresholdDoor 调降落／落地／交互，ThresholdFish 保留鱼群逻辑，P4 中鱼对象暂时停用；场景子物体 Threshold starlight 的 ThresholdStarlight 管星光及 Door/Floor/Move 循环，三种符号统一从原贴图笔画内部随机覆盖采样，并向笔画内部轻度收拢，保留不规则疏密与横向偏移，每个符号 50 点；保持时轻微浮动，星点大小与闪烁相位各异；独立材质/Shader 为 ThresholdStarlight；P4 门框使用原有 ThresholdStone 材质与亮度；只调整星光。Reveal Duration 控制首次渐亮（默认 .8 秒），Symbol Size 为等比适配范围（默认 3.3×4.65，包含门框内留白）；聚合使用每颗星点独立的固定起终点线性插值；散开以平滑权重从符号位置混合到各星点当前时刻的 Drift 位置，结束时完全接回原 Idle 轨迹与相位，符号间持续正常游动；Stagger Fraction 控制错峰出发窗口占阶段时长比例（当前 .2），到达时间也有小幅差异。Trail Duration 当前 1.8 秒，按时间回溯采样 24 段，Trail Opacity（当前 .3）与 Trail Width（当前 .36）独立控制暗尾迹，尾迹使用低亮冷色柔散材质分支，不含亮核与星芒；到位后减弱并抑制静止叠亮。Star Size 当前 .52，星光 HDR 为 (24,27,32)，亮核、星芒及光晕已加宽，待试玩。自由游动采用独立相位的立体路径并绕开框实体；Suspended Radii 控制悬空聚集范围，Landed Radii 控制落地扩散范围（当前 6×2.7×4），Landing Spread Duration 默认 4 秒（落地散开与增亮平滑过渡）；首次 LandingCompleted 立即结束符号循环，从中断位置平滑过渡至落地 Idle，Landed Brightness 当前 2 倍，通过 Renderer 属性块提高 HDR 亮度，不修改共享材质；关闭门时继续遵循运动暂停与渐暗；初次显现后先游动再开始提示。当前试调：聚合 8 秒、保持 4 秒、散开 5.5 秒、符号间游动 5 秒、整轮后游动 9 秒（首次显现后也先游动 9 秒）；散开阶段结束后保留完整游动间隔，再开始下一次聚合；子节点 Landing mist 的 ThresholdMist 调整雾透明度、流速与淡入，材质为 Materials/V4/ThresholdMist.mat。DoorStateEffects 控制 on/off 发光与鱼暂停，星光读取同一状态和亮度，雾读取落地且 on 的通行状态；DoorPassage 检查实际跨门面并加载下一关。
- 快照备注：P4_Systems 与 V4Interface 模板的 Note text 使用 PlayerNotes 中文笔记字体，关闭 Align By Geometry，以固定行高对齐；文字矩形在对称内边距基础上上移 3 个参考像素，补偿该字体的基线，使原生光标在空白、输入和清空时保持居中。
- 无快照标记：WordLibrary 的 Empty Snapshot 与 `Memory print/No snapshot/Corner to corner` 原生 Image，SnapshotPlaceholder 适配框尺寸。
- 快照：WordLibrary 首次收灯／门时保存世界相机画面；Move Memory 引用 `GameData/V4/Opening/MoveMemory.asset` 和 `Textures/V4/MoveMemory.png` 帧图集。`MoveMemoryRecorder.Run()` 在全新 P4 Play 中使用实际首次行走逻辑重新录制，不注入输入设备；日常不必重录。
- 句子表现：Sentence Effects / IntroPresentation 的 Shots 绑定 `GameData/V4/Opening/IntroShots/` 下有世界表现的完整句式的独立资源（Rule 绑定具体规则）。LightMove 调【灯 move】，WorldMove 调【Ground move】，LightWorldMove 调【灯 Ground move】；每份配置可调 Aim（Focus／Pitch Only／Adaptive Light Ground／Pitch To Target）、Target（含 Light Group／Horizon）、Target Offset（世界坐标偏移）、Angle Offset（目标朝向的俯仰／左右偏移，X 负值抬头）、Enter／Hold／Exit Duration、Letterbox Height。Follow Target 控制轻微跟随，Snap Above Angle 控制大角度瞬切，Target Viewport Y 控制画面高度，Elevated Group Height 与 Light Group Radius 控制灯群取景。Pitch Only 的 Angle Offset X 为相对当前视角的 pitch，忽略 yaw；Adaptive 的同一偏移仅用于低处灯群。Sky 目标同时使用 Sky Minimum Elevation 与 Target Viewport Y，保留更高的当前仰角；Horizon 根据视场角和 Target Viewport Y 取景。Height Based Pitch Speed 按灯群高度在 Low／High Pitch Speed 间插值。所有镜头转向只插值 yaw/pitch，roll 固定为 0。Exit Duration 仅控制黑边收起，不再回转；正常结束与中断均由 FirstPersonController.AdoptCurrentView 接续当前朝向。未绑定配置直接应用效果，不播放 cutscene；无表现和退出句无配置。SentenceFeedback 控制演出前的涟漪和星芒，仍完整展示后才收面板；黑边到位后应用效果。星芒引用 `Prefabs/V4/UI/SentenceSparkle.prefab` 与 `Textures/V4/SentenceSparkle.png`，使用第 31 层独立相机渲染到 UI；该层不供世界物体使用。IntroPresentationSetup 仅增量绑定，不重建 UI。
- 悬空提示：Sentence Effects / SkyInscription 等 OpeningLightField 首次生成实体后显现；调 Fade Duration、Viewing Distance、Elevation、Angular Width 和昼夜颜色。NightEnvironment 仅在最后单条提示阶段等待淡出。Night Ink 与 Night Opacity 独立调整夜晚初始提示；材质 `Materials/V4/SkyInscription.mat` 只取符号 Alpha。IntroHintProgress 使用 Progress Key 对应的 PlayerPrefs 整数位掩码，完成时立即 Save，启动时读取；Play 中按 X 或组件右键“清除本地天空提示完成记录”删除该键并立即重建初始提示。
- ChapterI：直接编辑 Scenes/V4/ChapterI.unity。Unfolding arrival box 的 FoldingRoom 配置各墙／顶板铰链、展开时长及同步下沉深度；根节点位置控制初始埋深，当前 Y 为 -0.25 米，下沉与最长面板展开同时结束；Arrival mirror 为 Unity 原生镜框与镜面，按 Arrival 的反方向定位后墙，使用共享 PlanarMirror 和 ChapterVocabulary，场景不引用导出的隐藏镜子模型。OpeningFlow 的 Exploration Only 仅启用放置词收集，不运行 Intro 的行走发现、环境或语法演出。SceneEntry 独立 Play 自动加载 P4_Systems，不自动模拟 Intro 收词进度。mirror 快照为 `Textures/V4/MirrorMemory.png`，在闭合房间正面预拍并调用 PlanarMirror.RenderForCamera 保留反射，绑定 P4_Systems 的 WordLibrary.Vocabulary Images；收词读取预拍资源，词条用 FitInParent 完整显示。
- 宽敞展廊当前试设：走廊宽 21 米、层高 22 米，长度保持 54 米；活动盒体长 14、宽 16、高 16 米，后墙固定。左右画框及实时入口随侧墙各外移 3 米、整体上移 0.4 米，花房对应洞口与门槛同步升高；入口两侧设 0.35 米低踏步以保持步行穿框。镜框整体等比放大 15%，中心高约 2.36 米，仍位于后墙中轴；出生点与收词距离保留。上述为待试玩比例，不作为最终尺寸。
- 左画框与花房出口使用 ScenePortal；Forward 指向各自房间内部，孔径匹配。SessionSceneManager 异步预加载邻接内容，原生 Camera／RenderTexture 提供视差画面，CharacterController 穿越平面后映射位置与朝向并切换活动场景。花房内容位于独立空间偏移，往返期间两 Scene 保留；普通场景切换卸载之前的邻接内容。相机 shader 尚未加入。
- Blender 导出模型、纹理与源稿副本位于 ArtAssets-3D/V4/ChapterI/，Unity 材质位于 Materials/V4/ChapterI/。走廊合并墙网格停用，由可编辑 Cube 墙体补齐端墙并留下画框开口；展台在走廊远端。ChapterISetup 是一次性组装工具，场景存在时拒绝重建，日常直接编辑场景。
- 展台各部件围绕底座落地点等比调整，底座高约 1 米，展品处于玩家眼高附近；碰撞体随缩放同步。中转结构与走廊墙面共用 CorridorWall，沿用 V1 展墙的低光滑度，配合中性白底色与各朝向一致的柔和洗墙光。FBX 禁止导入灯光与相机；场景灯光为独立 Unity Light，Unity showcase light 提供展品补光。GalleryFloor 与 CorridorFloor 均使用中性哑光白色；ChapterI 与花房的 SceneEntry 引用 NeutralPicture，消除共享画面材质的青绿阴影染色，回到 Intro 自动恢复默认材质。走廊墙地使用 CorridorWall／CorridorFloor，引用 ChapterCorridorAmbient.shader（由 V3 CorridorAmbient 适配到世界 X 方向），关闭走廊顶灯，仅保留 Unity showcase light。花房照明见下方瓶花说明。SceneEntry 的 Eye Height 在 ChapterI 与花房设为 2 米，离开后恢复系统默认眼高和碰撞体。画框模型的局部 Z 轴对应世界高度；下沿完整露出，左右框外尺寸一致，画面和两端净开口均约 8.60×12.21 米。走廊双侧画框整体抬高 0.70 米，外框下沿高出地面约 0.71 米；花房双向门槛等高上移，两端均由三级踏步接近，花房门洞保留原净开口。右侧壁灯位于大画框上方，灯口对准画面。
- 日常编辑原生组件、场景和 Prefab，不重跑初次搭建脚本覆盖手调布局。

## 旧资源依赖（无需读取旧设计）

旧资源保留原位，目录版本名不意味着继承玩法。

- V4 符号独立放在 `Sprites/V4/`：Positive（正状态）、Negative（负状态）、World（世界）、Floor（地面）、Move（移动）、Light（灯）、Door（门），另有 Eye（词条模板原 I 符号）；收词眼睛继续使用共享 viewer 图标。旧版符号保留原位，V4 场景、Prefab、词资源使用独立副本；Floor 使用 `Sprites/v2_260929/Light.png` 的图案副本，保留 V4 GUID。仍复用 `Fonts/V3/PlayerNotes.fontsettings`、`Materials/V3/MemoryPrint.mat`、`UI/V3/SaveSquare.png`。
- 初次迁移工具 OpeningSetup 引用 V3Interface／V3Presentation，WorldEntrySetup 与 OpeningSetup 的符号路径已指向 V4；这些工具依赖不是采用旧规则的依据。
- General 及以上资产按共享资源处理；V4 专属修改前核对影响，必要时复制至 V4 再改引用，保留 `.meta` 和 GUID。
- ProjectHelper 原位保留。其旧来源字符串中的 GameDesign.md／Progress.md 指整理前内容，现位于 `Archive/` 同名文件；未改设计数据、审核指纹和历史。仅用户要求追溯 V3 时使用。

## 验证

- GrammarChecks.Run() 为无场景副作用的语法与整句冲突检查。SentenceRuntimeChecks.Run()、GatheringChecks.Run()、PassageChecks.Run() 分别在新的 P4 Play 中检查效果、聚集与目标选择、实际角色跨门进入 ChapterI；各读 Result。不注入输入设备，结束后退出 Play 恢复场景。
- SentenceEffectsSetup 为增量配置工具，日常只编辑原生组件／资源；不重跑初次开场搭建工具。

- OpeningChecks.Run(false/true) 分别覆盖先走与先点灯，读取 OpeningChecks.Result；完整测试状态见进度。
- LightFieldChecks.Run() 在新的 P4 Play 中检查小灯收词射线、玩家穿过、面板开关时移动、开关与移动共存、拆句、区域回收与返回恢复，读取 LightFieldChecks.Result；不注入键鼠设备，不等同完整人工操作验收。
- 覆盖本次变化涉及的实际玩家路径、先后顺序、共存、拆句及碰撞；规则成立或测试数量不能代替行为、画面与手感验证。
- `IntroPresentationChecks.Run()` 在全新 P4 Play 检查入场、首次快照、预录片段、UI 拖词回调、句子反馈、冲突接管、拆句不演出和镜头恢复；通过 Unity EventSystem 调用实际拖拽处理，不代表完整人工鼠标验收。
- IntroCameraChecks.Run() 在全新 P4 Play 检查 pitch 不偏 yaw、门对准与轻跟随、低／高灯群取景、门在身后及最终朝向保留、控制器接续；取景截图写入 Temp/V4Camera。
- SkyInscriptionChecks.Run() 在全新 P4 Play 检查入场隐藏、方块灯生成后在背后显现、持久完成记录、方位与大小、初始提示昼夜显示、单条提示白天显示、退出取消和清档后已有句子不误记；不修改玩家真实完成记录。
- 测试工具异常与产品缺陷分开处理；未复现的用户问题保留未解决状态。

- ReviewRoundChecks.Run() 在全新 P4 Play 检查视野内最低灯、初始开启门落地通行、门／灯阻挡抬升、聚类速度、三种移动冲突的弱涟漪与变淡、不演出，以及提示持久记录回读。

- 第 2 轮实现定位：ThresholdDoor.Release Landing Tolerance 调自由移动拆句贴地容错；门在物理帧持续水平朝向玩家，DoorPassage 在进入触发区时记录穿越平面，避免转向改变跨门判定。DoorStateEffects.Fade Duration 调门灯渐变；OpeningLight 与 NightEnvironment 的 Transition Duration 调灯／环境过渡。WorldSentenceEffects.Feedback Proximity 调近地弱反馈范围；DoorSentenceEffects.TargetClearance 使用门框整体范围判断近灯。ReviewRoundTwoChecks.Run() 检查本轮新增行为，PassageChecks.Run() 检查转向门的实际穿越。

- 第 3 轮：DoorAssembly 使用约 5.5 秒的连续光丝成形；Duration 可调，Stitch Size 控制错峰采样。同一条 LineRenderer 路径从框外直线经分散的框边中段落点延伸为表面直线，成对向两端铺开以覆盖完整框边；单根牵入与铺线时长为总时长的 32%（约 1.76 秒），错峰开始仍保证 85% 前铺完，尾端收至落点后停止，已铺部分留下发光，不另画表面纹理。Resources/DoorWeaveThread 统一绘制空中及落地光丝；Resources/DoorWeave 的实体显现与光丝退场由 DoorAssembly 统一控制：60%–85% 进度随铺线淡入实体，85% 最后一批线铺完时恢复完整原门框材质，光丝在最后 15% 淡出；光丝沿每段框边全长铺开并覆盖两侧轮廓，整体按真实未缩放时间推进，读取原材质自发光时遵循其启用状态；Thread Count（48）、Thread Reach（1.1–2.8 米）与 Thread Width（.04 米）可调，具体取值待试玩。完成或停用时恢复原材质并清理临时细线、对象与材质。两个 Shader 随 Resources 打包。OpeningFlow.RememberDoor 使用独立正交相机和临时图层 30 拍摄 640×640 正面留白门快照，调用后立即恢复图层；实体尺寸不变。NightEnvironment.MatchedStarSpeed 将当前云漂移速度换算成同取景中心的星空角速度。ReviewRoundThreeChecks.Run() 验证本轮反馈顺序、起步／加载、显现与快照，并输出 Temp/V4Review 供画面检查。

- Playtest 调整：OpeningFlow 的 Walk After Floor Distance 控制先收 Move 时收 Floor 后的步行距离（暂取 1.2 米）；OpeningLightField 的 Initial Clear Radius 控制解锁时周围无灯半径（暂取 20 米）。EntryMemory.png 为按快照框约 2:3 比例排版的黑底菜单示意图，导入关闭 NPOT 缩放，on/off 使用 FitInParent 完整等比显示，WorldMemory.png 为居中透明图标，位于 Textures/V4 并绑定 WordLibrary。PlaytestPolishChecks.Run(true/false) 分别在全新 P4 Play 检查两种收词顺序。

- SessionSceneChecks.Run() 在独立 ChapterI Play 检查临时词清理与重收、连续转场、输入恢复、单一会话、旧场景卸载与退出句；测试中的临时词清单仅存在于 Play，不保存到场景。
- ChapterIChecks.Run() 在全新 ChapterI Play 检查镜子射线收词、入库时机、展开、镜面渲染、门外视差、真实角色跨框与往返状态、花朵漂浮及邻接场景卸载。截图写入 Temp/ChapterI；不等同于人工键鼠试玩与观感验收。

## 花房瓶花

- 左侧画框通往 ChapterI_Garden。瓶花使用 FloatingGarden/Source~/PortraitStudy/PortraitGarden_PaintingCapture.blend 拍画时可见的对象，导出为 FloatingGarden/PortraitBouquet.fbx。Layout 源稿、正交拍摄与油画参考图保留在 PortraitStudy 中。
- 花房净宽 26 米、进深 27 米、净高 19 米。入口洞口、门槛、三级踏步及传送平面沿用展廊匹配尺寸。墙、顶、地面和内侧踏步共用 GardenVelvet，深棕底色在斜视时略微提亮，近看可见细绒纹理。
- 室内版以 Blender 拍画相机为基准，所有网格共用一个正面比例，整组沿相机视线增加约 19% 的纵深。每根枝条只按中心定位到对应深度，网格本身按原形等比放大。最低处会穿地的花叶沿画面上方抬至地面以上，保留其在拍画相机中的深度。花、叶、枝分别使用 FloatingFlowers；叶片的升降较小、周期更长，翻摆幅度略大，枝条最轻。花瓶固定在地板下，瓶沿露出约 0.90 米，并带网格碰撞。花叶使用源稿配色。入口侧的暖色聚光灯投射实时软阴影，另一侧用较弱的暖光补亮。
