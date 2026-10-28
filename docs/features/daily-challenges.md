# 圣兽试炼与双影幻境

[总索引](../README.md)

状态：未完成

## 功能简介

圣兽试炼（活动 ID 209、212、226）与双影幻境（217）使用官方活动页、关卡、难度和奖励配置，并进入共用本地战斗。每日记录按本地 08:00 换日，活动期次以首次进入本功能的本地时间为起点；用户已确认双影进度使用本地实际战斗结果、宝箱每次领取生成并持久化本地种子。

游记页每日活动卡片打开四项离线挑战选择层：圣兽试炼 1/2/3（活动 ID 209、212、226）和双影幻境（217）。选择层复用官方 `DailyActivityEntry` 卡片与对应官方挑战页；各卡片标题为本地入口标识，官方资源没有为这四项提供旅行笔记专属卡片。

## 原始官方实现

圣兽试炼与双影幻境分别读取原活动期次、08:00 日界、难度、关卡／属性修正、首杀／阶段奖励和结果界面。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

`prepare_challenge_ui.py` 经 `official_apk.Apk` 从固定 APK 与同版本已校验 CDN 读取 SecretArea、JointStrike、VoidFissure、DemonInvasion 四个原 UI 包，转换67个 prefab； 保存原层级、锚点、尺寸、缩放、布局组、裁切、状态、组件字段、文本及脚本哈希。来源完整保存在 `challenge-modes/source-manifest.json`，含312项实际使用资源的输入／输出 SHA256、bundle 版本／URL／哈希和21项圣兽技能图标与原 ItemInfo.RawIcon 的对应关系。62张独立 PNG 保存在 `assets/official/challenge-ui/sprites/`；其余图片复用字节一致的既有官方转换图，不覆盖或重绘原图。

圣兽七种 StyleSetting 直接读取原 SecretAreaSettings：首杀列表与详情图片、规则预览图片、原模型缩放／偏移；怪物技能直接使用原 phase 表 ClientShowSkills 和实际同名 Sprite。图标收集目录可能含 lCommon_NoAtlas，按精确图标名在对应官方图标族中唯一定位，缺失或歧义即报错。首杀 NPC 的 FigureModelUI 复用已转换的官方伙伴模型，角色使用原角色网格。原语言资源按 DateTimeFormat 显示中国时区的击破日期。

圣兽／双影主页面按原 TopBarHandler 的空货币列表清除主城余额栏，保持原首杀按钮位置。原馈赠页使用 AchievementPanel／Base／Item 的五行槽位、原状态、文本、奖励及滚动；专用规则页使用 SecretAreaRuleDialog／JointStrikeRuleDialog 与 RuleTipNumer 原编号，不再以通用规则窗口替代。轮换预告及魔物详情仍待接线，不显示 prefab 示例值。

用户授权的单机调整：首杀记录本人当期首次击破；原隐藏的主页面邀请按钮显示后进入原 NPC 选择页；首杀说明同步为本人／伙伴规则，移除仅真人及同位面限制。主规则中的组队说明同步改为邀请伙伴，首杀说明同步为本人首杀；好友额外金币说明随已排除的在线社交功能隐藏。文字调整记录在运行控制器中，不改原表、prefab 或来源数据。四玩法主入口与其余交互仍在迁移；提取及转换完成不表示四套玩法已接入完毕。

`prepare_challenge_results.py` 严格读取固定版本两个原战斗结果 prefab、成员结算行及 PopupWindow，共24个 MonoBehaviour；连同开场 AnimationClip、原 UIMesh／MeshRenderer／Mesh／Material／Shader 共36项输入，原对象来源、bundle 凭据及输入 SHA256 嵌入 ，所有节点引用均核对既有 `map-ui.json`。保留原 BlurMask、开场曲线、伤害／波数／历史峰值文本、本人／NPC 原模型槽及离开按钮；战斗数值与紧凑数值沿原 CultureNumberArg 使用不同格式，相关格式化代码哈希另存 `format_code_sha256`。

