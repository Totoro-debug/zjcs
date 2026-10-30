# 地图10：初始岛

[总索引](../README.md)

状态：未完成

## 地图简介

地图10：初始岛，范围按下列原配置与本地决策维护。

## 原始官方实现

Map10 已接入固定版官方地图定义、导航网格、原生地形网格，以及地图战斗实体所需的 11 个官方怪物 idle 模型与动画帧，共 309 帧。另已提取地图引用的 21 个官方阶段、13 个波次阵形、对应战场网格，以及按官方阵形、怪物组、等级属性和技能道具配置展开的战斗编队。角色进入地图、实体交互、剧情、战斗、HUD 和存档恢复尚未串成可玩流程。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

地图 10 单独重建固定版本 `871_156598` 的 `MapGround_10.prefab`；来源仍为原 APK / 已校验原 CDN。`scripts/prepare_map10_materials.py` 仅输出地图 10 的地形、独立蝴蝶数据与水面 10 条目，不改共享 `regions/skeletons.json`，不生成新版替代或自绘图。详细原对象、输入 / 输出 SHA、旧输出与纠正依据见 `official-map/materials-10-source-manifest.json`；原 `regions` / `water` 来源历史保留。
Map10 Windows 原生地形和怪物帧资源由 `scripts/prepare_unity_map10_rendering.py` 生成，逐项来源、路径和 SHA256 见 `unity/Assets/Official/World/Map/10/rendering-source-manifest.json`。地形来自固定版 `assets/official/regions/10.geom`（SHA256 `cdd26c364ce85d62bd5225bd23fda7b1f7b5ec7d7dd446fba5f3bec6f08618ff`），恢复 1,404 个地形网格批次和 166 张原始纹理；11 个地图战斗怪物按 `map10.json` 官方 `Stages` 值关联原始模型及骨骼 idle 帧，共 309 帧。`scripts/prepare_unity_map10_battle_data.py` 按固定 APK 的官方 `TbStageInfo`、`TbStageMonsterArrayInfo`、`TbSkillInfo`、`MonsterGroupInfo`、等级属性及已核验战场数据生成 21 个阶段、13 个阵形、逐只怪物的战斗属性/技能/站位及对应战场网格；`battle-rules.json`、`skill-rules.json`、`rules.json` 和公式源码均校验固定版本及来源哈希，详细输入输出见 `assets/reference/official-map/map10-battles-source-manifest.json`。模型、骨骼及 atlas 的原始来源继续由 `models-source-manifest.json`、`spine-pages-source-manifest.json` 和 rendering manifest 交叉记录。Unity 侧由 `OfficialMap10TerrainView`、`OfficialMap10MonsterView` 生成运行时网格；地形水效、实体场景接入和游戏运行尚未完成。

Map10 `MapBgmId=10` 的固定版 `bgm_event_key` 记录为 `Map_10_Play/Stop/Pause/Resume`；`Map_10_Play` 复用已核验 MapAudio OGG，离线运行以循环音轨适配官方 BGM Play/Stop 生命周期。剧情和 MapTalk 共 39 个 `VoiceEventKey` 由 `scripts/prepare_unity_map10_audio.py` 接入 `unity/Assets/StreamingAssets/Map10Audio/`：25 个 OGG 复用已核验的 `assets/official/map-audio.json` 与 `audio-source-manifest.json`，14 个缺失项从固定 APK `zh_cn_voice_chapter_01_pck.rawfile` 按官方 Event→Action→Sound→WEM 解码为 PCM WAV。三个 `FeiLe_appear`、`FeiLe_disappear`、`npc_disappear` playable 声音事件也按原轨道加入本地映射；声音媒体分别位于固定 `map_sfx_01` PCK 中记录的 bank/offset/size，提取器校验包 MD5、银行范围和 WEM SHA256。对话/演出时间、原来源、输出哈希与映射见 `assets/reference/official-map/map10-audio-source-manifest.json` 和 `Map10Audio/map10-audio.json`；运行播放由 `OfficialMap10AudioPlayer` 绑定。

