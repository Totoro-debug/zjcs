# 13 泽之国

[总索引](../README.md)

状态：未完成

## 地图简介

泽之国地图 ID 为 13，含 106 个地区、2,690 个放置实体及 240×240 导航网格，后继地图为鲸背岛。新档入口、跨图状态与国度商店通用规则见[主线地图公共模块](../features/world-map.md#主线地图公共模块)。

## 原始官方实现

官方来源固定为 `871_156598`。地图导航、地区、实体、剧情、战斗、水路、地形、渲染及本地图 UI 的来源和转换记录见 [Map13 来源清单](../../assets/reference/official-map/map13-source-manifest.json) 与对应的 Map13 专项清单。

客户端剧情通过节点请求和响应与服务器协作；APK 不包含服务器执行逻辑。当前只接入下列有官方客户端证据或已确认单机行为的节点，其他服务端行为仍待确认。

## 完整还原部分

- 主线地图运行时已加载 Map13 导航、地区与实体数据，并接入本地地图进度、部分故事条件、地区标签及共享战斗载荷；正式地图主体、实体视图和整图流程尚未完成。
- 原地形、环境分区、区域光照、地图文字、剧情和战斗载荷已恢复到 Unity 资源目录。载荷可用不代表正式场景、角色或 UI 已接通。
- 部分原剧情节点、跟随者名单、商店进度、手动水路及终端规则已有本地适配。通用入口、探索记录和国度商店规则集中见[主线地图公共模块](../features/world-map.md#主线地图公共模块)。
- 公共战斗、物品提交窗口及伙伴显示分别复用[战斗执行](../features/battle-execution.md)、[物品与背包](../features/inventory.md)及共享角色资源；Map13 的正式业务入口仍有缺项。

## 自定义实现

- Map13 在 Map12 战斗碑 8101 胜利后写入本地解锁回执。此准入方式属于已确认的离线流程。
- `AddNpcFollowerNode` 按节点配置将 8402／8440／8441／8442 去重写入持久化跟随者列表；`RemovePlayerNpcFollowerAllNode` 清空列表。显示使用原角色模型资源并由地图宿主管理移动与生命周期。
- 已接入的剧情节点包括区域进入／离开、附近实体等待、`Talk1`–`Talk4` 黑板触发、职业条件、`Rank` 故事属性，以及 `WorldStopMoveNode` 的本地点击移动锁定。完整节点清单和运行数据见剧情来源清单。
- 三个 `HandInItemNode` 按原配置分别提交 2203×1、40502×5、43039×5，各限一次；按用户确认，只有本地原子扣除成功后才增加提交次数，取消、中断或失败不扣除。
- 水路和升降机按原路径／阶段顺序执行；用户确认路径受阻时不移动，载人保留原偏移，水位变化不发放额外奖励。五处终端的重置、道具回执和开路状态按原实体清单保存。
- Map13 商店等级阈值为 0／3500／8000／13000，使用商店 131–134 的官方固定商品和刷新配置；货币消耗计入商店经验的共用单机规则见[主线地图公共模块](../features/world-map.md#主线地图离线适配)。

## 尚未实现

- 完成正式地图入口、连续移动、区域探索、剧情、实体交互、战斗、奖励和出口流程；接通完整原生 HUD、迷你地图及商店界面。
- 服务端剧情执行逻辑未取得。当前未覆盖的服务端节点、首次开放条件、动态黑板键及职业／排行类条件不得按推测实现。
- 接通全部实体 prefab、骨骼和动画、完整跟随者外观、五组水路与终端正式 UI、NPC 交互、资源绑定及保存恢复。
- 以正式入口完成模拟输入、地图画面、剧情／战斗结算、失败恢复、冷读存档和 Windows Player 流程。

## 代码与资源入口

- 地图宿主与状态：[OfficialMainlineMapRuntime.NativeMap13.cs](../../unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.NativeMap13.cs)、[离线导航会话](../../unity/Assets/Game/Runtime/Offline/World/Map13Navigation/OfflineMap13NavigationSession.cs)、[水路会话](../../unity/Assets/Game/Runtime/Offline/World/OfflineMap13WaterSession.cs)。
- 公共规则：[主线地图公共模块](../features/world-map.md#主线地图公共模块)、[战斗执行](../features/battle-execution.md)。
- 来源：[Map13 来源清单](../../assets/reference/official-map/map13-source-manifest.json)、[剧情来源清单](../../assets/reference/official-map/map13-story-runtime-source-manifest.json)。
