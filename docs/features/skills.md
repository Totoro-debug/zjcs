# 技能、方案与培养

[总索引](../README.md)

状态：未完成

## 功能简介

人物页进入技能，包含战技、秘法、碎片、推荐、详情、七套方案、共享槽位升级、祝福培养、单技能升阶及批量升阶。完整迁移范围还包括主动技能、被动秘法、被动与触发效果、子技能、增益／减益、技能动画和人物动作；各战斗入口的装配和技能页交互在本页维护，共用执行规则、状态生命周期及演出缺项见[战斗执行](battle-execution.md)。当前仅完成下述部分，不能以技能页或局部规则通过代替全系统完成。

人物页的技能入口已连接七套方案面板。技能档案与角色主档案共同保存，离线规则和人物页数据源已接入；技能窗口、推荐、碎片详情、合成确认、培养页及升阶结果均按官方布局数据构造并连接本地事务。推荐页使用 `skill_recommend`、`skill_recommend_item` 与 `skill_tab` 官方布局，按当前职业前置链和阶位显示方案。官方确认弹窗已绑定，纯祝福一键升级在预演达到 40 次时要求确认。技能页、详情、培养和结果弹窗使用 `skill-ui.json` 的官方动画轨道；结果列表逐项展示、跳过和自动滚动，槽位培养播放官方动画和 `Skill_Upgrade.ogg`。技能页现在使用 `character_skills` 的官方整屏 `CharacterPanelSkillPanel` prefab，包括返回条、方案与技能槽、战技／秘法／碎片列表和底部“技能／馈赠”双页；底部第二页按官方 `Achievement` 状态切换。技能列表使用官方 `skill_list_row` 与 `skill_list_separator`，行列数和间距按 `AdaptiveRowLayoutCalculator` 计算（155 px 项目、两侧各 50 px、最小间距 20 px），合成、收藏和未收藏分组各自起行，收藏组与未收藏组之间按官方条件显示虚线分隔；碎片页按官方 160×160 网格尺寸排布。打开技能时播放官方 `CharacterPanel_Skill` 状态：保留人物预览和背景，按原动画隐藏资料区并调整模型位置；返回时播放 `CharacterPanel_SkillSwitchmain` 并恢复被修改的动画属性。40 级伤害测试及通用窗口表现仍未完成，不能据此将整个技能功能标为完成。

## 原始官方实现

官方技能页按职业、前置链、品质、元素、标签、范围、安装槽、附加属性和升级／突破配置刷新；详情与模型预览使用原控件、数值计算及显示规则。

### 官方代码与资源依据

技能图资源由 `prepare_battle_skill_graph_assets.py` 按官方实体根对象和纹理身份生成，导出目录仅作分组，不作为实体 ID；纹理候选先匹配原名，再处理同前缀别名。资源依赖、输入与输出哈希见技能图来源清单。

13100系列分身图已按原脚本标识接回初始化、播放、清理与停止流程。分身动画状态经现有外观接口提供，使用独立原生动画／物理时钟；外观资源注入、位移物理继承及正式演出仍待完成。来源见[分身代码](../../assets/reference/battle-animation/spine-clone-runtime-source-manifest.json)和[配置](../../assets/reference/battle-animation/spine-clone-settings-source-manifest.json)。

`ApplyOnStopCheckSourceDie` 已经独立的死亡视图接口，在技能图实例化、Awake 订阅前由 scope 注入；公共战斗控制器把本场共享场景传给技能图调度器。场景仅匹配当前检查点仍保留的来源实体根对象，并在结算后仍为零血时清除待播动作、停止旧动作并强制重播已有死亡动画；存活、即时复活、空来源、模型子对象及已移除实体不重播，不重新创建已移除的场景对象。原生模型／已有 Spine 绑定支持强制重新开始，附挂 Animator 使用同一重播入口。尚未装入战斗死亡片段，因此人物死亡动作和完整官方死亡 Playable 绑定仍未完成，也未完成整场战斗／Windows Player 验证。此项不代表分身正式演出已经完成。

`OfficialBattleCharacterDeathPlayback` 迁移原角色视图死亡播放、暂停恢复、强制重播和复活停止，并由显式依赖解析实体根和死亡状态。当前原生人物载体与视图生命周期尚未接入；过渡混合、独立武器／染色及完整战斗流程仍缺，详见[人物死亡图来源](../../assets/reference/battle-animation/character-death-assets-source-manifest.json)。

`PlayerModel_Game` 保留原层级、变换、外观及裁剪组件引用；默认控制器没有动画，人物初始化须按职业切换 Game 控制器。`SkeletonClipAttachment` 与 `SkeletonRendererWeaponClipSet` 保留附件身份、材质／粒子分支和生命周期。完整人物工厂、动作混合／物理及生产战斗绑定仍待完成，来源见[人物 prefab 与裁剪清单](../../assets/reference/battle-animation/player-model-prefab-source-manifest.json)。

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