8402 初始岛故事 NPC 的 `npc_8402/appear_01`、`npc_8402/disappear_01` 由 `prepare_unity_map10_rendering.py` 从固定版 `story-actors-source-manifest.json` 验证并生成 Unity 原生帧载荷；帧数、贴图与输出 SHA256 记在 `unity/Assets/Official/World/Map/10/rendering-source-manifest.json` 的 `storyActors` 条目中。两个载荷复用官方 `npc_8402` atlas，不含自绘资源。

26 个 `Sprites/Submersion` 分层保留 Rock01/Rock03/Rock04 的原 `_HeightMap`、atlas UV、renderer 高度偏移和 0.5 水位；官方 `General_Manual_mask_atlas` 解码为原贴图。水下层在水面后绘制，上水层在水面前绘制。4 个原 `Sprite-Lit-CustomWater2` 水面仅在地图 10 使用原 GLES 的红色高度、深浅色、岸线噪声、法线、焦散和泡沫参数。原 `Sprites/WaterMask` 的 `clamp(texture.a*renderer.a+renderer.r-renderer.g)` / `BlendOp Max` 按 430 次原遮罩绘制离线合成为 `assets/official/map-water-masks/10.png`；world rect 为 `[-24,-29,50,53]`，64 像素 / 世界单位，3200×3392。这是官方数据派生转换，使用固定采样密度，不声称与 Unity 的逐帧屏幕像素完全相同。

历史 `missing_skeleton_data: Effect_Butterfly` 是转换器只接受 `.skel`、漏识别原 `.json` 导致的误判，不是官方素材缺失。原包实际含 JSON Spine 4.1.17、atlas、Texture2D 和动画。新增 `assets/official/regions/10-butterflies.json` 保存 15 个原激活实例、原分配的 `loop1` / `loop2`、12 fps 网格帧与原精确循环时长；同时接入 `Eff_Scene_Butterfly01` 原飞行 / 旋转曲线（3.983333349 秒循环）。15 个静态序列化网格实际剔除，原 `Effect/Noise_Mask_Pro` 的 `USE_MAINTEX`、绿色 `_MainColor` 和 `_ColorIntensity=4` 应用到动态网格。旧缺失记录保留并在新清单中明确纠正。转换后地形为 1404 批、17856 顶点，15 个独立原骨骼实例，不再绘制原静态蝴蝶替身。

CAB 读取器通过原清单中唯一相同 MD5 解析旧缓存的 `_materials.bundle` 别名；真实名称为 `defaultpackage_assets_textmesh_pro_assets_fonts_&_materials.bundle`。原 APK、缓存、预期 MD5 / SHA 和来源版本均未修改。新资源须在协调会话最终共享注册与素材包锁生成时一并纳入。

祈愿教学使用固定 `871_156598` 的原 `FurnitureRepairDialog`、`FurnitureRepairedNotifyDialog` 与 `StatueOfGod` prefab，运行目录为 `assets/official/home-wish-tutorial.json`；独立来源为 `home-wish-tutorial/source-manifest.json`，保留原 prefab / bundle / path_id、客户端代码指纹、9 项原语言键和 18 张既有官方贴图来源。18 张贴图的重新解码结果、旧共享清单与当前磁盘 SHA 完全相同，仅复用，未新增或重绘这些贴图。最终教学 catalog SHA 为 `aad8443772a737548cad9f88eb009f25ce93087c46c9641c25c37a8ba1dc94e8`，独立来源 SHA 为 `8c7653b998fef0fe73f70501a84af496c67ced42c67512390867d9dcfd331b60`。

教学在原家园设施碰撞入口执行修复、成功通知、奖励与实际祈愿点击检查点，使用原家具 4004 条件 / 修复费用、fixed_reward 8 奖励及原祈愿 / 快捷购买价格；本地事务与持久化承担离线执行。其他家园教学图的完整调度、场景 NPC IdleTreeRight / PortalLeave 演出和原家具动态分类不在这次适配范围。

另一张 `FirstMergeItem` 教学图请求固定奖励 ID 7。固定版 `fixed_reward.g.bytes` 由原 `TbFixedRewardInfo` 构造器完整解码，ID 7 是罗拉 350、水 10、青禾 5，`CanGetTimes=1`；`prepare_unity_wish_catalog.py` 将 ID 7 与祈愿教学奖励 ID 8 一起写入 Unity 运行目录，输入和输出哈希记录在 `assets/reference/wishes/source-manifest.json`。

