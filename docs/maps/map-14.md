# 14 龙之国

[总索引](../README.md)

状态：未完成（道路属性门子模块已完成）

## 地图简介

龙之国地图 ID 为 14，含 111 个地区、2,961 个放置实体及 280×280 导航网格；默认入口 101 位于 (247,115)。主线地图生命周期、跨图状态和存档规则见[主线地图公共模块](../features/world-map.md#主线地图公共模块)。

## 原始官方实现

地图基础来源为 `871_156598`；道路属性门及其 UI／音效使用选定 `884_156692`，对应共享表与放置身份已逐项核对。地图配置、导航、实体、剧情、战斗、地形、环境、UI 和机关数据的来源与转换记录见 [Map14 来源清单](../../assets/reference/official-map/map14-source-manifest.json) 及 Map14 专项清单。官方地图的开放条件、奖励和剧情执行以客户端实现与已确认的离线方案为界，不推测缺失服务器逻辑。

## 完整还原部分

- 主线宿主已加载原生地形、环境、地区标签、实体目录和本地玩家状态；入口、区域揭示及实体进度使用公共地图状态。
- Map14 的原生地图资源、实体 prefab 目录、剧情／战斗数据和机关目录已恢复到 Unity 工程。载荷恢复不代表完整实体视图、地图界面或玩法已接通。
- Map14 的物品提交窗口和部分原生 UI 资源已恢复；正式故事入口、角色移动、背包交易和地图存档流程仍有缺项。
- 五处道路属性门已接入原门体、原窗口和伙伴选择，完成规则、单机差异及当前验证范围集中维护于 [NPC邀请](../features/npc-assistance.md)。
- 地区公开／私有状态、玩家冷读和实体阶段展示复用[主线地图公共模块](../features/world-map.md#主线地图公共模块)。战斗数据复用[战斗执行](../features/battle-execution.md)。

## 自定义实现

- Map14 的 `HelpHuiAfterBattle` 节点提交 `hand_in_item[4]` 中的 2208×1，最多一次。按用户确认，本地事务成功后才增加提交次数；失败回滚，相同凭证不重复扣除。确认、取消和中断分别沿原节点出口处理。
- 七处火炮终端仅按配置移除指定障碍，不模拟炮弹、成功演出或额外奖励。五组走格子终端的辅助操作仅解开指定锁；重置只恢复本组踏板和锁，并保留已领取奖励。
- Map15 准备配置已接入本地进度：Gold 段解锁后 14 日结束准备期，任务组 7101 按配置收集物品。服务端未提供起始时间和状态转换逻辑；以首次达到 Gold 的时间作为计时起点是已确认的本地决策，不代表官方服务器规则。
- Windows 序列化与故事演出使用本地兼容适配；该适配保持原数据语义，不改变玩法规则。

## 尚未实现

- 接通 111 个地区、2,961 个实体、雕像、怪物、战斗碑、遗迹、火炮、走格子和 Map15 出口的正式地图入口、原条件、交互、战斗及奖励。
- 恢复并接通完整实体 prefab、角色／实体动画、原生小地图迷雾与标记、HUD、对话、音频及机关演出。
- 完成剧情节点、商店、背包交易、地图状态和存档的正式流程，包括失败回滚、退出与冷读恢复。
- 完成其余正式地图入口的模拟鼠标交互、地图画面、受影响流程与 Windows Player 验证；道路子模块的交付和验证范围见 [NPC邀请](../features/npc-assistance.md)。

## 代码与资源入口

- 地图定义及主线宿主：[OfficialMainlineMapProfile.cs](../../unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapProfile.cs)、[OfficialMainlineMapRuntime.cs](../../unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.cs)。
- 通用地图／战斗规则：[主线地图公共模块](../features/world-map.md#主线地图公共模块)、[战斗执行](../features/battle-execution.md)。
- 来源：[Map14 来源清单](../../assets/reference/official-map/map14-source-manifest.json)、[实体目录来源](../../assets/reference/official-map/map14-entity-catalog-source-manifest.json)、[剧情来源](../../assets/reference/official-map/map14-story-runtime-source-manifest.json)、[机关来源](../../assets/reference/official-map/map14-puzzles-source-manifest.json)。
