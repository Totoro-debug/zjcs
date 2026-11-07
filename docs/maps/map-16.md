# 16 鲸背岛

[总索引](../README.md)

状态：未完成

## 地图简介

鲸背岛是带独立准备流程的可交互大地图。本页维护鲸背岛专属数据、规则、离线差异和缺项；导航、白雾、小地图及存档公共行为见[主线地图公共模块](../features/world-map.md#主线地图公共模块)。

## 原始官方实现

地图 ID 为 16，场景类型 `map_world`，BGM 配置 16，后继地图 14。原地面为 `Map_16/MapGround_16`，小地图为 `Map_16/MiniMap/MiniMap_16`（配置 ID 4）。地图含 43 个地区、140×120 导航网格和 1021 个放置实体；原 `CanExplore=false`，支持私有地区，不开放公共地区或组队开关。

官方放置、导航、互动、剧情、战斗、独立地图和原生资源来源分别见[地图定义](../../assets/reference/official-map/map16-definition-source-manifest.json)、[实体目录](../../assets/reference/official-map/map16-entity-catalog-source-manifest.json)、[交互配置](../../assets/reference/official-map/map16-interactions-source-manifest.json)、[剧情运行资源](../../assets/reference/official-map/map16-story-runtime-source-manifest.json)、[战斗运行配置](../../assets/reference/official-map/map16-battle-runtime-source-manifest.json)、[独立地图配置](../../assets/reference/official-map/map16-alone-configuration-source-manifest.json)、[地面](../../assets/reference/official-map/map16-current-ground-source-manifest.json)及[小地图](../../assets/reference/official-map/map16-current-minimap-root-source-manifest.json)。所选版本以 `official-source.json` 为准；旧版输入保留各自来源清单，不混作当前版本。

## 完整还原部分

共享地图档案已登记 Map16 的 43 个地区、入口放置 ID 101、导航、白雾、互动及版本来源，但 `IsRuntimeEntryReady=false`，鲸背岛尚未开放正式宿主入口。地图实体目录、交互配置、故事图、战斗数据、原地面和小地图资源已各自转换或接入共享资源模块；这些数据与资源不代表地图流程已连接。

运行时代码入口为 [`OfficialMainlineMapProfile`](../../unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapProfile.cs)、[`OfficialMainlineMapRuntime`](../../unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.cs) 与 [`OfflineMainlineProgressSession`](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs)。完整入口仍缺角色／HUD 状态、白雾与地区状态联动、实体视觉、交互、故事演出、战斗及小地图数据响应。

两份官方 NPC 消失演出 prefab 的 17 份材质已将外部 shader 引用从原生序列化资产类型修正为 `ShaderImporter` 类型，保留原 GUID、材质参数、shader 源、纹理与网格；转换器和逐项输出哈希同步更新，见[演出资源来源](../../assets/reference/official-map/map16-native-story-playables-source-manifest.json)。该 shader 的 260 组原程序／两个 pass 已纳入[共享构建校验](../features/battle-execution.md)，素材绑定检查不代表鲸背岛故事流程已接通。

独立 Play Mode 与最终 Windows Player 已检查全部 17 份材质的 shader 引用与支持状态，并查看两份 prefab 的粒子抽帧。该夹具单独激活粒子，不绑定故事中的 NPC 来源，画面仅证明材质能够产生输出；原人物消失效果、完整故事激活顺序和声音仍未验证。

## 自定义实现

用户确认的地区时间条件以首次达到位阶 4 的本地时间为基准；位阶 4 的 14 天及其他地区各自的原始偏移继续生效，位阶高于 4 时免除该时间门槛。绳索准备剧情、等级和其他原条件仍需满足；不应用服务器合服加速。

独立地图准备需至少 300 秒。角色恰为位阶 4 时还需距首次达到位阶 4 满 14 天，按两者较晚者开放；高于位阶 4 时仍需完成 300 秒。原绳索、位阶、等级及其他条件保留。

用户确认的六条手动剧情触发为：首次启程 `LookForWhaleIsland`；准备完成 `DiscoverWhaleIsland`；进入地图 `WhaleIslandArrive`；NPC 10801 交付 43039×5 绳索并播放 `SubmitRopes`；NPC 8443 播放 `WhaleIslandFinishLoop`；离岛播放 `WhaleIslandLeave`。`WhaleIslandFinish` 仍按原自动条件启动，次数遵循原 `MaxCount`。

独立地图奖励按原规则 770／771 发放钻石 200、金币 80000；奖励、剧情、随机状态及一次性领取记录应在同一存档事务提交，失败回滚且冷读不重复领取。踩格机关的辅助菜单按原配置扣道具 325×1，只解开 `LockLoEntityIds`；重置仅恢复对应踏板和辅助锁，保留奖励状态及领取记录，不增加重置入口。上述完整事务和界面流程尚未接入。

## 尚未实现

鲸背岛尚未注册为可进入的正式地图。入口准备、绳索交付、剧情、战斗、雕像／资源刷新、五组踩格机关、领奖、完整大小地图和存档恢复仍需接线；完整交互与 Windows Player 验证及发布尚未完成。当前验证范围限于共享数据和资源模块，不代表鲸背岛可玩。