底部原121顶点／200三角形网格按原 RectTransform 的三维旋转及三个缩放分量投影，材质 CommonJieSuanDown／UI/EffectDistort 沿已迁移原 GLES 着色程序、纹理坐标、混合与时间参数。唯一新增 PNG 为原 Mask_065_tex 的 Texture2D 解码，位于 `assets/official/challenge-results/`，输入／输出哈希和原始包凭据在 `assets/reference/challenge-results/source-manifest.json`，纳入显式加载与独立素材包；未自绘背景或替换原布局。

成员伤害服务端写入未取得，统计口径待用户意。动态圣兽 MonsterIcon 已定位到原对应 prefab，仍待适配。当前隔离结果页隐藏未接通内容，不用占位数值或其他模型代替，正式战斗区域接线另行完成。

结果页按 GameEndPanelBoss.RefreshBoss 读取原 StageInfo.Monsters[0] 和 MonsterType.Normal=1 选择原 BossSlot／NormalSlot，不使用奖励计算的 GetMainMonster 替代。3703、3704、3705、3707、3708、3709 的 MonsterIcon 与已转换 MonsterUI 逐一核对 SkeletonDataAsset PPtr、皮肤、初始动画及网格哈希，复用同一原骨骼，保留各自0.42／0.44缩放、原枢轴和 SkeletonGraphic 参数。UIUtils.KeepScale=11 在实例化时清零位置与旋转，避免误沿编辑器预览位移。原 prefab／RectTransform／SkeletonGraphic 输入凭据及 UIUtils 代码哈希嵌入结果绑定的 icon_sources／icon_code_sha256；无新增图片或素材包变更。

3706 为原 TempRTUI，已取得相机正交尺寸3.5、640×512 RT、原目标位置／缩放和带粒子的 MonsterModel_3706_RT；该独立渲染链待迁移，保留 pending_icons 元数据，不用其他骨骼或自制图片代替。

`scripts/prepare_unity_daily_challenges.py` 从当前热更新 `871_156598` 提取圣兽活动／难度／属性修正、双影活动、08:00 活动时间、阶段奖励、宝箱及奖励规则表；输入 bundle 与构造器哈希、完整消费行数和输出哈希见 `daily-challenges/source-manifest.json`。`scripts/prepare_unity_daily_challenge_assistance.py` 从已核验当前 Luban 表缓存导出 `conditions`、`npc`、`npc_level_detail`、`npc_profession_detail`、`player_robot_invite` 与 `skill`，运行载荷及输入来源见 `daily-challenges/npc-assistance-source-manifest.json`。`scripts/prepare_unity_daily_challenges_assets.py` 使用 AssetRipper 1.3.14 对已核验 CAB 闭包转换官方活动页、挑战页、战斗 HUD、结果 prefab 和依赖；清单登记 15 个 prefab、82 项官方转换输出、6 个 Resources prefab 映射（共 88 项输出）、33 项载荷目录 GUID、193 个省略的官方 MonoBehaviour 脚本及 836 项 CAB 依赖。`unity/Assets/Official/DailyChallenges/` 为官方转换载荷，运行时资源映射列于该清单。

## 完整还原部分

当前版 `871_156598` 的圣兽活动、难度、阶位属性修正、双影活动、活动期次、阶段奖励、宝箱与奖励规则已转换为 `assets/official/daily-challenges/daily-challenges.json`，运行时副本位于 `unity/Assets/Game/Resources/DailyChallenges/daily-challenges.json`。`scripts/prepare_unity_daily_challenges.py` 负责配置转换。关卡数据来自固定 APK `854_156383`，由 `scripts/prepare_unity_daily_challenge_battles.py` 转换到 `battle-stages.json`。

