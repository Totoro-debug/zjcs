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

13100 系列五份原分身 prefab 已接回初始化、播放、清理与停止流程。正式技能图工厂在原对象激活前，将原外观、角色、部件槽和资源 GUID 绑定到本场人物资源服务，仅替换旧 SDK 渲染端点；原 `PlayableSpinePlayerCloneInit` 继续从实际施法人物复制外观数据，身体、武器和贴图由既有原生外观管线加载。每份分身保留独立骨骼、动画／物理时钟和原轨道名。原分身没有 SortingGroup，因而保持 MeshRenderer 原层／序号及 -0.0002 的槽位深度，不把人物组内的部件插入序号提升为全局排序。原图暂时隐藏本体时，移动绑定使用该实际控制器在初始化时核实的参数／层记录；不强行激活本体，未知参数仍不通过。来源见[分身代码](../../assets/reference/battle-animation/spine-clone-runtime-source-manifest.json)和[配置](../../assets/reference/battle-animation/spine-clone-settings-source-manifest.json)。

完整工程编译及 `Zjcs.Editor.ProjectTools.Validate` 通过，公共 weekly 4800 Play Mode 用隔离诊断档案配置一次 13100，经原开始按钮模拟射线点击，实测首段 `100/skill_62_1` 的实际外观、独立状态／时钟、身体与武器几何及贴图、无分组排序、本体隐藏时的真实／未知参数判定、后续普通行动、败北结算、本地检查点记录和战斗退出后的分身释放；已查看分身与返回家园画面。该输入不修改用户档案，不能代替技能职业获得条件验证。同一元数据检查脚本在该隔离工程通过（134,566 个 GUID）；共享工作区全量检查仍受副本资源生成目录的重复 GUID／缺少 meta 阻塞，未计为通过。实际启动 Windows Player（既有完整原生资源包与当前脚本增量构建）也通过上述首段、模拟点击、后续行动、败北结算与释放检查，已查看分身及返回家园截图。技能图调度回归另通过方向与原空引用分支、同时间稳定顺序、跨帧时钟分段、延迟来源／目标及伙伴适配后追加分身绑定。家园空头像、白块、黑三角及边缘裁切仍存在，未完成完整视觉验收及正式发布。原 `MyFightSkillCombo` 只有空客户端类，唯一 `FightSkillComboComponent` 配置为 13100→13104／13105／13106／13107／13108；连段顺序／执行与 `AIRandomTargetFallbackSpawner` 仍缺服务器实现，不能把首段通过写成完整连段通过。位移物理继承、其余分身的公共战斗及完整视觉验收仍待完成。

灼烧状态 13052／13482 的持续演出图已从同版官方资源转换为 Windows 原生 prefab，保留完整层级、粒子、材质、贴图、shader、13052 的原声音事件及两份图的 `AutoStopOnSourceDie`，共 9 个依赖资产；资源入口为 `Combat/status-burns`，来源见[持续火焰资产](../../assets/reference/battle-animation/status-burn-assets-source-manifest.json)及[死亡停止组件](../../assets/reference/battle-animation/grid-playable-runtime-source-manifest.json)。死亡停止组件保留原立即停止分支，以及 UniTask 的整数毫秒、缩放时间、PlayerLoopTiming 8 和取消令牌；场景通过独立订阅接口提供已结算死亡通知。隔离 Unity Play Mode 已通过两份原生火焰的实际可见渲染、持有者／创建者区分、状态结束、死亡退订及恢复播放，结束后均无残留可见像素；13052 派发原事件 543904643，13482 按原 prefab 不派发声音。已查看运行中及结束后的截图。声音与死亡事件在此验证中由记录／通知适配器提供，不代表音频 bank 实播或生产地块接线。生产场景已接通地块与正式持续状态，3051／13052 已通过下述公共战斗 Play Mode；3051／13052 的 Windows Player 范围见下文；音频 bank 实播仍待验证。

`prepare_battle_grid_item_views.py` 已完整解码 3050／3051／3054 地块及全部 38 份配置引用的附带状态 prefab 的全部对象、组件和原指针，输入保留在 `Official/Combat/GridItemSources`，来源见[地块视图输入](../../assets/reference/battle-animation/grid-item-views-source-manifest.json)。其中三份地块的 `ECViewDriver` 原生 YAML 漏掉 `SerializeReference` 注册表，现已从官方程序集和原 bundle 恢复：按原顺序登记角色视图及状态显示视图，保留死亡 Playable、空外观／HUD 引用和行列配置。原始恢复输入已用于下述完整原生地块 prefab，并接入生产战斗场景。