复核固定版本 `871_156598` 的 `CharacterPanelSkillPanel`、`SkillPanelSkillDetailDialog`、`SkillRecommendDialog`、`SkillRecommendDialogItem`、`SkillTabGroupItem`，以及客户端 `CharacterPanelSkillContent.BeginPrepareEquip / RefreshEquipedSkill` 和 `SkillPanelSkillDetailDialog.RefreshBtn`。原有实现未给七个空技能槽生成 `ItemIconEmpty`，将“推荐”误接为祈愿，并使用通用 `SkillDetailDialogPanel`；安装时直接追加技能，无法选择位置或替换满槽。

`python scripts/prepare_skill_ui.py` 使用既有 APK 读取器、同版本 CDN 缓存和已校验中文文本分包，转换上述五个原始 prefab、禁用图像状态、遮罩、高亮框、中文文字及脚本引用目标；运行时资源继续纳入 `ui.json` 和显式本地加载清单。`skill_recommend.g` 经原始 `TbSkillRecommendInfo` 构造器完整读取，名称和说明取自原始中文表，输出为 `assets/official/skills.json`。bundle / 对象、APK / CDN 来源、原始配置与输出 SHA256、中文分包凭据及行为来源代码哈希见 [来源总表](../../assets/reference/SOURCES.md)。没有新增自绘图片。

技能页恢复四个战技与四个秘法槽位的原始空槽素材，详情使用技能专用 prefab 和同一套官方遮罩图标；点击安装 / 替换后使用原版暗色遮罩与同类型四槽高亮，选择目标槽位后才提交本地事务，取消不改档。推荐页使用原始方案与说明，仅安装已拥有且职业适用的技能，未获得部分保持空位。新增槽位位置与方案名称兼容旧紧凑数组，并随本地存档保存；原技能等级、升阶费用和战斗公式仍属已有单机适配。

技能列表排序／收藏字段的原 C# 输入哈希见 skill-ui/source-manifest.json 的 list_behavior；全部开关使用原 CommonCheckItem_On／Off。CharacterPanel_Skill 的原 AnchorMin／AnchorMax 曲线从固定 APK AnimationClip 解码，输入对象哈希见 character_animation_inputs；未用手调坐标补偿。收藏值保存到本地存档以替代原网络操作，界面状态和分组仍取原客户端实现。

`prepare_skill_quality_text.py` 从固定 `871_156598` 的 APK／匹配 CDN 读取 `GlobalColors.asset.QualityColorConfig` 的 Normal 调色板，以及 `SkillPromotePanel`、`SkillRankUpNotifyDialog`、`SkillPanelSkillDetailDialog`、`SkillDetailDialog` 中六个品质文字的 `TMPColorGradient`。原四个 RGB 关键点、两个独立 alpha 关键点、Blend 模式和各组件 0°／45° 角度导出到 `unity/Assets/Official/Skills/skill-quality-gradient.json`（SHA256 `a735d8107a91afa88c592a5077b7955c489fafd1d084123ae728470697cb632e`）；没有使用 `GreenRainbow` 四角预设或绘制图片。技能详情、培养和升阶结果的原生 TMP 文本网格由 `OfficialSkillQualityTmpGradient` 接收原关键点。原 bundle、对象、CDN URL、输入字节、调用源码及生成绑定 SHA256 见 [skill-quality-text/source-manifest.json](../../assets/reference/skill-quality-text/source-manifest.json)。

渲染迁移依据恢复后的同 APK `TMPColorGradient.OnPreRenderText`（`0x1121dd44`）、`SetGradientColor` 和 `UpdateMesh`，保留每行未旋转范围与绕原 RectTransform 原点旋转后的顶点归一化、顶点颜色替换和 Color32 的 ties-to-even 舍入。原顶点与取色样本保存在 `scripts/fixtures/skill_quality_native.json`，`SkillTextValidation` 对运行时计算执行 36 个顶点与 10 个颜色样本的逐字节比对；原生镜像、metadata、Unity 引擎和方法字节哈希同存来源清单。

技能详情与角色预览继续使用固定 APK `871_156598`：`prepare_skill_detail_data.py` 解码原 `SkillItemRangeInfos.g.bytes` 的 1,365 条范围记录，恢复 `SkillTagItem`、标签枚举及职业归属／前置链；布局明确绑定 174 张原 UI 精灵。`prepare_skill_native_text.py` 先比对固定 APK 原始对象 SHA-256，再复用对象完全一致的既有原生 FZY4K 字体／材质／Alpha8 图集与 TMP shader 转换，为技能建立独立 GUID 和运行时副本；保留初始图集，避免 TMP 构建处理修改共享资源。`prepare_skill_character_shader.py` 同样核对 APK 中 `Universal Render Pipeline/Spine/SkeletonDye-2D` 原对象与既有转换凭据，保留全部 544 组 GPU 程序，仅调整桌面预览 pass 标签；没有自绘替代角色素材。