官方资源清单 `assets/reference/daily-challenges/source-manifest.json` 记录当前版 12 个来源 bundle、836 项 CAB 依赖、15 个官方 prefab、82 项官方转换输出及 6 个 Resources prefab 映射（共 88 项输出）。资源由 `scripts/prepare_unity_daily_challenges_assets.py` 经 AssetRipper 1.3.14 转换；载荷在 `unity/Assets/Official/DailyChallenges/`，运行时 prefab 映射在 `unity/Assets/Game/Resources/DailyChallenges/`。圣兽／双影结果 prefab 现由 `OpenDailyChallengeResult` 按战斗结算打开。官方脚本通过项目 C# 适配实现必要行为，不以已导入 prefab 代表原脚本已运行。

`OfficialDailyChallengeCatalog` (`unity/Assets/Game/Runtime/Offline/Progression/OfficialDailyChallengeCatalog.cs`) 读取活动、奖励和难度表，并按 `ActivityDomainBossUtils.GetActivityDomainBossProps` 的配置顺序合并阶位／等级修正与难度属性。`OfflineDailyChallengeBattleCatalog` (`unity/Assets/Game/Runtime/Offline/World/OfflineDailyChallengeBattleCatalog.cs`) 读取四个官方 stage 与场景映射。

`OfflineDailyChallengeService` (`unity/Assets/Game/Runtime/Offline/Progression/OfflineDailyChallengeService.cs`) 保存角色槽位外的本地活动状态：首次进入锚点、335 小时期次、08:00 逻辑日及其高水位、期次最高索引、每日参与次数、难度解锁、挑战检查点、阶段领取、双影当日最高成绩及宝箱事务。用户确认系统时间回拨不重置进度；每日逻辑日及期次只前进不回退，旧存档从既有活动记录恢复高水位。参与和检查点在战斗启动时一起记录；战斗结算检查点可在重启后恢复。双影击杀按官方每日上限计数，挑战尝试本身不限次。

当前版 NPC 助战表由 `scripts/prepare_unity_daily_challenge_assistance.py` 从具备构造器证明的 Luban 缓存导出到 `unity/Assets/Game/Resources/DailyChallenges/npc-assistance-rules.json`，独立清单记录源表及输入／输出哈希。`OfficialCombatAssetCatalog.LoadNpcAssistance` 从该载荷加载 `NpcType=4` 候选、等级详情、职业、技能、邀约上限和条件；`FightOfficialNpcAssistanceCatalog.AssistConditionMet` 支持实际候选所用等级、好感、组合及剧情计数条件。游记挑战页的 `OpenDailyChallengeAssist` 使用官方 `TeamInvitePanel` 与 `TeamInvitePanelRobotItem` prefab 显示伙伴并保存每项挑战的选择；`DailyDungeonBattleRunner.ResolveDailyChallengeAssistSelection` 在开战前重核官方条件与次数，`OfflineNpcAssistanceProgress.CommitBattle` 在挑战入场时记录邀约次数。战斗检查点包含助战单位，结束时清除进行中的助战快照。普通日常副本也由 `HomeHudView.Dungeons.SelectDailyDungeonRoom` 打开同一伙伴选择界面，按房间分别存档并接入实际战斗；该入口和通用助战迁移状态见 [NPC 助战](npc-assistance.md)。

NPC 战斗属性和技能由当前版 `npc_level_detail`、`npc_profession_detail`、`skill` 计算；用户确认将 NPC 放在官方多格起点或玩家起点周围最近的可行走空格。`DailyDungeonBattleRunner.AddAssistNpcs` 避开检查点中已占格，按 Manhattan 距离和固定坐标顺序选位。`OfficialBattleModelCatalog` 已从现有官方绑定识别 `body_<NpcClassId>`，但命中的 12 名候选原动画数据都只有 `map/run_01`，不具备战斗所需的 `idle_01` 与 `run_01`。选择面板因此禁用这些未具备完整战斗动画的候选；当前官方 `FightEntityId` prefab 包还未转换为 Windows 原生战斗模型。已核对的 NPC 4202 包依赖闭包为 65 个序列化文件且闭包完整，源对象使用 Android 平台资源；该输入核对不代表转换或 Windows 渲染已完成。

