# 战斗执行

[总索引](../README.md)

状态：未完成

## 功能简介

本页记录 Unity 共用战斗规则、格网执行和公共 HUD 的实际迁移状态。范围为各玩法和地图共用的战斗部分，不包含副本、活动等模式各自的入口、专属规则与结算页。已有部分规则、官方 HUD 组件构建和共用会话控制器；`OfficialMap10GameRuntime.BeginBattle` 已调用控制器，但玩家技能、怪物 CD、倍速、完整 HUD、场景和演出仍有缺口，并存在可复现的跨波次选敌错误，不能视为通用战斗过程已完整还原。开场剧情的 `OpeningFightPlayback` 是记录播放，不替代通用战斗运行时。

## 完整还原部分

`FightDamageFormula`、`FightDamageResolver`、`FightDamageSettlement`、`FightSkillState`、`FightTurnQueue`、`FightBattleTargeting`、`FightBattleHitCollector`、`FightBattleHitExecutor`、`FightDamageFalloff` 和 `FightBattleExecutor` 位于 [`Runtime/Official/Combat`](../../unity/Assets/Game/Runtime/Official/Combat/)。它们覆盖公式伤害与治疗、命中结算、技能状态、行动队列、目标选择、命中图收集、部分状态衰减及带回执的分步执行／恢复。

[`FightOfficialCombatCatalog`](../../unity/Assets/Game/Runtime/Official/Combat/FightOfficialCombatCatalog.cs) 从随工程交付的 Combat JSON 及来源清单装配单位、技能、命中图与状态数据。目录绑定和编辑器规则入口在 [`ProjectTools.cs`](../../unity/Assets/Game/Editor/ProjectTools.cs) 与 [`FightCombatValidation.cs`](../../unity/Assets/Game/Runtime/Official/Combat/Editor/FightCombatValidation.cs)。规则数据位于 [`Assets/Official/Combat`](../../unity/Assets/Official/Combat/)；其来源清单记录原始来源及转换输出。

[`FightBattleGrid`](../../unity/Assets/Game/Runtime/Official/Combat/FightBattleGrid.cs) 保存格网地形、占位与边阻挡，并按官方 `AStarUtils` 的四方向代价和 MoveMask 规则计算路径；[`FightBattleExecutor`](../../unity/Assets/Game/Runtime/Official/Combat/FightBattleExecutor.cs) 在技能超出范围时按 MoveDist 尝试移动，再重新选择目标。每个单位回合按技能槽检查可用技能，技能状态记录施放、次数限制与冷却，步骤事件记录移动、施放和目标。Map10 的 `BuildCheckpoint` 已从战斗 JSON 装配基础格网、出生位置和波次；当前位置只在步骤结束后直接赋给模型，未消费路径事件恢复逐格移动演出，也未完整消费地形、身体占格及阵型字段。

公共 HUD 的官方 prefab 树、sprite、字体和 Spine 骨骼输入由 [`prepare_unity_battle_assets.py`](../../scripts/prepare_unity_battle_assets.py) 按来源哈希恢复到 `Assets/Official/Combat`；原 HUD 有 30 个 prefab、701 个 sprite。[`OfficialBattleHudCatalog`](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudCatalog.cs)、[`OfficialBattleHudRenderer`](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudRenderer.cs) 和 [`OfficialBattleHudView`](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudView.cs) 已有隔离 Unity 工程导入及逐 prefab UI 组件树检查。视图支持回合文本、按 `GameRoundUI` 的 NextTime 排序和间距参数呈现行动时间轴、技能图标、冷却、次数耗尽态，并可由步骤事件刷新行动态和技能施放态。Map10 调用端目前把技能图标和时间轴头像均解析成同一张 `C_none` 占位图，未传阵营图标；玩家也未装配技能，因此不能以视图接口存在认定 HUD 的实际内容已接通。原版 `GameSkillIcon` 的常规技能图标不是手动施法按钮，prefab 中 `DebugBtn` 保持禁用。

