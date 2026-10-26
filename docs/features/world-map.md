# 世界地图

[总索引](../README.md)

状态：未完成

## 功能简介

本页描述 Unity 世界地图共享底层迁移状态，包括原地图数据、导航与进度。单张地图只有在其官方资源、交互、存档及实际游玩流程完成验证后才可标记完成。

## 完整还原部分

Unity 已建立地图数据模型与 `IWorldMapAssets.Load(int)` 资源接口。`OfficialMapNavigationView`、`OfficialMapPathfinder`、`OfficialMapGroundRules`、`OfficialMapExplorationProgress` 和 `OfficialMapProgressCalculator` 实现导航读取、A* 路径、地面规则、探索点累计与区域／地图进度计算。导航实现见 [OfficialMapPathfinder.cs](../../unity/Assets/Game/Runtime/Offline/World/OfficialMapPathfinder.cs)。相关代码位于 [`Runtime/Official/World`](../../unity/Assets/Game/Runtime/Official/World/) 和 [`Runtime/Offline/World`](../../unity/Assets/Game/Runtime/Offline/World/)。

Map10、Map11 已接入同一套主线运行时，具有固定 APK 导出的地图、区域、实体、导航、剧情与原生 UI 载荷；公共 HUD、寻路、区域条件、小地图和存档的复用及验证范围见[主线地图公共模块](mainline-map-modules.md)。单图仍按[初始岛](../maps/map-10.md)和[森之国](../maps/map-11.md)列出的缺项继续迁移，不能据此将整图标为完成。其他地图页保留各图官方配置、已确认的单机差异和待迁移范围，尚无完整 Unity 运行结论。

地图转换工具集中在 `scripts/prepare_map_*.py`、`prepare_unity_map10_*.py`、`map_native_animation.py`、`map_player_assets.py` 与 `official_ec_codec.py`；剧情／动作／时间轴转换沿用 `prepare_story_*.py`、`extract_story_character_actions.mjs` 及相应来源清单。保存这些工具和地图文档不等于其全部输出已接入 Unity；运行时只消费随工程打包的本地载荷。

## 自定义实现

地图进度与探索采用本地单机数据。运行时不请求服务端；具体条件和奖励只在已有官方配置或明确的离线决定支持时接入。

以下为已确认的迁移约束，不代表当前 Unity 地图流程已经接通。

- 固定854_156383 APK 与已校验原版本 CDN 优先；适用的新版同 CAB 依赖独立登记，不冒充原版本补齐。
- 固定 APK 和已校验原版本 Wwise 包未找到 chapter_5_main_36_17，用户确认该事件静音，不补造音频；来源见 [audio-source-manifest.json](../../assets/reference/official-map/audio-source-manifest.json)。
- 地图、区域、位置、格访问、公共／私人实体、剧情、机关、活动、货架、战斗及领取状态须共同持久化，失败回滚，时钟回拨不刷新。旧 official.play.adventure 是规则映射参考，Unity 完整存档接线尚未完成。
- 服务器与运营日历开放采用[离线政策](offline-access.md)，个人等级、阶位、材料与真实地图目标继续检查。地图活动首次打开目录建立本地起点，内部阶段按原日界及 UnlockHours 推进；身份、回合、机关与领奖回执分别保存，共享地形不合并活动进度。


规则与验证参考：[主线地图公共模块](mainline-map-modules.md)、[命运探寻](world-exploration.md)及各图所属页。工程静态检查不能代替编辑器、Player 与实际游玩验证，相关边界见[Unity 工程页](unity-project.md)。

## 尚未实现

- 补齐 Map10／Map11 尚缺的官方资源与交互，并恢复其他地图的地形、区域、MiniMap、放置实体和角色资源绑定。
- 玩家移动、镜头、实体交互、剧情演出、机关、战斗、领奖及退出恢复的完整地图流程。
- 地图状态与正式本地存档的端到端集成。
- 各地图独立迁移及实际界面验收；共享 DTO、单张地图数据或导航规则不代表地图完成。
