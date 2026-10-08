# 当前画面方案

2026-09-29 起，以下空间资源由三个平级原型复用。V1 为英文流程，V2 为符号替换，V3 为天平解谜；实际场景入口和共享资源修改边界见 [Prototypes.md](Prototypes.md)。本页旧记录中的 V2 指美术空间方案，并非新的符号原型编号。

## 已确认方向

### 亮房到暗厅的走廊衔接（2026-10-08，当前优先）
- 用户反馈：上一版降低入口点光源虽消除白斑，但破坏了亮房到暗厅的衔接；走廊本身应承担均匀渐暗的过渡。
- 走廊墙、顶、底使用独立 `CorridorPlaster`，`CorridorAmbient` Shader 复用 URP Lit，按世界 Z=4→12 逐像素将间接光从 0.75 平滑降至 0.004。原生直射光、阴影和深度仍保留；这是美术控制的间接光近似，并非烘焙 GI，也不是相机曝光切换。点光源单独调参无法控制三面一致的纵向过渡，因此使用局部材质方案。
- `Corridor light` 位于 (0,1.9,5.5)，强度降至 0.05，仅保留少量局部补光；大厅及开场房间的原始材质、全局曝光不变。MainHallLook 重设时保留走廊独立材质。
- 已在实际 Play 查看亮端朝向开门后的天平大厅、暗端回望亮房、关门状态，未再出现三块过曝白斑；截图在 `Temp/LightingReview/gradient-final-*.png`。最终过渡观感待用户确认。

### 状态反馈与符号（2026-10-08）
- `Sprites/V3/vertical.png` 沿用 640×640 透明画布、浅灰 84 像素粗直笔画与平头，作为苹果核解析结果的视觉符号，尚未注册为可收集词。
- OpeningPrototype/Main route 下的 Insufficient weight feedback 使用独立 2.8 秒 Timeline 和低机位按钮特写，复用黑边、输入锁定和视角恢复组件，未绑定成功事件或按钮按压动画。
- 玩家相机下的 Completion paper confetti 为原生 ParticleSystem：少量红、米白、浅金和灰绿矩形纸片旋转飘落，成功演出结束后一次播放；替代英文完成提示。粒子和镜头均保留在 Prefab 中可编辑。
- 大厅 RoomLighting 首次显示后锁存亮起，只在新游戏重新开始时恢复初始暗态。

### V3 苹果核与出口（2026-10-08）
- 外观反馈修订：果皮连续覆盖上下残留部分的外侧，延伸到啃咬边缘；浅色果肉仅露在向内凹的啃咬面，取消原先像剥皮一样的厚白色外缘。模型几何尺寸与碰撞配置保持。
- `ArtAssets-3D/V3/AppleCore/AppleCore.fbx` 为新苹果核，Blender 可编辑源及生成脚本在同目录 `Source~/`（Unity 不自动导入该源目录）。上下残留红皮、中间细核、果梗；Unity 使用独立 AppleCoreSkin／Flesh／Stem 材质。
- 果皮复用原苹果贴图中的红皮区域，避免采到原图集黑底；原法线贴图不适用于新模型，因此不复用。果肉使用浅黄哑光材质。
- OpeningPrototype 两只苹果的 AppleState 指定完整外形和苹果核；苹果核使用中央 CapsuleCollider 与上下两块凸 MeshCollider，保留细腰空间。`AppleCoreSetup.Apply()` 可维护配置，`Preview()` 生成 Unity 对照图到 Temp。
- 当前 V3 完成出口移除五块死路通道几何体；后墙和升降门保留。游戏相机使用纯黑清屏，不改全局天空环境光，以免影响已有室内照明。

### V3 门与镜框材质归属（2026-10-07）
- 两扇周期门共用 `Materials/V3/Opening/Door.mat`；镜框独立使用 `Materials/V3/Opening/MirrorFrame.mat`，镜面继续使用 `Materials/V3/Tutorial/MirrorSurface.mat`。不得再继承墙体 GalleryWall 或让门和镜框互相共用。
- 此次独立材质保留原有外观参数，后续可分别调整。门属于走廊照明，LocalAmbientProbe 的 Follow Room Lighting 关闭；大厅开关灯不替换任一门扇材质，避免门位于灯光边界时两侧被不同处理。

