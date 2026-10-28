# NPC邀请、站位与战前准备

[总索引](../README.md)

状态：未完成

## 功能简介

本页的“NPC 助战”专指可邀请加入我方队伍的伙伴，不包含敌方怪物。当前规则版本 `871_156598` 中，伙伴候选按 `NpcType=4`、`CanDevelop=true`、存在战斗实体和等级详情筛选，共 85 名；职业导师和商人不属于此集合。日常副本、秘境、道路属性门槛、探索首领、节庆首领、联合行动和羁绊冒险等玩法使用伙伴助战时，按各玩法资格进入我方战斗。地图战斗可保存邀请、站位和准备状态；开始战斗时才扣除本次助战次数。

以上为 Unity / C# 的目标范围，尚未完整接入和验收。

## 完整还原部分

已接入圣兽试炼、双影幻境及普通日常副本的候选选择、存档、战斗单位与次数事务；其余玩法入口和所有实际 Play Mode 验收仍未完成。

- `assets/official/rules.json` 与规则来源清单提供 `npc`、`npc_level_detail`、`npc_profession_detail`、`player_robot_invite`、`skill` 数据。`FightOfficialNpcAssistanceCatalog.Load` 校验规则哈希，按 `NpcType=4`、`CanDevelop`、战斗实体 `FightEntityId` 和等级详情筛伙伴候选，并读取遇见限制、助战条件和玩法次数规则。
- `FightOfficialNpcAssistanceCatalog.CreateUnit` 按官方等级详情与职业配置计算 NPC 属性、主动技能、被动技能、冷却和状态。被动属性按官方 `NpcFightData.CalcPropsWithSkill` 的基础属性计算与 `BattleFormula.CalcFinalProps` 顺序合成。
- Home runtime 通过 `OfficialCombatAssetCatalog.LoadNpcAssistance` 加载目录，并用 `OfflineNpcAssistanceProgress` 将选择、分玩法邀请次数和活动战斗快照保存至当前角色槽位。次数只由 `CommitBattle` 提交；`-1` 次数上限按不限制处理，正数上限允许达到配置值。
- `HomeHudView.DailyChallenges.OpenDailyChallengeAssist` 与 `OpenDailyDungeonAssist` 使用官方 `TeamInvitePanel`／`TeamInvitePanelRobotItem` 资源显示候选并保存选择。日常副本与每日挑战共用 `RobotInvitePlayType.All`（10），选择按活动／房间分别保存；战斗开始时重核助战条件、单 NPC 次数和人数上限，`DailyDungeonBattleRunner.BuildCheckpoint` 调用 `CreateUnit` 并为伙伴寻找未占用的可行走格。普通副本 checkpoint 与助战快照一起恢复，启动失败回滚次数，战斗结束清除活动快照。
- 官方通用 Team 邀请窗口与 RobotItem 已转换到 `unity/Assets/Game/Resources/Dungeons/UI/`，Windows 加载入口为上述 picker；该资源转换本身不代表其余玩法已接入。

山之国战斗碑的原 `TeamInviteNpc` 使用独立 Team 候选链：`GetNpcs(MapExploreBoss)` 排除探索首领，再按 `IsFightAssist` 和原遇见／助战条件筛选，当前配置共有95名候选，包括具有正式战斗数据的9名导师和1名商人。新增 `TeamInviteNpcClassIds`／`CanInviteTeamNpc`／`CreateTeamInviteUnit` 供该原组队链使用；上述既有伙伴 API 的85名范围保留。95名资格、10名额外角色实际属性／技能装配及伙伴结果一致性已通过独立规则检查；窗口／地图队伍、站位、模型、次数和实际战斗尚未接线，范围与原代码哈希见[山之国](../maps/map-12.md)。

战斗碑组队调用 `TeamPanelHelper` 的普通 Player 邀请页，再切换 NPC 标签；该入口没有初始化 `TeamInviteRobotPanel` 的 `RobotInvitePlayType`，因此这里不能套用日常助战的 All=10 配置。原 Team 页及邀请依赖的10份 prefab／645节点／1105个脚本载荷已完整解码并核对引用，尚未转换／绑定为可用的 Windows 队伍窗口；本地队伍、站位与真实战斗仍待接通，见[山之国](../maps/map-12.md)。

山之国原队伍邀请新增直接迁移的 `PlayerPlayTeamAction`，保留原 Team 动作与 NoneParam，并复用原状态互斥检查。独立编译与规则夹具验证邀请前的聊天／舞会／匹配限制、异步退出取消、退出成功及重新检查；原 NPC 邀请窗口和本地队伍事务尚未绑定，不代表可从正式地图邀请。

