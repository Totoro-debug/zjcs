# 战斗执行

[总索引](../README.md)

状态：未完成

## 功能简介

本页维护通用战斗会话、阶段、格网、队伍、行动队列、目标、技能、命中、伤害、状态、召唤、胜负、HUD、战报、演出与检查点。地图入口、活动规则和奖励事务由所属功能／地图页维护。

## 原始官方实现

官方战斗由配置、共享规则程序集、实体组件、步骤时间轴、场景对象和 HUD 协同执行。战斗阶段和结果、站位与移动、技能与命中、伤害／治疗、状态与召唤均按各自官方组件和配置处理。

官方代码、配置和资源的输入版本、哈希与处理方法见[来源总览](../../assets/reference/SOURCES.md)。

原 `FightHitExtHemophagiaComponentInfo.HemophagiaPropType` 指向 `OnceHitHemophagiaPer`，共享组件没有服务端执行体。所选 `Battle.HemophagiaEntityId` 为 10110；原自目标治疗技能经 10111 固定命中使用伤害实体 10028，其公共图为 `playable_cure_lifesteal`。原技能 `CanTriggerChild=false`、状态白名单仅 25136，原治疗公式及禁疗／转盾继续按此配置执行；相关原 prefab、图及依赖已包含在既有技能／命中／伤害资源目录，不新增替代资源。

原 `AppearanceOnDemandTextureLoader.GetPlaceholderTextureName` 返回图集页名加 `_placeholder`。部分导出图集在外观加载器绑定前即被解析；原生材质加载适配仅在加载器未绑定时接受这个原命名，保留已绑定加载器的原查询。使用现存材质及其贴图，不改名、不重绘，也不把占位页视为完整换装贴图；来源见[图集占位页绑定](../../assets/reference/battle-animation/atlas-placeholder-binding-source-manifest.json)。

官方 63110 技能图的 `Eff_Monster_3511_Skill02_3` 同时被 Animator 控制器和三个自动播放的 `Animation` 对象引用，原包的片段 `m_Legacy` 为 false。Windows 适配为这三个 `Animation` 引用生成仅将 `m_Legacy` 设为 true 的副本，保留全部原曲线、事件及其他字段；Animator 仍引用原非 Legacy 片段。来源与确定性转换见[动画双用途绑定](../../assets/reference/battle-animation/legacy-animation-bindings-source-manifest.json)；该适配不代表该技能整套表现已验证。

所选版本共享代码 `Common/GridGroundComponent.EnterPosSub` 将同一实体登记到 `GridTransformComponentInfo.BodyRangeInfos` 的各个身体格，`GetPosEntitys` 按格返回实体集合。客户端 `MyFightAI` 只有空代理，未包含服务端选敌和接近决策；下方身体格选敌规则属于用户确认的单机实现。

`MyFightHitBase.PlayScopePlayable` 通过命中实体的 `GridTransform.LocalToWorldPos(scope.Pos)` 放置各命中格。原 `GridTransformComponent.CalcLocalToWorldDir` 保留 `RealCenter`，先做 X 翻转，再调用 `MyMath.Rotate`，最后用 `Math.Round` 转回整数格；`Common.Direction` 为 Up=0、Down=1、Left=2、Right=3，与本地 AI 的四向索引不同。`FightGridTransformMath` 已移植这段计算，来源及哈希见[格网变换清单](../../assets/reference/battle-rules/grid-transform-source-manifest.json)。命中实体创建时的方向／翻转赋值是服务端缺口，单机处理见下方用户确认规则。

`MyFightHitBase`、六类命中视图、三个命中图初始化器及 `CalcSkillHitDelay`／`CalcSkillStartPos` 已按所选 884 客户端和共享实现迁移到 `Official/BattleSkillPlayables`。原范围条目的顺序与相对等待、命中位置订阅、连锁起点的世界坐标偏移顺序均保留。`OfficialBattleNativeEntityLoader` 为技能和命中提供本地资源、共享身份及激活前注入，复用现有完整 `MyEntityLoadHandler`；其六个官方加载／对象池实现与当前所选版本的源文件哈希一致。所选原 `game_settings.g.bytes` 已与官方 bundle 核对并完整解析，`ECIsUseGameObjectPool=true`；命中移除沿原 End、Remove、回池顺序，连锁起点同级对象随池实体复用。同版 2,260 份完整命中实体 prefab 及其播放图依赖已转换并写入本地 Resources，完整资源目录同时保留 1,906 份原技能实体 prefab 与播放图绑定（含 22 个普通移动技能）。来源见[命中视图迁移](../../assets/reference/battle-animation/hit-view-runtime-source-manifest.json)。命中专项验证入口显式设置现有 `ZJCS_BATTLE_INPUT_AUTOMATION` 启动开关，使用独立验证场景，避免进入默认游戏启动流程。命中 prefab 与六类代表图已通过下方隔离运行验证，正式回执播放的接入范围见下文。

`FightOfficialCombatCatalog` 为每条 `HitCfg` 保留独立的 `FightBattleHitLink`，包含原顺序、重复 ID、Delay、ActOnSouce 和 IgnoreAICount；旧 ID 列表仍供现有执行器与检查点兼容使用。`ScopeEntries` 保存原 `HitScopCfg` 的位置、逐格延迟、伤害偏移、旋转及翻转开关，显式零旋转和 false 不用默认值覆盖；固定命中及连锁按原 Init 排序，随机目标保留配置顺序，召唤范围按原 `FightHitSummonComponentInfo.Init` 排序后提取 Base。命中类型、轨迹起终点偏移、旋转中心、DestroyDelay 和连锁的 Repeat、Back、PreferUnvisited 分别读取；旧检查点只补全这些不可变配置，保留生命、CD、命中回执及统计。配置 payload 的固定版本仍以[技能规则来源](../../unity/Assets/Official/Combat/skill-rules-source-manifest.json)为准。子命中展开已优先消费每条 `HitCfg` 的独立 Delay、ActOnSouce 和 IgnoreAICount，保留原顺序及重复引用，不改写共享实体定义；根引用只绑定根节点自身，不再覆盖包装节点的首个伤害后代。随机包装节点的范围选择使用该次父引用的锚点；仅含旧 ID 列表的图保留原展开时序。命中实体创建／移除的当前接入范围见下文，连锁逐跳生命周期仍未接通。

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

战场状态说明窗口改用所选 884 的原生 `FightStageStatusDialog` 和 `FightStageStatusDialogItemUI` prefab，保留原层级、布局、字体字段、图标背景、动画及 `LoopVerticalScrollRect` 的原始行模板引用。迁移的两个官方类沿原 `ClearCells → dataSource → totalCount → RefillCells` 顺序绑定，复用行启用时更新内容、禁用时释放动态说明订阅。`OfficialBattleStageStatusDialogView` 负责本地目录、窗口实例字体、原显示／隐藏图及现有原生模糊遮罩的接线；原 `FightSettings.FightStageStatusDescTypes` 仅把 26030 指定为倍率动态说明，准备阶段、倍率变化通知、三位小数与原百分比格式分别沿原 provider 分支处理。原生窗口来源为 884，共用模糊遮罩保留独立 854 来源，既有状态图标／文案继续使用已追溯的 HUD 目录；不把它们标为同版资源。原对象、绑定、脚本与输出哈希见[战场状态窗口来源](../../assets/reference/battle-hud/stage-status-native-source-manifest.json)。

顶部状态条从同版 `FightMainPanel/Content/Top/FixedContent/StageStatus` 分支恢复，31 个原对象、15 个脚本字段组件与 `mSlot／mStatusUIItem／mStatusBack／mBtn` 引用保持原样；`FightMainPanelStageStatus` 和 `FightStatusUIItem` 负责原模板克隆、去重后的状态顺序、图标／背景、零层数隐藏及原按钮开窗，`HorizontalLayoutGroupJSZX` 与 `ContentSizeFitter` 按原参数排列和定宽，不再手动设置图标位置。状态条挂接前已核对保留的 854 HUD 的 Content、Top、FixedContent 与 884 原父层级的锚点、位置、尺寸、缩放及旋转一致，来源单独记录。原主面板入场图中的状态条滑入和透明度曲线也逐键与 884 原动画核对；迁移宿主把原动画目标重新绑定到原生子树，沿原播放时钟与插值执行。`OfficialBattleStageStatusStripView` 只接本地目录、字体及经原事件引用解码的点击音；本地宿主拥有克隆行对象，状态更新或禁用时释放行，重新启用时按当前检查点重建。

行动进度的场景底板改用所选 884 的完整原生 `GameRoundScene` prefab，保留四个层级节点、12 个对象、原 `mIconRoot／mLine` 引用、线条与箭头的 sprite、材质、shader、尺寸、位置和排序字段。`GameRoundScene` 与 `SpriteRendererCanvas` 迁移到 `Official/BattleRoundUI`，原隐藏时移至远处、世界坐标投影、相机尺寸缩放及透明度传播由原类执行；Windows Overlay 画布允许空 UI 相机，战斗相机由公共宿主显式传入，并在画布布局后重投影、窗口尺寸变化后重新读取画布高度。`OfficialBattleSceneTimelineView` 只接检查点与头像队列，不再重复实现场景位置和缩放。保留的 854 HUD 中 `RoundUI／Linear` 的锚点、位置、尺寸、pivot、缩放和旋转已按原 float32 与 884 原生字段核对一致，原挂接布局继续使用。原生对象、脚本、转换依赖与输入输出哈希见[行动进度场景来源](../../assets/reference/battle-hud/round-scene-native-source-manifest.json)。头像卡片改用同版完整 `GameRoundCharacterItem_Scene`：72 个原对象、7 个脚本、全部序列化字段、Animator、片段、粒子、材质及纹理依赖保持原绑定；不再向旧配置重建卡片补加组件。`GameRoundCharacterItemScene` 与原接口迁入 `Official/BattleRoundUI`，保留 NPC、人物、幻兽实体、三种守卫、拟态与怪物分支顺序、资料等待标志、禁用／销毁订阅清理、受击／行动／死亡触发器及原 0.5 秒死亡回调。来源见[卡片代码清单](../../assets/reference/battle-hud/round-card-runtime-source-manifest.json)。公共时间轴在停用容器中实例化，在激活前注入配置、头像请求与图集，随后沿原 Init／Refresh／UpdateRoundData 更新；原布局、几何和美术没有修改。完整原生 `RoundUI` 的 17 个对象、4 个脚本组件及两种卡片引用现已恢复，同版 `GameRoundUI` 原管理器通过显式 `IBattleRoundUIBindings` 接入本地队列、实体、伤害、死亡、阶段与同局恢复通知，原预创建 16 个头像、排序、最小间距 0.05、0.2 秒绕回插值、等待实体和死亡队列由原类执行；`OfficialBattleSceneTimelineView` 负责投影检查点及按表现延迟交付通知，已删除重复的队列位置与动画实现。来源见[管理器代码清单](../../assets/reference/battle-hud/round-manager-runtime-source-manifest.json)。普通 UGUI 卡片也按同版完整 prefab 恢复，包含 116 个对象、37 个脚本、五个原片段与控制器；引用的官方平面网格保留全部顶点、UV、索引及其他源字段，只修复 Unity 原生字节数组序列化格式，头像 UV 矩形补回原生 `serializedVersion=2` 以完整导入 0,0,1,1。未改变原 UI 的布局、样式或素材。当前行动者按用户确认的单机时间显示规则投影，全部实体类型接线仍须完成整体验证。

行动进度的玩家头像取景接入所选 884 客户端 `DynamicTexture2DAtlas` 原实现，包含原工厂、ETC2／回退图集、格位分配与扩容、Sprite／UV、纹理所有权及 `GridSpriteImage／GridSpriteRawImage` 消费类。所选 `GamePlayManagerConfig` 的两项头像 ETC2 标志均为真，输入与原生导出摘要相互核验；工厂仍按设备格式支持和 `CopyTexture` 能力选择实现，Windows 不支持 ETC2 时执行官方 `FallBackGridDynamicAtlas`，复制可读头像纹理并创建原 100 PPU Sprite。公共场景适配将调用方已完成的原模型 100×120 取景交给原头像请求／缓存层，再使用原请求键送入场景图集；本场实体身份仅用于本地已完成取景寻址，不查询网络资料或再次生成外观。NPC／怪物图标复用既有已解析原 Sprite 目录。队列清空或头像死亡不提前释放图集，HUD 所属场景销毁时释放图集的 Sprite 与纹理，不销毁取景提供者持有的原纹理。没有修改原 UI 资产、几何、颜色或素材。代码入口为 `Official/BattleRoundUI/DynamicTexture2DAtlas` 与 `OfficialBattleSceneTimelineView`，输入输出及 C#／日志适配见[动态图集代码来源](../../assets/reference/battle-hud/round-atlas-source-manifest.json)。守卫与拟态的正式实体数据接线仍有缺项。