`BattleGridItemEntities` 已迁移 3050／3051／3054 的官方客户端组件及完整 `ECFightRoleCharacterView`／`ECFightRoleStatusDisplayView`，保留原角色／主人登记、召唤通知、友敌标记、Z 排序、回合进度标志、出现／死亡／延迟显示和状态栏结束订阅。八个空客户端组件仍为空，包含新核实的 `MyFightStatusItemTrigger`；服务器数据通过显式视图输入提供，不另造服务器执行逻辑。地图原有 871 `MyActionAgent` 保持独立，地块使用同版 884 的事件专用实现，来源见[地块组件](../../assets/reference/battle-animation/grid-entity-runtime-source-manifest.json)。`prepare_battle_grid_item_views.py` 已按官方配置核对并解码全部 38 种地块附带状态；`prepare_battle_grid_entity_assets.py` 恢复三份地块和这 38 份状态的完整原生 prefab、全部依赖、挂点的空字符串／数字字符串及托管视图注册表，共 483 项资产、194 份贴图及两份 Windows shader，入口为 `Combat/grid-entities`，来源见[原生地块资产](../../assets/reference/battle-animation/grid-entity-assets-source-manifest.json)。3051／3054 按原指针绑定 `playable13061_fire_die`，3050 的死亡引用按原数据为空；声音和死亡停止叶节点复用既有官方迁移实现。隔离 Unity Play Mode 已通过三份地块完整层级及原托管视图加载／卸载、原死亡图或空引用、复用和退订；全部依赖 prefab 均无缺失脚本。38 份状态均经真实管理器、池和中心启动、逐帧渲染与可见像素检查、结束、恢复启动及订阅清理，已查看实际火焰、冰光、陷阱及雪地截图。验证相机持续渲染以保持 Unity 粒子可见性裁剪正常；声音与死亡输入仍使用验证适配器。规则回归另通过原 13480 的 29 格范围、逐格 40% 概率、0.6 秒生成时序、13482 根技能源及 13485 对大体型目标的实际伤害。新 3050 实体的服务器属性继承仍待单机方案确认，17002 的地块触发及持有者回合规则尚未核实，未启用这些服务器规则；公共战斗范围仍以下段为准。

生产接线由 `OfficialBattleGridEntityPlayback` 消费执行器记录的地块／状态生命周期事件：创建与结束各自保留发生时的不可变快照，外层移动、重放、宠物和触发施法偏移在本步结束后统一换算，覆盖时先结束旧状态及地块，再创建替代物；回合到期在本次行动结束时结束视图。共享技能图调度器按同一时钟推进地块和格内子技能，格内施法继续使用原地块实体及原 `ECGridTransform` 数据，不用普通角色数据覆盖它。工厂通过实际 `NativeMyEntityBinding`、`MyEntity`、原托管视图、角色／主人登记及状态代理连接目录中的原生 prefab；动作通知按原 `ActionAgentComponentImplement.StartAction`／`NotifyActionEnd` 的登记与开始／结束顺序提供。地块零 HP 不发角色死亡通知。冷恢复只装载当前仍有效的地块和状态，不重放历史创建或扣血事件；关闭战斗会结束原状态、实体和订阅。当前公共战斗实测覆盖 3051／13052；3054 及其余附带状态的完整公共流程仍待验证。新增目录资产不代表其服务器地块规则均已启用或经过公共流程验证，目录以外的 prefab 继续显式报未绑定。

