# 游记入口与日常副本

[总索引](../README.md)

状态：未完成

## 功能简介

游记提供日常副本、阶梯／羁绊、地图、竞技、素材秘境及每日／每周挑战；具体玩法分别维护。

日常副本与竞技的本地规则、存档、界面及战斗流程已接入；Unity Editor 编译、Play Mode 输入与画面验收尚未完成。

## 完整还原部分

官方资源已按固定版 `854_156383` 转换为本地运行目录：`scripts/prepare_dungeons_runtime.py` 导出日常副本地图、房间、难度条件、奖励规则及战斗场景；`scripts/prepare_battle_arena.py --runtime --verify-apk` 导出竞技守卫（含原性别分组附加皮肤）、名次模板、匹配策略和竞技战斗场景。资源由 `OfflineDailyDungeonSession`、`OfflineArenaSession` 读取并校验。

`HomeHudView.Dungeons` 使用原版 `TravelNotesPanel` prefab 保留游记卡片布局、插画与层级，并接入日常副本列表／详情／房间图、离开确认及 `ArenaPanel`。目录只列入口区域进度为 `10000` 的公开副本并排除剧情难度 50；难度页签按原 tab 条件显示，默认选择最高永久解锁难度，锁页显示等级、战力／阶位压制及前难度评级条件，左右翻页按公开副本／可见难度组合顺序推进。日常副本按官方配置检查永久难度解锁、独立入场条件、每日次数和额外次数消耗，保存当前路线、清理房间、评级及战斗检查点；奖励按官方 `RewardRuleInfoParser` 的条件、`Percent`、候选权重及数量结算；`RandomReward` 装备占位项通过固定版 `random_reward` 的候选装备及权重生成实际装备实例，装备等级和祝福等级使用对应地图组、难度的官方范围。主动离开仅结算清理房间配置掉落且不返次数，战斗失败保留当前路线并清除本场战斗检查点以允许重打；已清理房间不能重复挑战。区域公开通过已保存的主线区域进度判定。竞技场按 `ArenaEntry` Type 58 的地图预览／开放门槛显示，读取原匹配区间和守卫权重，支持免费／付费刷新、购买挑战次数、本地挑战名次和胜负奖励；胜利后名次移至对手名次。挑战前按官方守卫战力与 `ArenaCombatRatingDirectWinPercent` 提供正常战斗／直接胜利选择，直接胜利消耗次数并走同一胜场结算；守卫使用模板附加外观，性别按本地均衡随机选择并随挑战保存。`DailyDungeonBattleRunner`、`ArenaBattleRunner` 使用官方战斗配置和场景接入战斗控制器，并保存战斗检查点。

副本目录通过 `HomeFeatureRuntime.MapRegionProgress` 读取统一离线主线区域进度接口；进度达到 `10000` 时视为公开。该接口由主线区域目标推进，本次不扩展地图类 11–23 的运行时接线。

## 自定义实现

以下为已确认的单机处理要求及实际采用的行为。

服务端进入事务由本地保存替代；次数在实际进入时扣除，主动离开不退还。旧自创幻兽派遣与真人赛事／镜像入口停用，不增加统一体力或角色等级硬门槛。

成功完成至少携带一位本地邀请伙伴的战斗时，固定增加100友情币，不设置每周或累计领取上限；这替代原由服务器下发的好友助战奖励。

沿用已确认的单机世界阶段方案，服务器开服日期条件不计等待；个人等级、战力和个人评级仍按原配置检查，区域官方主线目标完成后公开。难度仅按当前格式中的有效进度和原条件开放；旧格式存档不导入。

竞技入口在离线版采用原 `ArenaEntry` 的 Type 58 地图预览／开放门槛，不使用 `ArenaAreaEntry` 中依赖组队挑战的旁路开放条件。服务端对手资料和赛季同步改由固定版守卫模板及玩家本地名次替代；挑战结算用原每日次数及胜负奖励配置，本地胜场和名次只属于当前玩家，胜利后采用对手名次。守卫数值按用户确认翻倍，附加外观按模板数据加载，性别在官方可用选项中均衡随机并写入活动挑战存档。

