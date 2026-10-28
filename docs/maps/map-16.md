# 16 鲸背岛

[总索引](../README.md)

状态：未完成

## 地图简介

16 鲸背岛，范围按下列原配置与本地决策维护。

## 原始官方实现

原目录：坎斯汀大陆/鲸背岛。场景类型：`map_world`；原区域配置43项，BGM配置16。入口为坎斯汀 NPC `10801` 的寻岛／提交绳索剧情，按原准备条件开放后从独立地图卡片登岛；覆盖 HUD、地图、小地图、怪物关卡、区域、任务、机关、剧情、音画表现和本地存档。共用规则见[世界地图](../features/world-map.md)。

原地形：`Map_16/MapGround_16`。 后继地图：`14`。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

`prepare_map13_minimap_ui.py --map-id 16` 从固定 `854_156383` APK／已校验原版本 CDN 转换鲸背岛原 `MiniMap_16`、全屏／局部小地图、筛选、角色动画、区域说明、迷雾及标记弹窗；原 prefab、Sprite、中文语言包、调用代码和输入输出 SHA256 见 [official-map/minimap-16-source-manifest.json](../../assets/reference/official-map/minimap-16-source-manifest.json)。运行数据为 `assets/official/map16-minimap.json`；Unity 显式资源绑定尚待接入。未使用新版替代或自绘素材；本地新增素材尚未进入完整 ZIP 快照。

通天塔三张大区域（42061／42062／42063）小地图：`prepare_map13_minimap_ui.py` 按原 `MiniMapPath` 从固定版 Utopia／Bronze／Snow bundle 转换 HUD、全屏 UI、玩家动画、区域说明与迷雾，原配置与本地输出为 `assets/official/map42061-minimap.json`、`map42062-minimap.json`、`map42063-minimap.json` 及各自 sprites 目录。实际原对象、包凭据、官方调用代码、语言、输入输出 SHA256 分别见 `official-map/minimap-42061-source-manifest.json`、`minimap-42062-source-manifest.json`、`minimap-42063-source-manifest.json`；对应 显式绑定。复用既有原小地图几何；没有自绘或新版替代包，新增资源尚未进入完整素材 ZIP 快照。

`prepare_map16_dialogs.py` 绑定原 38 条探索任务和 157 条中文文本，原 `TaskInfoParser`、`ClientWorldMapProgressCondition`、任务卡／详情调用代码及语言输入哈希见 [official-map/dialogs-16-source-manifest.json](../../assets/reference/official-map/dialogs-16-source-manifest.json)；运行数据为 `assets/official/map16-dialogs.json`，文本常量尚待 Unity 端接入。

通天塔小楼层地形材质与水面：`prepare_map15_rendering.py` 从固定 APK／原版本 CDN 的 10 种原地形读取 TilemapRenderer 材质与整体染色，保留原骨骼，去除重复静态骨骼网格，并转换 SnowFloor2／Floor1 原 Water2 平面与 11 个原 WaterMask 绘制的高度遮罩。输入对象、包凭据、原材质、资源处理与输入输出 SHA256 分别见 `official-map/materials-15-{地形ID}-source-manifest.json`；输出为 `assets/official/regions/{地形ID}.{json,geom}`、`map15-rendering.json`、`map-water-masks/42001.png`／`42012.png` 及原贴图，绑定由 管理。没有自绘或新版替代包；新增资源尚未进入完整素材 ZIP 快照。

鲸背岛地形材质、水面与灯光由 `prepare_map16_rendering.py`、`prepare_map13_lighting.py` 转换；世界区域说明由 `prepare_map16_world_ui.py` 转换。原版地形包 `ac1ab099039df1aa41312abb27b4cef2.bundle` 来自 manifest `854_156383`，原 APK 依赖、CDN 凭据、对象与输出哈希分别登记于 `official-map/materials-16-source-manifest.json`、`lighting-16-source-manifest.json`、`world-ui-16-source-manifest.json`。区域激活直接迁移原动画曲线及固定 APK 的 `Hidden/Light2D-Shape/USE_RECESSION` 程序；不新增原地图未启用的几何阴影。

通天塔大地形材质：`scripts/prepare_map15_special_materials.py` 与 `prepare_map15_rendering.py` 从固定版三张幻境和顶层原 prefab／PartitionData 转换 Tilemap 材质、原 Movable-Sprite GLES 程序及 TransparentMask 颜色写入、有效范围和图集 UV；区域昼夜几何写入 `assets/official/map15-layers/`，原透明通道遮罩写入 `assets/official/map-transparent-masks/`，仍由本地图资源绑定显式加载。原输入、着色器程序、控制器代码和实际输出哈希见 `materials-15-42061-source-manifest.json`、`materials-15-42062-source-manifest.json`、`materials-15-42063-source-manifest.json`、`materials-15-42053-source-manifest.json`；