完整工程 Play Mode 已通过公共 weekly 4800 战斗及模拟原开始按钮：原 13050／13052／13055 延迟生成九格火焰，同一原大体型 Boss 分别承受九次命中；九份实际原生地块、状态代理、持续火焰和原格变换数据保持有效，零 HP 不误播死亡；销毁宿主后从本地存档重建，整个检查点与计数保持一致，九格火焰恢复且不重放旧命中；三回合到期逐格结束状态并移除实体，之后正常获胜并保存结算。已查看实际九格火焰、冷恢复、到期和结算后画面；返回家园仍有空头像、白块、黑三角和边缘裁切，视觉验收未通过，不能据此发布完成版本。独立规则回归另通过移动后施法的原生成／伤害时间一致性、同格覆盖顺序、地块事件真实序列化及恢复不重放；技能重放、状态重放及触发时序回归通过。 Windows 构建检查发现新增地块 shader 未登记稀疏原程序域，导致编译原包不存在的关键字组合；现已将 `GridEntity_Noise_Mask_Pro_0` 的原 260 组程序、两个 pass 接入既有 `OfficialSparseShaderVariants`，保留原 shader 字节和全部有效组合，已通过全部 48 份 shader 的 45,692 个阶段的原程序域检查，并实际编译本次新增地块 shader 的全部 1,040 个 Windows GPU 阶段；完整工程 `Zjcs.Editor.ProjectTools.Validate` 通过，完整 Windows 构建成功。实际 Windows Player 使用该原生资产包及增量构建的当前脚本，已通过九格延迟创建、同一大型 Boss 九次命中、冷恢复时检查点与计数不变且不重放、三回合逐格结束／移除及正常胜利存档；已查看火焰、冷恢复、到期与返回家园画面。首次 Player 检查在尚未就绪的按钮容器中心点击失败，诊断现与 Editor 一样等待原生射线命中区并查找实际子图像；已补充实际按钮点击事件凭据，冷恢复截图等待原延迟 HUD 挂载后采集，同组 Player 检查通过。上述家园视觉缺项仍在，完整视觉验收及正式发布未完成。

`ApplyOnStopCheckSourceDie` 已经独立的死亡视图接口，在技能图实例化、Awake 订阅前由 scope 注入；公共战斗控制器把本场共享场景传给技能图调度器。场景仅匹配当前检查点仍保留的来源实体根对象，并在结算后仍为零血时清除待播动作、停止旧动作并强制重播已有死亡动画；存活、即时复活、空来源、模型子对象及已移除实体不重播，不重新创建已移除的场景对象。原生模型／已有 Spine 绑定支持强制重新开始，附挂 Animator 使用同一重播入口。人物宿主 `OfficialBattlePlayerNativeModelView` 已引用原 `playable_die`／`playable_die_onStart` 及死亡生命周期适配器，死亡图通过原 `die`／`immediately_die` Animator 参数驱动；这一绑定不能代替人物死亡、复活和强制重播的完整公共战斗／Windows Player 画面验证，分身公共战斗目前仅验证首段，完整连段仍未完成。

`OfficialBattleCharacterDeathPlayback` 迁移原角色视图死亡播放、暂停恢复、强制重播和复活停止，并由显式依赖解析实体根和死亡状态。人物模型宿主已接入这些死亡方法，完整死亡／复活公共流程与画面验证仍缺，详见[人物死亡图来源](../../assets/reference/battle-animation/character-death-assets-source-manifest.json)。

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

命中结算入口 `FightBattleExecutor` 已接入官方 `FightHitTriggerSkillComponent`、低生命值额外伤害倍率、命中条件计数、来源／目标解析、概率／最大次数、命中后子技能与状态、驱散、状态开始附加、叠层变化、召唤物创建／移除触发和回合间隔技能。召唤物创建时按 `SummonStatusId` 附加状态；移除技能采用下述已确认的单机目标方案。死亡触发在该目标的 HP 效果、死亡监听和即时复活结算后执行，属性重算本身不代表移除；回合开始／结束处理到期移除。移除标记写入检查点，旧检查点中已死亡／结束的召唤物不补发历史事件，嵌套技能期间仅处理已结算目标。`FilterSubstitute` 同时过滤创建和移除，禁止中的状态及未解析的 `FilterCondition` 不触发。`FightBattleHitExecutor` 在结算前保留 `FightHitExtApplyDamageComponent` 的低生命值倍率，并在伤害类型随机结果之后应用 `FightStatusDamageGetTypeComponent` 的元素／固定类型覆盖，在护盾结算前应用带次数和阈值的 `FightStatusDamageReduceAbsoluteComponent`。`FightStatusRoundIntervalComponent` 按状态回合目标只尝试一次当前回合，调用现有 `FightTurnQueue.ChangeInterval`，快进／后退方向与官方 `FastForward` 保持一致；含尚未解析的服务器过滤条件的记录暂不触发。触发技能使用现有 `FightBattleStatusSkillInvocation`，继续经过统一目标选择、伤害公式、状态生命周期、动画 cue、人物动作和战报事件链。相关入口为 `FightBattleState`、`FightStatusLifecycle`、`FightBattleExecutor.MissingStatusComponents`、`FightBattleHitComponentRuntime`、`FightOfficialCombatCatalog.MissingComponents` 与 `FightBattleHitCollector`。