临时来源 `assets/reference/official-map/wish-tutorial-source-manifest.json` 已迁至独立模块；原来源 SHA `d59a5aefd96ee96229452f35357c73f85bebd4a401b027679e255489d85d6186` 与旧 catalog SHA `4da99eb146fe351591fdb6af7ae25abcbb387f851c4c9e024d64084b3f6f2261` 继续保留于迁移记录，没有改写成新的来源历史。

最终共享登记使用 `python scripts/register_map_assets.py --map10`，仅合并 `official-map/materials-10-source-manifest.json` 与独立教学模块，核对实际文件 SHA，保留同字节复用资源的原共享凭据。地图 10 的四份运行数据、两张转换图及教学 catalog 共七条运行路径全部有数据 / 资源哈希和显式加载绑定；新原生 shader / UID 位于 Git 代码目录。`--map10 --check` 可只读检查重复登记结果。范围外 NPC 旅行 / 果树新增资产由各自会话维护，没有登记它们；独立素材包与锁由主协调会话随后按批准增量生成。

`OfficialMap10BattlePlayModeAcceptance.RunRealInput` 在快捷聊天、窗口缩放和 HUD 截图检查全部结束后才恢复地图战斗执行，避免短战斗提前结束、销毁 HUD 并使检查持有失效的 `RectTransform`。静态 metadata 检查（40,688 个 GUID）、Unity Editor 导入／编译、`ProjectTools.Validate` 及该入口的 D3D11 Play Mode 回归通过；模拟指针经过实际射线命中和按下／抬起／点击事件，覆盖开始战斗、快捷聊天、450×800／675×1200 布局、技能图标、时间轴和返回地图，并实际查看运行画面。验证使用临时档案，不覆盖用户存档；该范围不代表完整地图或游戏迁移完成。

## 完整还原部分

Map10 原生 prefab 的来源清单记录 UTF-8/LF 字节哈希；根目录 `.gitattributes` 为两个 prefab 禁止 Windows 换行转换，使 `ProjectTools.Validate` 在 `core.autocrlf=true` 的检出中仍按原始 1,439／1,335 字节核验。模型索引也保留其来源文件的混合换行字节，以匹配清单 SHA256。

地图公共能力由 `OfficialMainlineMapRuntime` 组装 HUD、区域／雾、四类雕像、导航、体力、探寻、小地图七个独立状态模块；本图资源、剧情和进度按地图 ID 注入。模块边界、共享规则与未接入项见[主线地图公共模块](../features/mainline-map-modules.md)。按地图 ID 装载本图资源，沿用本图存档与回执。

固定 APK 转换出的 Map10 定义和数据来源位于 [`unity/Assets/Official/World/Map/10`](../../unity/Assets/Official/World/Map/10/)。`OfficialMap10TerrainView` 读取官方地形顶点载荷并绑定 1,404 个批次及 166 张原始纹理；`OfficialMap10MonsterView` 按原始骨骼采样帧播放官方模型。`scripts/prepare_unity_map10_rendering.py` 校验地形、模型、骨骼序列和纹理来源哈希。Map10 的 11 个战斗展示怪物共有 309 帧 idle 动画，按官方地图实体 `Stages` 映射至模型 ID。`OfficialMap10MiniMapView` 从固定版官方 geometry 和五张原始纹理构建九个小地图批次，`scripts/prepare_unity_map10_minimap.py` 校验固定 APK 来源、输入哈希和资源绑定。`scripts/prepare_unity_map10_battle_data.py` 从固定 APK 提取 21 个阶段（20 个实体阶段和切换阶段 9910）、13 个阵形及战场网格，并按 `StageMonsterArrayInfoParser.GenerateMonsters` 展开各波怪物；属性使用官方 `BattleFormulaHandler.CalcMonsterBattleProps` 与固定 `battle-rules.json` 等级表，技能道具通过 `TbSkillInfo` 映射到 `skill-rules.json` 战斗实体。首段 stage 1033（GameId 1000）由阵形 1205 展开为三只 3281，站位为 (10,13)、(12,12)、(11,13)，技能 14235/14236 分别映射至实体 18140/18150。固定版本、公式源码、输入输出哈希记录于 [`map10-battles-source-manifest.json`](../../assets/reference/official-map/map10-battles-source-manifest.json)。Unity 检查入口为 [`OfficialMap10TerrainTools.cs`](../../unity/Assets/Game/Editor/OfficialMap10TerrainTools.cs) 和 [`OfficialMap10Tools.cs`](../../unity/Assets/Game/Editor/OfficialMap10Tools.cs)，分别验证地形/怪物载荷及原生小地图、官方地图 catalog；官方 60×60 导航、移动掩码 65、最大寻路步数 27 及本地相邻格路径检查仍保留在 `OfficialMap10Tools`。这些编队数据尚未由 Map10 运行时装配为战斗检查点。原始 Android `MapGround_10` / `MiniMap_10` prefab 因平台与 CAB 依赖未直接用于 Windows。

