# 战斗执行

[总索引](../README.md)

状态：未完成

## 功能简介

本页记录 Unity 共用战斗规则、格网执行和公共 HUD 的实际迁移状态。范围为各玩法和地图共用的战斗部分，不包含副本、活动等模式各自的入口、专属规则与结算页。当前已有共用阶段机、回合与格网执行器、离线技能／状态结算、倍速控制、存档恢复及一部分官方 HUD、模型、动画和场景几何接入；`OfficialMap10GameRuntime.BeginBattle` 已调用控制器，玩家技能与被动属性从存档装配，跨波次选敌已有回归。准备与结束阶段接线、按 Playable 分帧结算、官方镜头与完整角色外观、环境粒子／Spine、状态生命周期分支及多个 HUD 仍未完成，故通用战斗尚不能视为完整还原。开场剧情的 `OpeningFightPlayback` 是记录播放，不替代通用战斗运行时。

### 原始官方功能与证据范围

下面按行为列出原版通用战斗功能，供迁移与逐项核对使用，不表示这些功能已经接入 Unity。依据为固定版本 `854_156383` 的[客户端反编译源码](../../.tools/source-packages/official-code/client-source/)、[共享组件与配置类型](../../.tools/source-packages/official-code/common-source/Common/)，以及已提取的 [rules.json](../../assets/official/rules.json)、[battle-rules.json](../../assets/official/battle-rules.json)、[skill-rules.json](../../assets/official/skill-rules.json)。表中类名均相对前述源码根目录；具体技能、命中图、状态与单位 ID 沿用目录数据，不以少量样例替代全量配置。

客户端可以证明面板、请求、响应消费、数据结构与表现流程；共享组件中声明的配置不能单独证明完整服务端决策已取得。单机确认规则见“自定义实现”，尚需还原或核对的部分见“尚未实现”。特定玩法控件仅在其原显示条件下存在。

### 进入、准备、回合与结束

| 编号 | 原始官方功能 | 官方入口、条件与行为 |
| --- | --- | --- |
| F01 | 战斗建立、场景加载与释放 | `FightRoot`、`FightSceneRoot`、`GamePlayManager` 建立会话、加载／激活／停用／释放战斗场景。`FightSceneRoot` 使用 `Assets/Prefabs/EcEntity/Fight/FightStage/{sceneID}/{game_scene}`，不能用地图上生成几个单位代替原场景。 |
| F02 | 模式面板与延迟显示 | `FightMainPanelLoader.Load/GetPrefabGuid/Unload` 根据记录战斗、普通战斗、录像回放选择面板，消费 `StageInfo.ShowMainPanelDelay`，取消时停止延迟加载并释放实例；`FightMainPanelExtBehaviour` 可覆盖公共显示／结束行为。 |
| F03 | 战斗阶段与阶段事件 | `GameStatus` 定义 Preparing、PlayStart、Play、Pause、EntryTransition、EntryTransitionFinished、BattleEnd、BattleEndTransition、BattleEndTransitionFinished、End。各阶段驱动 UI、生成、镜头、时标和清理；这是状态集合，不假定每种模式都按枚举顺序经过全部状态。 |
| F04 | 准备、就绪与自动开始倒计时 | `FightMainPanelReadyUI`、`GameTeamReadyCountDownUI`、`FightMainPanel.RefreshPrepareAutoStartUI` 消费就绪状态、准备截止时间、自动就绪开关与打断操作；`StageInfo.SkipPrepareStatus/UserStart` 等控制阶段入口。团队请求的本地替代须服从所属页方案。 |
| F05 | 波次、入场与回合推进 | 波次事件通知技能栏等订阅者刷新；`StageInfo.MaxRound/IsPlayerRoundLimit/PlayerRoundLimit` 和 `GameProgressUI` 区分全局回合、个人回合及限制，临近上限改变颜色并播放倒计时。行动队列和波次生命周期必须与入场、死亡和技能结束一致。 |
| F06 | 胜负与结束时序 | `MyFightResult`、`MyFightResultSucMonsterDie`、`MyFightResultFailedPlayerDie/FailedGuard/FailedPlayerDuel` 等对应不同结果组件；根据阶段／模式判定，不一律简化为任意敌人或玩家 HP 归零。公共层输出结果、完成演出和清理，奖励、下一关、剧情分支由所属玩法接收。 |
| F07 | 退出与切到后台 | `FightMainPanelLeaveHandler` 为录像、观战及各玩法选择处理器；`DefaultFightMainPanelLeaveHandler` 区分战斗中、队长／成员及确认提示。`FightMainPanelSwitchOutBtnUI` 按结果能否立即离开，否则请求后台切出；`StageInfo.LeaveInPreparing/LeaveInPlaying/Background` 控制可用性，切出不等同结束。 |

### 站位、格网、移动与目标

| 编号 | 原始官方功能 | 官方入口、条件与行为 |
| --- | --- | --- |
| F08 | 出生阵型与地形 | `FightSceneInfo`、`MyFightGridGround`、`MyFightRoleGrid`、`GridMoverComponentInfo` 定义场景坐标、地面／高度、身体占格、通行与边阻挡、出生偏移、方向与翻转。`WalkableGroundTypeKey/WalkableGroundTypeServerOverrideKey/MoveMask` 按单位消费，不能合并成所有单位通行的掩码。 |
| F09 | 准备阶段拖拽站位 | [`PrepareBattlePlayerDrag`](../../.tools/source-packages/official-code/client-source/PrepareBattlePlayerDrag.cs) 与 `PrepareBattlePlayerDragBase` 要求 Preparing、允许改站位、同阵营等条件；非匹配情况下还检查队伍／助战，自己已就绪与操纵他人有独立权限。拾起／放下 Playable、拖拽阈值、鼠标到格网、边界／占位检查、最近合法格、失败回退均有原实现，不是无限制任意拖动。 |
| F10 | 可调整区域与本队／NPC 控制 | `ChangeBattlePosHandler` 按 `TeamChangePosRange`、阵营 `EntryPosList`、队伍偏移或副本中心确定范围，偶数尺寸有半格中心修正；仅准备且允许时显示范围。可控自己、所属 NPC、匹配 NPC 或辅助机器人等分支影响占位交换；普通队友不自动获得控制权。原请求区分队伍站位与本场偏移。 |
| F11 | 特殊中途加入站位 | `ChangePosHandler` 按 `Stage.JoinInMiddle` 选择普通或灵魂站位工具；`PrepareBattlePlayerSoulDrag` 在允许观战加入时预览、选择合法位置并提交偏移。此为模式功能，不能直接启用为单机通用拖拽。 |
| F12 | 寻路、移动与位移 | 官方 `AStarUtils`、`MyFightGridMover`、`MyFightActionSkillMove` 消费 MoveDist、通行掩码、占位、逐段路径、持续时间、朝向和移动 Playable；`FightSkillComponentInfo.SourceMoveList` 按 Delay 排序，还包含技能自身位移。传送、保持距离、固定方向等受技能 AI 配置控制。 |
| F13 | 自动选择目标与施法范围 | `FightSkillComponentInfo.Range` 明确从近到远排列，近战范围包含四向；`HitTargetType` 包含自己、敌人、友方、非自身友方、所有、无目标、主人、自身召唤物、其他真人友方、玩家与 NPC 友方。`SkillTargetType` 区分点、实体、可站立点、技能方向可站立点、跟随实体与锁定实体；`HitTargetActionType` 与 `state_mutex` 现共同过滤技能施放者和命中候选，包括状态触发的显式目标，只有矩阵标记 `Discard` 的现有行动状态会阻止该动作。 |
| F14 | AI 优先级、嘲讽与混乱 | 固定 `rules.skill.AiPriorityId/LastAiPriorityId` 关联 `rules.ai_priority` 的有序策略组；技能实体另配属性最值、角色类别、角色类型分组、忽略状态及 `ReleaseWhileRidicule/ReleaseWhileConfusion`，状态 `ActionComponent.ActionType` 标记控制类型，`MonsterInfo` 提供单位 `CharacterType/AiType`。客户端 `MyFightAI` 为空组件包装、共享 `FightAIComponent` 仅保存 `AiType`，未取得服务端策略评分器；确定性筛选、已解释比较与用户确认的控制状态模拟已在本地接入，剩余策略、评分公式和权重列为未实现。 |
| F15 | 点击单位查看详情 | `FightRoleClickInput` 及玩家／NPC／怪物／宠物等派生类先校验命中和查询条件，再打开对应信息或定制回调。它与自动选敌不同；没有把点击模型或常规技能图标解释为通用手动指定攻击目标。 |

### 技能、命中与状态生命周期

| 编号 | 原始官方功能 | 官方入口、条件与行为 |
| --- | --- | --- |
| F16 | 实际技能列表与属性快照 | `FightSkillListDataPlayerProvider/FightSkillListDataMonsterProvider` 提供玩家装备方案、怪物技能、等级／品阶、技能属性、CD 和事件。职业／宠物限制、被动与来源属性须由实际存档／单位配置装配，不以空列表、统一默认值代替。 |
| F17 | 消耗、CD 与释放次数 | `FightSkillData.CD`、`FightSkillComponentInfo.CostType/MustHaveValidCost/ResetCDAtStart/LimitedTimes/ResetReleasedTimeOnNextWave` 约束可释放性、开场 CD、每场次数及换波重置。`FightStatusSkillModifyCD/SkillPermanentReduceCd/SkillStopCd` 等还会修改冷却；必须保留触发条件和更新时点。 |
| F18 | 技能动作与结束条件 | `MyFightSkillAgent` 发送开始／结束事件；`FightSkillComponentInfo` 分开配置 ActionDuration、Duration，以及等待 Hit/Move 或全部 Hit/Move/Damage/child Skill 完成后的时间。负值可代表持续不自动结束，不能只用固定间隔整回合瞬间结算。 |
| F19 | 子技能、重放与触发限制 | `FightSkillReplayCfg`、`ReplayMaxCountEachRound`、`FightSkillExtSkill`、`CanTriggerChild/CanTriggerStatusEntityIds` 控制重放、额外施放与子状态白名单。旧 `ExtSkill` 字段标废弃并指向 `FightSkillExtSkill`；主动槽检查、重放和被动触发须分开计数。 |
| F20 | 命中图、延时与目标锚点 | `HitCfg` 和 `MyFightHitBase` 保留 ClassId、Delay、作用锚点、目标跟随、范围格与 Playable；`MyFightHitFixed/NormalNext/Chained/RandomTarget/CustomTarget/TriggerSkill` 覆盖固定、后继、链式、随机、自定目标与触发技能命中。`IgnoreAICount`、延时覆盖、命中时临时属性及吸血等附加组件按实体消费。 |
| F21 | 伤害、治疗与命中反馈 | `FightDamageComponentInfo`、公式与命中结算保留属性／等级／位阶／场景修正、暴击、格挡、闪避、免疫及伤害衰减等原分支；`MyFightDamage` 驱动来源命中与目标受击、通用／普通／暴击／格挡／触发状态 Playable，并有零伤害与隐藏飘字配置。技能、被动来源和实际 HP、治疗、护盾结算分别记录。 |
| F22 | 状态装配、持续与叠加 | [`FightStatusComponentInfo`](../../.tools/source-packages/official-code/common-source/Common/FightStatusComponentInfo.cs) 明确回合跟随对象、开始／结束更新点、持续回合（-1 无限）、跟随对象销毁移除、同状态平行／新顶旧／旧顶新、可驱散、层数／随机步距／最大层数、叠时／上限、同来源条件、叠层重置时长、层数缩放属性及固定属性快照。死亡禁用／复活重启、韧性门槛和行为限制也不能遗漏。 |
| F23 | 状态事件与条件触发 | `FightStatusOnStart/OnEnd`、`RoundStart/RoundBeforeEnd/RoundEnd/GlobalRoundUpdate/RoundPeriodic/RoundInterval`、`ActionStart/ActionEnd`、`SkillStart/SkillEnd`、`Hit*`、`RoleDie/KillSkill`、`StackChange` 等组件按事件触发；另有属性／技能／状态条件、状态转移／移除／持续变化、伤害累计和自定义伤害。实际组件参数和触发来源必须一同恢复，不能统一在回合末处理。 |
| F24 | 控制、属性、行动条与防护 | `FightStatusProp*`、`FightStatusLogicComponentInfo`、行动／状态禁止配置、HP 条件／限制、CD／重放组件及护盾组件共同改变可行动作、属性和结算；`FightStatusShield*` 区分普通、持续、一次性和无敌等类型。`FightRoleResilienceInfo` 还配置破韧状态、技能、伤害比例／上限与个人回合恢复；`MonsterGroup.HpPliesId` 经固定版 `MonsterHpPliesInfoParser.TryGetInfo` 按等级装配到通用单位快照。破韧／恢复与关联技能链仍不完整。 |
| F25 | 召唤与附属单位生命周期 | `FightHitSummonComponentInfo`、`FightHitSummonMonsterCfg/PetCfg/GridItemCfg/SmartGridItemCfg/VoodooDollCfg`、`FightStatusHitSummon` 与角色生成组件保留召唤类型、来源／主人、生成、计数及销毁关系；死亡与复活、来源结束后的状态／附属对象处理按实际组件，不能把所有召唤物当普通下一波怪物。 |