### V3 大厅美学修订（2026-10-06，当前优先）
- 用户反馈：前面开场房间的氛围保留；天平大厅回到原 V3 博物馆式空间，周边更暗，避免平亮、卡通和天平过曝的效果。
- 大厅使用独立 HallPlaster／HallStone／HallBrass 材质：低饱和墙地、受控地面反射、真实金属参数；原 General 材质与旧场景不变。天平模型与玩法不变。
- 用户追加确认：参考图的无黄色版本，墙地与光均为中性白；周围仍暗，白墙在阴影中呈灰色。HallStone 与 HallPlaster 底色统一为 (0.86, 0.86, 0.86)，不通过黑地面制造对比。
- 早期大厅与整段走廊共用低亮度环境光，由入口 Point Light 沿走廊衰减；此方式产生入口白斑，降低强度后又导致亮端衔接不足。走廊当前已改为本页上方的逐像素间接光过渡；大厅继续使用 HallPlaster／HallStone。
- 此次修订原因：此前 LocalAmbientProbe 按 Renderer 的包围盒中心设置整块模型的 SH，走廊与大厅的整块模型各自取得亮／暗值，实际在门槛硬切；不能将它描述为大模型表面逐点的连续照明。现扩大低环境光范围，使走廊和大厅取得相同暗底，再由原生灯光产生逐点渐变。LocalAmbientProbe 仍用于区域基线与移动物体，不承担墙地表面的渐变。
- 大厅局部曝光 Volume 继续停用，镜头进出不切曝光。此版为用户授权试做的过渡方案，最终观感待确认。
- 主聚光移到天平正上方，白色顶灯、柔和光斑、弱轮廓光和正面补光集中突出展品；后墙洗光关闭。增加淡淡的局部光束，采用深度遮挡的局部散射 Shader，原生 Light 仍负责表面照明与阴影。这是局部丁达尔近似，不包含完整体积阴影或全屋雾。
- Gallery reflection 为原生盒投影探针，按当前大厅重新烘焙至 `ArtAssets-3D/V3/Generated/MainHallReflection.exr`；烘焙时暂隐藏光束，避免深度采样散射进入静态反射贴图。
- 玩家及镜像相机启用后处理，通过独立 `V3 Hall Volumes` 层和 NeutralViewProfile 屏蔽管线默认调色。MainHallProfile 保留为中性参数、Volume 停用；其 BoxCollider 只作为 LocalAmbientProbe 的空间范围引用。HallStone 关闭带来蓝灰偏色的天空反射，使用同一局部环境的中性反射回退；黄铜仍使用本地反射探针。
- 原生 Light／Volume／Renderer 都在 `OpeningPrototype/Main route` 编辑；画面观感仍待用户确认。用玩家相机同等配置、HDR 离屏目标及正确线性转 sRGB 检查远近视角、光束内部和墙体遮挡；开场同视点前后平均 RGB 差异约 0.009/255、单通道最大 1/255。未开启持续 Play，未测 GPU 性能。
- 后续实机修正：上述离屏检查遗漏 V3Presentation 实际使用 ARGB32 的显示链路。主／镜像帧缓冲现改为 DefaultHDR，避免高亮在负曝光前截断；已进入 Play 验证开场完成后的大厅 Game 输出和镜像分屏，均正常保留中心照明。此前“相机同等配置”的离屏结果不能代表原 ARGB32 游玩画面。

### 早期空间记录
- 当前场景为 `Assets/Scenes/V1/TutorialLevel.unity`（原 SacredSpaceV2 改名，仍使用 V2 空间资源）。旧 SampleScene、WhiteRoomStudy、WhiteGalleryStudy、SacredSpaceStudy 及其专用资源已按用户确认删除；当前构建入口为 TutorialLevel。
- 白色柔和空间、红色地毯，无旧版黑色描边。PC/Mobile 管线只保留 `SacredSpaceV2_Renderer.asset`，默认及相机 renderer index 均为 0，SSAO 关闭。
- 走廊约 3.2 米宽、4 米高；主房约 18×20 米、高 12 米。出生 X=9.05，朝向主房；台座中心保持 Unity X≈-8/Z=0，尺寸 1.2×2 米、高 1 米，底部 Y=0.04。后方留给未来的门。

## 当前资源与维护

