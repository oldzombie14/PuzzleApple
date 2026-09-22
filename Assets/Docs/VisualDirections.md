# 画面方案对比

目前三套均为候选，用户尚未决定最终方向。分别打开场景即可试玩，使用同一出生点、第一人称控制和 60 FPS 目标上限。

## 空间比例试验（在 C 基础上）
- 用户认可 V2 空间氛围；按最新反馈将 V2 台座恢复为 1.2×2 米、高 1 米，中心不变、底部位于 Y=0.04（红毯上表面）。Blender V2 副本、FBX、碰撞与反射图已同步，生成脚本也更新为此尺寸。
- **当前迭代 V2**：`Assets/Scenes/SacredSpaceV2.unity`，对应外部 `PuzzleApple-sacred-space-v2.blend` 和 `PuzzleApple-SpaceStudyV2.fbx`。用户反馈上一版走廊压迫、主房占地不足，目标修正为“走廊也有神圣感，进入主房后更空旷”。走廊约 3.2 米宽/4 米高；主房约 18×20 米、12 米高，面积约 360 平方米（V1 约 80）。围绕原中心向前后扩展，台座仍在 Unity X≈-8/Z=0；入口移至 X≈2，走廊与出生点前移 6 米（出生 X=9.05）。没有新增物件，后方仍留给未来的门。
- V2 相机使用独立 renderer index 3 (`SacredSpaceV2_Renderer.asset`)，关闭 SSAO 和描边，移除用户指出的灰色墙角暗线。沿用白色材质，提亮环境光、调整现有补光，重烘焙 `SpaceV2Reflection.exr`；旧版本外观未更改。更精细的间接光/地面倒影仍是后续可调项。
- V2 已验证出生落地、走廊侧墙、入口通行、台座阻挡、后方绕行、扩大后的后墙和侧墙。修复模型重新导入后派生网格显示缺面，并重新截图确认。脚本 `space_proportion_v2.py` 从原 corridor 文件生成，不应对 V2 副本重复运行。
- 以下为保留的 V1 记录：
- 已确认约束：只调整现有物件，不新增装饰或建筑；台座居中，不向后移，后方留给用户未来制作的门。
- Blender 副本：`D:/NYU/Study/26Fall/Thesis/PuzzleAppleAssets/PuzzleApple-sacred-space-study.blend`；原 `PuzzleApple-corridor.blend` 未修改。15 个原有对象保留，旧线条画隐藏避免旧比例线稿干扰；隐藏的门布尔辅助体同步调整，未导出。
- 走廊外宽约 2.5→1.7 米、高约 2.5→1.9 米；主房宽 8→10 米、高 8→12 米。前后方向长度保持 8 米。入口开口同步收紧，地面高度与墙厚尽量保留。
- 台座中心校正为 Blender X≈7.998/Y=0，保留这次源 blend 中较小的台座尺寸（约 0.70×1.16×0.58 米）及较窄红毯（0.8 米）。这些尺寸与旧 Unity 版本不同，来自用户当前源文件。台座抬高 0.03 米避免陷入地面。
- 导出 `Assets/ArtAssets-3D/PuzzleApple-SpaceStudy.fbx`，独立试玩场景 `Assets/Scenes/SacredSpaceStudy.unity`；沿用 C 材质与玩家参数，匹配碰撞和地面材质分区，重新烘焙独立反射图。仅调整现有主灯高度/朝向/强度及入口补光高度，无新增灯具。
- Play 验证：出生落地、窄走廊侧墙、门洞通行、台座阻挡、绕行至后墙均正常；控制台无错误。当前保留走廊出生视角。
- Blender 修改脚本：`Assets/Scripts/Editor/Blender/space_proportion_study.py`，针对原 corridor 文件运行；再次运行会覆盖此试验副本和 FBX，不能对已修改副本重复执行。