### HUD、详情与公共控件

| 编号 | 原始官方功能 | 官方入口、条件与行为 |
| --- | --- | --- |
| F26 | 进度与行动时间轴 | `GameProgressUI` 显示个人／全局回合、上限和预警；`GameRoundUI` 按 NextTime 和间距移动队列，`GameRoundCharacterItem` 恢复玩家／NPC／怪物头像、阵营、主角色标识、受击／死亡／待机／行动动画与移除时序。 |
| F27 | 单位名称、HP 与死亡标识 | `GamePlayerHUD` 负责玩家／NPC 名称与信息绑定；生命显示分别由 `GameCharacterHpUI`、`FightRoleFightHpView` 等消费，`GamePlayerHpStatus` 呈现相关状态。怪物 World HUD 已绑定官方单位血条、韧性、状态及持久护盾；护盾沿客户端 `BattleHpAndShieldHandle` 规则叠加到 HP 显示值，并在总量超过 `LastMaxHp` 时扩展显示上限。玩家 `PlayerBattleHpBar` 的官方脚本已定位，但其 prefab／运行时绑定尚未提取并接入。名称 HUD 不是完整血量实现。 |
| F28 | Boss 栏、护盾、多段血量与韧性 | `FightMainPanelBossUI.CheckShowBossCondition` 选存活、为 Boss、位于敌方视野内的怪物，最多同时显示两个并动态替换；`FightMainPanelBossUIItem` 绑定名称、头像、状态、HP／盾与韧性。`FightBossHpAndShieldBar` 根据多条血／盾组合切换填充和平滑表现；`MyFightRoleResilience` 按比值显示，最大值为0时隐藏。离线 Boss 栏已接入官方每层 HP 填充、剩余层数、持续护盾覆盖与韧性；各层 HP／盾 Sprite／颜色序列及平滑过渡仍未完整恢复。 |
| F29 | 单位状态图标与详情 | `FightStatusController` 池化展示状态，订阅应用、结束、禁用、层数、回合与自定义计数变化，并限制图标排布、提供详情。图标、叠层数字、持续回合、禁用态与移除必须对应真实状态实例。 |
| F30 | 阶段状态与说明弹窗 | `FightMainPanelStageStatus` 汇总 `StageInfo.Status`、自定义与实际阶段状态并去重，绑定图标／底图，点击 `FightStageStatusDialog` 展示明细；无条目时按原逻辑禁用或隐藏。 |
| F31 | 技能槽、详情与宠物能量 | `FightMainPanelSkillUI`、`FightMainPanelMonsterSkillUI` 接收技能开始／结束、CD、列表和换波事件；`GameSkillIcon` 有 Ready、Applied、InCD、Ended、LimitedTimes、ProfessionLimit、Disabled 状态，含品质、图标及详情。`GamePetSkillIcon` 按实际宠物／进化／辅助技能显示图标、品质、能量百分比和放电动画，无宠物有独立占位逻辑。常规槽的 DebugBtn 不作为正式施法按钮。 |
| F32 | 准备阶段切换技能方案与警告 | `FightMainPanelSkillUI.ShowSkillPlanPanelCondition` 在准备阶段检查不匹配／禁用技能及宠物提示和忽略选项；打开 `FightChangeSkillPlanHandlerPanel` 前尝试取消就绪。养成和方案编辑规则归[技能页](skills.md)，战斗负责入口、阶段门槛、快照刷新和提示。 |
| F33 | 倍速、文案与锁定 | `FightMainPanelSpeedUI.InitValidGameSpeeds/SwitchSpeed` 按模式、条件解锁、团队／成员／加入／匹配等限制生成可选档、切换 `FightComponent.Speed`、更新文案与锁定状态。回放走本地逻辑，原联网模式走对应请求，结束／停用恢复时标。具体配置见下表。 |
| F34 | 快进 | `FightMainPanel` 按功能解锁、阶段支持、角色权限、真人组队、观战／回放条件控制显示；`FightMainPanelFastForwardUI` 用未缩放时间等待解锁延时，在允许阶段发起一次请求或本地快进，处理失败、不可用提示和停用复位。快进与倍率切换是两个功能。 |
| F35 | 镜头跟随与自由镜头 | `FightMainPanelCameraUI` 切换跟随模式；准备阶段不能按普通按钮切换，初始化结合观战、GM、`GameCameraDefautLock` 与 `FightMainPanelExtBehaviour`。`StageInfo.ChangeCameraMode` 和观战／录像条件影响显示，不等同只移动 UI。 |
| F36 | 托管与战斗内背包入口 | `FightMainPanelHostingUI` 按 `FightUtils.CanHosting` 或注入条件显示托管状态并打开 `GameHostingDialog`；`FightMainPanelBagUI` 打开配置的 DialogType 并显示红点。是否保留及可用模式按所属功能页，不默认赋予战斗中自由换装／用物品。 |
| F37 | 录像保存、回放与剧情跳过 | `FightMainPanelSaveRecordUI` 按功能开关、支持的阶段、回放／观战条件记录本场保存意愿并提示；`FightMainPanelRecordFight` 消费剧情的可跳过信号、延迟显示跳过并调用 OnSkipBtn。录像保存／回放不等于活动战斗断点恢复，开场记录播放也不是通用执行器。 |
| F38 | 观战、聊天及模式扩展 | `FightMainPanelAudienceUI` 切换观战对象、自由镜头与对应技能提供者，部分模式可选择加入位置；`FightMainPanelChatUI` 切换迷你聊天／快捷入口。`FightMainPanelSecretArea` 的 Boss 伤害含实际伤害绝对值加护盾伤害、历史峰值提示；竞技决赛支援等也有独立控件。这些仅记录原版能力，离线关闭／替代与玩法专属统计沿所属页，不扩为通用单人规则。 |
| F39 | 战报与技能统计 | `FightEndReportPanelProvider` 按阶段与报告配置选择个人或全员报告；`FightEndReportPlayerTabView/FightEndReportSkillTabView` 等分角色与技能展示，技能项含施放／命中次数、每目标平均次数、实际伤害、治疗、护盾及占比、增益／减益状态次数。公共层须提供可追溯事件与统计；报告入口和胜负奖励页面由模式接线。 |

### 官方倍率与节奏配置

以下为固定版本 `rules.json → game_settings` 中的原值，不能把档位文字当实际倍率，也不能默认所有模式开放四档。

| `Battle` 配置 | 原值与含义 |
| --- | --- |
| `Speeds` / `SpeedUIs` | 索引0：实际1.0、文案 `1`；索引1：实际1.5、文案 `2`；索引2：实际2.0、文案 `3`；索引3：实际2.5、文案 `4`。最终按钮样式仍消费原 prefab。 |
| `PlayModeSpeeds` Type0（None） | 索引0无条件，索引1条件 `[3005]`，索引2条件 `[144]`；条件 ID 须按原条件系统判断，不自行改成免费全开。 |
| Type12（RecordPlayback） | 索引0／1／2／3均无附加条件；`RecordPlaybackSpeedDefaultIndex=0`。 |
| Type13（FreeBattle）、Type20（LegionPlunder） | 分别只配置索引1、索引2；模式标签来自 `GamePlayModeType`，保留原模式边界。 |
| `RoundCountDown` | 5，回合上限临近时的提示配置；具体上限仍由阶段决定。 |

全局 `GameMaxRound=10000` 是兜底，不覆盖阶段／个人上限；`DelayUnlockGameFastforwardBtnTime=1.0` 为快进按钮延迟，`GameCameraDefautLock=true` 为默认镜头配置。原 `RealTeamGameSpeed=1.5`、`CreatePlayerRecordFightSpeed=1.5` 属相应模式配置，不据此决定离线所有战斗速度。

`GamePlayManager.UpdateTimeScale` 在准备阶段恢复1，在 PlayStart／Play／EntryTransition／BattleEndTransition 应用战斗速度，其他阶段按原分支处理，End 释放并复位；不能仅凭 Pause 枚举推定此函数会将时标置0。`GameSoundSpeedController` 用 Wwise RTPC 同步速度。逻辑、移动、技能 Playable、UI 动画与声音需分别核对使用缩放或未缩放时间的原意；快进解锁延迟等明确使用未缩放时间的控件不随意改动。

### 模型、场景、演出与资源

| 编号 | 原始官方功能 | 官方入口与恢复要求 |
| --- | --- | --- |
| F40 | 玩家、NPC 与怪物外观 | `MyFightRoleCharacter`、`PlayerAnimatorAppearance` 及单位组件绑定玩家复合外观、职业／装备／皮肤／武器、NPC 与怪物模型；保留原骨骼、贴图、材质、网格、层序、尺寸、方向、翻转与挂点。外观目录映射不能代替运行时组合与换装。 |
| F41 | 生成、行动、受击、死亡与召唤演出 | `MyFightRoleCharacter` 的生成／延迟／可见性／开场死亡／死亡 Playable，`MyFightActionSkillMove` 的路径演出，技能、命中与伤害 Playable 共同驱动动画；来源与召唤物关系、死亡后移除／隐藏和复活状态按组件保留，不一直播放待机。 |
| F42 | 技能特效、范围、飘字与声音 | `MyFightHitBase` 恢复范围格、来源到目标／目标锚点、偏移与延迟；`MyFightDamage` 区分伤害类型与反馈。官方 HUD 包含普通／暴击／格挡、合并飘字、治疗、自定义动作、免疫、致盲、闪避和重置 CD 等资源；具体音效、震动／镜头等效果按实际 Playable 绑定消费，不自行配近似资源。 |
| F43 | UI 层级与完整生命周期 | `FightMainPanel` 动态装配各子控件，订阅／撤销阶段、实体、技能等事件，停用时释放实例和重置；保留原布局、锚点、字体、遮罩、滚动、布局组、按钮命中、图标旋转与 Animator。Windows 竖屏按比例适配须与输入和遮挡一并验证。 |

