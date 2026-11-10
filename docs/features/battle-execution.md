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

`MyFightHitBase`、六类命中视图、三个命中图初始化器及 `CalcSkillHitDelay`／`CalcSkillStartPos` 已按所选 884 客户端和共享实现迁移到 `Official/BattleSkillPlayables`。原范围条目的顺序与相对等待、命中位置订阅、连锁起点的世界坐标偏移顺序均保留。`OfficialBattleNativeEntityLoader` 为技能和命中提供本地资源、共享身份及激活前注入，复用现有完整 `MyEntityLoadHandler`；其六个官方加载／对象池实现与当前所选版本的源文件哈希一致。所选原 `game_settings.g.bytes` 已与官方 bundle 核对并完整解析，`ECIsUseGameObjectPool=true`；命中移除沿原 End、Remove、回池顺序，连锁起点同级对象随池实体复用。同版 2,260 份完整命中实体 prefab 及其播放图依赖已转换并写入本地 Resources，完整资源目录同时保留 1,884 份原技能实体 prefab 与播放图绑定。来源见[命中视图迁移](../../assets/reference/battle-animation/hit-view-runtime-source-manifest.json)。命中专项验证入口显式设置现有 `ZJCS_BATTLE_INPUT_AUTOMATION` 启动开关，使用独立验证场景，避免进入默认游戏启动流程。命中 prefab 与六类代表图已通过下方隔离运行验证，正式战斗的命中创建通知仍未接入。

`FightOfficialCombatCatalog` 为每条 `HitCfg` 保留独立的 `FightBattleHitLink`，包含原顺序、重复 ID、Delay、ActOnSouce 和 IgnoreAICount；旧 ID 列表仍供现有执行器与检查点兼容使用。`ScopeEntries` 保存原 `HitScopCfg` 的位置、逐格延迟、伤害偏移、旋转及翻转开关，显式零旋转和 false 不用默认值覆盖；固定命中及连锁按原 Init 排序，随机目标保留配置顺序，召唤范围按原 `FightHitSummonComponentInfo.Init` 排序后提取 Base。命中类型、轨迹起终点偏移、旋转中心、DestroyDelay 和连锁的 Repeat、Back、PreferUnvisited 分别读取；旧检查点只补全这些不可变配置，保留生命、CD、命中回执及统计。配置 payload 的固定版本仍以[技能规则来源](../../unity/Assets/Official/Combat/skill-rules-source-manifest.json)为准。这些新增配置尚未替换现有扁平命中执行及其时序。

`OfficialBattleHitEntityData` 要求共享格网及实际技能／命中身份，将这些快照桥接为六类原命中组件的 Info、GetScopes 与位置通知。884 命中表现资源与固定配置 payload 分别追溯。`OfficialBattleSkillEntityPlayback.Start` 返回本次实际分配的 `EcEntityIdentifier` 值；依赖视图直接使用该值关联施法，避免按技能类型查找，或保留会在 FIFO 复用时改写身份的对象引用。

对 1,884 个原技能实体 prefab 的脚本组件审计发现 18 种客户端组件。`prepare_battle_skill_prefab_runtime.py` 另迁移八类原始空代理，以及 `MyPlayableOverrider`、`PlayableOverrideByStatusStack`、`FightSkillPlayableOverrideByStatusStack` 和原序列化 DTO／接口；来源见[额外技能 prefab 代码清单](../../assets/reference/battle-animation/skill-prefab-runtime-source-manifest.json)。状态替换保留首次查询后的层数缓存、越界取最后一个图、无对应状态时不替换、禁用时还原，以及四向图替换后的刷新顺序。原 `Source -> MyFightStatusAgent -> ApplyStatusDict` 查询由 `OfficialBattleSkillSourceStatusSnapshot` 绑定施法创建时的状态实例、层数及共享总层数；保留原状态／技能代理缺席检查。快照来自 `FightBattleSkillCastPresentation.Capture`，不会从已结算到后续施法的单位状态反查，也不会在状态消耗后改写已创建的表现。13200 的五次 `playable13200`、五次 `playable13203` 和一次 `playable13204` 按原顺序绑定；54383 的 `EcView/ECViewDriver.mECViews` 按同版 APK 程序集 schema 恢复原 RID、`ECPlayableSupporterView` 类型及原 `mAppearPlayable=playable54380` 引用，并由原 ECViewDriver 生命周期执行。

`ActionAgentComponentImplement.CanStartActiveSkill` 在动作互斥为 Discard、且动作属于 `Battle.Ai.ActiveSkillWaitActionTypes` 时等待；原表该列表为 Move=1，Move 对 Skill=2 的互斥为 Discard=2。`FightActionSkillMoveComponent.GetTotalMoveDuration` 按每个目标格累计移动时长。本地事件路径额外包含起点，因此按路径边数累计，普通技能、命中和其连锁事件统一从接近结束后播放。