依据：`assets/reference/skill-ui/unity-icon-source-manifest.json`、`assets/reference/skill-quality-text/source-manifest.json`。

## 完整还原部分

### 战斗技能与状态运行时覆盖

本次迁移把官方 `skill-rules.json`／`battle-rules.json` 中的技能图、命中节点和状态组件保留到离线战斗检查点。`FightOfficialCombatCatalog` 继续从官方实体字段创建主动技能、被动秘法、子技能、状态、增益／减益及触发配置；状态副本现在同时保存 `FightStatusHitSkillComponent`、`FightStatusDamageSkillComponent`、`FightStatusHitApplyDamageComponent`、`FightStatusHitDmgAddPerComponent`、`FightStatusDamageGetTypeComponent`、`FightStatusDamageReduceAbsoluteComponent`、`FightStatusHpIncreaseUnitComponent`、`FightStatusRoundIntervalComponent`、`FightStatusStartComponent`、`FightStatusStackChangeComponent` 和 `FightStatusHitSummonComponent` 的参数、嵌套技能／状态列表及触发计数，存档恢复沿用同一副本。

命中结算入口 `FightBattleExecutor` 已接入官方 `FightHitTriggerSkillComponent`、低生命值额外伤害倍率、命中条件计数、来源／目标解析、概率／最大次数、命中后子技能与状态、驱散、状态开始附加、叠层变化和回合间隔技能；`FightBattleHitExecutor` 在结算前保留 `FightHitExtApplyDamageComponent` 的低生命值倍率，并在伤害类型随机结果之后应用 `FightStatusDamageGetTypeComponent` 的元素／固定类型覆盖，在护盾结算前应用带次数和阈值的 `FightStatusDamageReduceAbsoluteComponent`。`FightStatusRoundIntervalComponent` 按状态回合目标只尝试一次当前回合，调用现有 `FightTurnQueue.ChangeInterval`，快进／后退方向与官方 `FastForward` 保持一致；含尚未解析的服务器过滤条件的记录暂不触发。触发技能使用现有 `FightBattleStatusSkillInvocation`，继续经过统一目标选择、伤害公式、状态生命周期、动画 cue、人物动作和战报事件链，而不是另建一套离线伤害实现。相关入口为 `FightBattleState`、`FightStatusLifecycle`、`FightBattleExecutor.MissingStatusComponents`、`FightBattleHitComponentRuntime`、`FightOfficialCombatCatalog.MissingComponents` 与 `FightBattleHitCollector`。

本地检查已覆盖官方实体数量统计（1,906 个技能实体、1,595 个状态实体）和新增组件的 Unity 导入／C# 编译、`Zjcs.Editor.ProjectTools.Validate` 及静态元数据检查；伤害减免、伤害处理、回合间隔、状态应用、组件访问和触发时序脚本均通过。当前源码构建的 Windows Player 已发布到 `D:/Desktop/杖剑传说/杖剑传说.exe`，在内置屏实际启动并检查官方健康忠告画面；总战斗脚本因仓库缺少其固定输入 `assets/reference/battle-rules/damage-reference.json` 在加载参考数据前停止。`FightStatusHitApplyDamageComponent`、`FightStatusHitDmgAddPerComponent`、`FightStatusHpIncreaseUnitComponent`、`FightStatusHitSummonComponent` 当前已完整保留配置和计数，但其服务器侧条件／召唤生命周期仍未接通；复杂 `FightStatusLogicComponent`／`FightSkillLogicComponent` 图、低频组件的逐字段行为、全量角色动作／特效资源绑定和正式技能战斗流程的 Play Mode 画面仍属于未完成项，不能以当前运行时入口声明全系统完成。

官方 `MyPlayableAnimation`、`MyPlayableDefault`、`MyPlayableRenderSortLayer`、`MyPlayableLight` 和 `MyPlayableLight2D` 已迁入 StoryPlayables，保留动作播放、Transform 持续时间、Renderer 排序以及 Light／Light2D 强度曲线和循环行为；来源见[动作 Playable 来源清单](../../assets/reference/battle-animation/skill-animation-playable-runtime-source-manifest.json)。

官方 `MyPlayableFixFlipXTrigger` 已迁入 EntityLifecycle，保留按 source/target 选择角色并调用 `MyGridTransform.SetScaleByFlipX` 的触发行为；来源见[翻转触发器来源清单](../../assets/reference/battle-animation/skill-trigger-playable-runtime-source-manifest.json)。