## 自定义实现

以下为已确认的单机处理要求，尚未代表对应玩法流程已经接入或验收。

伙伴等级／阶位按玩法处理，不能统一套用一种公式。通用战斗邀请 `TeamInviteNpc` 创建的 `NpcTargetData` 初始为 `NpcLevel=0`、`NpcSubRank=None`，随后 `PlayerInviteInfoManager` 调用 `OperPlayerQueryTeamInvitePlayersInfoByTargetData` 获取 `TeamInvitePlayerInfoData.Level/SubRank`；现有客户端源码没有该服务端赋值公式。用户已确认通用战斗伙伴等级沿用当前玩家等级、阶位沿用玩家存档中同步的 `SubRank`；`DailyDungeonBattleRunner.AddAssistNpcs` 按这两个值创建本地伙伴单位。NPC 冒险页 `NpcAdventurePanelMainContent` 明确同步主角等级与阶位；秘境故事伙伴默认跟随主角，并允许 `TeamInviteNPCNode` 指定等级／阶位；`PlayerBriefInfoUtils` 的 `CreatePlanesRank` 逻辑仅用于数据无效或跨赛季时校正，不是通用邀请的等级生成规则。

探索首领的敌方目标候选和击败后的地图进度属于探索首领玩法，不是 NPC 助战伙伴候选。单机该玩法按上述配置顺序选择首个地图匹配且 `CheckStageId` 已完成的目标，并在本地保存时限、开始、放弃和胜利结果；此处只记录玩法调用伙伴助战时的关联流程。

节庆首领击破、血量同步、层推进和领奖原由服务器保存；单机根据原层配置维护这些状态，并将待领奖励合并为本地逐层领取操作。活动 200、216 标记 `UseSeverLevelAndRank=true`，服务器的玩家等级／阶位公式无法在客户端复用；经用户确认，本地使用活动 `DefaultLevel + LevelOffset` 计算等级（首层为 `25 - 14 = 11`），并沿用关卡 `SubRank`。重复层偏移仍按官方 `ActivityFestivalBossUtils.GetLevelOffset` 使用该层击破数。此等级基准是确认过的单机规则，不代表官方服务器算法。活动开启时间在本地首次打开时记录，`UnlockTimeOffset` 按官方的小时单位计算；线上时间基准来自活动服务器数据。助战说明中的 NPC 获取提示依赖服务器 `NavigateManager.Navigate`；单机保留原按钮并提示该导航目标不可用，不虚构 NPC 获取路线。

## 尚未实现

- 素材秘境（2）、道路属性门槛（1）、探索首领（All=10）、节庆首领（5）、联合行动（4）各自的实际选择入口、条件筛选、官方属性排序／门槛、站位、战斗加入、快照提交及结算接线；不能用日常副本的 All 规则替代这些玩法。
- 羁绊冒险（6）的 NPC 战斗邀请入口和战斗接线；NPC 旅行是派遣系统，不等同战斗助战。
- NPC 战斗模型／动画加载绑定。目前候选列表仅放行 `OfficialBattleModelCatalog.HasNpcBattleAnimations` 已确认包含 `idle_01`、`run_01` 的角色；85 个伙伴对应的官方 `fightnpc_*` 包已按当前规则实体 ID 从固定 APK／已校验 CDN 缓存提取至 `.tools/source-packages/extracted/`，但还未转换并绑定为 Windows 原生资产，不能把原始包或地图移动动画当作可运行战斗模型。现有 `scripts/prepare_battle_animation.py` 因其共享依赖 `scripts/prepare_map_regions.py` 在工作区及 Git 历史中均缺失而无法启动；其他地图与剧情素材转换工具也引用该缺失模块。
- 将用户确认的等级／阶位规则扩展到其余通用助战玩法；当前日常战斗按玩家等级与存档 `SubRank` 创建伙伴。
- 在 Unity Play Mode 验证候选列表、真实鼠标选择、人数限制、战斗表现、次数保存恢复及失败回滚；静态检查和 prefab 可加载不计入该验收。

### 待迁移的规则与表现

以下要求中每日挑战及日常副本已经接入；其余玩法仍是迁移目标，不构成当前实现结论。

