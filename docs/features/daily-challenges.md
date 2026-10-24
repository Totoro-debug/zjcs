# 圣兽试炼与双影幻境

[总索引](../README.md)

状态：未完成

## 功能简介

圣兽试炼（活动 ID 209、212、226）与双影幻境（217）使用官方活动页、关卡、难度和奖励配置，并进入共用本地战斗。每日记录按本地 08:00 换日，活动期次以首次进入本功能的本地时间为起点；用户已确认双影进度使用本地实际战斗结果、宝箱每次领取生成并持久化本地种子。

游记页每日活动卡片打开四项离线挑战选择层：圣兽试炼 1/2/3（活动 ID 209、212、226）和双影幻境（217）。选择层复用官方 `DailyActivityEntry` 卡片与对应官方挑战页；各卡片标题为本地入口标识，官方资源没有为这四项提供旅行笔记专属卡片。

## 完整还原部分

当前版 `871_156598` 的圣兽活动、难度、阶位属性修正、双影活动、活动期次、阶段奖励、宝箱与奖励规则已转换为 `assets/official/daily-challenges/daily-challenges.json`，运行时副本位于 `unity/Assets/Game/Resources/DailyChallenges/daily-challenges.json`。`scripts/prepare_unity_daily_challenges.py` 负责配置转换。关卡数据来自固定 APK `854_156383`，由 `scripts/prepare_unity_daily_challenge_battles.py` 转换到 `battle-stages.json`。

官方资源清单 `assets/reference/daily-challenges/source-manifest.json` 记录当前版 12 个来源 bundle、836 项 CAB 依赖、15 个官方 prefab、82 项官方转换输出及 6 个 Resources prefab 映射（共 88 项输出）。资源由 `scripts/prepare_unity_daily_challenges_assets.py` 经 AssetRipper 1.3.14 转换；载荷在 `unity/Assets/Official/DailyChallenges/`，运行时 prefab 映射在 `unity/Assets/Game/Resources/DailyChallenges/`。圣兽／双影结果 prefab 现由 `OpenDailyChallengeResult` 按战斗结算打开。官方脚本通过项目 C# 适配实现必要行为，不以已导入 prefab 代表原脚本已运行。

`OfficialDailyChallengeCatalog` (`unity/Assets/Game/Runtime/Offline/Progression/OfficialDailyChallengeCatalog.cs`) 读取活动、奖励和难度表，并按 `ActivityDomainBossUtils.GetActivityDomainBossProps` 的配置顺序合并阶位／等级修正与难度属性。`OfflineDailyChallengeBattleCatalog` (`unity/Assets/Game/Runtime/Offline/World/OfflineDailyChallengeBattleCatalog.cs`) 读取四个官方 stage 与场景映射。

`OfflineDailyChallengeService` (`unity/Assets/Game/Runtime/Offline/Progression/OfflineDailyChallengeService.cs`) 保存角色槽位外的本地活动状态：首次进入锚点、335 小时期次、08:00 逻辑日及其高水位、期次最高索引、每日参与次数、难度解锁、挑战检查点、阶段领取、双影当日最高成绩及宝箱事务。用户确认系统时间回拨不重置进度；每日逻辑日及期次只前进不回退，旧存档从既有活动记录恢复高水位。参与和检查点在战斗启动时一起记录；战斗结算检查点可在重启后恢复。双影击杀按官方每日上限计数，挑战尝试本身不限次。

当前版 NPC 助战表由 `scripts/prepare_unity_daily_challenge_assistance.py` 从具备构造器证明的 Luban 缓存导出到 `unity/Assets/Game/Resources/DailyChallenges/npc-assistance-rules.json`，独立清单记录源表及输入／输出哈希。`OfficialCombatAssetCatalog.LoadNpcAssistance` 从该载荷加载 `NpcType=4` 候选、等级详情、职业、技能、邀约上限和条件；`FightOfficialNpcAssistanceCatalog.AssistConditionMet` 支持实际候选所用等级、好感、组合及剧情计数条件。游记挑战页的 `OpenDailyChallengeAssist` 使用官方 `TeamInvitePanel` 与 `TeamInvitePanelRobotItem` prefab 显示伙伴并保存每项挑战的选择；`DailyDungeonBattleRunner.ResolveDailyChallengeAssistSelection` 在开战前重核官方条件与次数，`OfflineNpcAssistanceProgress.CommitBattle` 在挑战入场时记录邀约次数。战斗检查点包含助战单位，结束时清除进行中的助战快照。普通日常副本也由 `HomeHudView.Dungeons.SelectDailyDungeonRoom` 打开同一伙伴选择界面，按房间分别存档并接入实际战斗；该入口和通用助战迁移状态见 [NPC 助战](npc-assistance.md)。

NPC 战斗属性和技能由当前版 `npc_level_detail`、`npc_profession_detail`、`skill` 计算；用户确认将 NPC 放在官方多格起点或玩家起点周围最近的可行走空格。`DailyDungeonBattleRunner.AddAssistNpcs` 避开检查点中已占格，按 Manhattan 距离和固定坐标顺序选位。`OfficialBattleModelCatalog` 已从现有官方绑定识别 `body_<NpcClassId>`，但命中的 12 名候选原动画数据都只有 `map/run_01`，不具备战斗所需的 `idle_01` 与 `run_01`。选择面板因此禁用这些未具备完整战斗动画的候选；当前官方 `FightEntityId` prefab 包还未转换为 Windows 原生战斗模型。已核对的 NPC 4202 包依赖闭包为 65 个序列化文件且闭包完整，源对象使用 Android 平台资源；该输入核对不代表转换或 Windows 渲染已完成。