行动进度头像请求层另迁入所选 884 的 `HeadCharacterPortrait`、场景／UGUI ETC2 子类、`IFightDynamicTextureETC2`、`IGameRoundPortraitHandler` 及三种原处理器，复用既有官方 `CharacterPortraitBase／Requestor／Cache` 和请求键实现。保留玩家 ID 的 0 类键、临时外观的 1 类键、持久化标志、先取消旧订阅再申请新取景、禁用后恢复及销毁移除事件的顺序；客户端注入改为显式 `BindAtlas`，职业与外观参数沿用现有离线取景接口的整数和不透明对象。另迁入 `GameRoundPlayerPortrait`、`GamePlayManagerConfig`、`GameEffectData` 与 `FightDynamicTextureETC2`，保留普通／UGUI／场景处理器选择、完整配置字段、默认／自身／观战／阵营列表的底图加载顺序、按路径哈希复用、图集缓存与清空销毁分支；回放黑名单按原枚举整数存储。原序列化字段不变。配置依赖改为激活前显式 `BindConfiguration`，图集由所属场景调用 `Initialize／Dispose`，纹理加载由本地提供者负责原资源的生命期。来源与适配见[头像请求代码来源](../../assets/reference/battle-hud/round-portrait-source-manifest.json)。完整原生卡片现已引用这些原类；同版原 `GamePlayManagerConfig` 的全部字段、三种阵营 ETC2 纹理／Sprite 和六个血条特效 prefab 的引用闭包已恢复，显式绑定于 `BattleRoundPortraitAssets`。公共 HUD 的本地 adapter 消费既有取景／图标及阵营图片，调用方仍拥有原取景纹理；不会经服务器查询或在此生成外观。守卫临时外观和拟态的正式实体投影仍未接通，不把这些分支列为完成。

`MyFightDamage.OnStart` 先向目标发出受伤通知，再查实际所属技能及来源，向来源发出命中通知；之后以目标为播放源、技能来源为播放目标，传入实际伤害实体的 `Damage` 参数。普通图独立播放，结果图按格挡、暴击、普通伤害的原分支选择，状态触发图另行判断。该完整客户端方法、五字段状态覆盖器及原空幻兽能量过滤器已迁移；`OfficialBattleDamageEntityPlayback` 负责激活前注入、实际身份和原 FIFO 对象池生命周期。所选 884 的 1,368 个伤害实体包含 `Global` 下 33 个公共实体，五类图引用、原隐藏／零伤害标志与启用状态均按原 prefab 保留；13202 的原覆盖驱动为禁用，加载时不主动开启。代码来源见[伤害视图迁移](../../assets/reference/battle-animation/damage-view-runtime-source-manifest.json)，原生资产及五字段绑定见[技能图资产来源](../../assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json)。正式步骤中已有归属、状态结果及冻结位置的直接伤害回执已按下述宿主接入；完整人物与 HUD 监听仍见下方缺项。

`MyFightRoleFight` 的原 `OnDamage／OnHit` 事件、碰撞器字段、订阅调用顺序及两个异常捕获边界已迁入。`MyFightDamage` 现在直接调用原角色通知，实体查询复用 `IFightSkillViewServices`，移除额外通知端口；监听器抛异常时，保留原多播委托中断本次后续监听器、捕获后继续伤害视图的行为，日志后端改用 Unity 异常日志。该客户端角色类不读取共享角色字段，`BattleRoleFightViewData` 只承接此空消费面，不替代共享阵营、HP 或服务器战斗回调。角色类入口为 [MyFightRoleFight.cs](../../unity/Assets/Game/Runtime/Official/BattleSkillPlayables/MyFightRoleFight.cs)，凭据沿伤害视图来源记录；正式人物／HUD 的全部事件监听仍未接通。

原共享 `DamageResultParameter`、`DamageTypeData`、三个结果／取值／元素枚举已完整迁入，保留全部字段、七个标志、治疗分类及 `GetDamageByType` 的有符号 64 位计算。`OfficialBattleDamageEntityData` 可显式接收完整结果，原角色受伤／命中通知传递同一值；仅移除联机序列化注解并适配原格坐标类型，不从 HP 差值补算原始结果或状态触发标志。来源与上述伤害视图凭据共用。

原 `CharacterDamageUI` 直接持有数字、提示、元素克制、破韧、真实伤害、图标和背景的序列化引用；以 `WaitForSeconds(mShowTime)` 完成展示，禁用时停止旧协程，复用时重新初始化。深渊数字使用白色，文字转换到线性色，图标、附加图和背景分别使用 `DamageColor` 的四种颜色；破韧、元素克制、真实伤害按原分支顺序显隐。`DamageFloatSize` 随主相机正交尺寸缩放，`DamageAutoSizeTextBack` 监听原文字／字体刷新后调整背景。原数字格式使用 `NumberFormatHelper.FormatBattleByFourWidth`，不是通用的三位有效数字缩写。

原 `CharacterDamageUIManager` 消费角色受伤、状态结算、自损耗血、驱散和技能重放通知。显示值为 `DamageValue - ShieldValue`；隐藏标志优先，其次元素克制、非零值、零伤害过滤器及显示零标志。样式按正值治疗、免疫、回避、未命中、暴击、格挡选择，深渊另选三种样式；颜色按治疗、对应元素的格挡／暴击／普通、默认色选择，样式和颜色的格挡／暴击优先级不同。无伤害实体的状态治疗沿用普通样式和状态治疗色。旧飘字按后一个飘字的原高度及配置间隔向上堆叠；对象池数量按原阶段预热覆盖，完成时从角色列表移除并归池。

## 完整还原部分

原公共伤害图的受伤／暴击／格挡沿 `MyFightDamage` → 原全局 prefab → `MyPlayableAnimator` → 角色原 `AnimationController`／Animator 执行。普通受伤和暴击设置 `damage`，格挡设置 `block_01`，保留各 prefab 的前置状态名单和原持续时间；不在 `OnDamage` 中追加近似动作。3100 人物原分层绘制使用可见 separator 子网格，禁用的主渲染器网格不代表当前姿态。共享公共流程已检查 3100／3206 的实际受伤状态及可见网格变化，并以真实公式结算的暴击／格挡检查原分支。

所选 884 的 13 种完整世界飘字 prefab 已恢复原层级、组件字段、Animator、曲线、原 bitmap Font、字形尺寸、贴图、材质和 Windows shader，载荷位于 `Official/Combat/DamageUI`。完整 `CharacterDamageUI`、`DamageNumberUI`、`DamageFloatSize`、`ElementConfrontationUI`、`DamageAutoSizeTextBack`、`DamageCustomActionUI` 与原枚举、四字段颜色结构迁入 `Official/BattleDamageUI`；复用现有原实体、池接口、语言与中文战斗数字格式实现，保留原执行顺序。原 `CharacterDamageUIManager` 的显示、堆叠、样式／颜色优先级、预热查找、借还、七标志零伤害过滤器与原 `UIBase` 启禁用已迁入同目录，完整 `FightDamageSettings` 原生资产保留四字段颜色、13 项 GUID 与数量、间距；原 `FightSettings` 的预热记录和元素阈值单独投影。`CharacterDamageUIContext` 只桥接本地资源租约、实体查询和实际规则通知，保留技能实体缺席时的异步 prefab 重放检查；不在显示层重新结算规则。来源见[脚本清单](../../assets/reference/battle-hud/damage-ui-runtime-source-manifest.json)及[原生资源清单](../../assets/reference/battle-hud/damage-ui-native-source-manifest.json)。正式公共宿主已将具有完整回执的直接伤害／治疗／纯状态命中交给原角色 `OnDamage` 通知与该管理器，并按实际技能实体的创建时刻播放重放提示；对应旧步骤飘字只在原生路径未接管时保留，避免双播。`OfficialBattleDamageUIPlayback` 绑定原资源、每个实际角色及其移除、原语言字体／文字和元素阈值，预热与通知均经过已有原生实例注入作用域。范围命中的各目标和延迟创建的地块在通知前注册。中文动态字体、材质与 atlas 使用战斗独立副本；原 prefab 在未激活的本地模板上先绑定字体，再交给原池预热，避免 TMP 的 Awake 写回原生资源。原 `game_1000` 的 `DamageFloats` 空子树单独提取为 `DamageUIRoot.prefab`，保留场景坐标、本地 Z=−14 和单位缩放，不挂到 Overlay HUD 画布。暂停和倍速沿原 `WaitForSeconds` 与公共 `Time.timeScale`；隐藏窗口只隐藏 renderer，保留借出对象的身份和归还计时，重新显示时恢复原可见性。实际施法回执另保存已结算的扣血前后值，在本次原技能实体的 `MyStart` 前发送耗血通知，沿原普通数字与自损颜色显示，重放各自持有独立值；不再从后来的生命或技能类 ID 猜测。旧回执没有明确标志时保留旧步骤路径。不创建配置伤害实体的历史派生结算可沿用原命中编号；播放索引按原直接实体选取回执，不因派生项排在前面而覆盖，也不把派生项再次创建为原伤害实体。基础单次命中吸血现走原 10110→10111→10028，各有独立施法、命中、伤害身份与原生治疗通知，旧检查点的无实体派生项仍保留兼容路径。状态及全部派生结果的统一通知仍见下方缺项。

状态免疫的单机裁决集中在 `Offline/Combat/FightStatusApplyCondition.cs`，由 `FightStatusLifecycle.Apply` 的执行期过滤接口统一覆盖添加与续期，不分散到各个技能／被动调用点；调用结束后清除委托。开场战场状态的实际施加也经过同一过滤，初始装配的模板列表保持原装配；开场尚未发送的免疫提示保存在角色检查点，在后续步骤按无直接命中关联的边界发送一次。原 `StatusStatusApplyConditionComponentInfo` 的类型／ID、来源与可驱散条件及 `ConfigMaxCount` 特殊参数替换保留，正次数与拒绝进度随状态实例保存，恢复旧检查点时只补定义。当前四项官方定义的后续技能／状态列表均为空，未据此宣称任意非空后续配置已支持。

状态自定义动作将执行器实际记录的 `StatusCustomActions` 转交原角色的 `OnStatusTriggerCustomAction`，由原管理器及 `DamageCustomActionUI` 执行原文字、冷却变化图标分支、堆叠、动画与回池。实际受伤产生的“霸体”关联本次伤害的 `SourceDamageId`；原 `FightStatusSkillModifyCDComponent.OnInvoked` 立即发出减冷却提示，本地对本次命中 `StatusList` 施加时实际新增的调用记录相同命中编号，并保存 `SourceDamageTargetId`，区分被命中的目标和实际获得状态的角色，支持给自身施加、治疗及纯状态命中。施加前已待发送的记录保留，不归入本次命中。 同一 `StatusList` 被免疫拒绝时同样记录该实际伤害编号与受击目标，提示发送给受保护角色；被拒绝项不触发状态特效成功标志，也不计入“施加状态”。宿主由唯一实际回执取得最终 `Delay`，自然包含接近、父子命中和派生伤害偏移；相同时刻先执行原命中通知，再派发该动作。原命中直接关联要求配置伤害实体，排除共用编号的派生治疗；霸体仍支持替身派生伤害的同目标关联。已排队接管的精确动作不再播放旧步骤提示。零关联的旧回执及非命中直接施加的冷却变化仍保留步骤边界；关联缺失／歧义、未装载角色、`None` 与未知动作保留兼容路径，不据此推断发生时刻。

- `OfficialBattleRuntimeController` 连接战斗会话、准备界面、场景宿主与结算回调；`FightBattleCheckpoint` 保存可恢复的本地战斗状态。
- 回合步骤、格网与单位、受控站位、技能装配、目标选择、命中及部分伤害／HP／护盾／状态规则已有共享实现。各模块仍按官方配置和已迁移的客户端代码执行，不代表所有技能及组件覆盖完整。
- 公共战斗 HUD、状态列表、技能槽、战报、角色与部分 NPC 资源使用原配置和资源绑定；表现、音频、动画和资源依赖仍有缺项。
- 玩法专属入口通过公共宿主提供站位、角色状态、战斗快照及结果接口；实际完成范围由[所属地图](../maps/)或[玩法页](skills.md)维护。

### 公共战斗模块的 interface

成功驱散沿原 `CharacterDamageUIRole.DisperseStatus → CharacterDamageUIManager` 播放。执行器保留兼容 `DispersedUnitIds`，同时在实际伤害／纯状态回执保存 `HasDisperseNotification／DisperseNotificationIndex`，同目标多次操作用各自位置区分；失败或自然到期不产生关联。原伤害实体完成 `MyFightDamage.OnStart` 的目标受伤、来源命中和配置图播放后，才按该实际回执交付驱散，不再另设浮点计时器。只有明确关联且可创建原伤害实体的操作接管旧提示，旧检查点和未接管对象继续保留兼容路径；暂停、隐藏、倍速、堆叠及归池仍由原显示实现执行。

公共交互验证的十组场景共用 `OfficialBattlePublicFlowValidation.Steps`，Editor 入口仍为 `OfficialBattlePublicMovementAcceptance`；专用 Windows 构建入口为 `OfficialBattlePublicPlayerBuild.Build`，启动器为 `scripts/test_battle_public_player.ps1 -Workspace <本会话目录>`。验证程序集仅在 Editor 或显式 `ZJCS_BATTLE_PUBLIC_VALIDATION` 构建中启用，通过独立参数跳过正常存档启动，使用带标记的隔离目录与内存档案；原开始按钮沿实际射线命中的模拟指针执行，保留原模型、HUD、技能、命中与伤害管理器，不操作系统鼠标／键盘。该入口本身不改变游戏规则或资源。

`OfficialBattleRuntimeController` 是各玩法共用的 module。外部 seam 位于 `Begin(OfficialBattleRequest)` 和 `IOfficialBattleModeAdapter`，规则与表现的实现集中在公共战斗目录内。