`OfficialMap10EnvironmentView` 在 Map10 运行时实例化 4 个官方水面网格，并将法线、焦散、泡沫、交界噪声、岸线遮罩及原材质纹理缩放绑定到水效材质；15 只蝴蝶使用官方 Spine 网格帧、`loop1`/`loop2` 动画和场景飞行/旋转曲线。`scripts/prepare_unity_map10_rendering.py` 由固定版配置、材质来源清单和原始纹理生成运行载荷；`rendering-source-manifest.json` 记录输入/输出 SHA256、材质属性、贴图绑定与重建 Shader 哈希。固定版只有 Android GLES 编译字节码，没有可移植 Windows Shader；经用户确认，`Map10Water.shader` 与 `Map10Butterfly.shader` 按官方材质参数、关键字和贴图重建，保留这些来源值，但不保证与 GLES 逐像素相同。`OfficialMap10TerrainTools.ValidateEnvironment` 在 Unity 中核对 4 个水面、15 个蝴蝶、2 段动画和 19 个运行时渲染器。

`OfficialWorldMapAssetCatalog` 与 `OfficialWorldMapAssetProvider` 拆分到同名脚本，修复 Unity Resources 目录资产的脚本绑定；`OfficialMap10Tools.Validate` 同时核对运行时目录加载。地图战斗在官方 `EvaluateProps().OutOfFightBattleProps` 流程中接入装备目录，避免使用未转换的 Base 属性作为最终战斗属性。`OfficialMap10PlayModeAcceptance.Run` 提供隔离入图、HUD、对话推进、实体和环境载荷检查入口；该入口尚未成功完成运行验收。

`OfficialMap10UiTools` 保留原 Sprite 输入哈希、尺寸、PPU、轴心／边框检查与显式目录绑定；`StreamingAssets/Map10Audio` 保存43个原剧情语音／演出／BGM事件的本地音频与映射，输入切片和输出哈希由 `map10-audio-source-manifest.json` 记录。音频来源、三段 NPC 演出及小地图转换的五项 Python 检查通过。运行数据／对话来源检查仍各有一项失败：现有 `assets/official/adventure.json` 实际 SHA256 为 `a6820d65b7498af98f242ab797c24f5dc92646b0638f09da8331c793a0340b4e`，原清单期望 `84b92c87a8cde16a1f1cb377ca8c03629ed2e4f522af04637d5db2e260f90aec`；未改动该输入或预期哈希。结构与原对话几何检查可通过，不据此声明全部来源检查或地图音频实际播放通过。

Windows D3D11 构建已复现并修复水面 shader 的 `TRANSFORM_TEX` 复合参数展开错误：对传入的 UV 表达式整体加括号，避免 `.xy` 被应用到末尾标量。保留原有参数、纹理及 UV 计算表达式；原生重建文件的输出哈希同步到 `rendering-source-manifest.json.environment.water.material.reconstructionSha256`。Windows Player 构建通过且无 shader 编译错误；该项不代表地图水面动态画面与完整路线已验收。

## 自定义实现

