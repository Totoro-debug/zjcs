# 世界地图

[总索引](../README.md)

状态：未完成

## 功能简介

本页描述 Unity 世界地图共享底层迁移状态，包括原地图数据、导航与进度。单张地图只有在其官方资源、交互、存档及实际游玩流程完成验证后才可标记完成。

## 原始官方实现

原世界地图包括大陆总览、地图／地区配置、导航格、原地形、放置实体、小地图、探索点和进度计算；剧情、机关、条件和奖励按各自配置调用。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

大陆总览移植：新增 `prepare_official_maps.py`，直接从固定 APK 转换 `WorldMapIsland`、`WorldMapSky`、`WorldMapSpace` 的 RectTransform、原图、九个地点 ID、背景自适应和缩放参数。登记 33 个原始 Sprite 与 3 张星球 Spine 图集，其中实际新增 29 张 PNG；其余图像复用既有文件。原始数据、输出哈希、bundle / 对象 ID、完整消费的三张地图配置表和效果缺口见 [地图来源清单](../../assets/reference/official-map/source-manifest.json)。

本节记录后续素材转换及实际替换，保留前文地图总览阶段的来源历史。范围为固定版本全部 373 个地图定义，包含九个大陆地区、副本和活动地图。

来源继续使用固定 APK `854_156383` 和匹配原清单的已校验 CDN 缓存。373 组原始导航 / 放置表输入凭据见 [map-layout-source-manifest.json](../../assets/reference/official-map/map-layout-source-manifest.json)。地形、实体模板、外观、交互包装、界面、昼夜层、水面、剧情媒体及音频分别登记在 `official-map/` 对应 `*-source-manifest.json`；当前地图资源注册包含 18,559 项本地资源，并保存输出数据 SHA256。已缓存新版同名包仍为独立研究来源，没有冒充原版本补齐。没有新增自绘位图。

`prepare_map_minimaps.py` 读取原 `MiniMapPrivateRegionMaskManager` 的动态分区 Tilemap GUID、原 manifest 资源路径、Grid 单元尺寸和世界变换，补转换静态根 prefab 未包含的地图地块。149 个地图配置共用 65 份原始小地图，包含 1,788 条配置分区引用；转换记录 3,148 项输入对象凭据、5 张解码纹理及网格输出哈希，见 [minimaps-source-manifest.json](../../assets/reference/official-map/minimaps-source-manifest.json)。原编辑器玩家占位图不作为地块叠加。全屏地图接入 169 种原 MiniMapMark 外观、筛选、定位、滚轮 / 原滑条缩放、拖动和已激活入口传送。地图交互对话根据原 DialogIconPrefab 或 NPC FigureImage 绑定官方图像。

原 WorldStage 组件、970 个原始剧情图及 15 个旧版对话适配图在本地执行。NPC Touch 与 Function 分开提供交谈和委托，支持原始提交物品流程。私有 / 公共区域生成条件、区域状态条件和原探索度的 100 点激活加成已接入。无女神像区域在私有探索全部完成后转为公共状态，是缺少服务端实现时的单机适配；没有把这个推断标为已取得的原服务端规则。原战斗属性计算仍使用项目的本地规则。

活动的 522 条原始关卡配置保留赛季和关卡各自进度。踩石板接入方向、进入次数和传送落点事件；活动棋盘和世界机关的完成标记分别保存，离开地图再返回恢复当前棋盘。

当前地图功能及剩余实现范围见 [世界地图功能文档](world-map.md)。

踩石板解法工具 `scripts/solve_map_plates.py` 使用独立开发目录 `.tools/map-solver-python` 中的 OR-Tools 9.15.6755，不进入运行资源。

浅水与浮桥补齐：`prepare_map_fording.py --scan-all` 检查全部 2,355 个实体模型 prefab，转换 132 个使用原始 `CustomWater.FordingSpriteRenderer` 的模型，并刷新对应状态外观。网格按原 Sprite、pivot、pixels per unit、颜色和世界变换生成；原 `Sprite-Lit-Fording` 的压缩 GLES 程序解码后移植水深、噪声、浅深色混合和边缘公式，使用原 WaterController 的参数与 FordingTex。另检查全部 373 个地形引用的 6,177 个 prefab，未发现需要补转换的 FordingSpriteRenderer，见 [地形运行组件检查](../../assets/reference/official-map/ground-runtime-renderer-audit.json)。未新增替代位图。

