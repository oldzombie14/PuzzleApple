# 平级原型与资源边界

## 当前入口

| 原型 | 场景 | 用途 |
| --- | --- | --- |
| V1 | `Assets/Scenes/V1/P1.unity` | 原英文认知流程，保留对照版本 |
| V2 | `Assets/Scenes/V2/P2.unity` | 相同流程、句式和效果，词汇以 sprite 呈现 |
| V3 | `Assets/Scenes/V3/P3.unity` | 新开场、复用词库、镜像／本质化、闪烁门及两条天平解法 |

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