鲸背岛独立地图入口与登岛界面：`prepare_map16_alone_ui.py` 直接转换固定版 `AloneWorldMapEntryManager`、`AloneWorldMapEntryItemUI`、`AloneWorldMapEntryItemContentMap16`、`AloneWorldTouchDialog` 与 `Map16_DialogIcon`。原脚本、RectTransform／布局组、中文语言、状态及剧情按钮绑定、资源来源和输入输出哈希见 `official-map/alone-ui-16-source-manifest.json`；运行数据为 `assets/official/map16-alone-ui.json`，由 显式加载。没有自绘或新版替代，新增素材尚未进入完整 ZIP 快照。

鲸背岛角色区域灯：固定版 `maproot_16` 的 `ECMapCharacterSpawnerView.mMeMapCharacterExtendSupportPrefab` 指向原 `mapcharacter` 包，`CharacterRegionLightHandler` 使用默认 2 秒渐变，`ECActionMapRegionActiveView` 使用 1 秒淡出。`prepare_map13_lighting.py`／`prepare_map16_lighting.py` 读取原世界摄像机选定的 Renderer2DData、Light2D overlap 与原 Shader 分支，转换区域 Alpha 叠加和角色加法光；组件、CDN 凭据、调用代码及输入输出哈希见 `official-map/lighting-16-source-manifest.json`。未使用替代或自绘素材，新增素材尚未进入完整 ZIP 快照。

鲸背岛绳索解锁与提交次数：原 `CanHandInItemNode`、`HandInItemNode` 及 `Common.Player.CanHandInItem` 的代码 SHA256 纳入 `official-map/alone-ui-16-source-manifest.json`，剧情图与 `hand_in_item[3]` 继续使用既有 `adventure.json` 官方来源。查询节点只核对次数，确认提交时核对并扣除 `43039×5`；没有新增或推测奖励／解锁条件。离岛领奖使用原 `SourceOperate.AloneMapAward=222` 与 `def_award_handler` 的 Popup 配置；该二进制表、枚举、`DefAwardHandler／AwardUtils／AwardPanel` 调用代码的 SHA256 同样记录于此清单，复用既有原版奖励面板。

通天塔角色区域灯：固定版 `maproot_15` 的 `ECMapCharacterSpawnerView` 实际引用原角色扩展支持 prefab，`CharacterRegionLightHandler.mLight` 的原 Light2D 保留加法光、强度、排序层与 FalloffLookup cookie；普通切区 2 秒、区域激活 1 秒渐变。`prepare_map15_lighting.py`／`prepare_map13_lighting.py` 按原引用提取，运行时 与共用 使用当前地图区域状态；四份 `official-map/lighting-{42061,42062,42063,42053}-source-manifest.json` 登记原组件、来源、代码及输入输出哈希。

通天塔原环境显隐：`prepare_map15_environment.py` 从固定版 14 种原地形的 prefab／PartitionData 提取实际 `MapEffect` 注册、原区域和 Day／Night 状态及精确骨骼／粒子路径；344 个分区注册复用对应原昼夜层，顶层 9 个非分区原雾效保持原显隐状态。输出 `assets/official/map15-environment.json`，显式绑定 ；原对象、官方控制代码、已转换输入与实际输出哈希见 `assets/reference/official-map/tower-environment-source-manifest.json`。没有新增或自绘特效，新增配置尚未进入完整 ZIP 快照。

通天塔原任务目标查询：`prepare_map15_ui.py` 的 `tower-ui-source-manifest.json` 补记原任务区域追踪、世界出口／传送点查询、地图组当前地图、坐标距离和固定坐标指引代码哈希；复用原任务、地图与方向 prefab，无新增贴图。运行时 ／ 读取原目标、完成阶段和出口／传送配置，仅定位镜头。

通天塔原自由镜头拖拽：`prepare_map15_scene.py` 沿 CameraControlBrain 的实际 IgnoreUIDragController 引用补提原拖动参数与 WorldCameraIniter，`tower-scene-source-manifest.json` 保留六个原对象、十二份官方代码与输出哈希。 按原 DragControllerBase／DragControlUtils 接入锚点、导航边界、额外限位、惯性与弹性回位；独立引擎参照为 [Unity 2022.3 Mathf](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/2022.3/Runtime/Export/Math/Mathf.cs)，URL、缓存路径和 SHA256 另列，不标为游戏代码。更新配置尚未进入完整 ZIP 快照。