依据：[fording-source-manifest.json](../../assets/reference/official-map/fording-source-manifest.json)。

浮桥采用原 `Sprites/Submersion` 的高度贴图和双通道颜色高度偏移；原 Mecanim 升降关键帧驱动物件位置、遮罩高度和阴影透明度，见 [材质来源](../../assets/reference/official-map/materials-source-manifest.json)。

离线规则对单状态、DestroyOnEnd 且没有后续结果的一次性 Touch 记录完成；多状态传送和重复开关仍按各自结果执行。这是本地完成状态适配，不将客户端未提供的服务端逻辑声称为直接提取。

`map-legacy-displays.json` 先匹配原地图路径，再使用名称精确对应 `class_view` 的唯一原始 prefab 或 `Map/Common` 原始 prefab；有歧义的地区变体不作全局选择。真实输入、候选路径、输出哈希见 `official-map/extra-models-source-manifest.json`，完整绑定记录见 `official-map/views-source-manifest.json`。平台按原始 OffsetPath 逐格移动，使用原 GridMover 地面掩码等待被升起路面挡住的轨道，携带站立角色并保存当前站点、方向和运行状态；触发后的单段行程及本地定时推进是缺少服务端实现时的离线适配。

状态、活动刷新点与剧情导演补充：`prepare_map_status_effects.py` 读取原始状态效果、作用范围及地图 Action 组件，输出 `assets/official/map-status.json`，凭据见 [status-source-manifest.json](../../assets/reference/official-map/status-source-manifest.json)。本地执行战斗作用范围、次数、离图移除、原始移动倍率和环境伤害；烟雾保留施放格及三格范围，护盾、烟雾按原 Action 的十秒持续时间执行，变身解除原隐身。状态图标的五秒寿命和地图 Action 的十秒寿命分别保存，重载不重新计时。雷霆随机落点及其完整场景演出仍需补齐，不能据此称全部状态效果完成。

`prepare_map_activity_points.py` 提取原始活动刷新点类别、逻辑格、分区和变换，输出 `assets/official/map-activity-points.json`，凭据见 [activity-points-source-manifest.json](../../assets/reference/official-map/activity-points-source-manifest.json)。悬赏及躲猫猫道具选择原类别的可用点；金币使用原 ActivityEntity 的奖励规则，刷新数量与上限使用原关卡 / 道具表。被捕后的抓捕次数、狂暴倍率、协助金币已按配置接入。追捕参与者由本地代理控制，这是单机适配；未取得的服务器 AI 和匹配规则不作原样迁移声明。

`prepare_map_timelines.py` 离线解析原 Story Timeline GUID、PlayableDirector 绑定、父子导演、时间缩放、Cinemachine 相机 / 镜头参数、Mecanim 曲线、AnimatorController 参数与状态、Spine 片段、激活、信号和 Wwise 事件。转换 138 个原始场景，含 1,010 个 Animator 组件记录，运行文件在 `assets/official/map-timelines/` 与 `map-timelines.json`。Spine 分包按原 SkeletonData 的 SplitBinarySO 字符串表、动画名和 GUID 获取并解码，不把已有但未解码的动画列为素材缺失。原始 bundle / 对象 / CDN 凭据、逐文件输出哈希及未解码绑定记录见 [timelines-source-manifest.json](../../assets/reference/official-map/timelines-source-manifest.json)。解码辅助 schema 使用 Unity Timeline 1.7.6、Cinemachine 2.9.7 和 Spine 4.2 官方源码，其下载 URL、SHA256 及 schema 代码哈希均独立记录；这些开发依赖不进入游戏导出。