技能命中能力已拆为 `IFightHitPattern.Plan(FightHitPatternContext)`，由原技能／命中配置选择点目标或范围实现；调用方取得包含目标、命中格和延迟的 `FightHitOccurrence`，再逐次进入既有官方公式、护盾、状态和伤害结算。随机父节点已选定的锚点不再重复消耗随机数量，明确的随机目标数量先筛选实体，再对所选实体覆盖的身体格生成伤害；治疗保持原有处理。多格攻击按原 `HitScopCfg` 的顺序与 Delay 逐格处理，同一格的同一身体声明不重复计数；同一怪物占据不同范围格会产生多次伤害。命中格与延迟随伤害回执保存，旧检查点缺少新字段时保持兼容。多段时序、衰减、子技能、状态与地形的能力拆分尚未全部完成，现有官方基础公式及执行顺序继续保留。

行动进度 HUD 由原 `GameRoundUI` 管理器计算排序、逆向兄弟索引与移动。`OfficialBattleSceneTimelineView` 将本地检查点转换成原管理器所需的驱动数据：有效 `Timeline.Acting` 的显示 `NextTime` 取本次行动的 `Timeline.Current`，其余实体取各自 `Next`；行动切换后，上一行动者恢复显示其下次 `Next`。此项已获用户确认，只转换显示数据，不修改检查点的时间、队列或战斗数值；缺少有效行动者时按 `Next` 回退。

`OfficialBattleTimelineLifetime` 在原管理器之前恢复独立卡片场景，保证原池预热实例先运行 Awake、取得缓存 Transform；避免隐藏后返回时在尚未激活的场景内取用未初始化卡片。原管理器的池、排序、动作和卡片初始化顺序保持不变。正式公共控制器的连续两场声音专项已同时覆盖界面隐藏／返回及原卡片重新显示。

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

`FightBattleSkillCastEvent.HitSelections` 现由真实执行器在该次选敌之后、伤害／位移／子技能结算之前记录。每次引用在所属施法内有独立序号，保存命中 ID、随机父组、施法方向／翻转／起点、相对施法的配置延迟、随机实际抽中的格、范围展开使用的效果中心，以及目标当时位置、逐身体格与逐格延迟；全部按值复制，后续角色状态和复用的选择列表不改写回执。嵌套施法保留独立列表，步骤级偏移沿所属施法的 Delay 使用，不重复写入局部时间。新增字段随现有检查点保存，旧存档未提供时保持空列表，不重新抽取随机或重放伤害。该列表是执行选择快照；固定命中、召唤及已有触发技能命中回执已由下述宿主转为原实体创建／移除，随机根包装节点也已有独立的无伤害回执；嵌套随机／自定义包装及特殊魔王直接清场仍未完整记录，不能据此宣称所有原命中演出已接通。

`FightBattleHitTiming` 将已有伤害／治疗命中选择的基础飞行时序与原 `FightHitViewFormula.CalcSkillHitDelay／CalcSkillStartPos` 对齐：以本次施法的冻结起点和实际命中实体中心计算距离，保留源偏移、Z、X 翻转、目标偏移与旋转中心；范围内各受击者共用这一飞行时间，已有逐身体格命中再叠加原范围条目的 Delay。随机选中同一大怪的不同身体格时，各格保留独立命中和距离，不退回怪物中心，也不因实体列表重复而额外复制伤害。选择回执增加每个效果中心的到达时刻，以及受击条目对应的中心下标，随检查点保存；旧直接命中定义缺少轨迹时只补回官方不可变配置。固定命中的正式创建／移除由下述宿主接通，其他类型和连锁逐跳起点与飞行调度仍未完成。

状态层数的延迟覆盖已接到正式结算，入口为 `FightBattleHitTiming.StatusStack.cs`。按原 `MyFightHitExtDelayInfoOverriderByStatusStack.GetStatusCount`，先检查来源实体的状态／技能代理，再读取本次施法冻结的共享层数；存在共享项时即使为零也不回退到个体状态，否则使用按施加顺序遇到的首个同类状态，覆盖下标为层数减一。按原 `MyFightHitBase.UpdateDelayInfo` 与共享 `SetValue` 分别覆盖五组参数；列表为空或下标越界时保持原基础值，不将下标钳制到最后一项。当前固定配置中使用该组件的 12030／13390／14000 均按共享键 24029 选择十组源偏移，其他四组列表为空。覆盖参数逐命中复制，不改写共享定义；伤害到达时间、效果中心时间和带显式有效标志的覆盖下标一同保存到选择回执与检查点。原命中视图仍从同一施法快照执行原覆盖代码，完整战斗的命中实体创建接线仍未完成。

`OfficialBattleHitReceiptPlayback` 已接入正式 `OfficialBattleSkillEntityPlayback.Start／Tick／Dispose`，将固定命中、召唤和已有触发技能命中回执转成原命中实体的创建和移除。每个效果中心使用独立的 `EcEntityIdentifier`，直接关联所属施法的实际身份；原配置由既有解析器读取，原 prefab 继续执行飞行、范围图和状态偏移覆盖。命中格、方向、翻转和创建时刻来自冻结回执，不反查已移动的最终目标；到达时刻之后等待全部原范围条目的 Delay，再叠加原 `DestroyDelay`，其共享 Info 的说明明确为“所有 damage 都触发之后多久销毁自身”。原范围局部坐标继续使用官方浮点中心、先翻转后旋转及取整公式。创建和移除均作为时间分段边界；同刻到期时先创建命中，再将所属技能交还原 FIFO 池。缺少 `EffectDelays` 的旧回执从已绑定的原 `DelayInfoProvider` 计算飞行时间，保留状态覆盖。触发技能命中沿原 `MyFightHitTriggerSkill`／`MyFightHitBase` 执行；同 ID 的重复引用和空格回执均保留各自创建时间、格位与实体身份，原 `SourceToMeAnchor` 继续决定连线起点，不重新选敌或结算子技能。召唤命中沿原 `MyFightHitSummon` 及其 BaseScopes 播放，旧回执继续从原视图解析飞行参数。这里只消费已有回执，随机／自定义目标包装和连锁实体的完整生命周期仍未接通，召唤角色模型的完整创建／移除演出亦未完成。

怪物／幻兽／替身召唤的位置现取本次命中选择的实际效果中心，依原 `FightHitSummonComponentInfo.Init` 排序后的 Base 格，经原 RealCenter、先翻转后旋转和取整公式得到；多个效果中心分别展开。`FightHitSummonCfgBase.ReferenceType` 的原注释明确用于属性继承、视野组及阵营参考，继续用于这些数据，不再拿参考实体的位置／朝向替代命中格。所选怪物／幻兽／替身的召唤偏移均为零，范围概率及组件概率均为 1；原放置合法性、上限、继承属性和状态注册继续执行。地块召唤的旧落点计算未在本项修改，17001 唯一非零额外偏移的坐标系仍待用户确认；角色实体的出现时间与完整入场演出也未由命中视图自动补齐。

无伤害随机根包装节点现由 `FightBattleHitCollector.CollectAll(includeRootViews: true)` 按每条根引用记录，默认参数继续只返回原玩法命中列表，既有 AI 范围／命中计数不包含这些节点。执行器的 `IsViewOnly` 分支只复制父节点的锚点、创建延迟、冻结中心及飞行时序，不抽取目标、不产生受击条目，也不调用伤害或逻辑子技能；子节点仍持有原独立随机组与排除记录。所选配置扫描到 76 个此类根节点，全部有范围且基础飞行系数为零，未引用层数延迟覆盖；嵌套随机包装及自定义包装未在本项扩展。父节点回执随原检查点保存，旧回执缺少父节点时不从子结果推断或重抽。原父视图的销毁时机仍有服务端缺口，已提出具体问题，正式播放暂未消费这些父节点回执。

伤害归属回执按共享 `FightDamageComponent.SkillId` 的实例语义保留本次施法归属：`FightBattleSkillCastEvent.DamageEventIds` 记录该次执行直接创建的实际伤害序号，记录点位于正式伤害加入步骤之后、嵌套响应之前。同来源同技能的重放各持独立列表，子技能记录自身的伤害，父技能不使用连续序号区间猜测归属；零伤害条目也保留。该记录不增加随机抽取，不重跑伤害或选敌，不修改伤害标志、延迟及结算。仅有表现的 HP 消耗／派生同步条目不据此伪装成直接命中；缺少此字段的回执保持空列表。这是为原实体接线增加的离线执行回执，现由正式伤害播放宿主消费。

`OfficialBattleDamageReceiptPlayback` 沿正式 `OfficialBattleSkillGraphPlayback.Play` 接收步骤回执，在每次原技能实例创建后只取该实例的 `DamageEventIds`；伤害时间减去所属施法 Delay 一次，再按原 `FightDamageComponent.Duration` 创建／移除原 prefab。命中与伤害在技能结束边界先派发，再移除技能，保持实际技能身份可查询。完整数值、伤害标志、元素、结果分类和状态结果直接投影到原 `DamageResultParameter`，不重算公式、状态、选敌或随机；治疗的 `AbsoluteDamage` 字段保存已经算出的原 `AbsoluteCure`，有效治疗及统计仍沿原结算。范围受击保存实际身体格，点命中在本次结算及其位移前保存目标位置；位置、状态结果或施法归属缺失的旧回执不从当前场景推断。角色装载复用已有原 `MyFightRoleFight`，缺少时添加原通知组件；原格网实体保持自身绑定，不被单位投影覆盖。状态覆盖器消费该次施法的状态快照，原通知与五类图仍由未改写的 `MyFightDamage.OnStart` 执行。

原伤害图使用的角色材质接口已为 33 个已安装配置恢复：`OfficialBattleActorMaterialBinding` 仅按同版原 prefab 的根节点、启用状态和完整引用路径添加原 `MeshMaterialProperty`；已有组件保持原绑定，所属单位租约在图停止后释放新增组件。现存原 `AppearanceRenderer／Lite` 继续负责原独立材质集合；尚未装载该外观组件的原生骨骼后端使用其现有每角色材质实例和原 `MeshRenderer` 引用，不生成新 shader 或材质。配置输入及绑定见[角色材质来源](../../assets/reference/battle-animation/actor-material-bindings-source-manifest.json)。另外 36 个已安装幻兽配置已接入下述完整外观材质集合，保留原 `MeshRenderer` 空引用。

`OfficialBattleMonsterAppearance` 统一装配 68 个已安装怪物、幻兽及召唤物的原 `AppearanceRendererLite`、根 `MeshMaterialProperty` 和材质引用；`BattleNativeMonsterAppearanceCatalog` 保存原 renderer 路径、引用是否为空、精度及网格副本引用顺序。67 个原 Low 配置保留图集／占位材质及原贴图加载器；3803 保留原 `LevelOfResolution.None`：原 AppearanceConfig 无其路径、所选完整资源清单无对应加载器，沿原客户端空加载器分支保留八份材质的原纹理与三类 shader，仅为三张骨骼图集页创建独立材质。四个原 `CustomRenderExistingMesh` 复用正式 prefab 的迁移代码和原启用字段，将原材质到独立材质的映射转接到各自描边／隐藏替换材质，不克隆五份替换材质。`OfficialBattleLiveMonsterModelView` 与既有幻兽跟随视图共用该入口，退出时先释放材质属性及外观，再释放加载器路由。保留幻兽原空 renderer；未修改原 shader 能力：普通骨骼 shader 支持受伤变色、不含 `_DitherAlpha`，3803 的混合 shader 由原属性收集器拒绝不共同支持的变色字段。输入、对象指针、ASTC mip 转换、显式 `Combat/NativeMonsterAppearance/catalog` 绑定及输出哈希见[原生角色外观来源](../../assets/reference/battle-animation/native-monster-appearance-source-manifest.json)。该接线不代表全部角色状态表现或 Windows Player 验证完成。

`PartnerNativeBattleActor` 沿既有模型、Animator、附件和阴影入口接入原同伴外观：34 份原实体配置中的 35 个 `AppearanceRendererLite` 及 34 个根 `MeshMaterialProperty` 保留完整引用、Low 精度、启用字段、空 MeshRenderer 和空网格副本列表。11005 的根收集器仅引用主身体，第二身体保留独立外观。`PartnerNativeBattleAppearance` 在模块内部管理同一角色的原 `AppearanceAssetManager`、按需贴图路由和销毁顺序，调用方仍使用现有 actor 接口；网格绘制通知驱动原加载器，原 atlas／占位材质切换到已核验的 Low 页。地址计算使用原 `AppearanceAddressUtils` 所读取的骨骼数据名称，完整 SkeletonDataAsset 名称仍供原附件绑定使用。既有直接使用骨骼网格的技能后端保留自身材质生命周期。原对象、原代码、ASTC mip 转换、359 项资源输出和显式 `Combat/NativePartnerAppearance/catalog` 绑定见[同伴原生外观来源](../../assets/reference/battle-animation/native-partner-appearance-source-manifest.json)。11015～11022 仍保持既有正式战斗排除，其原 Low 目标只有 24×24，不能据此声称这八名同伴已完整恢复。原 Animator 生成器另核验并保留目录中既有的 10607 额外控制器及其原片段闭包，避免重建 34 名战斗同伴时删除该绑定。

### 原生战斗声音

匹配的官方 Wwise Unity Integration `2022.1.18.8567.3199` 已迁入 132 份核心 C#、Windows Editor Profile／Player Release 引擎及八个 DSP，恢复原全局／Windows 初始化设置、40 个声音包及 `Combat/Wwise/catalog`。依据同版 `CAkFilePackageLUT` 将 Android 语言表转换为 Windows UTF-16LE，保持 bank／媒体字节、ID、对齐及原 HIRC／RTPC／DSP 配置。入口、转换和逐文件哈希见 [Windows 声音运行素材](../../assets/reference/battle-animation/windows-sound-runtime-source-manifest.json)。