通天塔原渲染跟随：`prepare_map15_scene.py` 按原 CameraControlBrain→VirCamera→m_ComponentOwner→CinemachineTransposer 提取跟随管线，并核对焦点祖先旋转和 Main Camera 的 CinemachineBrain；`tower-scene-source-manifest.json` 保留十六个原对象、十三份官方游戏控制代码及输出哈希。引擎实现单独引用 Unity 官方 Cinemachine 2.9.7 UPM 包，记录原 URL、归档及 Transposer／Predictor／VirtualCameraBase／Brain 源码 SHA256； 迁移其标准指数阻尼与原 0.2 参数，渲染与裁剪共用平滑后的坐标。无新增图片；更新配置尚未进入完整素材 ZIP 快照。

通天塔原箭头镜头定位：`tower-scene-source-manifest.json` 补记 `CameraControlBrain`／`ADirectionGuiderItemUIBase` 官方代码哈希。

通天塔元素连线输入：`scripts/prepare_map15_element_link.py` 从固定版 `defaultpackage_assets_prefabs_ui_minigame.bundle` 的 24 个元素连线 prefab 提取实际组件引用、状态切换、元素编号顺序、尺寸基准及成功延迟，复用已转换原 UI 与粒子素材；587 个原 Unity 对象、固定版客户端／Common 代码、语言包与输入输出哈希记录在 `official-map/tower-element-link-source-manifest.json`。运行数据为 `assets/official/map15-element-link.json`。`scripts/fixtures/element_link_reference_harness.cs` 只适配引擎／显示依赖，转换时插入并独立执行原 `ElementLinkGame` 的 26 个方法，生成开发输入 `scripts/fixtures/map15-element-link-native.json`；不把引擎适配、测试样本或本地存档标为原服务端逻辑。该输入控制器尚未替换运行界面。

创角原生 UI 与字体：`prepare_character_creation_runtime_ui.py` 核对原 `creation-performance.json` 与 `assets/official/ui.json` 来源哈希，恢复 CreatePlayerPanel、DIYContent 与 ConfirmDialog 的真实字段、47 张原 Sprite、布局、遮罩及 TMP 样式；官方 StepTitles 由原 CreatePlayerPanel MonoBehaviour（pathID -3964310077427183409）读取，并沿固定版语言包取标题。`CharacterCreationUiTools` 生成 Unity 原生 prefab，输入输出与生成工具哈希见 `character-creation/runtime-ui-source-manifest.json`、`native-ui-source-manifest.json`。原 `FZY4K` TextAsset TTF 与 `unity/Assets/Official/Commerce/Art/fonts/FZY4K.ttf` 字节一致，SHA256 `90023872dffe6a58503de655bd0631bf3334ce3b6b72cc75a6625b685c94c386`，使用已有 FZY4K-Dynamic TMP 资产；没有替代字体或自绘图片。

创角原生预览：`prepare_character_creation_meshes.py` 复用已有固定来源 24 fps 官方网格帧缓存，逐项沿 appearance、gender、sign-preview 与 `appearance-group-icons-source-manifest.json` 等来源清单核验输入及别名，打包男女 idle／run、捏脸选项及 12 组外观组件网格与原图集、遮罩为 `unity/Assets/StreamingAssets/CharacterCreation/creation-meshes.zip`；`character-creation/native-mesh-source-manifest.json` 保存来源清单哈希、输入、别名、包与逐条目哈希。C# `CharacterCreationMeshFramePlayer` 独立使用 Unity MeshRenderer 播放，加载时逐条核对 SHA256；`CharacterCreationMeshFrameDye.shader` 适配官方 HSV 遮罩公式，不依赖 Spine runtime。已完成捏脸、染色、草稿和双槽位 JSON 回读及画面验证，组装外观和创角后家园节点验收见功能页；新增载荷尚未纳入完整素材 ZIP 快照，不声称已备份。

## 完整还原部分

尚无本图完整 Unity 游玩流程的完成结论。

### 已迁移代码与局部资源

以下为局部代码／资源接入，完整流程仍受本页缺项限制。

通天塔默认镜头：`scripts/prepare_map15_scene.py` 按固定版世界场景 Main Camera、CameraControlBrain 的实际 ResolutionAdaptor 引用和 CameraControlSettings 转换 `assets/official/map15-scene.json`；原对象、四份官方控制代码及输出哈希见 `assets/reference/official-map/tower-scene-source-manifest.json`。 使用原安全范围、默认视野及背景色，资源须加入 Unity 显式加载清单。