官方 `MyPlayableFadeEffectSequence` 已迁入 StoryFollowBone，按序列化数组的 StartTime 累计帧时间并调用对应 `MyEffectFadeOut.FadeOut(Duration)`；重复触发会先取消前一序列，销毁时停止协程。Unity Coroutine 承接原逐帧 UniTask 时序，运行验收尚未执行；来源见[效果淡出触发序列来源](../../assets/reference/battle-animation/skill-trigger-fade-sequence-source-manifest.json)。

官方 `MyPlayableCameraShake` 已迁入战斗技能 Playable 程序集，保留可序列化的 `CinemachineImpulseSource`、默认偏移 `(0,0,-250)`、按施法者世界坐标定位和即时 `GenerateImpulse`，并返回原配置持续时间。尚未接到各战斗模式，生产视觉验证未完成；来源见[相机震动代码凭据](../../assets/reference/battle-animation/skill-camera-shake-runtime-source-manifest.json)。

官方 `ApplyOverrideToMaster` 已迁入：按 `UseSourceDataToOverride` 从原施法者或目标查找角色母体，再按 `OverrideSource` 替换 Playable 的 source 或 target；scope 通过显式 `IBattleMasterEntityResolver` 提供母体查询。尚未取得这两条断言的运行结果。生产实体容器尚无 `MasterId → GameObject` resolver 绑定，完整技能图与生产调度仍未接通；官方来源与范围见[母体覆盖来源凭据](../../assets/reference/battle-animation/skill-master-override-runtime-source-manifest.json)。

主线、日常副本、竞技场与幻想阶梯的技能等级、秘法属性和状态统一读取 `SkillProgression.SlotLevel(..., SkillContext().CurrentSeasonId)`，只合并基础槽级和当前赛季祝福；其他赛季不参与。沿用原 `PlayerSkllSlotDataExt.GetSkillSlotLevel`，不改变技能品阶、子技能实体或计算公式。

技能演出 cue 保留原施法者、技能实体、目标、延迟及重放状态；GroundEffect、相机来源选择、抓钩、组合皮肤、残影和战斗镜头组件已迁入对应官方适配。技能图资源与调度入口已接入；生产相机、声音、角色母体与部件显隐仍缺少宿主绑定，完整战斗演出未完成。具体代码来源见各技能演出清单及[战斗执行](battle-execution.md)。

技能列表附件已按原 `ItemIconAttachmentConfig` 的层级顺序恢复：`CenterHintContent`（点击合成）、`ProfessionMismatch`（职业不符）、`NewMark` 和 `RedDotContent` 均使用原 prefab、字体及组件字段。红点沿 `RedDotMultiItem` 组合原升阶、装配及合成条件；原行项目是否考虑职业、碎片列表不挂这些附件等分支保持客户端配置。装配附件属于技能皮肤页的原附件类型 12，不额外叠加到原列表。新获得标记按原 `ItemNewMarkManager` 的零数量到正数量、正数量到零数量分支更新，点击技能、刷新列表或关闭技能列表时移除上次列表涉及的标记（包括视口外项目），当前玩家会话共享，重新登录清空；不写入主档案。同帧重复的数据投影不会提前消费尚未显示的新技能标记；刷新停止旧列表惯性并恢复首项。

伤害测试结算恢复原 `map/win_01`／`map/injuried_idle` 骨骼动画，不再仅切换空 Animator 状态。复用项目独立原生骨骼、曲线、IK、插槽及绘制顺序求值器，沿原骨骼顺序变换官方附件网格；表情附件来自 `AppearanceConfig` 的 UI 附加皮肤 `performFace`／`map_01`，补齐创建角色专用 bake 未包含的眼睛和嘴部附件。源骨骼、图集及纹理沿用已核验的 `871_156598` 人物资源，UI 动作名称与附加皮肤配置仍取固定 APK `871_156598`，不混改原始哈希。

方案名称已恢复原 `TMP_InputField`（对象 `5229620140741382476`，原始 580 字节 SHA-256 `12c6afe0debddb1e9a5bd164cc495b7f1f0ede5acc182a01872a6bd0728d104c`），保留原文字视口、字体、32 px 字号、颜色、光标与单行配置，不再添加旧 InputField 或透明替代图像。官方 TMP 分支额外序列化的 `m_EncodingType` 根据 APK 元数据恢复，严格检查完整对象字节。名称校验直接迁移 `Common.CJK` 与 `FormulaHandler.CheckSkillPlanNameLegal`：当前区域配置上限 12 计数单位，中日韩文字及原扩展区计 2，按原 StringInfo 文本元素计数；空名称、超长、`#<>`、半角／全角空格分别保留原错误分支，不执行 Trim。保留旧版本已接受的名称，空名称恢复“方案一”至“方案七”。单机没有在线 ContentCustom 禁用标志，原禁用遮罩保持隐藏；其原用途是禁用时重置名称，并非编辑入口。另有表情计数和旧名称兼容规则测试。