本地检查已覆盖官方实体数量统计（1,906 个技能实体、1,595 个状态实体）和新增组件的 Unity 导入／C# 编译、`Zjcs.Editor.ProjectTools.Validate` 及静态元数据检查；伤害减免、伤害处理、回合间隔、状态应用、组件访问和触发时序脚本均通过。`scripts/test_battle_hit_summon.ps1` 使用真实官方配置执行 12140／25012 的创建附加、12200 的替身过滤、25015→11020 的召唤者来源／移除位置／敌我范围／0.4 秒死亡时序、结束移除、检查点防重、旧档兼容及 24233 即时复活排除。本批 Unity Play Mode 已通过真实公共 weekly 战斗、模拟原始开始按钮、召唤物到期触发、12040 实际恢复 100 万 HP 后触发一次并保留 25 万 HP、24242 减层、60290 目标减益门槛与真实伤害触发、检查点回读及最终胜利；已查看战斗和返回场景截图，实际 Windows Player 也已通过同组模拟点击、12140／25012 创建、12202 到期移除→25015／11020、12040 治疗减层、60290 条件命中、凭据回读及最终胜利，并查看了准备、移除演出和返回场景截图。返回家园时仍有空头像、局部白块、黑色三角及角色边缘裁切，因此完整视觉验收未通过；正式目录 `D:/Desktop/杖剑传说/杖剑传说.exe` 尚未更新为本次验证构建，不能将规则验证通过写成技能系统完成。总战斗脚本因仓库缺少其固定输入 `assets/reference/battle-rules/damage-reference.json` 在加载参考数据前停止；替身回归已补齐测试状态的根技能源并通过负面状态转移、同来源叠层／刷新、到期和检查点检查。`FightStatusHpIncreaseUnitComponent` 的实际恢复累计和 25131 减层已按下述确认方案接入；`FightStatusHitDmgAddPerComponent` 已按确认方案接入每段临时属性与施法次数，独立回归通过。`FightStatusHitApplyDamageComponent` 的基础条件与充能已按下述确认方案接入；失血倍率已按确认方案接入；实体数量缩放的计数与回合结束门槛、规则地块生命周期与上述限定范围的生产地块演出已接入；技能标签过滤已按确认方案接入。`FightStatusHitSummonComponent` 的三项召唤物标签过滤已按下述确认方案解析，未知过滤类型仍禁用。复杂 `FightStatusLogicComponent`／`FightSkillLogicComponent` 图、低频组件的逐字段行为、全量角色动作／特效资源绑定和正式技能战斗流程的 Play Mode 画面仍属于未完成项，不能以当前运行时入口声明全系统完成。

24387 的回合间隔过滤按官方 `FightStatusRoundIntervalComponentInfo.FilterCondition` 的“传入 ApplyEntity”注释、`LogicNotFilter` 和 `EntityCharacterTypeFilter` 定义恢复：排除状态承受者的角色类型 8（Substitute），不检查状态创建者或替身关联角色。配置仍为 20% 快进，过滤在抽概率和本回合凭据之前执行，不匹配不消耗凭据；原开始回合的两个阶段共用一次记录。`FightBattleStatusRoundIntervalComponentState` 保存排除列表、定义标记、触发次数与已检查回合，复制不共享列表，旧档重建定义时保留两项计数；未知过滤仍禁用。入口为 `FightOfficialCombatCatalog.MissingComponents` 与 `FightBattleExecutor.MissingStatusComponents`。规则回归通过角色类型 0～9、创建者类型无关、实际行动条从完整间隔 3,162,278 缩短到 2,529,822、每回合仅一次快进，以及复制、JSON 和旧档计数保持。静态检查、完整 Unity 编译及 `ProjectTools.Validate` 通过；公共周常战斗的 Play Mode 和实际 Windows Player 均以模拟鼠标点击开始，验证普通玩家实际队列快进 20%、同回合不重复、后续符合回合仍可触发及 Unity JSON 检查点保留排除条件，并完成召唤创建／到期和结算回归。已查看两端运行截图；替身排除仅有规则测试，未声称该分支已在画面中实测。Player 复用本会话原生资源完整构建，仅增量重编本批脚本；返回主页仍存在头像缺失、异常图块及边缘裁切，不代表完整表现验收或正式发布。

技能命中范围按目标全部身体格判断相交，每个命中节点对同一实体只结算一次。原 `GridGroundComponent.EnterPosSub` 将实体登记到所有 `BodyRangeInfos` 格，`GridTransformComponent.InRange` 同样使用身体范围；`FightBattleTargeting.SelectScopeTargets` 复用已有 `InScope`，修复仅查询中心格导致 10660 无法命中相邻大型首领的问题。规则回归使用原 10660、25 格首领身体及 60290／24009，覆盖身体边缘命中、原范围外排除、四段伤害各结算一次及四次条件触发。