原剧情引用 139 个导演 GUID，其中 `FeiLeCaptured2` 法杖分支的 `7efc66a61627d5d4c9014986ad32a5cf` 未出现在原版本五包 AssetTable 或已核对的新版 AssetTable 中。相同事件附近另有原版 `CelestialFeatherRealm_Babel_StoryShow02_2.prefab`，目前仍在核对其适用性，尚未声称原缺失引用已恢复。27 项动画绑定 / 缺名动画记录保留在清单；相机跟随、部分材质曲线、复杂 Animator 混合及玩家专属参数演出仍需核对。这不是全部剧情演出完成的结论。

私人副本胜利改为推进一个原始入口状态，避免跳过结晶螺旋树的战后剧情；公共重复挑战保留建筑激活状态。旧本地存档仅恢复尚未播放的原战后检查点，不重复发放已领战斗奖励。

本节更新地图模块的实际接入状态，前文 2026-10-13 的资源数量及“新版尚未接入”保留为历史记录。`prepare_map_particles.py --use-verified-counterparts --upgrade-particle-motion` 检查当前实体、地形、Timeline、Playable 和剧情角色引用的 8,706 个 prefab，转换 3,010 个带粒子的 prefab、41,551 个发射器及原始材质纹理，当前转换依赖无失败。转换结果为空时移除旧缓存，清理 54 个已失效 prefab 的 278 个隐藏发射器。资源、对象、输入哈希、原始 URI 及逐文件输出哈希见 [particles-source-manifest.json](../../assets/reference/official-map/particles-source-manifest.json)；运行文件位于 `assets/official/map-particles/` 和 `map-particles.json`。未新增自绘位图。

原版仍缺失的粒子依赖通过 `official_counterpart_assets.py` 显式补充。累计使用 68 个已校验的 `864_156496` 独立缓存包，以精确 CAB 身份及可用外部依赖核验后解析；凭据含新版真实 URL、大小、MD5、SHA256、对象 ID 和所对应的原版缺项，见 [particle-counterparts-source-manifest.json](../../assets/reference/official-map/particle-counterparts-source-manifest.json)。原版预期哈希和缓存没有被修改，新版文件没有冒充原版补齐。

粒子转换保留原生命周期、发射、突发、形状、旋转、速度、颜色、尺寸、UV 帧、网格、状态显隐、材质滚动、噪声参数及拖尾。Unity 内部噪声使用本地 Perlin 播放，噪声算法一致性不作原样声明。子发射器、碰撞及部分 3D 参数仍需继续核对，具体未转换模块保留在来源清单。373 张最新地图截图仍在重新生成。

`prepare_story_playables.py` 转换 36 个原始序列；原引用中两项 GUID 未匹配，未采用相邻素材替代。原始开始时间、持续时间、层级、动画及可解析效果见 [playables-source-manifest.json](../../assets/reference/official-map/playables-source-manifest.json)，运行数据为 `map-playables.json` 和 `map-playables/`。`prepare_story_actors.py` 转换 95 个剧情角色场景，保留原变换、Animator、动画关键帧与 Spine 分包动画，见 [story-actors-source-manifest.json](../../assets/reference/official-map/story-actors-source-manifest.json)。

剧情副本引导根据原目标建筑推进入口点击状态，再接续原私人副本；

躲猫猫准备区限制按原 `MapHideAndSeekInputSupportHandler`、`HideAndSeekHiderVisualStateUtils` 和 `Region0Enterable` 实现；生存金币按原间隔和金额执行，感染后的追捕身份按原 GameMode 区分。完整雷霆视觉演出仍在核对。

地图剧情挂点补充：`scripts/prepare_map_story_bindings.py` 检索固定版全部 MapWorld 逻辑实体 prefab、地图显示 prefab，以及 373 个地图的基础地形和分区昼夜场景，从原 `StoryNodeRegister` / `StoryNodeDelayRegister` 提取目标与父级变换。11 个剧情引用键全部找到，分布在 13 个原逻辑实体类；没有缺项或解析失败。转换结果为 `assets/official/map-story-bindings.json`，原 schema、bundle / 对象、输入哈希、原坐标及输出哈希见 [story-bindings-source-manifest.json](../../assets/reference/official-map/story-bindings-source-manifest.json)。重复分区使用同名注册键时，离线运行选择当前地图最近的可见注册实体，此选择规则单独登记，不声称与 Unity 流式加载顺序相同。

