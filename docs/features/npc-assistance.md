# NPC邀请、站位与战前准备

[总索引](../README.md)

状态：未完成

## 功能简介

本页的“NPC 助战”专指可邀请加入我方队伍的伙伴，不包含敌方怪物。当前规则版本 `871_156598` 中，伙伴候选按 `NpcType=4`、`CanDevelop=true`、存在战斗实体和等级详情筛选，共 85 名；职业导师和商人不属于此集合。日常副本、秘境、道路属性门槛、探索首领、节庆首领、联合行动和羁绊冒险等玩法使用伙伴助战时，按各玩法资格进入我方战斗。地图战斗可保存邀请、站位和准备状态；开始战斗时才扣除助战次数。

## 原始官方实现

原 NPC 助战使用邀请资格、NeedUnlock 正反分支、队伍上限、站位／拖拽、属性技能装配及原战前准备流程。

### 待迁移的规则与表现

以下要求中每日挑战及日常副本已经接入；其余玩法仍是迁移目标，不构成当前实现结论。

邀请资格、NPC 遭遇／职业限制、最大人数、累计次数、单 NPC 次数和原冷却来自 `PlayerRobotInvite`、`npc` 与助战属性配置。选人继续使用原 NPC 邀请窗口；原地图道路窗口、探索首领组队窗口、节庆首领面板、节庆助战说明窗及其条目沿固定 APK prefab 和布局接入。节庆邀请列表将 `ActivityBuff` 作为首个排序项，并按 `activity_festival_boss_npc` 标出对应助战。日常使用 `RobotInvitePlayType.All`（10），秘境为 2，道路属性门槛为 `PropGate`（1），探索首领使用 `All`（10），节庆首领为 5，联合行动为 4，羁绊冒险为 6。

地图属性门槛按 `StageConPropStandard` 累加已选 NPC 的官方战斗属性；条件满足后，NPC 作为实际战斗实体加入队伍。地图战斗运行时记录助战次数快照，站位准备阶段不扣次，点击开始后保留扣次记录；日常副本、挑战与羁绊战斗复用同一 NPC 属性、技能、阶位和战前站位规则。

探索首领按地图 `StageResultExploreBossComponent.Info.StageIds` 顺序查找首个满足 `CanShowMapClass` 且须实现 `CheckStageId` 的配置，使用原挑战信件和组队窗口。挑战时限来自 `stage_map_explore_boss.Duration`；开始、胜利结算和放弃均保存到本地地图进度。

节庆首领读取 `activity_festival_boss`、`activity_festival_boss_layer` 与 `activity_festival_boss_npc`，分别保存每层基础剩余血量、当日最高单次伤害、击破数和领奖数，并按原层序推进；面板血量显示基础血量扣除当日最高单次伤害后的结果，次日刷新将该伤害计入基础血量并清零当日记录。击破判定沿用 `PlayerActivityFestivalBossDataExt.UpdateActivityFestivalBossLayerData` 的伤害规则；重复层在每日刷新后恢复。每日战斗奖励按活动每日一次发放，重复层等级偏移按击破次数套用 `RepeatLevelOffset`。关卡奖项、怪物掉落和普通通关标记不会重复计入活动奖励。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

`scripts/prepare_fidelity_npc_assist.py` 从固定 APK 的 `defaultpackage_assets_config_luban.bundle` 读取 `npc_level_detail`、`npc_profession_detail`、`player_robot_invite`、`team_target`、`game_play_mode`，逐字节对照提取缓存后按原构造器完整解析，共 5 表 2,178 条。另从已校验原版本 WwiseBnk 的 `level_prop.g.bytes` 提取 450–456 共 7 个属性类，输出 `assets/official/npc-assist.json`，纳入显式加载清单。

`scripts/generate_npc_assist_reference.py` 编译固定原 C# 属性方法，产生 24 组 NPC 属性及 7 组最终属性对照，见同目录 `reference.json`。没有新增图像、自绘或新版混入；这里的转换与规则验证不表示邀请界面、助战等级下发或实际战斗已经完成。

依据：`assets/reference/npc-assist/source-manifest.json`。

scripts/prepare_dungeon_bindings.py 从 prefabs_ui_fight_changebattleposarea 的 ChangeBattlePosArea SpriteRenderer 直接提取 ImgChangeBattlePosArea，并保留原颜色、边界、PPU、DrawMode、TileMode 与 SortingOrder；从 prefabs_ecentity_fight_fightcharacter_3100 的 PrepareBattlePlayerDrag 提取 DownKeepTime 与 DragThreshold。新增 assets/official/dungeon-ui/ImgChangeBattlePosArea-41679de83e.png 为官方同版本 PNG 解码，无自绘或新版替换。输入原对象与源包 URL／哈希、输出 SHA256 归入 assets/reference/dungeon-ui/source-manifest.json；ChangeBattlePosHandler、PrepareBattlePlayerDrag／Base、FightComponent、GridGroundComponentInfo 和 Pos2d 代码 SHA256 由  保存。

`prepare_npc_assistance_ui.py` 从固定 `871_156598` APK／校验 CDN 转换 `ExploreBossTeamDialog`、`ActivityFestivalBossPanel`、`ActivityFestivalBossAssistDialog`、助战说明条目、邀请面板及邀请条目；运行布局为 `assets/official/npc-assistance-ui.json`，原 prefab、Sprite、调用代码、状态说明文本及 SHA256 见 [npc-assistance/source-manifest.json](../../assets/reference/npc-assistance/source-manifest.json)。道路属性门槛复用原 `RoadblockTeamPropStageDialog` 与地图 UI；未绘制替代图片。

