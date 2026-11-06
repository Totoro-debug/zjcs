# 游记入口与日常副本

[总索引](../README.md)

状态：未完成

## 功能简介

游记入口汇集日常副本、地图、竞技及每日／每周挑战；各玩法的具体规则由所属页面维护。

日常副本与竞技已有本地规则、存档及战斗控制器接线。官方入口与详情界面仍有缺项，完整输入、结算和存档恢复流程尚未完成。

## 原始官方实现

日常副本由 `DailyDungeonEntryPanel`、`DailyDungeonDetailContent`、路线与结果面板及 `DailyDungeonBattleInfo`、`DailyDungeonMapGroupInfo` 配置驱动。难度、等级、区域进度、评级、房间、战斗和奖励规则使用各自原配置；Type 22 是按累计成功领奖次数发放奖励的成就页，不增加每日领奖次数。

竞技场入口使用 `ArenaEntry`、守卫、名次、匹配和奖励配置。原客户端将榜单、对手和战斗结果请求交给服务器，未提供完整的服务端结算规则。

日常副本规则见[运行来源清单](../../assets/reference/dungeons-runtime-source-manifest.json)，界面绑定见[副本界面来源清单](../../assets/reference/dungeon-ui/prefab-binding-source-manifest.json)。竞技场来源版本与项目范围见[官方来源总表](../../assets/reference/SOURCES.md)。

## 完整还原部分

### 日常副本

`HomeFeatureRuntime` 使用 `OfflineDailyDungeonModule` 与 `OfflineDailyDungeonSession` 加载目录、路线和存档；`DailyDungeonBattleRunner` 使用原战斗配置接入战斗控制器。已接入区域开放、难度与入场条件、房间推进、战斗检查点、评级、奖励预览及待领取结算。原 `RandomReward` 装备占位项按配置权重生成装备实例。

公共副本保存个人最佳评级，不创建虚构玩家榜单。路线、清理房间、战斗结果、领奖和背包更新写入本地存档事务，领奖回执防止重放。

### 竞技场

`OfflineArenaSession` 与 `ArenaBattleRunner` 使用原守卫、名次、匹配区间和战斗场景。地图进度达到原入口条件后开放；离线对手取自本地守卫模板，支持刷新、购买次数、挑战、战斗或直接胜利结算。

### 私有副本

Map12 的 121／50 机神山路线共用日常副本运行时，其奖励、次数隔离和结算边界见[山之国地图](../maps/map-12.md)。

## 自定义实现

- 每日免费领奖次数按官方日界刷新，未用次数保留；购买次数和价格档位每日刷新。单机仅在成功领取最终奖励时扣领奖池次数，进场、失败和未领奖均不扣。批量领取上限当前为 1；Type 22 只累计成功领奖次数。
- 评级使用官方地图评级行，不增加客户端未使用的职业贡献系数。`ServerRankUnlockDaysCondition` 按档案创建后经过的完整 24 小时计算。
- 奖励按官方条件、`Percent` 和候选权重结算；评级双倍概率为官方基础值 5%，未接入的本地加成按 0% 处理。服务器没有提供的行为不推断为官方规则。
- 竞技场用本地守卫和个人名次代替在线玩家及服务器赛季同步；胜利后使用原胜负奖励与对手名次结算。守卫附加外观按模板读取，性别在可用选项中均衡随机并保存。

## 尚未实现

- 游记入口、日常副本与竞技的完整官方界面还原。游记页仍缺部分原始图像素材；详情存在未绑定内容、默认外语文本和遮罩问题。
- 匹配倒计时、在线目标更新和服务器榜单不属于已迁移流程；完整的原生界面输入、战斗结算、关闭重开及冷启动存档恢复仍需接通并实际验证。
- 本功能尚未完成 Unity Play Mode 全流程验证和 Windows Player 发布。
- 福利额外领奖次数、奖励找回数据及批量领奖上限配置仍有缺项。

## 代码与资源入口

[OfflineDailyDungeonModule.cs](../../unity/Assets/Game/Runtime/Offline/World/OfflineDailyDungeonModule.cs)、[OfflineDailyDungeonSession.cs](../../unity/Assets/Game/Runtime/Offline/World/OfflineDailyDungeonSession.cs)、[OfflineArenaSession.cs](../../unity/Assets/Game/Runtime/Offline/World/OfflineArenaSession.cs)、[DailyDungeonBattleRunner.cs](../../unity/Assets/Game/Runtime/Presentation/Home/DailyDungeonBattleRunner.cs)。

[日常副本规则与资源](../../assets/reference/dungeons-runtime-source-manifest.json)、[竞技场来源版本与范围](../../assets/reference/SOURCES.md)、[副本界面绑定](../../assets/reference/dungeon-ui/prefab-binding-source-manifest.json)。