`OfflineBattleWwiseSoundHost` 通过现有声音接口复用原 AkGameObj，按 Minimum 包→Init→Minimum→关联包／bank 加载，接入 Spatial Audio、通信、回调与原设置的清理顺序；空环境只发送一次零 AuxSend。原四项音量 RTPC 按 `(int)(volume * 100f)` 设置，倍速使用原 `414249091`。原 SDK 的四处 AkVector64 字段 getter 返回借用地址，Windows Mono 的引用类型返回封送会释放内部地址；依据原 Profile／Release DLL 指令适配借用指针取值，另修复三双精度值按 Windows x64 ABI 返回，保持原数值及公共返回类型。官方代码、原 RTPC 资产 GUID 及本地绑定见 [原生宿主来源](../../assets/reference/battle-animation/native-sound-host-source-manifest.json)。

256 个 Game ID 对应的 229 份当前原场景 prefab 已逐一核对：默认 `AudioListener` 位于 `Main Camera` 下，本地位置 `(0,0,20)`、单位旋转／缩放，绑定 AkGameObj、AkAudioListener 和 FollowSceneActiveSelfController。独立 `Combat/Wwise/AudioListener` prefab 保留原节点与字段，仅重绑脚本及根父级；迁入同版 SDK 的 11 份监听／声源／环境／触发依赖及 24 份 Wwise 值／引用代码，复用已有原场景激活脚本。绑定和逐场景证据见 [战斗监听器来源](../../assets/reference/battle-animation/sound-listener-source-manifest.json)。

`OfflineBattleWwiseRuntime` 常驻拥有原引擎与包，战斗租约仅管理自己的原监听器；引擎先于监听器初始化，每帧沿原 LateUpdate 顺序派发回调／RenderAudio，并以实际 `Time.timeScale` 更新速度。`OfficialBattleRuntimeController.Begin` 在已有官方环境和表现相机、未指定自定义声音宿主时取得租约，传入原技能图；隐藏／返回战斗同步监听器，退出先执行原图 Stop，再停用／销毁监听器，应用退出才释放引擎。原 Player 暂停／焦点事件沿已迁 SDK 设置执行；该分支的 Windows 实际切换尚未验证。

`HomeAudioSettings` 将当前角色保存的总／音乐／语音／音效四项音量传给原 RTPC，加载前沿现有单机默认值，设置成功或保存失败回滚仍复用原保存流程；不改变已确认的默认值及存档。测试独立将原生最终输出静音，音量 RTPC 和声音事件照常执行。设置控件与保存规则见 [设置与本地存档](settings-and-save.md)。

`OfflineBattleWwisePackages` 统一 Editor 素材路径与 Player 的 `StreamingAssets/Combat/Wwise` 路径；`OfficialBattleWwisePackageBuild` 在 Windows 构建前核对 40 包的来源，构建后逐个复制并复核大小和 SHA256，不复制 `.meta` 或在源码树中重复载荷。

`Official/BattleBgm` 直接迁入原 `BgmManager`、事件键配置、接口、播放句柄及声源 provider，保留场景堆栈、同键引用计数、旧场景暂停／恢复、非栈顶移除与 FIFO 延迟队列。仅隔离命名空间、移除追踪日志、修复反编译的 `TryDequeue(ref)`，将原宽 Wwise 代理缩为实际使用的两项操作；原算法与执行顺序不变。[代码来源](../../assets/reference/battle-animation/bgm-runtime-source-manifest.json)记录原输入和输出。原生宿主沿原哈希发送事件名；未注册的声源复用既有 AkGameObj 路径，原 SDK 声源已注册时保留其自身生命周期。

新增原 `Battle_BGM_Switch`、00～06、Public 及共享音乐包。按 APK 优先核对实际包、1,427 个原对象与 458 份完整媒体（含流式预读片段与其他包内共享媒体）；音频包与原 RTPC、切换容器、播放列表和转场不改写。[音乐依赖来源](../../assets/reference/battle-animation/bgm-sound-dependencies-source-manifest.json)与 Windows 转换来源分别保留原包及输出哈希。原生 Play Mode 已检查真实 Switch 值、原音乐播放编号、嵌套场景操作和延迟停止；记录适配器另外核对重复键、非栈顶移除、未知停止、句柄幂等、延迟队列及即时 Switch。

原 `GameBgm`、`GameBgmConfig`、`BgmEventConfig`、扩展方法和 `BgmGameObject` 已迁入；229 份场景音乐节点覆盖 256 个 GameId，88 份原 Wwise 引用、三份原配置和全局音乐节点保留原序列化字段。全局节点沿原 SDK AkGameObj 与非默认 AkAudioListener 自监听，不附着战斗镜头。原关卡表的 55 条覆盖与主题表的一条配置由 APK 优先解码；主题优先于关卡，事件配置依次取明确路径、场景覆盖、全局默认，非空覆盖组禁用场景开关后设置原 Switch。正式控制器在场景初始化完成后播放，释放会话时停止；隐藏仅改变镜头监听器和场景节点，保留音乐专用监听器，返回时恢复开关。隐藏状态初始化先让原 GameBgm.OnEnable 应用开关，再同步节点可见性。来源及转换见[场景音乐素材](../../assets/reference/battle-animation/bgm-scene-source-manifest.json)，入口为 `OfflineBattleWwiseRuntime.Lease.PlayMusic`。模式可传入已选中的 `FightThemeId`；自动主题选择条件尚未迁移。

静音 Play Mode 已加载全部 229 份原节点，验证专用自监听、真实开关与播放事件、主题压过关卡、关卡配置／场景配置优先级、隐藏／返回及隐藏初始化；正式控制器连续两场通过原开始按钮模拟输入，核对音乐与技能音效的真实编号、伤害、四项存档音量、暂停和释放，实际画面已查看。独立 Windows Release 声音探针已通过 40 包加载、1,747 份事件引用、五处向量封送与原音乐管理器的真实开关／音乐编号检查，最终输出为零并正常退出；离线渲染同步等待，播放编号先查询数量再完整读取。该探针不包含正式 HUD／人物战斗。完整公共资源构建及音效专项增量构建随后通过，两个编辑器以 0 退出；Direct3D 11 Windows Player 的十一组公共流程全部通过并正常退出，80 张实际运行画面已查看。新增原场景 1000 检查原 BattleMusic 开关、BattleBgm_Play 与实际技能图的原生播放编号、原开始按钮模拟指针、四格跳跃、三次伤害、暂停、隐藏／返回与退出清理；既有人物／怪物、耗血、吸血、状态、暴击／格挡、3803、3204 和原同伴外观回归继续通过。构建入口在依赖收集前沿正式流程注册 URP，检查原 shader 程序域，并将构建报告和实际错误日志共同作为成功条件；没有修改原 shader 程序或素材。静态元数据检查通过（152,818 GUID）。声音宿主来源清单同时记录共享 Player 夹具与启动／构建工具哈希。该受控场景验证不代表全部技能、完整音乐时长、所有切换容器／混音或完整 HUD 均已还原；正式发布包尚待构建与运行。

隔离完整工程已通过编译、`ProjectTools.Validate`、静态元数据与静音 Play Mode：两次显式引擎生命周期、四处借用字段及按值转换、原 Event 播放编号／原生声源坐标／缓存宿主／注销；真实监听器 prefab 的相机跟随、场景进入／退出／再进入、默认监听列表及销毁；两个租约复用引擎、音量与实际 1／2.3／0 倍时间、退出 Play Mode 后引擎终止。正式公共控制器专项连续两场使用原场景与真实技能图，以模拟指针点击原开始按钮，核对实际原生播放编号、伤害、四项存档音量、暂停、隐藏／返回及退出监听器清理，正常以 0 退出；已查看其实际运行截图。

独立 Windows x64 Release 声音验证 Player 已在 GPU 静音运行并以 0 退出，构建目录引擎哈希与同版原 Release DLL 一致，全部 26 包和 1,735 条事件引用、四处借用字段及按值转换均通过。该程序仅验证声音包及运行库；正式游戏 Windows 构建、入口与战斗声音尚未验证，短帧／播放编号检查不代表全部效果器和完整时长混音输出已验证。新增素材尚未进入完整备份快照。

## 自定义实现

- 用户确认基础 `FightHitExtHemophagiaComponent` 的单机执行：逐次取本命中实际扣除的 HP（排除护盾、溢出伤害及后续共享／复活），乘 `OnceHitHemophagiaPer/10000` 后向零截断，不额外封顶比例；正数在施法者当时自身格释放原 `Battle.HemophagiaEntityId=10110`。`FightBattleExecutor.Hemophagia.cs` 将该数值作为原自定义治疗传入统一技能执行，保留原 10111／10028、治疗增减益、禁疗、25136→24435 转盾、原技能状态白名单与播放图；满血仍生成治疗回执和演出，统计只计实际恢复。旧直接加血及手工补发监听已移除，避免绕过原治疗链或重复通知。原 APK 仅保留扩展属性与空服务端代理，公式基数和比例取整为用户确认的单机规则；独立 10544／25023 的吸收与 13960／13965 的命中完成吸血沿各自已确认规则，不改用本项基数。

- 用户确认原 `FightDisperseModification` 的本地通知时序：只有实际移除状态时才发出本次驱散操作的一次通知，时间取该次命中的结算时刻；同一时刻先播放该命中的受伤通知，再播放驱散提示。无可驱散状态、概率失败和自然到期不通知。成功回执现已关联具体命中，并在原伤害实体启动后交付原管理器；验证范围见下文。
- 用户确认 `StatusStatusApplyConditionComponent` 的本地次数规则：`MaxCount≤0` 不限次数，正数只按实际拒绝施加的次数消耗；达到上限且 `DestoryOnTriggerMaxCount=true` 时移除保护状态。其他限制继续按原配置的状态类型／ID、来源阵营和可驱散条件检查；APK 仅保留配置和 `OnInvoked` 的客户端免疫提示。已在统一状态施加入口落实实际拒绝计数、上限移除和检查点恢复；初始装配不制造施加事件。原 24671 按 `Battle.StatusMutexNotShowImmunityFloat` 静默拒绝 24670，其他保护的实际拒绝沿原 `OnInvoked` 显示“免疫”。

- 用户确认独立 `FightDamageComponent.SkillElemntType` 与 `DamageResult.SkillElementType` 的单机赋值均取本次实际技能元素，消费执行器已有的 `ElementType`，包括技能元素被状态改写的结果；APK 不含前者的服务端赋值。原管理器仍分别用它们判断深渊样式与颜色。

- 用户确认伤害状态特效的单机规则：本次伤害的 `StatusList` 至少成功施加一个状态时，`IsDamageTriggerStatus=true`，包括给来源自身施加；概率失败、免疫或拒绝添加均为 false，其他被动连带状态不计入。此为 APK 缺少服务器赋值时采用的单机裁决；原客户端仍按该布尔值独立播放 `myTriggerStatusPlayable`，执行器在原施加顺序中记录 `HasDamageTriggerStatus／IsDamageTriggerStatus`，成功续期同样计入；列表前项成功不会跳过后续项，也不重新抽取随机数。旧检查点缺少结果时保持未知，不从现存状态反推；正式伤害实体只消费已知的实际结果。
- 用户确认伤害实体的单机姿态规则：范围受击使用本次实际命中身体格，点命中使用该次受击时的目标位置，方向与 X 翻转沿用本次施法快照。APK 未包含服务器创建时的赋值代码；本地回执在结算及由此产生的位移前冻结位置，原 `MyTransform／MyGridTransform` 消费该位置、原方向旋转和翻转，不追随后来变化的单位坐标。
- 用户确认 `DamageResultParameter.ResultType` 的单机分类：治疗结算为 `Cure`，伤害结算为 `Damage`（包括零伤害和免疫），两者均未执行为 `None`。APK 缺少服务端赋值，正式投影消费已有治疗标志及公式结果证据，不通过 HP 变化的正负猜测分类。无公式／结算的 `ActionType=None` 命中保留实际状态、驱散、位移及配置子技能并生成正式 `None` 回执；不进入伤害／治疗结算、次数和专用被动，仅继续符合条件的命中监听。原治疗技能即使互斥类型为 None，仍沿原治疗分支结算为 `Cure`。尚不支持的状态逻辑过滤器不据此宣称已实现。
- 用户确认一个技能的伤害、护盾、增益和减益独立统计；增益与减益均纳入人物“施加状态”的统计；人物显示沿用原 `FightReportUIUtils.GetApplyStatusScore` 的分类权重和破韧分，纯状态技能也计入。执行器复用 `FightStatusLifecycle` 的实际成功记录和 `RecordPendingStatusApplications`，不按 `None` 回执额外加一次：成功添加、续期各记录实际施加，失败／免疫／拒绝不计，保存恢复不重复统计。原 `StatusType=None` 的纯护盾沿护盾量统计，不改写为增益或减益。