`DailyDungeonBattleRunner` (`unity/Assets/Game/Runtime/Presentation/Home/DailyDungeonBattleRunner.cs`) 将圣兽 stage type 102 与双影 stage type 103 接到共用战斗执行器。挑战 checkpoint 从挑战战斗目录补充阵型到构建用的映射，不改写普通日常副本目录。难度选择按钮和 `ActivityDomainBossNumCell` 按官方 prefab 层级接入；胜利推进圣兽难度并发放配置的每日固定奖励。双影记录玩家及其召唤单位的实际 HP 伤害与护盾吸收，按首领最大 HP 折算最高万分比进度；阶段领奖一次结清所有已达成未领档位。`FightBattleHitExecutor` 与 `FightBattleStatistics` 独立记录护盾吸收，不改变实际扣血统计。战斗结束后使用官方 `GameEndPanelActivityDomainBoss`／`GameEndPanelActivityFunnyBoss` 显示胜负和实际伤害，返回按钮回到当前挑战页。

圣兽难度奖励及双影阶段奖励按当前配置为单物品固定数量；双影宝箱按官方权重表抽取。宝箱领取先将随机种子、抽取结果和角色物品事务 ID 写入本地，再提交幂等物品事务，进程崩溃后可继续完成同一笔领取。

Unity Editor／C# 编译：未取得有效结果。现有 Editor 由其他验收流程使用；尝试以 batchmode 启动时，Unity 检测到工程已由另一实例打开并在导入／编译前退出。静态工程检查运行超过可用等待时间后被中止；此前一次检查报告 `Assets/Official/Gathering/Prefabs` 与 `Assets/Game/Resources/Gathering` 缺少稳定 `.meta`，与本功能无关且未修改。挑战 Play Mode 鼠标／键盘、真实画面及存档恢复尚未验证。

## 自定义实现

用户确认期次以首次进入功能的本地时间起算；该时间由 `EnterFeature(DateTime.Now)` 首次持久化。每日逻辑日采用官方 08:00 配置。官方活动 `Duration=335` 小时，最后 1 小时结算。用户确认系统时间回拨不得降低已到最高期次或逻辑日；系统时间向前跳转仍会按本地时间推进期次。

圣兽读取 `MaxDailyChallengeCount=1` 与 `MaxDailyChallengeAwardCount=1`。双影的 `DailyMaxKillCount=3` 限定每日击杀，不当作挑战次数。用户确认挑战成功进入战斗才计参与，失败也保留回执，同一逻辑日不重复挑战圣兽；双影每次开始进入本地战斗均记录参与。服务器写入和奖励执行逻辑未在客户端来源中取得，以上离线行为不声称为官方服务端实现。

双影进度采用本地战斗结果的当日最高实际团队伤害，阶段分数按官方客户端 `max(DailyMaxTeamDamage, DailyMaxTeamDamagePercent * bossMaxHp / 10000)` 规则折算。由于离线战斗为单角色加本地召唤物，伤害按其 `MasterId` 归属角色；未取得服务端个人／队伍伤害写入实现。

### 已确认的本地接线与边界

