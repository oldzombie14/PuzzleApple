# 版本归档与依赖边界

## 场景入口

- V1：`Assets/Scenes/V1/P1.unity`。
- V2：`Assets/Scenes/V2/P2.unity`。
- V3：`Assets/Scenes/V3/P3.unity`；同目录 `Archive/` 保留本版旧场景。
- V4：`Assets/Scenes/V4/P4_Systems.unity` 为会话入口；`P4.unity`、`ChapterI.unity`、`ChapterI_Garden.unity` 为内容场景，也支持直接 Play。

- V5：`Assets/Scenes/V5/P5.unity`，菜单 `PuzzleApple/V5/Open`；设计与进度见 [V5](V5.md)。

编辑器菜单 `PuzzleApple/Prototypes` 提供 V1–V4 入口。各版本独立打开和运行，不将旧版本场景与新版本内容混合为同一会话。当前开发为 V5，本轮未更改分支。

## 目录与所有权

沿用“资源类别／版本”的结构，不将不同版本合并到一个目录：

- 脚本、场景、Prefab、玩法数据分别在 `Assets/Scripts/Vn/`、`Assets/Scenes/Vn/`、`Assets/Prefabs/Vn/`、`Assets/GameData/Vn/`。
- 模型、材质、Shader、字体与 UI 图标分别在 `Assets/ArtAssets-3D/Vn/`、`Assets/Materials/Vn/`、`Assets/Shaders/Vn/`、`Assets/Fonts/Vn/`、`Assets/UI/Vn/`。
- V2 实际引用的精灵在 `Assets/Sprites/V2/`。V4 音乐与音效在 `Assets/Audio/V4/Music/`、`Assets/Audio/V4/SFX/`。
- 仅某一版本使用的类别只保留该版目录，不为了对称创建无内容的版本目录。Blender `Source~/` 源稿与已有 `Docs/TMP/` 工作文件保留原位。

各版已导入资源的序列化依赖只指向本版或 Unity／Package 资源，不再引用其他版本或 General 的素材、字体、材质和镜面组件。V3 的归档场景也按 V3 所有权处理。

原 General 素材、旧精灵来源目录与未分版音频仍保留为来源和回退副本，没有删除或合并旧版内容。日常改某一版本时，应编辑该版目录中的实际引用副本，不再修改旧来源以影响多个版本。

本次批量导入产生的 33 个无引用空目录已集中到 `Assets/_EmptyFolders_ToDelete/`，保留各自的 .meta 和 GUID，供用户在 Unity 中手动删除。该目录不承载运行资源。

## 复制规则与代码边界

- 副本使用新 GUID，并只在对应版本内替换引用。原有场景、Prefab、脚本与素材的 GUID 保持不变；场景布局、组件 fileID 和已保存参数不因归档重建。
- 复制脚本必须有独立完整类型名。本次镜面与基础控制器副本使用 `PuzzleApple.Vn.Dependencies`；调用处显式引用本版类型，不能仅复制同命名空间、同类名的脚本。
- Shader 名也按版本区分，例如 `PuzzleApple/V4/Planar Mirror`，避免 `Shader.Find` 在多个同名副本之间选错。
- V4 的 HUD 搭建模板已独立，不再借用 V3 玩法脚本或 Prefab。编辑工具缺少本版源资源时应报错，不从其他版本静默补回。
- 不添加 asmdef，也不改现有玩法架构。本项目仍统一编译，一版的编译错误仍可能阻止整个 Unity 工程运行。

## 仍属于整个 Unity 工程的配置

以下不是各版本素材归档的一部分，保持原配置：

- Unity 引擎及 `Packages/`。
- `ProjectSettings/` 中的 Player、Quality、Graphics、Input 和 Build Settings；原场景列表与启用状态未改。
- 当前 URP 管线、Renderer 和 Input Actions 等由全局设置选择的资源。
- `FrameRateSettings` 的全局启动帧率策略，以及由当前 Renderer 执行的 `ReticleSceneColorFeature`。直接复制初始化脚本会让多份同时运行，单独复制管线资源也不会自动切换当前管线。
- 版本导航、依赖检查等工程级编辑器工具。V1 的旧准星搭建工具仍会访问全局 Renderer，不应把它当作无副作用的验证工具。

因此，此处实现的是版本自有玩法代码和资源的隔离，不是四个完全独立 Unity 工程。若以后要求连这些全局配置也独立，应另行决定分工程或明确的版本配置切换方式。

## 检查与验收

- `PuzzleApple/Prototypes/Validate asset isolation` 检查 V1–V4 全部已导入版本资源的递归 AssetDatabase 依赖，发现 General 或其他版本的资产依赖即报错。它不替代动态路径、全局初始化和玩法检查。
- 本次已检查现有 GUID 保持、GUID 无重复、序列化内容只发生允许的引用／类型替换；场景和 Prefab 未出现缺失脚本，Shader 编译未出现错误。
- 已完成 P1、P2、P3 与 V4 入口的基础 Play 检查及各版镜面调用；ChapterI 直接启动可加载系统和花房，保持单一玩家／会话。测试结束恢复 P4_Systems 编辑场景，未保存测试状态。
- 尚未做完整人工键鼠玩法、跨门往返流程、观感对比或发布构建验证；初始组装工具没有作为测试重新执行。