怪物外观来源已恢复为407个 Spine 模型绑定，343个怪物 ID 映射见 `OfficialBattleModelCatalog` 及 `model-identities.json`；[`OfficialBattleSceneView`](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.cs) 可按调用端提供的模型键和网格映射实例化骨骼，并按 `Spawned` 延迟显示后续波次。Map10 对玩家返回空模型键，继续使用地图 avatar；对怪物调用模型目录解析，但把所有步骤动画固定为 `idle_01`，朝向回调还会覆盖目录缩放。当前视图直接依赖 `Spine.Unity.SkeletonAnimation`，尚未落实角色动画的[原生转换方案](../asset-management.md#spine-角色动画迁移决策)。模型绑定数不代表贴图、实际尺寸、动作及画面已通过。

[`OfficialBattleRuntimeController`](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleRuntimeController.cs) 提供共用战斗会话入口：`Begin` 可新建或恢复检查点，`Advance` 执行下一步并刷新官方回合文本、行动时间轴、技能槽及战斗模型；`StepCompleted` 和 `HudControlClicked` 将步骤及官方面板交互转交给玩法入口。控制器要求入口传入检查点、网格映射、技能／时间轴图标、角色模型键、动作及朝向绑定；它不代替各玩法的阶段数据装配。`OfficialBattleSceneView.Refresh` 现按 `FightBattleWaveState.Spawned` 延迟实例化未生成波次的单位。

规则检查入口为 [`FightCombatValidation.RunSmokeTest`](../../unity/Assets/Game/Runtime/Official/Combat/Editor/FightCombatValidation.cs)，菜单为 `Zjcs/Combat/Run Rule Smoke Test`。`Zjcs.Editor.ProjectTools.Validate` 还会装载官方战斗目录并运行规则检查。实际验证范围如下：

- 规则：重新运行 `scripts/test_unity_combat.ps1` 通过，覆盖97个固定公式样例及战斗目录装配、技能／状态、命中图、衰减、胜负、检查点恢复、随机流、时间线、目标选择和多目标衰减。另用当前 C# 规则直接构造双波次场景，已复现无技能玩家跨20格击杀未生成第二波单位，详见下表；现有回归通过不代表这一缺陷不存在。
- 静态：`python -B scripts/check_unity_project.py` 通过。材质文本核对发现 Combat/Spine 下2620份 `.mat` 中2555份 `_MainTex` 为 `{fileID: 0}`，怪物材质也在其中；这是绑定缺口，不据此宣称官方原始贴图缺失。
- Editor：HUD 的隔离组件树检查覆盖30个 prefab、701张 sprite、228个 Image、85个文本、26个按钮、23个布局组、43个适配器和2个滚动视图。当前主工程以 Unity 2022.3.57f1c2、Direct3D 11 启动复核，但编译失败：`OfficialMap10GameRuntime` 调用不存在的 `PlayMapTalkDialog`，并以四参数调用没有对应重载的 `PlayStory`；`CommerceFeatureRuntime` 两处调用不存在的 `CommerceTransactionResult` 双参数构造函数，另调用不存在的 `OfflineCommerceOffers.ActivateFirstPurchase`。本轮未执行到 Editor 规则入口和 Play Mode。
- Play Mode：当前通用战斗的真实入口、鼠标／键盘命中、HUD 画面、角色／怪物动作、特效、音频和退出恢复均未通过本轮运行验证；主工程编译阻塞不能被静态检查或规则回归代替。未构建或验证 Windows Player。

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

当前调用链的具体缺口如下；官方客户端来源位于 `.tools/source-packages/official-code/client-source/`，共享状态位于 `common-source/Common/`。这里只记录已定位的客户端行为、配置消费和本地实现差异，不推定 APK 内存在完整服务器决策实现。

| 检查项 | 官方依据与当前实际差异 |
| --- | --- |
| 玩家技能与普攻 | `FightMainPanelSkillUI`、`FightSkillListDataPlayerProvider` 消费实际装备技能及施放／结束／CD 事件。`OfficialMap10GameRuntime.BuildCheckpoint` 只将主动技能装配给怪物，玩家 `Skills` 和技能槽为空；`FightBattleExecutor.Step` 无施放时走 `ExecuteBasic`，直接选择目标伤害，无攻击距离和移动检查。不能作为官方默认普攻的完整复刻。 |
| 怪物 CD、属性与被动 | 官方 `FightSkillData.CD` 和 `FightSkillListDataMonsterProvider.GetSkillLeftCD` 保留技能冷却。`Map10RuntimeSkill` 只有实体、物品、品阶和槽位，调用端没有设置 `FightOfficialSkillBinding.Cooldown`，默认0；还显式传空技能属性、未消费 `PassiveSkills`。同一 `enemySkillBindings` 列表聚合所有怪物再按实体／槽过滤，相同技能组合还可能重复装配槽位并抛异常。 |
| 波次推进与选敌 | `Initialize` 将未来波次单位标为 `Ended`，场景视图也等待 `Spawned`；但 `FightBattleTargeting.Select` 只排除空对象、死亡和不可选目标，不排除 `Ended`／未来波次。实际复现：第一波单位2仍存活，第二波单位3的 `Spawned=false`、`Ended=true`，玩家1无技能却跨20格将单位3的HP从10打到0。未来波次也被移动占位集合纳入。 |
| 站位、移动与目标条件 | `MyFightActionSkillMove` 按路径逐段播放移动 Playable、计时和朝向。当前 `OfficialBattleSceneView.Refresh` 及 Map10 直接设置终点位置，未使用 `Movements.Path` 播放路径；格网只做部分数据装配。选敌工具虽接受嘲讽／混乱参数，实际执行器未传入对应状态；出生阵型的方向、翻转、视野组和完整身体占格也未贯通。未把未定位的手动拖拽站位或点选施法写成原版通用规则。 |
| 回合与节奏 | `GameProgressUI` 按阶段选择玩家回合或全局回合，临近上限时改变颜色并播放倒计时动画；当前 HUD 只格式化全局回合和上限。Map10 每0.35秒完整执行一个单位行动，未等待技能、命中、移动或死亡演出；单步骤的多招只在执行结束后一起刷新。 |
| HUD与单位反馈 | 原 `GamePlayerHUD`、`FightMainPanelBossUI`、`FightMainPanelBossUIItem`、`FightMainPanelStageStatus` 维护单位生命、护盾、韧性、状态及Boss筛选／切换。当前公共视图只有回合、技能槽和时间轴的部分刷新，缺少上述绑定；Map10 的图标／头像为统一占位，阵营未绑定。技能品质、职业限制／禁用态、施放结束及宠物能量等原版状态也未完整恢复。 |
| 倍速与公共控件 | `FightMainPanelSpeedUI.InitValidGameSpeeds`／`SwitchSpeed` 按官方配置切换 `FightComponent.Speed` 并更新文字、锁定态，结束时复位。当前 `OnBattleHudControl` 只处理包含 `Leave` 的路径，倍速点击无业务处理；执行与时间轴均用未缩放时间，没有对应倍速调度。其他公共按钮也不能以 Button 已创建认定功能可用。 |
| 模型、缩放与动作 | `MyFightRoleCharacter`、`PlayerAnimatorAppearance` 和 `MyFightActionSkillMove` 驱动外观、生成／移动／死亡及技能 Playable。当前怪物仅待机，玩家模型未进入共用视图；模型目录的缩放为0.01，而 Map10 朝向回调将 `localScale` 写成 `(±1,1,1)`，覆盖原尺寸、放大100倍。大量材质主贴图未绑定；`Refresh` 也不因 `Hp==0` 移除尸体或驱动死亡流程。 |
| 状态与技能生命周期 | 规则装配目前只读取状态基础字段及伤害衰减等部分组件；执行链没有贯通完整加状态、持续回合、反击、召唤、护盾和韧性事件。`BuildCheckpoint` 未消费怪物 `ResilienceInfo`／`HpPliesId` 等数据，不能以已有配置或伤害公式认定这些战斗行为完成。 |
| 本地恢复 | 控制器有恢复检查点参数，规则回执重放有测试；但 Map10 未保存活动战斗检查点，`LeaveBattle` 销毁会话，只保存地图进度，重新入场不等于继续原战斗。模式专属奖励、胜负页及剧情分支仍在所属页维护。 |

- 把完整官方技能、状态、反击、召唤和行动事件生命周期接入同一战斗调度；当前执行器的规则覆盖不等于完整官方生命周期。
- 从原版场景 prefab、`FightSceneInfo` 及战斗阶段／波次数据恢复公共战斗场景、地图几何、地面、格网占位和单位生成，并让格网移动事件驱动场景模型。已确认 `FightSceneRoot` 使用 `Assets/Prefabs/EcEntity/Fight/FightStage/{sceneID}/{game_scene}`；原版本 APK／CDN 中检出的261个战斗阶段包均已取得并校验，另有677份官方 `.geom` 场景数据。资源并非已确认缺失，但尚未转换为 Windows 可用场景、导入 Unity 并绑定会话控制器；运行时仍无通用地图几何与地面装配器。
- 将官方玩家复合外观、怪物 Spine 模型、角色动作、技能演出及受击／死亡表现接入统一战斗运行时。当前怪物身份表有343项；玩家外观组合与模型生命周期尚未接入，`OfficialBattleSceneView` 的模型键、方向和动画映射仍由调用端提供。
- 将公共 HUD 与执行事件持续绑定，补齐玩家和单位血量／韧性／状态、Boss 栏、阶段状态及受击／死亡演出。官方 `FightMainPanelBossUI.CheckShowBossCondition` 只收录存活、处于敌方视野内的 Boss，最多同时显示两个；[`FightMainPanelBossUI`](../../.tools/source-packages/official-code/client-source/FightMainPanelBossUI.cs) 与 [`FightMainPanelBossUIItem`](../../.tools/source-packages/official-code/client-source/FightMainPanelBossUIItem.cs) 分别维护实体筛选/切换和生命护盾、韧性、头像名称、状态图标。相应 prefab 已在 HUD 官方资源中恢复，但离线状态目前没有护盾值、Boss 态筛选/栏位切换及状态图标绑定；行动时间轴的队列、位置及原版 prefab 已接入，具体玩家／怪物头像、阵营图标的数据解析器尚未由玩法入口绑定，完整画面和事件流程仍未由 Unity 实际运行验证。
- 依据 `MyFightRoleCharacter`、`PlayerAnimatorAppearance` 和官方外观资源绑定玩家复合模型；接入各职业动作、怪物动作、格网移动插值、受击与死亡流程。现有 Spine 怪物身份表不能替代玩家角色组合外观和动作驱动。
- 按项目要求清点运行时配置、场景几何和动态资源的来源、哈希及显式加载绑定，并完成 Unity 编辑器导入、主工程编译、战斗运行和存档恢复验证。
- 各玩法模式的入场、胜负、奖励和退出流程由所属功能页处理，不属于本次公共战斗迁移范围。