`MyFightActionSkillMove.OnSingleMoveEnded` 先停止当前格的动作图，再启动下一格；整次移动技能图和逐格动作图保留不同持有者。`OfficialBattleMovementEffects` 按创建与换格时刻分段驱动原 `MyPlayableCenter`，先完成旧格最后一段更新，再停止旧图和创建新图，避免跨格大更新漏掉旧图尚未到期的效果。原 `Stop()` 只设置标记，调度在换格时通过零时长 Update／LateUpdate 交由原 PostProcessFrame 消费停止请求，避免旧图继续接收下一格的时长。原 `MyPlayable.ProcessFrame` 的创建首帧保护、序列、淡出与取消仍由原代码执行；本地调度的双精度绝对时间只用于保持相邻格的开始／结束时刻一致，不改原配置时长。绑定及原生资产哈希见[移动效果来源](../../assets/reference/battle-animation/movement-effects-source-manifest.json)。

15300 的原 `run` 参数为 Trigger，19810 的 `run` 参数为 Bool；15300 的两个尘土 FollowBone 配置 `myFollowDuration=0`，仅在原图时间为零时同步身体挂点，随后保留地面位置。主角 3100 的原 `CreateOverrideController=1`、`NotCreateGraph=1` 由原 `AnimationController` 执行并持有运行时 OverrideController，不能以存在 OverrideController 为由替换这条官方控制器链；来源见[主角动画绑定](../../assets/reference/battle-animation/player-animation-binding-source-manifest.json)。

正式 `OfficialBattleSceneView` 将角色推进作为移动效果宿主的依赖，在每段原序列更新前按同一段时长更新角色位置和朝向；跨格触发读取相应落点，剩余半格再继续推进跟随效果。角色路径的累计时间和段起点同样使用 double，进入原曲线计算时转回 float；原轨迹数据、曲线求值及挂点同步公式保持原样，未启用原移动效果的场景继续直接推进角色。

原 `PlayMovePlayable` 在创建动作图前调用 `DoMoveSourceTurn`；场景先恢复状态朝向，再按已经开始的当前格施加移动朝向，因此立即起步无需等待首个 Tick，延迟起步在到点前不提前转身。移动中的普通刷新重新应用当前格朝向，末格仍恢复权威 FlipX；共享处理只改变 X 缩放的符号，保留原缩放幅度。

`GameEndUIHandler.Delay` 对 `RoundLimit`／`TimeLimit` 分别显示 `game_end_roundlimit`／`game_end_timelimit`，沿原 `GameEndOverRoundDelayTime` 等待。`GameTimeoutUI` 用实时秒更新整数倒计时，结算等待本身使用缩放时间；倒计时结束仅通知，不自行隐藏内容。所选 884 的原 `FightMainPanel/Content/Center/TimeOutTipUI` 子树及祖先共保留 26 个对象，`Content.SafeArea` 在运行时恢复父级尺寸。原组件、原生绑定、文本与资源依赖见[超时提示来源](../../assets/reference/battle-timeout-native-source-manifest.json)。

官方 `AssemblyGSerializableBaseTypes` 注册表包含非 abstract 基类及具体叶类。`official_ec_codec.Formats` 按该注册表读取小端 ushort 类型编号，再直接消费对应具体 formatter，避免重复读取类型编号；组件 Info 类型沿原非泛型继承链追溯至泛型基类声明并缓存（包括回血状态继承扣血状态配置），仍严格检查整段字节消费。原程序集、注册表、formatter、配置分片、历史载荷及工具哈希见[当前实体配置对照来源](../../assets/reference/battle-rules/active-entity-configuration-source-manifest.json)。来源工具按单实体解析历史 JSON，并在验证工作区使用临时 SQLite 缓存；`--all-components` 纳入状态、伤害等全部已安装战斗实体，解析失败不会作为完整实体计数。对照只读取输入，不激活或改写历史配置数值。

57784 的 `playable_16040_SourceToHit` 保留原 3205 `SkeletonMecanim` 轨道及其未激活祖先。`EmbeddedMonsterMecanimResources` 按原 prefab 身份绑定免费原生骨骼、`monster_3205_SkeletonData`、低清图集、`monster_3205_Controller` 及原层混合参数。组件夹具显式激活祖先仅用于检查骨骼本身；正式图和受控命中视图均保留原 `activeSelf`，不会主动播放这条休眠轨道。专项来源与确定性输出见[内嵌 Mecanim 绑定](../../assets/reference/battle-animation/embedded-mecanim-source-manifest.json)。

完整原技能／命中图保留 1,735 个 Wwise Event 资产引用，对应 1,734 个事件 ID；两份不同 GUID 的 `Skill_14054` 引用保持独立。`prepare_battle_native_sound_sources.py` 将原事件名称的 FNV ID 与官方 EventBankMapping 比较，并按 APK 优先次序核验 25 个映射音频包和 Minimum 包的实际 bank。事件中 1,703 份引用有一个动作、32 份有两个动作；原顺序及 1,766 个独立 Play／Stop 动作的全部字段、HIRC 原字节哈希和位置均保留，不把多动作事件展开成单个 WAV。APK ARM64 `libAkSoundEngine.so` 的两个导出版本函数及原指令确认运行库为 `2022.1.18.8567`；版本、原包及绑定哈希见[原声音来源](../../assets/reference/battle-animation/native-sound-sources-source-manifest.json)。