现有 `Assets/Official/Combat/HUD/panels-runtime.json` 的30项是已恢复资源子集：11项为主面板、倍速、托管、Boss栏／项、行动头像、阶段状态弹窗／项、技能、宠物技能与状态项；19项为各类飘字。它不表示官方面板总共只有30项，也不表示镜头、快进、准备、录像、方案切换、详情、战报及模式动态 prefab 已接入。后续按原引用与完整资源索引继续提取和恢复；未提取不等于原资源缺失。场景、shader、字体、音频、动态选用资源及依赖的迁移与显式加载统一遵守[素材管理](../asset-management.md)。

## 完整还原部分

`FightDamageFormula`、`FightDamageResolver`、`FightDamageSettlement`、`FightSkillState`、`FightTurnQueue`、`FightBattleTargeting`、`FightBattleHitCollector`、`FightBattleHitExecutor`、`FightDamageFalloff` 和 `FightBattleExecutor` 位于 [`Runtime/Official/Combat`](../../unity/Assets/Game/Runtime/Official/Combat/)。它们覆盖公式伤害与治疗、命中结算、技能状态、行动队列、目标选择、命中图收集、部分状态衰减及带回执的分步执行／恢复。

[`FightOfficialCombatCatalog`](../../unity/Assets/Game/Runtime/Official/Combat/FightOfficialCombatCatalog.cs) 从随工程交付的 Combat JSON 及来源清单装配单位、技能、命中图与状态数据。目录绑定和编辑器规则入口在 [`ProjectTools.cs`](../../unity/Assets/Game/Editor/ProjectTools.cs) 与 [`FightCombatValidation.cs`](../../unity/Assets/Game/Runtime/Official/Combat/Editor/FightCombatValidation.cs)。规则数据位于 [`Assets/Official/Combat`](../../unity/Assets/Official/Combat/)；其来源清单记录原始来源及转换输出。

[`FightBattleGrid`](../../unity/Assets/Game/Runtime/Official/Combat/FightBattleGrid.cs) 保存格网地形、占位与边阻挡，并按官方 `AStarUtils` 的四方向代价和 MoveMask 规则计算路径；[`FightBattleExecutor`](../../unity/Assets/Game/Runtime/Official/Combat/FightBattleExecutor.cs) 在技能超出范围时按 MoveDist 尝试移动，再重新选择目标；若整回合没有可释放技能，则不造成虚构伤害，并按用户确认的本地规则尝试移动到更远离敌人的合法位置。每个单位回合按技能槽检查可用技能，技能状态记录施放、次数限制与冷却，步骤事件记录移动、施放和目标。Map10 的 `BuildCheckpoint` 已从战斗 JSON 装配基础格网、出生位置和波次；`OfficialBattleSceneView` 现按步骤 `Movements` 的逐格路径以官方移动节奏推进模型位置，地图 avatar 通过 `AttachUnitTransform` 交给同一套移动播放，不再逐步瞬移。地形高度、身体占格与阵型字段仍未完整消费。

跨波次生成与可选状态已统一：`FightBattleTargeting.IsSelectable` 要求单位存活、可选中且不属于未生成的后续波次，目标选择、命中目标收集与移动占位共用这一判定；`FightBattleExecutor.Initialize` 将后续波次单位标为 `Ended` 并排除出行动队列，`SpawnNextWaveIfCleared` 在清场后恢复生成、入队和可选中状态。`FightBattleCheckpointRules.Validate` 在恢复及步骤结束时校验“后续波次不得标记已生成、其单位必须 Ended”。无可用技能的单位不伪造普攻；若存在可行格网路径，会按本地确认规则向远离最近敌人的位置移动，且照常推进回合、冷却与状态时点。

玩家技能已从统一存档装配：技能档案 `OfflineSkillProfile` 加入 `OfflinePlayerProgress` 并随角色主存档校验；选择初始武器时按 `rules.json` 的 `SkillOptionsWhenChooseSword/Wand` 授予初始主动技能并放入方案一槽位0。[`FightOfficialSkillItemCatalog`](../../unity/Assets/Game/Runtime/Official/Combat/FightOfficialSkillItemCatalog.cs) 解析 `rules.json` 的 `skill` 表得到技能道具到 `EcEntityId` 的映射；Map10 战斗入口按当前方案读取主动技能、阶位与槽位等级，并单独为每个怪物装配自身技能，避免此前共享绑定列表造成的重复槽位与串用。`FightOfficialCombatCatalog` 新增 `CalculateSkillProps`、`CalculatePassiveSkillProps` 与 `CalculateSkillCooldown`，按官方 `CalcSkillProps` 规则读取 `entity_prop`、`entity_prop_group_level` 与数值表；主动技能属性加入命中结算的来源属性，被动技能属性在推导 MaxHp、速度和移动距离前并入单位属性。技能冷却按 `BattleFormula.SkillCD` 从 CD 属性计算，原数据没有 CD 属性时为0，不自行编造冷却值。

怪物与自动技能目标选择现已接入新式 AI 优先级表：`FightOfficialSkillItemCatalog` 读取固定 `rules.ai_priority` 的40组有序策略及 `skill.AiPriorityId/LastAiPriorityId`；`FightBattleExecutor` 将它们绑定到技能，并在本回合没有其他可用技能时选用末技能组。`FightOfficialCombatCatalog` 同时装配技能实体的 `IgnoreStatusArray`、`AIPriorityHpLowerCharacterTypeFilter`、`AIPriroityCharacterType`、`AiPriorityPropType/FindMax`、`AIPriorityCharacterTypeGroups` 与 `ReleaseWhileRidicule/ReleaseWhileConfusion`；状态 `ActionComponent.ActionType` 也进入战斗快照，固定状态24216／24217分别映射为嘲讽58／混乱54。执行器按固定 `MonsterInfo` 表为初始怪物和战斗中召唤怪物绑定 `CharacterType/AiType`。`FightBattleTargeting` 已按配置执行命中数／状态数、低 HP、移动／目标距离、体型、指定角色类型、属性、队友距离及类型分组等本地确定性比较；活动嘲讽／混乱状态下跳过未配置放行的主动技能，允许嘲讽的技能优先选状态来源，允许混乱的技能在选敌与范围命中时忽略阵营过滤；无技能时按用户确认的本地规则远离敌人。控制状态模拟规则由用户确认，优先组和单位类型字段来源属官方数据，评分仍是离线模拟；服务器评分公式、`AiType` 对应的默认策略／权重及部分优先类型的位置搜索语义仍未取得。

公共 HUD 的官方 prefab 树、sprite、字体和 Spine 骨骼输入由 [`prepare_unity_battle_assets.py`](../../scripts/prepare_unity_battle_assets.py) 按来源哈希恢复到 `Assets/Official/Combat`；当前已恢复的 HUD 子集有30个 prefab、701个 sprite。[`OfficialBattleHudCatalog`](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudCatalog.cs)、[`OfficialBattleHudRenderer`](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudRenderer.cs) 和 [`OfficialBattleHudView`](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudView.cs) 已有隔离 Unity 工程导入及逐 prefab UI 组件树检查。视图支持回合文本、按 `GameRoundUI` 的 NextTime 排序和间距参数呈现行动时间轴、技能图标、冷却、次数耗尽态，并可由步骤事件刷新行动态和技能施放态。玩家已有实际技能，技能栏可显示技能图标与冷却；Map10 仍把技能图标和时间轴头像解析成同一张 `C_none` 占位图，未传阵营图标，因此不能以视图接口存在认定 HUD 的实际内容已接通。原版 `GameSkillIcon` 的常规技能图标不是手动施法按钮，prefab 中 `DebugBtn` 保持禁用。

怪物外观原绑定恢复为407个 Spine 模型、343个怪物 ID，见 `OfficialBattleModelCatalog` 及 `model-identities.json`。通用 [`OfficialBattleSceneView`](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.cs) 现优先使用 `native-battle-meshes.zip` 中固定版854_156383的官方采样网格：来源清单固定403个模型、343个怪物映射和4609个动画，包哈希为 `9af18fb0d8c5b2b3c686a0ee65fb550055d89727ed3600e223175a8fa96265c9`；其中342个怪物有 `idle_01`／`hit_01`、340个有 `die_01`、232个有 `skill_01`。缺少原生待机的模型回退到原绑定。默认动作事件使用可确定的通用 `skill_01`、`hit_01`／`block_01`、`run_01`，非循环动作结束回待机，存在 `die_01` 时播放完成后移除；逐技能 Playable、精确技能动画别名和未绑定怪物动作仍未确定。Map10 对玩家返回空模型键，沿用地图 avatar 并经 `AttachUnitTransform` 驱动。朝向回调保留采样模型缩放、只翻转 X 轴符号。Editor 验收器现把原生动画库传入 SceneView，并检查3207模型生成有贴图的 Mesh；仍未执行 Unity Editor／Play Mode 的实际渲染与输入验收，尺寸和最终画面不能据此判定通过。

[`OfficialBattleRuntimeController`](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleRuntimeController.cs) 提供共用战斗会话入口：`Begin` 可新建或恢复检查点，`Advance` 执行下一步并刷新官方回合文本、行动时间轴、技能槽及战斗模型；`StepCompleted`、`HudControlClicked` 和 `SpeedChanged` 将步骤、面板交互与倍速变更转交给玩法入口。控制器要求入口传入检查点、网格映射、技能／时间轴图标、角色模型键、动作及朝向绑定；它不代替各玩法的阶段数据装配。`OfficialBattleSceneView.Refresh` 现按 `FightBattleWaveState.Spawned` 延迟实例化未生成波次的单位。

倍速已按官方配置接通：`FightBattleSpeedSettings` 从 `game_settings.Battle` 读取 `Speeds`／`SpeedUIs`／`PlayModeSpeeds`，按钮文案与实际倍率分开；`ConfigureSpeeds` 用 `OfficialCharacterCatalog.EvaluateCondition` 判断速度所需官方条件（等级、地图解锁与组合条件，缺少依据的条件保持锁定），`CycleSpeed` 只在可用档位间切换，并通过 `Time.timeScale` 同步战斗步骤、移动插值与模型动画。HUD 已挂载官方 `FightMainPanelSpeedUI` 按钮，`RefreshSpeed` 绑定当前文案与锁定图标；`DisposeSession` 恢复时标。快进、阶段机、Pause 的业务语义与声音 RTPC 同步仍未接入。

每次命中都会生成 `FightBattleDamageEvent`，记录伤害序号、来源、目标、技能实体／道具、命中节点、伤害类型、暴击／格挡／闪避／免疫／致盲、忽略护盾、真伤与破韧标记、结算值、绝对伤害、护盾承受量与命中前后 HP；这些标记由官方命中组件读取并贯通到飘字节点。命中动作按官方 `FightHitBaseComponentInfo.DamageActionType` 区分：5 治疗（即使技能类型不是治疗也会回血）、4 伤害、0 无动作（不结算 HP、不写伤害事件，但技能仍记为已施放）；这些事件随步骤回执保存，可用于飘字与战报追溯。`FightBattleStatistics` 同时累计每单位的施放、命中、承伤、造成／承受伤害与治疗，以及每技能的施放、命中、伤害与治疗，数值取实际 HP 结算结果，因此已包含衰减、溢出与致死截断。 `FightBattleReportBuilder` 在此基础上生成结算报告数据：阶段、胜负、原因、回合、双方血量、总伤害／治疗，以及逐单位的施放／命中／承伤／伤害／治疗与逐技能的施放／命中／伤害／治疗；官方结算面板 prefab 尚未接入，当前只有数据结构与回归。