- 2026-09-30 V3 试玩修订：睁眼改为宽羽化曲边，暗处逐渐显露场景，低分辨率双向高斯模糊逐步退去；初始黑屏约 0.45 秒、睁眼 5.5 秒、稳定 1.2 秒后收【我】。收词约 3.4 秒，淡入／停留／收入，白色符号加细暗边提高明亮背景可读性。参数为本轮试调值。
- Tab 保持约 1/3 屏宽：炭黑底、中性白符号、左侧窄词列与细滚动条，右侧大面积留白供组句。快照在右侧居中显示，右上角小 X 关闭；`MemoryPrint` 材质降低饱和度、中性灰调色、暗角与静态细颗粒，不改变原始快照。移除备注区和保存键，不显示解释性词义。
- 开场学步沿用 V2 的 26% 初速度、短步起步、步频迟疑和轻微 yaw／上下起伏；按实际行走距离渐退，不偏转运动方向。当前 2.3 米获得【移动】，4.6 米恢复正常步态；苹果身份／运送时不叠加踉跄。
- 2026-09-30 V3 玩法制作补充：P3 的双扇门已接入平移开关，指示灯使用运行时材质显示黑／白。天平从原混合网格派生 `Generated/Balance-0/1/2.asset`，将支架、两盘分离，原混合 renderer 停用；横梁、链条和两盘围绕专属枢轴运动，用户源模型保持原样。
- 为完成本轮玩法，P3 后墙另用局部场景几何留出通道，设置无【门】词义的升降石板及短出口通道。此项是玩法新增，不是用户此前源文件中的墙面改动；V1/V2 后墙与模型保持原样。
- 睁眼模糊和相同视角分屏由 V3 专属全屏材质处理；词库快照来自该玩家相机，不含面板。符号从已有用户素材复制到 `Sprites/V3`，没有修改 V2 素材配置。
- 2026-09-30 V3 新资源：`ArtAssets-3D/V3/Balance.fbx` 由外部 `balance.glb` 转换，门与指示灯 FBX 从各自包含整套旧场景的外部导出中提取。`Scripts/V3/Editor/Blender/prepare_models.py` 在 Blender 后台运行，`--` 后依次传源目录与输出目录，保留外部原件。天平枢轴归到底部中心、导出高约 2.4 米；金色材质抽取为 `Materials/V3/BalanceBrass.mat`。
- V3 `P3.unity` 的天平基座落在台座顶面 Y=1.04；苹果初始放在台座旁，最低点 Y=0.045。门位于入口 X=2，底部 Y=0.04、顶部约 Y=3.5455；指示灯中心约 (2.096, 3.767, 0)，编辑态用黑色 `IndicatorOff.mat`，Play 中闪烁。单网格指示灯 prefab 保留 FBX 自带的 100 倍单位换算，在此基础上放大 1.3 倍。
- 本次只同步用户改动的「主房-入口」，对比基准是 9 月 24 日的 `PuzzleApple-场景-0.fbx`。使用 V3 派生 `ArtAssets-3D/V3/Generated/EntranceWall.asset` 将现有空间门洞顶部降低 0.4 米，MeshFilter 与 MeshCollider 同步引用；房间其他尺寸及后墙不变。后墙源文件开口是历史差异，不属于本轮改动。

- 开场镜框共用 `GalleryWall.mat`；镜面使用 `MirrorSurface.mat`（由 FrostedMirror 重命名，保留 GUID）＋`PlanarMirror` 平面反射组件与 Shader。运行时反射走廊，无玩家模型；镜面不可见时不更新反射，旧 `Corridor reflection` 探针已删除。
- 建筑模型：`Assets/ArtAssets-3D/General/PuzzleApple-SpaceStudyV2.fbx`；源文件在项目外 `D:/NYU/Study/26Fall/Thesis/PuzzleAppleAssets/PuzzleApple-sacred-space-v2.blend`。
- `Assets/Scripts/General/Editor/Blender/space_proportion_v2.py` 从原 corridor 文件生成 V2 副本和 FBX；不要对已修改的 V2 副本重复运行。项目外 Blender 源文件本次未清理。
- 共享材质 `GalleryWall.mat`、`GalleryFloor.mat`、`WhiteRoomCarpet.mat` 仍供新版使用，名称保留；`SampleSceneProfile.asset` 也是新版场景仍引用的 Volume Profile。
- `Generated/SpaceV2-主房-顶底.asset` 与 `SpaceV2-走廊-顶底.asset` 用于地板朝上面的材质分区，碰撞使用源模型网格。模型重导入后需核对派生网格、材质分区与碰撞。
- `Generated/Gallery/SpaceV2Reflection.exr` 保留为 V2 反射烘焙资源；模型或灯光变化后需核对探针并重烘焙。更精细的间接光与地面倒影仍可后续调整。
- 苹果使用 `Apple.mat`（URP/Lit），通过 FBX 材质 remap 保持重导入引用。`AppleNormal` 为 Normal Map；`AppleRoughness` 是线性源图，更新后需重新生成 `AppleMetallicSmoothness.png`（RGB=0，alpha 为反相粗糙度）。

## 验证记录与边界
- 此前已验证出生落地、走廊阻挡、门洞通行、台座阻挡、后方绕行及主房墙体；模型重导入后的派生网格缺面已修复。
- 历史检查截图已清理。真人手感、独立构建及 GPU 性能仍未完成验收。

## 2026-09-24：苹果与门资源