## 尚未实现

- Unity Editor 导入／编译及 Play Mode 全流程尚未验证：实际鼠标／键盘输入、界面渲染、战斗胜负与失败重打、奖励结算、路线中断后恢复、竞技排名变化及相关存档恢复均待运行核验。当前仍持锁的 Editor 是旧 Safe Mode 实例；其旧日志记录 Commerce LINQ 导入与数值转换编译错误，工作区源码已更新，但尚未由可重新编译的 Editor 确认，副本 Play Mode 尚未启动。

### 依据与代码定位

以下为官方来源、既有规则与回归参考；

[TravelNotesPanel](../../.tools/source-packages/official-code/client-source/TravelNotesPanel.cs)、[PlayerDailyDungeonDataExt.CanUnlockDailyDungeonDifficulty／CanEntryDailyDungeonDifficulty](../../.tools/source-packages/official-code/common-source/Common/PlayerDailyDungeonDataExt.cs)、[Player.IsUnlockDailyDungeonByInfo](../../.tools/source-packages/official-code/common-source/Common/Player.cs)、[DailyDungeonDifficultyContent.RefreshTabItem](../../.tools/source-packages/official-code/client-source/DailyDungeonDifficultyContent.cs)、[DailyDungeonLockContent.Refresh／OnClickUnlockBtn](../../.tools/source-packages/official-code/client-source/DailyDungeonLockContent.cs)

[ArenaEntry.IsShowPreview／IsUnLock](../../.tools/source-packages/official-code/client-source/ArenaEntry.cs)、[ArenaAreaEntry.IsShowPreview／IsUnLock](../../.tools/source-packages/official-code/client-source/ArenaAreaEntry.cs)、[PlayerUtils.IsFunctionLimit](../../.tools/source-packages/official-code/common-source/Common/PlayerUtils.cs)、[WorldMapProgressCondition](../../.tools/source-packages/official-code/common-source/Common/WorldMapProgressCondition.cs)、[rules.json](../../assets/official/rules.json)

[ArenaGuardTemplateInfo](../../.tools/source-packages/official-code/common-source/Common/ArenaGuardTemplateInfo.cs)、[BattleFormulaHandler.CalcCombatRatingSkill](../../.tools/source-packages/official-code/common-source/Common/BattleFormulaHandler.cs)、[ArenaFightDialog.CheckDirectWin](../../.tools/source-packages/official-code/client-source/ArenaFightDialog.cs)、[ArenaRankTemplateInfo](../../.tools/source-packages/official-code/common-source/Common/ArenaRankTemplateInfo.cs)、[ArenaMatchStrategyInfo](../../.tools/source-packages/official-code/common-source/Common/ArenaMatchStrategyInfo.cs)、[ArenaMatchRuleInfo](../../.tools/source-packages/official-code/common-source/Common/ArenaMatchRuleInfo.cs)、[prepare_battle_arena.py](../../scripts/prepare_battle_arena.py)

[日常副本运行资源来源清单](../../assets/reference/dungeons-runtime-source-manifest.json)、[竞技场运行资源来源清单](../../assets/reference/arena-source-manifest.json)

主要接入代码：[HomeHudView.Dungeons](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.Dungeons.cs)、[OfflineDailyDungeonSession](../../unity/Assets/Game/Runtime/Offline/World/OfflineDailyDungeonSession.cs)、[DailyDungeonBattleRunner](../../unity/Assets/Game/Runtime/Presentation/Home/DailyDungeonBattleRunner.cs)、[OfflineArenaSession](../../unity/Assets/Game/Runtime/Offline/World/OfflineArenaSession.cs)、[ArenaBattleRunner](../../unity/Assets/Game/Runtime/Presentation/Home/ArenaBattleRunner.cs)
