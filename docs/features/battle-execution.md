# 战斗执行

[总索引](../README.md)

状态：未完成

## 功能简介

本页维护通用战斗会话、阶段、格网、队伍、行动队列、目标、技能、命中、伤害、状态、召唤、胜负、HUD、战报、演出与检查点。地图入口、活动规则和奖励事务由所属功能／地图页维护。

## 原始官方实现

官方战斗由配置、共享规则程序集、实体组件、步骤时间轴、场景对象和 HUD 协同执行。战斗阶段和结果、站位与移动、技能与命中、伤害／治疗、状态与召唤均按各自官方组件和配置处理。

官方代码、配置和资源的输入版本、哈希与处理方法见[来源总览](../../assets/reference/SOURCES.md)。

原 `AppearanceOnDemandTextureLoader.GetPlaceholderTextureName` 返回图集页名加 `_placeholder`。部分导出图集在外观加载器绑定前即被解析；原生材质加载适配仅在加载器未绑定时接受这个原命名，保留已绑定加载器的原查询。使用现存材质及其贴图，不改名、不重绘，也不把占位页视为完整换装贴图；来源见[图集占位页绑定](../../assets/reference/battle-animation/atlas-placeholder-binding-source-manifest.json)。

官方 63110 技能图的 `Eff_Monster_3511_Skill02_3` 同时被 Animator 控制器和三个自动播放的 `Animation` 对象引用，原包的片段 `m_Legacy` 为 false。Windows 适配为这三个 `Animation` 引用生成仅将 `m_Legacy` 设为 true 的副本，保留全部原曲线、事件及其他字段；Animator 仍引用原非 Legacy 片段。来源与确定性转换见[动画双用途绑定](../../assets/reference/battle-animation/legacy-animation-bindings-source-manifest.json)；该适配不代表该技能整套表现已验证。

所选版本共享代码 `Common/GridGroundComponent.EnterPosSub` 将同一实体登记到 `GridTransformComponentInfo.BodyRangeInfos` 的各个身体格，`GetPosEntitys` 按格返回实体集合。客户端 `MyFightAI` 只有空代理，未包含服务端选敌和接近决策；下方身体格选敌规则属于用户确认的单机实现。

`MyFightHitBase.PlayScopePlayable` 通过命中实体的 `GridTransform.LocalToWorldPos(scope.Pos)` 放置各命中格。原 `GridTransformComponent.CalcLocalToWorldDir` 保留 `RealCenter`，先做 X 翻转，再调用 `MyMath.Rotate`，最后用 `Math.Round` 转回整数格；`Common.Direction` 为 Up=0、Down=1、Left=2、Right=3，与本地 AI 的四向索引不同。`FightGridTransformMath` 已移植这段计算，来源及哈希见[格网变换清单](../../assets/reference/battle-rules/grid-transform-source-manifest.json)。命中实体创建时的方向／翻转赋值是服务端缺口，单机处理见下方用户确认规则。

`ActionAgentComponentImplement.CanStartActiveSkill` 在动作互斥为 Discard、且动作属于 `Battle.Ai.ActiveSkillWaitActionTypes` 时等待；原表该列表为 Move=1，Move 对 Skill=2 的互斥为 Discard=2。`FightActionSkillMoveComponent.GetTotalMoveDuration` 按每个目标格累计移动时长。本地事件路径额外包含起点，因此按路径边数累计，普通技能、命中和其连锁事件统一从接近结束后播放。

`GameEndUIHandler.Delay` 对 `RoundLimit`／`TimeLimit` 分别显示 `game_end_roundlimit`／`game_end_timelimit`，沿原 `GameEndOverRoundDelayTime` 等待。`GameTimeoutUI` 用实时秒更新整数倒计时，结算等待本身使用缩放时间；倒计时结束仅通知，不自行隐藏内容。所选 884 的原 `FightMainPanel/Content/Center/TimeOutTipUI` 子树及祖先共保留 26 个对象，`Content.SafeArea` 在运行时恢复父级尺寸。原组件、原生绑定、文本与资源依赖见[超时提示来源](../../assets/reference/battle-timeout-native-source-manifest.json)。

## 完整还原部分