伤害飘字按官方 `CharacterDamageUIManager.GetEffectUITypeByDamage` 的优先级选择已恢复 prefab：治疗、免疫、闪避、致盲、暴击、格挡、深渊元素与普通伤害；`OfficialBattleHudRenderer` 恢复原节点、sprite、文本、本地化键与 TMP 字体，伤害数字按 `BattleUIUtils.GetBattleNumberString` 的单位格式显示，闪避／免疫／致盲使用 prefab 内的官方本地化文本。技能 HP 消耗会作为步骤回执中的自我伤害表现事件，以官方 `SkillCostDamageColor` 显示，但不计入命中或伤害统计。主飘字曲线按原始 `GameCanvas_FlyText_*` 名称播放，CD 重置使用默认 `GameCanvas_CharacterUI_Delay` 曲线；`OfficialBattleHudAnimationPlayer.Tick` 随战斗步骤时钟推进，节点按 prefab 的 `showTime` 回收。颜色来自官方 `battle-hud/rules.json` 的 `damage_settings`（7 种元素 × Normal/Crit/Block、治疗、技能消耗与默认色，由 `scripts/prepare_unity_combat_damage_colors.py` 导出到 `Resources/Combat/damage-colors`）。固定版 `FightSettings.asset` 的克制提示阈值已核实为 `0.5`；客户端只消费伤害结果中的克制值，状态间的聚合算法仍无官方执行依据。当前不选择 `_Combine` prefab，未找到官方合并条件；状态来源伤害／治疗、自定义动作、驱散、护盾量飘字和元素克制提示尚未接入。

状态血量阈值触发已按官方 `FightStatusHpDecreaseUnitComponent` 接入：状态记录 `UnitHpPer`／`MaxCount`／`TryAtStart` 与 `StatusList`，拥有者生命比例不高于阈值时按次数上限重复施加配置状态；检查点在开战时执行 `TryAtStart`，每次受击后按当前生命再次判定。触发技能列表也已执行：由状态拥有者施放，沿用官方目标选择与命中结算，伤害／位移并入当前步骤，施放计入统计并生成 `Slot=-1` 的施放事件（事件新增 `ActorId`），且最多一层触发（触发技能不再级联触发），不占用拥有者技能槽。核心规则里 12 个状态配置了该组件。

回合时点触发已接入两种固定配置：状态 25256 按 `FightStatusRoundStartComponent` 在施加者回合开始执行 12980；状态 25177 按 `FightStatusRoundEndComponent.ListenTargetType=ApplicatorMaster` 在主人回合结束执行 50180，来源解析为状态施加者、目标及命中位置解析为本次触发对象。均使用官方技能目标类型、命中范围与结算，写入同一步骤的施放、伤害和演出时长，并持久化触发次数。仅执行无延迟／额外筛选、固定来源和目标、无 AI 或自定义伤害的配置；其他延迟、阶段、状态触发、属性概率、AI 或自定义句柄分支仍跳过。固定版 25256 与 25177 的执行回归覆盖真实技能目标和检查点往返。

技能开始触发已接入固定状态 24440／24476：状态持有者开始 `ActionType.Skill` 或 `SkillMove` 时，按配置由状态创建者施放 12520／12580，以状态持有者为目标，并将主技能的首个有效目标作为触发技能命中位置；固定概率分别为50%与100%。触发技能的扣费、命中、施放事件与时长并入当前步骤，触发次数进入检查点。只支持单一 `SkillActionType` 条件、来源／目标固定且无自定义条件／效果的配置；其余技能条件组与技能开始状态应用仍待实现。

技能结束触发已接入固定状态 25011／60840：每次符合配置的技能结束后累计 `MetCount`，按 `FilterTypes` 跳过被过滤的动作，再按 `StatusCfgs.ApplyTarget` 将状态施加到状态持有者或创建者；施加概率、状态来源、生命周期与检查点沿共用状态规则处理。仅支持无元素／技能筛选、无技能条件检查、无 `StatusEntClassId` 和无属性替换的状态列表配置；其余状态结束分支仍待实现。

`FightStatusHpAccDecreaseComponent` 已解析到检查点状态，并在每次命中实际扣除 HP 后累计伤害；按最大生命值比例或固定值计算阈值，单次伤害跨多个阈值时连续触发并保留余数，治疗不抵消已累计伤害，按 `MaxCount` 限制触发次数，`ResetAtRoundEnd` 控制回合末重置。配置的状态与技能触发沿本地目标／命中执行器结算；其中伤害累计只针对拥有者实际承受的 HP 损失，不把溢出伤害计入。客户端反编译与共享组件仅有数据结构，没有找到该组件的服务器执行流程；治疗不冲减与跨阈值重复触发按用户确认作为离线规则实现。

技能额外施放已按官方 `FightSkillExtSkillComponent` 接入：动作结束时按 `Rate` 判定并释放固定 ID 的额外技能，由同一单位施放，沿用目标选择与命中结算，伤害／位移并入当前步骤，生成 `Slot=-1` 施放事件并计入统计；额外技能最多一层（不会级联释放自身额外技能）。核心规则中有 5 个技能配置该组件。，官方飘字 AnimationClip 尚未迁移，暂用 0.9 秒上升淡出，与开场战斗飘字的本地处理一致。

技能重播已按官方 `FightSkillReplayComponent` 接入：技能动作结束后检查拥有者的状态层数条件（当前仅建模 `StatusStackCountFilter`），满足则重播同一技能一次，并按 `ConsumeStackStatusHanldeCfg` 消耗层数（层数归零时移除状态）；重播计入施放统计并生成施放事件，最多一层，不会无限重播。核心规则中 4 个技能配置该组件。

子技能已按官方 `FightDamageComponent.ChildSkillCfg` 接入：命中落地后由受击目标按 `Rate` 与剩余生命条件（`LessThanHpPer`）释放配置的子技能，沿用目标选择与命中结算，生成 `Slot=-1` 施放事件并计入统计；子技能最多一层，不会级联释放。核心规则中 25 个伤害实体配置该组件。

`FightBattlePhaseMachine` 按官方 `GameStatus` 提供 Preparing、PlayStart、Play、Pause、EntryTransition、EntryTransitionFinished、BattleEnd、BattleEndTransition、BattleEndTransitionFinished、End 的合法迁移与准备倒计时；单机主线可跳过准备，`Ready` 结束倒计时，倒计时归零只触发一次自动开始请求。`OfficialBattleRuntimeController` 已接入该状态机：开始即进入 PlayStart→Play，暂停只允许在 Play 与 Pause 间切换，步骤产生结束结果时进入 BattleEnd，释放时进入 End 并复位时标，阶段变化通过 `PhaseChanged` 交给玩法处理。准备面板、就绪 UI、倒计时显示、入场与结束演出尚未接各模式面板。

技能动作与命中时序已按官方配置进入步骤事件：`FightBattleSkillDefinition` 保存 `ActionDuration`、`AllEndedActionDuration`、`AllRelevantEndedActionDuration`、`Duration`、`AllEndedDuration`、`AllRelevantEndedDuration`；`FightBattleHitRoot`／`FightBattleHitEntity` 保存命中引用边上的 `Delay`、`ActOnSouce` 锚点与 `IgnoreAICount`，同一实体的多次根引用各自保留自己的延迟与锚点。执行器按根引用收集命中图、把根引用配置写到该根的第一个命中节点，并在伤害事件中记录 `Delay`／`Anchor`；步骤结束按“最后一个命中延迟 + AllRelevantEnded／AllEnded，或固定时长”计算演出时长，移动再按每格0.5秒补足，写入 `FightBattleStepEvent.Duration`／`ActionDuration`。`OfficialBattleRuntimeController.LastStepSeconds` 把该时长交给 Map10 的推进定时器，替换固定0.35秒。当前伤害仍在同一执行步骤内结算，尚未按 Delay 分帧结算或等待每个命中动作完成。

Map10 阶段行中的官方上下文已进入 Unity DTO：`Type`、`IsPlayerRoundLimit`、`PlayerRoundLimit`、`ShowMainPanelDelay`、`SkipPrepareStatus`、`UserStart`、`LeaveInPreparing`、`LeaveInPlaying`、`Background`、`ChangeCameraMode`、`JoinInMiddle`、`DismissTeamWhenLeaveOrEnd` 及 `CanFastforward`。检查点新增个人回合上限字段，执行器在主玩家个人回合达到 `PlayerRoundLimit` 时结束战斗并以结果原因4区分全局超时；战斗进行中若阶段 `LeaveInPlaying=false`，Map10 拒绝离开请求并提示，避免把官方禁止离开的阶段当作可随时退出。`ShowMainPanelDelay`、`SkipPrepareStatus`、`CanFastforward` 等字段已读入但尚未驱动面板延迟、准备阶段与快进。

Boss 栏已接入官方 `FightMainPanelBossUI`：`RefreshBoss` 只选存活、波次已生成、敌方且 `MonsterType` 为3–5的单位，最多两个，按顺序绑定 `FrontItem` 与 `BehindItem`，HP 用 Filled 的 `TopImg.fillAmount`，名称取官方 monster 表的 `CustomTypeName`（无则 `Memo`），无名称时隐藏文本；无 Boss 时隐藏整个面板。此前把控制台编码显示问题误判为载荷乱码，已用码点核对更正：`3206` 的名称为“野猪”。盾、多层血量、韧性与头像暂时隐藏。Boss 状态列表已按官方 `FightStatusController` 逐状态加载 `entry.g` 图标与背景，叠层显示遵循状态显示配置；HUD 渲染尚待 Unity Play Mode 实际验证。

战斗检查点可持久化：`FightBattleCheckpointSerializer` 以版本化 JSON 保存并还原检查点头（阶段、模式、回合上限、个人回合上限、随机种子、序列与计数）、单位（属性、技能、技能状态、状态实例、身体占格）、波次、格网、行动时间线、步骤回执、命中伤害事件与统计；保存按字段声明顺序稳定输出，读取按字段名还原并执行严格检查点校验。Map10 每5步把进行中的非剧情战斗写入本地存档（阶段与放置点标识、保存时间），重新进入同一阶段和放置点时提示“继续上次战斗”并用 `Restore` 恢复；战斗结束、主动离开或异常终止时清除存档，剧情战斗不自动续战。

技能图标已接入：`scripts/prepare_unity_combat_skill_icons.py` 从 `icons.json` 解析技能道具到同名图标 PNG（以文件名中的道具 ID 为准；同一文件的碎片 ID 折叠为同一技能 ID），读取现有 PNG 尺寸与归一化 pivot、ppu，生成 `Assets/Game/Resources/Combat/OfficialCombatSkillIconCatalog.asset` 和带输入／输出哈希的来源清单。运行时 `OfficialCombatSkillIconCatalog.Find` 用官方纹理即时创建 Sprite 并缓存，Map10 技能栏优先使用真实图标，缺失时回退 `C_none`；当前绑定376个技能图标；纹理取自 Wish 模块已恢复的官方图标目录，若该目录未随仓库交付则图标回退占位。

怪物头像由 `scripts/prepare_unity_battle_monster_portraits.py` 从固定 APK 的两个无图集头像包提取，按 Map10 战斗 roster 引用的 Monster.IconId 输出11张 PNG，生成 Sprite meta 与 `OfficialBattleMonsterPortraitCatalog.asset`；来源为 `defaultpackage_assets_image_itemicon_gameheadicon_monster_map4countries_noatlas.bundle`（MD5 `0479892d6d89bf17fd2e592af3f03a49`）与 `defaultpackage_assets_image_itemicon_gameheadicon_monster_mapsp_noatlas.bundle`（MD5 `2683984d4e613c8eb9411bc16ea483cb`），两者均来自固定 APK `854_156383`。输入 bundle SHA256 与输出哈希见本地 `assets/reference/battle-hud/monster-portraits-source-manifest.json`；头像 PNG 位于 Git 忽略的 `unity/Assets/Official/Combat/MonsterPortraits/`，可由脚本重建，当前完整快照尚未更新。官方玩家头像节点使用 `GridSpriteRawImage`，HUD catalog 保留 `scripts` 元数据，renderer 创建 `RawImage`，`RefreshTimelineCard` 按 Sprite.texture 与 textureRect UV 绑定回退头像；怪物 `MonsterIconImg` 仍走 Image.sprite。

