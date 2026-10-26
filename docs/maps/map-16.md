# 16 鲸背岛

[总索引](../README.md)

状态：未完成

## 地图简介

地图 ID：16。原目录：坎斯汀大陆/鲸背岛。场景类型：`map_world`；原区域配置43项，BGM配置16。入口为坎斯汀 NPC `10801` 的寻岛／提交绳索剧情，按原准备条件开放后从独立地图卡片登岛；覆盖 HUD、地图、小地图、怪物关卡、区域、任务、机关、剧情、音画表现和本地存档。共用规则见[世界地图](../features/world-map.md)。

当前验收对象为 Unity / C#。

## 完整还原部分

尚无本图在 Unity 完整运行并验收的结论。共享地图 DTO、导航／进度规则以及保留的转换数据不能计作本图已还原。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

原客户端 `AloneWorldMapEntryItemContentCommonBase.FinshAward` 仅提供 `OperPlayerAloneMapFinshAward(16)` 请求，包内没有服务端领取实现。经用户明确允许，单机在本地按原奖励规则 `770／771` 发放钻石 200、金币 80000，并持久化一次性领取记录；奖励、剧情、随机状态和领取记录同事务写盘，失败回滚，冷加载后防止重复领取。领奖表现按原 `SourceOperate.AloneMapAward=222`、`def_award_handler[222].AwardStyle=Popup` 调用项目已转换的 `AwardPanel`，保留两种货币图标、数量及延迟关闭交互；原生。

原 `StageResultAutoPuzzle／ResetPuzzle`、`MapPuzzleTerminalDataStepGrid／LockBase` 只保留客户端／Common 标记与配置，包内没有服务端执行器。经用户明确允许，辅助从原菜单扣 `325×1`，仅解开 `LockLoEntityIds`；原未解开菜单提供的重置恢复该组踏板和辅助锁，不清除奖励实体状态或已领取记录。锁完成后按原配置生成独立奖励并切换终端状态，不额外增加重置入口；检查 覆盖全部五组原菜单、实际领奖、写盘回滚、冷加载和防重复领奖。

## 尚未实现

恢复鲸背岛原入口、提交43039×5绳索、准备／登岛／离岛剧情、43区及38条任务、45场战斗和五组踩格机关；领取奖励770／771和退出恢复需完成实际本地事务验证。

按原地图配置恢复场景、区域、导航、放置实体、原条件、交互、战斗、奖励和出口；原 prefab、骨骼、材质、粒子、动画及音频需在 Unity / Windows 逐项恢复并绑定。 原地形：`Map_16/MapGround_16`。 后继地图：`14`。

- 完成本图独立的本地资源绑定、玩家入口、正常游玩、战斗／交互结算、失败回滚、退出与冷读恢复。
- 在 Unity 编辑器实际运行，并验证 Windows 目标表现；

### 依据与代码定位

以下为官方来源、既有规则与回归参考；

[`prepare_map13_minimap_ui.py --map-id 16`](../../scripts/prepare_map13_minimap_ui.py)、[`minimap-16-source-manifest.json`](../../assets/reference/official-map/minimap-16-source-manifest.json)、

、[`prepare_map16_dialogs.py`](../../scripts/prepare_map16_dialogs.py)

、

、[`prepare_map16_rendering.py`](../../scripts/prepare_map16_rendering.py)、[`prepare_map16_world_ui.py`](../../scripts/prepare_map16_world_ui.py)、

、[`prepare_map16_alone_ui.py`](../../scripts/prepare_map16_alone_ui.py)

、[adventure.json](../../assets/official/adventure.json)、[16.json](../../assets/official/regions/16.json)

、