- `OfficialBattleRuntimeController` 连接战斗会话、准备界面、场景宿主与结算回调；`FightBattleCheckpoint` 保存可恢复的本地战斗状态。
- 回合步骤、格网与单位、受控站位、技能装配、目标选择、命中及部分伤害／HP／护盾／状态规则已有共享实现。各模块仍按官方配置和已迁移的客户端代码执行，不代表所有技能及组件覆盖完整。
- 公共战斗 HUD、状态列表、技能槽、战报、角色与部分 NPC 资源使用原配置和资源绑定；表现、音频、动画和资源依赖仍有缺项。
- 玩法专属入口通过公共宿主提供站位、角色状态、战斗快照及结果接口；实际完成范围由[所属地图](../maps/)或[玩法页](skills.md)维护。

### 公共战斗模块的 interface

`OfficialBattleRuntimeController` 是各玩法共用的 module。外部 seam 位于 `Begin(OfficialBattleRequest)` 和 `IOfficialBattleModeAdapter`，规则与表现的实现集中在公共战斗目录内。

行动进度 HUD 由 `OfficialBattleSceneTimelineView` 按官方 `GameRoundUI` 的顺序恢复：先放置检查点 `Timeline.Acting` 的当前行动者，再按 `Next` 排列其余实体；因此一个逻辑步骤已经落盘但技能演出仍在播放时，队列不会提前把下一名角色显示成正在行动。检查点缺少有效当前行动者时才回退到 `Next` 排序。

- 玩法构造包含官方地图格网、阵容、技能、阶段和胜负条件的 `FightBattleCheckpoint`，提供场景父节点、HUD 父节点及玩法 adapter，一次调用 `Begin`。恢复存档时设置 `RestoreCheckpoint`，不重新生成行动队列或技能 CD。
- 请求可提供环境 ID、准备／离开／站位参数、人物外观、图标、动画及原生模型 adapter；未覆盖的资源解析由公共官方目录处理。`SpeedModeType` 与 `SpeedConditionMet` 只描述玩法类型及官方条件判断，官方速度表加载、解锁、HUD 刷新和时间倍率在 `Begin` 内统一初始化，不再要求玩法随后调用速度初始化函数。没有条件判断器时，带条件的速度保持锁定。
- 每帧玩法在允许战斗运行时调用 `Tick(Time.deltaTime)`。公共模块统一处理准备、暂停、行动间隔、步骤播放门控和最终命中等待；玩法不计算行动队列、跳跃时长或技能 CD。销毁入口或离开时调用 `DisposeSession`，释放 HUD、场景、技能图、相机与渲染设置，并复原时间倍率。已接管会话后的初始化异常也经同一释放路径处理，技能图只释放一次。
- adapter 接收步骤快照用于所属玩法存档；最终演出完成后先接收 `OnBattleReportReady`，再接收 `OnBattleCompleted`。奖励、次数消耗、结果页和返回位置由所属玩法处理。回调中允许释放会话，不能在最后一次逻辑伤害刚结算时提前销毁表现。

| 公共实现职责 | 实现入口 |
| --- | --- |
| 官方 HUD、技能槽、CD、行动进度和状态显示 | `OfficialBattleHudView` |
| 战斗地图、格网与准备站位 | `OfficialBattleEnvironmentView`、`OfficialBattleGridMapper`、`OfficialBattlePreparationView` |
| 人物／怪物跳跃移动、朝向、模型与动作 | `OfficialBattleSceneView`、`OfficialBattleGridMotion` |
| 技能释放、命中表现和原生技能图 | `FightBattleExecutor`、`OfficialBattleSkillGraphPlayback` |
| 行动进度、回合、阶段和可恢复状态 | `FightBattleExecutor`、`FightBattlePhaseMachine`、`FightBattleCheckpoint` |
| 结束时序、战报与统计 | `CompleteStepPresentation`、`FightBattleReportBuilder` |

当前实际调用方为日常副本（含地图私人副本和活动挑战）、周常挑战、地图主线战斗、竞技场、幻想塔及技能伤害测试。它们都传入 `OfficialBattleRequest`，通过 `IOfficialBattleModeAdapter` 接收结果。新增玩法沿用这一 seam；完整规则、资源和运行覆盖仍以下方缺项及各玩法所属页为准。