状态基础生命周期已按官方 `FightStatusComponentInfo` 落地：`FightBattleStatusState` 保存 RoundTarget、DurationRound、CurDurationRound、StartRound、RoundUpdateTiming、RemoveAtRoundTargetDestroy、ActionBehOfSameStatus、CanDispersed、IsAddDurationRound／MaxAddDurationRound、IsResetDurationWhenStack、叠层层数与随机步距、MaxStackedCount、来源类型等基础字段；`FightStatusLifecycle` 按官方公式计算剩余回合（`Max(0, CurDurationRound - (round - StartRound))`，负数表示无限），实现叠时扩展与上限、叠层上限、Parallel／Immediate／Discard 同名状态处理，并在回合开始／结束时按 RoundTarget 与 Timing 到期移除、对 RoundTarget 已销毁的状态执行移除。执行器在开战初始化回合、每个单位回合开始／结束及步骤末调用该生命周期。状态属性变换、护盾吸收与用户确认的24181减伤已接入；控制、破韧／恢复、生命层及其余事件触发仍未完整实现。

技能自身位移已接入 `FightSkillComponent.SourceMoveList`：按 `Delay` 排序保存`FightBattleSkillMove`（移动行为 ID、偏移、朝向模式、距离、条件标记）；执行器支持`Distance=-1` 拉到目标旁可站立点与 `Distance>=0` 且 `RelativeToTarget` 的直线进退，按格网通行、占位与身体占格寻路，目标格被占时自动尝试更短距离，产生逐格 `Movements` 并更新朝向；带 `DoMoveConditions` 或非 `RelativeToTarget` 的配置保持不执行，不用猜测替代。初版 `skill-rules.json` 只包含 6 类 EC 组件（Skill／Status／四种 Hit），但战斗目录同时装载的 `battle-rules.json` 已包含 7920 个实体及其子组件：`FightStatusPropComponent` 664、`FightStatusShieldPersistentComponent` 53、`FightStatusDamageReducePerComponent` 34、`FightHitFixedComponent` 1936、`FightRoleFightComponent` 570 等。`FightOfficialCombatCatalog` 通过 `HasComponent`／`TryGetComponent` 读取效果配置；状态属性变换、持续／一次性／无敌护盾吸收、自定义护盾外部值及用户确认的 24181 单段减伤已接入并有规则测试。`scripts/prepare_official_combat_entities.py` 相对 `battle-rules.json` 只额外补出 23 个实体的 `VisibilityHideComponent` 等世界显示组件，没有新增战斗效果组件，不参与战斗结算。控制、破韧／恢复、生命层及其余事件触发仍待实现。

命中附加状态已按官方 `FightDamageComponent.StatusList` 接入：`FightBattleHitDefinition` 保存 `StatusId`、`TargetType`（1 技能来源／2 受击目标）、概率 `BasePercent` 与 `LessThanHpPer` 条件；命中结算后执行器按条目选择来源或目标、检查血量条件与概率，再经 `FightStatusLifecycle` 施加状态（起始回合留待拥有者的下次回合更新）。`battle-rules.json` 中有 470 个伤害实体配置了状态列表，官方技能 12860 的命中即会附加状态 24078；持续 0 回合的状态会在同回合结束时按生命周期移除。`FightDamageComponent` 的驱散、位移、切换形态、子技能与状态随机单条等其余字段尚未实现。

命中节点补上了 `FightHitFixedComponent`（`battle-rules.json` 中 1936 个固定／治疗命中），此前这类节点不会被装配成命中定义；现在与链式／随机／自选命中一样读取伤害字段、范围、动作类型与附加配置。驱散按官方 `FightDamageComponent.DisperseStatus` 接入：命中后按 `TargetTypes` 与状态 `CanDispersed` 过滤，按 `Count`（0 表示全部）与 `IsRandom` 决定移除哪些状态，并按 `BasePercent` 判定概率；`AffectedByProp` 与 `FilterCondition` 尚未参与判定。核心规则里 66 个伤害实体配置了驱散。

状态列表还支持官方 `StatusListRandomOne`：为 true 时从命中状态列表中随机取一条尝试，为 false 时按顺序逐条尝试；`OverrideRateByCharacterTypes`、`SkillCostHp`／`DamageCostHpPer` 与 `AffectedByProp` 仍未参与判定。

吸血按官方 `FightHitExtHemophagiaComponent` 接入（`battle-rules.json` 中 16 个命中节点）：读取 `HemophagiaPropType`，按来源单位的该属性与本次实际伤害计算回血并受最大生命限制，回血写入治疗事件与统计。该属性未出现在 `percent_value_props` 表中，本实现按 10000 基准的百分比处理（显式本地假设），属性缺失或为 0 时不回血，治疗动作本身不再产生吸血。

命中位移按官方 `FightDamageComponent.MoveCfg` 接入：默认 `Distance=-1` 表示不位移，`Distance>0` 时沿来源到受击者的方向击退、`TowardsTargetPos=true` 时改为拉近，支持偏移与取消斜向，目标格被占或越界时自动尝试更短距离，位移作为受击者移动事件输出；带 `DoMoveConditions` 的配置保持不执行。核心规则里 134 个伤害实体配置了有效位移，`BasePercent`／`AffectedByProp` 缩放与移动行为实体仍未消费。

阶段状态已按官方 `fight_stage_status` 接入：`FightStageStatusCatalog` 从 `rules.json` 读取 `stage.Status` 到 ClassId 的映射与每行配置（Status、CharacterStatus、GlobalRoundStatus、FactionGlobalRoundStatus、TargetIdStatus、ProfessionStatus、GlobalRoundStatusDelay、ReleaseEveryGlobalRound、ViewStatusBlackList），视图列表与官方 `FightStageStatusInfoParser.GetViewStatusList` 一致（并集去黑名单）。`FightStageStatusRuntime` 在开战时按全体、角色类型、职业与目标怪物 ID 施加状态，并在全局回合达到 Delay 的整数倍时施加全局／阵营全局状态；检查点保存该配置并可往返，负延迟会被拒绝。战场没有独立阶段实体，阶段级状态暂施加到全部单位。Map10 中只有 9910 阶段配置了阶段状态（ClassId 1），而该阶段在入口处直接完成、不进入战斗，因此当前主线战斗不会触发阶段状态；进入战斗的阶段状态使用通用状态生命周期与已接入的属性、护盾和触发组件。主面板阶段状态条按官方 `entry.g` 为所有可解析的 `ViewStatusIds` 显示对应图标／背景；资源目录由 `scripts/prepare_unity_battle_status_icons.py` 从固定 APK 恢复715条状态绑定（35个官方图标／背景键）并固定来源摘要。点击状态条会打开官方 `stage_status` 弹窗，按固定版41条本地化数据显示图标、名称与说明；框架动态描述提供器及 Unity Play Mode 的鼠标／画面验收仍待完成。之前仅查 `fight_status.g.bytes` 得出“状态图标表为空”的结论不适用于此处。

目标选择已按官方 `FightSkillComponentInfo.Range` 的近到远顺序优先：未配置 AI 优先属性时，候选按其在 `Range` 列表中的下标排序（越靠前越近），射程内目标优先于射程外目标，射程外仍保留给移动规划；显式 `AiPriorityPropType` 优先属性仍先于射程顺序，完全并列时沿用原有属性／血量与 ID 比较。命中范围 `HitCfg.Scope`／`FightHit*ComponentInfo.Scope` 的解析与范围命中见下一段。

命中范围 `Scope` 已接入：`FightBattleHitDefinition`／`FightBattleHitEntity` 保存 `HitScopCfg.Pos` 偏移与 `AllowEmptyScope`；命中按锚点取中心（`OnTarget` 用主目标位置，`OnSourceCastPos` 系列用施法者位置），从技能目标阵营候选中筛选落在范围内的单位，`TargetMode=1` 保留范围内全部目标、`TargetMode=0` 取最近一个、随机命中只在范围内随机；范围为空时不再命中范围外单位。官方 12860 的链式命中为单格范围，16100 的随机命中为48格范围，另有 `NotSameTarget`／`NotSameEmptyScope`／`FilterCondition`／`FilterOutOfGroundScope`、Scope 延迟与旋转尚未消费。点目标技能（`SkillTargetType=Point`）在 `Range` 内没有敌人时，会在范围内有可命中单位的情况下以施法者为中心释放（如 16100 的 `Range=(0,0)` 自爆范围），`FindReadySkill` 与执行器使用同一检查；没有可命中单位时不释放。

命中延迟按官方 `CalcSkillHitDelay` 计算：`FinalDelay = Delay + 距离 × DelayDistFactor`，距离取命中起点到命中点的直线距离（格坐标欧氏距离，未计偏移与旋转）；`OnTarget` 命中按施法者到目标计算，`OnSourceCastPos` 系列锚点距离为 0 而保留配置延迟。伤害事件保存每个目标各自的最终延迟，步骤时长取其中最大值；事件仍按目标顺序记录，不按延迟重排。

规则检查入口为 [`FightCombatValidation.RunSmokeTest`](../../unity/Assets/Game/Runtime/Official/Combat/Editor/FightCombatValidation.cs)，菜单为 `Zjcs/Combat/Run Rule Smoke Test`。`Zjcs.Editor.ProjectTools.Validate` 还会装载官方战斗目录并运行规则检查。实际验证范围如下：

