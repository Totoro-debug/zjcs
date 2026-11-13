# NPC邀请、站位与战前准备

[总索引](../README.md)

状态：未完成（Map14 道路助战子模块已完成）

## 功能简介

本页的“NPC 助战”专指可邀请加入我方队伍的伙伴，不包含敌方怪物。当前助战目录仍锁定历史规则版本 `871_156598`；该版候选按 `NpcType=4`、`CanDevelop=true`、存在战斗实体和等级详情筛选，共 85 名。切换到选定 `884_156692` 的完整助战目录尚未完成。日常副本、秘境和主线地图常规战斗已接入伙伴；羁绊冒险已局部接入三名伙伴选择与实际战斗；探索首领、节庆首领和联合行动仍属未完成。道路属性门槛只计算玩家与受邀伙伴的属性，不启动战斗。地图战斗可保存邀请、站位和准备状态；开始战斗时才扣除已接入玩法的战斗助战次数。

## 原始官方实现

原 NPC 助战使用邀请资格、NeedUnlock 正反分支、队伍上限、站位／拖拽、属性技能装配及原战前准备流程。

### 待迁移的规则与表现

每日挑战、普通日常副本和山之国私图的战斗邀请入口已接入。Map14 道路属性门槛已接入原窗口、伙伴选择和本地通行状态；羁绊冒险已接入三名伙伴选择、按 `PlayType=6` 提交／清理助战战斗快照、实际战斗及用户确认的本地次数／奖励结算。探索首领、节庆首领和联合行动按用户决定暂留未完成。

邀请资格、NPC 遭遇／职业限制、最大人数、累计次数、单 NPC 次数和原冷却来自 `PlayerRobotInvite`、`npc` 与助战属性配置。选人继续使用原 NPC 邀请窗口；Map14 道路窗口从选定 884 APK prefab 恢复。客户端配置列出日常 `RobotInvitePlayType.All`（10）、秘境（2）、道路属性门槛 `PropGate`（1）、探索首领（10）、节庆首领（5）、联合行动（4）和羁绊冒险（6）；配置枚举本身不代表对应玩法已经迁移。

Map14 道路属性门槛按官方属性类型与标准累加玩家和已选 NPC 的战斗属性。其他已接入地图战斗入口满足战斗门槛后才会把 NPC 作为战斗实体加入队伍。地图战斗运行时记录助战次数快照，站位准备阶段不扣次，点击开始后保留扣次记录；已接入的日常副本与挑战沿用原 NPC 属性、技能、阶位和战前站位规则。羁绊冒险由用户确认战斗开始即扣 1 次，胜利按关卡 `stage.Awards` 发奖，失败／退出无奖励且不返还次数；其余三类玩法不据此推断离线结算行为。

探索首领、节庆首领和联合行动按用户决定暂留未完成。它们的服务端阶段推进、次数扣除、放弃行为、奖励来源与离线差异尚未形成确认方案；不从其他战斗玩法推导并写入本地规则。羁绊冒险关卡与伙伴战斗目录目前分别来自 `884_156692` 与历史 `871_156598`，版本一致性仍待核实。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

`scripts/prepare_fidelity_npc_assist.py` 从固定 APK 的 `defaultpackage_assets_config_luban.bundle` 读取 `npc_level_detail`、`npc_profession_detail`、`player_robot_invite`、`team_target`、`game_play_mode`，逐字节对照提取缓存后按原构造器完整解析，共 5 表 2,178 条。另从已校验原版本 WwiseBnk 的 `level_prop.g.bytes` 提取 450–456 共 7 个属性类，输出 `assets/official/npc-assist.json`，纳入显式加载清单。

`scripts/generate_npc_assist_reference.py` 编译固定原 C# 属性方法，产生 24 组 NPC 属性及 7 组最终属性对照，见同目录 `reference.json`。没有新增图像、自绘或新版混入；这里的转换与规则验证不表示邀请界面、助战等级下发或实际战斗已经完成。

依据：`assets/reference/npc-assist/source-manifest.json`。

scripts/prepare_dungeon_bindings.py 从 prefabs_ui_fight_changebattleposarea 的 ChangeBattlePosArea SpriteRenderer 直接提取 ImgChangeBattlePosArea，并保留原颜色、边界、PPU、DrawMode、TileMode 与 SortingOrder；从 prefabs_ecentity_fight_fightcharacter_3100 的 PrepareBattlePlayerDrag 提取 DownKeepTime 与 DragThreshold。新增 assets/official/dungeon-ui/ImgChangeBattlePosArea-41679de83e.png 为官方同版本 PNG 解码，无自绘或新版替换。输入原对象与源包 URL／哈希、输出 SHA256 归入 assets/reference/dungeon-ui/source-manifest.json；ChangeBattlePosHandler、PrepareBattlePlayerDrag／Base、FightComponent、GridGroundComponentInfo 和 Pos2d 代码 SHA256 由  保存。