重建挂点使用 Python 3.12 与既有 UnityPy / numpy 开发依赖，设置 `PYTHONPATH=.tools/character-art-python;scripts` 后运行 `scripts/prepare_map_story_bindings.py`，再运行 `scripts/register_map_assets.py`。`GetEntityPos`、坐标类型转换、NPC 删除和镜头目标使用实际原实体引用；相机转换包含地图原点与格子中心。原三处 `TimeSequenceNode` 的七个并行移动执行器，以及主线的移动交互节点，接入本地保存的独立时钟；没有新增或替代图像。

全屏剧情插图复用已转换的原 `StoryScreenImagePanel`、`map-story-media.json` 中的官方插图与 `screen_image_panel_front_shake` 原曲线。淡入、双图混合、关闭淡出、等待震动结束和演出时钟按原节点执行；插图面板与曲线不是自行绘制或合成的素材。

两场音乐节的 `Map/Common/FirstMusicFestival/7003_view` 与 `Map/Common/LoTIanYiMusicFestival/7003_93_view` 原 `ECEntityOverlapDisplayView.mDisplaySwitcher` 控制占用时的粒子显隐。`prepare_map_interactions.py` 保留该显示类型，`prepare_map_particles.py` 按原布尔状态转换隐藏的官方发射器；运行时检查角色与可见实体的实际身体格交集。未占用、占用与移开使用原粒子资源，输入／输出哈希保留在 `particles-source-manifest.json` 和显式资源清单；没有自行绘制踏板高亮。