`OfficialDailyChallengeCatalog` 与 `OfflineDailyChallengeBattleCatalog` 读取当前规则及固定 APK 关卡数据；`OfflineDailyChallengeService` 保存首次进入时间、335 小时期次与最高期次、08:00 逻辑日高水位、参与回执、挑战检查点、档位领奖和双影进度／宝箱事务。`DailyDungeonBattleRunner` 将圣兽／双影 stage 接入共用战斗，按本地 HP 结算及护盾吸收记录双影成绩，领奖写入角色物品事务；宝箱使用用户确认的逐次本地随机种子并先存抽取结果以支持崩溃恢复。`prepare_unity_daily_challenges_assets.py --runtime-only` 校验来源 prefab SHA256 并映射圣兽／双影结果页至 `Resources`；胜负、伤害及已提交的胜利奖励明细接入结果页。当前版 NPC 候选规则表由独立清单记录；游记挑战入口支持逐项选择，按官方条件与邀请次数筛选，布阵使用官方起点及用户确认的邻近可行走空格策略，战斗检查点保存助战单位。现有模型目录命中 12 名助战候选的 `body_*` 绑定，但原动画数据只有 `map/run_01`，缺少战斗所需 `idle_01`／`run_01`，选择界面会禁用它们。官方 NPC 4202 的 65 个序列化文件 CAB 闭包已核验；85 个候选 `fightnpc_*` 战斗模型、动画及依赖尚未完成 Windows 原生转换和运行绑定。官方客户端 C# 行为由本地 C# 适配实现，未复制被省略脚本。Editor／Play Mode 验证状态见[圣兽与双影功能页](daily-challenges.md)。

## 尚未实现

- 将 85 个可配置 NPC 对应的官方 `fightnpc_*` 战斗模型、动画及依赖转为 Windows 可用原生资产并绑定到 `OfficialBattleModelCatalog`／战斗场景视图；只核验 NPC 4202 一个包的依赖闭包，尚未完成模型转换与画面验证。已发现的 12 个官方 `body_*` 绑定仅有地图跑步动画，不能替代战斗动画。入口选择、官方条件／次数复核、检查点单位、最近可行走格布阵及助战计数已实现；未获得战斗模型的 NPC 会禁用选择。
- 对有多个 NPC 助战的四个挑战地图进行 Play Mode 实测，确认 NPC 出场、单位渲染、移动／技能、胜负结算和检查点恢复；当前 Unity Editor 被其他工作占用，无法在不干扰该流程的情况下完成此项。
- 在既有 Unity Editor 完成导入与 C# 编译检查；该 Editor 当前由另一工作区验收流程占用，未对其操作。
- 在可用 Editor 中以真实鼠标／键盘验证四项入口、难度选择、助战筛选、领奖、08:00 换日、期次交界、界面渲染与存档恢复；未执行。

## 代码与资源入口

官方客户端：`.tools/source-packages/official-code/client-source/ActivityDomainBossPanelDetailContent.cs`（难度格及阶位选择）、`ActivityDomainBossPanelBattleContent.cs`（挑战次数、奖励显示及领奖）、`GameEndPanelActivityDomainBoss.cs`（结算奖励消息接入）、`ActivityDomainBossUIUtils.cs`（参与与逻辑日）、`AnniversaryFunnyBossContent.cs`（双影挑战／宝箱入口）、`AnniversaryFunnyBossStepAwardView.cs`（当日最高伤害与阶段分数）、`AnniversaryFunnyBossUtils.cs`（可开宝箱数）、`FightActivityFunnyBossMainPanel.cs`（实际伤害加护盾吸收）。官方共享奖励解析位于 `.tools/source-packages/official-code/common-source/Common/RewardRuleInfoParser.cs`。客户端调用不能揭示的服务端规则在本地按已确认单机范围实现。

配置：当前热更新 `871_156598`，活动 ID 209、212、226、217；地图 94、96、98、95；关卡 21153、21154、22600、72100；双影宝箱 6525、阶段组 1010。运行配置与逐项输入／输出哈希见 [daily-challenges/source-manifest.json](../../assets/reference/daily-challenges/source-manifest.json)，助战规则表见 [npc-assistance-source-manifest.json](../../assets/reference/daily-challenges/npc-assistance-source-manifest.json)；战斗关卡来源见 [battle-stages-source-manifest.json](../../assets/reference/daily-challenges/battle-stages-source-manifest.json)。
