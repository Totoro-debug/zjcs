# 14 龙之国

[总索引](../README.md)

状态：未完成

## 地图简介

地图 ID：14。原目录：坎斯汀大陆/龙之国。场景类型：`map_world`；原区域配置 111 项，BGM ID 14，后继地图 ID 15。

当前验收对象为 Unity / C#。

## 完整还原部分

尚无本图在 Unity 完整运行并验收的结论。共享地图 DTO、导航／进度规则以及保留的转换数据不能计作本图已还原。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

火炮终端共 7 个，实体 ID 为 `1512`、`2914`、`5015`、`6912`、`8011`、`8516`、`9413`，原类型为 `MapWorldPuzzleType.Cannon=4`。终端目标 `NeedRemoveLoEntityIds`、自动解谜阶段结果与实体类 `6502` 的 `CostItems` 来自固定版配置。固定版 Common／客户端代码仅有 Cannon 参数和自动／重置结果标记，没有发射、失败或重置处理器；对应定义见 [`MapPuzzleTerminalDataCannon.cs`](../../.tools/source-packages/official-code/common-source/Common/MapPuzzleTerminalDataCannon.cs)、[`MyStageResultAutoPuzzle.cs`](../../.tools/source-packages/official-code/client-source/MyStageResultAutoPuzzle.cs)、[`MyStageResultResetPuzzle.cs`](../../.tools/source-packages/official-code/client-source/MyStageResultResetPuzzle.cs) 与 [`adventure.json`](../../assets/official/adventure.json) 的 `Map/entity_extend_parameter`。

经用户确认的离线自定义适配使用原自动解谜费用（道具 `325 × 1`），按每个终端配置清除障碍；重复目标去重，不模拟炮弹发射、成功演出或失败状态，也不生成额外奖励。重置只清除该终端配置障碍的 `destroyed` 标记，不修改终端状态、玩家位置、其他机关、费用或奖励。规则位于 的 `cannon`、 的 `reset_cannon`。离线存档、地图移动与共用交互适配见[世界地图](../features/world-map.md)。

## 尚未实现

恢复111区、2961个放置实体、MiniMap_14迷雾及标记、8块战斗碑、3处遗迹、7个火炮终端和通往地图15的原出口。

按原地图配置恢复场景、区域、导航、放置实体、原条件、交互、战斗、奖励和出口；原 prefab、骨骼、材质、粒子、动画及音频需在 Unity / Windows 逐项恢复并绑定。

- 完成本图独立的本地资源绑定、玩家入口、正常游玩、战斗／交互结算、失败回滚、退出与冷读恢复。
- 在 Unity 编辑器实际运行，并验证 Windows 目标表现；

### 依据与代码定位

以下为官方来源、既有规则与回归参考；

[`map-layers/catalog.json`](../../assets/official/map-layers/catalog.json)、[`map-particles.json`](../../assets/official/map-particles.json)、[`regions/14.json`](../../assets/official/regions/14.json)、

、[`minimap-14-source-manifest.json`](../../assets/reference/official-map/minimap-14-source-manifest.json)、[`MapPuzzleTerminalDataCannon.cs`](../../.tools/source-packages/official-code/common-source/Common/MapPuzzleTerminalDataCannon.cs)

[`MyStageResultAutoPuzzle.cs`](../../.tools/source-packages/official-code/client-source/MyStageResultAutoPuzzle.cs)、[`MyStageResultResetPuzzle.cs`](../../.tools/source-packages/official-code/client-source/MyStageResultResetPuzzle.cs)、[`adventure.json`](../../assets/official/adventure.json)、