`prepare_map_audio.py` 搜索全部已校验的原版 Wwise PCK，随后运行 `prepare_map_music.py` 转换复杂事件；重建时保持这个顺序，后者会扩充前者生成的目录。复杂转换严格解析 113 个原始 bank、734 份 media，使用 [wwiser](https://github.com/bnnm/wwiser) `v20260808`（commit `94580dd3de3280933d69c435e96d4e2c3b443207`）生成原生 TXTP，以 [vgmstream](https://github.com/vgmstream/vgmstream) `r2117` 渲染原始分段、叠层、音量和循环，再用 libsndfile 编码本地 Ogg。保留原 bank 音量、开头与循环起点；没有合成或替代音乐。15 个复杂音乐事件共有 29 个配置分支，另转换 3 份 Map23 过渡音频；13 段内嵌 bank 语音亦已取得。版本、包来源、输入和输出哈希、严格解析结果及工具哈希见 [music-source-manifest.json](../../assets/reference/official-map/music-source-manifest.json)。

运行目录共覆盖 4,632 个音乐／语音事件、49 个 BGM 编号；火／冰、普通／混沌、三种岩层区域和活动歌单按原控制事件选择。Map14 的 `chapter_5_main_36_17` 是原剧情时间轴路由键，对应语音资源与演出时序见[龙之国地图页](../maps/map-14.md)。Map23 的九段顺序过渡片段与原 132 BPM／四拍节拍、来源专用过渡规则、淡入淡出、前奏／尾奏提示点已接入跨区域播放，记录见 music-source-manifest.json。

`prepare_map_activity_boss.py` 按固定版原构造器完整消费解码 4 张首领表，另解码 `monster_group`、`step_award`、`chest` 的共享池和奖励。地图 94、95、96、98 使用原首领、模型挂点、面板、关卡、难度修正、每日挑战／参与奖／击败上限、伤害阶梯和宝箱奖励；大额伤害拆成有界整数保存，符合现有地图存档格式。使用已转换的原始怪物属性公式，实际回合仍由项目本地战斗执行器结算。

依据：[activity-boss-source-manifest.json](../../assets/reference/official-map/activity-boss-source-manifest.json)。

原联网全服首领改由本地玩家累计伤害，个人与队伍成绩均来自单机角色；没有生成其他玩家成绩。普通首领使用原 region-zero 关卡；首次生成的首领等级按角色阶位约束在原修正表的有效等级范围内，随后保存。周年庆保留原二十倍场外生命值和每日最多三次击败，重复挑战不额外设置次数上限。没有新增位图。

## 完整还原部分

Unity 已建立地图数据模型与 `IWorldMapAssets.Load(int)` 资源接口。`OfficialMapNavigationView`、`OfficialMapPathfinder`、`OfficialMapGroundRules`、`OfficialMapExplorationProgress` 和 `OfficialMapProgressCalculator` 实现导航读取、A* 路径、地面规则、探索点累计与区域／地图进度计算。导航实现见 [OfficialMapPathfinder.cs](../../unity/Assets/Game/Runtime/Offline/World/OfficialMapPathfinder.cs)。相关代码位于 [`Runtime/Official/World`](../../unity/Assets/Game/Runtime/Official/World/) 和 [`Runtime/Offline/World`](../../unity/Assets/Game/Runtime/Offline/World/)。

Map10、Map11 已接入同一套主线运行时，具有固定 APK 导出的地图、区域、实体、导航、剧情与原生 UI 载荷；公共 HUD、寻路、区域条件、小地图和存档的复用及验证范围见[主线地图公共模块](mainline-map-modules.md)。单图仍按[初始岛](../maps/map-10.md)和[森之国](../maps/map-11.md)列出的缺项继续迁移，不能据此将整图标为完成。其他地图页保留各图官方配置、已确认的单机差异和待迁移范围，尚无完整 Unity 运行结论。

地图转换工具集中在 `scripts/prepare_map_*.py`、`prepare_unity_map10_*.py`、`map_native_animation.py`、`map_player_assets.py` 与 `official_ec_codec.py`；剧情／动作／时间轴转换沿用 `prepare_story_*.py`、`extract_story_character_actions.mjs` 及相应来源清单。保存这些工具和地图文档不等于其全部输出已接入 Unity；运行时只消费随工程打包的本地载荷。

### 已迁移代码与局部资源

以下为局部代码／资源接入，完整流程仍受本页缺项限制。

地图重建命令为 `python scripts/prepare_official_maps.py`，复用本页既有 UnityPy / Spine 开发依赖和固定 APK 校验，通过 APK ZIP 按需解析实际依赖；中间骨骼与采样数据放在 `.tools/source-packages/map-skeletons/`。运行时数据为 `assets/official/maps.json`，新图像纳入 Unity 显式加载清单，来源记录由 `verify_official_assets.py` 校验。

## 自定义实现

地图进度与探索采用本地单机数据。运行时不请求服务端；具体条件和奖励只在已有官方配置或明确的离线决定支持时接入。

- 按 `official-source.json` 选择输入，优先核对 APK，再读所选版本已校验的云端缓存；既有转换保留真实版本，跨版同哈希复用另记凭据，不冒充原版本补齐。
- Map14 对话音频的时间轴路由与本地素材核对见[龙之国地图页](../maps/map-14.md)；该路由键不作为单条直接语音事件处理。
- 地图、区域、位置、格访问、公共／私人实体、剧情、机关、活动、货架、战斗及领取状态须共同持久化，失败回滚，时钟回拨不刷新。旧 official.play.adventure 是规则映射参考，Unity 完整存档接线尚未完成。
- 服务器与运营日历开放采用[离线政策](offline-access.md)，个人等级、阶位、材料与真实地图目标继续检查。地图活动首次打开目录建立本地起点，内部阶段按原日界及 UnlockHours 推进；身份、回合、机关与领奖回执分别保存，共享地形不合并活动进度。

规则与验证参考：[主线地图公共模块](mainline-map-modules.md)、[命运探寻](world-exploration.md)及各图所属页。工程静态检查不能代替编辑器、Player 与实际游玩验证，相关边界见[Unity 工程页](unity-project.md)。

## 尚未实现

- 补齐 Map10／Map11 尚缺的官方资源与交互，并恢复其他地图的地形、区域、MiniMap、放置实体和角色资源绑定。
- 玩家移动、镜头、实体交互、剧情演出、机关、战斗、领奖及退出恢复的完整地图流程。
- 地图状态与正式本地存档的端到端集成。
- 各地图独立迁移及实际界面验收；共享 DTO、单张地图数据或导航规则不代表地图完成。