邀请资格、NPC 遭遇／职业限制、最大人数、累计次数、单 NPC 次数和原冷却来自 `PlayerRobotInvite`、`npc` 与助战属性配置。选人继续使用原 NPC 邀请窗口；原地图道路窗口、探索首领组队窗口、节庆首领面板、节庆助战说明窗及其条目沿固定 APK prefab 和布局接入。节庆邀请列表将 `ActivityBuff` 作为首个排序项，并按 `activity_festival_boss_npc` 标出对应助战。日常使用 `RobotInvitePlayType.All`（10），秘境为 2，道路属性门槛为 `PropGate`（1），探索首领使用 `All`（10），节庆首领为 5，联合行动为 4，羁绊冒险为 6。

地图属性门槛按 `StageConPropStandard` 累加已选 NPC 的官方战斗属性；条件满足后，NPC 作为实际战斗实体加入队伍。地图战斗运行时记录助战次数快照，站位准备阶段不扣次，点击开始后保留扣次记录；日常副本、挑战与羁绊战斗复用同一 NPC 属性、技能、阶位和战前站位规则。

探索首领按地图 `StageResultExploreBossComponent.Info.StageIds` 顺序查找首个满足 `CanShowMapClass` 且须实现 `CheckStageId` 的配置，使用原挑战信件和组队窗口。挑战时限来自 `stage_map_explore_boss.Duration`；开始、胜利结算和放弃均保存到本地地图进度。

节庆首领读取 `activity_festival_boss`、`activity_festival_boss_layer` 与 `activity_festival_boss_npc`，分别保存每层基础剩余血量、当日最高单次伤害、击破数和领奖数，并按原层序推进；面板血量显示基础血量扣除当日最高单次伤害后的结果，次日刷新将该伤害计入基础血量并清零当日记录。击破判定沿用 `PlayerActivityFestivalBossDataExt.UpdateActivityFestivalBossLayerData` 的本次伤害规则；重复层在每日刷新后恢复。每日战斗奖励按活动每日一次发放，重复层等级偏移按击破次数套用 `RepeatLevelOffset`。关卡奖项、怪物掉落和普通通关标记不会重复计入活动奖励。

### 依据与代码定位

官方实现：`.tools/source-packages/official-code/common-source/Common/NpcFightData.cs` (`CalcPropsWithSkill`)、`.tools/source-packages/official-code/common-source/Common/NpcLevelDetailInfoParser.cs` (`Get`)、`.tools/source-packages/official-code/client-source/NpcUtils.cs` (`IsNpcAssistLimit`)、`.tools/source-packages/official-code/client-source/TeamInviteNpc.cs` (`GetAssistNpcActorTargetDatas`)、`.tools/source-packages/official-code/client-source/TeamInvitePanel.cs` (NPC 邀请页)、`.tools/source-packages/official-code/common-source/PlayerInviteInfoManager.cs` (助战候选资料请求)、`.tools/source-packages/official-code/client-source/NpcAdventurePanelMainContent.cs` (伙伴冒险等级同步)、`.tools/source-packages/official-code/client-source/TeamInvitePanelPrivateMap.cs` 与 `TeamInviteNPCNode.cs` (剧情伙伴等级)、`.tools/source-packages/official-code/common-source/PlayerBriefInfoUtils.cs` (跨赛季数据校正)。客户端代码来自旧版固定 APK `854_156383`；当前伙伴候选按新版 `871_156598` 的 `NpcType=4`、`CanDevelop`、战斗实体和等级详情识别。

配置与素材：[NPC 助战来源清单](../../assets/reference/npc-assistance/source-manifest.json)、[规则表来源清单](../../assets/reference/official-city/rules-manifest.json)、`assets/official/npc-assistance-ui.json`。通用邀请素材来自固定 APK `defaultpackage_assets_prefabs_ui_team.bundle`；固定包的提取记录以来源清单为准。

Unity 当前实现：规则目录 `unity/Assets/Game/Runtime/Official/Combat/FightOfficialNpcAssistanceCatalog.cs`、次数与战斗快照 `unity/Assets/Game/Runtime/Offline/World/OfflineNpcAssistanceProgress.cs`、官方 Team picker `unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.DailyChallenges.cs`、战斗接线 `unity/Assets/Game/Runtime/Presentation/Home/DailyDungeonBattleRunner.cs`。普通日常副本入口位于 `HomeHudView.Dungeons.SelectDailyDungeonRoom`；其余玩法入口未接线。TeamInvitePanel 的 bundle 输入、45 项官方依赖、6 个运行时 prefab 与 52 项共享绑定记录见 [team-ui-source-manifest.json](../../assets/reference/npc-assistance/team-ui-source-manifest.json)。
