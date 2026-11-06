# 圣兽试炼与双影幻境

[总索引](../README.md)

状态：未完成

## 功能简介

圣兽试炼（活动 ID 209、212、226）与双影幻境（217）共用本地战斗。玩法读取各自的活动、难度、关卡和奖励配置；日界为 08:00，期次时长为 335 小时。

## 原始官方实现

客户端配置圣兽难度与首杀奖励、双影阶段分数与宝箱权重，并使用各自的结果页。官方客户端代码与原始来源入口见[来源总表](../../assets/reference/SOURCES.md)。

## 完整还原部分

活动领域首领排行榜的本地化、字体、图片及皮肤绑定归入[共用窗口绑定生命周期](partners.md)，窗口释放自身令牌，关闭顺序不再恢复失效委托。原排行榜 prefab、范围切换和本地实际成绩数据源保持原入口。

绑定变动已在 Play Mode 验证空榜及活动 209／212／226 的实际战斗成绩，模拟点击范围切换、关闭和重开，核对成绩保存／冷读及窗口释放，并查看实际排行榜画面。原规则按钮在这些配置中为 0；未据此声称验证不存在的按钮。

- `OfficialDailyChallengeCatalog` 加载活动、关卡、属性修正和奖励数据；`OfflineDailyChallengeService` 保存活动期次、参与次数、挑战进度、成绩、领奖记录与战斗检查点。
- `DailyDungeonBattleRunner` 将圣兽 stage type 102、双影 stage type 103 接入公共战斗。结算按实际战斗报告记录伤害，保存有效检查点，并提交配置奖励。
- 游记入口支持四项玩法选择；圣兽难度、部分排行与邀请控件已迁入。结果操作区复用公共战报，规则见[战斗执行](battle-execution.md)。
- NPC 候选按官方条件和邀约次数筛选，选择随战斗检查点保存；通用助战规则见[NPC 助战](npc-assistance.md)。

## 自定义实现

- 用户确认期次从首次进入功能时起算；系统时间回拨不降低期次或逻辑日高水位。最后一小时按结算期处理。
- 圣兽本地排行只记录当前角色本期实际战斗成绩；单场失败后不跨场累计伤害。双影进度采用本地战斗实际伤害与护盾吸收，击杀数按官方每日上限计算。
- 服务端榜单、匹配与奖励写入未从客户端取得；离线行为仅按上述已确认范围执行。

## 尚未实现

- 圣兽活动页的正式入口、完整状态与排行接线，以及完整奖励结果页仍缺失。
- 双影首领和活动演出、完整结算奖励展示、关闭重开与冷启动恢复仍未接入。
- 助战候选的正式战斗模型和动画尚未完整绑定；不符合战斗动画要求的候选会禁用。
- 本功能尚未完成 Unity Play Mode 全流程验证和 Windows Player 发布。

## 代码与资源入口

[DailyDungeonBattleRunner.cs](../../unity/Assets/Game/Runtime/Presentation/Home/DailyDungeonBattleRunner.cs)、[OfflineDailyChallengeService.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineDailyChallengeService.cs)、[OfficialDailyChallengeCatalog.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialDailyChallengeCatalog.cs)。

活动页面与结果操作区资源入口见[原生界面清单](../../assets/reference/daily-challenges/native-ui-source-manifest.json)、[结算资源清单](../../assets/reference/daily-challenges/battle-result-actions-native-source-manifest.json)和[结算运行清单](../../assets/reference/daily-challenges/battle-result-actions-runtime-source-manifest.json)。