`DailyDungeonBattleRunner` (`unity/Assets/Game/Runtime/Presentation/Home/DailyDungeonBattleRunner.cs`) 将圣兽 stage type 102 与双影 stage type 103 接到共用战斗执行器。挑战 checkpoint 从挑战战斗目录补充阵型到本次构建用的映射，不改写普通日常副本目录。难度选择按钮和 `ActivityDomainBossNumCell` 按官方 prefab 层级接入；胜利推进圣兽难度并发放配置的每日固定奖励。双影记录玩家及其召唤单位的实际 HP 伤害与护盾吸收，按首领最大 HP 折算最高万分比进度；阶段领奖一次结清所有已达成未领档位。`FightBattleHitExecutor` 与 `FightBattleStatistics` 独立记录护盾吸收，不改变实际扣血统计。战斗结束后使用官方 `GameEndPanelActivityDomainBoss`／`GameEndPanelActivityFunnyBoss` 显示胜负和实际伤害，返回按钮回到当前挑战页。

圣兽难度奖励及双影阶段奖励按当前配置为单物品固定数量；双影宝箱按官方权重表抽取。宝箱领取先将随机种子、抽取结果和角色物品事务 ID 写入本地，再提交幂等物品事务，进程崩溃后可继续完成同一笔领取。

Unity Editor／C# 编译：本轮未取得有效结果。现有 Editor 由其他验收流程使用；本轮尝试以 batchmode 启动时，Unity 检测到工程已由另一实例打开并在导入／编译前退出。静态工程检查本轮运行超过可用等待时间后被中止；此前一次检查报告 `Assets/Official/Gathering/Prefabs` 与 `Assets/Game/Resources/Gathering` 缺少稳定 `.meta`，与本功能无关且未修改。挑战 Play Mode 鼠标／键盘、真实画面及存档恢复尚未验证。

## 自定义实现

用户确认期次以首次进入功能的本地时间起算；该时间由 `EnterFeature(DateTime.Now)` 首次持久化。每日逻辑日采用官方 08:00 配置。官方活动 `Duration=335` 小时，最后 1 小时结算。用户确认系统时间回拨不得降低已到最高期次或逻辑日；系统时间向前跳转仍会按本地时间推进期次。

圣兽读取 `MaxDailyChallengeCount=1` 与 `MaxDailyChallengeAwardCount=1`。双影的 `DailyMaxKillCount=3` 限定每日击杀，不当作挑战次数。用户确认挑战成功进入战斗才计参与，失败也保留回执，同一逻辑日不重复挑战圣兽；双影每次开始进入本地战斗均记录参与。服务器写入和奖励执行逻辑未在客户端来源中取得，以上离线行为不声称为官方服务端实现。

双影进度采用本地战斗结果的当日最高实际团队伤害，阶段分数按官方客户端 `max(DailyMaxTeamDamage, DailyMaxTeamDamagePercent * bossMaxHp / 10000)` 规则折算。由于离线战斗为单角色加本地召唤物，伤害按其 `MasterId` 归属角色；未取得服务端个人／队伍伤害写入实现。

## 尚未实现

- 将 85 个可配置 NPC 对应的官方 `fightnpc_*` 战斗模型、动画及依赖转为 Windows 可用原生资产并绑定到 `OfficialBattleModelCatalog`／战斗场景视图；本轮只核验 NPC 4202 一个包的依赖闭包，尚未完成模型转换与画面验证。已发现的 12 个官方 `body_*` 绑定仅有地图跑步动画，不能替代战斗动画。入口选择、官方条件／次数复核、检查点单位、最近可行走格布阵及助战计数已实现；未获得战斗模型的 NPC 会禁用选择。
- 对有多个 NPC 助战的四个挑战地图进行 Play Mode 实测，确认 NPC 出场、单位渲染、移动／技能、胜负结算和检查点恢复；当前 Unity Editor 被其他工作占用，无法在不干扰该流程的情况下完成此项。
- 在既有 Unity Editor 完成导入与 C# 编译检查；该 Editor 当前由另一工作区验收流程占用，本轮未对其操作。
- 在可用 Editor 中以真实鼠标／键盘验证四项入口、难度选择、助战筛选、领奖、08:00 换日、期次交界、界面渲染与存档恢复；本轮未执行。

### 依据与代码定位

官方客户端：`.tools/source-packages/official-code/client-source/ActivityDomainBossPanelDetailContent.cs`（难度格及阶位选择）、`ActivityDomainBossPanelBattleContent.cs`（挑战次数、奖励显示及领奖）、`GameEndPanelActivityDomainBoss.cs`（结算奖励消息接入）、`ActivityDomainBossUIUtils.cs`（参与与逻辑日）、`AnniversaryFunnyBossContent.cs`（双影挑战／宝箱入口）、`AnniversaryFunnyBossStepAwardView.cs`（当日最高伤害与阶段分数）、`AnniversaryFunnyBossUtils.cs`（可开宝箱数）、`FightActivityFunnyBossMainPanel.cs`（实际伤害加护盾吸收）。官方共享奖励解析位于 `.tools/source-packages/official-code/common-source/Common/RewardRuleInfoParser.cs`。客户端调用不能揭示的服务端规则在本地按已确认单机范围实现。

配置：当前热更新 `871_156598`，活动 ID 209、212、226、217；地图 94、96、98、95；关卡 21153、21154、22600、72100；双影宝箱 6525、阶段组 1010。运行配置与逐项输入／输出哈希见 [daily-challenges/source-manifest.json](../../assets/reference/daily-challenges/source-manifest.json)，助战规则表见 [npc-assistance-source-manifest.json](../../assets/reference/daily-challenges/npc-assistance-source-manifest.json)；战斗关卡来源见 [battle-stages-source-manifest.json](../../assets/reference/daily-challenges/battle-stages-source-manifest.json)。
