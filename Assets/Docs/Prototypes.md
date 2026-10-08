# 平级原型与资源边界

## 当前入口

| 原型 | 场景 | 用途 |
| --- | --- | --- |
| V1 | `Assets/Scenes/V1/P1.unity` | 原英文认知流程，保留对照版本 |
| V2 | `Assets/Scenes/V2/P2.unity` | 相同流程、句式和效果，词汇以 sprite 呈现 |
| V3 | `Assets/Scenes/V3/P3.unity` | 密闭开场 → 周期门 → 苹果／天平压力板大厅；隐藏房间含苹果与解析器 |

三者为平级探索方向，不表示后一个替代前一个。旧模型和 renderer 名称中的 `SpaceStudyV2` / `SacredSpaceV2` 是历史美术方案编号，与本表无关。

以表中实际文件为准，直接双击场景打开。旧 `PuzzleApple > Prototypes` 菜单仍写旧场景名，暂不使用；V3 另有 `PuzzleApple > V3 > Open playable scene`。当前 Build Settings 仅收录 P1，本轮未改构建入口；没有运行时选关菜单。

## 文件归属

- `Scripts/V1` 保留原玩法与命名空间；`Scripts/V2` 是独立复制，使用 `PuzzleApple.V2` 命名空间。包括控制器、面板、认知状态、交互、教学及适用的 Editor 检查。不要跨版本挂同名组件。
- `Prefabs/V1` 与 `Prefabs/V2`、`GameData/V1` 与 `GameData/V2` 分别独立。V2 的场景、功能 prefab、SO、专用材质与被咬苹果派生网格均使用新 GUID，并重定向内部引用。
- `Scripts/General` 只放不依赖某个玩法的内容：帧率设置、镜面和准星渲染、基础第一人称控制器、通用场景入口及美术辅助工具。旧控制器含教学和认知逻辑，仍归 V1/V2。
- 模型源文件、纹理与空间派生网格位于 `ArtAssets-3D/General`。环境材质在 `Materials/General`；各版本的道具材质在 `Materials/V1`、`V2`、`V3`。
- 字体、交互图标、shader、管线设置分别位于所属类别的 `General`。Input Actions 在 `Input/General`。
- V3 玩法使用 `Scripts/V3`、`GameData/V3`、`Sprites/V3` 和 `Shaders/V3` 独立副本／新增资源，命名空间 `PuzzleApple.V3`。专属玩家控制器、面板、词库、场景行为和 Editor 检查不引用 V1/V2 玩法；静态空间、苹果与镜子源模型、通用镜面渲染继续共享只读资源。新门／灯在 `Prefabs/V3`，天平及派生入口墙／分离托盘网格在 `ArtAssets-3D/V3`；钥匙保持隐藏，旧出口门仅在 P3 停用。

共享资源仍会影响所有引用它的场景。移动场景中的模型实例只影响该场景；若要改变某一原型的模型源文件、环境材质、shader 或渲染配置，先在对应版本目录复制该资产，再替换该场景引用。不要直接修改 `General` 来做某个原型的专属实验。

## V3 开场编辑（2026-10-06）

