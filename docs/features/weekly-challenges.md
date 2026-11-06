# 虚空裂隙与混沌侵袭

[总索引](../README.md)

状态：局部完成。本地关卡战斗与检查点已接入；活动页、周期奖励和地图实体流程仍未完成。

## 功能简介

游记每周活动包含虚空裂隙与混沌侵袭；魔王是混沌战斗阶段之一，不是独立的无限挑战。具体离线开放条件见[离线开放条件](offline-access.md)。

## 原始官方实现

两类活动使用各自的期次、开放与结算时限、阶段顺序、奖励和关卡配置，不沿用日常挑战的逻辑日。混沌奖励、保底、魔王战斗和家园商店按官方客户端与配置处理；服务端活动实体调度未从客户端取得。

## 完整还原部分

- `OfflineWeeklyChallengeBattleCatalog` 加载虚空与混沌关卡；`WeeklyChallengeBattleRunner` 接入公共战斗执行器。
- `OfflineWeeklyChallengeSession` 按角色存档保存周期、阶段次数与胜场、魔王伤害和战斗检查点；重新进入可恢复已保存的战斗。
- 魔王 4800 使用原怪物与战场配置；战斗结果从公共战报读取，完成后保存本期胜场和伤害。
- 公共战斗执行和检查点规则见[战斗执行](battle-execution.md)。

## 自定义实现

- 用户确认活动周期改为每周日 08:00 开放，保留原阶段顺序及时长；混沌累计改为本人当期实际魔王伤害，原累计门槛按十分之一使用，奖励不变。
- 魔王 4800 按玩家本体及其召唤物、幻兽的实际生命伤害累计；周期首次入场时固定等级与阶位，查看或领取旧期奖励不补造历史。
- 魔王等级按原 NPC 表的世界阶段最高等级取值：1 至 8 阶为 10、40、70、170、250、400、400、999；更高阶回退到 999。
- 挑战按单人模式运行，隐藏真人组队和联网排名；官方未配置此活动的 NPC 邀请规则，因此不添加 NPC 助战。
- 服务端实体调度缺失时，按用户确认的可用地图点位本地生成、每 5 秒维护并保存目标批次与截止时间。活动比例与延迟沿原配置处理，此行为是单机适配。
- 官方保底计数改为角色级长期保存，不按周清空；虚空层间概率沿原规则，并由本地持久化计时推进。
- 用户确认混沌商店在本期结算开始时开放 24 小时，创建、首次访问和离场操作写入本地存档。家园商人按当前门的绘制顺序显示在门前，保留原变换与行为。

## 尚未实现

- 游记正式活动页、原生结算面板和完整奖励领取流程。
- 虚空与混沌的活动实体调度、方向指引、跨次保底与自动推进的地图接线。
- 家园商店和商人的完整运行接入。
- 本功能尚未完成 Unity Play Mode 全流程验证和 Windows Player 发布。

## 代码与资源入口

[OfflineWeeklyChallengeBattleCatalog.cs](../../unity/Assets/Game/Runtime/Presentation/World/OfflineWeeklyChallengeBattleCatalog.cs)、[OfflineWeeklyChallengeSession.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineWeeklyChallengeSession.cs)、[WeeklyChallengeBattleRunner.cs](../../unity/Assets/Game/Runtime/Presentation/Home/WeeklyChallengeBattleRunner.cs)。

关卡来源见[周常战斗清单](../../assets/reference/weekly-challenges/battle-stages-source-manifest.json)；官方客户端来源见[来源总表](../../assets/reference/SOURCES.md)。
