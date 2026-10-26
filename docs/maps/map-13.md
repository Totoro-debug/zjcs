# 13 泽之国

[总索引](../README.md)

状态：未完成

## 地图简介

地图 ID：13。原目录：坎斯汀大陆/泽之国。场景类型：`map_world`；原区域配置106项，BGM配置13。

当前验收对象为 Unity / C#。

## 完整还原部分

尚无本图在 Unity 完整运行并验收的结论。共享地图 DTO、导航／进度规则以及保留的转换数据不能计作本图已还原。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

五组水路终端2634／3337／5334／7439／9338的服务器执行未包含在现有 APK、客户端反编译及 Common 程序集中，已核对 `MapPuzzleTerminalDataSwitchTheRoad` 参数和终端对话调用。用户明确允许：重置只恢复 `NeedResetLoEntityIds` 的原始状态与位置；自动解谜消耗原道具并开放 `ExtendRoadLoEntityIds` 通路；奖励仍须实际领取且不重复发放。

## 尚未实现

恢复泽之国原地区、海之宫剧情、五组水路、浅水通行、探索／任务、小地图和出口。灯光、水面、粒子与导演直接按原 Unity 内容恢复，不继承旧引擎渲染差异。

按原地图配置恢复场景、区域、导航、放置实体、原条件、交互、战斗、奖励和出口；原 prefab、骨骼、材质、粒子、动画及音频需在 Unity / Windows 逐项恢复并绑定。 原地形：`Map_13/MapGround_13`。 后继地图：`16`。

- 完成本图独立的本地资源绑定、玩家入口、正常游玩、战斗／交互结算、失败回滚、退出与冷读恢复。
- 在 Unity 编辑器实际运行，并验证 Windows 目标表现；

### 依据与代码定位

以下为官方来源、既有规则与回归参考；

、[prepare_map13_lighting.py](../../scripts/prepare_map13_lighting.py)、[lighting-13-source-manifest.json](../../assets/reference/official-map/lighting-13-source-manifest.json)、

、

、

[adventure.json](../../assets/official/adventure.json)、[13.json](../../assets/official/regions/13.json)、[prepare_map13_rendering.py](../../scripts/prepare_map13_rendering.py)、[materials-13-source-manifest.json](../../assets/reference/official-map/materials-13-source-manifest.json)

、[本地转换文件](../../assets/official/battle-presentation/effects/004256efd3dd43355f59fe75.json)、[战斗特效来源清单](../../assets/reference/official-map/battle-effects-source-manifest.json)