Map14 道路窗口使用选定 `884_156692` 的 `RoadblockTeamPropStageDialog`、条目、五种门预览图、五种区域背景和 `RuleDialog`／`TipInfo1`。`prepare_map14_roadblock_ui.py` 解码原层级、锚点、尺寸、颜色、布局、组件字段与本地化，`OfficialMap11UiTools.Roadblocks` 显式恢复 Windows 原生 prefab、原弹窗曲线、按钮控制器、规则说明、头像和音效绑定；两个窗口按原 MaskType=3 复用已迁移的官方全局模糊遮罩，并保留其历史来源版本。五种门体、Animator、粒子和材质经原生转换后通过 `OfficialMainlineMapRuntime.RoadblockDisplays` 接入原实体生命周期与阶段事件，`ElementDoor` 原提示图按玩家自身属性达标且阶段为 0 显示。输入与输出分别见道路 UI、原生视图及补充资源来源凭据。选人条目复用原 `TeamInvitePlayerInfoView.SetProp` 与 `PropInfo` 的序列化图标／数值控件，本地宿主绑定既有官方属性图标与数值格式。道路地面读取原 `GridTransformComponent` 与 `StageResultOverrideBodyGroundTypeComponent`：关闭时三格均为 Block，完成后恢复中央 None、两侧石柱 Block；导航与冷读使用相同占位。871／884 的 34 个共享 Map14 表及 `MapInfoWrap` 字节一致；五个门的身份在加载后再与 884 元数据核对。

通天塔地图组（map 15）任务 HUD：`scripts/prepare_map15_ui.py` 从固定版任务 prefab 与语言包提取 81 条任务、366 项文本及原控件状态字段，复用已转换地图 UI 和战力颜色配置。未重建开发素材 ZIP，本地新增 `assets/official/map15-ui.json` 尚未进入完整快照。

依据：`assets/reference/official-map/tower-ui-source-manifest.json`。

## 完整还原部分

已接入圣兽试炼、双影幻境、普通日常副本、主线地图常规战斗、山之国私图／战斗碑、Map14 道路门，以及羁绊冒险伙伴选择、助战快照与战斗结算。羁绊冒险的节点目录和战斗目录来自 `884_156692`，通用助战目录仍为 `871_156598`，统一伙伴数值、技能与资格配置版本以及完整发布验证仍待完成。探索首领、节庆首领、联合行动按用户决定保留未完成。

Map14 道路助战子模块已完成，Windows 可执行版本发布至 `D:/Desktop/ZjcsGame/Zjcs.exe`。道路资源来源校验、规则持久化测试、静态检查、Unity 导入／编译、`ProjectTools.Validate` 与 Play Mode 主流程通过。独立 Windows Player 在内置屏以 540×960 实际运行，使用隔离角色存档和模拟鼠标，覆盖五种属性门的原窗口预览与取消；风属性门进一步覆盖规则弹窗、伙伴邀请／移除／取消、属性不足／达标、一次性开门、实际穿越、冷读恢复及再次通行，确认不产生战斗或奖励、不重复扣次。原窗口、元素图标、伙伴列表、关闭／开启门体与冷读后的地形画面均已实际查看。 发布目录中的 Player 已再次通过风属性门流程，全部 6,331 个交付文件与验收构建逐文件 SHA-256 一致；测试存档始终保存在独立验证目录。

