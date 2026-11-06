# 15 通天塔入口

[总索引](../README.md)

状态：未完成

## 地图简介

通天塔入口及其关联楼层共用主线地图宿主。本页记录本图入口、已接入模块和离线决策；跨图数据、导航、存档与交互规则见[主线地图公共模块](../features/mainline-map-modules.md)。

## 原始官方实现

地图 ID 为 15，场景类型 `map_world`，BGM 配置 15，单个区域，后继地图为 `42001`。入口地形为 `Map_Tower15_SnowFloor1/MapGround_Tower15_SnowFloor1`。原入口条件为服务端位阶 5，并按该位阶解锁时间加 14 天计算开放时间；在线规则还会应用合服加速。

通天塔相关地图组数据、条件、地形与交互分别见[地图组来源](../../assets/reference/official-map/map15-group-data-source-manifest.json)、[条件来源](../../assets/reference/official-map/map15-configurations-source-manifest.json)、[地形来源](../../assets/reference/official-map/map15-current-ground-source-manifest.json)和[视图来源](../../assets/reference/official-map/map15-world-view-source-manifest.json)。

## 完整还原部分

`OfficialMainlineMapProfile` 将 Map15 注册到公共宿主，入口档案为条件 115、区域 0、放置 ID 101；运行时读取 25×30 导航网格和 26 个放置实体。第一层原生地形、区域标签、导航、移动、交互状态和位置存档使用公共地图模块。原根未挂白雾控制器，原配置没有独立小地图 prefab；档案按此保留，不补造雾层或小地图。

`OfficialMap15GroupDataCatalog` 提供通天塔地图组数据，但不会据此自动生成后续楼层场景。入口可用性不代表楼层切换、机关、战斗、奖励或原 HUD 已完整接入。

## 自定义实现

离线入口需角色达到位阶 5 后手动开始 14 天倒计时；位阶提升、显示入口或读取状态不会自动开始计时。起点和开放状态写入本地进度，读取时不应用服务端合服加速。对应规则由 `OfflineMainlineProgressSession.StartMap15Preparation`、`GetMap15PreparationState` 和 `RefreshMap15Preparation` 维护。

已确认的机关适配保留原菜单、费用和目标：5 处火炮终端仅移除 `NeedRemoveLoEntityIds`；2 处特殊锁与 5 处普通锁终端仅解开 `LockLoEntityIds`，费用为道具 325×1。重置只恢复原配置允许重置的目标，不回滚其他状态或已领取奖励，也不增加菜单选项。客户端没有服务端执行器；本地实现不宣称为原服务端规则。

六处遗迹沿原 20 层配置与奖励推进：进入后挑战当前未探索层，战败保留当前层，胜利后按原阶段进入下一层或领奖；最后一层领取后标记完成。遗迹 17–19 的原配置地图 ID 与放置位置不一致，按实际放置保留配置，不改写原表。

手动火炮按原速度、10 格上限、可飞地形和当前镜面方向推进；命中执行目标阶段，遇障碍停止。事务保存发射结果，存档恢复不重复命中。原炮弹、炮口、受击、震屏和音效表现尚未接入。获准使用 FastNoiseLite 的范围限于炮弹／炮口 11 个及元素连线 39 个原噪声发射器；仅噪声采样采用本地算法，粒子轨迹与 Unity 原噪声不同。镜头震屏沿用原 `Mathf.PerlinNoise`。

## 尚未实现

后续楼层场景、原 HUD、地图切换、机关和火炮演出、战斗与奖励的正式流程仍未完成。当前只接通入口及共享地图运行时的数据路径；完整楼层交互、存档恢复与 Windows Player 尚未完成或发布。验证范围限于已接入模块，不代表通天塔完整流程可玩。