固定 APK 的 `GameSettings.SkillPlanMaxCount=7` 已恢复；旧单机档案的三套方案原样保留，并补入原文名称的方案四至方案七，不改变已选方案、装配、幻兽及共享槽位等级。方案下方按原 `Scrollbar` 的 Handle、20% 尺寸、颜色、方向与不可交互状态恢复，步数与 `CharacterPanelSkillPlan.RefreshView` 一样使用七档。

Unity 已新增离线技能档案与规则服务 [OfflineSkillProfile.cs](../../unity/Assets/Game/Runtime/Offline/Skills/OfflineSkillProfile.cs)、[OfflineSkillProgression.cs](../../unity/Assets/Game/Runtime/Offline/Skills/OfflineSkillProgression.cs)。模型保存七套方案、主动／被动槽位、基础等级、按赛季分开的祝福等级、技能阶位与道具数量；服务提供方案切换／改名、技能安装／替换／卸下、单槽及一键培养、单技能及批量升阶。培养和升阶在档案副本核算费用后提交。

固定 APK `871_156598` 的 394 个主动／被动技能、职业链、阶位累计差额、槽位培养、人物等级限制及材料已由 [prepare_unity_skill_runtime.py](../../scripts/prepare_unity_skill_runtime.py) 按三个来源清单校验并整理为 Unity 可解析的服务 DTO。固定 APK `item` 表确实缺少 18 个由 `Formula.GetPieceIdBySkillId` 计算出的碎片条目；另 190 条有效技能／碎片配对具有相同的稳定字段关系：碎片名为技能名加“碎片”，RawIcon 沿用技能 RawIcon，碎片类型为 111、Rank 为 -1、Quality 为 1。运行时 [OfficialCharacterAssetCatalog.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialCharacterAssetCatalog.cs) 仅对缺失且能映射至职业技能条目的碎片 ID 按此官方记录规律生成展示元数据；`OfficialSkillAssetTools.Validate` 校验 18 个目标 ID 的映射。该派生不声称缺失 `item` 原始行已从服务器配置取得。当前 `871_156598` 资源清单含技能图标图集，但未包含 `item` 规则表。[OfficialCharacterAssetCatalog.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialCharacterAssetCatalog.cs) 同时载入技能培养规则和 `skill-ui.json`；[OfficialSkillAssetTools.cs](../../unity/Assets/Game/Editor/Skills/OfficialSkillAssetTools.cs) 提供技能载荷导入绑定与哈希校验入口。官方 UI 层级和 270 张布局引用图像已整理至 `unity/Assets/Official/Skills/`，包括人物技能页与培养布局及原始 RectTransform 参数。

人物主存档中的 `OfflinePlayerProfile.Skills` 由 `OfflineMainlineProgressSession` 保存，材料与背包由 `OfflineCharacterProgressionService` 在角色事务中同步。人物技能面板接入方案选择／改名、收藏、列表、安装／卸下、拖拽交换、推荐、碎片详情、合成确认、单／批量升阶及馈赠；列表和弹窗沿用官方布局与动画。字体从本地官方资源加载，缺失时使用系统中文字体回退。生产合成、部分失败分支仍依赖未接入服务端响应。

技能槽使用官方 `item_skill` 和 `item_empty`，不额外添加培养按钮；点击已装备槽位进入官方技能详情，再由详情培养入口操作。图标替换时恢复原颜色，空槽使用原灰色加号精灵，槽位等级使用“级”。九宫格图像恢复导出记录的原 Sprite border。星级按 `skill_rank` 的 Quality／RankAddition 显示，数字星阈值 4 与品质星图映射由原 `SkillStarInfo`、`StarMiniInfo` 组件提取，来源凭据见 `assets/reference/skill-ui/unity-icon-source-manifest.json`；可用 `scripts/prepare_unity_skill_icons.py` 重建。

技能页顶栏复用已恢复的官方 `WorldCanvas/PublicPlayerInfo`、`PublicTopBar` 与 `MoneyUI` 原生 prefab，包含头像、等级、经验、名字、战力与技能升阶材料计数。`TabItemTopBarSwitcher.ShowTypeList=[18]` 对应 `GameSettings.SkillSeniorMaterialItemId=42200`，不显示其他货币；切到馈赠隐藏玩家信息，返回技能恢复。`OfficialCharacterSkillHeader` 保留官方布局与胶囊裁剪网格，按原头像摄影参数、皮肤排除项和 `home/idle_01` 生成头像；D3D 投影矩阵经 GPU 适配。人物预览依原骨骼的 100 UI 单位映射定位，世界几何不继承 Canvas 缩放；无幻兽时按原 `HasAnyPetCondition` 隐藏幻兽占位。布局读取同时修复弧度旋转、内容自适应、透明射线目标与文本溢出。窗口使用原 1080×1920 Expand 缩放；主页入口改为独立人物 Canvas，避免嵌套画布忽略自身 CanvasScaler 而继承主页宽高混合缩放，造成槽位放大、方案和列表下移；桌面安全区域按 Windows 全窗口处理，测试另检查与参考图相同的 675×1463 比例，角色外观、技能、等级和材料显示当前档案数据。