通天塔地图组（map 15）任务 HUD：`scripts/prepare_map15_ui.py` 从固定版任务 prefab 与语言包提取 81 条任务、366 项文本及原控件状态字段，复用已转换地图 UI 和战力颜色配置。未重建开发素材 ZIP，本地新增 `assets/official/map15-ui.json` 尚未进入完整快照。

依据：`assets/reference/official-map/tower-ui-source-manifest.json`。

## 完整还原部分

已接入圣兽试炼、双影幻境及普通日常副本的候选选择、存档、战斗单位与次数事务；公共原生选择视图已完成独立 Play Mode 输入检查，其余玩法入口及非空伙伴队伍的完整实战验证仍未完成。

- `assets/official/rules.json` 与规则来源清单提供 `npc`、`npc_level_detail`、`npc_profession_detail`、`player_robot_invite`、`skill` 数据。`FightOfficialNpcAssistanceCatalog.Load` 校验规则哈希，按 `NpcType=4`、`CanDevelop`、战斗实体 `FightEntityId` 和等级详情筛伙伴候选，并读取遇见限制、助战条件和玩法次数规则。
- `FightOfficialNpcAssistanceCatalog.CreateUnit` 按官方等级详情与职业配置计算 NPC 属性、主动技能、被动技能、冷却和状态。被动属性按官方 `NpcFightData.CalcPropsWithSkill` 的基础属性计算与 `BattleFormula.CalcFinalProps` 顺序合成。
- Home runtime 通过 `OfficialCombatAssetCatalog.LoadNpcAssistance` 加载目录，并用 `OfflineNpcAssistanceProgress` 将选择、分玩法邀请次数和活动战斗快照保存至当前角色槽位。次数只由 `CommitBattle` 提交；`-1` 次数上限按不限制处理，正数上限允许达到配置值。
- `HomeHudView.DailyChallenges.OpenDailyChallengeAssist` 与 `OpenDailyDungeonAssist` 使用官方 `TeamInvitePanel`／`TeamInvitePanelRobotItem` 资源显示候选并保存选择。日常副本与每日挑战共用 `RobotInvitePlayType.All`（10），选择按活动／房间分别保存；战斗开始时重核助战条件、单 NPC 次数和人数上限，`DailyDungeonBattleRunner.BuildCheckpoint` 调用 `CreateUnit` 并为伙伴寻找未占用的可行走格。普通副本 checkpoint 与助战快照一起恢复，启动失败回滚次数，战斗结束清除活动快照。
- `OfficialBattleAssistOptions` 将助战 PlayType、入口标识和人数上限作为战斗宿主适配参数，开始时复制调用方配置；日常／每日挑战默认使用官方 `All=10`，节庆首领可传入 5。恢复时检查玩法、入口、关卡及伙伴快照，非空伙伴 checkpoint 缺少已提交快照时拒绝恢复，避免重新扣次；只回滚本次新提交的事务。独立内置屏 Play Mode 以空伙伴选择验证自定义玩法／入口、配置复制、错误玩法拒绝、已有快照恢复、387步真实结算清理和主城姿态恢复，通过；该夹具不证明非空伙伴扣次、节庆活动层、奖励或正式入口已完成。
- `OfficialBattleAssistSelectionView` 提供公共原生邀请视图，输入为候选展示资料、人数上限及选择／确认／取消回调。日常副本与每日挑战共用该视图，宿主继续计算资格、剩余次数和存档选择；原 `LoopVerticalScrollRect` 负责条目复用，不再由玩法入口手排条目或复制确认按钮。确认空选择时沿用单人开战，非空选择按宿主既有事务处理。
- 公共视图复用已有 `Map12/Team/TeamInvitePanel`／`TeamInvitePanelRobotItem` 原生资源，替代日常目录中缺少 Sprite／字体绑定的旧副本。`record_battle_assist_selection.py` 实际读取选定884 APK，核对面板340节点／1,559对象和条目82节点／346对象；保留使用的序列化内容相同，外部引用表编号差异解析为同一原对象且原始载荷哈希相同。条目中被离线视图移除的旧在线 `AppearanceUI` 单独记录，不把它认作当前代码相同。历史871资源身份与当前884核对凭据分别保留于[公共选择视图来源](../../assets/reference/battle-hud/assist-selection-source-manifest.json)。没有新绘图片、替代布局或修改原资源载荷。排序／职业筛选、完整NPC头像与外观、条件提示参数及战力字段仍未在公共视图接通，不计作完整邀请UI迁移。

山之国战斗碑的原 `TeamInviteNpc` 使用独立 Team 候选链：`GetNpcs(MapExploreBoss)` 排除探索首领，再按 `IsFightAssist` 和原遇见／助战条件筛选，当前配置共有95名候选，包括具有正式战斗数据的9名导师和1名商人。新增 `TeamInviteNpcClassIds`／`CanInviteTeamNpc`／`CreateTeamInviteUnit` 供该原组队链使用；上述既有伙伴 API 的85名范围保留。95名资格、10名额外角色实际属性／技能装配及伙伴结果一致性已通过独立规则检查；战斗碑现已接入原 TeamInvitePanel 的 NPC 页、选择保存、队伍容量校验、战斗单位创建和站位准备，范围与原代码哈希见[山之国](../maps/map-12.md)。

战斗碑组队调用 `TeamPanelHelper` 的普通 Player 邀请页，再切换 NPC 标签；该入口没有初始化 `TeamInviteRobotPanel` 的 `RobotInvitePlayType`，因此这里不套用日常助战的 All=10 配置。原 Team 页及邀请依赖的10份 prefab／645节点／1105个脚本载荷已完整解码并核对引用，战斗碑使用已转换的 Map12 TeamInvitePanel、NPC 列表与本地战斗碑进度接线；更广泛的其他地图／玩法 Team 窗口仍待接通，见[山之国](../maps/map-12.md)。

