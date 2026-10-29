# 成长成就与积分奖励

[总索引](../README.md)

状态：未完成

## 功能简介

功能菜单成就及各玩法馈赠使用独立原成就组，积分按钮显示原档位奖励。

## 原始官方实现

官方成就配置定义成长目标、积分档位、完成条件、奖励和循环锚点；原成就面板按类别显示进度及领取状态。

### 待迁移的规则与表现

原 `AchievementGroupPanel`、组内容与临近目标行接入；固定档和循环档按原 `AchievementRawInfoParser`、`CycleTemplateInfo`、`AchievementInfo` 展开，循环模板不成为可领0／0奖励。

人物、职业、伙伴、宝箱和实际累计驱动成长成就；赛季积分读取对应 `SeasonId` 真实记录，已结束赛季分数冻结。

特殊条件按原类计算：`AchievementTaskMopUpBreak` 分素材秘境类型累计真实击破；`AchievementTaskCostItem` 区分物品与 `SourceOperate`，探寻命运、装备强化、技能槽普通／快速升级在实际扣款处登记来源；`AchievementTaskNpcAdventureNode` 按原地图组、地区、节点顺序读取当前已通关前缀；`AchievementTaskNpcAdventureTravelFinishedCount` 读取已领取旅行完成奖励的永久回执；`AchievementTaskDailyDungeonGetRewardCount` 只累计公共日常副本最终奖励成功结算，剧情私有难度不计；`AchievementTaskDailyDungeonPersonalGrade` 读取指定副本、难度的历史最佳评级，按原版显示 0／1 进度。Type 28 `AchievementTaskLegionFightJoinBattleCount` 读取本地 NPC 公会战实际进攻次数；Type 54 `AchievementTaskLegionBossBattleCount` 读取首领挑战次数；Type 55 `AchievementTaskLegionBossProgressTop` 将当前首领层换算为已击败数量 `max(0, layer - 1)`，例如第 10 层为 9。Type 69 读取竞技场挑战次数。日／周刷新不清除这些永久统计。

原 `ScoreRewardUI`／`ScoreRewardDialog` 主界面一次领一档，详情领该组全部须实现档；与成就页共享组／索引回执，兼容旧原ID标记，不重复发奖。

## 完整还原部分

尚无完整 Unity 流程的完成结论；已取得源码和转换素材仅作迁移依据。

## 自定义实现

服务端统计和领取由本地真实事件、赛季记录及原子存档事务替代，未记录的历史分数为零，不根据角色年龄补造。Type 28 在无真人公会战的离线版本按本地 NPC 公会战进攻次数累计；Type 55 的层数按用户确认解释为当前层减一，即已彻底击败的首领数。

长期玩法永久累计、活动另建计数的规则经用户确认。各项进度只读取当前格式中明确保存的计数和回执，不从缺失字段或库存推导历史。

## 尚未实现

- 独立成就组、馈赠积分档、真实玩法事件、永久与活动计数及领取去重。

- 完成 Unity 实际入口、交互／表现、存档与失败恢复验证，并发布对应 Windows 可执行版本。
- Type24真人组队竞猜不提供替代；星神养成没有可确认的完整接入，不生成虚构伙伴或好感进度。

## 代码与资源入口
