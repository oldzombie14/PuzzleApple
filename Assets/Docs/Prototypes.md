# V4 编辑与资源

按需阅读。行为要求只见 [当前设计](GameDesign.md)，开发状态只见 [进度](Progress.md)。

## 入口与编辑位置

- 场景：`Assets/Scenes/V4/P4.unity`。Build Settings 以 P4 为入口，另含 V4_NextRoom（加法加载的下一关空房间）；旧 P1 条目保留。旧 Prototypes 菜单含过时场景名，不用它定位当前场景。
- 脚本、Prefab、规则在 `Assets/Scripts/V4/`、`Assets/Prefabs/V4/`、`Assets/GameData/V4/Opening/`；其它资源按类别置于对应 V4 目录。
- 菜单：`Prefabs/V4/WorldEntry.prefab` 与 WorldEntry 组件。
- 面板：`Prefabs/V4/UI/V4Interface.prefab` 及同目录控件模板；场景 `V4Interface/Opening HUD` 编辑眼睛、准星与收词符号。OpeningFlow 绑定流程，Player 独立控制器调整行走学习。场景 Sentence Effects 上的 SentenceEffectManager 转发已仲裁的句子效果，LightSentenceEffects／DoorSentenceEffects／WorldSentenceEffects 负责各域行为。
- 词与规则：`GameData/V4/Opening/OpeningCatalog.asset` 及 Words／Rules。
- 灯：场景 `Opening Light Field/OpeningLightField` 调整生成；`Lamp State/OpeningLight` 保留全局昼夜与共享外观来源，不显示大测试 cube。运行时小灯复用原生 Renderer、Rigidbody、Collider 和 LightMotion，忽略玩家碰撞，共用一份运行时材质，最近的有限小灯启用无阴影点光。
- 分布参数：Seed、Cell Size、Lamps Per Cell、Scale Range（立方体边长）、Clearance Range（底部离地高度）在重新进入 Play 后对全场一致生效；已有记录不会因调参数而洗牌。Spawn Radius、Recycle Buffer、Max Active、Spawn Budget、Refresh Interval、Move Speed 和点光预算支持运行时调整。选中管理器显示加载／回收范围线；默认场景只保存管理器，进入 Play 才生成实体。
- 环境：`Night Environment` 集中昼夜参数，可用 Preview Night；`Cloud Layer/CloudLightCookie` 调整真实高度、贴图、移动和全局雾；云平面与方向光 Cookie 共用 `Textures/V4/CloudOpacity.asset`。
- 画面：WorldPictureStyle 与 `Materials/V4/WorldPicture.mat` 控制世界画面处理；UI 独立。参数以资源为准，不重复记录试调历史。
- 门：`Prefabs/V4/FishThreshold.prefab`，ThresholdDoor 调降落／落地／交互，ThresholdFish 管鱼群；子节点 Landing mist 的 ThresholdMist 调整雾透明度、流速与淡入，材质为 Materials/V4/ThresholdMist.mat。DoorStateEffects 控制 on/off 发光与鱼暂停，雾读取落地且 on 的通行状态；DoorPassage 检查实际跨门面并加载下一关。
- 无快照标记：WordLibrary 的 Empty Snapshot 与 `Memory print/No snapshot/Corner to corner` 原生 Image，SnapshotPlaceholder 适配框尺寸。
- 日常编辑原生组件、场景和 Prefab，不重跑初次搭建脚本覆盖手调布局。

## 旧资源依赖（无需读取旧设计）

旧资源保留原位，目录版本名不意味着继承玩法。

- V4 直接复用 `Sprites/V3/` 中 Apple、Door、I、Move、Negative、Positive、red；`Fonts/V3/PlayerNotes.fontsettings`、`Materials/V3/MemoryPrint.mat`、`UI/V3/SaveSquare.png`。
- 初次迁移工具 OpeningSetup 引用 V3Interface／V3Presentation，WorldEntrySetup 引用旧符号路径；这些工具依赖不是采用旧规则的依据。
- General 及以上资产按共享资源处理；V4 专属修改前核对影响，必要时复制至 V4 再改引用，保留 `.meta` 和 GUID。
- ProjectHelper 原位保留。其旧来源字符串中的 GameDesign.md／Progress.md 指整理前内容，现位于 `Archive/` 同名文件；未改设计数据、审核指纹和历史。仅用户要求追溯 V3 时使用。

## 验证

- GrammarChecks.Run() 为无场景副作用的语法与整句冲突检查。SentenceRuntimeChecks.Run()、GatheringChecks.Run()、PassageChecks.Run() 分别在新的 P4 Play 中检查效果、聚集与目标选择、实际角色跨门和空房间；各读 Result。不注入输入设备，结束后退出 Play 恢复场景。
- SentenceEffectsSetup 与 NextRoomSetup 为本次增量配置工具，日常只编辑原生组件／资源；不重跑初次开场搭建工具。

- OpeningChecks.Run(false/true) 分别覆盖先走与先点灯，读取 OpeningChecks.Result；完整测试状态见进度。
- LightFieldChecks.Run() 在新的 P4 Play 中检查小灯收词射线、玩家穿过、面板开关时移动、开关与移动共存、拆句、区域回收与返回恢复，读取 LightFieldChecks.Result；不注入键鼠设备，不等同完整人工操作验收。
- 覆盖本次变化涉及的实际玩家路径、先后顺序、共存、拆句及碰撞；规则成立或测试数量不能代替行为、画面与手感验证。
- 测试工具异常与产品缺陷分开处理；未复现的用户问题保留未解决状态。
