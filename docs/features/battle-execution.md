# 战斗执行

[总索引](../README.md)

状态：未完成

## 功能简介

本页记录 Unity 共用战斗规则、格网执行和公共 HUD 的实际迁移状态。范围为各玩法和地图共用的战斗部分，不包含副本、活动等模式各自的入口、专属规则与结算页。已有部分规则、官方 HUD 组件构建和共用会话控制器；`OfficialMap10GameRuntime.BeginBattle` 已调用控制器，玩家技能与被动属性可从存档装配，跨波次选敌错误已修复并有回归，但阶段机、倍速、完整 HUD、场景、动作演出与状态生命周期仍有缺口，不能视为通用战斗过程已完整还原。开场剧情的 `OpeningFightPlayback` 是记录播放，不替代通用战斗运行时。

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
| F13 | 自动选择目标与施法范围 | `FightSkillComponentInfo.Range` 明确从近到远排列，近战范围包含四向；`HitTargetType` 包含自己、敌人、友方、非自身友方、所有、无目标、主人、自身召唤物、其他真人友方、玩家与 NPC 友方。`SkillTargetType` 区分点、实体、可站立点、技能方向可站立点、跟随实体与锁定实体；`HitTargetActionType` 还限制可起效行为。 |
| F14 | AI 优先级、嘲讽与混乱 | `SkillAiCfg` 与技能 `AiPriorityId` 包含角色类别分组、属性最值、低血目标、忽略状态、嘲讽／混乱时能否释放、距离／朝向等条件。旧 `AiPriorityTypes/LastAIPriorityTypes` 已标废弃；仅读取旧字段或统一按血量百分比排序不能代表官方 AI。未取得的服务端评分不写成已复刻。 |
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
| F24 | 控制、属性、行动条与防护 | `FightStatusProp*`、`FightStatusLogicComponentInfo`、行动／状态禁止配置、HP 条件／限制、CD／重放组件及护盾组件共同改变可行动作、属性和结算；`FightStatusShield*` 区分普通、持续、一次性和无敌等类型。`FightRoleResilienceInfo` 还配置破韧状态、技能、伤害比例／上限与个人回合恢复，`HpPliesId` 对应生命层数据，不能用一个 HP 数字覆盖全部。 |
| F25 | 召唤与附属单位生命周期 | `FightHitSummonComponentInfo`、`FightHitSummonMonsterCfg/PetCfg/GridItemCfg/SmartGridItemCfg/VoodooDollCfg`、`FightStatusHitSummon` 与角色生成组件保留召唤类型、来源／主人、生成、计数及销毁关系；死亡与复活、来源结束后的状态／附属对象处理按实际组件，不能把所有召唤物当普通下一波怪物。 |

### HUD、详情与公共控件

| 编号 | 原始官方功能 | 官方入口、条件与行为 |
| --- | --- | --- |
| F26 | 进度与行动时间轴 | `GameProgressUI` 显示个人／全局回合、上限和预警；`GameRoundUI` 按 NextTime 和间距移动队列，`GameRoundCharacterItem` 恢复玩家／NPC／怪物头像、阵营、主角色标识、受击／死亡／待机／行动动画与移除时序。 |
| F27 | 单位名称、HP 与死亡标识 | `GamePlayerHUD` 负责玩家／NPC 名称与信息绑定；生命显示分别由 `GameCharacterHpUI`、`FightRoleFightHpView` 等消费，`GamePlayerHpStatus` 呈现相关状态。名称 HUD 不是完整血量实现。 |
| F28 | Boss 栏、护盾、多段血量与韧性 | `FightMainPanelBossUI.CheckShowBossCondition` 选存活、为 Boss、位于敌方视野内的怪物，最多同时显示两个并动态替换；`FightMainPanelBossUIItem` 绑定名称、头像、状态、HP／盾与韧性。`FightBossHpAndShieldBar` 根据多条血／盾组合切换填充和平滑表现；`MyFightRoleResilience` 按比值显示，最大值为0时隐藏。 |
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

[`FightBattleGrid`](../../unity/Assets/Game/Runtime/Official/Combat/FightBattleGrid.cs) 保存格网地形、占位与边阻挡，并按官方 `AStarUtils` 的四方向代价和 MoveMask 规则计算路径；[`FightBattleExecutor`](../../unity/Assets/Game/Runtime/Official/Combat/FightBattleExecutor.cs) 在技能超出范围时按 MoveDist 尝试移动，再重新选择目标。每个单位回合按技能槽检查可用技能，技能状态记录施放、次数限制与冷却，步骤事件记录移动、施放和目标。Map10 的 `BuildCheckpoint` 已从战斗 JSON 装配基础格网、出生位置和波次；`OfficialBattleSceneView` 现按步骤 `Movements` 的逐格路径以官方移动节奏推进模型位置，地图 avatar 通过 `AttachUnitTransform` 交给同一套移动播放，不再逐步瞬移。地形高度、身体占格与阵型字段仍未完整消费。

跨波次生成与可选状态已统一：`FightBattleTargeting.IsSelectable` 要求单位存活、可选中且不属于未生成的后续波次，目标选择、命中目标收集与移动占位共用这一判定；`FightBattleExecutor.Initialize` 将后续波次单位标为 `Ended` 并排除出行动队列，`SpawnNextWaveIfCleared` 在清场后恢复生成、入队和可选中状态。`FightBattleCheckpointRules.Validate` 在恢复及步骤结束时校验“后续波次不得标记已生成、其单位必须 Ended”。无可用技能的单位不再伪造跨图普攻，改为不造成伤害，避免绕过范围、消耗与命中图。

玩家技能已从统一存档装配：技能档案 `OfflineSkillProfile` 加入 `OfflinePlayerProgress` 并随角色主存档校验；选择初始武器时按 `rules.json` 的 `SkillOptionsWhenChooseSword/Wand` 授予初始主动技能并放入方案一槽位0。[`FightOfficialSkillItemCatalog`](../../unity/Assets/Game/Runtime/Official/Combat/FightOfficialSkillItemCatalog.cs) 解析 `rules.json` 的 `skill` 表得到技能道具到 `EcEntityId` 的映射；Map10 战斗入口按当前方案读取主动技能、阶位与槽位等级，并单独为每个怪物装配自身技能，避免此前共享绑定列表造成的重复槽位与串用。`FightOfficialCombatCatalog` 新增 `CalculateSkillProps`、`CalculatePassiveSkillProps` 与 `CalculateSkillCooldown`，按官方 `CalcSkillProps` 规则读取 `entity_prop`、`entity_prop_group_level` 与数值表；主动技能属性加入命中结算的来源属性，被动技能属性在推导 MaxHp、速度和移动距离前并入单位属性。技能冷却按 `BattleFormula.SkillCD` 从 CD 属性计算，原数据没有 CD 属性时为0，不自行编造冷却值。