战场状态说明窗口改用所选 884 的原生 `FightStageStatusDialog` 和 `FightStageStatusDialogItemUI` prefab，保留原层级、布局、字体字段、图标背景、动画及 `LoopVerticalScrollRect` 的原始行模板引用。迁移的两个官方类沿原 `ClearCells → dataSource → totalCount → RefillCells` 顺序绑定，复用行启用时更新内容、禁用时释放动态说明订阅。`OfficialBattleStageStatusDialogView` 负责本地目录、窗口实例字体、原显示／隐藏图及现有原生模糊遮罩的接线；原 `FightSettings.FightStageStatusDescTypes` 仅把 26030 指定为倍率动态说明，准备阶段、倍率变化通知、三位小数与原百分比格式分别沿原 provider 分支处理。原生窗口来源为 884，共用模糊遮罩保留独立 854 来源，既有状态图标／文案继续使用已追溯的 HUD 目录；不把它们标为同版资源。原对象、绑定、脚本与输出哈希见[战场状态窗口来源](../../assets/reference/battle-hud/stage-status-native-source-manifest.json)。顶部状态条仍沿既有 HUD 实现，不属于本次原生窗口转换范围。

## 完整还原部分

- `OfficialBattleRuntimeController` 连接战斗会话、准备界面、场景宿主与结算回调；`FightBattleCheckpoint` 保存可恢复的本地战斗状态。
- 回合步骤、格网与单位、受控站位、技能装配、目标选择、命中及部分伤害／HP／护盾／状态规则已有共享实现。各模块仍按官方配置和已迁移的客户端代码执行，不代表所有技能及组件覆盖完整。
- 公共战斗 HUD、状态列表、技能槽、战报、角色与部分 NPC 资源使用原配置和资源绑定；表现、音频、动画和资源依赖仍有缺项。
- 玩法专属入口通过公共宿主提供站位、角色状态、战斗快照及结果接口；实际完成范围由[所属地图](../maps/)或[玩法页](skills.md)维护。

### 公共战斗模块的 interface

`OfficialBattleRuntimeController` 是各玩法共用的 module。外部 seam 位于 `Begin(OfficialBattleRequest)` 和 `IOfficialBattleModeAdapter`，规则与表现的实现集中在公共战斗目录内。

技能命中能力已拆为 `IFightHitPattern.Plan(FightHitPatternContext)`，由原技能／命中配置选择点目标或范围实现；调用方取得包含目标、命中格和延迟的 `FightHitOccurrence`，再逐次进入既有官方公式、护盾、状态和伤害结算。随机父节点已选定的锚点不再重复消耗随机数量，明确的随机目标数量先筛选实体，再对所选实体覆盖的身体格生成伤害；治疗保持原有处理。多格攻击按原 `HitScopCfg` 的顺序与 Delay 逐格处理，同一格的同一身体声明不重复计数；同一怪物占据不同范围格会产生多次伤害。命中格与延迟随伤害回执保存，旧检查点缺少新字段时保持兼容。多段时序、衰减、子技能、状态与地形的能力拆分尚未全部完成，现有官方基础公式及执行顺序继续保留。

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

- 所选 884 状态 24679／24680 的原 `FightHandleCfgBBPickFloatByIndex` 按计数器取序列值：下标越界取末值，空序列为 1，以 double 写入原 `StorageKey`；`FightHandleCfgTriggerSkills.DamageFactorKey` 未写入时取 1，收集触发技能时捕获倍率。取值及未写入默认值依据原共享配置类的描述恢复，客户端执行类仍为空代理；Runtime 键按下述用户确认的事件级单机规则处理。既有命中 `UseDamageFactor` 开关保留。两份原图目前只作为来源固定的执行回归输入，正式历史配置尚未切换到新版蓝图。

- 共享状态层数读取所选版本 `FightStatusShareStackComponent.ShareId` 与 `Battle.ShareStatusMaxStacks`：24022～24025 共享 25300（上限 5），24166／24385／24720 共享 24029（上限 10）。后续加减按原 `FightStatusAgentComponentImplement.CanAddShareStack／ChangeShareStack` 限额，个体上限保留；消耗、结束释放占用，不同角色与共享组互不串用。检查点保存各状态已占用的层数，恢复后按存活状态汇总，并重新绑定官方组与上限。

## 自定义实现

- 状态 26030 的倍率由服务器 `MsgGameStartComplete／MsgGamePropsScaleUpdate` 下发，APK 的 `FightStatusPropsScaleFactorDescProvider` 只负责显示。用户确认单机固定为 1，不附加这项属性缩放；准备阶段仍显示原待计算说明，战斗开始后显示原“本场战斗无衰减”。受控验证额外输入 0.875 仅检查官方 provider 的更新与格式分支，不参与正式战斗裁决。

- 用户确认按全新游戏制作，不新增旧存档兼容规则；当前游戏的检查点保存与恢复继续验证。