官方 `MyPlayableAnimation`、`MyPlayableDefault`、`MyPlayableRenderSortLayer`、`MyPlayableLight` 和 `MyPlayableLight2D` 已迁入 StoryPlayables，保留动作播放、Transform 持续时间、Renderer 排序以及 Light／Light2D 强度曲线和循环行为；来源见[动作 Playable 来源清单](../../assets/reference/battle-animation/skill-animation-playable-runtime-source-manifest.json)。

官方 `MyPlayableFixFlipXTrigger` 已迁入 EntityLifecycle，保留按 source/target 选择角色并调用 `MyGridTransform.SetScaleByFlipX` 的触发行为；来源见[翻转触发器来源清单](../../assets/reference/battle-animation/skill-trigger-playable-runtime-source-manifest.json)。

官方 `MyPlayableFadeEffectSequence` 已迁入 StoryFollowBone，从原 UniTask 状态机恢复异步执行：先累加本帧 `Time.deltaTime` 再判断 `StartTime`，按序列化数组每帧最多推进一项并调用原 `MyEffectFadeOut.FadeOut(Duration)`，不排序或合并同时到期项。重复播放重置时间并取消旧序列，显式停止、最后一项及销毁取消令牌；单独停用触发器对象仍继续原异步任务。修复了先前 Coroutine 适配导致的首帧延后及停用即停止差异。隔离 Unity Play Mode 已使用官方波纹粒子通过首帧阈值、同时间项分帧、原数组顺序、重播重置、显式取消、停用继续、空效果引用及销毁取消，并通过既有十份移动效果图／17 个跟随节点回归；已查看实际原生粒子截图。完整生产技能和 Windows Player 对应淡出流程仍未验证；来源见[效果淡出触发序列来源](../../assets/reference/battle-animation/skill-trigger-fade-sequence-source-manifest.json)。

官方 `MyPlayableCameraShake` 已迁入战斗技能 Playable 程序集，保留可序列化的 `CinemachineImpulseSource`、默认偏移 `(0,0,-250)`、按施法者世界坐标定位和即时 `GenerateImpulse`，并返回原配置持续时间。尚未接到各战斗模式，生产视觉验证未完成；来源见[相机震动代码凭据](../../assets/reference/battle-animation/skill-camera-shake-runtime-source-manifest.json)。

官方 `ApplyOverrideToMaster` 已迁入：按 `UseSourceDataToOverride` 从原施法者或目标查找角色母体，再按 `OverrideSource` 替换 Playable 的 source 或 target；scope 通过显式 `IBattleMasterEntityResolver` 提供母体查询。公共控制器已将共享 `OfficialBattleSceneView` 绑定到技能图调度器，按当前检查点直接 `MasterId` 查找对应场景实体；缺失、已移除或模型子对象不补造来源，仍保留的死亡实体可提供母体。现有独立检查入口为 `OfficialBattleAnimatorPlaybackAcceptance.RunMasterBindings`；本批未重跑该入口，完整公共战斗和 Windows Player 的对应画面仍待验证。官方来源与范围见[母体覆盖来源凭据](../../assets/reference/battle-animation/skill-master-override-runtime-source-manifest.json)。

主线、日常副本、竞技场与幻想阶梯的技能等级、秘法属性和状态统一读取 `SkillProgression.SlotLevel(..., SkillContext().CurrentSeasonId)`，只合并基础槽级和当前赛季祝福；其他赛季不参与。沿用原 `PlayerSkllSlotDataExt.GetSkillSlotLevel`，不改变技能品阶、子技能实体或计算公式。

技能演出 cue 保留原施法者、技能实体、目标、延迟及重放状态；GroundEffect、相机来源选择、抓钩、组合皮肤、残影和战斗镜头组件已迁入对应官方适配。技能图资源与调度入口已接入，公共控制器通过 `BindSceneHosts` 绑定实际战斗相机、角色母体查询与原部件显隐；这项代码接线不代表各技能及模式均已完成画面验证。生产声音仍缺少原事件 DSP 宿主，完整战斗演出未完成。具体代码来源见各技能演出清单及[战斗执行](battle-execution.md)。

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