Editor 程序集显式引用 `Zjcs.Presentation.Character`，以供技能 Play Mode 验收访问人物技能面板；该引用由 `Zjcs.Editor.asmdef` 声明。

技能和公共战斗的实际运行检查按[素材管理](../asset-management.md)执行；技能战斗触发链及其当前缺项见[战斗执行](battle-execution.md)。

馈赠沿用原 `AchievementPanelWithTypes → AchievementPanelBase → AchievementPanelItem` 的两组数据键：Group 33 为 `CostItem(42200, SkillLevelUp)`，保留 895 条固定奖励；Group 9 为 `SkillScoreCount`，由原 15 条初始记录及 4,500×15 条循环增量记录展开为 67,515 个里程碑。积分按 `PlayerSkillDataExt.GetSkillScoreCountByAllSkill` 汇总持有技能各阶位的 `skill_rank.Score`，列表按原搜索范围、已完成优先排序及五行数量显示。点击领取按原 group、count=-1 行为，在同一本地事务中发放当前组全部可领奖励并保存领取凭据；重复领取不增加道具。培养材料累计值只在实际成功的槽位升级事务中增加，旧存档缺少此历史字段时从 0 开始，不由库存或当前等级反推。原页签动画、状态颜色、箱子、奖励图标和文案已绑定；原 TMP 自动缩放及换行参数从固定 APK 组件恢复。奖励图标已连接原 `ItemDetailDialog`：显示品质、数量、原说明及来源条件，按原规则隐藏不可用的使用／附加属性区域。

人物预览的阴影已恢复原 `AttachmentFollower` 对 `shadow` 骨骼的位置与局部缩放跟随，不再保留 prefab 编辑态的静止位置。测试存档与用户存档分开。未将整体技能功能标为完成。

技能页按官方 `CharacterPanel.mCharacterPanelSkillPanelParent` 挂入 `/CharacterPanel/BgPanel1/Content/TopGo/SkillContent`，恢复人物页、共享 HUD 和技能窗口的 `SafeArea` 参数。返回人物页按原节点字体绑定恢复 FZY4K 中文字体，避免原生 prefab 的缺失字体回落为方块。系统消息从 APK 原 `SystemMsgPanel` 提取层级、镜像背景、Canvas 排序与 Show／Hide 动画，收藏／取消收藏使用原文案。详情、碎片、合成确认、培养及升阶通知按原 `PopupWindow.mMaskType` 选择原生普通或模糊遮罩；模糊复用原 `ScreenBlur` 材质和四级处理，外部点击保留原 0.5 秒防误触条件。

技能详情使用原生 FZY4K TMP 字体、材质和 Alpha8 字体图集；多个文本标签共享按引用计数释放的字体资源，技能页使用独立图集副本。详情属性沿用原 `PropConfigUtils.CalcSkillViewProps` 与 `SkillPanelPropPanel.Sort`；职业图标、技能标签和作用范围来自原配置。品质背景、普通品质文字色、空星、战技／秘法状态、技能等级及祝福显示按原组件绑定。

人物预览和头像改用经原始对象 SHA-256 核验一致的官方 `Universal Render Pipeline/Spine/SkeletonDye-2D` shader 转换结果，保留 544 组原 GPU 程序、透明度截断、染色及渲染状态；仅适配桌面预览相机的 pass 选择。技能附属 prefab 与脚本字段由 `prepare_skill_extra_ui.py` 提取，18 个 `icons.json` 缺项按 APK 地址补入并显式绑定；来源见 `assets/reference/skill-ui/unity-icon-source-manifest.json`。

## 自定义实现

实体被动装配会读取官方 `DeactiveEntityPassiveSkills`，初始未激活的实体被动不合并属性或状态；其余实体被动按宠物已保存品阶、等级和持有者位阶传入实体技能源。

幻兽能量释放先写入根技能施放事件，再执行命中图、子技能和状态触发，保证战报与动画时间轴按根技能到派生效果的顺序消费。