`OfficialMainlineMapRuntime.RunStoryGraph` 对 `RemoveMapExclusiveNpcNode` 只保留一个分支：配置删除 playable 时先播放，再删除 NPC 并沿 `Exit` 继续；未配置时直接删除。执行顺序依据官方 `RemoveMapEntitiesByCfgNode.PlayPlayables/OnPlayableStop`。工程启用 Unity 内置 `unitywebrequest` 与 `unitywebrequestaudio` 模块，供 `OfficialMap10AudioPlayer` 从本地文件 URI 解码音频；模块启用不代表音频播放已验收。

地形与怪物动画使用官方原始网格、纹理、骨骼帧数据，经 Unity MeshRenderer 适配为 Windows 原生资产；未自绘或用静态图替代。现有本地地图进度 DTO、地图寻路和战斗执行器尚未由 Map10 运行时调用。

### 已确认的本地接线与边界

Map10 的 `FeiLe_appear`、`FeiLe_disappear`、`npc_disappear` 由 `scripts/prepare_unity_map10_playables.py` 按原始 playable 与 `map-particles.json` 转为 Unity Resources 粒子载荷，并校验材质、贴图、网格及输出 SHA256；详细输入/输出见 `assets/reference/official-map/map10-playables-source-manifest.json`。固定版缺失材质 `2db68f5a7be450142e43` 的原 CDN 包 MD5 为 `959e064e1a060a6ed93bad71ca11a8f0`，已按独立记录使用官方 `864_156496` 同名包 `758d1b10bbe6ee211e8de79dbb727ec1.bundle`；未将其标为固定版来源。其他材质来源均逐项注明 `871_156598` APK 或原版本 CDN。原 APK 的 GLES 编译 shader 无 Windows HLSL，`OfficialMap10PlayableView.shader` 是经用户确认、按官方材质参数和贴图重建的 Windows shader，不宣称逐像素等同。

Unity 资源转换器现将 Map10 的 4 个原水面网格、15 个蝴蝶骨骼实例、两段蝴蝶动画和 5 张官方贴图复制到 `Assets/Official/World/Map/10/Resources/Map10/Environment/`。`map10-water.json` 是固定版 `map-water.json` 的 Map10 条目，`10-butterflies.json` 保持原始字节；对应的 `map10-water-runtime.json`、`map10-butterflies-runtime.json` 和两个循环帧载荷供 `OfficialMap10EnvironmentView` 运行时实例化。`rendering-source-manifest.json.environment` 记录固定版输入哈希、输出哈希、纹理 URI 到 `Resources.Load` 路径、材质参数与蝴蝶动画帧数。水法线按 NormalMap/线性导入，派生水遮罩按线性导入；`Map10Water.shader` 和 `Map10Butterfly.shader` 是经用户确认按官方参数、关键字与贴图重建的 Windows shader，保留来源值但不保证与 GLES 逐像素相同。Unity `Zjcs.Editor.ProjectTools.Validate` 已通过水面/蝴蝶载荷、贴图绑定、shader 导入及 19 个运行时渲染器检查；完整地图 Play Mode 流程仍未验收。

## 尚未实现

- 小地图玩家标记与区域遮罩、WorldPanel 和实体 UI 的 native 资源接线、Map10 场景 catalog 与摄像机绑定待完成。
- 82 个放置实体的原始模型/状态绑定、角色外观换装、寻路移动、点击交互及地图进度存档待接入。
- `FirstMeetChaos`、地图教学、其余官方剧情、对话与镜头演出待接入。
- 怪物属性、技能和编队已按官方表生成，但尚未接入 Map10 战斗检查点、角色模型、原始战斗 HUD、步骤演出及战斗结算后的阶段/奖励推进；已确认首段剧情战斗即使失败也按成功继续。
- 新角色至地图出口的完整路线、重新载入后的状态恢复、Windows 实际运行和画面验收未完成。Map10 相关 Python 测试 24 项通过；Unity 编辑器已通过水面/蝴蝶载荷、shader 导入与 19 个运行时渲染器构造检查。最新战斗属性接线尚未通过 Unity 复验，完整 Play Mode 仍未验收。