- 用户确认行动者时间采用上述显示投影：本地执行器在演出前已推进其下次 `Next`，当前行动动画期间只向行动条传入本次 `Current`，切换后再显示下次时间；原管理器、UI、动画与战斗时钟保持原职责。

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
- 用户确认大型目标的单机选敌规则：以施法者中心格为原点，技能范围覆盖目标任一身体格即可选中；接近移动也按目标身体格寻找施法位置。`FightBattleTargeting` 的范围顺序、接近距离与候选位置使用同一身体占格，候选位置去重；施法者自身的身体不扩展技能范围。普通移动改为在欧氏距离不超过当前行动力的圆内枚举落点，检查落点地形、阻挡及完整身体占格；中途不按四方向步数寻路，回执只保存起点和落点的一段原小跳。技能自身配置的特殊位移仍按原配置执行。用户已确认所有多格攻击范围技能按范围格结算：每格独立命中其覆盖的合法目标，大怪的多个身体格均在范围内时逐格受伤，不按实体合并伤害；技能仍只释放一次，原配置的衰减、随机数量及其他明确限制继续适用。此规则已接到上述命中模式接口，规则测试及 13740 实际 Play Mode／Windows Player 通过；13740／13745 的地块爆炸同样遵循此规则，专项说明见[技能](skills.md)。
- 用户确认普通行动先移动再释放技能；当前回合没有通过 CD、次数、控制和效果条件检查的可释放技能时，保持原地，不执行退避。原 `level_prop` 的 50 类各阶级均给出 `MoveDist=4`，百分比与状态修正继续走原公式；四格范围按 `dx²+dy²≤16` 判断。原 `FightRoleCharacterComponentInfo.MoveSkillId` 明确为“小跳模式”，3100 绑定 15300／15301；普通移动只向原 `MyFightActionSkillMove` 提供一个落点，沿原 playable、动作占用和距离／速度公式播放。
- 用户确认攻击、对敌减益的选敌分支互斥：有“优先选择为攻击目标”状态的敌方时必须选择该目标，不因大怪、群怪或血量改选；没有标记敌人而有大体型敌人时，以覆盖最多的大怪身体格为目标；其后才选择一次能命中最多敌方的落点。未进入以上分支时，本回合最后一个战技优先安全位置，其他战技优先移动距离短的位置，再按敌方生命百分比排序。同一有效分支多个落点同分时选远离敌方整体区域的落点；可移动范围无法满足时向对应目标的合法施法位置尽力接近。`FightBattleTargeting.Movement` 集中处理落点、原命中范围变换、去重身体格及最终选敌，普通移动和施法使用同一决策；随机范围只统计可能覆盖的身体格，不预先执行伤害或消耗战斗随机数。安全性暂以到最近敌方身体格的距离、再以到全体敌方最近身体格距离之和比较，这是该单机规则的落地方式；APK 未含服务端候选落点算法。增益、治疗、防御沿技能原效果条件及目标配置选敌，不使用上述敌方攻击分支。单目标分支边界待用户确认：当前暂将“所有有效落点最多命中一个敌人”交给第 4／5 条，只有可同时命中两个以上敌方时进入群怪分支；此边界不列为已确认的官方或单机规则。
- 用户确认命中实体继承本次技能的方向与翻转，使用原 `RealCenter` 变换局部范围。`FightOfficialCombatCatalog` 读取各命中实体的中心，命中图将普通与随机父范围的变换配置分别传递到叶节点；执行器在技能开始时捕获方向／翻转，普通命中、随机选格和 AI 覆盖统计使用同一官方坐标变换。旧检查点从原配置补回缺少的变换元数据，保留 HP、CD 和释放计数；显式编写的世界偏移夹具不作二次变换。
- 用户确认连锁命中按逐跳起点处理：首跳从本次技能的施法位置开始，后续每跳从上一跳实际命中落点开始。官方 `MyFightHitChained.myHitToNextHitPlayable` 的字段注释也明确说明“第一次 hit 的起点是技能位置”；共享 `StartGridPos` 的赋值实现未随客户端提供。下一跳按用户确认限制处理：`Repeat=false` 排除本链已命中过的目标，`Back=false` 排除上一跳目标，`PreferUnvisited=true` 优先未命中过的目标，其余按距离、实体 ID 排序，无候选则停止。原 `Scope` 是伤害范围（10821 仅有当前格），下一跳搜索范围的服务端实现未提供，用户确认以上一跳实际落点为中心，平移原技能 `Range` 搜索；伤害范围仍使用各命中节点的原 `Scope`。这些规则尚待接入真实命中创建事件，不能据此视为连锁执行已经完整还原。
- 用户确认子命中相对父命中触发时刻等待各自延迟。当前 `HitCfg` 展开累计沿途已保存的固定 Delay；例如 10910 的父引用 0.37 秒与 10911 七条 10912 子引用的 0～0.6 秒，投影为 0.37～0.97 秒。此累计是单机调度规则，客户端只有 `FightHitBaseComponent.HitDelayTime` 状态，未提供服务端子实体调度。动态父命中的距离延迟、实际命中后的选敌／位置变化及逐跳创建仍须由后续命中生命周期处理，不能视为已恢复；伤害叶节点的既有距离延迟仍由执行器计算。
- 用户确认 `FightHitTriggerSkillComponent` 的 `SelfTargetPosResolver` 使用命中实体的实际格子作为子技能原点，包括随机抽中的非中心身体格，方向／翻转继承该命中。所选配置中该类型对应 13522→13523；APK 仅保留 resolver 类型及客户端命中格播放，缺少服务端赋值。执行器从本次选择的 `EffectCenters` 冻结原点，在实际命中触发时刻创建子技能，子技能自身延迟继续累加；施法者身份、属性和真实位置独立保留。新回执显式保存技能实体位置，规则范围与原实体 prefab 使用同一原点和方向；旧回执缺字段时保持原来源位置默认值。13522 原 `DamageId=0` 不创建伤害视图，保留原命中图、触发和后续真实伤害；正数伤害配置缺失仍报错。
- 地图专属等级、选向、奖励和失败回滚只在所属地图／功能页维护，避免在本页重复定义。

- 用户确认共享状态首次施加按剩余额度分配；不足时部分分配，剩余额度为 0 则不创建新状态。已有状态加层使用同一总上限，状态结束释放占用。首次创建前的分配裁决是单机规则，APK 已有后续增减与结束回收公式。

随机父命中沿用户要求以官方客户端 `MyFightHitBase.PlayScopePlayable` 的命中格和局部范围展开语义执行：保留实际抽中的身体格，不再将落点改回目标中心；多次抽中同一实体的不同格仍是独立落点。允许抽中空格时保留该次选择，其子范围可命中邻格，范围为空时保留零伤害选择供后续表现使用；组内目标／格子排除与随机流沿原有处理。距离延迟及无范围命中与所选格的完整关联仍须后续接线，不将范围格修复等同于全部命中时序完成。

## 尚未实现

- 原管理器已接管完整直接伤害回执、实际技能重放、已知扣血通知及已记录的状态自定义动作；已关联命中的成功驱散由原伤害创建回调通知；状态结算仍沿旧步骤驱动；非命中直接施加的冷却变化等无明确关联的状态动作仍缺逐次发生时刻，须继续补齐共享通知与时序。未接管或旧检查点缺证据的结果仍保留原有兼容路径；全部飘字与特殊派生结果尚未迁移完成。

- 原伤害实体已接到具有完整归属、位置及状态结果的直接伤害／治疗／纯状态回执；派生同步、旧无实体吸血等不在该施法直接归属列表中的表现、旧回执未知字段及全部特殊组件仍未完整覆盖。原人物受伤动作、全部外观后端的材质变色及正式人物／HUD 的全部通知监听尚未完整接通；已有飘字与代表图验证不代表这些部分完成。
- 官方服务器缺失的 AI、随机、结果裁决及复杂触发条件尚未完整恢复；未确认的行为不作推测。
- 完整技能／命中／状态覆盖、同来源与叠层边界、特殊组件时序和实体生命周期仍有缺项。身体格选敌／接近和命中局部坐标变换已有指定原技能的 Play Mode 回归，尚未完成全部技能与 Windows Player 验证。
- 原人物、NPC、幻兽、召唤物的完整模型、动作、骨骼、挂点、粒子、音频和材质表现尚未全部接通。
- `GameRoundUI` 原管理器及两种完整卡片已接入；竞技场守卫临时头像已接线；军团战／多人争霸玩法宿主、拟态目标和怪物／幻兽全部图标类型的正式实体投影仍未全部接通。等待实体装载、连续死亡／复活与阶段门控仍须覆盖真实战斗流程，ETC2 设备后端尚未运行验证。
- 原技能图声音已接到正式公共控制器；自动主题选择、语言切换、Windows 暂停／焦点实际切换、完整时长声音／混音及全部技能／场景的 Windows Player 验证仍待完成。短帧及播放编号检查不代表全部效果器输出已验证。
- 命中实体视图及本地加载绑定已迁移，2,260 个原 prefab 的加载和六类代表命中图已通过隔离 Play Mode；正式固定命中、召唤和已有触发技能命中已按执行回执创建／移除原命中实体。随机／自定义目标包装和连锁的完整实体调度、召唤角色模型及入场时序仍未接通；全部原图、施法起点连线、逐范围格时序／清理及特殊状态替换尚未完整覆盖，不能按代表图验证推断全部演出完成。
- 正式技能宿主已改为加载完整原技能实体 prefab，保留原默认／四向图、角色组件和序列化引用，复用原加载、End／Remove／FIFO 池与选图流程；代表图、状态快照及对象池复用已通过隔离 Play Mode 与 Windows Player，全部原技能和完整人物演出尚未覆盖。
- 57784 命中图的原骨骼适配、受控实体创建及复用已有专项验证，但所选配置中的 57780 只引用 57781 召唤命中；全部已安装战斗实体中未找到指向 57784 的父引用。保留该未引用内容及其原休眠层级，不自行接入技能；此项验证不表示 57780 整套战斗演出已经还原。
- 正式入口中的准备输入、战斗过程、胜负返回、存档恢复、模拟鼠标交互和 Windows Player 尚未完成整体验收。
- 活动挑战、私有副本和裂隙的退出事务尚未完整接通；已接线玩法仍须分别完成奖励页面及整体验证，公共按钮可用不代表所有玩法完成。

## 当前验证范围

- `OfficialBattleAnimatorPlaybackAcceptance.RunActorMaterials` 已在内置屏静音 Play Mode 通过全部 67 个实际原生角色，编辑器以 0 正常退出。逐一检查原外观组件、36 个空 renderer 引用与 31 个非空引用、原材质收集顺序、每角色独立材质、66 个原 Low 贴图绑定与 3803 的原 None／空 loader 分支、原受伤变色节点、双实例重叠与最后归还、同类角色隔离。3803 另检查三个独立皮肤材质、八个原材质及四个网格复制引用的原顺序、启用状态和运行时替换映射；混合 shader 保留原属性支持差异。270 张实际运行画面已查看：66 个角色通过相机画面逐像素恢复，3803 相机保留原动态星光，另以固定 shader 时钟的两张实际 GPU 绘制检查逐像素恢复。主工程 Unity 导入／编译及 `ProjectTools.Validate` 以 0 正常退出，静态元数据检查通过（151,868 GUID），182 项输入与 793 项输出哈希一致。检查使用生产角色创建／原生绘制和原伤害图的完整材质叶节点；冻结姿态以隔离材质效果，未改原节点参数、材质数值或原 shader，不代表完整伤害图、全部幻兽动作／支援流程或 Windows Player 已验证。本批尚未重建或发布 Windows Player。

- 公共入口共享验证覆盖人物／怪物移动、技能耗血、原技能吸血、状态通知、真实公式暴击与格挡，并增加 3803 特殊外观与真实训练目标 3204 流程。模拟原开始按钮后，检查原 `Damage`／`block_01` 状态和实际可见骨骼网格；人物按原 separator 分层取网格，保留原 .4 秒受伤图的观察窗口。主工程导入／编译、`ProjectTools.Validate` 与内置屏静音 Play Mode 通过前七组，编辑器以 0 退出，51 张画面已查看。已提交源码的隔离工程修复战斗程序集重复引用 `Zjcs.Official.StoryFollowBone` 后，Unity 编译、`ProjectTools.Validate` 与 Windows 构建均以 0 完成；内置屏静音 Direct3D 11 Player 通过全部十一组，80 张实际画面已查看；新增原同伴 11005 受伤／材质及原场景音乐与实际技能音效检查。原 10630 三段攻击与 16020 单段攻击各只结算一次，四格欧氏跳跃、暂停恢复、落点、原飘字、技能槽、次数／冷却及原生吸血实体继续通过。3803 在正式公共控制器下检查原 None／空 loader、三个独立皮肤材质、四个复制引用及原三个组件启用字段；对象是否激活仍由原层级／动画决定，验证不强制激活副本。静态检查通过（151,903 GUID）。这些受控黑底流程覆盖实际角色、HUD 与受击接线，3803 保留原尺寸且超出夹具相机边界；67 个完整材质专项仍以前项 Editor 验证为准，不据此宣称完整场景、全部角色／技能或 Windows 全部材质组合已覆盖。