用户已确认 `FightStatusHitApplyDamageComponent` 的基础单机规则：每段结算前按原伤害／治疗类型、目标护盾、角色类型、技能 ID 判断，仅把该状态原属性中 `AddPercentPropType` 对应值加到本段属性、`ReducePercentPropType` 对应值减去，其他属性及命中不受影响；充能遵守原门槛、指定技能和数量。60230 限定目标有效护盾，60240 限定召唤物，替身按原 `ApplyTargetCharacterTypeIfSubstitute` 查关联对象类型；24079 对 12860／14070 消耗 24031 的 5 层。入口为 `FightConditionalApplyProperties`，由同一施法上下文接入结算前属性。固定版本共 46 项，其中 38 项基础配置、3 项失血倍率及 4 项技能标签过滤已接入；25098 已接入地块计数与回合结束门槛，规则执行器已生成独立灼烧地块；公共场景的完整原生绑定和跟随父释放节点移除的行为仍未完成。独立回归覆盖护盾耗尽、角色类型／替身、指定技能、减属性、充能、真实护盾吸收前的伤害增幅、临时属性隔离与旧检查点定义恢复；本批已通过 Unity 导入／C# 编译、`Zjcs.Editor.ProjectTools.Validate` 和公共 weekly 战斗的模拟点击；Play Mode 实际执行了 60290 条件增伤并完成胜利结算。实际 Windows Player 也已执行 60290 条件增伤并完成上述持久化与胜利检查；其他条件仍仅完成独立规则回归，返回家园后的视觉缺项见本页验证范围。

用户已确认状态 24590／25095／26060 的失血倍率单机规则：每段命中前，以目标当前已损失 HP 比例除以原 `HpDecreaseUnit` 向下取整，受原 `MaxHpScale` 上限约束，满血为零份；仅把指定增伤／无视防御属性乘以份数，临时用于本段。原秘法 10594 的说明与配置均为每损失 15% 一份、最多 5 份。累计单位用 decimal 承接原 float 的配置精度，避免恰好 45% 时少算一份；回归覆盖满血、15%／45% 边界、五份上限和最大 HP 为 1001 时的分数阈值。

用户已确认 60270／25135／25141／25282 的 `SourceEntityGameplayTagFilter` 单机绑定：以本段实际命中技能实体读取官方 `FightGameplayTagComponent`，子技能使用自身实体，不以根技能代替；查询复用已迁移的 `GameplayTagQueryExtension.IsMatch`／`GameplayTagExtension` 四字位运算，按原 All／Any／Without 组合检查，不猜测补标签。查询词和掩码随状态深复制及保存，旧技能检查点缺少标签时从原实体配置恢复。回归覆盖原 8192／4096／2048 标签、四项配置逐项匹配与排除、子技能与根技能区分、缺失标签拒绝及副本隔离。

用户已确认两类本段属性修正（`HitApplyDamage`／`HitDmgAddPer`）不受 `CanTriggerChild`／`CanTriggerStatusEntityIds` 限制：它们只修改当前命中属性，不创建子技能／子状态，仍遵守各自过滤、充能和次数。官方 `FightSkillComponentInfo` 对该开关的注释限定为触发子技能或子状态；25141／25282 匹配的原技能均关闭此开关。真正创建子技能／子状态的触发继续保留既有门禁。

25098 的地块计数已按确认的单机方案接入：以状态持有者当前格为中心，统计原 13 格范围内仍在场的 3051，不限制创建者及敌我；0 块不触发回合结束治疗，至少 1 块触发原 12040；每段治疗重新计数，仅把指定治疗属性乘以 `min(数量,6)-1`。原秘法 10597 的描述及 `FightStatusEntityCountCfg` 为依据，APK 的 `FightGridItem_part1.g.bytes` 包含实体 3051，`grid_item.g.bytes` 中 3320 指向该实体；`MyFightRoleGridItemSpawner` 为空。当前计数读取检查点 `GridItems` 中的独立实体，状态副本保留独立的原范围；隔离规则回归覆盖 0～8 块、范围／移除／实体类型过滤、归属无关、移动后重新计数、真实治疗、保存回读及旧档定义恢复。规则执行器已接入地块创建、持续、覆盖和移除，实际 Unity 检查点恢复亦通过；公共场景的完整原生地块、状态演出及读档已接通并通过下述 Play Mode；3051／13052 的 Player 范围见上述公共验证，不能用普通地形类型代替灼烧物件。