战报窗口与动态统计行由 `OfficialBattleReportView` 绑定官方文案，先查所选窗口的原始文本绑定，再查共用战报文本目录。该绑定只作用于窗口及其统计行，不改写宿主页的全局文案解析器，宿主页的局部文案目录不会使战报标题和列名显示为本地化键。玩家在行动前战败时，技能页按真实记录显示零行；切换统计对象后按所选人物及其召唤物重新筛选技能。

`OfficialBattleResultReport` 为结果页保留独立的最终战报快照。关闭统计弹窗只释放窗口、头像和详情的生命周期，结果页仍可使用同一快照再次打开；禁用结果页会关闭其弹窗。完整原生结果页与旧资源导出的动作子树共用 `BindNativeBattleResultReport`，由原 `GameEndPanelBase` 订阅战报按钮。结果数据尚未初始化时不调用依赖 `ResultMsg` 的原按钮开关，避免先隐藏后无法恢复。窗口和动态行的原文字使用实例字体提供器，保持宿主页的全局字体及其生命周期。

公共退出按钮按原 `FightMainPanelLeaveModule` 仅在允许离开的 `Preparing`、`PlayStart` 或 `Play` 阶段显示，暂停、入场和结算过渡不显示。`FightBattleLeaveRules` 保留单人参与者的原处理分支：默认战斗中确认、准备时直接离开；日常副本始终确认；裂隙按原层数分支选择提示；幻想阶梯所属 `DemonBattlefield` 与自由战斗直接离开。公共控制器绑定原按钮路径，使用已恢复的原生 `ConfirmDialog`、遮罩及显示／隐藏动画，原文案来自所选 884 官方语言文件并按窗口实例解析；日常副本按已确认单机结算规则覆盖提示，原文本另行保留。取消不交接玩法，确认后再次核对战斗仍可离开，防止弹窗期间战斗结束后重复结算。玩法 adapter 接收确认后的退出请求，其状态／奖励事务仍由所属页维护。来源见[退出来源清单](../../assets/reference/battle-rules/leave-source-manifest.json)。