山之国原队伍邀请新增直接迁移的 `PlayerPlayTeamAction`，保留原 Team 动作与 NoneParam，并复用原状态互斥检查。独立编译与规则夹具验证邀请前的聊天／舞会／匹配限制、异步退出取消、退出成功及重新检查；原 NPC 邀请窗口和本地队伍事务尚未绑定，不代表可从正式地图邀请。

## 自定义实现

以下为已确认的单机处理要求，尚未代表对应玩法流程已经接入或验收。

伙伴等级／阶位按玩法处理，不能统一套用一种公式。通用战斗邀请 `TeamInviteNpc` 创建的 `NpcTargetData` 初始为 `NpcLevel=0`、`NpcSubRank=None`，随后 `PlayerInviteInfoManager` 调用 `OperPlayerQueryTeamInvitePlayersInfoByTargetData` 获取 `TeamInvitePlayerInfoData.Level/SubRank`；现有客户端源码没有该服务端赋值公式。用户已确认通用战斗伙伴等级沿用当前玩家等级、阶位沿用玩家存档中同步的 `SubRank`；`DailyDungeonBattleRunner.AddAssistNpcs` 按这两个值创建本地伙伴单位。NPC 冒险页 `NpcAdventurePanelMainContent` 明确同步主角等级与阶位；秘境故事伙伴默认跟随主角，并允许 `TeamInviteNPCNode` 指定等级／阶位；`PlayerBriefInfoUtils` 的 `CreatePlanesRank` 逻辑仅用于数据无效或跨赛季时校正，不是通用邀请的等级生成规则。

用户进一步确认通用多人战斗邀请在离线版组成“本地主角 + 所选伙伴”队伍；沿用上述等级／阶位规则及原 NPC 候选资格、排序项、队伍人数和助战次数配置，不生成或模拟其他玩家队伍成员。此为单机队伍模型确认，不表示正式 NPC 邀请窗口和战斗入口已经接通。

探索首领的敌方目标候选和击败后的地图进度属于探索首领玩法，不是 NPC 助战伙伴候选。单机该玩法按上述配置顺序选择首个地图匹配且 `CheckStageId` 已完成的目标，并在本地保存时限、开始、放弃和胜利结果；此处只记录玩法调用伙伴助战时的关联流程。

节庆首领击破、血量同步、层推进和领奖原由服务器保存；单机根据原层配置维护这些状态，并将待领奖励合并为本地逐层领取操作。活动 200、216 标记 `UseSeverLevelAndRank=true`，服务器的玩家等级／阶位公式无法在客户端复用；经用户确认，本地使用活动 `DefaultLevel + LevelOffset` 计算等级（首层为 `25 - 14 = 11`），并沿用关卡 `SubRank`。重复层偏移仍按官方 `ActivityFestivalBossUtils.GetLevelOffset` 使用该层击破数。此等级基准是确认过的单机规则，不代表官方服务器算法。活动开启时间在本地首次打开时记录，`UnlockTimeOffset` 按官方的小时单位计算；线上时间基准来自活动服务器数据。助战说明中的 NPC 获取提示依赖服务器 `NavigateManager.Navigate`；单机保留原按钮并提示该导航目标不可用，不虚构 NPC 获取路线。

### 已确认的本地接线与边界

`prepare_official_rules.py` 导出 `stage_map_explore_boss`、`activity_festival_boss`、`activity_festival_boss_layer`、`activity_festival_boss_npc`，并更新运行时 `rules.json` 的数据哈希与显式绑定；APK 表、Luban 构造器和客户端／共用 C# 调用哈希见 `official-city/rules-manifest.json`。调用依据包括 `ExploreBossTeamDialog.OnConfirmBtnClicked`、`ActivityFestivalBossPanelBattleContent.StartBattle`、`ActivityFestivalBossUtils.GetLevelOffset`／`GetLayerBossLevelAndRank`、`PlayerActivityFestivalBossDataExt.UpdateActivityFestivalBossLayerData` 与 `RoadblockTeamPropStageTouchUI.HandleTouchBtnClicked`。单机流程由 、 和 接入；服务端缺失及经用户确认的本地选择／结算／等级规则见 [NPC邀请与战前准备](npc-assistance.md)。

## 尚未实现

完整骨骼分支已增加随包交付的本地资源目录与原生角色对象。`prepare_partner_native_battle_assets.py` 校验既有数据／Animator 凭据，再以 `PartnerNativeBattleAssetCatalog` 显式绑定35个骨骼包、35份原材质字段、28份运行时贴图和34个原控制器；原 shader 对象对应的 Windows 转换载荷按原字节安装至 `NativePartnerAssets/`。`PartnerNativeBattleActor` 恢复原 Transform 链、组合皮肤、材质参数与渲染设置，由原 Animator 驱动独立几何，并单独推进11005背饰；运行时只读取 Unity 本地对象引用。输入、输出、shader 对象及绑定元数据哈希见[本地绑定来源凭据](../../assets/reference/partner-native-battle-assets-source-manifest.json)。

内置屏 Direct3D Play Mode 通过上述本地目录逐一创建34名伙伴／35个渲染器，检查原主体节点位置／缩放／层、实际 Animator 片段、shader／贴图绑定和有限网格；每名各检查待机及一个原技能状态，68张实际运行截图已查看。26名显示原贴图；11015–11022 显示与当前24×24原纹理对应的白色轮廓，不计作完整外观恢复，仍需继续核对所选来源。正式工程导入／编译及 `ProjectTools.Validate`、静态检查（119,626个 GUID）通过，2,341项本地目录／数据／Animator 来源与输出哈希一致。该角色对象尚未接入正式战斗的候选资格、事件播放、状态特效及附着点；未完成正式邀请入口、完整实战或 Windows Player 验证，未生成完整素材快照。