- 规则：已运行 `scripts/test_unity_combat.ps1` 通过，覆盖97个固定公式样例及战斗目录装配、技能／状态、命中图、衰减、胜负、检查点恢复、随机流、时间线、目标选择、多目标衰减、技能道具到 EC 实体映射、初始武器技能、技能／被动属性计算、官方倍速表与条件档位、命中伤害事件与战斗统计、官方命中延迟与技能动作／持续时间映射、官方怪物名称查表，以及跨波次生成与可选状态回归（未生成单位不中选、不被命中、不占位，清场后立即可选）；阶段机另由 `scripts/test_battle_phase_machine.ps1` 通过，覆盖准备倒计时、就绪、一次性自动开始、Play／Pause、入场与结束迁移、释放及非法顺序拒绝；步骤时序由 `scripts/test_battle_step_timing.ps1` 通过，覆盖命中延迟、根引用锚点／延迟、动作时长、持续时间回退与移动补时；个人回合上限由 `scripts/test_battle_stage_limits.ps1` 通过，覆盖达到上限以原因4结束、结束后拒绝继续执行、关闭上限与0上限不生效；检查点持久化由 `scripts/test_battle_checkpoint_persistence.ps1` 通过，覆盖版本化载荷、全字段往返（含阶段状态、状态血量阈值字段、回合开始触发、命中状态列表／驱散／位移／吸血、伤害元素）、稳定重存、恢复重放与续战、非法载荷拒绝；状态基础生命周期由 `scripts/test_battle_status_lifecycle.ps1` 通过，覆盖剩余回合、无限持续、叠时扩展与上限、叠层上限、同名状态合并规则、回合时点到期、销毁目标清理与执行器集成。状态25256回合开始技能触发由 `scripts/test_battle_status_round_start.ps1` 通过，覆盖固定映射、施加者时点、技能目标选择与检查点恢复。技能自身位移由 `scripts/test_battle_skill_moves.ps1` 通过，覆盖拉到目标旁、正向距离、占位回退与不支持配置跳过；规则套件同时校验官方技能 15980 的 `SourceMoveList` 映射。射程顺序选敌由 `scripts/test_battle_target_range.ps1` 通过，覆盖近到远顺序、配置下标顺序、射程内优先与 AI 优先属性前置；规则套件同时校验官方技能 12860 的 `Range` 顺序。命中范围扩展由 `scripts/test_battle_hit_scope.ps1` 通过，覆盖目标锚定 AoE、施法者锚定 AoE、空范围不命中、随机命中限定在范围内与点目标技能以施法者为中心释放；命中距离延迟由 `scripts/test_battle_hit_delay.ps1` 通过，覆盖按距离缩放、源锚点保留配置延迟与范围命中的逐目标延迟；命中视觉标记由 `scripts/test_battle_hit_visual_flags.ps1` 通过，覆盖官方 IsTrueDamage／IsBreakResilience 映射、事件传递与检查点恢复；命中动作由 `scripts/test_battle_hit_action.ps1` 通过，覆盖治疗动作、无动作命中与普通伤害动作；阶段状态由 `scripts/test_battle_stage_status.ps1` 通过，覆盖官方阶段映射、视图黑名单、开局按角色类型施加、全局回合节奏、阵营范围、检查点校验与往返；命中附加状态由 `scripts/test_battle_status_application.ps1` 通过，覆盖官方状态列表映射、目标／来源选择、概率与血量条件；命中驱散由 `scripts/test_battle_status_disperse.ps1` 通过，覆盖官方驱散映射、状态类型过滤、可驱散标记、数量上限、随机选择与概率；命中位移由 `scripts/test_battle_hit_move.ps1` 通过，覆盖官方位移映射、击退、拉近、越界回退与条件跳过；状态／驱散／位移组合的检查点回放由 `scripts/test_battle_effect_replay.ps1` 通过，覆盖随机单条状态、顺序状态、存档重放一致与继续执行；命中吸血由 `scripts/test_battle_hemophagia.ps1` 通过，覆盖官方属性映射、百分比回血、上限、属性缺失与治疗命中不回血；状态血量阈值由 `scripts/test_battle_hp_trigger.ps1` 通过，覆盖官方映射、阈值判定、触发状态施加、触发技能请求与执行（含施放事件）、次数上限与开战检查；结算报告由 `scripts/test_battle_report.ps1` 通过，覆盖报告头、单位行、技能行、合计与血量；`OfficialBattleHudAssetTools` 现包含伤害飘字的时间延迟、标志节点、普通／重置曲线推进和同目标堆叠检查，尚未在 Unity 编辑器执行。Map10 真实数据整场战斗由 `scripts/test_battle_map10_integration.ps1` 通过：从 `Map10/battle-runtime.json` 读取怪物最多的阶段（GameId 1004）网格、波次、怪物属性与主动技能，用合成玩家（5000 生命、MoveDist 8）装配官方技能 12860，并通过 `CalculateSkillProps` 传入技能属性后跑完整场战斗，校验每步 HP 边界、步骤回放与结束结果（当前 48 步、95 个伤害事件、9 只怪物全部击杀，获胜结束）；玩家面板属性为测试用合成值，不代表正式角色数值。该回归同时确认：技能伤害必须携带 `CalculateSkillProps` 结果，否则伤害百分比属性缺失会算出 0 伤害。规则套件同时校验官方 16100（48格）与 12860（单格）的 `Scope` 映射。技能额外施放由 `scripts/test_battle_extra_skill.ps1` 通过，覆盖官方映射、按概率释放、零概率不释放与单层限制。技能重播由 `scripts/test_battle_skill_replay.ps1` 通过，覆盖官方映射、层数条件、层数消耗与单层重播。子技能由 `scripts/test_battle_child_skill.ps1` 通过，覆盖官方映射、受击目标释放、血量条件、零概率与单层限制。
- 回合结束状态：`scripts/test_battle_status_round_end.ps1` 通过，核对固定版 25177→50180、ApplicatorMaster 监听、Applicator 来源、TriggerTarget 目标／命中位置、真实触发施放与检查点往返；通用战斗、25256 回合开始、检查点和 Map10 全场集成在本次改动后均通过。Unity Editor／Play Mode 画面仍未验证。
- 技能开始状态：`scripts/test_battle_status_skill_start.ps1` 通过，核对固定版 24476、Skill／SkillMove 条件映射、Creator→Applicator 触发目标、首个目标命中位置、父技能执行顺序与检查点往返。通用战斗目录按 `FightSkillComponent.ActionType` 装配动作类型。
- 技能结束状态：`scripts/test_battle_status_skill_end.ps1` 通过，核对固定版 25011／60840 的 `MetCount` 与动作过滤、Applicator／Creator 目标枚举转换、状态施加和检查点往返。
- 素材：`scripts/test_battle_monster_portraits.ps1` 通过，核对11个 Map10 IconId、固定 bundle 收据、PNG 哈希、Sprite meta 与 catalog 引用；`scripts/prepare_battle_spine_pages.py` 从固定 APK 核验并恢复 Map10 已映射怪物所需的 Spine atlas 页。`Zjcs.Editor.Combat.OfficialBattleTimelinePortraitAcceptance.Run` batchmode 回归通过，确认玩家 RawImage 纹理与怪物 `G_3206` Sprite；`OfficialMap10BattlePlayModeAcceptance.RunPortraitOnly` 在 GUI Play Mode 实际 Map10 入口通过，确认玩家 RawImage 与首个怪物 Sprite 均已绑定。
- 静态／规则：本轮 `scripts/test_unity_combat.ps1` 与 `scripts/test_battle_map10_integration.ps1` 通过；Map10 固定数据集成战斗完成48步、生成95个伤害事件、击杀9只怪物并以胜利结束。`python -B scripts/check_unity_project.py` 通过静态元数据检查（38,668 GUID）。该检查不覆盖 Unity 编辑器导入／编译、Play Mode 输入／画面或 Windows Player；这些仍未验证。
- Editor／Play Mode：已记录的 GUI `RunPortraitOnly` 检查曾确认 Map10 玩家 RawImage 纹理与怪物 `Image.sprite` 绑定；完整 `Run` 当时未执行首个战斗步骤（检查点 `Sequence=0`）。HUD Editor 验收器已有隔离 prefab 检查，并包含伤害／重置飘字动画曲线及同目标堆叠偏移断言；战斗场景验收器会实例化怪物3207、检查带官方纹理的网格并播放 `skill_01`，但本次改动尚未在 Editor 或 Play Mode 重新导入、编译及实际画面验证，Unity 当前被锁定。当前未构建 Windows Player。

- 条件召唤：当前战斗核心回归通过，验证58140／70120分别从固定命中配置装配3907与3908各3只容量；满两个池时不可释放，六只其他类型的主人召唤物不占用这两个池；18480在配置触手少于4只时可释放，达到4只时不可释放。

## 自定义实现

战斗按本地单机方式设计，不依赖运行时服务器。原客户端共用入口和面板行为分别见 `FightSceneRoot`、`MyFightGridMover`、`MyFightGridGround`、`MyFightRoleGrid`、`FightMainPanelSkillUI`、`GameRoundUI`、`FightMainPanelBossUI`、`FightMainPanelBossUIItem` 和 `FightMainPanelStageStatus`。现有迁移只覆盖已经接入的规则、格网算法、部分战斗模型数据及 HUD 构建；没有把尚未迁移的原客户端行为写成已完成。

以下为已确认的单机规则要求；是否已由当前 C# 执行器落实，须逐项核对和验证，不能沿用旧引擎的通过结论。

- 个人回合按从左到右检查所有技能，每槽最多主动一次，每招使用完整 MoveDist；移动、伤害、被动和死亡后重新评估下一招。不因相同增益权重自行禁止重复技能。
- TriggerDamageAddToDamageSource：被动触发伤害归属被动，技能效果伤害归属所属技能；统计实际生命损失。确定性 AI 使用原候选、条件、移动、范围与队列参数，不声称复刻未取得的服务端评分。
- 技能／秘法效果之间的外部概率修正乘算；固定概率不受外部修正，直接加状态无需判定。官方 EffectRate 公式及 EffectRateValue - EffectDodgeValue 加减项仍原样使用，不能据补证改成全乘算。
- 状态效果默认触发后立即结算，明确的回合时点保留：25256 在回合开始触发12980，25177 在主人回合结束触发50180；生成的行动条状态立即处理。作用于当前行动者的行动条调整在行动结束重新入队时用于下一次行动。
- DamageFalloff 的同目标递减分别计数，整体递减跨目标累计；首击计数1且不减伤，30%递减的第二击乘70%，之后按原幂次继续。施放相互隔离，子技能沿用父施放编号，行动结束清理临时计数；明确的起始次数仍按原配置。命中后附加状态时补记首击属于已确认本地接线，未取得服务端执行器。
- 状态24181 按用户补证先减伤再扣盾／HP；伤害严格超过目标最大生命的4%时，读取目标当前232号属性作为减伤率，每层降低一段并消耗一次，其余条件沿原配置。
- HP 累积伤害按实际 HP 损失累计，治疗不抵消；跨越多个阈值时逐次触发并保留余数。该执行组件在客户端与共享代码中仅有数据结构，具体累计规则按用户确认在本地模拟。
- 无可释放技能时不伪造普攻；若格网、占位与 MoveDist 允许，则向远离敌人的合法位置移动，随后正常推进个人回合、冷却、状态时点与胜负检查。该退避方向按用户确认在本地模拟，客户端／共享实现未提供服务器移动目的地规则。
- 条件技能按状态 `ConditionalSkills` 与 `SkillConditionRules` 本地判定；固定版有15个 `FightStatusSkillConditionComponent` 实体，其 `Settings` 只出现6个 HP 条件、1个回合条件与1个召唤池条件，分别由本地检查点规则和召唤池容量执行。技能18480「肢体修复」按状态24314关联的召唤池计算，存活触手少于4只可释放、达到4只关闭。条件孵化技能58140／70120没有关联状态条件组件，按固定召唤命中配置检查自身召唤池：召唤物3907与3908各自未达到3只时可释放，两个池都满时关闭；其他召唤物不占用这两个池。共享枚举中的技能类型、召唤物数量、全局回合、命中目标类型、技能行为与元素条件在固定版未找到配置实例，也未找到客户端执行链。怪物召唤命中已按固定配置创建单位、属性、被动状态／技能、主人关系、占位、行动队列、检查点及模型模板；其他召唤类型、召唤继承属性与来源／死亡销毁联动仍未完整迁移。
- FightStatusDamageModification.Run 在本项目暂弃用；无已定位原玩法调用点，不将调用来源未知单列缺项，不称官方弃用，兼容扩展不冒充官方服务端流程。
- 本地检查点保存规则状态、随机流、时间线和步骤回执；奖励回执及退出恢复尚未接入战斗入口。

## 尚未实现

以下清单覆盖上面 F01—F43 的公共战斗迁移范围；未勾选表示尚有实现、接线或验证工作，不表示相关官方资源不存在。已具备的算法、配置、资源或接口以“完整还原部分”的实际边界为准。模式功能另有保留／关闭／替代决定时，执行该决定，不以此清单擅自重新开放。

### 会话、阶段与进度（F01—F07）