`OfficialSparseShaderVariants` 为战斗特效、角色和共用 UI 的原生 shader 副本登记原程序域，保留原 GPU 程序与内部平台条件。主线 Windows 构建入口已接入完整程序域校验、运行时 URP 依赖准备与恢复，并同时检查构建结果及 Error／Exception／Assert 日志；缺少的关键字组合不会获得替代实现。登记范围包含 Map16 剧情粒子依赖，共 47 个资产、44,652 个顶点／片元阶段；程序域校验及带错误检查的最终 Windows 构建已通过，此处不表示逐阶段独立编译专项全部执行。实际画面缺项仍按下文维护。操作方式见[Windows 工程验证](../asset-management.md#4-接入-windows-工程并验证)。

`FightOfficialCombatCatalog` 为每条 `HitCfg` 保留独立的 `FightBattleHitLink`，包含原顺序、重复 ID、Delay、ActOnSouce 和 IgnoreAICount；旧 ID 列表仍供现有执行器与检查点兼容使用。`ScopeEntries` 保存原 `HitScopCfg` 的位置、逐格延迟、伤害偏移、旋转及翻转开关，显式零旋转和 false 不用默认值覆盖；固定命中及连锁按原 Init 排序，随机目标保留配置顺序，召唤范围按原 `FightHitSummonComponentInfo.Init` 排序后提取 Base。命中类型、轨迹起终点偏移、旋转中心、DestroyDelay 和连锁的 Repeat、Back、PreferUnvisited 分别读取；旧检查点只补全这些不可变配置，保留生命、CD、命中回执及统计。配置 payload 的固定版本仍以[技能规则来源](../../unity/Assets/Official/Combat/skill-rules-source-manifest.json)为准。这些新增配置尚未替换现有扁平命中执行及其时序。

## 自定义实现

- APK 未提供服务器裁决的分支只按客户端证据或用户确认的离线规则处理；离线随机、结果和 AI 适配不得标成官方服务器实现。未确认的条件、奖励和状态结果保留为缺项。
- 幻兽主动支援按已确认的时序在主人回合开始、原回合监听处理后和普通技能前判断；满足原 CD、次数、控制、目标及范围条件时额外施放，不占普通行动。状态及检查点保留独立支援 CD 和次数。
- 死亡禁用按用户确认的结算顺序处理：一次受击内的伤害、HP 阈值触发、即时复活及死亡响应完成后，仍为 0 HP 才进入禁用；同次受击即时复活不触发禁用，恢复正 HP 后解除。
- 单机检查点可能在最终逻辑命中已落盘、演出结束回调尚未发出时中断。恢复这类 `Finished` 检查点后，公共宿主在首个未暂停的 `Update` 中按原有顺序发送战报与完成回调，允许玩法先完成模型绑定；不重新执行步骤、伤害、CD 或统计。正常战斗仍等待最后一步演出结束后交接。官方 `GameEndUIHandler.OnEnableAndInited` 同样对已结束的播放立即进入结果处理；落盘恢复与延迟至首帧属于单机适配。
- 用户确认日常副本与混沌中途退出正常结算。`RequestPartEnd` 使用 `OfflineBattlePartEnd` 标记可持久化的 `PartEnded`，保留当前生命、伤害、CD、随机状态与步骤回执，不追加行动或伪造胜利；玩法立即保存该检查点，当前演出完成后仍按战报先于结果的顺序交接；演出已结束时在下一次 Update 交接，不因保留已完成的播放对象而挂起。准备阶段允许直接进入结束阶段，不为退出启动回合。各玩法的评级、奖励与次数规则只在所属页维护。
- 竞技场按用户确认的主动退出判负，通过 `RequestForfeit`／`OfflineBattleForfeit` 生成普通失败结束（`Reason=2`、`PartEnded=false`），保留真实生命、CD、随机状态、行动日志与伤害统计。当前演出完成后先交接最终战报，再交接玩法结果；不通过清空生命伪造战败。玩法奖励和结算页面范围见[副本与竞技场](dungeons.md)。规则检查覆盖冷恢复后的真实统计及重复退出去重，竞技场原生结果的内置屏 Play Mode 已通过真实胜负、取消／确认退出和直接胜利，检查结果落盘、战报反复打开不推进状态及离开释放，并已查看运行截图；共用窗口及音频改动另通过魔王首次／重复结算回归，核对奖励去重、模型保留、两次打开统计、道具详情和离开释放。Unity 编译、`ProjectTools.Validate` 与静态元数据检查（108,888 个 GUID）通过。完整玩法和 Windows Player 范围仍见所属页。
- 幻想阶梯进入后台时，仍以同一执行器和步骤时长推进规则及存档；隐藏的原生模型不接收新的 Animator 或技能图指令，避免向停用对象发送动作。重新回到前台时由当前检查点刷新场景和 HUD；已开始的步骤仍保留演出完成门控。
- 用户确认大型目标的单机选敌规则：以施法者中心格为原点，技能范围覆盖目标任一身体格即可选中；接近移动也按目标身体格寻找施法位置。`FightBattleTargeting` 的范围顺序、接近距离与候选位置使用同一身体占格，候选位置去重；施法者自身的身体不扩展技能范围。执行器仍用原格网寻路检查全身占格、阻挡与移动距离，优先可到达的施法位置，防止寻路器为受阻格返回的短退路遮蔽另一条可达路径。命中范围原有的实体去重不变，多个身体格不增加释放次数、伤害或战报统计；实际验证范围见下方。
- 用户确认命中实体继承本次技能的方向与翻转，使用原 `RealCenter` 变换局部范围。`FightOfficialCombatCatalog` 读取各命中实体的中心，命中图将普通与随机父范围的变换配置分别传递到叶节点；执行器在技能开始时捕获方向／翻转，普通命中、随机选格和 AI 覆盖统计使用同一官方坐标变换。旧检查点从原配置补回缺少的变换元数据，保留 HP、CD 和释放计数；显式编写的世界偏移夹具不作二次变换。
- 地图专属等级、选向、奖励和失败回滚只在所属地图／功能页维护，避免在本页重复定义。

## 尚未实现

- 官方服务器缺失的 AI、随机、结果裁决及复杂触发条件尚未完整恢复；未确认的行为不作推测。
- 完整技能／命中／状态覆盖、同来源与叠层边界、特殊组件时序和实体生命周期仍有缺项。身体格选敌／接近和命中局部坐标变换已有指定原技能的 Play Mode 回归，尚未完成全部技能与 Windows Player 验证。
- 原人物、NPC、幻兽、召唤物的完整模型、动作、骨骼、挂点、粒子、音频和材质表现尚未全部接通。
- 命中实体的原生表现生命周期尚未接通：`MyFightHitBase` 的施法起点到命中位置连线、命中自身 Playable、逐范围格 Playable 及其延迟／清理，以及 `MyFightHitChained` 的前一命中起点仍须迁移。现有 `MyFightSkill` 原生技能图播放和伤害飘字不包含这些命中实体表现。
- 正式入口中的准备输入、战斗过程、胜负返回、存档恢复、模拟鼠标交互和 Windows Player 尚未完成整体验收。
- 活动挑战、私有副本和裂隙的退出事务尚未完整接通；已接线玩法仍须分别完成奖励页面及整体验证，公共按钮可用不代表所有玩法完成。

## 当前验证范围

- 命中配置读取、原范围初始化排序与旧检查点补全的规则回归通过：10781 恢复延迟与曼哈顿距离排序，10921 保留随机范围原序；10911 七次同 ID 引用保留各自延迟，10821 区分下一跳与范围延迟，16121 保留伤害偏移，13303 保留显式零旋转和禁用翻转，13051 保留轨迹偏移与 DestroyDelay，11541 恢复九格 BaseScopes；12861 补回连锁标志、范围、子引用及轨迹配置，HP、CD、命中回执和统计不变。现有 460 组公式及战斗核心回归一并通过；两个规则文件另在已提交代码基线上独立编译并通过同一组规则回归。这是配置读取与存档兼容的检查，不代表新增连锁执行或原生命中播放已完成。

- `GameTimeoutUI` 在实际魔王战斗的全局／主角回合耗尽路径通过内置屏 Play Mode 验证：原生父级与 `SafeArea`、原中文与字形、可见的 `2 → 1`、一次超时通知、等待后显示结果、保留战场及原按钮离开后的释放均已核对并查看运行截图。回合数与生命／速度仅在隔离夹具中缩短，不直接调用完成回调；`TimeLimit`、观战及其他玩法的提示接线仍未完成运行覆盖。玩法奖励与战报交接范围见[混沌侵袭](weekly-challenges.md)。

- 原生魔王结算的真实按钮、两次打开战报、技能页及只读详情、最终快照保留和窗口独立字体已通过内置屏 Play Mode；已查看实际画面。所属玩法的奖励、存档、战后交接和未完成范围集中见[混沌侵袭](weekly-challenges.md#当前验证范围)。
- 身体格选敌／接近的执行器回归通过：固定伤害夹具检查边缘近战只命中一次、一次释放与 CD、战报不按身体格重复、冷恢复保留占格与伤害、合法接近、受阻最近格的备用路径、完整阻挡、施法者中心原点、单格目标和范围／最短接近优先级。相同回归在旧中心格实现上于边缘命中处失败；原有范围、命中区域及原配置回合目标回归通过。Unity 编译及静态元数据检查通过，地图宿主显式引用 `Config` 和 `Zjcs.Official.LegacyWorldStages`。此变更尚未构建、运行或发布 Windows Player。
- 官方格网变换与原配置命中选择检查通过：四个原方向、半格中心、翻转先于旋转、ties-to-even 舍入及未知方向回退；原 10011 的三段四向命中、技能开始方向快照、已复现的魔王身体边缘位置、56051 半格中心、随机父范围及旧检查点补全均通过。原有命中区域、选敌与原配置回合目标回归在当前代码上通过；接近结束后出手的步骤时序、连锁时序、技能位移、命中延迟、击退／拉拽及宠物支援回归通过。
- 原 10011／4800 已通过内置屏 Play Mode 回归：使用原地图、魔王占格、技能、命中配置和公式，以及受控生命／攻击夹具，模拟点击开始，角色从 `(6,14)` 沿原移动配置接近到 `(10,14)`，三段仅各生成一次对魔王的伤害；移动位置实际随播放变化。检查原技能实体创建时角色已在最终落点，技能与命中延迟不早于原移动结束；已查看接近、挥剑、命中与冷恢复运行画面。销毁会话并从磁盘重开后，行动计数、魔王生命及技能状态保持一致，原 HUD 的 Show 动画完成后完整显示。Unity 编译、`ProjectTools.Validate` 和静态元数据检查通过；场景仍有既有无 AudioListener 提示，未验证音效输出。
- 公共速度初始化收口已通过 Unity 编译和 `ProjectTools.Validate`；阶段机制、技能位移、战报及 Map10 战斗集成规则检查通过，静态元数据检查通过。
- 63110 三个原 `Animation` 引用经 Unity 加载及内置屏 Play Mode 检查通过：Animator 保留原非 Legacy 片段，三个 `Animation` 使用兼容副本；在受控播放夹具中逐帧核对计时和原旋转曲线确实驱动对象，已查看三个原特效运行画面。夹具仅降低播放速率以避开加载长帧，生产片段及参数保持原样；未覆盖该技能主图、命中图、目标、全段时序或正式战斗生命周期，不能视为 63110 整技能演出完成。
- 图集占位页绑定经 Unity 实际加载核对，`npc_middle_model_Atlas` 的 168 个原图集页均解析到原材质及原贴图；此前这些页在构建时逐项报缺少材质。转换器也验证了 Git 原输入的 LF／CRLF 处理与重复执行；更新后的隔离 Windows Mono Player 已完成竞技场结果流程；完整换装贴图及所有角色动画仍须分别验证。
- 行动进度 HUD 已按官方 `GameRoundUI` 的 `CurActingEntityId` 优先顺序修复；内置屏 Play Mode 专项验证通过，覆盖行动者重排、行动切换、检查点序列化恢复、死亡移除及空队列。Windows Mono Player 构建成功；指定参考竖屏尺寸后，正式发布目录的技能伤害 Player 自检通过真实战斗、结果页、报告切换及返回设置，并检查隔离存档和运行截图。
- Arena 结果自检兼容原生旅行页不含 `ScrollRect` 的布局；有滚动容器时仍模拟原滚轮，并采样原 Graphic 的实际射线命中区域。运行复现了 884 旅行页生成绑定遗漏 `NoDrawingRayCast`、按钮采样点命中 `Bg` 的问题。按同一官方 bundle 及原对象哈希运行 `prepare_travel_entry_layout.py`，恢复 3 个 `SafeArea`、1 个比例适配器、10 个透明射线 Graphic 和 3 个入口按钮引用后，隔离内置屏 Play Mode 通过真实胜利、失败、取消／确认退出判负和直接胜利；检查扣票、对应奖励、战报两次打开及存档去重，并已查看实际结算、统计和返回截图。主工程恢复后的绑定与该验证副本 SHA-256 一致；旅行页及竞技场宿主页仍有既有贴图／材质和动态文本缺项。Unity 编译、`ProjectTools.Validate` 和静态元数据检查通过；隔离 Windows Mono Player 构建成功，内置屏实际启动后以模拟指针完成同样四种结果、奖励、两次战报开关和返回流程，检查磁盘结算去重，已查看运行截图；验证存档独立于用户目录，验证前后用户存档文件哈希一致。构建／导入仍出现既有缺失脚本及 Burst 插件引用诊断，不代表全工程资源问题已消除。
- 内置屏 Play Mode 的日常副本、竞技场、幻想塔和周常公共宿主回归通过，检查了模拟准备／速度输入、实际战斗推进、演出后结果回调、官方速度初始化及最终战报。已查看准备、战斗和返回画面；返回页仍可见已有贴图／表现缺项，逻辑通过不表示完整美术还原。
- 活动 209／212／226／217 使用生产 `BeginChallenge` 和官方关卡、类型、期次与次数配置，公共宿主 Play Mode 回归通过，检查实际战斗推进、演出后结算回调、统计及宿主恢复，并查看四项配置的准备／战斗画面。这是公共战斗 seam 的验证；正式活动页仍在战斗启动前因 `GoToBtn` 不在可交互层级而失败，不能据此前者宣称活动页面入口通过。
- 竞技场真实战斗后的全人物公共战报已通过内置屏 Play Mode 回归：玩家／技能页切换、逐人选择、技能统计与真实记录对应、空技能统计、水平拖动、关闭及重开通过。回归特意安装无关的宿主页全局文案解析器，确认标题、列名和动态统计行仍使用官方文案，且全局解析器未被替换；已查看双方汇总及对手技能统计的运行截图。
- Map10 公共宿主 Play Mode 回归通过：模拟点击助战确认、准备阶段射线拖放与一次保存、开始战斗、HUD／技能槽缩放、暂停与速度、最终演出等待、主线 adapter 保留的统计、战报切换及场景／相机／角色恢复。已查看助战、准备、战报和返回截图；该回归直接调用实体战斗入口，未覆盖地图实体点击。助战列表仍有既有姓名占位和条件文本重叠，流程通过不表示该页表现完整。
- Windows 构建与实际 Player 的技能伤害测试通过，使用隔离存档、模拟点击及运行截图，检查公共战斗、伤害结果、技能／人物战报和返回设置。已查看画面，标题和列名显示官方中文，技能图标、人物头像及统计保留正式宿主页提供的绑定；Player 回归同时检查了这些标签的文案。
- 包含公共模块初始化收口及战报文案修复的 Windows 版本已发布到 `D:/Desktop/杖剑传说/杖剑传说.exe`，运行时代码对应提交 `b3dc44fcc`。已核对入口、UnityPlayer 和主程序集与验证构建的哈希，并从发布目录实际启动，使用隔离存档重跑技能伤害测试、结算、战报及返回设置；回归通过，已查看运行截图，用户存档哈希未改变。桌面快捷方式指向该入口。
- 地图实体点击入口、完整战斗中断／恢复流程及正式活动页面仍未完成整体验证。
- 最终命中已落盘、演出尚未交接时的竞技场冷恢复已通过隔离工程的内置屏 Play Mode 回归：模拟点击准备按钮、实际推进至最终步骤后暂停并销毁会话，关闭宿主页后从磁盘重新打开存档，核对检查点不变、没有新增步骤、战报先于完成通知、只扣一张票且只增加一次胜负记录；已查看中断前战斗和恢复后返回画面。返回页仍有既有人物／贴图缺项。Unity 编译、`ProjectTools.Validate`、静态元数据、阶段、检查点保存恢复、步骤时序和生命周期检查通过；此修复的竞技场结果专项已完成 Windows Player 构建／运行验证；正式发布版本仍按功能完成条件单独构建。
- 普通竞技场中途冷恢复回归通过：首个未结束步骤通知中暂停，落盘后关闭并重新打开宿主页，检查回合／CD／随机状态／统计不变，模拟速度按钮后继续实际战斗，核对最终只结算一次及宿主相机、角色父节点／位置／可见性恢复；已查看恢复后的战斗与返回画面。测试入口使用可见竖屏 Game 视图，避免隔离工程的编辑器布局影响模拟输入和截图。

- 原退出路径的隔离内置屏 Play Mode 回归通过：竞技场确认／取消／再次确认与一次扣票判负；幻想阶梯主动放弃、后台实际失败停推与冷重开；日常副本部分退出检查点在结果回调前中断、冷恢复后只生成当前评级及已清房间待领取记录；混沌魔王真实命中后退出，核对不增加步骤、战报先于结果、本人实际生命伤害入期次存档及检查点清除。已查看原确认框和返回画面，返回家园仍有既有人物／贴图缺项，场景无 AudioListener，未验证确认音效输出。阶段、退出规则与检查点检查、Unity 编译、ProjectTools.Validate 及静态元数据检查通过；本次退出变更尚未构建、运行或发布 Windows Player。

- 主线原退出路径已通过隔离内置屏 Play Mode：根据原配置选择允许退出的普通关 1002，模拟助战确认、开始、原关闭按钮和确认框，核对战斗检查点及关卡／实体 ID 清零、地形／世界 HUD 的原激活状态、相机与角色父节点／缩放恢复。已查看确认和返回地图截图，返回画面的 HUD／人物仍有表现缺项；入口直接调用既有实体开战方法，未覆盖地图实体点击。教学关 1001 保留原有禁止退出配置。

## 代码与来源入口

- 官方规则与执行：[`Runtime/Official/Combat`](../../unity/Assets/Game/Runtime/Official/Combat/)。
- 离线适配：[`Runtime/Offline/Combat`](../../unity/Assets/Game/Runtime/Offline/Combat/)。
- 场景与 HUD：[OfficialBattleRuntimeController.cs](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleRuntimeController.cs)、[OfficialBattleHudCatalog.cs](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudCatalog.cs)。
- 官方配置及转换记录：[battle-rules.json](../../assets/official/battle-rules.json)、[skill-rules.json](../../assets/official/skill-rules.json)、[来源总览](../../assets/reference/SOURCES.md)。