- 主入口：`Assets/Prefabs/V3/OpeningPrototype.prefab`，P3 中为 `Opening Prototype`。`Rooms` 中的原生 Transform、Light、Renderer、Collider 可直接调整开场几何、灯光和布局。主线位于 `Main route`，隐藏流程位于 `Hidden room route`（Z=-8 周围）。
- `Hidden room route` 的 WordAnalyzer 配置解析时长、配方及单占位落点 `Intake — one place`。`Analyzer` 使用 `ArtAssets-3D/V3/Analyzer.fbx`；`Analysis screen` 是原生世界空间 Canvas，结果图标、箭头、加号可直接编辑。`GameData/V3/Opening/AppleAnalysis.asset` 指定苹果拆出 red / round；该目录下 `Words` 独立保存本轮新词，不改旧原型词汇。
- 天平的 WordTransformTarget 指定可染色 Renderer 和红色值；形变仍按物件逐项扩展。隐藏房间检查入口为 `PuzzleApple > V3 > Checks > Hidden route (Edit Mode)`。
- 根节点 `OpeningRoom`：Movement Speed 控制镜子速度，Apple Movement Speed 控制苹果速度（暂 0.9）；自由移动方向只由实际 Collider 碰撞改变，不再使用随机转向间隔或虚拟边界。另有来／去距离、交互距离与 Player Spawn；OpeningMirrorView 保留镜像平面引用。
- 嵌套 `V3Interface` 的 `V3Presentation`：`Self Blackout Duration` 为纯黑等待（1 秒），`Self Fade In Duration` 为【我】线性淡入（1.5 秒），`Self Hold Duration` 为完整显示（0.3 秒）。不再使用 Self Lead Time。
- 词汇标准图位于 `Assets/Textures/V3/Vocabulary/<wordId>.png`，属于词汇图像资源。OpeningPrototype 内 WordLibrary 的 `Vocabulary Images` 按词汇 ID 引用图片，可直接替换 Texture2D；每词固定一张，不随物体变色／变形，正／负为亮灯／灭灯，【我】留白。当前 10 张为原运行时拍摄流程导出的 512×640 原图，显示滤镜仍由现有 UI 材质处理。当前场景与 Prefab 已移除全部 11 个快照相机，收词不再拍摄或临时切换灯状态。
- `Reflected view camera` 供右半屏使用；左半屏保留 Player/Main Camera。根节点 OpeningMirrorView 的 `Self Plane` 引用 `Doorway mirror/Player view reflection plane`：穿过镜子中心的左右对称平面，使两个视角看同一面镜子。`Plane` 仍是物体镜像到对面墙的前后平面，两者分开调整；没有可见的玩家／镜像身体网格。
- `WordLibrary` 的笔记保存只在当前运行内有效；不读取或修改旧 PlayerPrefs。每次启动新 playtest 为新笔记会话，保存按钮与两处悬停行为不变。
- 开场材质在 `Assets/Materials/V3/Opening/`（Plaster、Floor、Ceiling）；后处理在 `Assets/Settings/V3/OpeningProfile.asset`，由 P3 的 `Global Volume` 引用。优先编辑这些原生资源，不通过运行时代码重设颜色。
- 新句法 SO 在 `Assets/GameData/V3/Opening/`。`OpeningObject.destinationSlots` 为后续物件指定落点。规则的 Exclusion Key 表示互斥行为。
- 后续收词节奏在 V3Presentation 的 `Word Fade In Duration`（0.3）、`Word Fly Duration`（0.6）、`Word Display Duration`（总时长 1.6）调整。原 `Rooms/Main next room` 及苹果标记停用，由 `Main route` 接替。
- `Main route` 下的 `Gallery and corridor` 为搬回的空间；周期门位于世界 Z=12，天平中心 Z=22。`Pressure plate` 为低矮按钮，`Balance - V3` 的两个 Tray slot 为落盘点。MainRoute 组件可调门周期、压力板行程、平衡等待时间与出口引用。
- `Main route/Balance completion`：可复用 CutscenePlayer 管理固定 Camera、Cinema overlay 上下黑边、玩家／面板锁定和恢复；原生 PlayableDirector 播放 `Animations/V3/BalanceCompletion.playable`，AnimationTrack 绑定 MainRoute 的 Animator，`BalanceCompletion.anim` 驱动按钮与出口进度。默认 5.5 秒，0.65 秒后压下、1.8 秒后开启出口。更换 Timeline、相机和完成事件即可配置其它过场，无需再写一套播放器。
- `RoomLighting` 管理大厅 Light、ReflectionProbe、可见灯面及光束；`Hall lighting boundary` 前方的局部环境光和环境反射随开灯状态恢复。自然周期与开门途中为暗，正状态全开后 1.2 秒渐亮。关灯时通过缓存的运行时材质关闭 URP 环境反射兜底，开灯恢复原材质，不改共享美术材质。
- `Balance spotlight`、`Balance edge light`、`Exhibit soft fill`、`Rear wall wash 1/2`（当前强度 0）、`Gallery dim fill`、`Corridor light`、`Doorway bounced light` 均为原生 Light。后两者负责从明亮开场往大厅的距离衰减和门口弱补光。`Hall exposure volume` 的 Volume 停用，BoxCollider 环境光范围覆盖走廊和大厅。LocalAmbientProbe 按整块 Renderer 中心提供 SH 基线，不能用于长墙／地面的逐点渐变；墙地过渡由上述原生灯负责。Neutral camera baseline 维持统一中性相机效果，镜头进出不切曝光。
- `Exhibit light shaft` 为无碰撞的原生立方体边界，材质 `Materials/V3/Opening/ExhibitLightShaft.mat` 使用局部深度散射 Shader；相机需启用 Depth Texture。光束长度、半径和方向须与主聚光一致（当前长 11.64 米、全角 38°）；Density 调整可见程度。`Ceiling light aperture` 为白色顶灯面。原生照明独立运行，重烘焙反射时暂隐藏光束后恢复。
- 大厅独立材质为 `Materials/V3/Opening/HallPlaster`、`HallStone`、`HallBrass`；`Gallery reflection` 引用本大厅的 `ArtAssets-3D/V3/Generated/MainHallReflection.exr`。更改布局或照明后可用原生 Reflection Probe 烘焙流程更新；日常不用重跑 MainHallLook.Apply，以免覆盖手调值。
- 苹果 OpeningObject 的 Reflection Plane 指向所在房间的镜像中线；自由移动碰撞使用真实 Collider。门、灯、天平用 OpeningCollectible 声明收词；【等于】兼作天平目标词。镜子 WordTransformTarget 的 Surfaces 仅引用镜框，不包含 Planar Mirror 镜面。
- 镜面 `PlanarMirror` 的 `Max Reflection Depth` 默认 2，控制有限层镜中镜；每层独立相机／贴图，内层使用当前反射视角，达到上限后用中性镜面收尾。
- 当前 P3 不再包含 `Legacy V3 — Reserved (inactive)`。清理前完整场景保存在 `Assets/Scenes/V3/Archive/P3BeforeVocabularyImages.unity`，已解开 Prefab 实例以独立保留当时层级，含 X=100 的 134 个旧对象及 11 个快照相机；材质、脚本等资源仍共享，归档用于查看／恢复场景，不是冻结整个项目版本。更早的 `P3BeforeOpening.unity` 也保留。请单独打开归档查看，不叠加到当前场景。
- 拍摄辅助脚本收在 `Assets/Scripts/V3/Authoring/`，仅供归档中的拍摄节点使用；旧版图像移动到 `Assets/Textures/V3/Archive/Vocabulary/` 并保留 GUID。当前场景保留玩家、镜像分屏、显示输出、成功过场、重量不足过场共 5 个相机；Rooms／Main route／Hidden room route 原有分组沿用。
- 日常调整直接编辑 Prefab，不重跑搭建代码。编辑模式检查：`PuzzleApple > V3 > Checks > Opening collection (Edit Mode)`、`Main route (Edit Mode)`；后者在独立预览物理场景验证机关及三种解法顺序，不运行持续画面渲染。旧体验检查适用于备存场景。