- `assets/official/rules.json` 与规则来源清单提供 `npc`、`npc_level_detail`、`npc_profession_detail`、`player_robot_invite`、`skill` 数据。`FightOfficialNpcAssistanceCatalog.Load` 校验规则哈希，按 `NpcType=4`、`CanDevelop`、战斗实体 `FightEntityId` 和等级详情筛伙伴候选，并读取遇见限制、助战条件和玩法次数规则。
- `FightOfficialNpcAssistanceCatalog.CreateUnit` 按官方等级详情与职业配置计算 NPC 属性、主动技能、被动技能、冷却和状态。被动属性按官方 `NpcFightData.CalcPropsWithSkill` 的基础属性计算与 `BattleFormula.CalcFinalProps` 顺序合成。
- `OfflineNpcAssistanceCandidates` 是正式邀请入口共用的资格深模块：`FightAssist` 使用 85 名可培养伙伴，`TeamInvite` 使用原 `TeamInviteNpc` 候选链；两种模式均保留官方相遇、助战条件、战斗模型、人数上限和次数边界，并向原生邀请条目提供统一的阻塞原因和好感等级。
- Home runtime 通过 `OfficialCombatAssetCatalog.LoadNpcAssistance` 加载目录，并用 `OfflineNpcAssistanceProgress` 将选择、分玩法邀请次数和活动战斗快照保存至当前角色槽位。战斗次数由 `CommitBattle` 提交，道路通行由 `RecordUsage` 保存独立完成回执；`-1` 次数上限按不限制处理，正数上限允许达到配置值。所选 `PlayerRobotInvite` 的七行均为 `CdType=0`（每日），原 `DailyResetTime=08:00:00`；原客户端日界计算使用 `ServerInfoManager.TimeZone`。用户确认离线版全部助战次数按本地机器时间 06:00 重置，代码按本地日界处理，不依赖固定时区。保存 schema 2 以当地 06:00 边界记录次数，并把 schema 1 的本地午夜日期迁移到相应边界。
- `HomeHudView.DailyChallenges.OpenDailyChallengeAssist` 与 `OpenDailyDungeonAssist` 使用官方 `TeamInvitePanel`／`TeamInvitePanelRobotItem` 资源显示候选并保存选择。日常副本与每日挑战共用 `RobotInvitePlayType.All`（10），选择按活动／房间分别保存；战斗开始时重核助战条件、单 NPC 次数和人数上限，`DailyDungeonBattleRunner.BuildCheckpoint` 调用 `CreateUnit` 并为伙伴寻找未占用的可行走格。普通副本 checkpoint 与助战快照一起恢复，启动失败回滚次数，战斗结束清除活动快照。
- `OfficialMainlineMapRuntime.Map12Buildings` 在素材秘境战斗房间打开同一官方邀请视图，按路线运行和房间保存 `PrivateDungeon`（2）选择。确认后 `DailyDungeonBattleRunner.BeginPrivateMapRoom` 重读存档并复核候选资格、单 NPC 次数和队伍上限，以主角等级／阶位创建伙伴；`CommitBattle` 在战斗挂载前提交次数，挂载失败回滚本次提交。私图 checkpoint 保存在路线档案，助战快照保存在助战档案；恢复时核对玩法、入口、关卡和伙伴列表，既有快照不重复扣次，胜负结算清除活动快照。空队伍仍可单人进入。角色档案、公开奖励、公共次数和购买次数均未改变。完整进程冷启动恢复及多伙伴实战仍待验证。
- `OfficialBattleAssistOptions` 将助战 PlayType、入口标识和人数上限作为战斗宿主适配参数，开始时复制调用方配置；已接入的日常／每日挑战使用 `All=10`，私图使用 `PrivateDungeon=2`。恢复时检查玩法、入口、关卡及伙伴快照，非空伙伴 checkpoint 缺少已提交快照时拒绝恢复，避免重新扣次；只回滚本次新提交的事务。配置中的探索首领、节庆首领和联合行动 PlayType 尚未绑定到这些流程。
- `OfficialBattleAssistSelectionView` 提供公共原生邀请视图，输入为候选展示资料、人数上限及选择／确认／取消回调。日常副本与每日挑战共用该视图，宿主继续计算资格、剩余次数和存档选择；原 `LoopVerticalScrollRect` 负责条目复用，不再由玩法入口手排条目或复制确认按钮。确认空选择时沿用单人开战，非空选择按宿主既有事务处理。
- `OfficialMainlineMapRuntime.BattleAssist` 将同一原生邀请窗口接入主线地图常规战斗。使用官方 `TeamInvite=3` 候选链，确认后在开战前提交次数，按战斗场景可行走格创建 `CreateTeamInviteUnit` 伙伴；战斗完成清理快照，离开、挂载异常、地图关闭或启动异常回滚本次次数并显示原生地图提示。地图 12 战斗碑的 `TargetCatalog=9` 专用窗口继续使用独立的战斗碑选择与队伍上限。
- 公共视图复用已有 `Map12/Team/TeamInvitePanel`／`TeamInvitePanelRobotItem` 原生资源，替代日常目录中缺少 Sprite／字体绑定的旧副本。`record_battle_assist_selection.py` 实际读取选定884 APK，核对面板340节点／1,559对象和条目82节点／346对象；保留使用的序列化内容相同，外部引用表编号差异解析为同一原对象且原始载荷哈希相同。条目中被离线视图移除的旧在线 `AppearanceUI` 单独记录，不把它认作当前代码相同。头像绑定为空时原生控件清理上一条目的图片引用，避免复用残影；这只说明运行时绑定为空，不代表 APK／云端来源已核实缺失，也不计作正式头像完成。历史871资源身份与当前884核对凭据分别保留于[公共选择视图来源](../../assets/reference/battle-hud/assist-selection-source-manifest.json)。没有新绘图片、替代布局或修改原资源载荷。公共视图的自由排序／职业筛选、完整NPC外观、条件提示参数及战力字段仍待接通；Map14 道路入口已单独接入要求元素属性的显示，并直接复用 `TeamInviteRobotComparer.RobotElementPropTypeComparer` 的官方优先级。不能据此认定整个通用邀请UI完成。

当前公共视图验收通过 Unity 窗口 Play Mode 模拟输入，覆盖候选选择／取消、确认／关闭、列表缩放、窗口释放和实际画面；额外检查 11015 姓名占位沿用官方候选姓名颜色、11014 不显示占位。`scripts/check_unity_project.py` 通过；`ProjectTools.Validate` 在本次姓名对比度调整前通过，调整后的再次运行因同一工程已有 Unity 实例而被拒绝，最终代码编译及面板流程由上述 Play Mode 验收确认。