- 用户确认状态蓝图采用事件级 Runtime 黑板：同一次事件的各流程共享非持久键，事件结束丢弃；`IsPersistentBlackboard=true` 的键保存于所属状态，并随检查点恢复。两个黑板按端口属性分开，同一数值 ID 不串读；触发技能先捕获倍率，再结束事件。该规则适用于现有计数器、整数、布尔和新增倍率读写；不会在新事件中使用先前 Runtime 输出。旧存档中已保存的已知 Runtime 键按原图端口元数据清除；同 ID 的持久端口和图中未声明的键保留。

- APK 未提供服务器裁决的分支只按客户端证据或用户确认的离线规则处理；离线随机、结果和 AI 适配不得标成官方服务器实现。未确认的条件、奖励和状态结果保留为缺项。
- 幻兽主动支援按已确认的时序在主人回合开始、原回合监听处理后和普通技能前判断；满足原 CD、次数、控制、目标及范围条件时额外施放，不占普通行动。状态及检查点保留独立支援 CD 和次数。
- 死亡禁用按用户确认的结算顺序处理：一次受击内的伤害、HP 阈值触发、即时复活及死亡响应完成后，仍为 0 HP 才进入禁用；同次受击即时复活不触发禁用，恢复正 HP 后解除。
- 单机检查点可能在最终逻辑命中已落盘、演出结束回调尚未发出时中断。恢复这类 `Finished` 检查点后，公共宿主在首个未暂停的 `Update` 中按原有顺序发送战报与完成回调，允许玩法先完成模型绑定；不重新执行步骤、伤害、CD 或统计。正常战斗仍等待最后一步演出结束后交接。官方 `GameEndUIHandler.OnEnableAndInited` 同样对已结束的播放立即进入结果处理；落盘恢复与延迟至首帧属于单机适配。
- 用户确认日常副本与混沌中途退出正常结算。`RequestPartEnd` 使用 `OfflineBattlePartEnd` 标记可持久化的 `PartEnded`，保留当前生命、伤害、CD、随机状态与步骤回执，不追加行动或伪造胜利；玩法立即保存该检查点，当前演出完成后仍按战报先于结果的顺序交接；演出已结束时在下一次 Update 交接，不因保留已完成的播放对象而挂起。准备阶段允许直接进入结束阶段，不为退出启动回合。各玩法的评级、奖励与次数规则只在所属页维护。
- 竞技场按用户确认的主动退出判负，通过 `RequestForfeit`／`OfflineBattleForfeit` 生成普通失败结束（`Reason=2`、`PartEnded=false`），保留真实生命、CD、随机状态、行动日志与伤害统计。当前演出完成后先交接最终战报，再交接玩法结果；不通过清空生命伪造战败。玩法奖励和结算页面范围见[副本与竞技场](dungeons.md)。规则检查覆盖冷恢复后的真实统计及重复退出去重，竞技场原生结果的内置屏 Play Mode 已通过真实胜负、取消／确认退出和直接胜利，检查结果落盘、战报反复打开不推进状态及离开释放，并已查看运行截图；共用窗口及音频改动另通过魔王首次／重复结算回归，核对奖励去重、模型保留、两次打开统计、道具详情和离开释放。Unity 编译、`ProjectTools.Validate` 与静态元数据检查（108,888 个 GUID）通过。完整玩法和 Windows Player 范围仍见所属页。
- 幻想阶梯进入后台时，仍以同一执行器和步骤时长推进规则及存档；隐藏的原生模型不接收新的 Animator 或技能图指令，避免向停用对象发送动作。重新回到前台时由当前检查点刷新场景和 HUD；已开始的步骤仍保留演出完成门控。
- 用户确认大型目标的单机选敌规则：以施法者中心格为原点，技能范围覆盖目标任一身体格即可选中；接近移动也按目标身体格寻找施法位置。`FightBattleTargeting` 的范围顺序、接近距离与候选位置使用同一身体占格，候选位置去重；施法者自身的身体不扩展技能范围。执行器仍用原格网寻路检查全身占格、阻挡与移动距离，优先可到达的施法位置，防止寻路器为受阻格返回的短退路遮蔽另一条可达路径。用户已确认所有多格攻击范围技能按范围格结算：每格独立命中其覆盖的合法目标，大怪的多个身体格均在范围内时逐格受伤，不按实体合并伤害；技能仍只释放一次，原配置的衰减、随机数量及其他明确限制继续适用。此规则已接到上述命中模式接口，规则测试及 13740 实际 Play Mode／Windows Player 通过；13740／13745 的地块爆炸同样遵循此规则，专项说明见[技能](skills.md)。
- 用户确认命中实体继承本次技能的方向与翻转，使用原 `RealCenter` 变换局部范围。`FightOfficialCombatCatalog` 读取各命中实体的中心，命中图将普通与随机父范围的变换配置分别传递到叶节点；执行器在技能开始时捕获方向／翻转，普通命中、随机选格和 AI 覆盖统计使用同一官方坐标变换。旧检查点从原配置补回缺少的变换元数据，保留 HP、CD 和释放计数；显式编写的世界偏移夹具不作二次变换。
- 用户确认连锁命中按逐跳起点处理：首跳从本次技能的施法位置开始，后续每跳从上一跳实际命中落点开始。官方 `MyFightHitChained.myHitToNextHitPlayable` 的字段注释也明确说明“第一次 hit 的起点是技能位置”；共享 `StartGridPos` 的赋值实现未随客户端提供。下一跳按用户确认限制处理：`Repeat=false` 排除本链已命中过的目标，`Back=false` 排除上一跳目标，`PreferUnvisited=true` 优先未命中过的目标，其余按距离、实体 ID 排序，无候选则停止。原 `Scope` 是伤害范围（10821 仅有当前格），下一跳搜索范围的服务端实现未提供，用户确认以上一跳实际落点为中心，平移原技能 `Range` 搜索；伤害范围仍使用各命中节点的原 `Scope`。这些规则尚待接入真实命中创建事件，不能据此视为连锁执行已经完整还原。
- 地图专属等级、选向、奖励和失败回滚只在所属地图／功能页维护，避免在本页重复定义。