完整骨骼分支的技能 Animator 节点已按当前版本单独恢复。`prepare_partner_native_battle_playables.py` 从选定884 APK的原 `npc_level_detail`、`npc_profession_detail`、`skill` 构造器读取34名伙伴的全部等级／阶位配置和主动技能，包含11038的随机技能列表，共75个技能、93个默认／方向分支和95个 Animator 节点。`Combat/partner-native-playables.json` 保留原节点顺序、延迟、方向分支、触发参数及其他参数字段；原 Sequence 没有可序列化的固定总时长，未编造持续时间。跟随骨骼／部位、音效、朝向和镜头等其他节点只记录原字段与来源，未据此宣称完整技能表现。配置构造器、组件模式、原对象载荷、应用源码和输出哈希见[伙伴技能节点来源](../../assets/reference/partner-native-playables-source-manifest.json)。

隔离内置屏 Direct3D Play Mode 使用生产 `OfficialBattleAnimatorPlayback`、迁移的原 `MyPlayableAnimator`／`AnimationController`、本地 `PartnerNativeBattleActor` 和34个原控制器，按原延迟逐一触发上述95个节点；93个分支全部进入原图中匹配触发条件的目标状态，网格坐标有限，播放器释放后恢复原控制器。93张实际运行截图已查看；正式工程重新导入／编译、`ProjectTools.Validate` 与静态检查（119,634个 GUID）通过，34项来源／安装身份哈希一致。该检查覆盖节点触发和早期姿态，不证明完整动作结束、其他技能效果、正式战斗事件接线或邀请入口；白色纹理伙伴仍沿前述未完成范围处理。

完整骨骼分支的数据与原贴图映射已恢复，战斗绑定仍未完成。`prepare_partner_native_battle.py` 按选定 `884_156692` APK／匹配云端依赖检查余下 34 个不含 `SpineLazyAnimLoad` 的实体，完整读取原 Spine binary，恢复 35 个骨骼渲染器和 791 段动画，包括 11005 的独立 `SkeletonAnimation` 背饰；保留 Atlas、原材质／MeshRenderer 参数、Animator 状态／别名、实体根至骨骼的 Transform 链及 BoneFollower／AttachmentFollower 字段，安装至 `unity/Assets/Official/Combat/NativePartners/`。独立 `ZJCSPNBT` 包由 `ProfessionNativeSkeletonData.LoadPartnerBattle` 校验版本、NPC 身份、解压长度与内容哈希；不与立绘包混用。原 11007 的 Transform `bone41.mixY=-0.4` 保留，显式战斗包沿用已验证的有限外推几何分支。

完整骨骼分支的原 Animator 资产已恢复。`prepare_partner_native_battle_animators.py` 复用固定原生转换缓存，复制34个原控制器和532个片段文件，仅重绑外部资产 GUID；逐项核对原编译资源的参数顺序／类型／默认值、层、默认状态、状态数值／动作、过渡顺序／目标／条件／时长／打断标记和片段时长。11004、11014 的原一维混合树及子节点阈值／速度／偏移／镜像保留。`PartnerNativeBattleAnimatorCatalog` 在 `Combat/NativePartnerAnimators/catalog` 显式绑定 NPC 与控制器，载荷位于 `unity/Assets/Official/Combat/NativePartnerAnimators/`；来源与输入输出哈希见[Animator来源凭据](../../assets/reference/partner-native-battle-animators-source-manifest.json)。

内置屏 Direct3D Play Mode 已检查34个控制器的539个状态、541个片段引用，两个一维混合树在参数0／0.5／1时的 Unity 权重符合原阈值。11001、11003、11005、11007、11009、11010、11042 由实际 Animator 状态时钟驱动独立骨骼求解，使用原贴图、材质、Transform 与背饰；已查看14张待机／技能运行截图。另按原 run 参数类型触发待机到奔跑，各采样30帧并检查有限网格，共210帧；这些原过渡为零时长，未产生混合帧。正式工程导入／编译、`ProjectTools.Validate` 与静态检查（119,316个 GUID）通过，Animator 凭据的1,750项输入／转换／输出哈希一致。原11033的 `200/die_02` 片段时长为0，而骨骼关键帧终点为0.333333343秒；两份原数据保留。生成器逐通道检查后确认：所有通道从0开始且数值恒定，唯一末帧在阶梯曲线后重复首帧旋转值。`ProfessionNativeAnimatorPose` 仅对该版本／NPC／动作及通过恒定通道检查的原数据保留零时钟采样，其余时长不匹配仍拒绝。内置屏 Direct3D Play Mode 按原 `immediately_die` Bool 保持死亡状态，比较0／0.1／0.333333343／1秒的全部骨骼世界矩阵；4张运行截图已查看且文件内容一致，另以错误绑定的零时长待机片段验证拒绝分支。正式战斗与 Windows Player 尚未验证该动作。完整34名伙伴的正式战斗候选／加载、其余原材质与完整战斗流程，以及 Windows Player 发布验证均未完成。