- 基础单次命中吸血的 15 组真实执行器专项通过：原 10110→10111→10028、三段逐命中、多目标、实际扣血与溢出／护盾排除、向零截断、超过 100% 的比例、零／负属性、满血回执、治疗增益、禁疗及满血／非满血转盾；每次治疗独占施法与伤害编号，保留导致它的命中时刻和自身格，不额外消耗普通技能次数。统计只计实际恢复，检查点保存／恢复与后续步骤一致；生命增加与 25067 生命区间回归确认原 10110 的白名单过滤，不再用父攻击技能补发一次监听；既有 24315 在此次非白名单治疗中保留。既有独立命中吸收回归及 460 项基础战斗检查通过。主工程编译、`ProjectTools.Validate` 与内置屏静音公共交互五组 Play Mode 通过、编辑器以 0 退出，33 张实际截图已查看；吸血的三组原技能、命中、伤害实体全部创建，绿色原治疗数字及 `playable_cure_lifesteal` 在原人物自身位置播放，整个场景没有旧兼容飘字重复。静态检查通过（150,558 GUID）。夹具显式提供原治疗基础属性，严格要求三次正数治疗。历史无实体回执和玩法宿主的测试观察已适配，后者的全部玩法入口未在本项重跑。本项未重建 Windows Player，正式发布目录未更新；五组共享步骤的旧 Player 覆盖及完整公共战斗缺项见下文。

- 状态免疫通过 `test_battle_status_immunity.ps1` 的 23 组真实执行检查：原 24141／25243／60640／24671、正数耗尽移除或保留、零／负数无限、5001 替换、指定 ID 静默拒绝、类型／来源／可驱散受控条件组合、概率失败、禁止状态、续期、深复制、旧检查点补定义、正数计数恢复，以及开场真实施加被拒绝后待发提示的存档恢复。实际成功标志、增益／减益统计和战报原权重评分一致，拒绝不计入“施加状态”；独立暂存内容同组通过。460 条核心、16 组纯状态／混合效果及 13 组状态结果回归通过。最新主工程导入／编译、`ProjectTools.Validate` 和内置屏静音公共 Play Mode 的五组流程全部通过，编辑器以 0 正常退出；33 张实际人物、原 HUD 和特效画面已查看。状态场景三次实际命中均被 60640 拒绝减益，对应三次原“状态免疫”、三次“霸体”和一次自身减冷却通知；接近与命中前不提前显示，原 TMP 文字实际生成网格，原驱散仍只发送一次，旧步骤飘字为零。静态元数据检查通过，162 项原生资产及 34 项原脚本哈希一致。场景使用受控黑底，不代表全部战场、人物受伤／材质或音频验证；开场无直接命中关联提示仍沿步骤边界，未宣称逐次开场演出时刻完整恢复。本批未重建或发布 Windows Player，前项公共 Player 不包含新增免疫接线。

- 成功驱散接线通过主工程导入／编译、`ProjectTools.Validate` 与内置屏静音公共 Play Mode：原 HUD 模拟点击启动的五组流程全部通过，编辑器以 0 正常退出。状态动作场景在三次原命中中的首次实际移除 24436，只派发一次原驱散通知，接近和技能命中前不提前显示，后两次无状态可驱散时不重发，旧步骤飘字始终为零；33 张实际人物、HUD 和特效画面已查看。真实回执专项扩展为 22 组，包含伤害／纯状态驱散、概率失败、无可驱散状态、同目标连续两次操作、保存恢复和旧关联缺失；同刻先原受伤通知再驱散、各操作精确归属、隐藏／暂停／倍速、归池和释放断言通过，22 张受控蓝底时序画面已查看。该专项编辑器停在原生退出阶段，本会话停止其进程，不计为正常退出。16 组纯状态／混合效果与图调度回归通过；独立暂存的 135 份战斗核心和图调度同样通过。静态检查通过（150,485 GUID）；162 项原生资产和 34 项官方脚本哈希保持一致。本次只接通成功驱散提示，未改状态移除、概率、随机选择、统计或自然到期规则；未将状态结算、全部派生结果或免疫次数规则列为完成。本次未重建或发布 Windows Player，前项公共 Player 不包含新增驱散接线。

- 公共交互专项由 `OfficialBattlePublicFlowValidation.Steps` 统一驱动 Editor 与 Windows Mono Player。完整资源构建及后续脚本增量构建成功，两个构建编辑器均以 0 退出；实际 Player 的五组流程全部通过并以 0 退出。同一共用步骤再经主工程 `ProjectTools.Validate` 与内置屏静音 Play Mode，五组流程通过、编辑器以 0 正常退出，33 张 Editor 画面已查看。模拟原开始按钮后核对人物 15300／10630 与怪物 19810／16020 的四格欧氏距离一段跳跃、暂停／恢复、落地后一次技能、三／一次命中、原 HUD 技能槽、最终站位与外观档案不变；另检查七点受控耗血只结算一次、三次吸血派生治疗保留旧兼容飘字（此 Windows 构建尚未含下方原 10110 治疗链）、三次命中各派发一次原霸体提示及首次命中给自身施加 60481 的一次原减冷却提示。原生 UI 接管的直接伤害、耗血及状态动作没有旧步骤飘字重复。已查看 Player 的 33 张实际画面，包括原生模型、原技能／命中特效、红色耗血、伤害数字、绿色派生治疗和原提示文字／堆叠。静态元数据检查通过（150,428 GUID）；162 项原生资源及 34 项官方脚本输出哈希在运行前后吻合。此构建使用共享工作区和隔离参数入口，内存档案不打开用户存档，未新增或改写存档 JSON；不是与已提交内容一致的正式发布包。场景仍为受控黑底战场，不覆盖完整地图／玩法入口、全部技能、资源、受伤材质及声音输出；保留既有缺失脚本、资源配置、API Updater 与帧呈现诊断。正式发布目录未更新，公共战斗仍未完成。

- 状态自定义动作的逐命中时序通过主工程编译、`ProjectTools.Validate`、内置屏静音 Play Mode 与静态检查（150,422 GUID）。十六组真实回执覆盖霸体、命中给目标／自身施加减冷却、纯状态／治疗命中、保存恢复关联、同刻先原命中通知后动作、旧回执边界、实际角色、原图标、精确归属、暂停／隐藏／倍速及释放，十六张实际画面已查看。正式公共控制器五组流程全部通过：模拟原 HUD 点击、接近与暂停后，三次受伤分别在命中时派发原霸体提示，首个命中给自身施加 60481 后派发一次原减冷却提示；步骤开始没有提前提示，全场没有旧步骤提示重复，三十三张实际运行画面已查看。两项最终编辑器均以 0 正常退出。减冷却规则专项、十六组纯状态／混合效果回归、共享与独立暂存图调度检查通过；独立暂存的 135 份战斗核心也通过减冷却专项，162 项原生资产及 34 项官方脚本输出哈希不变。减冷却专项按明确来源技能身份与初始状态步骤驱动；25275 的原概率为 0.5，夹具单独设为 1，旧回执边界专项预置一条待发送记录。60481 来自原配置，夹具将其加入受控命中状态列表，不修改正式配置；未据此声称全部冷却触发或被动动作时刻完成。正式控制器五组流程的 Windows 验证见公共交互专项；十六组受控回执专项未在 Player 重跑。

- 历史无实体派生回执兼容专项使用受控回执模拟与原命中共享编号的治疗，确认原配置伤害实体不会被派生项覆盖或重复创建，反转回执顺序仍保留原伤害；十组伤害回执断言通过，八张实际画面已查看，但该次编辑器停在退出阶段，已停止本次进程，不记为正常退出。正式公共控制器的主角、怪物、耗血和吸血四组通过内置屏静音 Play Mode，编辑器以 0 正常退出；吸血夹具为原 10630 的命中图节点补入受控吸血属性，不改正式配置，三次直接命中与三次派生治疗各结算一次。旧飘字检查扩大到整个测试场景，确认直接伤害均由原管理器播放，仅派生治疗保留兼容路径；二十六张实际运行画面已查看。生命恢复及吸血规则回归、共享与独立待提交图调度回归、`ProjectTools.Validate` 和静态检查（150,422 GUID）通过，162 项原生资产与 34 项飘字脚本输出哈希保持一致。此项修复播放索引冲突，未迁移派生治疗的统一原生通知，正式控制器的 Windows 验证见公共交互专项；十组受控伤害回执专项未在 Player 重跑。当前基础吸血已替换为原 10110 治疗链，此处历史兼容验证不能替代该链的运行验证。

- 耗血通知接线已通过主工程编译及内置屏静音 Play Mode：九组真实步骤回执包含连续三次同类重放的独立耗血，核对一次扣血、保存／恢复、技能开始前出现红色数字、旧回执未知标志不反推及完整释放，编辑器以 0 退出。正式公共控制器增加带七点耗血的主角流程，并回归原主角／怪物两组；模拟点击开始、跳跃、暂停、技能、三次命中、扣血不重复和新旧飘字不双播通过，编辑器以 0 退出，已查看新增耗血流程七张及两张回执兼容截图。七点耗血仅为受控夹具，不改原技能配置。`test_battle_hp_cost.ps1` 继续通过无命中技能、原 25067、阈值子技能／状态、白名单、零耗血与确定性恢复，并新增实际施法耗血快照往返检查；460 条公式／战斗核心与图调度回归、`ProjectTools.Validate` 和静态检查（150,422 GUID）通过。运行后原生资源及脚本清单哈希一致；正式控制器耗血流程的 Windows 验证见公共交互专项；九组受控回执专项未在 Player 重跑。

- 正式公共宿主的原生伤害飘字与实际技能重放接线已在主工程执行内置屏静音 Play Mode：七组真实执行器回执通过创建时刻、原持续时间、角色双向通知、纯状态／满血治疗分类、真实重放身份、旧回执兼容及释放检查；隐藏后借出对象身份与计时保留，暂停不归还，恢复四倍速后按原时长归池。主角与怪物两组正式控制器流程通过模拟点击开始、四格跳跃、暂停、技能／命中及结算检查，完整原管理器生成实际角色飘字，旧飘字数量始终为零；已查看两组共十二张实际运行画面及五张伤害回执画面。两项最终 Play Mode 均正常退出，正式控制器末尾再次通过 `ProjectTools.Validate`；静态元数据检查通过（150,422 GUID），图调度检查在共享工作区及本次独立待提交源码上均通过。中文字体在激活前绑定战斗独立副本，运行结束后 162 项原生资源与清单 SHA-256 全部一致。正式控制器主角／怪物流程的 Windows 验证见公共交互专项；实际技能重放及七组受控回执专项未在 Player 重跑。状态结算和全部派生伤害通知仍未接管；已关联命中的驱散接线与验证见本节专项。正式发布目录未更新。

- 完整原生飘字及管理器已在 Unity 2022.3.57f1c2 隔离工程执行内置屏静音 Play Mode：13 个原 prefab 全部加载，原 bitmap 数字和六种中文提示生成实际网格，`1234567 → 123万`、元素免疫、破灭伤害、破韧优先、暂停、FIFO 复用和单次完成通过；原阶段 101 预热 198 个、无战斗／未配置阶段预热 112 个通过。通过实际角色受伤事件及共享通知投影，核对九种样式的互斥优先级、格挡与暴击同时存在时的样式／颜色差异、线性色与 TextMesh 的 Color32 存储、64 位数值、护盾合并、原零值过滤、状态治疗和自损颜色、驱散／状态动作、普通／Extra／隐藏／异步重放、堆叠、角色移除与重新加入、管理器启禁用、回池和非池销毁；原数字格式 `5000000000 → 500000万` 保留，不自行改成常用单位。已查看完整 13 种效果及管理器九种样式的实际截图。完整夹具断言和截图检查通过，但编辑器仍停在原生退出阶段，已停止本会话验证进程，不记为 Play Mode 进程正常退出。主工程编译和 `ProjectTools.Validate` 以 0 正常退出；静态元数据检查通过（150,419 GUID）。该夹具通过角色通知输入管理器；正式步骤投影的验证范围见上项，Windows Player 仍未覆盖。

- 完整原图声音的来源检查通过：1,735 个原 Event 资产引用、1,734 个不同事件及其 1,766 项有序动作，传递核对 2,913 个对象、2,977 条引用、2,942 份原 HIRC 载荷、4 个原全局设置块、1,562 份完整媒体及 2 份匹配的流式预取片段；7 个引用 RTPC 均在 8 项原全局默认值中解析。原 HIRC 的长度、类型、身份与 SHA256，媒体完整／预取身份及全部包的输入哈希已检查；原插件编号与 HIRC 编号分别处理，Actor-Mixer 的兄弟列表不启动无关声音，各包重复原载荷仍分别保留。静态元数据检查通过（144,784 个 GUID）。来源工具不改 Unity 场景或生产声音播放，未据此运行声音专项或发布 Player；正式战斗宿主接入与完整实际输出仍是缺项。来源见[声音事件](../../assets/reference/battle-animation/native-sound-sources-source-manifest.json)及[传递依赖](../../assets/reference/battle-animation/native-sound-dependencies-source-manifest.json)。

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

- 原生战场状态条和说明窗口已通过 Unity 导入编译、`Zjcs.Editor.ProjectTools.Validate`、静态元数据检查，以及内置屏 Play Mode 和实际 Windows Mono Player 验证。永久入口 `Zjcs.Editor.BattleStageStatus.OfficialBattleStageStatusAcceptance.Run` 在受控公共 HUD 上检查原滑入／淡入曲线绑定后的起点、中段与终点，14 项状态的原布局及宽度、重复状态去重、缩减至 3 项、清空后的图标／背景移除、禁用后重新启用及未变化数据的行对象保留；模拟指针从原状态条打开说明窗口，检查原滚动行复用与滚轮、准备说明、默认倍率 1 的原无衰减说明、动态说明更新、原显示／隐藏动画、模糊遮罩关闭和重新打开。已查看编辑器及 Player 的动画中段、状态条和窗口实际画面。完整资源构建与后续代码增量构建通过，验证前后用户存档文件哈希一致。该验证未经过完整战斗控制器或正式页面入口，未驱动完整准备倒计时及场景数据，也未覆盖其他战斗 HUD、声音实际输出或各模式整合；构建仍有既有缺失脚本／URP shader 诊断，Player 启动仍有既有帧呈现诊断。本模块尚未更新正式发布目录，不能作为整个战斗功能完成结论。