- 用户确认共享状态首次施加按剩余额度分配；不足时部分分配，剩余额度为 0 则不创建新状态。已有状态加层使用同一总上限，状态结束释放占用。首次创建前的分配裁决是单机规则，APK 已有后续增减与结束回收公式。

## 尚未实现

- 官方服务器缺失的 AI、随机、结果裁决及复杂触发条件尚未完整恢复；未确认的行为不作推测。
- 完整技能／命中／状态覆盖、同来源与叠层边界、特殊组件时序和实体生命周期仍有缺项。身体格选敌／接近和命中局部坐标变换已有指定原技能的 Play Mode 回归，尚未完成全部技能与 Windows Player 验证。
- 原人物、NPC、幻兽、召唤物的完整模型、动作、骨骼、挂点、粒子、音频和材质表现尚未全部接通。
- 正式战斗未提供 `SkillPlayableSoundHost`，原图声音节点仍由静音宿主处理。原完整图的事件、有序动作及传递依赖已有来源核验，包括随机播放列表、继承／总线／ducking／效果器／输出设备、全部媒体和原全局 RTPC 默认值；选中对象未使用状态组或 Switch 容器，原 STMG 中未引用的全局状态设置保留原字节。尚未接入 Windows Wwise 运行库，Android bank／插件载荷在 Windows 的兼容性、原总线 EQ／压缩器／限幅器／TimeStretch 和实际声音输出仍未验证。原游戏速度参数为 RTPC `414249091`，不得用 `AudioSource.pitch` 代替原曲线。声音来源检查只固定原始输入身份，不表示音效已还原。
- 命中实体视图及本地加载绑定已迁移，2,260 个原 prefab 的加载和六类代表命中图已通过隔离 Play Mode；正式执行器的真实命中创建／移除事件仍未接入表现。仍须覆盖全部原图、施法起点到命中位置连线、逐范围格时序及清理、连锁逐跳通知与特殊状态替换。现有正式技能图播放和伤害飘字仍不包含这些命中实体表现。
- 正式技能宿主已改为加载完整原技能实体 prefab，保留原默认／四向图、角色组件和序列化引用，复用原加载、End／Remove／FIFO 池与选图流程；代表图、状态快照及对象池复用已通过隔离 Play Mode 与 Windows Player，全部原技能和完整人物演出尚未覆盖。
- 57784 命中图的原骨骼适配、受控实体创建及复用已有专项验证，但所选配置中的 57780 只引用 57781 召唤命中；全部已安装战斗实体中未找到指向 57784 的父引用。保留该未引用内容及其原休眠层级，不自行接入技能；此项验证不表示 57780 整套战斗演出已经还原。
- 正式入口中的准备输入、战斗过程、胜负返回、存档恢复、模拟鼠标交互和 Windows Player 尚未完成整体验收。
- 活动挑战、私有副本和裂隙的退出事务尚未完整接通；已接线玩法仍须分别完成奖励页面及整体验证，公共按钮可用不代表所有玩法完成。

## 当前验证范围

- 完整原图声音的来源检查通过：1,735 个原 Event 资产引用、1,734 个不同事件及其 1,766 项有序动作，传递核对 2,913 个对象、2,977 条引用、2,942 份原 HIRC 载荷、4 个原全局设置块、1,562 份完整媒体及 2 份匹配的流式预取片段；7 个引用 RTPC 均在 8 项原全局默认值中解析。原 HIRC 的长度、类型、身份与 SHA256，媒体完整／预取身份及全部包的输入哈希已检查；原插件编号与 HIRC 编号分别处理，Actor-Mixer 的兄弟列表不启动无关声音，各包重复原载荷仍分别保留。静态元数据检查通过（144,784 个 GUID）。来源工具不改 Unity 场景或生产声音播放，未据此运行声音专项或发布 Player；运行库兼容性与实际输出仍是缺项。来源见[声音事件](../../assets/reference/battle-animation/native-sound-sources-source-manifest.json)及[传递依赖](../../assets/reference/battle-animation/native-sound-dependencies-source-manifest.json)。