通天塔原灯光与区域退散：`scripts/prepare_map15_lighting.py` 从固定版 Main Camera／Renderer2DData 与三张幻境、顶层 prefab 提取 165 个 Light2D、61 个 MapRegionLight 动画及原 GLES 程序，使用原色值、顺序、排序层、AlphaBlend／Additive 和 MapEnvData.FadeTime；运行绑定 ；输入、派生贴图、原动画与输出哈希见 `assets/reference/official-map/lighting-{42061,42062,42063,42053}-source-manifest.json`。Unity 2022.3 官方 Light2DBlendStyle 参考仅用于枚举和 Multiply 因子核对，URL／哈希另列，不冒充游戏代码。

通天塔炮口与镜面演出：`prepare_map15_cannon_actions.py` 读取固定版 `actionworldpuzzle.bundle` 的三个 Action View／ECViewDriver 管理引用和五个原实体显示 prefab 的 CommonEffectSlot，转换四方向炮口／转向发射与镜面受击共九个原序列、82 个发射器；合并到原炮弹材质目录后共 96 个发射器、31 份材质及 5 个原 GLES 变体。`map15-cannon-actions.json` 保留挂载、时序、声音与尚未接入的原震屏配置。四个 `Map_Entity_FireCanon_1..4` 原 Wwise 事件由原 map_sfx_01／minimum 银行解析，并从实际 battle_sfx_01 跨包媒体补齐；使用 WWiser TXTP／vgmstream 保留原音量，规范化 Ogg 容器序号。`tower-cannons-source-manifest.json`／`tower-cannon-materials-source-manifest.json` 核验范围扩展为 718 个 Unity 对象，另存 Wwise 包、实际媒体片段、解码器和输出哈希；两项新版同 CAB 依赖保持独立登记。Unity 运行绑定仍待接入，新增素材尚未进入完整 ZIP 快照。

通天塔原炮口震屏：`prepare_map15_impulse.py` 从固定 APK／匹配原 CDN 的世界场景读取唯一自由虚拟相机的 `CinemachineImpulseListener`、原五类炮口／镜面 `MyGridTransform` 和 `WorldGamePlay_part1.g.bytes` 的真实中心，输出 `assets/official/map15-impulse.json`，须加入 Unity 显式加载清单。震屏片段及原 NoiseSettings 随 `tower-cannons-source-manifest.json` 保留；本项独立 `tower-impulse-source-manifest.json` 保留 18 个原 Unity 对象、输入输出哈希、固定 APK `libunity.so` 原 ARM64 `Mathf.PerlinNoise` 函数／512 项置换表／归一化常量区段哈希、官方 Cinemachine 2.9.7 UPM 包实际 URL 和归档／源码哈希。`scripts/fixtures/map15-perlin-native.json` 的 56 组输入输出由 Unicorn 独立执行原 ARM64 入口产生；原三维距离、传播速度、包络、随机相位、指数衰减和监听器配置直接采用，二维呈现接收原 XY 位移；不新增原场景未配置的次级反应。

## 自定义实现

原客户端 `AloneWorldMapEntryItemContentCommonBase.FinshAward` 仅提供 `OperPlayerAloneMapFinshAward(16)` 请求，包内没有服务端领取实现。经用户明确允许，单机在本地按原奖励规则 `770／771` 发放钻石 200、金币 80000，并持久化一次性领取记录；奖励、剧情、随机状态和领取记录同事务写盘，失败回滚，冷加载后防止重复领取。领奖表现按原 `SourceOperate.AloneMapAward=222`、`def_award_handler[222].AwardStyle=Popup` 调用项目已转换的 `AwardPanel`，保留两种货币图标、数量及延迟关闭交互；原生。

原 `StageResultAutoPuzzle／ResetPuzzle`、`MapPuzzleTerminalDataStepGrid／LockBase` 只保留客户端／Common 标记与配置，包内没有服务端执行器。经用户明确允许，辅助从原菜单扣 `325×1`，仅解开 `LockLoEntityIds`；原未解开菜单提供的重置恢复该组踏板和辅助锁，不清除奖励实体状态或已领取记录。锁完成后按原配置生成独立奖励并切换终端状态，不额外增加重置入口；检查 覆盖全部五组原菜单、实际领奖、写盘回滚、冷加载和防重复领奖。

### 已确认的本地接线与边界