- 原生行动进度场景、完整原管理器、两类原卡片、配置及动态图集通过 Unity 导入编译、`Zjcs.Editor.ProjectTools.Validate`、静态元数据检查（145,770 个 GUID）、内置屏 Play Mode 和 Windows Mono Player 专项。永久入口 `Zjcs.Editor.BattleRound.OfficialBattleRoundSceneAcceptance.Run` 使用公共 HUD 的原渲染管线；主工程完整检查已覆盖原管理器、普通卡片与 UV 修复，新增时间投影、阶段判断和未入队实体断言再经隔离工程的同一 `RunIsolated` 夹具复核，隔离版只复制原 Game View 配置方法，不代替主工程全部资源检查。核对四个场景节点、线条与箭头 sprite／材质／shader、原排序、完整场景卡片的七个脚本与 0.5 秒死亡回调、RoundUI 的 17 个对象／4 个脚本引用、原预创建 16 个头像，以及普通卡片的 37 个脚本和原网格顶点／UV／索引。受控配置分别运行 UGUI 图集与普通头像分支，核对原取景 100×120、完整 UV、当前行动者与自身效果；普通分支的原 Slot 仅由夹具临时激活，生产 prefab 保持原值。一名原生模型取景玩家与两名原 NPC 头像检查初次排序、行动切换、检查点恢复、受伤通知触发原动画、死亡移除、相机／画布及主面板动画锚点、隐藏／重现、空队列、场景／纹理销毁；模拟指针检查原速度按钮未被场景遮挡。保留 `SpriteRendererCanvas` 原空透明度数组。Windows Player 额外通过当前行动者已更新至未来 Next 的冷恢复、切换后恢复上一行动者的 Next、不改检查点、未入队实体不申请头像和准备／战斗阶段门控投影的检查。425 项来源及输出哈希通过核对。编辑器与 Player 各六张实际画面均已查看，字体、原头像、箭头、遮挡与动画位置正常，退出码为 0。验证使用隔离工程、公司／产品目录及专用启动条件，不调用用户存档持久化。构建保留既有缺失脚本／程序集和 URP shader 诊断，Player 保留既有帧呈现诊断；隔离工程遗漏的 TMP shader include 已补齐，并检查实际字体 shader 支持。受控 HUD 未经过完整战斗控制器或正式玩法入口，回合格式尚未喂入数据，也未包含完整战场；不覆盖全部怪物／幻兽类型、守卫／拟态、连续死亡／复活、等待实体、真实阶段门控和声音输出。未更新正式发布目录，不能作为完整行动进度 HUD 或整个战斗功能完成结论。
- 原头像请求层、选择器和场景图集由同一入口在编辑器与实际 Windows Player 运行通过。`OfficialBattlePortraitRequestAcceptance` 复用完整 prefab 中的原组件和请求参数，使用原生男女模型的实际 100×120 取景，经原请求、缓存订阅、场景处理器和图集通知验证逐像素一致性、原玩家／临时请求键、持久化标志、取消令牌、禁用取消、恢复复用、替换注销与销毁解除通知。Windows 实际选择官方回退图集，检查原 UV／100 PPU、缓存复用、单项移除、公共时间轴恢复时复用纹理及场景销毁所有权；调用方持有的原取景纹理仍存活。受控配置另验证普通／UGUI 选择、五种委托行为、阵营底图加载顺序和重复键、缺失纹理、颜色复位、清空销毁与禁用开关。UGUI 两种分支已有实际显示验证；ETC2 设备后端仍未运行覆盖。

- `test_battle_hit_links.ps1` 通过三类子引用、重复 ID、显式零／false、无伤害包装根、随机父锚点与分组、旧 ID 图和共享定义不变检查，并直接比较原 10911 的七条引用。实际执行器夹具检查七次伤害事件的 0.37～0.97 秒、一次释放、检查点时序／HP／计数及恢复不重放；现有随机选敌（含原 13660／13990）、命中范围、距离延迟和步骤时序回归通过。隔离 Unity 2022.3.57f1c2 完整战斗规则代码导入编译及同一七次引用检查通过。全工程静态检查因工作区既有未提交 `OfficialBattleGraphCapabilityValidation.FirstPromotion.cs.meta` 的 31 位 GUID 失败；主工程正被其他验证使用，本次未执行全工程 `ProjectTools.Validate`、Play Mode 画面或 Windows Player，也未更新正式发布目录。这是命中执行的局部迁移，公共战斗仍未完成。

- `test_battle_hit_selection_snapshots.ps1` 通过生产执行器七次引用的独立身份／随机选格／时序、后续位置变化隔离、多身体格与逐格延迟、检查点保存恢复及缺少新字段的旧存档兼容检查；原 13660 按原十字范围与三组实际身体落点产生 11／11／13 次伤害，共 35 次；原 13990 与随机组排除规则回归通过。隔离 Unity 2022.3.57f1c2 完整规则代码编译及 `JsonUtility` 的新／旧施法快照恢复通过。尚未驱动 Play Mode 画面、原命中 prefab 或 Windows Player；全工程静态元数据检查通过（145,778 GUID），主工程仍被其他验证使用，未执行全工程 ProjectTools.Validate，本模块未更新正式发布目录。

- 四格普通小跳与按技能效果选位已通过 `test_battle_jump_position.ps1`：圆形边界／斜向落点、越过中间阻挡但拒绝非法落点、完整身体占格、优先标记／大怪身体格／群怪分支、不可达优先目标的接近、治疗／增益／自身防御、嘲讽期间允许的友方技能、单目标暂定边界及生命百分比；真实执行器核对原 15300 一段小跳、落地后一次施法、方向与检查点。暂存版本的 460 条公式／战斗核心及范围选敌回归通过，无可释放技能时不移动。静态元数据检查通过（145,931 GUID）。隔离 Unity 2022.3.57f1c2 编译及内置屏 Play Mode 两场公共控制器回归通过：模拟原开始按钮，玩家 15300／10630 和怪物 19810／16020 的斜向小跳、暂停恢复、原技能实体仅在落地后播放一次、3／1 次扣血、技能槽状态、CD／次数、最终朝向和外观档案不变；八张实际截图已查看。验证使用受控黑底战场，已查看回合／按钮文字、原行动头像、小跳中段及技能／伤害画面；仍有既有缺失脚本及地面表现配置警告，不代表完整 HUD／场景已还原。主工程被其他验证占用，未执行本次全工程 `ProjectTools.Validate`；本次修改尚未构建或发布 Windows Player，公共战斗继续标记未完成。

- 基础命中飞行时序通过 `test_battle_hit_delay.ps1` 的共同范围中心、逐格延迟、同一大怪的独立随机身体格、源偏移／Z／翻转／旋转中心和检查点关联往返检查；随机选敌、逐格命中、范围、施法快照及步骤时序回归通过。只将本批核心修改叠加到已提交基线后，460 条固定公式向量及战斗核心、旧命中轨迹恢复检查仍通过。Unity 中直接对照原 `FightHitViewFormula` 的 256 组向量通过，全工程编译、`ProjectTools.Validate` 和静态元数据检查通过（145,959 GUID）。隔离内置屏 Play Mode 连续通过人物 15300／10630 与怪物 19810／16020 两场公共控制器检查：原 HUD 模拟开始、小跳与暂停恢复、落地后一次原技能实体、3／1 次扣血、技能槽、CD／次数、检查点及外观档案保持；八张实际画面已查看，退出码为 0。夹具使用受控黑底战场及原生模型／头像，保留既有缺失脚本和地面表现配置警告；本项未接通正式命中实体演出、状态延迟覆盖或连锁逐跳调度，未构建或发布本批 Windows Player，公共战斗仍未完成。

- 状态层数延迟覆盖通过 `test_battle_hit_status_delay.ps1`：三套原技能的十档偏移、共享项优先、零值不回退、首个个体状态、越界保留基础参数、来源代理缺席、共享定义不变、正式伤害与回执时序一致及检查点往返。只将本批核心修改叠加到已提交基线后，同组专项和 460 条固定公式／战斗核心仍通过。全工程 Unity 编译、`ProjectTools.Validate` 与静态元数据检查通过（146,029 GUID）。隔离内置屏静音 Play Mode 的 `OfficialBattleSkillPrefabPlaybackAcceptance.Run` 实际创建三套技能的 30 个原命中 prefab，对照原 `MyFightHitBase.DelayInfoProvider`、覆盖下标和 `CalcSkillHitDelay`，并检查状态移除后的施法快照保持；13200 的原状态选图／FIFO 复用及 54383 的原管理视图回归通过。五张原生粒子／技能运行截图已查看，场景为受控纯色背景、未加载角色或 HUD。两次运行均写出通过结果并完成视图释放，编辑器在关闭阶段停滞；改用单线程图形模式后仍复现，已停止本次进程，不记为正常退出通过。该验证直接使用原技能／命中加载入口，未覆盖正式战斗的命中调度、完整场景交互、Windows Player 或正式发布。

- 固定命中的正式回执路径已通过主工程 Unity 2022.3.57f1c2 编译、`ProjectTools.Validate` 和 Play Mode，编辑器正常以 0 退出。`OfficialBattleSkillPrefabPlaybackAcceptance.Run` 保持三套技能的 30 组原状态覆盖对照，另核对延迟创建、恰逢技能到期时的实际身份关联、移除来源状态／移动最终目标后的快照保持、多个中心独立身份、旧回执的原飞行参数回退，以及原 `DestroyDelay` 后移除。公共控制器两场窗口化回归经模拟原开始按钮运行：15300／10630 的人物小跳及三个固定命中、19810／16020 的怪物小跳及一个固定命中，逐个核对原实体身份、所属施法和回执格；暂停恢复、伤害不重复结算、技能槽、CD／次数和外观档案保持通过。十张公共战斗画面和六张原生效果夹具画面已查看，包含实际原生命中特效；公共夹具为受控黑底战场，保留既有缺失脚本、地面配置及编辑器 API Updater 程序集解析诊断，不代表完整 HUD／地图场景已还原。静态元数据检查通过（146,031 GUID），状态延迟专项回归通过。本批未构建或发布 Windows Player，其余命中类型与完整公共战斗仍未完成。

- 触发技能命中的正式回执路径已通过主工程 Unity 编译、`ProjectTools.Validate` 和内置屏静音 Play Mode。`OfficialBattleHitPlaybackAcceptance.Run` 从真实执行器读取原 `13520 → 13521 → 13522` 的八条回执，检查空格也创建原命中实体、0.8 至 0.87 秒各自创建、八个不同随机格及实体身份、实际所属技能、原技能表现目标连线、目标后续移动不改写格位、原销毁延迟后释放，以及清空新时序字段的旧回执恢复；播放前后检查点字节一致。既有六类原命中加载、范围时序、暂停、通知订阅与回池清理回归通过，七张实际运行画面已查看，包含正式八次触发的原特效。静态元数据检查通过（146,031 GUID）；场景为受控纯色效果夹具，仍有既有编辑器 API Updater 程序集解析诊断。本项只接通已有触发技能回执的原实体生命周期，不表示随机父包装、全部触发规则、正式 HUD 交互或完整战斗流程完成，未构建或发布本批 Windows Player。

- 召唤命中的回执生命周期及怪物／幻兽／替身落点通过 `test_battle_summon_placement.ps1`、既有 HitSummon／继承／替身及 HP 触发恢复专项、460 条公式／战斗核心回归；四向位置错误先在专项中复现，再修复。替身的伤害传递夹具改为瞄准实际召唤格，不再固定使用旧的上方两格。主工程 Unity 编译、`ProjectTools.Validate`、静态元数据检查（146,031 GUID）和内置屏静音 Play Mode 通过，验证与最终运行均以 0 退出。原 `12140／12200／57780` 的实际召唤位置及顺序逐项对照已创建的原 `MyFightHitSummon` 的 `GridTransform.LocalToWorldPos`，包括旧回执、实际技能身份与半格中心；三份原命中 prefab 的播放字段均为空，保留原绑定，画面中的其他效果来自所属技能。`11540／11541` 检查九格 BaseScopes、实际生成地块均在原范围内、原陨石图播放事件、范围延迟后销毁及播放不重复创建／改写检查点。十一张受控效果夹具运行画面已查看，包含陨石落地阶段，仍有既有编辑器 API Updater 程序集解析诊断；未加载召唤角色模型、HUD 或完整地图，不代表全部召唤入场、地块落点及额外偏移完成。本批未构建或发布 Windows Player。

- 随机父命中回执通过 `test_battle_random_parent_receipts.ps1`：10910／10920／13520 各含普通及重复根引用共六组，对照加入父回执前的执行，检查根引用独立身份、锚点和延迟、零额外随机抽取／伤害／AI 计数、检查点往返；仅移除新增父回执并重排序号后，完整检查点逐字节一致。既有命中选择专项、460 条公式／战斗核心回归、主工程 Unity 编译、`ProjectTools.Validate`、静态元数据检查（146,068 GUID）及内置屏静音 Play Mode 通过，编辑器正常以 0 退出。十一张实际运行画面已查看，覆盖既有六类受控原图与正式触发技能／召唤回执；父包装的正式实体生命周期仍未接线，截图中的随机父图来自既有受控夹具，不代表该新增回执的正式播放验证。本批未构建或发布 Windows Player。