- [ ] 将各战斗入口需要的 Stage、GamePlayMode、PVE／PVP 上下文、胜负条件、回合限制及官方配置装配到共用会话。公式已从可恢复检查点读取 `StageType`／`IsPvp`；Map10 取官方阶段类型，日常副本与 NPC 竞技场使用各自官方模式类型并保持离线 PVE。其余入口的阶段装配、GamePlayMode、胜负条件与官方配置仍未贯通，不能据此开放已关闭 PVP。
- [ ] 接通准备、开始、战斗、暂停、入场过渡、战斗结束、结束过渡和释放事件及对应面板，保留模式跳过准备、延迟面板和外部覆盖条件。阶段机与控制器事件已具备，Map10 仍按定时器调用 `Advance`，尚未把各阶段接到面板、演出与模式入口。
- [ ] 接入就绪、取消就绪、准备倒计时、自动开始与失败提示；`FightBattlePhaseMachine` 已提供准备倒计时、`Ready`、一次性自动开始请求与就绪校验，缺准备面板、倒计时显示与失败提示。仅在适用的本地模式恢复，团队或服务器时间的替代按所属页已确认范围处理。
- [ ] 接通个人／全局回合、临近上限的颜色与倒计时、不同阶段上限和上限结果；个人回合上限已按 `StageInfo.IsPlayerRoundLimit/PlayerRoundLimit` 在检查点执行，达到上限以结果原因4结束并可区分全局超时；HUD 仍只格式化全局回合与上限，个人回合、颜色预警与倒计时未显示。
- [ ] 将技能动作、命中、移动、伤害、子技能与死亡完成条件纳入调度，逐事件刷新表现。当前步骤时长已取官方动作／持续时间与命中延迟，Map10 按该时长推进；仍未按 Delay 分帧结算、等待每个命中／移动 Playable 完成，多招仍在一步内整体执行后刷新。死亡后模型已从场景移除；死亡动画未接入。
- [ ] 按实际结果组件判定胜负，提供结果事件、终止后防重复执行、结束演出及资源清理；把结果交给所属玩法的胜负页、奖励、下一关和剧情分支，不能把原报告消费误作已取得奖励生成规则。
- [ ] 恢复离开按钮可见条件、阶段／模式限制、确认提示和后台切出差异，处理取消、失败、重复点击及释放。战斗进行中已按 `StageInfo.LeaveInPlaying` 拦截离开请求并提示，`LeaveInPreparing`、确认框、后台切出及控制路径仍未接入。

### 站位、场景与选敌（F08—F15）

- [ ] 从原 prefab、`FightSceneInfo`、阶段／波次数据恢复公共场景、地面、格网、几何、坐标映射、出生点与相机。`scripts/prepare_unity_battle_scenes.py` 已按固定版为256个 GameId／229个场景恢复6,656项 Unity 资源；[`OfficialBattleRuntimeController.Begin`](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleRuntimeController.cs) 按 GameId 装配基础几何与 prefab 默认昼／夜几何，Map10 入口传入 GameId。仍未接入原相机与 StageType 偏移、`StageInfo.TimeType`／`MapActivedAuto` 选择、环境粒子和 Spine；当前几何使用 Unity `Sprites/Default` 材质，未恢复原材质／shader，且尚未执行 Editor 导入和 Play Mode 画面验证。
- [ ] 按单位恢复 MoveMask、地面高度、身体多格占位、边阻挡、出生方向、Rotation、FlipX、ViewGroup 和阵型偏移；当前 `BuildCheckpoint` 只装配部分基础字段，不能使用统一可通行掩码替代单位差异。
- [ ] 接入原准备阶段拖拽：阶段／阵营／队伍或助战／就绪／所有权门槛、允许区域、拾起放下、屏幕到格网、阈值、非法位置提示与回退，保留鼠标拖拽真实输入。自己的 NPC 站位与出战快照按[NPC 助战](npc-assistance.md)接线，不将普通队友控制权扩大。
- [ ] 按 `ChangeBattlePosHandler` 恢复范围中心、偶数尺寸半格偏移、最近合法格、占位检查和允许的交换。原请求仅证明提交内容及回包处理；本地站位持久化和失败恢复需与对应入口贯通。
- [ ] 技能 `SourceMoveList` 已接入拉到目标旁与 `RelativeToTarget` 直线进退，并按占位回退；尚未消费移动行为实体、`RelativeToSource`／`TowardsPathSides`、`DoMoveConditions`、传送、保持距离、固定方向、每段移动时长与移动中目标变化。
- [ ] AI 优先级仍未完整迁移：当前已接通固定版40组 `AiPriorityId/LastAiPriorityId`，并本地模拟 `ShorterMoveDist`、`MoreHitCount`、`LowerTargetHp`、`ShorterTargetDist`、`ShorterMovedTargetDist`、`MoreHitStatusCount`、`LeastMoveNearTarget`、`BiggerBodyRange`、`PriorGameCharacterType`、`PriorCloserTeammate`、`PriorPropCompare`、`PriorGameCharacterTypeGroups` 与 `ShorterFriendDistToTargetPos`，同时消费忽略状态等直接配置；初始与召唤怪物也已从固定 `MonsterInfo` 读取 `CharacterType/AiType`。已装配 `ActionComponent.ActionType` 与技能放行标志，并按用户确认实现嘲讽来源优先、混乱忽略阵营过滤及对应禁放行门槛。服务器对已模拟类型的精确评分公式尚无客户端执行依据；`SaferPos`、`PriorTargetEntityPos`、`ShorterEnemyTargetPosDistAndSameDir`、`PriorBiggerBodyHitCoverage`、`PriorRandom`、`StayAwayFromEnemies`、`AttachOperateChildSkillFriendHp`、`SummonSkillLandingPos`、`ShorterEnemyDistToTargetPos`、`BattleAISetting` 权重和按 `AiType` 选择默认策略仍未实现。
- [ ] 伤害与治疗、闪避／免疫／致盲、深渊、暴击／格挡及 CD 重置已按官方 prefab 和本地化文本显示，飘字曲线由 prefab 动画数据驱动；尚缺 `_Combine` prefab 选择条件、状态伤害／治疗、自定义动作、驱散、护盾量飘字、元素克制提示与上下限提示。
- [ ] 结算报告数据已由 `FightBattleReportBuilder` 生成，覆盖胜负／原因／回合、单位与技能统计；`FightEndReportPanelProvider` 的面板选择、个人／全员报告界面、护盾与状态次数展示及排行分数仍未接入。
- [ ] 恢复单位 collider、查询条件与玩家／NPC／怪物／宠物详情入口，验证 UI 遮挡不会点到场景目标；详情点击不新增手动施法。中途加入／灵魂拖拽仅在所属玩法确认保留时接线。

### 技能、CD、命中与状态（F16—F25）

- [ ] 从实际角色存档装配装备技能、槽序、等级／品阶、技能属性、被动、职业与其他来源状态，并在准备阶段变更后刷新快照。当前已会从技能档案读取方案主动技能、初始武器技能、阶位与槽位等级，并消费主动技能属性、被动属性、冷却及已装备被动的 `PassiveStatusIdList`；Map10 玩家与怪物的被动状态由同一官方绑定入口在战斗开始前装配。装备提供的技能、职业限制与其他加成仍待接入。
- [x] 无技能回合按用户确认的本地规则向远离敌人的合法格网位置移动（若无合法路径则留在原位），并推进个人回合、冷却、状态与胜负检查；核心回归覆盖了退避、无虚构攻击与冷却推进。官方客户端／共享代码没有提供该 AI 决策的服务器目的地规则。
- [ ] 处理技能消耗与开场条件：`CostType=1` 已按正数 `SkillCost` 扣除 HP，缺值按0正常释放；`CostType=2` 已消耗全部持续护盾并按用户确认的等比例结算，HP 消耗也显示官方技能消耗飘字且不计作命中。`Map10RuntimeSkill` 仍未携带每技能 CD，`MustHaveValidCost`、开场 CD 更新时点、次数限制、换波重置、CD 增减／停止／永久调整、职业限制与条件状态限制仍待接入；来源和目标动作合法性已按固定 `state_mutex` 矩阵接入。
- [ ] 完整消费被动技能的状态部分：固定版 `PassiveStatusIdList` 已转换为开战状态绑定并覆盖 Map10 玩家与怪物；属性变换与护盾按通用状态执行器结算，控制、破韧／恢复、生命层及其他未实现事件仍按相应组件缺口处理。
- [ ] 每槽主动施放、额外施放、状态阈值触发与状态层数重播已接入并生成独立施放事件；子技能、Replay 的其他条件与处理类型、白名单、触发上限和结束清理仍未贯通；不把所有负持续时间强制改为固定值。
- [ ] 覆盖命中图各分支、锚点与跟随、Delay／覆盖延时、链式／随机／后继命中、临时属性、吸血／附加伤害及多目标时点，维持确定性随机流与命中来源。命中延迟、`ActOnSouce` 锚点、`Scope` 范围命中、按距离计算的延迟、固定／治疗命中节点、命中驱散与命中位移已接入；仍缺命中特效锚点／跟随、Scope 的 `NotSameTarget` 等标志与区域过滤、按 Delay 分帧结算与覆盖延时。
- [ ] 将完整伤害／治疗配置、等级与位阶修正、特殊属性、场景修正和命中分支接入真实单位；命中 `DamageActionType` 的伤害／治疗／无动作已按官方区分，控制与状态类动作（50+）仍未结算。保留伤害衰减、概率和状态24181等已确认本地规则；公式单测通过不替代完整装备、怪物及状态集成。
- [ ] 状态基础字段与生命周期、阶段状态配置、命中附加状态（含随机单条）、驱散、位移、吸血、血量阈值与 HP 累积伤害触发已接入；状态 25256 的施加者回合开始、25177 的主人回合结束、24440／24476 的技能开始及 25011／60840 的技能结束触发已按固定配置接入；属性变换、持续／一次性／无敌护盾、自定义护盾外部输入及用户确认的 24181 单段减伤已接入。尚缺阶段状态面板、控制／破韧／恢复、生命层、共享层数、同来源条件、层数属性缩放，以及其余完整的应用／启动／行动／技能／命中／伤害／击杀／死亡／结束事件链。
- [ ] 贯通状态的应用、启动、回合／行动／技能／命中／伤害／击杀／死亡／层数变化／结束触发，条件检查、移除、转移及禁用／复活重启。当前装配仅有基础字段和部分衰减组件，不能视为完整事件生命周期。
- [ ] 将控制／禁止动作、属性修改、行动条、HP 门槛与限制、反击、累计／自定义伤害和治疗修正等组件接入执行顺序，并用实际配置覆盖来源与目标变化。
- [ ] 护盾类型、消耗与外部自定义盾值已接入通用命中结算；怪物 `HpPliesId` 已按固定版层数表和单位等级进入战斗快照。尚缺破盾后完整事件链、`ResilienceInfo` 的破韧／恢复及关联状态技能。
- [ ] 按原召唤配置完成怪物／宠物／格网对象等生成与属性、主人／来源、数量限制、入队、可选与占位、死亡／销毁联动。固定怪物召唤已接入上述通用执行路径；58140/58141 的3907、3908各三只容量和“其他召唤不占池”已有规则回归，18480 的24314召唤条件已实测三只可释放、四只不可释放。宠物／格网对象、`InheritProp` 的 `summon_monster_fix/additive/rank_additive_factor` 外部配置、来源结束及死亡／复活销毁链尚未完整接入。

### HUD、详情与报告（F26—F32、F39、F43）