通天塔原炮弹演出：`prepare_map15_cannons.py` 沿固定版 `actionworldpuzzle.bundle` 的 `MyActionBullet(22108)` 实际飞行／停止 Playable 引用提取 14 个粒子发射器、14 份材质及 3 个网格，保留附着变换、原 GLES 算术、mip 链、Bilinear／各向异性、三轴尺寸及 TagManager 排序层。；按用户确认的本地规则播放用户已确认的本地发射路径，不冒充服务端碰撞实现。固定 APK／854_156383 CDN 输入与 864_156496 的两个独立同 CAB 依赖（c31b86c7ea536f280fd88aeaa0b8b557、5380c94014dd85cc88f5b8749f3225cf）分别登记于 `official-map/tower-cannons-source-manifest.json`、`tower-cannon-materials-source-manifest.json`，记录真实来源、URL、99 个 Unity 对象及输入输出哈希；未混入原版本缓存。运行资源位于 `assets/official/map15-cannon*`，shader 位于 `shaders/map15-cannon-particles/`，须加入 Unity 显式加载清单；没有新增自绘素材，新增素材尚未进入完整 ZIP 快照。

通天塔元素连线界面：`prepare_map15_element_link.py` 沿固定版 `854_156383` 的实际 prefab、PPtr、GUID 清单及配置转换六处石堡 182–187 的 ElementLinkGamePanel、24 个绑定 prefab、五种原线头、十种线段／消散段、RuleDialog、TipInfo1、ConfirmDialog 与 PopupWindow 片段。`tower-element-link-source-manifest.json` 保留 1,215 个原 Unity 对象及原代码、Luban MiniGameSettings／rule_tip 输入哈希；原 Canvas 顺序、状态切换、布局、缩放和动画使用原序列化值。129 个原粒子发射器、17 份材质、4 种 GLES 变体和原 mip 链独立输出至 `map15-element-materials` 与 `shaders/map15-element-particles`，输入输出哈希见 `tower-element-materials-source-manifest.json`。六个 Wwise 事件从原 UI_BGM_01、UI_SFX_01 等实际银行／跨包媒体提取，由 WWiser／vgmstream 保留原银行音量；原 BGM 循环点和全部音频哈希记录于主清单。以上使用 APK／匹配原版本 CDN，未混入新版包或自绘素材；用户已明确允许该界面的 39 个原 ParticleSystemNoiseModule 与火炮 11 个共同采用现有 FastNoiseLite 采样，保留原素材及频率、强度、滚动、曲线和阻尼；扰动轨迹与 Unity 存在差异，不声称 FastNoiseLite 为 Unity 原算法。输入差分依据和来源沿用上段；Unity 运行绑定尚待接入。

通天塔遗迹：固定 manifest `854_156383` 的原 WorldRuinDialog／StageDialog、两种建筑预览、GameRuinEnterNextStageDialog／ConfirmDialog、二十层配置及竹林／雪地背景已用于六处入口与原下一层界面；`prepare_map15_ruins.py`／`tower-ruins-source-manifest.json` 保留九个 prefab、1,433 个 Unity 对象、57 个资源、原代码／语言包／清单和实际输入输出哈希。原 Image 射线标记、PopupWindow 动画、层状态、进度开关和按钮按原绑定迁移。遗迹 17–19 原配置地图 42062 与实际放置地图 42063 的差异保留。固定版完整清单中两条层背景仅有同名 BigMapStageBg 原图；经用户确认，在资源绑定中修正 MapStageBg→BigMapStageBg，不修改原层配置或素材。服务端层推进／结算执行器缺失；首层立即进入、按原 StageId／顺序战斗、战败重试、本层原追加物领取、已探索保存及退出保留采用用户确认的离线适配，具体差异见 docs/maps/map-15.md。

## 尚未实现

恢复鲸背岛原入口、提交43039×5绳索、准备／登岛／离岛剧情、43区及38条任务、45场战斗和五组踩格机关；领取奖励770／771和退出恢复需完成实际本地事务验证。

恢复原场景、导航、实体、条件、剧情／机关／战斗、奖励、出口及全部界面／表现绑定。

接通玩家入口、本地事务、退出／冷读和失败恢复，完成 Unity 游玩与对应 Windows 发布验证。

## 代码与资源入口

`scripts/prepare_map13_minimap_ui.py`（来源工具标识）、[`minimap-16-source-manifest.json`](../../assets/reference/official-map/minimap-16-source-manifest.json)、

`scripts/prepare_map16_dialogs.py`（来源工具标识）

`scripts/prepare_map16_rendering.py`（来源工具标识）、[`prepare_map16_world_ui.py`](../../scripts/prepare_map16_world_ui.py)、

[`prepare_map16_alone_ui.py`](../../scripts/prepare_map16_alone_ui.py)

[adventure.json](../../assets/official/adventure.json)、[16.json](../../assets/official/regions/16.json)