用户已确认灼烧地块生成时的单机来源绑定：当前实际施法者为状态 Creator，地块为 Applicator；`ReferenceType` 仅用于原配置的属性、阵营等继承。13052 等状态依原配置在 Creator 回合末扣减 3 回合，Creator 死亡按 `RemoveAtRoundTargetDie` 移除，状态结束按 `RemoveApplyEntity` 移除地块；格内触发技能由地块发起并保留原根技能源。此决定补足空的 `FightRoleGridItemSpawnerComponent`／`MyFightRoleGridItemSpawner` 所缺的服务器来源绑定；创建、回合到期和 Creator 死亡移除已接入规则执行器；地块与状态的官方持续演出已在公共场景绑定；3051／13052 的 Player 范围见上述公共验证。

用户已确认地块 3051 的属性快照规则：创建时复制 `ReferenceType` 参照单位当时的最终属性、等级、位阶和阵营，保留原根技能培养及祝福信息；后续不跟随参照单位属性变化，不额外套用普通怪物召唤继承倍率，伤害继续使用原触发技能公式。该单机决定补足空的地块 `FightPropComponent` 配置及缺失的服务器继承计算。规则执行器及公共 Unity 演出已接入并通过 Play Mode；3051／13052 的 Player 范围见上述公共验证。

用户已确认地块生成与覆盖的单机规则：按原命中锚点、范围格、延迟和逐格概率创建；必须位于战场内并符合原地形掩码，普通角色占格不阻止灼烧地块。同格 `OverrideTypes` 命中时，不分阵营先结束旧地块及其状态，再创建新地块；`GridItemFindAdjacentIfBlocked=false` 时失败仅跳过本格。`FightOfficialGridItemRules` 通过 `scripts/prepare_battle_grid_items.py` 从同一官方版本解码 3320→3051、3328→3054 的表项、实体及地形掩码，来源见 `assets/reference/battle-rules/grid-items-source-manifest.json`。大体型怪物按完整身体占格触发，每个特殊地块独立结算：同一怪物覆盖 3 个灼烧地块就结算 3 次；进入范围前后也检查完整身体占格。`FightGridItemRuntime` 保存独立属性快照，`FightBattleExecutor.GridItems` 管理状态来源、生成和移除，地块不加入普通角色行动／胜负名单。规则测试已覆盖原 13050／13051 九格、原 13052→13055 的实际伤害与 0.78 秒延迟、旧技能图字段恢复、Creator／Applicator、培养来源、属性隔离、存档后回合到期和 Creator 死亡移除（活着离场按 `RemoveAtRoundTargetDestroy=false` 保留）、敌方 3328 被覆盖后的完整移除、地形／战场边界，以及同一大体型怪物的九个灼烧格分别产生九次伤害、站立／进入触发。另以既有 Play Mode 捕获的 4800 初始战场快照作规则回放，保留原场景格子和 Boss 身体，生成九个地块并结算实际伤害；另已完成下述实际公共地块 Play Mode 流程。小型 Unity 工程已通过实际 JsonUtility 检查点写入／恢复后的到期及死亡移除；两份持续火焰已通过上述独立 Play Mode 渲染和生命周期检查。公共场景的完整 prefab 绑定、模拟点击战斗和地块读档已通过下述 Play Mode；3051／13052 的 Windows Player 范围见上述公共验证。

地块的 Unity 读档检查发现并修复了共用检查点问题：`JsonUtility` 将空内联 `VoodooDoll` 字段还原成默认对象，旧的非空判断会把普通角色误认为替身。`FightBattleUnitState.IsVoodooDoll` 统一按有效关联目标 ID 判断，行动、增益限制、伤害／减益传递、触发过滤和胜负统计均使用同一判定，不增加存档版本或丢弃有效旧关联。小型 Unity 工程已通过实际 `JsonUtility` 写入／恢复后的地块到期和死亡移除；独立回归同时通过真实替身的原 12200／4600 创建、溢出伤害传递、护盾绕过、增益拒绝、减益转移及存档，条件命中回归亦通过。本批已通过完整工程 Unity 导入／C# 编译、`Zjcs.Editor.ProjectTools.Validate` 及静态元数据检查。公共战斗的新地块读档流程已通过下述 Play Mode；3051／13052 的 Windows Player 恢复范围见上述公共验证。