## V3 界面编辑（2026-10-06）

入口：双击 `Assets/Prefabs/V3/UI/V3Interface.prefab`，或在 P3 Hierarchy 选择 `Opening Prototype/V3Interface`。非 Play 模式下直接修改原生 UI 组件；场景实例的改动可通过 Overrides → Apply 保存回 Prefab。无需运行生成菜单。

| 要调整的内容 | Prefab 中的位置与组件 |
| --- | --- |
| 面板宽度、背景 | Panel：RectTransform 的横向锚点、Image |
| 字符列表间距、行高、符号大小 | Panel/Content/Vocabulary rail 下的 VerticalLayoutGroup；VocabularyEntry.prefab 的 LayoutElement、Library symbol |
| 右侧整组位置和快照高度 | Panel/Content/Word memory/Centered memory stack：RectTransform、VerticalLayoutGroup；Memory print 的 LayoutElement 使用剩余高度 |
| 快照与输入框间距 | 同一组的 Gap before note：LayoutElement |
| 输入框高度、文字居中、字体 | Semantic note 的 LayoutElement、InputField；Note text 的 Text |
| 保存按钮、关闭按钮 | Save row/Save note 与 Close row/Close memory：Button、Image、RectTransform |
| 悬停文字、背景与内边距 | Panel/Content/Semantic tooltip：Text、Image、HorizontalLayoutGroup、ContentSizeFitter；位置跟随字符上沿属于运行逻辑 |
| 组句字符、句柄与划线样式 | 同目录 WordToken、SentenceGroup、SentenceHandle、ChalkStroke Prefab；词宽根据符号比例、句长与拖拽状态计算 |
| 收词、交互图标与世界提示 | V3 HUD 下的 Image/Text；World display 为开场和分屏的显示层 |

WordLibrary 仅负责词库、快照、笔记与悬停行为；不再创建静态控件或重写文字对齐。组句、拖拽、开合面板、收词动画与分屏仍需要运行逻辑。VocabularyScrollRect 保留“词少时短滑块可空滑”的已确认特殊行为，其余布局优先用原生组件。PlayerNotes.fontsettings 使用系统中文字体及回退列表，可在 Text 上直接替换字体；未验证所有目标平台字体可用性。

## V2 替换符号

在 `Assets/GameData/V2/Cognition/Words/` 中选择词汇 SO，将 `Symbol` 指向用户提供的 sprite。当前用户素材位于 `Assets/Sprites/v2_260929/`，沿用该目录；纹理导入为 Sprite，尽量裁去无用透明边距。

`Id` 用于认知判断，应保持稳定；`Display Text` 保留为语义调试信息，不在 V2 的词块和收词动画中显示。句式、词序和冲突规则仍与 V1 相同。2026-09-29 按用户要求接入以下素材，consume/open 补齐后已全部替换：

| ID | 当前 sprite |
| --- | --- |
| i | I.png |
| no | Negative.png |
| move | Move.png |
| consume | consume.png |
| apple | Apple.png |
| open | open.png |
| door | Door.png |

Equal、Light、Mirror、Positive 等未使用素材保持原样。旧七个内置占位已从 V2 词汇配置解除引用；它们属于 Unity 内置资源，无需删除引擎资源。

面板、单词拖拽预览、整句拖拽和收词动画统一读取 Symbol，保持图片纵横比。词块宽度、句子容纳判断和吸附提示基于 sprite 尺寸；首次符号居中，句首方块、无效句划线与冲突淡化沿用原流程。替换正式素材后仍需检查透明边距、视觉重心和实际辨识度。

## 后续设计边界

已确认：V2 只换词汇呈现；V3 采用不同解谜流程和语法。2026-09-30 已按讨论制作 V3 首版，包含词库复用、多义词、三轴镜像、本质化、等同与本质化先成立者优先，以及天平两条解法。设计与本版默认值见 [游戏设计](GameDesign.md)，已验证范围见 [当前进度](Progress.md)。

当前按三个编号命名、共享静态空间资源、保留 V1 为构建首场景。V3 完成后的短通道仅用于验证通关，未制作下一关。
