# 当前进度

## 2026-09-22
- 按 GnomonTrial 的方式安装 Unity MCP v10.2.0，复用 Codex 的 stdio 服务。
- 连接验证通过：目标为 PuzzleApple，项目路径正确，编辑器就绪，验证时控制台无错误。
- 建立项目协作规则及文档入口；沿用 `Assets/Docs/` 和 `Assets/Scripts/` 等现有分类。

## 当前状态
- 当前打开 SacredSpaceV2：走廊 3.2×4 米，主房占地 18×20 米、高 12 米；台座保持原中心。关闭 V2 的 SSAO 暗边，保持白色柔和空间方向。材质分区、反射与碰撞同步，通行验证通过；旧版本保留。
- 新增 SacredSpaceStudy：基于源 blend 的空间比例试验，走廊压低收窄、主房加宽拔高、台座居中且后方留空；原 blend 和前三版场景保留。碰撞与通行检查通过，详见 VisualDirections.md。
- WhiteGalleryStudy 已试调较矮玩家：眼高 1.15、FOV 75、碰撞体高 1.3，增强空间尺度感；通行/阻挡检查通过，等待用户手感反馈。
- 新增 WhiteGalleryStudy：更明亮的白色展厅候选，独立墙/地面材质、弱接触阴影和烘焙反射探针；原两版保留。详见 VisualDirections.md。
- 苹果材质已修复：AppleNormal 按 Normal Map 导入；AppleRoughness 按线性数据处理，反相写入派生 AppleMetallicSmoothness.png 的 alpha（RGB=0，非金属）。外部 `Assets/Materials/Apple.mat` 使用 URP/Lit，并通过 FBX 材质 remap 保持重新导入后的引用。粗糙度源图若更新，需重新生成派生贴图。临时照明截图已确认纹理和高光正常；封闭房间内仍较暗，未改变场景照明。
- 模型已同步：地毯单独红色，普通地面和墙体白色；SampleScene 保留 flat，WhiteRoomStudy 为独立白色空间候选。详见 VisualDirections.md。
- MCP 可用；后续连接时应重新验证实例和编辑器状态。
- SampleScene 已接入第一人称控制器、二分色材质、屏幕空间描边和 9 个非凸 MeshCollider；出生点为走廊尽头 `(3.05, 0.08, 0)`，朝向主房（Y=-90°）。
- Play Mode 验证：落地稳定、走廊侧墙/尽头阻挡、门洞通行、主房远墙/侧墙阻挡。脚本与两个 shader 无编译错误；尚未进行真人键鼠手感验收或独立构建验证。

## 使用与调整
- 运行时统一设置 60 FPS 目标上限（`FrameRateSettings` 启动时自动应用，无需挂组件），关闭运行时 VSync，避免高刷新率屏幕覆盖 60 帧设置。适用于编辑器 Game 视图和桌面构建；不限制 Scene 视图等编辑器界面刷新。
- Play 后点击 Game 窗口，WASD 移动、鼠标转向；Esc 释放鼠标，左键重新锁定。Player 上调整移动速度和鼠标灵敏度。异常跌落到 Y=-5 以下自动回出生点。
- `Assets/Materials/Architecture.mat` 和 `Floor.mat`：`Sunlit Color` / `Shadow Color` 分别控制亮暗色，`Light Threshold` 控制二分阈值；`Received Shadow Strength` 控制接收投影的影响。模型封闭屋顶会遮住阳光，室内主要显示暗部颜色。
- `Assets/Materials/Outline.mat`：`Outline Width (pixels)` 控制黑线宽度（默认 3，设 0 关闭），法线和深度阈值控制边缘识别。PC/Mobile Renderer 均配置 URP Full Screen Pass。描边检测可见深度/法线边界，不绘制三角面线；同平面同法线的材质接缝不会额外描边。
- 两场景已直接使用最新 FBX 网格；旧 Generated 资源暂留但不再作为场景网格。重建菜单已更新为按名称分配地毯材质，具体说明见 VisualDirections.md。