用户已确认 `FightStatusHitDmgAddPerComponent` 的条件增伤单机规则：每段命中结算前检查原来源、伤害类型、元素、目标 HP／状态条件和概率；满足后仅为本段临时合入该状态原属性，按配置消耗层数和更新触发计数。持续次数按符合 `OnlySkillActions`／`OnlySkillType` 的一次施法扣一次；`UpdateDurationSkillCountIfTrigger=true` 时仅成功触发才扣，剩余次数保留到检查点。固定版本共 29 项此组件，均以持有者作为命中来源；60280 限定目标 Action 52／57，60290 检查目标减益，24085 消耗 24058 层数。`FightConditionalHitProperties` 使用每次施法独立的上下文和本段属性字典，原技能排序分类取 `skill.SkillSortTypes`，不把 `SkillSortType` 当作 `SkillType`。独立规则回归已覆盖目标动作／减益、属性隔离、多段施法次数、充能倍率、真实伤害增幅及条件消失后的恢复、双段治疗和检查点恢复；Unity Play Mode 已通过公共 weekly 战斗中 60290 的目标减益门槛、实际伤害触发与最终胜利；实际 Windows Player 同一条件命中流程亦已通过；全量正式演出与返回家园后的视觉验收仍未完成，不视为该组件完整完成。其他尚无配置实例的来源类型及逻辑标签不推测执行。

实体被动装配会读取官方 `DeactiveEntityPassiveSkills`，初始未激活的实体被动不合并属性或状态；其余实体被动按宠物已保存品阶、等级和持有者位阶传入实体技能源。

幻兽能量释放先写入根技能施放事件，再执行命中图、子技能和状态触发，保证战报与动画时间轴按根技能到派生效果的顺序消费。

用户已确认召唤物移除触发技能的单机规则：以召唤者为施法者，以被移除召唤物所在格为技能位置锚点，目标仍按该技能原有范围、敌我关系和动作条件选择；状态 25015 的技能 11020 按此路径执行。该规则属于缺少服务器目标查询实现后的单机适配。

用户已确认召唤创建触发 25068／25112／25281 的单机标签绑定：检查本次新建召唤物自身的官方 GameplayTag，按原 All／Any／Without 查询；缺失标签不满足，不推测补标签，匹配后分别施加原 24071／24072／24206，仍遵守概率、替身过滤、寿命条件与次数上限。原 `MyFightStatusHitSummon` 缺少服务器实现，`SourceEntityGameplayTagFilter` 仅保留查询配置；该绑定属于单机决定。`FightBattleExecutor.MissingStatusComponents` 通过怪物表的 `TempEntityId` 读取真实角色实体标签，先检查查询再抽概率和消费次数；`FightBattleStatusHitSummonComponentState` 保存查询、已解析标记和计数，深复制不共享掩码，旧档按原来源属性重建定义且保留已用次数。独立规则回归已通过官方 12140→3196 和 10710→3181 的三种不同附加结果，以及实体模板映射、标签缺失、过滤不消耗次数、零概率、MaxCount=1、未知过滤禁用、深复制、JSON 与旧档恢复；召唤属性继承及条件命中回归通过。完整工程 Unity 导入／C# 编译、ProjectTools.Validate 及静态元数据检查通过。公共 weekly 战斗的 Play Mode 与实际 Windows Player 均已通过模拟原开始按钮、3196 的 24071／24206 实际创建附加及 24072 排除、Unity 检查点查询与次数保持，随后正常完成到期移除、治疗减层、60290 条件增伤和胜利存档；已查看准备、移除演出及返回场景截图。Player 使用上述完整构建的原生资产包和本批增量脚本；3181 的分支当前仅完成规则回归。返回家园的视觉缺项仍按本页记录，未据此更新正式发布或声明全量完成。

用户已确认 `FightStatusHpIncreaseUnitComponent` 的单机规则：累计实际恢复的 HP，溢出治疗不计；每满配置 `UnitHpPer × MaxHp` 触发一次并保留余量，遵守总次数和单次 HP 变化次数上限，被单次上限限制的余额留待下一次正恢复处理。`TryAtStart` 仅初始化零余额，不把初始生命当治疗。治疗命中、吸血、复活与属性重算导致的实际 HP 增加经独立通知进入该累计器，嵌套恢复按各次变化排队结算。余额与计数写入战斗检查点；入口为 `FightStatusTriggerRuntime.HpIncrease` 和 `FightBattleExecutor.HpIncrease`。

用户已确认状态 25131 的单机减层规则：首次生效及每次再次施加时执行一次，同来源续用活跃实例并累计最多 5 次；只减少同一施加者、同一根技能和来源实体产生的 24242，减至零层即移除。原配置的零回合持续时间仍有效，已结束实例不会因再次施加复用。`FightHealingStackReduction` 在状态应用／续用入口执行此规则，不将这项决定推广为其他未核实 `StackChange` 状态的服务器实现。

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