- 原伤害实体视图通过主工程 Unity 导入／编译、`ProjectTools.Validate`、静态元数据检查（149,956 GUID）及内置屏静音 Play Mode，编辑器正常以 0 退出。`OfficialBattleDamagePlaybackAcceptance.Run` 逐一加载 1,368 个原 prefab，核对启用状态、隐藏／零伤害标志及 4,213 个原图指针；10004、13012、11432、10152 分别覆盖完整结果图、仅状态触发图、仅公共图及空图，共 16 组格挡／暴击组合，检查格挡先于暴击、原目标／来源及 Damage 参数、目标通知先于技能查询、缺少目标／技能／来源、重复身份拒绝、同类实例回池复用，以及 13202 原覆盖驱动保持禁用。完整结果专项覆盖全部 128 组伤害标志、超过双精度整数精确范围的有符号 64 位结果、负护盾值、治疗／无结果分类和未知取值枚举；原目标与来源通知收到同一完整结果。角色通知由原 `MyFightRoleFight` 发出，核对实际发送者、伤害参数及无监听器分支；两种受控监听器异常均由原捕获边界隔离，跳过同次后续监听器并继续播放伤害图，仅消费这两条预期异常日志。同一入口在仅包含 65 个运行时程序集与 6,322 个原生输入的隔离工程通过全部断言，但原生退出停滞，限制工作线程数亦未解决，已停止本次进程，隔离运行不列为完整通过。四张主工程及四张隔离 13012 实际粒子画面已查看；既有命中回归的十一张画面、原六类图及正式固定／触发技能／召唤回执回归结论沿用。角色为无可见网格的受控对象，材质接口提供空集合；未验证真实人物受伤动作、材质变色或正式人物／HUD 的全部监听器。保留既有 API Updater 程序集解析诊断。此专项验证原视图与资产目录，正式步骤回执验证见下项；未构建或发布本批 Windows Player，公共战斗仍未完成。

- 施法伤害归属通过 `test_battle_damage_cast_receipts.ps1`：10910／10920／13520 原随机命中、同来源同技能三次连续重放、父子伤害序号交错及零伤害共六组；检查伤害只属于一次实际施法、子列表独立、序号不要求连续、检查点往返及缺字段不推断归属。分别编译修改前的真实执行器／状态与当前代码，仅去掉新增列表后，六份完整检查点逐字节一致。460 条公式／战斗核心、技能重放、子技能、检查点及随机父回执回归通过；主工程 Unity 编译、`ProjectTools.Validate`、静态元数据检查（149,910 GUID）及内置屏静音命中 Play Mode 通过，两个编辑器进程均以 0 退出。十一张受控原图与正式固定／触发技能／召唤回执画面已查看，保留既有 API Updater 程序集解析诊断。归属列表现由下述正式播放宿主消费；该规则专项不代表全部原伤害演出覆盖，未构建或发布本批 Windows Player。

- 状态特效结果回执通过 `test_battle_damage_status_result.ps1`：目标／自身施加、概率与 HP 条件失败、生命周期拒绝、前后项成功／失败、不同拥有者、随机择一、成功续期及仅有旧状态共 13 组。分别编译修改前真实执行器／伤害回执与当前代码，仅去掉新增两字段后，13 份完整检查点逐字节一致；新回执往返保留结果，旧回执不猜测成功。460 条公式／战斗核心、状态施加、命中延迟及状态层数延迟回归通过。主工程 Unity 编译、`ProjectTools.Validate`、静态元数据检查（149,956 GUID）及内置屏静音 Play Mode 通过，编辑器均以 0 退出。专项使用受控技能配置生成真实执行回执，再驱动未修改的原 13012 状态图，覆盖空列表、目标成功、自身成功和概率失败；六张实际画面已查看，其中两张为回执驱动状态特效，完整结果和原角色通知专项继续通过。该受控配置不代表 13012 原技能拥有同一 StatusList；正式接线另由步骤回执专项验证。未构建或发布本批 Windows Player。

- 正式伤害回执专项 `OfficialBattleDamagePlaybackAcceptance.RunReceipts` 通过主工程编译及内置屏静音 Play Mode，编辑器以 0 退出。检查步骤级非零起始偏移、配置创建时刻前后、原持续时间回池、同类技能三次真实重放的独立技能身份、目标后来位移时仍保持原点位置、10910 的实际身体格与连带点命中、原角色双向通知及完整结果、实际满血治疗仍为 `Cure`、真实纯状态命中为 `None`、旧回执缺位置时跳过、最终释放。`test_battle_damage_pose_receipts.ps1` 覆盖点命中、受击位移、10910／10920、零伤害与满血／有效治疗七组；分别编译修改前真实执行器／回执与当前代码，只剔除新增位置证据和已有治疗原始值的补充记录后，完整检查点逐字节一致，往返及旧字段未知检查通过。460 条公式／战斗核心、状态结果、施法归属、受击位移、身体格命中及治疗战报回归通过。原目录专项继续通过 1,368 个原 prefab、4,213 个图指针、128 组标志及原通知／回池断言；十张实际粒子画面已查看，其中四张由正式 Play 消费真实步骤回执，覆盖伤害、重放、纯状态和满血治疗。伤害回执专项的角色使用无可见网格的受控对象，真实模型另外由下述公共入口回归检查；完整受伤动作、飘字和全部 HUD 监听尚未验证。原生输入与绑定不变，只刷新本地播放代码凭据；未构建或发布本批 Windows Player。

- 材质绑定接入后，主工程公共控制器的玩家 10630 与怪物 16020 两场内置屏静音 Play Mode 回归以 0 退出：模拟原开始按钮、15300／19810 一段小跳、暂停恢复、施法、3／1 次实际受击、CD／次数、技能槽与最终站位断言通过；十张受控黑底战场画面已查看，原伤害图调用材质节点不再空引用。正式回执专项、原目录专项及主工程 `ProjectTools.Validate` 均正常以 0 退出，静态元数据检查通过（150,056 GUID）。保留既有地面效果／缺失脚本警告及 API Updater 解析诊断，不据此宣称完整场景或全部角色材质后端已恢复；未构建或发布本批 Windows Player。

- 纯状态与混合效果专项 `test_battle_status_only.ps1` 通过 16 组真实执行器检查：自身／目标增益、普通／异常减益、概率失败、续期、驱散、位移、配置子技能、通用命中监听与伤害／治疗专用监听隔离，以及原 11160／12890／11460。单个技能同时产生伤害、实际护盾、增益和减益时，四项分别记录；成功增益与减益均纳入“施加状态”，人物显示仍沿原分类权重与破韧分，原 12890 的纯护盾只计入实际护盾量。检查点完整往返、恢复后不重复统计，以及实际战报构建后的原权重评分通过。460 条公式／战斗核心与原状态结果、施法归属、命中位置、治疗统计、位移回归通过。主工程 Unity 导入／编译、`ProjectTools.Validate` 和内置屏静音 `RunReceipts` 均正常以 0 退出；位置、同类重放、旧回执、身体格、真实纯状态与满血治疗六个场景通过，四张实际粒子截图已查看。此处纯状态回执来自实际执行，不再手动移除公式字段；角色仍为无可见网格的受控对象，不覆盖完整人物／HUD 演出。静态元数据检查通过（150,054 GUID）；原资源输入／输出不变，仅刷新本地绑定哈希。隔离准备入口补入实际物品自动增长规则依赖、原嵌入模型目录及动态绑定，并按真实施法补齐三种技能、四种命中 prefab；隔离工程六个场景全部断言通过，另四张实际粒子截图已查看，但编辑器停在原生退出阶段，本会话停止其进程，不计为隔离运行正常完成。运行正常退出的结论以主工程为准；未构建或发布本批 Windows Player。

- 触发技能原点专项 `test_battle_hit_trigger_origin.ps1` 通过八组真实 13520→13522→13523 执行：四方向、伤害／纯状态两类触发、非中心身体格、方向和翻转、实际父触发时刻、原 13526 伤害、施法者位置及检查点新旧字段兼容。460 条公式／战斗核心、六组施法归属、七组命中位置及十六组状态统计回归通过。主工程编译与 `ProjectTools.Validate`、内置屏静音 `RunReceipts` 均以 0 退出；七个播放场景通过，含真实 13520 的无伤害实体触发、后续原技能／雷击、实际子实体位置、源角色投影及原双向伤害通知，五张实际粒子截图已查看。静态检查通过（150,079 GUID）。原模型仍为无可见网格的受控对象，不覆盖完整人物／HUD；未运行本项隔离 Play Mode，未构建或发布本批 Windows Player。

- 正式技能伤害测试使用真实目标 3204，现已沿现有提取链恢复其完整原生角色：38 根骨骼、32 个插槽、31 个网格附件、十段动画、两个 IK、原 Animator／AnimationController、附件／阴影与 Low 外观加载器。原生角色目录扩至 68 个，根材质绑定扩至 33 项；既有 1,777 项完整模型输出和 792 项外观输出字节保持不变，仅更新对应目录。`RunTrainingMonsterMaterials` 在内置屏静音 Play Mode 通过，编辑器以 0 正常退出；四张实际画面已查看，原伤害材质节点、双实例重叠、同类角色隔离和最终 GPU 像素恢复均通过。`ProjectTools.Validate` 和静态检查通过（151,903 GUID），2,889 对来源／输出哈希一致。公共 Windows 夹具已加入第九组真实 3204；九组 Player 均以 0 退出，65 张实际画面全部查看。正式发布采用已提交源码 `1cddbd164c`，严格全资源构建与随后不带专项开关的正常构建均成功。实际运行正常构建及发布后的 `D:/Desktop/杖剑传说/杖剑传说.exe`，通过原生技能页、模拟按钮、十回合伤害测试、受伤姿态、动态结算、技能／人物两类战报、技能详情与返回设置；两次均以 0 退出，20 张实际画面全部查看。发布前核对 7,018 个资源／运行时文件的逐文件 SHA-256，桌面快捷方式保持正式入口，用户默认存档目录的两个既有文件哈希不变。以上仍不覆盖全部技能／场景与所有材质组合。

- 同伴原外观材质接入在隔离完整工程中通过 Unity 导入／编译、`ProjectTools.Validate`、静态检查（152,106 GUID）与内置屏静音 Play Mode。`RunPartnerMaterials` 使用既有正式 actor 创建入口逐一检查 26 名已开放同伴的原 Low 加载、独立材质、根属性收集器、受伤闪白、重叠归还及另一实例隔离；11005 同时核对第二身体保持原材质，26 组共 104 张实际画面全部查看，GPU 像素最终恢复一致。十组公共控制器交互也以 0 退出，全部 73 张实际画面已查看，包含新增 11005 的八张；原 10630 三次受击沿原伤害图初始化收集器、只改变主身体并在结束后恢复，前九组既有断言及四格跳跃、技能动作、吸血治疗、状态提示、格挡／暴击与训练目标受击画面继续通过。4,122 项来源／输出哈希核对一致，359 项新增资源保留原字段与绑定；原模型、控制器、附件和已有技能骨骼载荷未被替换。当前已提交源码的独立图形进程再次通过 ProjectTools.Validate；完整 Windows 资源构建仍遇到内存峰值，分离图形验证后的资源构建进程占用约 35.9 GB、系统剩余可提交内存低于 1 GB 时由本会话监控停止，未取得成功构建结果。本批 Windows Player 实际运行与正式发布尚未完成，不代表全部角色状态／技能材质或完整公共战斗已恢复。

## 代码与来源入口

- 正式伤害回执播放：[OfficialBattleDamageReceiptPlayback.cs](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleDamageReceiptPlayback.cs)；专项：[OfficialBattleDamageReceiptAcceptance.cs](../../unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleDamageReceiptAcceptance.cs)。

- 命中触发子技能原点回归：[test_battle_hit_trigger_origin.ps1](../../scripts/test_battle_hit_trigger_origin.ps1)；实体位置投影：[OfficialBattleSkillEntityPlayback.cs](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillEntityPlayback.cs)。

- 纯状态命中执行：[FightBattleExecutor.StatusOnly.cs](../../unity/Assets/Game/Runtime/Offline/Combat/FightBattleExecutor.StatusOnly.cs)；规则回归：[test_battle_status_only.ps1](../../scripts/test_battle_status_only.ps1)。

- 原伤害视图与生命周期：[MyFightDamage.cs](../../unity/Assets/Game/Runtime/Official/BattleSkillPlayables/MyFightDamage.cs)、[OfficialBattleDamageEntityPlayback.cs](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleDamageEntityPlayback.cs)；专项入口：[OfficialBattleDamagePlaybackAcceptance.cs](../../unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleDamagePlaybackAcceptance.cs)。

- 官方规则与执行：[`Runtime/Official/Combat`](../../unity/Assets/Game/Runtime/Official/Combat/)。
- 离线适配：[`Runtime/Offline/Combat`](../../unity/Assets/Game/Runtime/Offline/Combat/)。
- 场景与 HUD：[OfficialBattleRuntimeController.cs](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleRuntimeController.cs)、[OfficialBattleHudCatalog.cs](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudCatalog.cs)。
- 官方配置及转换记录：[battle-rules.json](../../assets/official/battle-rules.json)、[skill-rules.json](../../assets/official/skill-rules.json)、[来源总览](../../assets/reference/SOURCES.md)。