- 新源模型 `Assets/ArtAssets-3D/General/AppleBitten.fbx` 为外部 `苹果被咬.fbx` 的副本，不修改项目外原件。
- 派生 `Generated/AppleBitten.asset` 将保留 UV 的表皮面与全零 UV 的新增咬口面分成两个子网格，使用原 `Apple.mat` 与浅黄 `Tutorial/AppleFlesh.mat`。该分区方法仅适用于本次模型；源模型重导入或拓扑/UV 改变后需要重新检查。
- `Assets/Prefabs/V1/Interaction/BittenApple.prefab` 使用该派生网格，统一成约 0.6 米大小；咬口采用网格碰撞，避免球形碰撞挡住钥匙交互。
- 派生被咬网格保留可读顶点，抛落演出据此计算倾斜后的实际最低点，避免按外包围盒落地造成悬空。`ApplePuzzleSetup.Configure()` 重建网格时保留该设置。
- 场景苹果上的 `CognitionApple` 引用被咬苹果 prefab 和钥匙物体；钥匙已替换为用户 `Key.fbx`，保留 `CognitionKey` 交互，具体摆放见下方正式钥匙记录。
- 完整苹果使用贴合外形的 SphereCollider 与 Rigidbody：首次起浮前固定在台座，漂浮时关闭重力并锁转动，拆句后恢复重力及转动。`ApplePuzzleSetup.ConfigurePhysics` 维护此配置；取到嘴边时暂停物理，避免与演出轨迹争夺位置。被咬苹果仍沿既有抛落演出轨迹运动。
- `Exit door - prototype` 在台座后方 X=-15；门扇通过 Hinge 绕轴打开，门框与门扇使用独立材质。当前是房内独立门框，不改变既有后墙，也未制作门后新区域。
- 编辑器配置入口 `PuzzleApple.Editor.ApplePuzzleSetup.Configure()` 负责派生网格、材质、占位物与引用。它不会覆盖已有门的造型，重新导入被咬苹果后可运行以更新派生资源，再做视觉检查。

## 2026-09-24：镜子与门正式模型替换

- 镜子源模型归档为 `Assets/ArtAssets-3D/General/Mirror.fbx`，来自外部 `镜.fbx`。场景沿用左侧位置，保持模型比例并适配为高 1.87 米；原模型镜框与镜面网格直接使用，不生成替代网格。镜面上的 `PlanarMirror` 显式配置模型局部反射平面和法线，凝视 Trigger 按实际镜面边界调整。
- 门源模型归档为 `Assets/ArtAssets-3D/General/Door.fbx`，来自外部 `门.fbx`。保留比例、整体高 3.2 米，沿用台座后方 X=-15 的位置；原门框保持静止，门扇沿侧边 Hinge 打开，碰撞使用各自原模型网格。当前仍未制作门后区域。
- 镜框、门框与门扇共用 `GalleryWall.mat`；实时反射材质重命名为 `MirrorSurface.mat`，保留原 GUID 与 Shader。已删除旧镜子/门占位几何体、旧禁用反射探针及无引用的 `DoorFrame.mat`、`DoorLeaf.mat`，配置脚本也不再生成这些占位资源。
- 后续模型替换入口 `AuthoredInteractableSetup.ConfigureMirror` / `ConfigureDoor`；`ReplaceSceneModels` 用于重建本次镜子与门并应用本轮参数，会覆盖这两处造型与参数，仅在明确需要重建时运行。
- 当时保留的钥匙方块现已由下方正式钥匙替代；被咬苹果已是用户模型。本节覆盖上文门为几何体原型的旧记录。

## 2026-09-24：门框颜色

历史试色为红门框、白门扇，现已改为下面的浅蓝试色。

本次颜色调整沿用场景当前门的位置与尺寸（现有场景已调整到后墙附近）；上述X=-15、整体高3.2米是导入时历史值。回归检查改为按门扇实际边界定位，不再依赖旧坐标。

历史浅蓝试色使用独立材质 `Tutorial/Door.mat`。用户已自行调整颜色，当前以用户场景/材质为准，不重新应用历史色值；重建门仍会使用项目中该材质，若用户改了材质分配，重建前需核对。

## 2026-09-24：正式钥匙

- `Assets/ArtAssets-3D/General/Key.fbx` 来自外部 `key.fbx`，保留源模型比例，统一缩放至总长约 0.40 米；不修改项目外源文件。
- 钥匙斜插在咬口内，齿端埋入果肉约 0.08 米，圆环与部分钥匙杆露出。从咬开时起，钥匙就是被咬苹果的子物体，展示、抛落、落地均保持相对位置与方向。沿用金色并设为黄铜材质 `Tutorial/KeyBrass.mat`，由旧占位材质重命名并保留 GUID。
- 场景根节点保留 `CognitionKey`，移除旧方块的 MeshFilter、MeshRenderer、BoxCollider，子物体采用原 FBX 与网格碰撞。`Interaction focus` 位于露出的圆环边缘，便于准确核对瞄准；咬口仍使用原网格碰撞。
- `ApplePuzzleSetup.ConfigureKey()` 负责重新配置本次模型、比例、插入姿态和引用；会重建钥匙造型。`CognitionKey` 上的 Apple Local Position / Euler Angles 可微调插入位置与角度。