伙伴头像另按[头像来源清单](../../assets/reference/battle-hud/npc-portraits-source-manifest.json)检查。当前选定 `884_156692` 的 APK／云端资源扫描了全部 9 个 `npc_icon_map_*` bundle（地图 9、11–14、17、19、21、23），未发现 `npc_icon_11015`–`11022` 的原始 Sprite 或 Texture2D。按用户确认，这 8 名联动伙伴在原生头像区域显示姓名文字，限定于这 8 个 ID；没有绘制或替换贴图。它们仍按[离线访问决定](offline-access.md#暂不开放的联动活动及伙伴)暂停开放，且当前本地战斗模型缺失，因此不可助战。

山之国战斗碑沿原 `TeamInviteNpc` 候选链筛选伙伴：排除探索首领，再按 `IsFightAssist`、遇见状态和助战条件过滤。当前候选为 95 名，含 9 名导师及 1 名商人；`TeamInviteNpcClassIds`、`CanInviteTeamNpc` 和 `CreateTeamInviteUnit` 服务于该入口，既有伙伴 API 仍按原 85 名范围运行。

战斗碑组队调用 `TeamPanelHelper` 的普通 Player 邀请页，再切换 NPC 标签；该入口没有初始化 `TeamInviteRobotPanel` 的 `RobotInvitePlayType`，因此这里不套用日常助战的 All=10 配置。原 Team 页及邀请依赖的10份 prefab／645节点／1105个脚本载荷已完整解码并核对引用，战斗碑使用已转换的 Map12 TeamInvitePanel、NPC 列表、本地战斗碑进度和 `CreateTeamInviteUnit` 实际战斗接线；Player 邀请仍未接入本地多人模拟，更广泛的其他地图／玩法 Team 窗口仍待接通，见[山之国](../maps/map-12.md)。

山之国原队伍邀请新增直接迁移的 `PlayerPlayTeamAction`，保留原 Team 动作与 NoneParam，并复用原状态互斥检查。原 NPC 邀请窗口和本地队伍事务尚未绑定，不代表可从正式地图邀请。

## 自定义实现

以下为已确认的单机处理要求，尚未代表对应玩法流程已经接入或验收。

Map14 五个道路门使用原属性门槛窗口与 `PropGate` 邀请资格。属性达标后直接开启对应道路、保存完成状态并记录本次邀请使用；不启动战斗，也不额外发放奖励。选中伙伴与通行记录保存在当前角色槽位；助战归档先写入不可重复扣次的完成回执，再保存地图完成状态，重载时据回执修复中断写档并恢复道路开放状态。官方道路阈值、原生窗口和运行时放置实体均按选定 `884_156692` 来源核对。

伙伴等级／阶位按玩法处理，不能统一套用一种公式。通用战斗邀请 `TeamInviteNpc` 创建的 `NpcTargetData` 初始为 `NpcLevel=0`、`NpcSubRank=None`，随后 `PlayerInviteInfoManager` 调用 `OperPlayerQueryTeamInvitePlayersInfoByTargetData` 获取 `TeamInvitePlayerInfoData.Level/SubRank`；现有客户端源码没有该服务端赋值公式。用户已确认通用战斗伙伴等级沿用当前玩家等级、阶位沿用玩家存档中同步的 `SubRank`；`DailyDungeonBattleRunner.AddAssistNpcs` 按这两个值创建本地伙伴单位。NPC 冒险页 `NpcAdventurePanelMainContent` 明确同步主角等级与阶位；秘境故事伙伴默认跟随主角，并允许 `TeamInviteNPCNode` 指定等级／阶位；`PlayerBriefInfoUtils` 的 `CreatePlanesRank` 逻辑仅用于数据无效或跨赛季时校正，不是通用邀请的等级生成规则。

用户进一步确认通用多人战斗邀请在离线版组成“本地主角 + 所选伙伴”队伍；沿用上述等级／阶位规则及原 NPC 候选资格、排序项、队伍人数和助战次数配置，不生成或模拟其他玩家队伍成员。此为单机队伍模型确认，不表示正式 NPC 邀请窗口和战斗入口已经接通。

用户已确认伙伴通话时区固定 UTC+8；助战使用单独确认的本地时区 06:00 重置方案。原服务器重置值为 `08:00:00`，离线版采用用户指定时刻；当前计数以本地 06:00 边界保存，并兼容旧 schema 的本地午夜日期。

羁绊冒险规则和旅行收益见[羁绊冒险](bond-adventure.md)与[伙伴旅行](partner-travel.md)；NPC战斗邀请入口仍未接线。

### 已确认的本地接线与边界

`prepare_official_rules.py` 曾从官方配置导出 `stage_map_explore_boss`、`activity_festival_boss`、`activity_festival_boss_layer`、`activity_festival_boss_npc`；相关来源清单记录表、构造器和客户端调用依据。配置数据和调用依据不代表这三种玩法已有确认的离线规则或完整接线。Map14 道路入口位于 `OfficialMainlineMapRuntime.Roadblocks`。

## 尚未实现

- 公共邀请窗口的部分锁定候选仍显示未格式化的好感门槛 `{0}`，并与本地不可用原因叠字；已在日常副本 Windows Player 的原邀请窗口实图中确认，尚待统一修复。

完整骨骼分支已增加随包交付的本地资源目录与原生角色对象。`prepare_partner_native_battle_assets.py` 校验既有数据／Animator 凭据，再以 `PartnerNativeBattleAssetCatalog` 显式绑定35个骨骼包、35份原材质字段、28份运行时贴图和34个原控制器；原 shader 对象对应的 Windows 转换载荷按原字节安装至 `NativePartnerAssets/`。`PartnerNativeBattleActor` 恢复原 Transform 链、组合皮肤、材质参数与渲染设置，由原 Animator 驱动独立几何，并单独推进11005背饰；运行时只读取 Unity 本地对象引用。输入、输出、shader 对象及绑定元数据哈希见[本地绑定来源凭据](../../assets/reference/partner-native-battle-assets-source-manifest.json)。26名显示原贴图；11015–11022 显示与当前24×24原纹理对应的白色轮廓，不计作完整外观恢复，仍需继续核对所选来源。该角色对象尚未接入正式战斗的候选资格、事件播放、状态特效及附着点；未完成正式邀请入口、完整实战或 Windows Player 验证，未生成完整素材快照。

完整骨骼分支的技能 Animator 节点已按当前版本单独恢复。`prepare_partner_native_battle_playables.py` 从选定884 APK的原 `npc_level_detail`、`npc_profession_detail`、`skill` 构造器读取34名伙伴的全部等级／阶位配置和主动技能，包含11038的随机技能列表，共75个技能、93个默认／方向分支和95个 Animator 节点。`Combat/partner-native-playables.json` 保留原节点顺序、延迟、方向分支、触发参数及其他参数字段；原 Sequence 没有可序列化的固定总时长，未编造持续时间。跟随骨骼／部位、音效、朝向和镜头等其他节点只记录原字段与来源，未据此宣称完整技能表现。配置构造器、组件模式、原对象载荷、应用源码和输出哈希见[伙伴技能节点来源](../../assets/reference/partner-native-playables-source-manifest.json)。

伙伴战斗动画由 `OfficialBattleAnimatorPlayback` 驱动原 `MyPlayableAnimator`／`AnimationController` 和本地角色对象；已接入节点状态与早期姿态，完整动作结束、技能效果、战斗事件和邀请入口仍有缺项。

完整骨骼分支的数据与原贴图映射已恢复，战斗绑定仍未完成。`prepare_partner_native_battle.py` 按选定 `884_156692` APK／匹配云端依赖检查余下 34 个不含 `SpineLazyAnimLoad` 的实体，完整读取原 Spine binary，恢复 35 个骨骼渲染器和 791 段动画，包括 11005 的独立 `SkeletonAnimation` 背饰；保留 Atlas、原材质／MeshRenderer 参数、Animator 状态／别名、实体根至骨骼的 Transform 链及 BoneFollower／AttachmentFollower 字段，安装至 `unity/Assets/Official/Combat/NativePartners/`。独立 `ZJCSPNBT` 包由 `ProfessionNativeSkeletonData.LoadPartnerBattle` 校验版本、NPC 身份、解压长度与内容哈希；不与立绘包混用。原 11007 的 Transform `bone41.mixY=-0.4` 保留，显式战斗包沿用有限外推几何分支。

完整骨骼分支的原 Animator 资产已恢复。`prepare_partner_native_battle_animators.py` 复用固定原生转换缓存，复制34个原控制器和532个片段文件，仅重绑外部资产 GUID；逐项核对原编译资源的参数顺序／类型／默认值、层、默认状态、状态数值／动作、过渡顺序／目标／条件／时长／打断标记和片段时长。11004、11014 的原一维混合树及子节点阈值／速度／偏移／镜像保留。`PartnerNativeBattleAnimatorCatalog` 在 `Combat/NativePartnerAnimators/catalog` 显式绑定 NPC 与控制器，载荷位于 `unity/Assets/Official/Combat/NativePartnerAnimators/`；来源与输入输出哈希见[Animator来源凭据](../../assets/reference/partner-native-battle-animators-source-manifest.json)。

NPC 动画按原 Animator 状态时钟驱动骨骼混合、路径、挂点与背饰；零时长过渡保持原状态，不插入自定义平滑。11033 的 `200/die_02` 动画时长为 0，骨骼关键帧终点为 0.333333343 秒，按两份原数据分别保留。

`ProfessionNativeAnimatorPose` 通过显式伙伴分支消费11004、11014的原两叶待机混合树；由 Unity 提供片段顺序、状态时钟和互补权重，端点排除零权重叶，中间权重按原 autoReset／MixNext／Replace 模式先混合局部通道，再按原顺序求解约束。依据[4.2公开混合说明](https://esotericsoftware.com/spine-unity-main-components-4.2#Animation-Blending-Control)，未复制动画 SDK；默认调用仍拒绝未绑定的混合树。尚未验证正式战斗与 Windows Player。Animator 来源清单按稳定路径排序，恢复的566项原控制器／片段资产内容保持一致。

`PartnerNativeBattleAttachments` 从选定884 APK的34份原实体 prefab 恢复1,085个 Transform／RectTransform 节点，保留父子与同级顺序、坐标、旋转、缩放、激活、Layer 和原矩形锚点／尺寸／轴心；复用原 `AttachAgent` 的888条注册记录，包括原名称顺序、同节点别名和空引用，不重新扫描生成另一份注册表。35个原骨骼组件显式绑定到既有独立姿态接口，34个阴影节点直接运行迁移的官方应用类 `AttachmentFollower`，沿原渲染器引用、shadow 骨骼与跟随字段更新。四份 SDK `BoneFollower` 仅保存原序列化配置，尚未迁移运行行为；其他记录中的 prefab HUD、SpriteRenderer 和特效组件也未由该挂点模块重建，不能将恢复节点层级视为完整 prefab 表现。原对象／组件模式、应用源码、转换与本地绑定哈希见[挂点来源](../../assets/reference/partner-native-attachments-source-manifest.json)。

伙伴阴影使用原动画姿态、位移、旋转与反射变换，并按原 `AttachPosRotCfg` 计算挂点偏移。完整 NPC 战斗模型、助战动画及正式战斗流程仍有缺项。

`PartnerNativeBattleAnimationBinding` 从选定884的34份原实体读取应用层 `AnimationController` 与 Animator 指针，显式绑定到原实体根节点，复用原控制器代码。保留 `InitOnAwake=true`、`CreateOverrideController=true`、`NotCreateGraph=false`、禁用时不重置初始化状态及原启用字段；实例暂存为非激活后再恢复激活，使原 Awake 在引用就绪后运行。释放时仅清理本次组件与覆盖控制器，恢复原 Animator 引用，不销毁实体节点。该绑定由调用方显式创建，既有独立姿态夹具不自动改变控制器生命周期。原组件对象、组件模式、应用源码和安装载荷哈希见[伙伴应用动画绑定来源](../../assets/reference/partner-native-animation-bindings-source-manifest.json)。

全部进入匹配触发器的原目标状态；原 Awake 初始化、禁用／再启用、覆盖控制器独立创建、释放后原引用恢复与组件清理均通过。该夹具直接播放原节点，只证明应用动画绑定与早期姿态，不证明完整技能特效图、正式邀请实战或 Windows Player。

伙伴完整技能图复用已转换的75个原技能实体、93份方向／默认 prefab。`PartnerNativeSkillSkeleton` 在生产 `OfficialBattleSkillGraphPlayback` 的实例工厂中，于 Awake 前为58550内嵌的原4.2伙伴骨骼绑定既有独立几何端口；保留原模型与单个分离渲染子节点、`100/skill_01_2` 循环、原 Update 时序、停用不清空动画状态及原 shadow 附着。该模型与11007原实体使用同一骨骼／材质对象，按原 `SkeletonRendererHighTexturePreview` 的高清加载引用绑定2048×668原贴图；其他原技能特效继续沿现有路径加载。`PartnerNativeBattleMesh` 复用原角色的网格、材质字段与渲染设置操作，不读取或重建 Spine SDK 算法。原 prefab 组件、加载器、贴图对象／像素、应用类来源及当前绑定输入输出哈希见[内嵌骨骼来源凭据](../../assets/reference/partner-native-skill-skeletons-source-manifest.json)。

逐帧核对已初始化原节点的应用组件，58550另检查原骨骼身份、高清页、现有分离子节点和旧组件替换，未出现原 atlas 缺失或附着类型异常。高清贴图导入后的RGBA哈希、尺寸、mip、颜色空间、过滤／Wrap及原不可读设置一致。11015–11022现按[已确认联动范围](offline-access.md#暂不开放的联动活动及伙伴)不开放并保留原资源。

伙伴角色视图已接入公共战斗场景：`OfficialBattlePartnerNativeModelView` 使用选定884的原实体、原 Animator／应用控制器和挂接注册表，复用生产技能图作用域；移动读取原角色的移动技能／行为节点，完整技能图继续控制原 Animator，不叠加别名动画。`prepare_partner_native_role_views.py` 解码34份原 `ECViewDriver` 的 `ECFightRoleCharacterView`，核对共享死亡／开局死亡 prefab 的原对象与载荷哈希，并恢复10608／10708引用的原生成序列。死亡状态按原无限期序列保留，复活停止序列，读档使用原开局死亡分支并恢复格点。11008原控制器与骨骼没有死亡动画和 `die`／`immediately_die` 参数；保留通用死亡视图的原引用及其警告行为，不新增参数或动画。原字段、组件模式、共享图来源与显式代码绑定见[角色视图来源](../../assets/reference/partner-native-role-views-source-manifest.json)。

`prepare_partner_native_shadows.py` 沿原实体提取34份完整阴影子树及共用 Sprite／贴图，保留原 Transform、SpriteRenderer、附着／骨骼跟随字段，复用公共阴影转换与既有独立跟随实现。场景用原阴影替换挂接图中的空节点，同时重绑原 `AttachAgent` 注册表；角色网格与阴影沿公共战斗排序。输入对象、转换缓存与安装载荷哈希见[伙伴阴影来源](../../assets/reference/partner-native-shadows-source-manifest.json)。这些载荷尚未纳入完整素材快照。

原生伙伴血条复用选定884的完整 `HpBarGame` 子树、4个 `SlicedFilledRender`、原 `HpSliderEffect`／`BattleHpSpriteSlider` 与 `GamePlayManagerConfig`；34份原 prefab 共67项资源（8张原纹理、3份完整Windows shader）按原字段、层级、坐标和引用加载。`OfficialBattlePartnerHpView` 从公共战斗快照同步HP、最后最大生命与持久护盾，保留原 `BattleHpAndShieldHandle.RefreshShieldAndHp` 的上下限分支；原血条代码保留UniTask逐帧缓动、取消、回血亮边／顶端动画和阵营色，特效复用原对象池及预建数量。原 `TransformLocker` 的旋转／缩放更新由原生视图tick消费；替换原空血条分支后同步原挂点引用。阵营排序沿原直接／默认／军团分支，当前范围为已确认的本地主角与伙伴队伍，不包含在线观战。未验证正式邀请鼠标流程、其余角色完整血条、名称／状态分支、实际声音或Windows Player。入口为 `OfficialPartnerNativeRoleSceneProbe`，来源与接线见[原生血条凭据](../../assets/reference/battle-animation/partner-native-hp-bars-source-manifest.json)。

伙伴社交编号与战斗实体编号分别保存：新增 `FightBattleUnitState.RoleEntityId`，创建伙伴时从原 `NpcInfo.FightEntityId` 绑定角色实体及其 `FightRoleCharacterComponent.CharacterType`；移动规则、场景实体和血条查询优先使用该实体，避免把11001等社交编号当成战斗实体或把伙伴设成主角类型。恢复旧 checkpoint 时重新核对并补全此字段。其中11004保留原15320／15321移动分支。11015–11022按[已确认联动范围](offline-access.md#暂不开放的联动活动及伙伴)不开放，原资源保留。这些仍须继续完成。

原生伙伴宿主集成现使用11001／11004／11005与本地主角进入实际日常副本场景；断言包含三名伙伴的原 Transform 链、Animator、实际网格与阴影，以及本场3206／3207的原挂点表；原16010技能不再发生停止挂接异常，怪物迁移边界见[战斗执行](battle-execution.md)。当前34份原生伙伴的血条已沿原 prefab 接入；其余伙伴的完整血条迁移，以及主城既有黑白几何／白块等显示问题仍未完成。不修改全局编辑器设置。

贴图沿原 `AppearanceRendererLite.SkeletonRenderer` 指针、`AppearanceConfig` 骨骼路径和 `AppearanceAddressUtils.GetTextureLoaderPath` 恢复。35 个渲染器均使用原 Low 分辨率；原材质的占位页与运行时 `TextureLoader_Low` 的实际目标分别保存，35 项映射对应 28 份不同的目标 PNG。11015–11022 的原 Low 目标为 24×24，当前原 High 目标也为该尺寸，未以其他图片替换。隔离 Unity 工程复用正式工程的 Linear 颜色空间；全部目标导入后的 RGBA 像素哈希、尺寸、mip、颜色空间、过滤／各轴 Wrap 参数一致，并恢复原可读性。

`ProfessionNativeOrderedConstraintPose` 按原 order 交错求解 IK 与 Transform，保留前序约束结果；`ProfessionNativeAffineIk` 使用独立平面几何处理单骨骼、双骨骼的圆形／非均匀缩放椭圆及反射坐标，保留原混合、弯曲、伸展和适用的柔化分支。非均匀缩放时子骨骼 Y 归零且不伸展，依据[公开 IK 约束说明](https://esotericsoftware.com/spine-ik-constraints)；该分支的非零 softness 仍显式拒绝，当前相关原配置和动画均为零。恢复原 noScaleOrReflection 继承、11042 的相对局部旋转；原非凸裁剪边界通过独立三角剖分接入既有半平面裁剪，保持原三角形朝向与插值 UV，不复制动画 SDK。

`ProfessionNativePathAnimation` 支持原 path position、spacing 和 mix 曲线；`MixNext` 按原规则替换已开始的 incoming 通道并保留未设置通道的 outgoing 状态。当前接入的 11009 单骨骼路径和 11010 三十节骨骼链保留原曲线、长度间距、端点外延与链式旋转；其他闭合路径、非恒速曲线和未使用的 spacing／rotation 模式不支持。伙伴原生路径动画及网格资源见[骨骼战斗来源清单](../../assets/reference/partner-native-battle-source-manifest.json)。相关伙伴尚未进入正式战斗候选。

- 探索首领（All=10）、节庆首领（5）、联合行动（4）各自的实际选择入口、条件筛选、官方排序、战斗加入、快照提交及结算接线；不能用日常副本的 All 规则替代这些玩法。
- 羁绊冒险（6）已局部接入三名伙伴邀请与战斗快照，当前运行验证范围及未完成的版本核对、模型与结算表现见[羁绊冒险](bond-adventure.md)；NPC 旅行属于派遣系统。
- NPC 战斗模型和动画尚未完整接入。只有带 `idle_01` 与 `run_01` 的模型可加入候选；目前可用的 `body_*` 模型不具备战斗动画，正式伙伴战斗模型仍需转换并绑定。模型层级和资源来源见[伙伴战斗模型来源凭据](../../assets/reference/partner-battle-fighters-source-manifest.json)。
- 等级／阶位规则目前接入日常助战路线；探索首领、节庆首领和联合行动仍需连接各自的邀请入口、条件与结算。

## 代码与资源入口

官方实现：`.tools/source-packages/official-code/common-source/Common/NpcFightData.cs` (`CalcPropsWithSkill`)、`.tools/source-packages/official-code/common-source/Common/NpcLevelDetailInfoParser.cs` (`Get`)、`.tools/source-packages/official-code/client-source/NpcUtils.cs` (`IsNpcAssistLimit`)、`.tools/source-packages/official-code/client-source/TeamInviteNpc.cs` (`GetAssistNpcActorTargetDatas`)、`.tools/source-packages/official-code/client-source/TeamInvitePanel.cs` (NPC 邀请页)、`.tools/source-packages/official-code/common-source/PlayerInviteInfoManager.cs` (助战候选资料请求)、`.tools/source-packages/official-code/client-source/NpcAdventurePanelMainContent.cs` (伙伴冒险等级同步)、`.tools/source-packages/official-code/client-source/TeamInvitePanelPrivateMap.cs` 与 `TeamInviteNPCNode.cs` (剧情伙伴等级)、`.tools/source-packages/official-code/common-source/PlayerBriefInfoUtils.cs` (跨赛季数据校正)。客户端代码来自旧版固定 APK `871_156598`；当前伙伴候选按新版 `871_156598` 的 `NpcType=4`、`CanDevelop`、战斗实体和等级详情识别。

配置与素材：[来源总表](../../assets/reference/SOURCES.md)、`assets/official/npc-assistance-ui.json`。通用邀请素材来自固定 APK `defaultpackage_assets_prefabs_ui_team.bundle`；固定包的提取记录以来源清单为准。

原生伙伴附着的编辑器校验显式引用外观跟随端口和剧情播放程序集，以解析原附着组件使用的类型；动画绑定校验另外显式引用原剧情动画控制程序集。

Unity 当前实现：规则目录 `unity/Assets/Game/Runtime/Official/Combat/FightOfficialNpcAssistanceCatalog.cs`、统一资格 `unity/Assets/Game/Runtime/Offline/World/OfflineNpcAssistanceCandidates.cs`、次数与战斗快照 `unity/Assets/Game/Runtime/Offline/World/OfflineNpcAssistanceProgress.cs`、官方 Team picker `unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleAssistSelectionView.cs`、主线战斗接线 `unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.BattleAssist.cs`、道路门接线 `unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Roadblocks.cs`、日常战斗接线 `unity/Assets/Game/Runtime/Presentation/Home/DailyDungeonBattleRunner.cs`。普通日常副本入口位于 `HomeHudView.Dungeons.SelectDailyDungeonRoom`；主线战斗入口位于 `OfficialMainlineMapRuntime.StartEntityStage`；山之国私图入口位于 `OfficialMainlineMapRuntime.Map12Buildings.EnterMap12PrivateDungeonBattle`。羁绊冒险的独立入口与结算由 `HomeHudView.NpcAdventureBattle`／`NpcAdventureBattleRunner` 接线；探索首领、节庆首领和联合行动入口仍未接线。TeamInvitePanel 的 bundle 输入、45 项官方依赖、6 个运行时 prefab 与 52 项共享绑定记录见 [team-ui-source-manifest.json](../../assets/reference/npc-assistance/team-ui-source-manifest.json)。