公共 HUD 的官方 prefab 树、sprite、字体和 Spine 骨骼输入由 [`prepare_unity_battle_assets.py`](../../scripts/prepare_unity_battle_assets.py) 按来源哈希恢复到 `Assets/Official/Combat`；当前已恢复的 HUD 子集有30个 prefab、701个 sprite。[`OfficialBattleHudCatalog`](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudCatalog.cs)、[`OfficialBattleHudRenderer`](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudRenderer.cs) 和 [`OfficialBattleHudView`](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudView.cs) 已有隔离 Unity 工程导入及逐 prefab UI 组件树检查。视图支持回合文本、按 `GameRoundUI` 的 NextTime 排序和间距参数呈现行动时间轴、技能图标、冷却、次数耗尽态，并可由步骤事件刷新行动态和技能施放态。玩家已有实际技能，技能栏可显示技能图标与冷却；Map10 仍把技能图标和时间轴头像解析成同一张 `C_none` 占位图，未传阵营图标，因此不能以视图接口存在认定 HUD 的实际内容已接通。原版 `GameSkillIcon` 的常规技能图标不是手动施法按钮，prefab 中 `DebugBtn` 保持禁用。

怪物外观来源已恢复为407个 Spine 模型绑定，343个怪物 ID 映射见 `OfficialBattleModelCatalog` 及 `model-identities.json`；[`OfficialBattleSceneView`](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.cs) 可按调用端提供的模型键和网格映射实例化骨骼，并按 `Spawned` 延迟显示后续波次，同时把步骤移动路径按每格0.5秒的节奏插值到模型或已挂接的 avatar。Map10 对玩家返回空模型键，改用地图 avatar 并经 `AttachUnitTransform` 驱动；对怪物调用模型目录解析，但把所有步骤动画固定为 `idle_01`。朝向回调改为保留模型目录缩放、只翻转 X 轴符号，此前覆盖缩放的问题已修。当前视图直接依赖 `Spine.Unity.SkeletonAnimation`，尚未落实角色动画的[原生转换方案](../asset-management.md#spine-角色动画迁移决策)。模型绑定数不代表贴图、实际尺寸、动作及画面已通过。

[`OfficialBattleRuntimeController`](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleRuntimeController.cs) 提供共用战斗会话入口：`Begin` 可新建或恢复检查点，`Advance` 执行下一步并刷新官方回合文本、行动时间轴、技能槽及战斗模型；`StepCompleted`、`HudControlClicked` 和 `SpeedChanged` 将步骤、面板交互与倍速变更转交给玩法入口。控制器要求入口传入检查点、网格映射、技能／时间轴图标、角色模型键、动作及朝向绑定；它不代替各玩法的阶段数据装配。`OfficialBattleSceneView.Refresh` 现按 `FightBattleWaveState.Spawned` 延迟实例化未生成波次的单位。

倍速已按官方配置接通：`FightBattleSpeedSettings` 从 `game_settings.Battle` 读取 `Speeds`／`SpeedUIs`／`PlayModeSpeeds`，按钮文案与实际倍率分开；`ConfigureSpeeds` 用 `OfficialCharacterCatalog.EvaluateCondition` 判断速度所需官方条件（等级、地图解锁与组合条件，缺少依据的条件保持锁定），`CycleSpeed` 只在可用档位间切换，并通过 `Time.timeScale` 同步战斗步骤、移动插值与模型动画。HUD 已挂载官方 `FightMainPanelSpeedUI` 按钮，`RefreshSpeed` 绑定当前文案与锁定图标；`DisposeSession` 恢复时标。快进、阶段机、Pause 的业务语义与声音 RTPC 同步仍未接入。

每次命中都会生成 `FightBattleDamageEvent`，记录伤害序号、来源、目标、技能实体／道具、命中节点、伤害类型、暴击／格挡／闪避／免疫／致盲标记、结算值、绝对伤害与命中前后 HP；命中动作按官方 `FightHitBaseComponentInfo.DamageActionType` 区分：5 治疗（即使技能类型不是治疗也会回血）、4 伤害、0 无动作（不结算 HP、不写伤害事件，但技能仍记为已施放）；这些事件随步骤回执保存，可用于飘字与战报追溯。`FightBattleStatistics` 同时累计每单位的施放、命中、承伤、造成／承受伤害与治疗，以及每技能的施放、命中、伤害与治疗，数值取实际 HP 结算结果，因此已包含衰减、溢出与致死截断。 `FightBattleReportBuilder` 在此基础上生成结算报告数据：阶段、胜负、原因、回合、双方血量、总伤害／治疗，以及逐单位的施放／命中／承伤／伤害／治疗与逐技能的施放／命中／伤害／治疗；官方结算面板 prefab 尚未接入，当前只有数据结构与回归。

伤害飘字已接入：`OfficialBattleSceneView.ShowDamageFloats` 在每次新步骤后按伤害事件在受击者格位生成 TextMeshPro 飘字，数字取实际结算值，字体用官方 `TMP_FontAsset`，颜色来自官方 `battle-hud/rules.json` 的 `damage_settings`（7 种元素 × Normal/Crit/Block、治疗与默认色，由 `scripts/prepare_unity_combat_damage_colors.py` 导出到 `Resources/Combat/damage-colors`）；闪避／免疫／致盲因缺少本地化文本暂不生成数字，官方飘字 AnimationClip 尚未迁移，暂用 0.9 秒上升淡出，与开场战斗飘字的本地处理一致。

`FightBattlePhaseMachine` 按官方 `GameStatus` 提供 Preparing、PlayStart、Play、Pause、EntryTransition、EntryTransitionFinished、BattleEnd、BattleEndTransition、BattleEndTransitionFinished、End 的合法迁移与准备倒计时；单机主线可跳过准备，`Ready` 结束倒计时，倒计时归零只触发一次自动开始请求。`OfficialBattleRuntimeController` 已接入该状态机：开始即进入 PlayStart→Play，暂停只允许在 Play 与 Pause 间切换，步骤产生结束结果时进入 BattleEnd，释放时进入 End 并复位时标，阶段变化通过 `PhaseChanged` 交给玩法处理。准备面板、就绪 UI、倒计时显示、入场与结束演出尚未接各模式面板。

技能动作与命中时序已按官方配置进入步骤事件：`FightBattleSkillDefinition` 保存 `ActionDuration`、`AllEndedActionDuration`、`AllRelevantEndedActionDuration`、`Duration`、`AllEndedDuration`、`AllRelevantEndedDuration`；`FightBattleHitRoot`／`FightBattleHitEntity` 保存命中引用边上的 `Delay`、`ActOnSouce` 锚点与 `IgnoreAICount`，同一实体的多次根引用各自保留自己的延迟与锚点。执行器按根引用收集命中图、把根引用配置写到该根的第一个命中节点，并在伤害事件中记录 `Delay`／`Anchor`；步骤结束按“最后一个命中延迟 + AllRelevantEnded／AllEnded，或固定时长”计算演出时长，移动再按每格0.5秒补足，写入 `FightBattleStepEvent.Duration`／`ActionDuration`。`OfficialBattleRuntimeController.LastStepSeconds` 把该时长交给 Map10 的推进定时器，替换固定0.35秒。当前伤害仍在同一执行步骤内结算，尚未按 Delay 分帧结算或等待每个命中动作完成。

Map10 阶段行中的官方上下文已进入 Unity DTO：`Type`、`IsPlayerRoundLimit`、`PlayerRoundLimit`、`ShowMainPanelDelay`、`SkipPrepareStatus`、`UserStart`、`LeaveInPreparing`、`LeaveInPlaying`、`Background`、`ChangeCameraMode`、`JoinInMiddle`、`DismissTeamWhenLeaveOrEnd` 及 `CanFastforward`。检查点新增个人回合上限字段，执行器在主玩家个人回合达到 `PlayerRoundLimit` 时结束战斗并以结果原因4区分全局超时；战斗进行中若阶段 `LeaveInPlaying=false`，Map10 拒绝离开请求并提示，避免把官方禁止离开的阶段当作可随时退出。`ShowMainPanelDelay`、`SkipPrepareStatus`、`CanFastforward` 等字段已读入但尚未驱动面板延迟、准备阶段与快进。

Boss 栏已接入官方 `FightMainPanelBossUI`：`RefreshBoss` 只选存活、波次已生成、敌方且 `MonsterType` 为3–5的单位，最多两个，按顺序绑定 `FrontItem` 与 `BehindItem`，HP 用 Filled 的 `TopImg.fillAmount`，名称取官方 monster 表的 `CustomTypeName`（无则 `Memo`），无名称时隐藏文本；无 Boss 时隐藏整个面板。此前把控制台编码显示问题误判为载荷乱码，已用码点核对更正：`3206` 的名称为“野猪”。盾、多层血量、韧性与头像暂时隐藏。Boss 状态列表已用官方 `status_item` 绑定：按状态数量生成条目，按 `StatusType` 使用官方 `BG_Buff`／`BG_Abnormal` 背景，叠层大于 1 时显示层数；每个状态的专属图标尚无映射，图标槽保持隐藏，避免显示无关图片。

战斗检查点可持久化：`FightBattleCheckpointSerializer` 以版本化 JSON 保存并还原检查点头（阶段、模式、回合上限、个人回合上限、随机种子、序列与计数）、单位（属性、技能、技能状态、状态实例、身体占格）、波次、格网、行动时间线、步骤回执、命中伤害事件与统计；保存按字段声明顺序稳定输出，读取按字段名还原并执行严格检查点校验。Map10 每5步把进行中的非剧情战斗写入本地存档（阶段与放置点标识、保存时间），重新进入同一阶段和放置点时提示“继续上次战斗”并用 `Restore` 恢复；战斗结束、主动离开或异常终止时清除存档，剧情战斗不自动续战。

技能图标已接入：`scripts/prepare_unity_combat_skill_icons.py` 从 `icons.json` 解析技能道具到同名图标 PNG（以文件名中的道具 ID 为准；同一文件的碎片 ID 折叠为同一技能 ID），读取现有 PNG 尺寸与归一化 pivot、ppu，生成 `Assets/Game/Resources/Combat/OfficialCombatSkillIconCatalog.asset` 和带输入／输出哈希的来源清单。运行时 `OfficialCombatSkillIconCatalog.Find` 用官方纹理即时创建 Sprite 并缓存，Map10 技能栏优先使用真实图标，缺失时回退 `C_none`；当前绑定376个技能图标；纹理取自 Wish 模块已恢复的官方图标目录，若该目录未随仓库交付则图标回退占位。

状态基础生命周期已按官方 `FightStatusComponentInfo` 落地：`FightBattleStatusState` 保存 RoundTarget、DurationRound、CurDurationRound、StartRound、RoundUpdateTiming、RemoveAtRoundTargetDestroy、ActionBehOfSameStatus、CanDispersed、IsAddDurationRound／MaxAddDurationRound、IsResetDurationWhenStack、叠层层数与随机步距、MaxStackedCount、来源类型等基础字段；`FightStatusLifecycle` 按官方公式计算剩余回合（`Max(0, CurDurationRound - (round - StartRound))`，负数表示无限），实现叠时扩展与上限、叠层上限、Parallel／Immediate／Discard 同名状态处理，并在回合开始／结束时按 RoundTarget 与 Timing 到期移除、对 RoundTarget 已销毁的状态执行移除。执行器在开战初始化回合、每个单位回合开始／结束及步骤末调用该生命周期。状态的属性、护盾、控制、事件触发与命中施加仍未实现。

技能自身位移已接入 `FightSkillComponent.SourceMoveList`：按 `Delay` 排序保存`FightBattleSkillMove`（移动行为 ID、偏移、朝向模式、距离、条件标记）；执行器支持`Distance=-1` 拉到目标旁可站立点与 `Distance>=0` 且 `RelativeToTarget` 的直线进退，按格网通行、占位与身体占格寻路，目标格被占时自动尝试更短距离，产生逐格 `Movements` 并更新朝向；带 `DoMoveConditions` 或非 `RelativeToTarget` 的配置保持不执行，不用猜测替代。初版 `skill-rules.json` 只包含 6 类 EC 组件（Skill／Status／四种 Hit），但战斗目录同时装载的 `battle-rules.json` 已经带有 7920 个实体及其子组件：`FightStatusPropComponent` 664、`FightStatusShieldPersistentComponent` 53、`FightStatusDamageReducePerComponent` 34、`FightHitFixedComponent` 1936、`FightRoleFightComponent` 570 等。因此状态属性、护盾与减伤的配置数据无需额外提取即可读取；`FightOfficialCombatCatalog` 新增 `HasComponent`／`TryGetComponent` 供效果实现按实体读取组件（已用状态 26004 的减伤组件与 60031 的护盾组件验证）。`scripts/prepare_official_combat_entities.py` 仍保留，但实测它相对 `battle-rules.json` 只额外补出 23 个实体的 `VisibilityHideComponent` 等世界显示组件，没有新增战斗效果组件，不参与战斗结算。状态的属性／护盾／减伤等效果结算仍待实现。

命中附加状态已按官方 `FightDamageComponent.StatusList` 接入：`FightBattleHitDefinition` 保存 `StatusId`、`TargetType`（1 技能来源／2 受击目标）、概率 `BasePercent` 与 `LessThanHpPer` 条件；命中结算后执行器按条目选择来源或目标、检查血量条件与概率，再经 `FightStatusLifecycle` 施加状态（起始回合留待拥有者的下次回合更新）。`battle-rules.json` 中有 470 个伤害实体配置了状态列表，官方技能 12860 的命中即会附加状态 24078；持续 0 回合的状态会在同回合结束时按生命周期移除。`FightDamageComponent` 的驱散、位移、切换形态、子技能与状态随机单条等其余字段尚未实现。

命中节点补上了 `FightHitFixedComponent`（`battle-rules.json` 中 1936 个固定／治疗命中），此前这类节点不会被装配成命中定义；现在与链式／随机／自选命中一样读取伤害字段、范围、动作类型与附加配置。驱散按官方 `FightDamageComponent.DisperseStatus` 接入：命中后按 `TargetTypes` 与状态 `CanDispersed` 过滤，按 `Count`（0 表示全部）与 `IsRandom` 决定移除哪些状态，并按 `BasePercent` 判定概率；`AffectedByProp` 与 `FilterCondition` 尚未参与判定。核心规则里 66 个伤害实体配置了驱散。

状态列表还支持官方 `StatusListRandomOne`：为 true 时从命中状态列表中随机取一条尝试，为 false 时按顺序逐条尝试；`OverrideRateByCharacterTypes`、`SkillCostHp`／`DamageCostHpPer` 与 `AffectedByProp` 仍未参与判定。

吸血按官方 `FightHitExtHemophagiaComponent` 接入（`battle-rules.json` 中 16 个命中节点）：读取 `HemophagiaPropType`，按来源单位的该属性与本次实际伤害计算回血并受最大生命限制，回血写入治疗事件与统计。该属性未出现在 `percent_value_props` 表中，本实现按 10000 基准的百分比处理（显式本地假设），属性缺失或为 0 时不回血，治疗动作本身不再产生吸血。

命中位移按官方 `FightDamageComponent.MoveCfg` 接入：默认 `Distance=-1` 表示不位移，`Distance>0` 时沿来源到受击者的方向击退、`TowardsTargetPos=true` 时改为拉近，支持偏移与取消斜向，目标格被占或越界时自动尝试更短距离，位移作为受击者移动事件输出；带 `DoMoveConditions` 的配置保持不执行。核心规则里 134 个伤害实体配置了有效位移，`BasePercent`／`AffectedByProp` 缩放与移动行为实体仍未消费。

阶段状态已按官方 `fight_stage_status` 接入：`FightStageStatusCatalog` 从 `rules.json` 读取 `stage.Status` 到 ClassId 的映射与每行配置（Status、CharacterStatus、GlobalRoundStatus、FactionGlobalRoundStatus、TargetIdStatus、ProfessionStatus、GlobalRoundStatusDelay、ReleaseEveryGlobalRound、ViewStatusBlackList），视图列表与官方 `FightStageStatusInfoParser.GetViewStatusList` 一致（并集去黑名单）。`FightStageStatusRuntime` 在开战时按全体、角色类型、职业与目标怪物 ID 施加状态，并在全局回合达到 Delay 的整数倍时施加全局／阵营全局状态；检查点保存该配置并可往返，负延迟会被拒绝。战场没有独立阶段实体，阶段级状态暂施加到全部单位。Map10 中只有 9910 阶段配置了阶段状态（ClassId 1），而该阶段在入口处直接完成、不进入战斗，因此当前主线战斗不会触发阶段状态；状态本身的效果仍未接入。主面板阶段状态条已按检查点配置显示：`RefreshStageStatus` 在 `ViewStatusIds` 非空时显示官方 `StageStatus` 节点，否则隐藏；图标槽保持隐藏，因为固定 APK 的 `fight_status.g.bytes` 只有 1 字节（状态图标／背景表为空），官方解析器同样取不到图标。阶段状态弹窗（名称／描述列表）仍未接入。

目标选择已按官方 `FightSkillComponentInfo.Range` 的近到远顺序优先：未配置 AI 优先属性时，候选按其在 `Range` 列表中的下标排序（越靠前越近），射程内目标优先于射程外目标，射程外仍保留给移动规划；显式 `AiPriorityPropType` 优先属性仍先于射程顺序，完全并列时沿用原有属性／血量与 ID 比较。命中范围 `HitCfg.Scope`／`FightHit*ComponentInfo.Scope` 的解析与范围命中见下一段。

命中范围 `Scope` 已接入：`FightBattleHitDefinition`／`FightBattleHitEntity` 保存 `HitScopCfg.Pos` 偏移与 `AllowEmptyScope`；命中按锚点取中心（`OnTarget` 用主目标位置，`OnSourceCastPos` 系列用施法者位置），从技能目标阵营候选中筛选落在范围内的单位，`TargetMode=1` 保留范围内全部目标、`TargetMode=0` 取最近一个、随机命中只在范围内随机；范围为空时不再命中范围外单位。官方 12860 的链式命中为单格范围，16100 的随机命中为48格范围，另有 `NotSameTarget`／`NotSameEmptyScope`／`FilterCondition`／`FilterOutOfGroundScope`、Scope 延迟与旋转尚未消费。点目标技能（`SkillTargetType=Point`）在 `Range` 内没有敌人时，会在范围内有可命中单位的情况下以施法者为中心释放（如 16100 的 `Range=(0,0)` 自爆范围），`FindReadySkill` 与执行器使用同一检查；没有可命中单位时不释放。

命中延迟按官方 `CalcSkillHitDelay` 计算：`FinalDelay = Delay + 距离 × DelayDistFactor`，距离取命中起点到命中点的直线距离（格坐标欧氏距离，未计偏移与旋转）；`OnTarget` 命中按施法者到目标计算，`OnSourceCastPos` 系列锚点距离为 0 而保留配置延迟。伤害事件保存每个目标各自的最终延迟，步骤时长取其中最大值；事件仍按目标顺序记录，不按延迟重排。

规则检查入口为 [`FightCombatValidation.RunSmokeTest`](../../unity/Assets/Game/Runtime/Official/Combat/Editor/FightCombatValidation.cs)，菜单为 `Zjcs/Combat/Run Rule Smoke Test`。`Zjcs.Editor.ProjectTools.Validate` 还会装载官方战斗目录并运行规则检查。实际验证范围如下：

- 规则：已运行 `scripts/test_unity_combat.ps1` 通过，覆盖97个固定公式样例及战斗目录装配、技能／状态、命中图、衰减、胜负、检查点恢复、随机流、时间线、目标选择、多目标衰减、技能道具到 EC 实体映射、初始武器技能、技能／被动属性计算、官方倍速表与条件档位、命中伤害事件与战斗统计、官方命中延迟与技能动作／持续时间映射、官方怪物名称查表，以及跨波次生成与可选状态回归（未生成单位不中选、不被命中、不占位，清场后立即可选）；阶段机另由 `scripts/test_battle_phase_machine.ps1` 通过，覆盖准备倒计时、就绪、一次性自动开始、Play／Pause、入场与结束迁移、释放及非法顺序拒绝；步骤时序由 `scripts/test_battle_step_timing.ps1` 通过，覆盖命中延迟、根引用锚点／延迟、动作时长、持续时间回退与移动补时；个人回合上限由 `scripts/test_battle_stage_limits.ps1` 通过，覆盖达到上限以原因4结束、结束后拒绝继续执行、关闭上限与0上限不生效；检查点持久化由 `scripts/test_battle_checkpoint_persistence.ps1` 通过，覆盖版本化载荷、全字段往返、稳定重存、恢复重放与续战、非法载荷拒绝；状态基础生命周期由 `scripts/test_battle_status_lifecycle.ps1` 通过，覆盖剩余回合、无限持续、叠时扩展与上限、叠层上限、同名状态合并规则、回合时点到期、销毁目标清理与执行器集成。技能自身位移由 `scripts/test_battle_skill_moves.ps1` 通过，覆盖拉到目标旁、正向距离、占位回退与不支持配置跳过；规则套件同时校验官方技能 15980 的 `SourceMoveList` 映射。射程顺序选敌由 `scripts/test_battle_target_range.ps1` 通过，覆盖近到远顺序、配置下标顺序、射程内优先与 AI 优先属性前置；规则套件同时校验官方技能 12860 的 `Range` 顺序。命中范围扩展由 `scripts/test_battle_hit_scope.ps1` 通过，覆盖目标锚定 AoE、施法者锚定 AoE、空范围不命中、随机命中限定在范围内与点目标技能以施法者为中心释放；命中距离延迟由 `scripts/test_battle_hit_delay.ps1` 通过，覆盖按距离缩放、源锚点保留配置延迟与范围命中的逐目标延迟；命中动作由 `scripts/test_battle_hit_action.ps1` 通过，覆盖治疗动作、无动作命中与普通伤害动作；阶段状态由 `scripts/test_battle_stage_status.ps1` 通过，覆盖官方阶段映射、视图黑名单、开局按角色类型施加、全局回合节奏、阵营范围、检查点校验与往返；命中附加状态由 `scripts/test_battle_status_application.ps1` 通过，覆盖官方状态列表映射、目标／来源选择、概率与血量条件；命中驱散由 `scripts/test_battle_status_disperse.ps1` 通过，覆盖官方驱散映射、状态类型过滤、可驱散标记、数量上限、随机选择与概率；命中位移由 `scripts/test_battle_hit_move.ps1` 通过，覆盖官方位移映射、击退、拉近、越界回退与条件跳过；状态／驱散／位移组合的检查点回放由 `scripts/test_battle_effect_replay.ps1` 通过，覆盖随机单条状态、顺序状态、存档重放一致与继续执行；命中吸血由 `scripts/test_battle_hemophagia.ps1` 通过，覆盖官方属性映射、百分比回血、上限、属性缺失与治疗命中不回血；结算报告由 `scripts/test_battle_report.ps1` 通过，覆盖报告头、单位行、技能行、合计与血量；伤害飘字与颜色导出当前只有 Roslyn 编译级验证（主工程被并行会话占用，未做 Play Mode 画面确认）。Map10 真实数据整场战斗由 `scripts/test_battle_map10_integration.ps1` 通过：从 `Map10/battle-runtime.json` 读取怪物最多的阶段（GameId 1004）网格、波次、怪物属性与主动技能，用合成玩家（5000 生命、MoveDist 8）装配官方技能 12860，并通过 `CalculateSkillProps` 传入技能属性后跑完整场战斗，校验每步 HP 边界、步骤回放与结束结果（当前 48 步、95 个伤害事件、9 只怪物全部击杀，获胜结束）；玩家面板属性为测试用合成值，不代表正式角色数值。该回归同时确认：技能伤害必须携带 `CalculateSkillProps` 结果，否则伤害百分比属性缺失会算出 0 伤害。规则套件同时校验官方 16100（48格）与 12860（单格）的 `Scope` 映射。
- 素材：`python scripts/prepare_unity_combat_skill_icons.py --verify` 通过，376个技能图标绑定、pivot／ppu 与输入输出哈希一致；新 catalog 资产尚未在 Unity 编辑器中导入，运行时 Sprite 创建与实际图标画面未验证。
- 静态：`python -B scripts/check_unity_project.py` 通过。材质文本核对发现 Combat/Spine 下2620份 `.mat` 中2555份 `_MainTex` 为 `{fileID: 0}`，怪物材质也在其中；这是绑定缺口，不据此宣称官方原始贴图缺失。
- Editor：HUD 的隔离组件树检查覆盖30个 prefab、701张 sprite、228个 Image、85个文本、26个按钮、23个布局组、43个适配器和2个滚动视图。本轮主工程检查使用 Unity 2022.3.57f1c2、Direct3D 11 启动，编译已越过此前的 Map10 对话／剧情与商城交易错误，止于 `OfficialMap10GameRuntime` 引用 `TMPro` 时所在程序集缺少 TextMeshPro 引用；已补 `Zjcs.Presentation.World` 的 `Unity.TextMeshPro` 引用。随后另一 Unity 实例持续占用工程，未能在编辑器内重跑；改用该 Unity 版本的 Roslyn，以主工程已编译程序集与 UnityEngine 模块为引用分别编译 `Zjcs.Combat.Official`、`Zjcs.Offline.Skills`、`Zjcs.Offline.Progression`、`Zjcs.Offline.CharacterCreation`、`Zjcs.Presentation.Combat`、`Zjcs.Presentation.World`，均无编译错误。后续轮次继续以同样方式编译 `Combat`、`Presentation.Combat` 及含阶段字段的 HEAD 版 `Presentation.World` 通过。本轮再次在 Unity 2022.3.57f1c2 batchmode 运行 `ProjectTools.Validate`：脚本编译仅因并行会话未完成的 `Assets/Game/Runtime/Presentation/Wishes/WishScreenController.cs(417)` 元组 `.ItemType` 错误失败，战斗、HUD 与地图程序集未报错；该次运行已完成资源导入（45 项更新），但技能图标 catalog 的实际导入结果与 Boss 栏、倍速、图标画面仍未验证。另尝试新建独立空工程导入技能图标 catalog 与对应 PNG 以绕开主工程编译错误，第二个 Unity 编辑器停在 IL Post Processor 连接等待，未执行到校验方法，已删除临时工程；其后并行会话修复了 WishScreenController，重新运行 batchmode 校验，脚本编译改为停在 `OpeningParticleSystemFactory.cs(220)` 的 `Array` 方法／类型误用（同样属于并行未完成改动），战斗程序集仍未报错；catalog 的导入与运行时 Sprite 创建仍待验证。随后又三次尝试在主工程运行资源探针：第一次因并行实例占用工程直接退出；第二次探针所在程序集缺少 `Zjcs.Combat.Official` 直接引用而编译失败（已改探针为仅调用 Presentation.Combat）；第三次编译停在并行会话未完成的 `HomeHudView.TreasureGacha.cs` 语法错误。主工程在并行改动稳定前无法完成编辑器内探针。并行会话修复后，本轮重新运行 batchmode 探针成功：`OfficialCombatSkillIconCatalog` 由 `Resources` 载入成功，376 项、`Find(10012)` 生成 256×256 Sprite、未知 ID 返回空；`Zjcs.Editor.ProjectTools.Validate` 通过（含战斗规则 smoke test 与 Map10 校验），编辑器脚本编译、资源导入不再被阻塞。临时探针与日志已删除。新增 Play Mode 战斗验收入口 `Zjcs/Validate/Map10 Battle Play Mode`（`Zjcs.Editor.OfficialMap10BattlePlayModeAcceptance.Run`，需 GUI 编辑器运行）：用临时存档启动 Map10，经 `StartEntityStage` 进入真实战斗，检查战斗进入 Play、步骤序列与伤害序号、技能图标、时间轴卡片、回合文本、倍速与暂停。首次运行进入 Play Mode 后暴露玩家战斗属性错误：Map10 用等级／位阶原始表构建属性，只得到 `Base*` 属性、缺少 `MaxHp` 等最终属性；已改为使用官方属性管线 `EvaluateProps(...).OutOfFightBattleProps` 并保留被动技能叠加（`8acabad`）。修复本身已用已编译的 `Zjcs.Offline.Progression` 程序集直接驱动官方属性管线复核：等级1／位阶1 的 MaxHp=327、30／3 的 MaxHp=4676、100／10 的 MaxHp=43199，均大于0；而原始等级表 30／3 只有 `BaseMaxHp`(50)=4290、没有 `MaxHp`(3)，确认旧写法必然构建失败。修复后的完整运行重试被并行 Unity 实例占用／系统饱和阻塞（主工程实例停在启动阶段），Play Mode 逻辑与画面验收仍未取得通过结果；技能图标、Boss 栏、倍速与飘字的实际画面也仍未验证。这不替代 Unity 编辑器自身的导入、域重载与 Play Mode。
- Play Mode：当前通用战斗的真实入口、鼠标／键盘命中、HUD 画面、角色／怪物动作、特效、音频和退出恢复均尚未完成运行验证；主工程编译阻塞不能被静态检查或规则回归代替。未构建或验证 Windows Player。

## 自定义实现

战斗按本地单机方式设计，不依赖运行时服务器。原客户端共用入口和面板行为分别见 `FightSceneRoot`、`MyFightGridMover`、`MyFightGridGround`、`MyFightRoleGrid`、`FightMainPanelSkillUI`、`GameRoundUI`、`FightMainPanelBossUI`、`FightMainPanelBossUIItem` 和 `FightMainPanelStageStatus`。现有迁移只覆盖已经接入的规则、格网算法、部分战斗模型数据及 HUD 构建；没有把尚未迁移的原客户端行为写成已完成。

以下为已确认的单机规则要求；是否已由当前 C# 执行器落实，须逐项核对和验证，不能沿用旧引擎的通过结论。

- 个人回合按从左到右检查所有技能，每槽最多主动一次，每招使用完整 MoveDist；移动、伤害、被动和死亡后重新评估下一招。不因相同增益权重自行禁止重复技能。
- TriggerDamageAddToDamageSource：被动触发伤害归属被动，技能效果伤害归属所属技能；统计实际生命损失。确定性 AI 使用原候选、条件、移动、范围与队列参数，不声称复刻未取得的服务端评分。
- 技能／秘法效果之间的外部概率修正乘算；固定概率不受外部修正，直接加状态无需判定。官方 EffectRate 公式及 EffectRateValue - EffectDodgeValue 加减项仍原样使用，不能据补证改成全乘算。
- 状态效果默认触发后立即结算，明确的回合时点保留：25256 在回合开始触发12980，25177 在主人回合结束触发50180；生成的行动条状态立即处理。作用于当前行动者的行动条调整在行动结束重新入队时用于下一次行动。
- DamageFalloff 的同目标递减分别计数，整体递减跨目标累计；首击计数1且不减伤，30%递减的第二击乘70%，之后按原幂次继续。施放相互隔离，子技能沿用父施放编号，行动结束清理临时计数；明确的起始次数仍按原配置。命中后附加状态时补记首击属于已确认本地接线，未取得服务端执行器。
- 状态24181 按用户补证先减伤再扣盾／HP，每层只降低一段，其余条件沿原配置。
- FightStatusDamageModification.Run 在本项目暂弃用；无已定位原玩法调用点，不将调用来源未知单列缺项，不称官方弃用，兼容扩展不冒充官方服务端流程。
- 本地检查点保存规则状态、随机流、时间线和步骤回执；奖励回执及退出恢复尚未接入战斗入口。

## 尚未实现

以下清单覆盖上面 F01—F43 的公共战斗迁移范围；未勾选表示尚有实现、接线或验证工作，不表示相关官方资源不存在。已具备的算法、配置、资源或接口以“完整还原部分”的实际边界为准。模式功能另有保留／关闭／替代决定时，执行该决定，不以此清单擅自重新开放。

### 会话、阶段与进度（F01—F07）

- [ ] 将各战斗入口需要的 Stage、GamePlayMode、PVE／PVP 上下文、胜负条件、回合限制及官方配置装配到共用会话。当前执行链部分公式上下文固定 `stageType=0/isPvp=false`；须按保留玩法传递，不能据此开放已关闭 PVP。
- [ ] 接通准备、开始、战斗、暂停、入场过渡、战斗结束、结束过渡和释放事件及对应面板，保留模式跳过准备、延迟面板和外部覆盖条件。阶段机与控制器事件已具备，Map10 仍按定时器调用 `Advance`，尚未把各阶段接到面板、演出与模式入口。
- [ ] 接入就绪、取消就绪、准备倒计时、自动开始与失败提示；`FightBattlePhaseMachine` 已提供准备倒计时、`Ready`、一次性自动开始请求与就绪校验，缺准备面板、倒计时显示与失败提示。仅在适用的本地模式恢复，团队或服务器时间的替代按所属页已确认范围处理。
- [ ] 接通个人／全局回合、临近上限的颜色与倒计时、不同阶段上限和上限结果；个人回合上限已按 `StageInfo.IsPlayerRoundLimit/PlayerRoundLimit` 在检查点执行，达到上限以结果原因4结束并可区分全局超时；HUD 仍只格式化全局回合与上限，个人回合、颜色预警与倒计时未显示。
- [ ] 将技能动作、命中、移动、伤害、子技能与死亡完成条件纳入调度，逐事件刷新表现。当前步骤时长已取官方动作／持续时间与命中延迟，Map10 按该时长推进；仍未按 Delay 分帧结算、等待每个命中／移动 Playable 完成，多招仍在一步内整体执行后刷新。死亡后模型已从场景移除；死亡动画未接入。
- [ ] 按实际结果组件判定胜负，提供结果事件、终止后防重复执行、结束演出及资源清理；把结果交给所属玩法的胜负页、奖励、下一关和剧情分支，不能把原报告消费误作已取得奖励生成规则。
- [ ] 恢复离开按钮可见条件、阶段／模式限制、确认提示和后台切出差异，处理取消、失败、重复点击及释放。战斗进行中已按 `StageInfo.LeaveInPlaying` 拦截离开请求并提示，`LeaveInPreparing`、确认框、后台切出及控制路径仍未接入。

### 站位、场景与选敌（F08—F15）

- [ ] 从原 prefab、`FightSceneInfo`、阶段／波次数据恢复公共场景、地面、格网、几何、坐标映射、出生点与相机。已取得并校验的261个战斗阶段包及677份官方 `.geom` 不等于 Windows 原生场景已恢复；当前没有通用几何／地面装配器。
- [ ] 按单位恢复 MoveMask、地面高度、身体多格占位、边阻挡、出生方向、Rotation、FlipX、ViewGroup 和阵型偏移；当前 `BuildCheckpoint` 只装配部分基础字段，不能使用统一可通行掩码替代单位差异。
- [ ] 接入原准备阶段拖拽：阶段／阵营／队伍或助战／就绪／所有权门槛、允许区域、拾起放下、屏幕到格网、阈值、非法位置提示与回退，保留鼠标拖拽真实输入。自己的 NPC 站位与出战快照按[NPC 助战](npc-assistance.md)接线，不将普通队友控制权扩大。
- [ ] 按 `ChangeBattlePosHandler` 恢复范围中心、偶数尺寸半格偏移、最近合法格、占位检查和允许的交换。原请求仅证明提交内容及回包处理；本地站位持久化和失败恢复需与对应入口贯通。
- [ ] 技能 `SourceMoveList` 已接入拉到目标旁与 `RelativeToTarget` 直线进退，并按占位回退；尚未消费移动行为实体、`RelativeToSource`／`TowardsPathSides`、`DoMoveConditions`、传送、保持距离、固定方向、每段移动时长与移动中目标变化。
- [ ] 目标种类与范围点顺序已接入（近到远优先、射程内外区分、AI 优先属性优先），命中 `Scope` 已能围绕目标／施法者扩展；尚缺可站立点、跟随／锁定、HitTargetActionType、角色类别与状态筛选。`Range` 的参照系（相对施法者或选定点）缺少官方调用代码证据，当前按相对施法者处理，并对无目标可选的 Point 技能以施法者为中心释放其 Scope；更复杂的点选逻辑仍需官方 AI 证据。嘲讽／混乱参数也未从状态传入，不能视为已接入。
- [ ] 接通 `AiPriorityId` 及其有效条件、属性排序与组别，保留已确认的确定性本地决策边界；不使用废弃字段或臆造缺少的服务端评分。
- [ ] 伤害数字已按官方字体与颜色显示（普通／暴击／格挡／治疗），尚缺闪避／免疫／致盲文本、数字合并、护盾浮字、元素克制与上下限提示。
- [ ] 结算报告数据已由 `FightBattleReportBuilder` 生成，覆盖胜负／原因／回合、单位与技能统计；`FightEndReportPanelProvider` 的面板选择、个人／全员报告界面、护盾与状态次数展示及排行分数仍未接入。
- [ ] 恢复单位 collider、查询条件与玩家／NPC／怪物／宠物详情入口，验证 UI 遮挡不会点到场景目标；详情点击不新增手动施法。中途加入／灵魂拖拽仅在所属玩法确认保留时接线。

### 技能、CD、命中与状态（F16—F25）

- [ ] 从实际角色存档装配装备技能、槽序、等级／品阶、技能属性、被动、职业与其他来源状态，并在准备阶段变更后刷新快照。当前已会从技能档案读取方案主动技能、初始武器技能、阶位与槽位等级，并消费主动技能属性、被动属性与冷却；装备提供的技能、职业限制与其他加成仍待接入。
- [ ] 核对无技能／无可释放技能的正式行为。执行器已不再伪造普攻，无可用技能时不造成伤害；官方在有技能但全部不可释放时是否有等待、移动或其它表现仍缺少证据，确认后接入并补回归。
- [ ] 处理技能消耗与开场条件：`Map10RuntimeSkill` 仍未携带每技能 CD，当前 CD 由官方属性表计算；消费、`MustHaveValidCost`、开场 CD 更新时点、次数限制、换波重置、CD 增减／停止／永久调整，以及职业、禁止动作、条件状态限制仍待接入。
- [ ] 消费怪物被动技能的状态部分：被动属性已并入单位属性，但 `PassiveStatusIdList` 对应的被动状态尚未在战斗开始时装配。
- [ ] 保留每槽主动施放、Replay、子技能、额外施放与被动触发的独立身份和计数，贯通父子施放、白名单、触发上限与结束清理；按配置等待实际结束，不把所有负持续时间强制改为固定值。
- [ ] 覆盖命中图各分支、锚点与跟随、Delay／覆盖延时、链式／随机／后继命中、临时属性、吸血／附加伤害及多目标时点，维持确定性随机流与命中来源。命中延迟、`ActOnSouce` 锚点、`Scope` 范围命中、按距离计算的延迟、固定／治疗命中节点、命中驱散与命中位移已接入；仍缺命中特效锚点／跟随、Scope 的 `NotSameTarget` 等标志与区域过滤、按 Delay 分帧结算与覆盖延时。
- [ ] 将完整伤害／治疗配置、等级与位阶修正、特殊属性、场景修正和命中分支接入真实单位；命中 `DamageActionType` 的伤害／治疗／无动作已按官方区分，控制与状态类动作（50+）仍未结算。保留伤害衰减、概率和状态24181等已确认本地规则；公式单测通过不替代完整装备、怪物及状态集成。
- [ ] 状态基础字段与生命周期、阶段状态（`fight_stage_status` 配置与状态条显示）、命中附加状态（`FightDamageComponent.StatusList`）已接入；命中附加状态（含随机单条）、驱散、位移与吸血也已接入；尚无属性／护盾／减伤等状态效果、阶段状态面板、共享层数、同来源条件与层数属性缩放；子组件已可从 APK 补出（属性 664、护盾 53、减伤 34 等），但运行时尚未读取；尚缺属性／护盾／控制／事件结算、共享层数、同来源条件判定、层数属性缩放，以及从命中和被动施加状态的链路。
- [ ] 贯通状态的应用、启动、回合／行动／技能／命中／伤害／击杀／死亡／层数变化／结束触发，条件检查、移除、转移及禁用／复活重启。当前装配仅有基础字段和部分衰减组件，不能视为完整事件生命周期。
- [ ] 将控制／禁止动作、属性修改、行动条、HP 门槛与限制、反击、累计／自定义伤害和治疗修正等组件接入执行顺序，并用实际配置覆盖来源与目标变化。
- [ ] 恢复盾值、类型与消耗、破盾及其事件；恢复怪物 `ResilienceInfo` 的破韧／恢复、关联状态技能，以及 `HpPliesId` 的生命层逻辑与显示，不能以普通 HP 或饰品图标替代。
- [ ] 按原召唤配置恢复怪物／宠物／格网对象等生成与属性、主人／来源、数量限制、入队、可选与占位、死亡／销毁联动；保留死亡和复活相关状态，不将所有附属单位套成普通敌人。

### HUD、详情与报告（F26—F32、F39、F43）

- [ ] 将技能品质、详情参数、时间轴头像、阵营和主角色标识绑定真实数据。玩家技能已装配，技能栏改为优先使用376个官方技能图标（资源与接线已完成，编辑器导入与实际画面未验证），缺失时回退 `C_none`；时间轴头像仍为占位：怪物在没有头像映射时保留官方预制体默认 `G_3207` 占位图，玩家仍用 `C_none`；阵营图标在未提供解析器时按阵营使用官方 `ImgWe`／`ImgEnemy`，立绘与逐怪物头像仍缺映射，技能品质与详情参数未接入。已核对 `assets/official/icons.json` 存在技能道具→`skill_<id>-hash.png` 映射，且 `Assets/Official/Wish/Art/sprites` 内有376张同名技能图；但这些 PNG 的 `.meta` 是 `textureType: 0 / spriteMode: 0`（按纹理导入，不是 Sprite），不能直接作为图标加载，需要按模块补 Sprite 导入或将图标载荷复制到战斗目录后重新生成绑定。
- [ ] 恢复队列移动、行动态、受击／死亡动画、死亡后的移除与新单位插入；现有 NextTime 排序和间距布局只覆盖其中一部分。
- [ ] 绑定名称、单位 HP、血量变化、死亡标识与配置控制的显示条件，分别接入名称 HUD 和血条组件，不混淆 `GamePlayerHUD` 的职责。
- [ ] 绑定 Boss 存活／敌方视野过滤、最多两栏与替换、名称头像、HP／盾组合、多层血量平滑变化、韧性及状态；已按官方条件筛选存活、已生成波次、敌方且 `MonsterType` 为3–5的 Boss，最多两栏绑定 `FrontItem`/`BehindItem`，以 `TopImg.fillAmount` 显示 HP，并绑定官方怪物名称；盾、多层血量、韧性（缺 Slider 组件）、头像、逐状态图标与替换动画仍未接线，状态列表已按官方样式显示背景与层数，并在无状态时隐藏。“敌方视野”暂以波次已生成为准。
- [ ] 恢复单位状态图标池、排序／排布、层数／回合／自定义数字、禁用／消失与详情输入；阶段状态按来源合并去重、显示说明弹窗和空态。
- [ ] 贯通技能 Ready／Applied／InCD／Ended／LimitedTimes／ProfessionLimit／Disabled 等图标状态、开始和结束事件，保持正式 DebugBtn 禁用；完成真实 CD 与次数显示而非仅图标对象构建。
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
- [ ] 落实已确认的免费 Spine 数据迁移：复用官方贴图、骨骼、网格与动画，独立转换 Unity 原生动画，补齐播放／换装。当前场景视图直接依赖 `Spine.Unity.SkeletonAnimation`，不能把第三方 runtime 存在写成迁移完成。
- [ ] 修复尺寸、方向和材质：朝向回调已改为保留目录缩放、只翻转 X 轴符号；Combat/Spine 材质文本核对的2620份中2555份主贴图为空，须查官方 atlas／纹理与绑定，不能据此认定源贴图缺失。
- [ ] 根据角色和怪物实际动作／Playable 映射待机、生成、移动、施放、受击、死亡、复活及召唤。当前全部映射 `idle_01`，移动位置已按路径插值但动作未切换，HP归零也未驱动完整死亡／移除；相同动画重播、延迟可见与中断也须恢复。
- [ ] 还原命中范围、弹道／链式／来源到目标特效、挂点、层序、材质／shader、延时和销毁，并与每次施放／命中身份绑定，避免整回合只播放最终状态。
- [ ] 接入19类已恢复飘字资源的实际伤害／治疗／免疫／闪避／致盲／CD 等事件、合并条件、位置与时长。已恢复的 `damage_*` prefab 中，伤害类 prefab 的 `NumberText` 节点在原始面板里由数字渲染组件承载，本次只恢复了 UI 层级，未取得该组件，因此不能用普通文本冒充官方数字表现；免疫／闪避／致盲类 `NumberText` 是文本节点，可后续按事件绑定。恢复 Playable 关联音频及 Wwise 依赖／速度响应，采用有依据的 Windows 适配，不自行换近似音效或静态图。

### 本地状态、资源与完成验证

- [ ] 战斗存档续战已覆盖非剧情 Map10 战斗：检查点经 `FightBattleCheckpointSerializer` 写入本地存档，重新进入同一阶段／放置点时恢复（关卡、波次、单位、技能、状态、格网、时间线、回执与统计均保留），结束／主动离开／异常终止时清除；剧情战斗不自动续战，跨模式入口、奖励回执与重复结算保护仍待贯通。
- [ ] 将胜负交接和奖励回执与所属玩法存档贯通，覆盖重复点击、重复加载、退出／重启及已结算记录，防止重复奖励；具体奖励公式或服务器决策未取得时按所属页说明缺口，不自行推定。
- [ ] 对尚未恢复的动态 UI、阶段场景、模型、Playable、材质／shader、音频和 CAB 依赖按原清单补取、转换、导入并显式绑定；同步真实版本、来源、输入输出哈希。所有入口运行时仅使用随游戏交付的本地资源，断网可用。
- [ ] 对新增或修复的阶段、未来波次、同技能多怪物、范围／移动、CD／消耗、状态事件、盾／韧性、召唤及恢复运行有意义的规则回归；既有97个公式向量及规则检查不覆盖全部待实现行为。
- [ ] 解决主工程编译阻塞后执行 Editor 导入／编译及 `Zjcs.Editor.ProjectTools.Validate`。已有启动尝试受 Map10 对话／剧情调用和商城交易接口共5处编译错误阻塞；这只是已执行检查的结果，不是所有剩余错误的穷举。
- [ ] 在 Unity Play Mode 从真实入口完成准备／拖拽／方案、回合／波次、选敌／移动、技能与 CD、HUD／详情、倍率／快进／镜头、胜负／退出及重启恢复；用真实鼠标／键盘操作，检查画面、遮挡、状态和存档，不以直接调用回调代替输入。
- [ ] 实际查看玩家／NPC／怪物／宠物／召唤物、地面、血条、状态、时间轴、动作、特效与音频；覆盖不同职业／技能、重复怪物、Boss／多波、不同竖屏比例、死亡／复活等受影响分支。没有实际画面验证的项目继续标为未完成。
- [ ] 按改动影响检查 Windows 原生材质／动画／音频与离线打包；仅在涉及导出行为或用户要求时增量构建 Windows Player。公共模块完成不代替各地图／玩法的完整游戏验收，不据此发布或覆盖正式成品。

上述清单中的模式结算、技能／宠物养成、NPC 雇佣消耗与真人功能取舍仍以所属功能页为唯一规则维护点；本页维护公共接口、事件、表现和接线缺口，不复制或擅自修改模式规则。