## C：明亮白色展厅
- 渺小感视角试调（待用户试玩）：眼高由 1.65 改为 1.15 米（相对玩家脚底），垂直 FOV 70→75；CharacterController 高 1.3、中心 Y=0.65、半径 0.24、stepOffset 0.16。仅 C 场景应用，出生位置和速度不变。已验证落地、门洞通行、台座阻挡和绕行，模型未修改。旧参数：胶囊高 1.8、中心 Y=0.9、半径 0.28、stepOffset 0.22。
- 场景：`Assets/Scenes/WhiteGalleryStudy.unity`，从 B 独立复制；参考用户最新白色展厅图，保持现有建筑布局与红毯。
- 明亮中性环境光、较轻的主光阴影、弱化墙角 SSAO；哑光墙面 `GalleryWall.mat` 与光滑度 0.78 的地面 `GalleryFloor.mat` 分开。地板向上的面使用派生网格单独分材质，碰撞仍保持源网格。
- 相机 renderer index 2：`Gallery_Renderer.asset`，独立 AO 参数，不改变 A/B。
- 主房使用 256 分辨率、盒投影的烘焙反射探针 `Gallery Reflection`，反射图 `Assets/ArtAssets-3D/Generated/Gallery/RoomReflection.exr`。不逐帧捕获；模型或灯光变化后需重烘焙。探针为近似反射，目前地面倒影比参考图弱，不是平面镜面反射。
- 这是实时光照方向试验，未烘焙漫反射 GI；进一步匹配参考图需继续调整间接光与地面倒影。当前无新增脚本或 shader，控制台无错误；未实测 GPU 性能。
- 主房预览：`Assets/Screenshots/screenshot-20260922-181239.png`。预览后已恢复走廊出生相机并保存。

## A：Flat / 二分色
- 场景：`Assets/Scenes/SampleScene.unity`。
- 方法：`TwoTone.shader` 将主光方向和投影分为亮/暗两个色块；材质分别控制两种颜色。`ScreenOutline.shader` 检测屏幕深度与法线边界，通过 URP Full Screen Pass 叠加黑线，默认宽 3 像素。
- `FlatWhite.mat` 用于建筑、普通地面、台座：亮部纯白，暗部 RGB 0.88；`Floor.mat` 现仅用于红毯。苹果保留独立 URP/Lit 贴图材质。
- PC/Mobile 默认 renderer 保留原描边设置。`Architecture.mat` 保留最初灰色版本参数（亮部 0.83/0.82/0.80，暗部 0.60/0.61/0.63），方便追溯。
- 参考截图：`Assets/Screenshots/screenshot-20260922-174111.png`。

## B：白色空旷空间试验
- 场景：`Assets/Scenes/WhiteRoomStudy.unity`；独立场景副本，后续模型/布局更改须同步两个场景。
- 方法：URP/Lit 连续光照、低光泽白色表面、深红毯；较暗走廊通往更亮主房，使用主房顶端点光、入口补光和两个低强度走廊补光。主灯软阴影和独立 SSAO 提供接触深度，无黑色描边。
- `WhiteRoomPlaster.mat` / `WhiteRoomCarpet.mat` 控制材质；场景 `White Room Lighting` 控制灯光。
- 相机选择 renderer index 1，即 `WhiteRoom_Renderer.asset`；PC/Mobile 管线都登记此 renderer，index 0 的 flat 配置保留。Scene 视图可能使用默认 renderer，请用 Game 视图判断最终效果。
- 为实时可试玩试验，未烘焙 GI，也没有真实面光源或体积雾；不是对参考图逐像素复现。白色材质在暗处自然呈灰色。

## 本次模型同步与验证
- 两场景直接引用重新导入 FBX 的网格，已解除旧派生网格覆盖；9 个建筑部件（含地毯、台座）均有非凸 MeshCollider。
- 红毯抬高 0.01 米以避免与地面重叠闪烁；仅红毯为红色。苹果保留已修复材质。
- WhiteRoomStudy 中 Play 验证落地、穿过门洞、台座阻挡、绕行和主房墙体阻挡通过；运行时帧率目标值为 60。尚未测试独立构建或 GPU 性能。
- `PuzzleApple > Configure Sample Scene` 已更新为读取当前 FBX、按对象名区分地毯，适用于 A 场景；仍会重设玩家相机，不要用于 B。