`ProfessionNativeAnimatorPose` 通过显式伙伴分支消费11004、11014的原两叶待机混合树；由 Unity 提供片段顺序、状态时钟和互补权重，端点排除零权重叶，中间权重按原 autoReset／MixNext／Replace 模式先混合局部通道，再按原顺序求解约束。依据[4.2公开混合说明](https://esotericsoftware.com/spine-unity-main-components-4.2#Animation-Blending-Control)，未复制动画 SDK；默认调用仍拒绝未绑定的混合树。内置屏 Direct3D Play Mode 在原参数0／0.25／0.5／0.75／1采样两名伙伴：端点逐骨骼比较完整世界矩阵，中间检查102项原局部位置插值，10张实际运行画面已查看。正式工程导入／编译与 `ProjectTools.Validate` 通过，静态检查通过（119,316个 GUID），1,750项 Animator 来源／输出哈希一致。此项是隔离动画检查，尚未验证正式战斗与 Windows Player。Animator 来源清单按稳定路径排序，恢复的566项原控制器／片段资产内容保持一致。

`PartnerNativeBattleAttachments` 从选定884 APK的34份原实体 prefab 恢复1,085个 Transform／RectTransform 节点，保留父子与同级顺序、坐标、旋转、缩放、激活、Layer 和原矩形锚点／尺寸／轴心；复用原 `AttachAgent` 的888条注册记录，包括原名称顺序、同节点别名和空引用，不重新扫描生成另一份注册表。35个原骨骼组件显式绑定到既有独立姿态接口，34个阴影节点直接运行迁移的官方应用类 `AttachmentFollower`，沿原渲染器引用、shadow 骨骼与跟随字段更新。四份 SDK `BoneFollower` 仅保存原序列化配置，尚未迁移运行行为；其他记录中的 prefab HUD、SpriteRenderer 和特效组件也未由该挂点模块重建，不能将恢复节点层级视为完整 prefab 表现。原对象／组件模式、应用源码、转换与本地绑定哈希见[挂点来源](../../assets/reference/partner-native-attachments-source-manifest.json)。

内置屏 Direct3D Play Mode 核对34名伙伴全部节点、注册项及矩形字段；站立与技能共2,720次阴影姿态采样通过，并检查整体位移／旋转／反射后跟随及原 `AttachPosRotCfg` 的挂接偏移。该挂接检查调用原应用方法，不是正式战斗输入验证。34张实际运行画面已查看，11015–11022保留原贴图现状。追加运行75个技能、93个方向分支的95个原 Animator 节点均通过。正式工程 Unity 导入／编译及 `ProjectTools.Validate` 通过，静态检查通过（120,456个 GUID）。完整技能特效、邀请实战与 Windows Player 验证仍待完成。

`PartnerNativeBattleAnimationBinding` 从选定884的34份原实体读取应用层 `AnimationController` 与 Animator 指针，显式绑定到原实体根节点，复用原控制器代码。保留 `InitOnAwake=true`、`CreateOverrideController=true`、`NotCreateGraph=false`、禁用时不重置初始化状态及原启用字段；实例暂存为非激活后再恢复激活，使原 Awake 在引用就绪后运行。释放时仅清理本次组件与覆盖控制器，恢复原 Animator 引用，不销毁实体节点。该绑定由调用方显式创建，既有独立姿态夹具不自动改变控制器生命周期。原组件对象、组件模式、应用源码和安装载荷哈希见[伙伴应用动画绑定来源](../../assets/reference/partner-native-animation-bindings-source-manifest.json)。

隔离内置屏 Direct3D Play Mode 在原实体根控制器上运行34名伙伴的93个技能分支、95个原 `MyPlayableAnimator` 节点，全部进入匹配触发器的原目标状态；原 Awake 初始化、禁用／再启用、覆盖控制器独立创建、释放后原引用恢复与组件清理均通过。93张重新隔离取景的实际运行截图已查看，沿用11015–11022的原24×24贴图现状。正式工程 Unity 导入／编译与 `ProjectTools.Validate` 通过，静态检查通过（120,460个 GUID），14项文件身份与68个原组件对象记录完整。该夹具直接播放原节点，只证明应用动画绑定与早期姿态，不证明完整技能特效图、正式邀请实战或 Windows Player。

伙伴完整技能图复用已转换的75个原技能实体、93份方向／默认 prefab。`PartnerNativeSkillSkeleton` 在生产 `OfficialBattleSkillGraphPlayback` 的实例工厂中，于 Awake 前为58550内嵌的原4.2伙伴骨骼绑定既有独立几何端口；保留原模型与单个分离渲染子节点、`100/skill_01_2` 循环、原 Update 时序、停用不清空动画状态及原 shadow 附着。该模型与11007原实体使用同一骨骼／材质对象，按原 `SkeletonRendererHighTexturePreview` 的高清加载引用绑定2048×668原贴图；其他原技能特效继续沿现有路径加载。`PartnerNativeBattleMesh` 复用原角色的网格、材质字段与渲染设置操作，不读取或重建 Spine SDK 算法。原 prefab 组件、加载器、贴图对象／像素、应用类来源及当前绑定输入输出哈希见[内嵌骨骼来源凭据](../../assets/reference/partner-native-skill-skeletons-source-manifest.json)。

隔离内置屏 Direct3D Play Mode 经生产技能调度器、原技能实体视图、对象池、原相机与完整 prefab 图运行全部93个分支；逐帧核对已初始化原节点的应用组件，58550另检查原骨骼身份、高清页、现有分离子节点和旧组件替换，未出现原 atlas 缺失或附着类型异常。每分支运行至少2秒并覆盖最后一个已知 Animator 节点之后0.5秒，共279张实际运行截图已查看；高清贴图导入后的RGBA哈希、尺寸、mip、颜色空间、过滤／Wrap及原不可读设置一致。正式工程导入／编译、`ProjectTools.Validate` 和静态检查（120,482个 GUID）通过，17项输入和4项安装文件身份一致。该夹具保持技能实体至检查结束，不合成玩法时长或结算伤害；没有验证自然播放完成、实际声音、正式邀请鼠标流程或 Windows Player；11015–11022现按[已确认联动范围](offline-access.md#暂不开放的联动活动及伙伴)不开放并保留原资源。

伙伴角色视图已接入公共战斗场景：`OfficialBattlePartnerNativeModelView` 使用选定884的原实体、原 Animator／应用控制器和挂接注册表，复用生产技能图作用域；移动读取原角色的移动技能／行为节点，完整技能图继续控制原 Animator，不叠加别名动画。`prepare_partner_native_role_views.py` 解码34份原 `ECViewDriver` 的 `ECFightRoleCharacterView`，核对共享死亡／开局死亡 prefab 的原对象与载荷哈希，并恢复10608／10708引用的原生成序列。死亡状态按原无限期序列保留，复活停止序列，读档使用原开局死亡分支并恢复格点。11008原控制器与骨骼没有死亡动画和 `die`／`immediately_die` 参数；保留通用死亡视图的原引用及其警告行为，不新增参数或动画。原字段、组件模式、共享图来源与显式代码绑定见[角色视图来源](../../assets/reference/partner-native-role-views-source-manifest.json)。

`prepare_partner_native_shadows.py` 沿原实体提取34份完整阴影子树及共用 Sprite／贴图，保留原 Transform、SpriteRenderer、附着／骨骼跟随字段，复用公共阴影转换与既有独立跟随实现。场景用原阴影替换挂接图中的空节点，同时重绑原 `AttachAgent` 注册表；角色网格与阴影沿公共战斗排序。输入对象、转换缓存与安装载荷哈希见[伙伴阴影来源](../../assets/reference/partner-native-shadows-source-manifest.json)。这些载荷尚未纳入完整素材快照。

原生伙伴血条复用选定884的完整 `HpBarGame` 子树、4个 `SlicedFilledRender`、原 `HpSliderEffect`／`BattleHpSpriteSlider` 与 `GamePlayManagerConfig`；34份原 prefab 共67项资源（8张原纹理、3份完整Windows shader）按原字段、层级、坐标和引用加载。`OfficialBattlePartnerHpView` 从公共战斗快照同步HP、最后最大生命与持久护盾，保留原 `BattleHpAndShieldHandle.RefreshShieldAndHp` 的上下限分支；原血条代码保留UniTask逐帧缓动、取消、回血亮边／顶端动画和阵营色，特效复用原对象池及预建数量。原 `TransformLocker` 的旋转／缩放更新由原生视图tick消费；替换原空血条分支后同步原挂点引用。阵营排序沿原直接／默认／军团分支，当前范围为已确认的本地主角与伙伴队伍，不包含在线观战。

内置屏Direct3D Play Mode已核对26名开放原生伙伴的满血初始化、扣血尾条、回血特效、持久护盾低于／超过最大生命、护盾移除、翻转／旋转锁定、移动、死亡血条归零、复活及死亡存档恢复；130张站立／治疗／护盾／移动／死亡运行截图已逐组查看。该检查直接构造真实公共场景与HP状态，未验证正式邀请鼠标流程、其余角色完整血条、名称／状态分支、实际声音或Windows Player。整工程导入／编译与 `ProjectTools.Validate` 通过，静态检查通过（123,667个GUID）。入口为 `OfficialPartnerNativeRoleSceneProbe`，来源与接线见[原生血条凭据](../../assets/reference/battle-animation/partner-native-hp-bars-source-manifest.json)。

伙伴社交编号与战斗实体编号分别保存：新增 `FightBattleUnitState.RoleEntityId`，创建伙伴时从原 `NpcInfo.FightEntityId` 绑定角色实体及其 `FightRoleCharacterComponent.CharacterType`；移动规则、场景实体和血条查询优先使用该实体，避免把11001等社交编号当成战斗实体或把伙伴设成主角类型。恢复旧 checkpoint 时重新核对并补全此字段。95名原 Team 候选的身份／移动配置、旧字段补全、十名额外角色属性技能及既有伙伴结果一致性规则检查通过，其中11004保留原15320／15321移动分支。

隔离内置屏 Direct3D Play Mode 经公共场景实际创建26名当前开放伙伴，核对原阴影、原移动参数与动画、移动终点、死亡／复活及死亡 checkpoint 的原序列和位置恢复，78张站立／移动／死亡运行截图已查看。正式工程导入／编译与 `ProjectTools.Validate` 通过，静态检查通过（123,495个 GUID）。11015–11022按[已确认联动范围](offline-access.md#暂不开放的联动活动及伙伴)不开放，原资源保留。该检查直接构造场景与状态，不代表正式邀请鼠标流程、完整血条／状态表现、实战结算、声音或 Windows Player 已验证；这些仍须继续完成。

原生伙伴宿主集成现使用11001／11004／11005与本地主角进入实际日常副本场景；夹具按原 FestivalBoss 的3人上限，以独立测试入口保存选择和提交记录，检查 checkpoint 暂停／恢复、调用方选项修改隔离、错误玩法恢复拒绝且原事务不变，模拟公共HUD输入后完成37步胜利，再核对助战清理和主城状态恢复。断言包含三名伙伴的原 Transform 链、Animator、实际网格与阴影，以及本场3206／3207的原挂点表；原16010技能不再发生停止挂接异常，怪物迁移边界见[战斗执行](battle-execution.md)。准备、播放和返回运行截图已查看，伙伴模型和阴影可见；当前34份原生伙伴的血条已沿原 prefab 接入；补跑三名伙伴的实际助战，原血条组件、4层渲染器、战斗完成与助战清理断言通过，准备／播放／返回3张运行画面已查看。其余伙伴的完整血条迁移，以及主城既有黑白几何／白块等显示问题仍未完成。该夹具直接建立测试选择，不证明正式邀请窗口资格／选中／确认的完整鼠标流程、活动正式入口或奖励、自然技能收尾全覆盖、实际声音及Windows Player发布。26名开放原生伙伴的站立／移动／死亡／复活与恢复死亡回归再次通过，78张运行帧已查看。本次整工程导入／编译、`ProjectTools.Validate` 和静态检查通过（123,549个GUID）。专用批处理验收入口在验证前关闭本进程自动资源刷新，显式启用宿主／助战检查与结束退出开关，以保持与其他会话并行写入时的编译快照；不修改全局编辑器设置。更新后的整工程导入／编译、`ProjectTools.Validate` 和三伙伴37步胜利复跑均通过，批处理正常退出码为0。

贴图沿原 `AppearanceRendererLite.SkeletonRenderer` 指针、`AppearanceConfig` 骨骼路径和 `AppearanceAddressUtils.GetTextureLoaderPath` 恢复。35 个渲染器均使用原 Low 分辨率；原材质的占位页与运行时 `TextureLoader_Low` 的实际目标分别保存，35 项映射对应 28 份不同的目标 PNG。11015–11022 的原 Low 目标为 24×24，当前原 High 目标也为该尺寸，未以其他图片替换。隔离 Unity 工程复用正式工程的 Linear 颜色空间；全部目标导入后的 RGBA 像素哈希、尺寸、mip、颜色空间、过滤／各轴 Wrap 参数一致，并恢复原可读性。

`ProfessionNativeOrderedConstraintPose` 按原 order 交错求解 IK 与 Transform，保留前序约束结果；`ProfessionNativeAffineIk` 使用独立平面几何处理单骨骼、双骨骼的圆形／非均匀缩放椭圆及反射坐标，保留原混合、弯曲、伸展和适用的柔化分支。非均匀缩放时子骨骼 Y 归零且不伸展，依据[公开 IK 约束说明](https://esotericsoftware.com/spine-ik-constraints)；该分支的非零 softness 仍显式拒绝，当前相关原配置和动画均为零。恢复原 noScaleOrReflection 继承、11042 的相对局部旋转；原非凸裁剪边界通过独立三角剖分接入既有半平面裁剪，保持原三角形朝向与插值 UV，不复制动画 SDK。

`ProfessionNativePathAnimation` 采样原 path position／spacing／mix 的线性、阶梯和 Bezier 曲线，MixNext 只替换已开始的 incoming 通道，未设置通道保留 outgoing 状态。11009 的5条单骨骼路径和11010 的30节骨骼链按原 order 接入；保留可见目标槽位、原多段三次曲线控制点、当前变形、长度间距、百分比位置及 tangent／chain 旋转，用独立数值积分与弧长反求处理恒速路径，两端按原切线外延。chain 的零旋转偏移保持相邻骨骼首尾连接，依据[公开路径约束说明](https://esotericsoftware.com/spine-path-constraints)。其他闭合、非恒速、间距／旋转模式仍显式拒绝，当前这34个实体没有使用这些分支。

隔离 Unity 编译及数据检查通过：35 个包的原动画名一致，错误 NPC 身份全部拒绝；对 791 段动画的起点、中点、终点、全部原关键帧及相邻关键帧中点构建网格，共 38,457 个姿态采样，791 段全部通过。另通过已知 FK 端点的非均匀缩放／反射双骨骼 IK、前序约束保留、单骨骼伸展、选择继承、非凸裁剪面积／重心／UV，以及解析可知的三次曲线长度／中点／切线、多段路径与两端外延、原阶梯位置关键帧、incoming 路径混合／未设置通道保留和30节链连接检查。正式工程 Unity 导入／编译与 `ProjectTools.Validate`、静态检查（118,404 个 GUID）通过，367 项源码／安装文件哈希及 337 项安装元数据一致。

内置屏 Direct3D Play Mode 使用当前原 shader 对象哈希完全匹配的 Windows 转换缓存，恢复原材质参数、Low 贴图、MeshRenderer 和 Transform 字段，检查 11001、11003、11005、11007、11009、11010、11042 的原 idle 与技能动作；11010 取原 skill_02 路径动作，两名路径角色另检查技能早期的0.1／0.2／0.3进度，共48张实际渲染画面已查看。11005 的独立背饰、11009 路径移动的木条和11010 的火焰长带均可见；截图夹具按非零透明度顶点取景，保留原完整网格与颜色。该隔离夹具直接采样动画，尚未证明正式 Animator、战斗集成或 Windows Player；34 个伙伴尚未加入正式战斗候选。输入对象、原贴图加载配置、shader 转换凭据、解码器、转换及安装载荷／元数据哈希见[完整骨骼战斗数据来源](../../assets/reference/partner-native-battle-source-manifest.json)。未生成完整素材快照。

- 素材秘境（2）、道路属性门槛（1）、探索首领（All=10）、节庆首领（5）、联合行动（4）各自的实际选择入口、条件筛选、官方属性排序／门槛、站位、战斗加入、快照提交及结算接线；不能用日常副本的 All 规则替代这些玩法。
- 羁绊冒险（6）的 NPC 战斗邀请入口和战斗接线；NPC 旅行是派遣系统，不等同战斗助战。
- NPC 战斗模型／动画加载绑定仍未完整。85 个官方伙伴战斗包按当前选定 `884_156692` 检查后，51 个实体沿原 `SkeletonMecanim` 所属 GameObject 匹配 Animator（10111、10503、10507 的对象名使用 NPC ID）；沿原 Spine binary、Atlas、材质页和 `AppearanceCombineSkin` 恢复骨骼、贴图、原 `CombineSkins` 组合与动画别名，安装至 `unity/Assets/Official/Combat/NpcFighters/`，由 `OfficialPartnerBattleFighterTools` 导入 `OfficialBattleModelCatalog` 的伙伴绑定。运行时沿原 `AppearanceCombineSkin.ApplyCombineSkinInRuntime` 合并实际存在的所有组合皮肤并重置槽位；缺失的可选皮肤仍按原代码跳过。另 34 个实体使用完整骨骼分支，其中部分不使用 `AppearanceCombineSkin`；原数据已恢复，约束、材质和播放接线边界见本节开头，不认作官方资源缺失。51 个实体的转换、Unity 编译及模型导入检查均已通过。战斗候选只放行 `OfficialBattleModelCatalog.HasNpcBattleAnimations` 确认具备 `idle_01`、`run_01` 的伙伴。`SkillPlayModeAcceptance.RunPartnerBattleModelAcceptance` 与合作战斗宿主验收会选择 8402 并断言伙伴进入保存的战斗 checkpoint；内置屏 GUI Play Mode 已验证 8402 进入 checkpoint、已有快照恢复、错误玩法拒绝、78 步真实战斗胜利、助战清理和宿主恢复，模拟输入入口通过。51 个伙伴同时提取官方 prefab 从实体根到骨骼节点的完整 Transform 链。骨骼保留原 SkeletonDataAsset 单位换算，独立战斗实体根负责格子移动和血条，子节点恢复原位置、旋转和缩放；4202 的 PlayerModel_Game 使用原 scale=0.23、局部 Y=-0.5，未把骨骼 scale=0.01 再乘入实体根。内置屏 Play Mode 重跑逐节点核对原变换、骨骼数据 scale 和实际网格，并再次完成 78 步胜利及恢复／清理；已查看准备和战斗截图，8402 身体、武器和血条正常可见。这只覆盖该伙伴及宿主夹具，其余伙伴与正式邀请入口仍需完整验证。当前仓库静态检查已通过（115,583 个 GUID），Unity 编译、51 个模型导入及 `ProjectTools.Validate` 均通过。APK 对象、匹配云端页资源、转换输入／输出及运行绑定哈希、跳过原因见[伙伴战斗模型来源凭据](../../assets/reference/partner-battle-fighters-source-manifest.json)。
- 将用户确认的等级／阶位规则扩展到其余通用助战玩法；当前日常战斗按玩家等级与存档 `SubRank` 创建伙伴。
- 非空伙伴队伍的正式入口、人数限制、战斗表现、次数保存恢复及失败回滚仍需完整 Unity Play Mode 验证。公共选择视图已在内置屏 Direct3D 11 Play Mode 通过20名夹具候选的原 Toggle 选择／取消、不可选条目、原确认／关闭按钮模拟鼠标输入、列表20→2→20的复用和释放，并检查弹窗位于宿主及其独立排序子窗口之上；该独立检查不证明实际NPC规则、画像、次数事务或正式玩法已完成。

## 代码与资源入口

官方实现：`.tools/source-packages/official-code/common-source/Common/NpcFightData.cs` (`CalcPropsWithSkill`)、`.tools/source-packages/official-code/common-source/Common/NpcLevelDetailInfoParser.cs` (`Get`)、`.tools/source-packages/official-code/client-source/NpcUtils.cs` (`IsNpcAssistLimit`)、`.tools/source-packages/official-code/client-source/TeamInviteNpc.cs` (`GetAssistNpcActorTargetDatas`)、`.tools/source-packages/official-code/client-source/TeamInvitePanel.cs` (NPC 邀请页)、`.tools/source-packages/official-code/common-source/PlayerInviteInfoManager.cs` (助战候选资料请求)、`.tools/source-packages/official-code/client-source/NpcAdventurePanelMainContent.cs` (伙伴冒险等级同步)、`.tools/source-packages/official-code/client-source/TeamInvitePanelPrivateMap.cs` 与 `TeamInviteNPCNode.cs` (剧情伙伴等级)、`.tools/source-packages/official-code/common-source/PlayerBriefInfoUtils.cs` (跨赛季数据校正)。客户端代码来自旧版固定 APK `871_156598`；当前伙伴候选按新版 `871_156598` 的 `NpcType=4`、`CanDevelop`、战斗实体和等级详情识别。

配置与素材：[NPC 助战来源清单](../../assets/reference/npc-assistance/source-manifest.json)、[规则表来源清单](../../assets/reference/official-city/rules-manifest.json)、`assets/official/npc-assistance-ui.json`。通用邀请素材来自固定 APK `defaultpackage_assets_prefabs_ui_team.bundle`；固定包的提取记录以来源清单为准。

原生伙伴附着的编辑器校验显式引用外观跟随端口和剧情播放程序集，以解析原附着组件使用的类型；动画绑定校验另外显式引用原剧情动画控制程序集。

Unity 当前实现：规则目录 `unity/Assets/Game/Runtime/Official/Combat/FightOfficialNpcAssistanceCatalog.cs`、次数与战斗快照 `unity/Assets/Game/Runtime/Offline/World/OfflineNpcAssistanceProgress.cs`、官方 Team picker `unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.DailyChallenges.cs`、战斗接线 `unity/Assets/Game/Runtime/Presentation/Home/DailyDungeonBattleRunner.cs`。普通日常副本入口位于 `HomeHudView.Dungeons.SelectDailyDungeonRoom`；其余玩法入口未接线。TeamInvitePanel 的 bundle 输入、45 项官方依赖、6 个运行时 prefab 与 52 项共享绑定记录见 [team-ui-source-manifest.json](../../assets/reference/npc-assistance/team-ui-source-manifest.json)。