Windows 竖屏显示适配按用户参考画面保留顶部安全留白：1080 UI 单位宽度对应约 137 UI 单位顶部留白，随窗口宽度缩放；这是桌面显示适配参数。原 `SafeArea` 的边界计算、PaddingTop／PaddingBottom、DisableTopAdapt／DisableBottomAdapt 与技能父级绑定保持官方数据，不修改内部槽位、方案或列表坐标。模糊遮罩恢复 APK 原 RawImage UV，并按 Windows 图形接口的纹理方向适配截图采样；不改变原 ScreenBlur 算法。

用户已确认伤害测试的单机规则：使用已迁移的本地战斗配置计算怪物属性，按原参数页勾选项应用怪物固定伤害 1／无限生命。这部分属于缺少服务器实现后的单机差异；原五组怪物、难度、回合数及勾选参数已连接本地 StageType 43 战斗，保留原 40 级开放条件。

用户已确认幻兽能量的单机时序：战斗开始使用原初始能量；幻兽自己的回合开始恢复原配置能量；主人实际生命损失累计满最大生命的 1% 时恢复原配置能量，护盾吸收不计；达到阈值后在主人行动结束时按原技能配置实体化并扣除阈值。实体技能、属性及持续时间继续使用原配置。APK 中 `FightRolePetTransferEntityComponent`／`FightRolePetEnergyModification` 只消费服务器能量结果，原配置 `NaturalRecoverEnergy=50`、`MasterLoseHpRecoverEnergy=6`、`MasterLoseHpTrigerRecover≈0.01` 已核对；`FightBattlePetEnergyRuntime` 已接入战斗检查点初始化、宠物回合开始恢复、主人实际 HP 损失阈值累计及主人行动结束释放，释放继续经过官方 `CreateSkill`、目标选择、状态／子技能和动画事件链。`OfficialBattlePetEnergyAssembler` 现在按 `FightEntityClassId`、宠物等级／品阶、主人属性调用 `CalculatePetEntityBaseProps` 与 `CalculateSummonInheritance`，装入实体主动技能和实体被动状态；支援技能按宠物等级和已保存品阶合并到主人属性与被动状态。实体释放使用 `CharacterType=5` 的临时施法源，不额外占行动条。完整宠物模型与独立 PetEntity 可见 HUD 仍需资源和绑定验证。关闭选项或进入普通关卡恢复普通结算。主页 shader 使用显式绑定；`OfficialSkillRuntimeShaderBuild` 在构建依赖收集前登记官方 URP，保留运行时切换所需变体，并在构建后恢复编辑器设置，Player 在首场景前恢复启动管线。存档选择与技能模糊流程在 finally 恢复活动渲染目标。主页展开／收起入口恢复原控制器默认的 `FunctionEntrysUI_Folded` 零时长动画，避免两个按钮同时命中。验证启动工具补上 Player 窗口识别，在内置屏按命令行客户端尺寸显示窗口并保持不抢前台，避免隐藏黑帧与占位矩形改变竖屏尺寸。

战报仅供查看本次战斗结果；退出后不写入本地历史。玩家／技能统计与快照详情读取本场内存检查点，关闭窗口释放头像纹理；在线分享按钮隐藏，不生成服务器链接或战报截图。

当前存档完整保存每个技能槽位等级及技能投资记录；缺少槽位字段或版本不符时拒绝读取，不执行旧投资转换。装备、费用、随机与进阶通知以本地事务提交，失败不保留部分升级。

服务器开放条件按[离线开放政策](offline-access.md)处理，保留人物、材料与实际原配方条件。

用户明确确认：将官方 Function 138 的一键批量升阶开放等级从 999 级改为与一键升级一致的 12 级；`SkillFastRanklUp`（Function 138）映射到 `SkillFastLevelUp`（Function 23）。费用、升阶上限及按钮优先条件继续使用原规则。

技能面板与培养使用本地角色存档，试伤战报复用[公共原生战报窗口](battle-execution.md)。主城技能入口的当前画面缺项见[主城与家园](city-and-home.md)；完整技能流程、战斗表现和 Windows Player 发布仍未完成。

技能试伤战报调用公共原生窗口；调用方提供本地头像／名称、原技能图标、只读技能快照和返回回调，公共模块管理分页、统计、循环列表、同步滚动与背景模糊遮罩。

## 尚未实现


- 培养页标题、四槽选择、材料数量／不足颜色、同品质升星与跨品质通知已按原组件绑定；当前奖励涉及的单人公会种植、钓鱼、养殖来源（类型 22／23／24，对应道具 1530／1540／1550）尚未连接 Unity 目的界面。被动触发技能保留 `FightSourceInfo` 对应的物品、品阶和祝福槽级，状态消失后仍能查看本场快照。全队幻兽实体模型、实体被动状态触发覆盖和完整宠物 HUD 仍待接入；怪物／召唤物／幻兽详情链接与馈赠来源跳转仍需补齐。