- [ ] 技能品质、详情参数、时间轴头像、阵营和主角色标识绑定真实数据。玩家技能已装配，技能栏优先使用376个官方技能图标（Resources catalog 已在 Editor 探针载入并创建 Sprite，HUD 画面未验证），缺失时回退 `C_none`。Map10 时间轴的11种怪物头像按 battle-rules `Monster.IconId` 从固定 APK `gameheadicon_monster_map4countries_noatlas` 与 `mapsp_noatlas` 提取为 `G_<id>` Sprite（覆盖11/11），由 `OfficialBattleMonsterPortraitCatalog` 查询；无映射仍回退预制体默认 `G_3207`，玩家头像仍 `C_none`。已核对官方 [`GameRoundPlayerPortrait`](../../.tools/source-packages/official-code/client-source/GameRoundPlayerPortrait.cs) 将性别、职业和外观数据交给 [`HeadCharacterPortrait`](../../.tools/source-packages/official-code/client-source/HeadCharacterPortrait.cs) 生成动态纹理；该 Portrait 渲染／缓存链尚未迁移到原生角色模型。阵营无解析器时按阵营使用官方 `ImgWe`／`ImgEnemy`；技能品质、详情参数与真实玩家头像未接入。
- [ ] 恢复队列移动、行动态、受击／死亡动画、死亡后的移除与新单位插入；现有 NextTime 排序和间距布局只覆盖其中一部分。
- [ ] 绑定名称、单位 HP、血量变化、死亡标识与配置控制的显示条件，分别接入名称 HUD 和血条组件，不混淆 `GamePlayerHUD` 的职责。怪物 World HUD 使用固定版503项官方 unit-bar 数据，已显示 HP 与韧性；当前持久护盾按官方规则显示 `HP + 护盾`，最大值取 `MaxHP` 与 `HP + 护盾` 的较大值。固定版 `fightcharacter_3100/3102` prefab 都挂有官方 `BattleHpSpriteSlider`，其条形配置已在 `unit-bars.json`；无怪物 ID 的 `CharacterType=1` 单位现回退到 3100，显式怪物 ID／模板仍优先。`PlayerBattleHpBar` 是目标／成员面板组件，不是世界浮条。3102 变体选择与角色外观／性别的官方映射、名称 HUD 和显示条件尚未完整接入。
- [ ] 绑定 Boss 存活／敌方视野过滤、最多两栏与替换、名称头像、HP／盾组合、多层血量平滑变化、韧性及状态；已按官方条件筛选存活、已生成波次、敌方且 `MonsterType` 为3–5的 Boss，最多两栏绑定 `FrontItem`/`BehindItem`；固定版 `MonsterGroup.HpPliesId` 与等级表驱动当前层填充和剩余层数，HP／持续护盾合成按 `FightBossHpAndShieldBar` 公式恢复五张 `HpSprites[]`、`ShieldSprite`、两项 `ShieldColors[]`、最后一层空图，以及配置的 `MoveSpeed=0.1` 逐帧平滑和层数取模；韧性驱动恢复 prefab 中的官方 `Fill` 图并按 `current/max` 填充；状态列表按官方状态图标、背景与层数显示，并在无状态时隐藏。Boss“敌方视野”仍暂以波次已生成为准，名称头像及实际画面尚未通过 Unity Play Mode 验证。
- [ ] 恢复单位状态图标池、排序／排布、层数／回合／自定义数字、禁用／消失与详情输入；单位 World HUD 已按官方显示标记绑定图标及层数／回合数。阶段状态按来源去重并显示图标；点击状态条现使用官方 `stage_status` 与 `stage_status_item` prefab 展示固定版41条本地化名称／说明，并可点击背景关闭；自定义动态描述提供器、空态及完整层级与输入行为尚未通过 Unity Play Mode 检验。
- [ ] 贯通技能状态与事件：HUD 已从检查点显示冷却、次数耗尽和当前施放标记；Map10 玩家技能按固定技能规则的可用职业链计算并绑定官方 `ProfessionMismatch` 覆盖层，错配仍可进入本地战斗，符合官方战前可忽略提示的行为。固定 prefab 的 `GMDisabled` 对应 `ServerInfoManager.CheckFunctionDisabled`，离线没有其服务端禁用输入，故未伪造禁用状态；`Applied/Ended` 独立状态动画、准备阶段提示／方案切换、`GMDisabled` 与实际 Play Mode 画面验证仍未接通。
- [ ] 恢复宠物与进化／辅助技能、解锁前布局、无宠物占位、实际能量变化与放电动画；数据和养成规则按[宠物页](pets.md)接入。
- [ ] 恢复准备阶段方案入口、取消就绪再切换、技能不匹配／禁用／宠物提示与忽略分支；对应存档与规则沿[技能页](skills.md)，避免只开弹窗而不刷新战斗快照。
- [ ] 战斗统计已记录每单位施放／命中／承伤／伤害／治疗与每技能施放／命中／伤害／治疗；仍需按被动与状态的独立来源归因，补齐盾值、状态次数、每目标平均次数与占比、报告属性快照，并按报告配置选择个人／全员视图。秘境含盾的模式伤害与已确认通用 HP 损失归因分别按所属口径记录。
- [ ] 恢复原层级、动态子 prefab、文字与本地化格式、字体、滚动／遮罩／布局、窗口开关和事件解绑；验证比例缩放、遮挡与鼠标命中，不能仅检查 Button／Text 已生成。

### 倍速与其他公共控件（F33—F38）

- [ ] 补齐倍速的模式／权限限制与部分档位：当前按官方 `Speeds`／`SpeedUIs`／`PlayModeSpeeds` 解析、按钮文案与倍率分离、条件按本地存档判断并在切换时应用 `Time.timeScale`；`RecordPlaybackSpeedDefaultIndex`、团队／成员／观战差异及当前档跨战斗恢复仍待接通。
- [ ] 让战斗调度、命中／技能／死亡演出与原本应受倍率影响的 UI／声音响应同一速度；当前战斗步骤、移动插值和模型播放受 `Time.timeScale` 影响，时间轴仍使用未缩放时间，声音 Wwise RTPC 同步尚未接入。
- [ ] 恢复快进显示条件、1秒延迟、允许阶段、防重、不可用与失败提示及停用复位；本地快进须消费同一状态与事件，不能跳过必要结算或假定等于切高倍速。
- [ ] 接入镜头跟随／自由模式、准备限制、默认锁定与外部模式覆盖，恢复原相机坐标和范围，实际检查角色移动时的镜头行为。
- [ ] 按所属玩法保留范围处理托管、战斗内背包及红点、后台切出、录像保存意愿、录像／剧情跳过。每项须贯通条件、状态、提示与本地持久化；未确定的模式行为先定位消费链，不补造规则。
- [ ] 对观战对象、观战技能提供者、灵魂加入、决赛支援、聊天、特定 Boss 统计等原版能力保留可追溯记录，并按[离线访问范围](offline-access.md)、[副本](dungeons.md)及所属模式页执行关闭或替代。真实聊天不恢复联网；尚未确认的模式差异不擅自选 NPC 替代。

### 模型、动画、特效与音频（F40—F43）

- [ ] 将玩家真实复合外观、职业／武器／装备／皮肤与 NPC、宠物、召唤物接入共用场景；当前 Map10 玩家模型键为空，仍沿用地图 avatar，模型身份表不能替代玩家组合外观。
- [ ] 落实角色与 NPC 的免费 Spine 数据迁移：复用官方贴图、骨骼、网格与动画，独立转换 Unity 原生动画，补齐角色播放／换装。怪物采样网格已可由 `OfficialBattleNativeModelView` 原生播放；角色创建、玩家复合外观与未映射模型仍依赖现有 `Spine.Unity.SkeletonAnimation`，不能把第三方 runtime 存在写成迁移完成。
- [ ] 修复尺寸、方向和材质：朝向回调已改为保留目录缩放、只翻转 X 轴符号；Combat/Spine 材质文本核对的2620份中2555份主贴图为空，须查官方 atlas／纹理与绑定，不能据此认定源贴图缺失。
- [ ] 根据角色和怪物实际动作／Playable 映射待机、生成、移动、施放、受击、死亡、复活及召唤。怪物现有原生通用动作映射和非循环结束处理；缺少 `skill_01` 的111个怪物尚无精确技能动画选择，逐技能动画、生成／复活演出、延迟可见与中断仍未恢复。移动路径已按路径插值，但动作时序尚未按 Playable 完成条件调度。
- [ ] 还原命中范围、弹道／链式／来源到目标特效、挂点、层序、材质／shader、延时和销毁，并与每次施放／命中身份绑定，避免整回合只播放最终状态。
- [ ] 飘字仍未完整：已接入普通／暴击／格挡／治疗／深渊伤害、免疫／闪避／致盲与 CD 重置，使用官方节点树、字体、文本、颜色及可转换动画曲线；同目标飘字现按官方 `CharacterDamageUIManager.UpdateDamageUIOffset` 使用下一条 prefab 的 `Height` 和 `FightDamageSettings.DamageUISpacing` 堆叠，未改变新飘字基准位置。尚未证实 `_Combine` 变体选择规则，也未接入状态伤害／治疗、自定义动作、驱散和护盾量浮字。命中元素克制／抵抗图文、Playable 音频与 Wwise 依赖／速度响应仍待恢复；固定版 `DamageFloatSize` 只处理场景相机正交缩放，当前战斗 UI 采用原始画布节点与 `showTime`，Editor 堆叠断言和实际画面仍待 Unity 解锁后验证。

### 本地状态、资源与完成验证

- [ ] 战斗存档续战已覆盖非剧情 Map10 战斗：检查点经 `FightBattleCheckpointSerializer` 写入本地存档，重新进入同一阶段／放置点时恢复（关卡、波次、单位、技能、状态、格网、时间线、回执与统计均保留），结束／主动离开／异常终止时清除；剧情战斗不自动续战，跨模式入口、奖励回执与重复结算保护仍待贯通。
- [ ] 将胜负交接和奖励回执与所属玩法存档贯通，覆盖重复点击、重复加载、退出／重启及已结算记录，防止重复奖励；具体奖励公式或服务器决策未取得时按所属页说明缺口，不自行推定。
- [ ] 对尚未恢复的动态 UI、原相机与环境特效、模型、Playable、材质／shader、音频和 CAB 依赖按原清单补取、转换、导入并显式绑定；同步真实版本、来源、输入输出哈希。战场基础／昼夜几何与贴图现由 Unity Resources 显式加载，仍须在 Editor 和 Play Mode 核对全部入口的本地资源装配。所有入口运行时仅使用随游戏交付的本地资源，断网可用。
- [ ] 对新增或修复的阶段、未来波次、同技能多怪物、范围／移动、CD／消耗、状态事件、盾／韧性、召唤及恢复运行有意义的规则回归；既有97个公式向量及规则检查不覆盖全部待实现行为。
- [ ] 执行 Editor 导入／编译及 `Zjcs.Editor.ProjectTools.Validate`。本轮 Unity 桌面 Editor 未解锁，日志只有进程启动记录，没有脚本导入／编译结果；`check_unity_project.py` 仅通过静态 GUID／元数据检查，不能代替 Editor 验证。
- [ ] 在 Unity Play Mode 从真实入口完成准备／拖拽／方案、回合／波次、选敌／移动、技能与 CD、HUD／详情、倍率／快进／镜头、胜负／退出及重启恢复；用真实鼠标／键盘操作，检查画面、遮挡、状态和存档，不以直接调用回调代替输入。
- [ ] 实际查看玩家／NPC／怪物／宠物／召唤物、地面、血条、状态、时间轴、动作、特效与音频；覆盖不同职业／技能、重复怪物、Boss／多波、不同竖屏比例、死亡／复活等受影响分支。没有实际画面验证的项目继续标为未完成。
- [ ] 按改动影响检查 Windows 原生材质／动画／音频与离线打包；仅在涉及导出行为或用户要求时增量构建 Windows Player。公共模块完成不代替各地图／玩法的完整游戏验收，不据此发布或覆盖正式成品。

上述清单中的模式结算、技能／宠物养成、NPC 雇佣消耗与真人功能取舍仍以所属功能页为唯一规则维护点；本页维护公共接口、事件、表现和接线缺口，不复制或擅自修改模式规则。