- 逐格移动效果通过内置屏隔离 Play Mode、Unity 编译、`ProjectTools.Validate`、Windows Mono Player 完整资源构建及实际运行。原实现中的末段漏触发、跨格采样位置过晚和首格转向滞后均已先复现失败再修复。检查原创建帧不累计时间、两个宿主互不推进、末段叶节点触发一次、越期叶节点取消、一次更新跨两格和精确终点释放；正式 `SceneView.Refresh／AttachUnitTransform／Tick` 使用 15321 原图，核对延迟创建、旧格触发所见落点、剩余半格的原 FollowBone 挂点、最终落点及图释放，并检查立即起步先转向、暂停刷新保留朝向、延迟起步与末格恢复、原 0.3 模型缩放不变。正式场景另加载 3206 原生怪物骨骼／图集／实时网格及原 application controller，使用官方 19811、速度 5、0.25 秒延迟的两格移动，核对 `run_01`、真实身体挂点、终点与跑动参数释放；未替换原控制器。另以男女两套默认战士外观加载 3100 原生身体、头发及独立武器，沿原 `AnimationController` 持有的 OverrideController 执行 15300／15301：核对原 `run` Trigger、0.25 秒延迟、每格 0.3 秒的两格移动、途中刷新保留实体朝向、两个尘土在各自原图时间零采样身体挂点后留在地面、最终落点和返回 idle，并确认人物外观状态不变。10 个原生图、17 个 FollowBone 和原序列淡出回归通过，十张 Editor 与九张 Player 实际画面均已查看；Player 退出码为 0，用户存档文件哈希前后一致。静态元数据检查通过（144,639 个 GUID），279 项原素材输出哈希不变。位置／朝向边界使用原挂接层级和空模型，原生模型专项仅覆盖男女默认战士及 3206，不覆盖全部职业／外观、全部怪物／特效、HUD 交互和音频输出；未据此发布正式版本。
- 公共入口原生接近检查 `Zjcs.Editor.OfficialBattlePublicMovementAcceptance.Run` 经 Unity 编译、`ProjectTools.Validate` 和内置屏 Play Mode 通过；同一入口连续检查人物与怪物两种受控战斗。8×8 格网使用正式 `Begin／Tick`，由原 HUD 的射线命中与模拟指针按下／抬起／点击开始。默认男战士使用原生身体、头发及独立武器，执行 15300／15301 接近后创建原 10630 技能实体并结算三段伤害；野猪 3206 按原第 0 组技能道具 14004 选用 16020，执行 19810／19811 接近后结算一次伤害。两种场景均检查途中暂停时位置、行动计数及检查点序列化结果不变，恢复后到达已结算格位，技能实体只创建一次且不早于落地，结算伤害与目标 HP 一致。本组属性下官方公式的 CD 均为 0，释放计数各为 1；人物技能槽按施放／结束切换，怪物动作期间主角空槽保持 None。八张 Editor 实际画面已查看，279 项原移动素材输出哈希不变；全工程静态元数据检查通过（144,882 个 GUID）。复用完整原资源的 Windows Mono Player 增量构建及内置屏实际运行通过相同的连续两场检查，八张无遮挡 Player 画面已查看，退出码为 0，用户存档文件哈希前后一致。Player 检查使用 Unity 2022.3 的 [D3D11 BitBlt 呈现参数](https://docs.unity3d.com/2022.3/Documentation/Manual/PlayerCommandLineArguments.html)，验证器沿 [developerConsoleVisible](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Debug-developerConsoleVisible.html) 收起引擎调试层并保留原日志；本次启动仍记录三条 PresentFrame／WaitForLastPresentationAndGetTimestamp 引擎提示，运行限制保留。夹具使用受控背景，未绑定完整人物头像，不覆盖正式地图、所有 HUD 交互、全部人物／怪物／技能及音频；逻辑伤害不表示正式命中实体表现已接入，未据此更新正式发布版本。

- `OfficialEmbeddedMecanimProbe` 已通过内置屏隔离 Play Mode、Unity 编译和 `ProjectTools.Validate`：两份 3205 组件实例使用独立骨骼状态、原低清图集及 Animator 时钟，有限网格和释放检查通过；已查看四张实际运行画面。新增命中夹具先验证同类技能并发时 ID 不同、精确到期后旧 ID 注销，以及 FIFO 复用原对象时分配新 ID、旧 ID 不会指向新施法；该身份夹具仅将生命期设为 0.125 秒。随后直接使用创建函数返回的真实 57780 施法身份，单独将未引用的 57784 原定义交给生产加载器，核对注册、两次创建间的 FIFO 实体复用、移除与完整图释放；原休眠祖先和未初始化的骨骼状态保持一致，已查看两张原粒子运行截图。Windows Mono Player 代码增量构建及内置屏实际运行通过，同样六张截图均已查看；运行退出码为 0，用户存档文件哈希前后一致。静态元数据检查通过（144,755 个 GUID）。本次原生资产的 20 项输出哈希均保持不变。该夹具不新增配置引用，不覆盖正式执行器通知、HUD 交互、伤害结算或音频。未据此发布正式版本。

- 逐格命中能力通过 `test_battle_hit_patterns.ps1` 的接口 与真实执行器检查：多身体格、范围内其他敌人、格位与原延迟、重复身体声明排除、随机数量／已选随机锚点、治疗和死亡排除均通过；同一大怪三个身体格按原衰减依次造成 100／50／25 点伤害，独立目标不共用该衰减，整套只释放一次。伤害格位、延迟及生命状态检查点往返通过。460 条固定公式向量及既有战斗核心、范围、充能、瞬时加层、逐段增伤回归通过。Unity 编译、ProjectTools.Validate 及 13740 实际 Play Mode 通过；原方向范围、命中延迟、连段、随机／延迟回合及状态逻辑回归通过。方向快照夹具改用生产 Capture 入口保留原施法位置，避免以默认零坐标替代。Windows Player 已构建并以原竖屏比例在内置屏实际运行，13740 主技能与地块爆炸分别产生 9 次逐格扣血，原动作／特效、模拟开始与结算关闭、地块保留和检查点恢复通过，五张运行截图已查看。静态元数据检查通过（142,645 个 GUID）。顶部黑色遮挡及返回家园的既有视觉缺项仍在，未正式发布。

- `test_battle_status_logic.ps1` 通过两份原序列蓝图的八回合执行、状态复制后的计数延续、检查点只保留持久 Counter、排除 Runtime 倍率、跨流程 Runtime 共享／下一事件清空、同 ID 双黑板隔离、旧 Runtime 键清理及未知键保留、已收集倍率快照、空序列／未写键默认值、零／小数和越界下标回归；既有状态逻辑回归及 460 组公式和战斗核心回归也通过。Unity 编译、Editor 中两份原图及事件级黑板专项和 `ProjectTools.Validate` 通过；尚未覆盖此新增分支的正式战斗与 Windows Player。

- 全部 7,920 个已安装战斗实体已逐组件对照所选 884 原配置，无解析错误、未定位实体或未定位伤害依赖：6,484 项同值，1,436 项有差异。其中原 1,906 个技能、2,260 个命中的逐项结果保持一致，10840、12010、12900、13140、13811、13990、60741 共七项差异尚未激活；其余差异也仍保留为来源对照。13871／13941／13985 的原定义分别由 191／194／197 号分片移动到 194／197／200，扫描同版分片后各仅有一个定义。13811 当前引用的伤害实体 13812 已取得唯一原定义，含命中后施加状态 24234 的配置；本地实体表仍未补入该依赖。六项二进制解码回归（含原 12030 多态及原 60680 继承配置）和三组嵌套／转义／Unicode／精确 64 位整数 JSON 对照通过。此检查不覆盖其他配置表、属性、其他新增实体成员或战斗运行行为。
- 本批全工程静态元数据检查通过，共 142,651 个 GUID；Unity 编译和 `ProjectTools.Validate` 通过。2,260 个原命中 prefab 全部加载，检查原 MyEntity、六类命中组件与缺失脚本。六类代表命中通过内置屏隔离 Play Mode，已查看六张实际运行画面：10501 投射、10721 范围、10821 连锁、随机父命中、召唤及触发技能；检查实际 Playable、位置通知、范围回调、暂停／加速、移除与池复用。对象池实例工厂在正常创建和异常通知后均恢复原解析器。夹具直接提供受控共享数据给原加载器，不覆盖正式战斗通知、HUD 输入或所有原技能；Windows Mono Player 已实际运行通过，六张截图均已查看并显示原投射、范围、连锁、召唤、随机父命中和触发技能特效；夹具使用正式战斗管线入口，拒绝仅有背景的截图，结束后核对原管线恢复。运行检查及退出码均通过，用户存档文件哈希前后一致；未覆盖正式战斗通知、HUD 输入、全部技能及音频输出，未据此发布正式版本。
- 完整原技能实体宿主通过 Unity 编译、`ProjectTools.Validate` 与全部 1,884 份技能／2,260 份命中 prefab 的加载和缺失脚本检查。隔离内置屏 Play Mode 通过 13200 五层／十层选图、原图实际粒子、回池后恢复并重新替换、状态消耗后的施法快照隔离、12030 共享层数和原十项延迟偏移、54383 原托管视图出现图及回池后重新注册播放；三张实际运行截图已查看。`test_battle_skill_cast_snapshot.ps1` 通过有序状态身份、重复类型、共享总层数、零总数移除、连续施法隔离、原坐标／朝向及当前游戏检查点保存恢复后的快照隔离检查，战斗核心 460 条公式回归通过，静态元数据检查通过（144,647 个 GUID）。完整资源 Windows Player 已构建并真实启动，同样通过五层／十层选图、状态快照、原延迟偏移、54383 托管视图和回池复用，三张 Player 实际截图已查看；共用加载器下的范围、弹道、连锁、随机父节点、召唤及触发技能六类命中回归也在新 Windows Player 中通过，六张截图已查看，并再次通过打包后的共享层数／检查点检查。两次隔离运行前后用户数据哈希一致。真实男女角色、直接／控制器动画路径下的 11170／10820 原技能实体、正／负／其他角色槽位、延迟施放、准确结束、已挂载与延迟挂载 HUD、空图 10100 及借用代理清理回归通过；技能槽夹具按正式流程刷新战斗阶段并等待原入场动画，已查看 CD 数字、施放高亮和结束画面，未把原空白占位图标视为技能图标覆盖。新增技能实体专项仍未覆盖全部人物动作、正式战斗输入、全部技能或音频输出。
- 命中配置读取、原范围初始化排序与旧检查点补全的规则回归通过：10781 恢复延迟与曼哈顿距离排序，10921 保留随机范围原序；10911 七次同 ID 引用保留各自延迟，10821 区分下一跳与范围延迟，16121 保留伤害偏移，13303 保留显式零旋转和禁用翻转，13051 保留轨迹偏移与 DestroyDelay，11541 恢复九格 BaseScopes；12861 补回连锁标志、范围、子引用及轨迹配置，HP、CD、命中回执和统计不变。现有 460 组公式及战斗核心回归一并通过；两个规则文件另在已提交代码基线上独立编译并通过同一组规则回归。这是配置读取与存档兼容的检查，不代表新增连锁执行或原生命中播放已完成。
- 2,260 个 884 命中视图的实体 ID 和六类组件类型对应固定配置；48 个当前命中运行时输出与已通过 Play Mode 的隔离文件逐项 SHA-256 一致。未据此将历史配置数值视作 884 数值，也不表示正式执行器已创建这些命中视图。

- `GameTimeoutUI` 在实际魔王战斗的全局／主角回合耗尽路径通过内置屏 Play Mode 验证：原生父级与 `SafeArea`、原中文与字形、可见的 `2 → 1`、一次超时通知、等待后显示结果、保留战场及原按钮离开后的释放均已核对并查看运行截图。回合数与生命／速度仅在隔离夹具中缩短，不直接调用完成回调；`TimeLimit`、观战及其他玩法的提示接线仍未完成运行覆盖。玩法奖励与战报交接范围见[混沌侵袭](weekly-challenges.md)。

- 原生魔王结算的真实按钮、两次打开战报、技能页及只读详情、最终快照保留和窗口独立字体已通过内置屏 Play Mode；已查看实际画面。所属玩法的奖励、存档、战后交接和未完成范围集中见[混沌侵袭](weekly-challenges.md#当前验证范围)。
- 身体格选敌／接近的执行器回归通过：固定伤害夹具检查边缘范围覆盖的三个身体格分别造成 17 点伤害、一次释放与 CD、战报合计 51 点伤害、冷恢复保留占格与伤害、合法接近、受阻最近格的备用路径、完整阻挡、施法者中心原点、单格目标和范围／最短接近优先级。相同回归在旧中心格实现上于边缘命中处失败；原有范围、命中区域及原配置回合目标回归通过。Unity 编译及静态元数据检查通过，地图宿主显式引用 `Config` 和 `Zjcs.Official.LegacyWorldStages`。此变更尚未构建、运行或发布 Windows Player。
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

- `test_battle_shared_stacks.ps1` 已通过原始两组上限、首次个体上限、2＋2＋1 部分分配、零额度不创建、跨状态增减／结束再分配、不同角色隔离和真实检查点恢复验证。共享层数新增分支已通过 Unity 导入编译、`Zjcs.Editor.ProjectTools.Validate` 与 Play Mode 同组规则检查；Windows Mono Player 也使用打包的原配置通过同组规则与当前检查点恢复检查；尚未覆盖正式战斗交互，暂不作为整个战斗功能完成结论。

- 原生战场状态说明窗口已通过 Unity 导入编译、`Zjcs.Editor.ProjectTools.Validate`、静态元数据检查，以及内置屏 Play Mode 和实际 Windows Mono Player 验证。永久入口 `Zjcs.Editor.BattleStageStatus.OfficialBattleStageStatusAcceptance.Run` 在受控公共 HUD 上使用模拟指针打开原窗口，以 14 项状态检查原滚动列表的行复用与滚轮、准备说明、默认倍率 1 的原无衰减说明、动态说明更新、原显示／隐藏动画、模糊遮罩关闭和重新打开；已查看编辑器和 Player 的实际画面。完整资源构建与后续代码增量构建通过，验证前后用户存档文件哈希一致。该验证未经过完整战斗控制器流程，未覆盖顶部状态条原生化、全部战斗 HUD、声音实际输出或各模式整合；Player 启动仍有既有帧呈现诊断。本模块尚未更新正式发布目录，不能作为整个战斗功能完成结论。

## 代码与来源入口

- 官方规则与执行：[`Runtime/Official/Combat`](../../unity/Assets/Game/Runtime/Official/Combat/)。
- 离线适配：[`Runtime/Offline/Combat`](../../unity/Assets/Game/Runtime/Offline/Combat/)。
- 场景与 HUD：[OfficialBattleRuntimeController.cs](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleRuntimeController.cs)、[OfficialBattleHudCatalog.cs](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudCatalog.cs)。
- 官方配置及转换记录：[battle-rules.json](../../assets/official/battle-rules.json)、[skill-rules.json](../../assets/official/skill-rules.json)、[来源总览](../../assets/reference/SOURCES.md)。