怪物详情使用原 `MonsterBriefInfoPanel` 与 `MonsterDetailPropsDialog`，怪物链接接入战斗模型目录。宠物详情按官方阶段、等级、主人属性和技能继承规则读取本地数据；技能列表沿用官方主动／被动顺序、品阶和详情 prefab。完整宠物模型及馈赠来源跳转仍待接入。

### 实现范围

技能文字、原生 TMP 渐变／效果、安装遮罩、取消与槽位提交及当前赛季祝福显示已恢复，伤害测试主动和被动技能使用完整槽级；

本地祝福存档按既有位面阶位键保存；读取 `system_level_gap_limit.SeasonId` 前通过 `astrological_season_config` 映射为原赛季编号，不直接比较两类编号。

培养页恢复 `SkillPromotePanel`、`PlayerUseItemContent`、`CharacterPanelSkillItem`、`SkillPromotePanelItem`、`ItemProgress`、`SkillStarInfo`、`StarMiniInfo` 与 `SkillRankUpNotifyDialog` 的原引用和状态字段，复用已有原生图标，不另套图标层级。顶部槽位只切换选择，升级按钮才扣除官方材料；升级／进阶后复用原窗口并播放 `SkillPromotePanel_Switch`。材料 42200 从固定原道具表显式补入技能载荷并绑定原图标，点击显示原道具详情；碎片材料去掉未绑定的模板数量，并按原 `GlobalColors.LightRed`／`Green` 显示是否足够。原 Light 调色板用 `TypeList` 定位颜色，不能把颜色枚举当数组下标。培养和通知名称使用原道具名，不追加手写阶数。同品质只刷新星级；只有跨品质才打开原进阶通知，通知显示实际前后品质、星级与原 `PlayerPropItemVariant` 属性行。

`PlayerSkillDataExt.CanSkillRankUp`／`TryGetAllSkillPromoteRank` 对已拥有技能不作职业限制，装配仍保留职业限制。祝福升级沿用 `CheckSkillBlessLevelCanUpgrade`、`IsBlessUpgradeLimitOtherSkillMax` 和 `SystemLevelGapLimitInfoParser`，要求八槽基础等级达到原上限并满足当前赛季最低槽级与阈值；前一赛季祝福不计入。横向培养列表使用官方 `LoopHorizontalScrollRect` 与 `ScrollToCellWithinTime`。

技能运行时使用本地事务保存七套方案、装配槽位和共享培养数据；方案切换不复制槽位投资，装备和费用校验按固定客户端规则执行。单技能升阶同品质时按官方 `SkillPromotePanelRankUpContent` 刷新星级与属性；跨品质时按原 `SkillRankUpNotifyDialog` 显示升阶结果。结果列表支持逐项展示、跳过和滚动；批量展示在原 `ShowItems` 动画事件（0.76666665 秒）后启动，并读取原 `ListItemShowSequence` 的 0.1 秒间隔与首次延迟，遮罩准备期间不提前播放结果；培养播放官方槽位升级动画及 `Skill_Upgrade.ogg`。批量升阶按已确认的离线开放规则处理。

固定版本缺少的 18 个技能碎片 `item` 行，按官方 `Formula.GetPieceIdBySkillId` 与其余 190 组稳定字段关系派生显示元数据；相关字段和图标对应由 `OfficialSkillAssetTools.Validate` 检查，不声称这些行来自服务器原始表。

彩虹品质调色板取自原 `LanguageUtils.SetSkillQualityWithColor → GlobalColors.SetTextColorByQuality → MultiLanguageText.SetColor(Gradient)` 数据路径，并由 `prepare_skill_quality_text.py` 导出固定包 Normal 调色板及六个 `TMPColorGradient` 角度。技能运行时将品质 7 渐变应用于详情、培养两端与升阶结果文字网格；原生 TMP 文本通过 `OnPreRenderText` 更新逐行顶点颜色，其他品质移除该效果。渐变关键点和 0°／45° 配置见 [OfficialSkillQualityGradient.cs](../../unity/Assets/Game/Runtime/Presentation/Skills/OfficialSkillQualityGradient.cs) 与 [skill-quality-gradient.json](../../unity/Assets/Official/Skills/skill-quality-gradient.json)。

## 代码与资源入口

以下为官方来源、既有规则与回归参考：`OfflineSkillProfile`、`OfflineSkillProgression`、`OfflineCharacterProgressionService`、`CharacterSkillPanel`、`OfficialCharacterPanelView.OpenSkillPanel()`、`OfficialSkillAssetCatalog`、`Zjcs.Editor.Skills.OfficialSkillAssetTools.Validate()`；客户端依据包括 `CharacterPanelSkillContent`、`CharacterPanelSkillListContent`、`SkillPieceDetailDialog`、`SkillMergeConfirmDialog`、`SkillPromotePanel` 与 `SkillMutiPromoteNotifyPanel`。
