# 技能、方案与培养

[总索引](../README.md)

状态：未完成

## 功能简介

人物页进入技能，包含战技、秘法、碎片、推荐、详情、七套方案、共享槽位升级、祝福培养、单技能升阶及批量升阶。完整迁移范围还包括主动技能、被动秘法、被动与触发效果、子技能、增益／减益、技能动画和人物动作；各战斗入口的装配和技能页交互在本页维护，共用执行规则、状态生命周期及演出缺项见[战斗执行](battle-execution.md)。当前仅完成下述部分，不能以技能页或局部规则通过代替全系统完成。

人物页的技能入口已连接七套方案面板。技能档案与角色主档案共同保存，离线规则和人物页数据源已接入；技能窗口、推荐、碎片详情、合成确认、培养页及升阶结果均按官方布局数据构造并连接本地事务。推荐页使用 `skill_recommend`、`skill_recommend_item` 与 `skill_tab` 官方布局，按当前职业前置链和阶位显示方案。官方确认弹窗已绑定，纯祝福一键升级在预演达到 40 次时要求确认。技能页、详情、培养和结果弹窗使用 `skill-ui.json` 的官方动画轨道；结果列表逐项展示、跳过和自动滚动，槽位培养播放官方动画和 `Skill_Upgrade.ogg`。技能页现在使用 `character_skills` 的官方整屏 `CharacterPanelSkillPanel` prefab，包括返回条、方案与技能槽、战技／秘法／碎片列表和底部“技能／馈赠”双页；底部第二页按官方 `Achievement` 状态切换。技能列表使用官方 `skill_list_row` 与 `skill_list_separator`，行列数和间距按 `AdaptiveRowLayoutCalculator` 计算（155 px 项目、两侧各 50 px、最小间距 20 px），合成、收藏和未收藏分组各自起行，收藏组与未收藏组之间按官方条件显示虚线分隔；碎片页按官方 160×160 网格尺寸排布。打开技能时播放官方 `CharacterPanel_Skill` 状态：保留人物预览和背景，按原动画隐藏资料区并调整模型位置；返回时播放 `CharacterPanel_SkillSwitchmain` 并恢复被修改的动画属性。40 级伤害测试及通用窗口表现仍未完成，不能据此将整个技能功能标为完成。

当前交付目标按用户限定为两个初始职业和四个首次转职的全部技能。所选 `884_156692` 的 `profession_base` 中，初始职业为 Rank=1，首次转职为 Rank=2；以下统计按各职业的直接 `Skills` 和 `FightUsedSkills` 列表计数，转职后仍继承前置职业全部技能。

初始职业的技能清单、详细行为、单机差异及验证范围分别见[战士（一阶职业）](skills-warrior.md)、[法师（一阶职业）](skills-mage.md)；后续二至八阶职业按路线维护于[骑士](skills-knight.md)、[斗士](skills-fighter.md)、[术士](skills-warlock.md)、[贤者](skills-sage.md)详情页。本页维护共用界面、方案、培养和跨职业基础能力；各路线的共享机制以链接引用，避免重复维护。

| 职业 | Profession | 战技（ItemType=100） | 秘法（ItemType=101） | 内置战斗技能项 |
| --- | --- | --- | --- | --- |
| 战士 | 1 | 13 | 13 | 10128、10125 |
| 法师 | 10 | 13 | 13 | 10126、10127 |
| 骑士 | 2 | 6 | 5 | 10129 |
| 斗士 | 6 | 6 | 5 | 10130 |
| 术士 | 11 | 6 | 5 | 10131 |
| 贤者 | 15 | 6 | 5 | 10132 |

去重合计 50 个战技、46 个秘法和 8 个内置战斗技能项，共 104 项；范围同时包含这些技能所依赖的属性、触发、子技能、增益／减益、召唤／地块、动画、人物动作及本地存档。上述六个职业的 `profession_base` 和这 104 项的 `skill`／`item` 已逐字段对照所选官方表与当前战斗运行表一致；当前技能页可用职业及培养数据仍由原职业继承链提供。结束本次目标须完成这份范围的实际技能行为、官方界面与演出、Play Mode 和 Windows Player 流程，并发布到约定目录；清单计数和局部检查通过不代表这些技能已全部完成。六份职业详情页继续保留其余技能的既有实现和缺项，但本次目标不要求继续扩展到更高阶职业。

## 原始官方实现

官方技能页按职业、前置链、品质、元素、标签、范围、安装槽、附加属性和升级／突破配置刷新；详情与模型预览使用原控件、数值计算及显示规则。

### 官方代码与资源依据

`scripts/prepare_battle_directional_skill_sources.py` 从所选 APK 优先解码六职业及 104 项直接技能／物品，规范化 JSON 数字字典键后逐字段比较运行表，并核对 10026 默认 10780、方向实体 11640／11650／11660 及直接命中组件，未修改历史运行载荷或其预期哈希。原 `FightSkillData.RefreshSkillEntityData`、`SkillEntityData` 与 `Pos2d.LookDirection` 的输入身份见[方向技能来源](../../assets/reference/battle-rules/directional-skill-source-manifest.json)。原 `MyFightSkill` 的四向播放图与 `DirectionEcEntityId` 的方向实体字典是两层配置：技能可使用同一个实体并按方向选择原播放图，空方向图继续沿用原默认图回退；不要求为每个技能制造四个实体。

技能图资源由 `prepare_battle_skill_graph_assets.py` 按官方实体根对象和纹理身份生成，导出目录仅作分组，不作为实体 ID；纹理候选先匹配原名，再处理同前缀别名。资源依赖、输入与输出哈希见技能图来源清单。

技能图中的 18160／18620／59470／59940 已接回原内嵌怪物骨骼，分别使用 monster_3481／3620／3331／3636 的完整 Spine 4.2.28 数据；18620 保留五个独立实例和各自 skill_4_1～skill_4_4 轨道、IK 与物理约束。`prepare_battle_embedded_monster_skeletons.py` 先查 APK，再按同版已校验本地资源恢复骨骼、图集、原材质和组件字段，入口为 `Combat/EmbeddedMonsterSkeletons/bindings`。正式技能图工厂在 Awake 前仅替换旧 SDK 骨骼渲染端点，原图仍控制启停、位置、跟随、透明度及结束；保留原 XY／旋转位移继承、Update 时序、循环、缩放、槽位深度和停用不清状态。59470 的 bone64 跟随与 59940 的 shadow 跟随按原引用重绑；59940 保留原分离渲染子节点、属性块／渲染标记，并按原 High 加载器引用接入完整 mip 的 Windows RGBA32 贴图。其余三组按原图集材质引用保留 placeholder 贴图，没有据资源名自行升级。来源、输入输出哈希和转换方式见[内嵌骨骼来源](../../assets/reference/battle-animation/embedded-monster-skeletons-source-manifest.json)；新增资产尚未生成完整素材快照。

八实例组件验证及完整编译、`Zjcs.Editor.ProjectTools.Validate` 已通过；960 帧覆盖实际移动、动画／约束采样、有限几何、重复绘制不推进姿态、原材质／高精度图集、原分离节点和跟随引用。材质替换接口复用既有原生渲染器的映射方式，已逐实例实测替换／恢复并确认只重画已完成姿态、不推进动画。该检查在未激活原图的组件层执行，不代表完整技能图的 Play Mode、Windows Player 或原怪物／伙伴获得流程已通过；隔离工程静态检查通过（134,581 个 GUID），共享工程最新全量静态检查亦通过（136,020 个 GUID）。公共 weekly 4800 Play Mode 已经原开始按钮模拟射线点击，通过 18620 的五实例原图播放、独立时钟、实际可见几何与原材质、后续普通行动、败北结算、检查点及退出释放，已查看技能和返回家园画面。此诊断使用隔离战斗输入控制技能及生命／攻击，不修改用户档案，也不证明原怪物获得／装配流程；18160／59470 的原怪物来源及后续验证见下一段，59940 的原伙伴来源仍待接入并验证。已构建完整 Windows 验证版并在内置屏实际启动，最终材质修正后的脚本增量构建也通过同一流程：原开始按钮模拟射线点击、五实例推进与实际可见几何／原材质、后续普通行动、败北结算、本地检查点及退出释放；已查看两轮 Player 的技能与返回家园截图。技能图调度回归通过方向、空引用／默认图、延迟与同时间顺序、跨帧时钟分段和伙伴／内嵌怪物适配后追加人物分身绑定。家园空头像、白块、黑三角及边缘裁切仍存在，本功能尚未完成完整视觉验收及正式发布。

技能内嵌 3205 的原 `SkeletonMecanim` 已接到现有完整骨骼／网格与 Animator 管线，保留原 prefab 层级、轨道、材质、Low 外观加载器和贴图；42 根骨骼、14 段动画及原控制器的 9 个状态、15 条转移已逐字段核对。资源注入使用实际来源 prefab 资产身份，避免同名 `playable_16040_SourceToHit` 在不同 bundle 中误绑；沿用公共外观管理服务，实际 Low 图集由本模块独立持有／释放。来源见[内嵌 Mecanim](../../assets/reference/battle-animation/embedded-mecanim-source-manifest.json)。所选官方 14657→57780 的直接 HitList 只有 57781，没有 57784；本次仅恢复原资源，不增加未证实的战斗触发。完整编译、`Zjcs.Editor.ProjectTools.Validate` 与共享工程静态检查通过（142,622 个 GUID）。实际 Play Mode 已核对来源资产身份、同名资源排除、两个独立骨骼状态及 Animator 时钟、原 Low 图集、有限网格和释放，并查看四张实际摄像机截图。原 `track (2)` 与 `Eff_Npc_WolfStone_Atk` 默认停用，生产流程保持原样；组件验证仅显式启用复制实例的父节点以检查渲染，当前实际画面覆盖默认 Idle，不代表全部 14 段动画或完整战斗触发。完整 Windows 验证版已构建，并在内置屏实际启动通过上述组件检查；已查看四张 Player 实际摄像机截图，原图集与待机姿态正常，进程退出码为 0。完整技能战斗流程、其余动画的实际播放及正式发布仍未完成，新增资产未进入完整素材快照。

内嵌技能的原施法模型 3331／3481／3620 已接入完整原生怪物目录，保留原有绑定；召唤模型的增补范围见下文；分别对应原组零技能项 14679→59470、14237→18160、14281→18620。`prepare_battle_embedded_source_actors.py` 组合既有 APK 优先提取、完整骨骼包、逐 mip 纹理转换、原控制器、挂点和阴影恢复流程，不改服务器规则或正式技能装配。新来源与依赖凭据见[原施法模型来源](../../assets/reference/battle-animation/embedded-source-actors-source-manifest.json)；新增资产尚未进入完整素材快照。已逐项对照所选 884 官方 `monster_group` 组零及 `skill` 表，确认三份模型的技能项／实体映射与当前运行表一致，未替换数值规则。完整编译与骨骼目录检查通过，当前全目录范围见下文；带物理约束的骨骼使用生产管线已有的有序处理器。共享工程静态检查通过，包含补充头像后的最新检查为 136,202 个 GUID。公共战斗诊断在场景挂载前保存原模型身份与原身体占格，并控制隔离输入的生命／攻击、一次技能与三回合时限，不修改用户档案或正式装配；三份原模型的公共 weekly 4800 Play Mode 已通过原开始按钮模拟射线点击、实际模型挂载、一次原技能、后续行动、结算／检查点及退出释放，已查看技能与返回家园截图。3331 的 59470 内嵌骨骼与 3620 的 18620 五实例均实际推进且可见。直接核对 18160 官方根序列发现，序列在 0.467 秒调用粒子 `effect`，没有引用 `effect (1)` 的骨骼跟随节点，且该骨骼对象原本停用；验证据此检查实际粒子显示、闲置骨骼始终停用且时间为零，不为通过检查强行开启原闲置分支。生成器保存原根序列、节点及对象字段和哈希，并拒绝未复核的额外触发引用。3620 的行动条头像在上述截图中为空白，已定位为目录缺项并从所选 APK 提取原 `G_3620`；原像素单位、中心枢轴、边框、挤出及纹理采样字段已核对，通过独立补充目录接入，不改原地图头像目录；Windows 使用 RGBA32，不再次进行块压缩或透明边缘像素扩散，并在实际战斗中检查加载格式。该 bundle 与原头像输入逐字节同一哈希，凭据同时保留当前来源与历史输入身份；补齐凭据后的 Play Mode 画面已显示原 3620 头像，但同次实际截图发现部分技能特效及准备阶段行动条出现青色块，已核对 Unity 2022.3 的异步 shader 编译会临时使用纯青色占位，材质绑定与变体就绪仍须逐图核对；该轮不能计为完整视觉通过，Player 画面仍待核对。完整 Windows 验证构建曾在大量资源重新导入期间因内存不足退出，来源刷新现已串行重跑通过；后续 Play Mode／Windows Player、完整视觉及正式发布仍待完成。

`prepare_battle_grid_item_views.py` 已完整解码 3050／3051／3054 地块及全部 38 份配置引用的附带状态 prefab 的全部对象、组件和原指针，输入保留在 `Official/Combat/GridItemSources`，来源见[地块视图输入](../../assets/reference/battle-animation/grid-item-views-source-manifest.json)。其中三份地块的 `ECViewDriver` 原生 YAML 漏掉 `SerializeReference` 注册表，现已从官方程序集和原 bundle 恢复：按原顺序登记角色视图及状态显示视图，保留死亡 Playable、空外观／HUD 引用和行列配置。原始恢复输入已用于下述完整原生地块 prefab，并接入生产战斗场景。

`ApplyOnStopCheckSourceDie` 已经独立的死亡视图接口，在技能图实例化、Awake 订阅前由 scope 注入；公共战斗控制器把本场共享场景传给技能图调度器。场景仅匹配当前检查点仍保留的来源实体根对象，并在结算后仍为零血时清除待播动作、停止旧动作并强制重播已有死亡动画；存活、即时复活、空来源、模型子对象及已移除实体不重播，不重新创建已移除的场景对象。原生模型／已有 Spine 绑定支持强制重新开始，附挂 Animator 使用同一重播入口。人物宿主 `OfficialBattlePlayerNativeModelView` 已引用原 `playable_die`／`playable_die_onStart` 及死亡生命周期适配器，死亡图通过原 `die`／`immediately_die` Animator 参数驱动；这一绑定不能代替人物死亡、复活和强制重播的完整公共战斗／Windows Player 画面验证；分身与连段的当前公共战斗验证范围见上文，完整视觉及 Windows Player 连段验证仍待完成。

`OfficialBattleCharacterDeathPlayback` 迁移原角色视图死亡播放、暂停恢复、强制重播和复活停止，并由显式依赖解析实体根和死亡状态。人物模型宿主已接入这些死亡方法，完整死亡／复活公共流程与画面验证仍缺，详见[人物死亡图来源](../../assets/reference/battle-animation/character-death-assets-source-manifest.json)。 新增 `ZJCS_PLAYER_DEATH_ACCEPTANCE` 公共宿主诊断入口，使用隔离战斗输入观察实际致死命中、原 `die` 参数图与 `Die` 状态、身体／武器贴图和退出释放；已通过完整工程编译、`ProjectTools.Validate`、隔离工程静态元数据检查（134,568 个 GUID）、公共 weekly 4800 Play Mode 与实际 Windows Player；两端均由原开始按钮的模拟鼠标点击进入，原 18050 命中使人物实际死亡，`die` 图设置原布尔参数、`Die` 状态推进后生成可见原生身体／武器几何和真实贴图，败北结算并保存检查点，退出后原人物对象释放，已查看死亡及返回家园截图。仅此普通人物死亡分支通过，复活、强制重播和带死亡恢复的公共战斗仍未验证；既有家园画面缺项不计为通过，未正式发布。

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

### 通用四向施法模块

普通装备技能统一经 `FightSkillCastModule.Prepare(actor, equipped, targets, targetPosition)` 产生配对的 `Skill` 和 `Cast`。模块从 `FightOfficialSkillItemCatalog.DirectionEntityIds` 读取官方映射，选择实体并装配该实体自己的命中图、时长和培养属性，继承根技能来源、槽位、外观、祝福、AI 与自定义伤害／治疗输入，然后快照目标、朝向及状态。无方向映射的普通技能也走此入口并保留原定义。模块不消费次数、不改写装备定义；执行器随后只对原槽执行一次释放。其他技能接入官方配置即可复用，模块没有 10026 专用分支。

通用链路复用已有职责，不为各技能复制选敌、伤害或渲染实现：

| 职责 | 共用入口与配置 |
| --- | --- |
| 对象选择、范围、AI、身体格与确定性随机 | `FightBattleTargeting`；原技能与命中配置，接近移动后重新选敌 |
| 方向实体、养成与来源、结算／表现一致的施法快照 | `FightSkillCastModule` 与 `FightBattleSkillCastPresentation` |
| 命中时序、伤害／治疗、状态、子技能及触发 | `FightBattleExecutor.ExecuteSkill`、已有伤害公式与能力实现；消费模块返回的技能定义 |
| 四向特效、原默认图回退、人物动作与骨骼 | `OfficialBattleSkillEntityPlayback` → 原 `MyFightSkill` 与 Playable → 既有原生 Spine 4.2 渲染；消费同次施法的快照 |

施法图开始时现在按该次施法快照再次调用原 `MyFightSkill.DoSourceTurn` 的镜像更新：先把来源 `GridTransform.FlipX` 写入，再通过 `MyGridTransform.ResetScale` 更新人物实际模型。这样场景刷新与原生技能图之间即使存在一帧边界，也不会出现人物仍朝旧方向而技能特效按新方向释放；不改变 `FacingIndex`、方向实体选择、目标范围或原始动作资源。

`scripts/test_battle_directional_skills.ps1` 通过公开模块入口及实际执行器验证四向伤害、方向实体独立培养、单次槽位消费、回执去重和冷恢复；同时验证普通技能保持原定义、无目标／同格／无映射回退，以及仅在测试内给第二技能绑定方向字典后无需新增专用代码。后者仅证明复用，不将诊断映射写入官方配置。普通位移及连段回归通过。通用化后的上／下／左／右四向已全部通过原开始按钮模拟输入、实际 11640／11660／11650 实体、严格的官方 `100/skill_03` 动作推进、伤害、一次槽位消费、检查点、结算及返回的 Play Mode，并查看四向技能与返回画面；Unity 全工程导入／编译、`ProjectTools.Validate` 与静态元数据检查通过（145,655 个 GUID）。Windows 脚本增量构建成功，实际 Player 四向专项全部通过同一流程，已查看四向技能、原结算及返回画面；验证使用独立临时档案，未覆盖用户存档。返回家园仍有既有空头像、白块和黑色几何缺项；完整技能功能及正式发布尚未完成。

普通技能的共用施法入口同时更新原 `FlipSource` 所需的人物镜像：接近移动完成后，用实际施法锚点与来源的横坐标按原 `GridTransformComponent.GetFlipX` 分支确定镜像；纵向与同格保留原镜像，未要求转身的技能保留已有状态。原 `FightSkillComponentImplement.Start` 要求先把技能的 GridTransform.FlipX 传给来源，`MyFightSkill.DoSourceTurn` 再恢复显示镜像；迁移入口补齐此前遗漏的输入更新，不改动作／特效资源、固定方向或连段规则。此修正覆盖无方向实体字典的普通技能，避免向左接近后向右施法仍播放左向动作。228 次施法回归已加入反向初始镜像，按实际目标（包括友方治疗）检查转身、单次槽位消耗与冷恢复；原四向实体和六段连招回归通过。10006 上／下／左／右的实际 Play Mode 复验均通过，原 MyGridTransform 的实际显示镜像与施法快照、横向目标位置一致，52 张阶段截图已查看；只含本批修正的隔离工程已完成严格全资源构建；10006／10009／10052 的四方向共 12 组 Windows Player 均通过实际镜像、原动作、状态、检查点及结算返回检查，156 张运行截图已查看。10052 的原自中心目标继续保留已有镜像。普通 10029／10810 的横向诊断使用原范围中最远的有效轴向站位，避免贴身阵容被原 AI 跳到目标另一侧后仍误标为原请求方向；仅调整隔离阵容，不修改生产移动或方向规则。最终工程已通过该技能朝左的实际 Play Mode 与严格脚本增量构建，构建出的 Windows Player 左右两向均通过原开始／结算模拟点击、施法方向、一次释放、检查点及返回，27 张实际运行截图已查看。成品入口沿用 `D:/Desktop/ZjcsGame/Zjcs.exe`；完整技能迁移仍未完成。

`scripts/test_first_promotion_skill_casts.ps1` 另覆盖六职业范围内 50 个主动技能与 7 个内置主动项的 228 组四向输入：使用原范围选择有效距离，经公共执行器实际释放，核对原实体／装备来源、一次装备释放、步骤回放，以及从施放前检查点冷恢复后完整结算状态一致。该组保留原伤害、治疗及子技能图参与冷恢复比较；完整状态一致不代替各技能数值和触发条件的逐项预期断言，也不代替原生动画／特效验证。

公共技能图诊断新增 `ZJCS_FIRST_PROMOTION_SKILL`，从原六职业及技能项读取实体、方向映射、培养属性和有效施法距离，原动作名取已有播放配置；继续复用原开始按钮模拟输入、场景、检查点、结算及返回，不为每项另写战斗宿主。该诊断固定隔离角色外观与单格敌方拓扑，治疗技能为测试角色配置半血以满足原 AI；逐项技能的完整条件、职业外观、增益／减益、召唤与伤害预期仍须专项验证。

新增代码已用 Unity 自带 C# 编译器及保留的 Player 参数编译；在完整资源验证副本内装入该程序集后，10008 的原 10600／`100/skill_01`、10021 的原 10730／`200/skill_11` 均通过上／下／左／右实际 Windows Player。施法快照的四向值、原敌方／自身对象选择、槽位、检查点与结算返回通过，各流程五张截图已查看。10021 各方向实际治疗量均为 138,186；该受控输入实测值不能代替完整培养和治疗条件的预期矩阵。10000、10010、10016、10004 的向上流程也已通过并查看五张截图。本次共享 HEAD 更新后重新通过 228 组四向规则回归，静态检查通过（145,778 个 GUID）。

同一诊断的职业输入现通过 `FirstPromotionSource()` 读取技能直接所属的原职业及 `PrePro` 链，使用对应初始武器和原起始技能，再经现有衣柜初始化、技能装配、人物外观服务和公共战斗构造器创建。技能图入口要求战斗单位的职业与来源一致；该输入仍为隔离诊断，不代表职业获得条件或技能页完整交互验证。六个职业各选 10008／10000／10010／10016／10004／10021，均已通过对应职业输入的向上 Player 流程、原动作、对象选择、检查点、结算及返回，各流程五张截图已查看；人物位阶由现有等级规则推导。职业输入改动的静态检查通过（145,925 个 GUID），最新共享代码上的 228 组四向规则回归通过。相关程序集已按共享 HEAD 的隔离 Home 源码副本与本次诊断改动编译，法师及骑士的最终 Player 复核通过，五张流程截图均已查看。

六职业范围的 50 个主动技能及 7 个内部主动项，全部 57 项均已通过对应职业向上 Player 流程；各流程原动作推进、来源与对象路由、单次装备释放、检查点、结算和返回通过，各五张截图均已查看。这些项目仍属于受控技能图覆盖，不代表全部条件、数值和其他方向已验收。

验证器在捕获待测技能后安排原起始技能完成结算，按阶段停止统计待测施法，避免 10012 本身就是原起始技能时将收尾施法重复计入；失败信息保留实体、动作、截图、施法计数与结算状态。`test_first_promotion_skill_player.ps1` 从同一官方职业／物品范围选择技能，统一运行四向流程并恢复进程环境；它接受已有 Windows 验证版和通过公共工具创建的隔离目录，沿用内置屏不激活启动、静音与进程内模拟输入。诊断增加方向快照断言，并以固定 60 帧步长保留施法后 0.1／0.25／0.45／0.7 秒四个早期画面，结束恢复帧设置，不改正式播放计时。10012 四向及 10021 朝下自身治疗已通过新增检查，各九张实际截图已查看，覆盖绿色蓄力／剑气及金色治疗效果。相关演出程序集已按提交版隔离源码加本次诊断改动编译，并装入复用资源的 Player 副本实测；静态检查通过（145,959 个 GUID），不代表新增 Unity 导入、Play Mode、完整视觉验收或正式发布。

六职业的 104 项技能运行表全部已消费字段，以及从本体、秘法状态、显式方向图递归追踪的 383 个配置引用实体，已按所选 884 APK 优先解码逐字段核对；可复跑 `python -B scripts/verify_first_promotion_skill_sources.py`，来源见[六职业技能凭据](../../assets/reference/battle-rules/first-promotion-source-manifest.json)。73 处新增空驱散排除名单、25019／25022 的新增 false 报告字段显式作为无限制／关闭默认值核对；24083 沿用上文所选版本适配，未改写历史载荷及预期哈希。

四向专项已覆盖范围内全部 57 项主动／内部主动技能，共 228 条 Windows Player 流程。每个方向的原实体路由、方向快照、一次施放、检查点、结算与返回断言通过；各方向的 0.1／0.25／0.45／0.7 秒及主采样画面均已实际查看，完整界面画面另按既有朝上流程与抽样方向查看。验证使用 683dd7f723 规则快照及增量编译的诊断，证明这些配置图在对应时点运行，不代表全部数值、后段召唤、状态和秘法已完整还原。补充的召唤检查从 HitGraphEntities 读取正式召唤节点，要求检查点实际出现原模型 ID／MasterId，并在后续帧检查真实模型可见；此前只检查技能图的通过结果不能替代此项。

独立检出 a7a069937d 后，隔离工程已完成 Unity 2022.3.57f1c2 导入／编译与 ProjectTools.Validate，静态检查通过（146,031 个 GUID）；致死复活专项 Play Mode 亦通过。导入日志仍有既有 Burst 查找 NaughtyAttributes.Core 的初始化异常，Validate 不受阻；不据此声称全部导入日志无异常。原工程隐藏会话及其产物保留：自动审批以“blocked by policy”拒绝进程／锁恢复和已完成会话清理命令，本轮已查看截图及完成子目录的清理也被拒绝，未提供具体原因，相关操作未执行，产物保留。独立工程验证没有绕过该拒绝。

### 战斗技能与状态运行时覆盖

本次迁移把官方 `skill-rules.json`／`battle-rules.json` 中的技能图、命中节点和状态组件保留到离线战斗检查点。`FightOfficialCombatCatalog` 继续从官方实体字段创建主动技能、被动秘法、子技能、状态、增益／减益及触发配置；状态副本现在同时保存 `FightStatusHitSkillComponent`、`FightStatusDamageSkillComponent`、`FightStatusHitApplyDamageComponent`、`FightStatusHitDmgAddPerComponent`、`FightStatusDamageGetTypeComponent`、`FightStatusDamageReduceAbsoluteComponent`、`FightStatusHpIncreaseUnitComponent`、`FightStatusRoundIntervalComponent`、`FightStatusStartComponent`、`FightStatusStackChangeComponent` 和 `FightStatusHitSummonComponent` 的参数、嵌套技能／状态列表及触发计数，存档恢复沿用同一副本。

命中结算入口 `FightBattleExecutor` 已接入官方 `FightHitTriggerSkillComponent`、低生命值额外伤害倍率、命中条件计数、来源／目标解析、概率／最大次数、命中后子技能与状态、驱散、状态开始附加、叠层变化、召唤物创建／移除触发和回合间隔技能。召唤物创建时按 `SummonStatusId` 附加状态；移除技能采用下述已确认的单机目标方案。死亡触发在该目标的 HP 效果、死亡监听和即时复活结算后执行，属性重算本身不代表移除；回合开始／结束处理到期移除。移除标记写入检查点，旧检查点中已死亡／结束的召唤物不补发历史事件，嵌套技能期间仅处理已结算目标。`FilterSubstitute` 同时过滤创建和移除，禁止中的状态及未解析的 `FilterCondition` 不触发。`FightBattleHitExecutor` 在结算前保留 `FightHitExtApplyDamageComponent` 的低生命值倍率，并在伤害类型随机结果之后应用 `FightStatusDamageGetTypeComponent` 的元素／固定类型覆盖，在护盾结算前应用带次数和阈值的 `FightStatusDamageReduceAbsoluteComponent`。`FightStatusRoundIntervalComponent` 按状态回合目标只尝试一次当前回合，调用现有 `FightTurnQueue.ChangeInterval`，快进／后退方向与官方 `FastForward` 保持一致；含尚未解析的服务器过滤条件的记录暂不触发。触发技能使用现有 `FightBattleStatusSkillInvocation`，继续经过统一目标选择、伤害公式、状态生命周期、动画 cue、人物动作和战报事件链。相关入口为 `FightBattleState`、`FightStatusLifecycle`、`FightBattleExecutor.MissingStatusComponents`、`FightBattleHitComponentRuntime`、`FightOfficialCombatCatalog.MissingComponents` 与 `FightBattleHitCollector`。

本地检查已覆盖官方实体数量统计（1,906 个技能实体、1,595 个状态实体）和新增组件的 Unity 导入／C# 编译、`Zjcs.Editor.ProjectTools.Validate` 及静态元数据检查；伤害减免、伤害处理、回合间隔、状态应用、组件访问和触发时序脚本均通过。`scripts/test_battle_hit_summon.ps1` 使用真实官方配置执行 12140／25012 的创建附加、12200 的替身过滤、25015→11020 的召唤者来源／移除位置／敌我范围／0.4 秒死亡时序、结束移除、检查点防重、旧档兼容及 24233 即时复活排除。本批 Unity Play Mode 已通过真实公共 weekly 战斗、模拟原始开始按钮、召唤物到期触发、12040 实际恢复 100 万 HP 后触发一次并保留 25 万 HP、24242 减层、60290 目标减益门槛与真实伤害触发、检查点回读及最终胜利；已查看战斗和返回场景截图，实际 Windows Player 也已通过同组模拟点击、12140／25012 创建、12202 到期移除→25015／11020、12040 治疗减层、60290 条件命中、凭据回读及最终胜利，并查看了准备、移除演出和返回场景截图。返回家园时仍有空头像、局部白块、黑色三角及角色边缘裁切，因此完整视觉验收未通过；正式目录 `D:/Desktop/ZjcsGame/Zjcs.exe` 尚未更新为本次验证构建，不能将规则验证通过写成技能系统完成。总战斗脚本现已恢复官方伤害基准并通过 460 组公式比对及全部后续断言；替身回归已补齐测试状态的根技能源并通过负面状态转移、同来源叠层／刷新、到期和检查点检查。`FightStatusHpIncreaseUnitComponent` 的实际恢复累计和 25131 减层已按下述确认方案接入；`FightStatusHitDmgAddPerComponent` 已按确认方案接入每段临时属性与施法次数，独立回归通过。`FightStatusHitApplyDamageComponent` 的基础条件与充能已按下述确认方案接入；失血倍率已按确认方案接入；实体数量缩放的计数与回合结束门槛、规则地块生命周期与上述限定范围的生产地块演出已接入；技能标签过滤已按确认方案接入。`FightStatusHitSummonComponent` 的三项召唤物标签过滤已按下述确认方案解析，未知过滤类型仍禁用。复杂 `FightStatusLogicComponent`／`FightSkillLogicComponent` 图、低频组件的逐字段行为、全量角色动作／特效资源绑定和正式技能战斗流程的 Play Mode 画面仍属于未完成项，不能以当前运行时入口声明全系统完成。

复杂状态逻辑已开始接入：`FightStatusLogicComponent` 的原始多态配置以规范 JSON 保留在每个状态实例，持久黑板值、初始化标记和触发计数写入检查点。`FightStatusLogicRuntime` 已接通状态自初始化／开始／结束、回合开始／结束、技能结束、施法命中、承受命中及命中后事件；当前按官方键解析 `LogicFilter`、概率、黑板比较、计数器比较、生命比例、状态类别与实体类别条件，并执行黑板整数／长整数／布尔初始化、计数增减／重置、触发技能／状态、状态叠层和状态移除。未覆盖的过滤器与处理器保持失败关闭并记录为未支持，不补造服务器语义；官方 `LogicNotFilter.TargetFilter`、黑板比较 `LhsBBKey` 与实体类别 `EntityClassIdFilter.ClassId` 已按反编译字段名解析；实体类别支持已绑定的主实体端口，状态改层／移除处理器内部另绑定正在遍历的状态；其他事件中的状态迭代、宠物、召唤及移除实体端口在完成对应事件绑定前标记为未支持，未知 OR／NOT 子条件、畸形条件和未知处理器不再误放行或继续执行依赖句柄。`scripts/test_battle_status_logic.ps1` 使用真实 24669 配置验证自初始化黑板、官方 NOT／黑板键／ClassId 正反例、未绑定实体端口（含 NOT 包装）、未知／畸形条件失败关闭及检查点恢复；召唤、条件命中、治疗和替身回归亦已通过。窗口化 Unity 实际 weekly 战斗已通过真实启动、24669 自初始化／开始、原始逻辑图与黑板检查点往返、胜利结果、结算按钮模拟射线点击及返回；已查看准备、原结果页和返回家园截图。模拟输入在 Play Mode 域重载后重新取得现有 Game View 引用，不操作系统鼠标／焦点。专项模式只验证状态与检查点，HP 动作专项现按实际生产原生玩家模型验证：完整原技能图拥有 Animator 叶节点时，本就跳过扁平场景队列；探针改为验证 24202→10170 与 13500 两个原始 `MyFightSkill` 实体启动、零延迟且不消耗个人回合，以及 `200/skill_41` 由真实渲染器回调推进、骨骼矩阵变化和实际可见网格，不再以旧 `HomePlayer` 或扁平队列判断原生角色。窗口化 weekly 已通过该组断言、胜利结算、模拟离开点击和检查点恢复，并已查看技能动作／绿色特效、原结算页及返回画面。这仅覆盖该技能与本次角色来源，不代表全部动作已验证。Unity 导入／编译及 `Zjcs.Editor.ProjectTools.Validate` 通过，最新静态检查通过（136,614 个 GUID）；返回家园仍可见空头像、黑色三角、白／灰矩形与角色缺失，完整视觉验收仍未通过。该段仅描述 24669 与 HP 动作专项；其余状态处理器仍待逐项接入。全部 15 个技能逻辑图、状态初始化／吸层及 Windows Player 的当前验证范围见本页对应段落；连段和随机替代的范围见上文。

幻兽 1620「诡术灵喵」的 50210 支援技能已按用户确认规则接入每段当前层数增幅：每段重新合计来源全部有效 24823，不消耗层数；原公式、暴击／格挡与减伤衰减后，护盾／扣血前乘以（1＋0.2×本段层数），伤害与 AbsoluteDamage 向零截断，DeltaDamage 保存绝对差。该数值修正不受原 CanTriggerChild=false 限制，实际创建子技能／子状态仍遵守原开关及白名单。`scripts/test_battle_skill_current_stacks.ps1` 已通过多来源、禁用状态排除、段间增减层重读、不消耗、整数取整、黑板差值、治疗／零伤害、护盾、损坏配置保护、真实施法与无层数对照及检查点恢复；13820 与 13830／13840 回归亦通过。原 16020→50210、24823 及全部 15 项图的所选 884 配置核验通过，静态元数据检查通过（142,553 个 GUID）。公共 weekly 4800 Play Mode 已通过原开始按钮模拟输入，实测原 9220 幼年幻兽的 skill_01 原动画与可见网格、原技能特效、独立支援槽的一次释放、原五层保留及随后新增层数、检查点回读、原结算页模拟关闭与宿主退出，准备、动作、原结果及返回截图已查看。该诊断本段实际扣血为 0，增幅数值另由真实 1620 支援路径的无层数／五层对照验证为两倍；该路径不占主人普通技能槽，释放计数与层数均通过检查点恢复。Unity 导入／编译、ProjectTools.Validate、完整 Windows 增量构建及实际 Player 专项均通过；Player 复验了原 9220 动作／网格与技能特效、一次独立支援释放、五层保留与随后新增层数、检查点、原开始／结算按钮模拟点击及退出释放，准备、动作、原结果和返回画面已查看。Player 的本段实际扣血同样为 0，数值增幅仍由上述规则对照证明。返回家园仍有本页已知视觉缺项，尚未正式发布。

技能 Windows 构建现增加原生 shader 程序域检查；副本 Spine UI 程序域登记修复见[副本页面](dungeons.md)。清理构建缓存后的完整 Windows 构建已通过，日志确认顶点、片元阶段均执行原六组程序筛选；54 项 shader 的 49,828 个 GPU 阶段域核验、Unity 导入／编译及工程校验方法通过。工程校验启动时仍出现 Burst 的 Assembly-CSharp／NaughtyAttributes.Core 解析异常，不能据此声称全工程无错误。

衰减能力现按 `IFightDamageFalloffRule.Apply` 拆为按目标累计与整次施法共同累计两种实现，按原状态 `Scope` 选择。原来源／伤害过滤、触发开关、概率、开始次数、最大次数、浮点乘方及向零取整顺序保留；每次施法的临时计数与无施法上下文时的状态计数仍按既有检查点结构保存。此为现有单机执行的模块拆分，不新增服务器语义。官方来源仍为所选 884 的 `FightStatusDamageFalloffComponentInfo`／`FightStatusDamageFalloffComponent` 和空客户端类；原公式与配置凭据见既有技能逻辑来源清单。入口为 `FightDamageFalloffRules.cs`。接口检查通过逐身体格累计、跨目标共同累计、起始／最大次数、同一伤害阶段防重复、独立爆炸施法计数、过滤先于概率及持久计数重置；完整战斗核心（460 份公式样本）、触发链、范围模式和 13740 地块爆炸回归通过。Unity 导入／编译、`ProjectTools.Validate` 及静态检查（142,649 个 GUID）通过。内置屏 13740 的 Play Mode 与重新构建的 675×1464 Windows Player 均通过模拟开始／结算关闭、原动作与网格推进、逐身体格伤害、两次地块爆炸、地块保留、一次普通释放及检查点恢复，十张实际截图已查看。Play Mode 动作帧出现青色块，Player 对应帧未见；两者均仍有顶部遮挡及返回家园视觉缺项，因此不视为完整视觉通过，尚未正式发布。

条件伤害缩放现按 `IFightSkillDamageScale` 拆为三种实现：13820 读取本次施法的持久充能快照，50210 每段重新读有效状态层数，13730 每段重新统计原范围的同阵营地块。各实现负责核对自己的原图及输入，返回缩放配置和份数；统一在既定的护盾前阶段执行取整与黑板输出，不改变原公式及触发开关。生产行为检查通过三类数值修正、护盾、黑板、检查点、异常图和瞬时加层回归；Unity 导入／编译、ProjectTools.Validate 与静态元数据检查（142,648 个 GUID）通过。内置屏 13730 实际 Play Mode 通过模拟开始按钮、原动作／特效、地块保留、一次释放、原结算关闭及检查点回读，五张截图已查看。新接口接入后的 Windows Player 已构建成功，随后在内置屏以 675×1464 竖屏分别运行 13820／50210／13730：原开始按钮及结算关闭均模拟点击，原动作与可见网格推进、一次释放、检查点回读通过；分别核对消耗五层充能、宠物支援独立次数及五层保留、三份灼烧地块及状态保留。三个进程正常退出，15 张实际截图已查看。此项为接口拆分后的 Player 回归，不代表全量技能或完整培养／地块获得流程均已验证。既有 Burst 程序集解析异常、顶部遮挡与家园视觉缺项仍在，完整系统与正式发布未完成。入口为 `FightSkillLogicRuntime.DamageScales.cs`。

子技能生成能力现按 `IFightSkillChildProvider.Collect` 拆分：地块爆炸收集器读取原 13740 范围，主人触发器读取 54370 的开始图，目标触发器读取 54373 的命中图；各自校验原处理器、黑板端口和概率配置，再返回明确的技能、目标／锚点及延迟，由执行器统一继承根技能培养、祝福与来源并调度，真正创建子技能仍遵守 `CanTriggerChild`。所选 884 的 `FightHandleCfgGetMasterEntityAndStore` 明确规定按 `MasterId` 查找在场主人，无主人或主人已离场时不写黑板；54370 因此触发原 54373，后者每次原 54374 命中节点执行时向 `SkillTargetEntity` 触发一次 54375，保留明确目标，不重新执行 AI 选点，也不按身体格重复生成子技能。原 `pet_evolution` 将 1620 的第二进化阶段、实体 9320 与主动技能项 16442 绑定，诊断使用这份来源身份；原 54371／54376 继续使用各自逐格 50% 概率、3320 地块、54372 状态、参照类型与 0.63 秒延迟；创建者、地块状态持有者及根技能源沿用已确认的地块规则。来源凭据见既有技能逻辑清单，入口为 `FightSkillLogicRuntime.ChildSkills.cs` 与 `FightBattleExecutor.SkillLogic.cs`。规则测试通过主人缺失／离场／零 ID、端口／过滤器异常、子技能开关、每次施法开始只执行一次、明确目标、原两级子技能链、两处地块、根来源、一次普通释放和检查点恢复；13740 爆炸回归通过。Unity 导入／编译、ProjectTools.Validate 及静态检查（144,772 个 GUID）通过。内置屏实际 Play Mode 已通过原开始按钮模拟点击、原 9320 实体模型与可见网格动作推进、两级主人子技能、灼烧地块、一次释放、检查点回读、原结果页关闭及返回；五张运行截图已查看。原 playable54370 发出 skill_03 参数，但官方 pet_9320_Controller_fight 将该参数路由到 skill_02 动作（状态 1102634108722742392），验证按此原绑定检查，未修改控制器。完整 Windows 验证版及当前脚本增量构建成功，正确 9320 身份的同一流程已在内置屏实际 Player 通过原开始／离开按钮模拟点击、两级子技能、地块、原动作推进、检查点及退出释放，五张运行截图已查看；顶部遮挡及返回家园视觉缺项仍存在，未正式发布，本项尚未完成完整验证。

技能逻辑的完整配置已接入：全部 15 份 `FightSkillLogicComponent` 原多态图按 `OnDoHit`／`OnDoHitTransient`／`OnSkillStart`／`OnHitFinish` 四个原字段保留到技能定义与检查点；恢复旧档时只补回缺失的不可变配置，保留原施法计数、冷却和命中图。独立规则回归覆盖全部 15 份图的保存往返、无逻辑技能和生产旧档恢复入口。已直接解码所选 884 原 EC 片段，核对全部 15 项技能逻辑实体的完整迁移组件逐字段一致，凭据一并保存在[技能逻辑来源](../../assets/reference/battle-rules/skill-logic-stack-source-manifest.json)；全部 15 项本体图现已按核实来源及用户确认的单机规则启用执行；各项的实际验证范围分别维护于本页，不能以图数量代替完整系统验收。Unity 导入／编译与 `Zjcs.Editor.ProjectTools.Validate` 通过，全量静态元数据检查通过（136,615 个 GUID）。50213 原配置 `HitList` 为空，是纯逻辑技能；现仅对此已核实本体开始图的实体放行空命中，其他未知空命中技能仍不放行。官方 `MyFightSkillLogic`、`FightHandleDamageScaleByScale` 与 `FightHandleTriggerSkillBySkill` 均无服务器执行代码；既有图的执行与验证范围见上文，13960／50213／50230／64450／13870／13970／13640 的能力与单机规则见下文；全部技能图的 Play Mode 与 Windows Player 行为仍未完成逐项验证。新增存档字段后的窗口化 weekly 回归已通过原生 24202→10170／13500 启动、`200/skill_41` 实际姿态／可见网格推进、胜利结算、模拟离开点击及检查点恢复，已查看动作、原结果页与返回画面；该回归只证明既有流程兼容，不能代替这 15 份图的执行验证。旅行入口仍有白块／洋红矩形，返回家园仍有空头像、黑三角、白／灰矩形和角色缺失，完整视觉验收未通过。13820 的 24031 开始施法读层／消耗、持久黑板快照及每层 7% 伤害缩放已按下文确认规则接入；当前验证范围见前述专项说明。剩余七份本体图已接入共享执行，并通过规则回归、下述窗口化 Play Mode 与实际 Windows Player 专项。包含 54370 复验与 13960 四种组合的本批共 11 条 Player 流程全部通过，已查看原战斗、结算与返回画面；生产战斗回归的 460 个官方向量、全量静态检查（144,801 个 GUID）、Unity 编译和 ProjectTools.Validate 均通过。24825／24772 等依赖状态的特殊事件图尚未完成，不能据此声称被动效果完整迁移。

伤害基准由 `scripts/prepare_battle_damage_reference.py` 直接编译 `884_156692` 的原 `BattleFormulaHandler.Damage`、原属性读取、位阶换算、大型怪物分类及调用辅助方法，配置仍使用工程固定的 `854_156383` 表；表查询、结果字段和关闭的日志由显式测试适配器提供，`Mathf.Approximately` 直接运行本机 Unity 引擎实现。460 组输入覆盖元素、角色与召唤类型、职业、等级／位阶、魔界位阶、特殊怪物增伤、固定伤害、暴击／格挡、目标生命门槛及组合属性；预期伤害、绝对伤害和元素标记来自原公式，不读取迁移公式输出。基准在 `assets/reference/battle-rules/damage-reference.json` 保存官方程序集、源码、配置、生成器和编译测试代码哈希，`test_unity_combat.ps1` 在比较前核对输入哈希；结算验证按命名基准读取原预期值，幻兽能量用例补齐原 16100 的 51000 技能实体并验证未装备时不扣能量。本批只恢复验证输入与测试前提，没有改动游戏执行规则或 Unity 资产；隔离工程静态检查通过（134,568 个 GUID），共享工程最新全量静态检查已通过，详见本页内嵌骨骼验证范围。原空的服务器自定义伤害阶段处理器保持为空，此基准不验证服务器条件触发或伤害结算规则。

24387 的回合间隔过滤按官方 `FightStatusRoundIntervalComponentInfo.FilterCondition` 的“传入 ApplyEntity”注释、`LogicNotFilter` 和 `EntityCharacterTypeFilter` 定义恢复：排除状态承受者的角色类型 8（Substitute），不检查状态创建者或替身关联角色。配置仍为 20% 快进，过滤在抽概率和本回合凭据之前执行，不匹配不消耗凭据；原开始回合的两个阶段共用一次记录。`FightBattleStatusRoundIntervalComponentState` 保存排除列表、定义标记、触发次数与已检查回合，复制不共享列表，旧档重建定义时保留两项计数；未知过滤仍禁用。入口为 `FightOfficialCombatCatalog.MissingComponents` 与 `FightBattleExecutor.MissingStatusComponents`。规则回归通过角色类型 0～9、创建者类型无关、实际行动条从完整间隔 3,162,278 缩短到 2,529,822、每回合仅一次快进，以及复制、JSON 和旧档计数保持。静态检查、完整 Unity 编译及 `ProjectTools.Validate` 通过；公共周常战斗的 Play Mode 和实际 Windows Player 均以模拟鼠标点击开始，验证普通玩家实际队列快进 20%、同回合不重复、后续符合回合仍可触发及 Unity JSON 检查点保留排除条件，并完成召唤创建／到期和结算回归。已查看两端运行截图；替身排除仅有规则测试，未声称该分支已在画面中实测。Player 复用本会话原生资源完整构建，仅增量重编本批脚本；返回主页仍存在头像缺失、异常图块及边缘裁切，不代表完整表现验收或正式发布。

技能范围查询使用目标全部身体格判断相交，修复仅查询中心格导致原 10660 无法命中相邻大型首领的问题。原 `GridGroundComponent.EnterPosSub` 将实体登记到所有 `BodyRangeInfos` 格，`GridTransformComponent.InRange` 同样使用身体范围。范围攻击的实际结算按用户确认的逐格规则执行，范围内的多个身体格分别命中，并继续遵守各技能原衰减和限制；明确目标的收集图（如 13640）仍按其原集合配置每个实体收集一次。具体命中能力及验证见本页上述多格范围说明。

官方 `MyPlayableAnimation`、`MyPlayableDefault`、`MyPlayableRenderSortLayer`、`MyPlayableLight` 和 `MyPlayableLight2D` 已迁入 StoryPlayables，保留动作播放、Transform 持续时间、Renderer 排序以及 Light／Light2D 强度曲线和循环行为；来源见[动作 Playable 来源清单](../../assets/reference/battle-animation/skill-animation-playable-runtime-source-manifest.json)。

官方 `MyPlayableFixFlipXTrigger` 已迁入 EntityLifecycle，保留按 source/target 选择角色并调用 `MyGridTransform.SetScaleByFlipX` 的触发行为；来源见[翻转触发器来源清单](../../assets/reference/battle-animation/skill-trigger-playable-runtime-source-manifest.json)。

官方 `MyPlayableFadeEffectSequence` 已迁入 StoryFollowBone，从原 UniTask 状态机恢复异步执行：先累加本帧 `Time.deltaTime` 再判断 `StartTime`，按序列化数组每帧最多推进一项并调用原 `MyEffectFadeOut.FadeOut(Duration)`，不排序或合并同时到期项。重复播放重置时间并取消旧序列，显式停止、最后一项及销毁取消令牌；单独停用触发器对象仍继续原异步任务。修复了先前 Coroutine 适配导致的首帧延后及停用即停止差异。隔离 Unity Play Mode 已使用官方波纹粒子通过首帧阈值、同时间项分帧、原数组顺序、重播重置、显式取消、停用继续、空效果引用及销毁取消，并通过既有十份移动效果图／17 个跟随节点回归；已查看实际原生粒子截图。完整生产技能和 Windows Player 对应淡出流程仍未验证；来源见[效果淡出触发序列来源](../../assets/reference/battle-animation/skill-trigger-fade-sequence-source-manifest.json)。

官方 `MyPlayableCameraShake` 已迁入战斗技能 Playable 程序集，保留可序列化的 `CinemachineImpulseSource`、默认偏移 `(0,0,-250)`、按施法者世界坐标定位和即时 `GenerateImpulse`，并返回原配置持续时间。尚未接到各战斗模式，生产视觉验证未完成；来源见[相机震动代码凭据](../../assets/reference/battle-animation/skill-camera-shake-runtime-source-manifest.json)。

官方 `ApplyOverrideToMaster` 已迁入：按 `UseSourceDataToOverride` 从原施法者或目标查找角色母体，再按 `OverrideSource` 替换 Playable 的 source 或 target；scope 通过显式 `IBattleMasterEntityResolver` 提供母体查询。公共控制器已将共享 `OfficialBattleSceneView` 绑定到技能图调度器，按当前检查点直接 `MasterId` 查找对应场景实体；缺失、已移除或模型子对象不补造来源，仍保留的死亡实体可提供母体。现有独立检查入口为 `OfficialBattleAnimatorPlaybackAcceptance.RunMasterBindings`；本批未重跑该入口，完整公共战斗和 Windows Player 的对应画面仍待验证。官方来源与范围见[母体覆盖来源凭据](../../assets/reference/battle-animation/skill-master-override-runtime-source-manifest.json)。

主线、日常副本、竞技场与幻想阶梯的技能等级、秘法属性和状态统一读取 `SkillProgression.SlotLevel(..., SkillContext().CurrentSeasonId)`，只合并基础槽级和当前赛季祝福；其他赛季不参与。沿用原 `PlayerSkllSlotDataExt.GetSkillSlotLevel`，不改变技能品阶、子技能实体或计算公式。

一、二阶六职业的被动属性成长由 `scripts/test_first_promotion_passive_growth.ps1` 对照反编译原公式验证：覆盖 47 项带被动属性组的技能（含内置项）、其全部配置人物位阶映射，品阶 1／2／3／5／10，以及等级 1／2／19／20、各属性表末级前一档／末级／超出末级一档的有效组合，共 65,800 组。参照代码直接提取所选 884 的 `BattleFormulaHandler.CalcSkillProps`、`ScaleProps`、`ScaleSrcProps` 与 `Formula.Long2Double`；数据适配独立读取运行载荷保留的 854 属性表，并从该版 APK 解码、核验原默认属性表哈希，保留不同来源身份。验证覆盖按顺序缩放及逐次整数截断、未配置缩放项过滤、末级封顶，以及返回字典修改后重新计算不改变配置；运行时与原公式一致，本项未修改生产规则。该规则测试不证明技能获得／装配界面、触发调度、状态生命周期、动画声音或 Player 交付；这些范围仍见各职业页。104 项来源与 228 次四向施法回归已复跑通过，静态元数据检查通过；本项只新增后台规则验证工具，未重新执行 Unity Play Mode 或发布构建。

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

状态图的 `FightHandleCfgChangeStatusStack` 已按下述确认规则接入 `FightStatusLogicRuntime`：使用独立的迭代状态上下文，保留主状态及其黑板，按原 `IteratingAgentStatusClassIdFilter` 和 `EntityKeyEqualsEntityFilter` 匹配实例；先核对所有候选条件，再执行变化，支持原乘数、层数上限、零层结束、实际消耗通知及无匹配时创建。主实体端口已绑定；宠物和图内自定义实体端口仍未绑定，遇到时停止依赖处理。规则回归覆盖自身／其他来源实例、多个匹配、实际消耗数、创建前层数初始化、默认／继承来源、上限、空集合及未知条件失败关闭，并直接执行未经改写的 24745→24748（60 层）和 24774→24752（100 层）自初始化图及检查点往返。处理器接入不代表所有包含它的图已可运行：其他伤害处理、宠物事件等前置节点仍待完成。

状态图的技能触发、状态触发和状态移除处理器明确区分单位端口与其他实体类型：当前只有已绑定的 MainEntity（1）可解析为持有者；主状态（2）、伤害实体（1023）、未绑定事件／图内端口、空端口均停止该流程，不再默认指向持有者、施法者或受击者。技能／状态触发在处理器自身的概率抽样前检查来源与目标单位端口；`UseDamagePos` 所需的伤害实体位置上下文尚未绑定，遇到时停止，不用单位位置代替。状态移除先检查过滤器与端口支持性且不抽随机，再按原候选顺序逐项执行实际过滤和结束回调；空集合也检查支持性，遇到不支持条件时不继续后续依赖处理。独立规则回归覆盖八类无效端口的 40 项来源／目标组合、随机流与依赖句柄不推进、伤害位置不可替代、空／非空集合中的未知过滤器、显式主实体端口正例，以及前一状态结束回调改变后一候选条件时的执行顺序。官方端口枚举、三组处理器配置及空服务器类见[状态实体端口来源](../../assets/reference/battle-rules/status-entity-ports-source-manifest.json)。

改层初始化、状态实体端口、状态生命周期与状态应用规则回归及 460 项原伤害公式向量通过；共享工程全量静态检查通过（144,914 个 GUID），Unity 编译及 `Zjcs.Editor.ProjectTools.Validate` 通过。完整内容 Windows 构建通过，并检查构建日志无着色器错误。内置屏窗口化 Play Mode 和该构建的实际 Windows Player 均通过公共 weekly 战斗的原开始按钮模拟点击、24745→24748（60 层）与 24774→24752（100 层）初始化、首个个人回合前生效、创建者／根技能／原技能项／品阶／等级／位阶保持、实际 Unity 检查点回读、胜利结算及原按钮模拟离开。两端各五张准备、技能、结算与返回运行图片均已查看；返回家园仍有本页已列视觉缺项，完整视觉验收及正式发布未完成。

状态图的吸收处理器已按确认方案接入：原 24774 在实际技能 64280 命中后，对本次伤害目标的 24750 快照求和，结束原匹配状态，将计数传给原乘数 10 的改层处理器。`SkillClassIdByEcIdKeyFilter` 的 `DamageSkill` 绑定到本段实际技能实体，根技能不同不影响该判断；其他未绑定技能端口失败关闭。HP 条件仅接受主实体和已绑定的本次伤害目标，未知实体端口在外层取反时也保持失败关闭。独立回归直接执行原图，覆盖多个来源实例、结束回调修改后续实例时的快照、无匹配清零、只统计不移除、默认不发送消耗事件、实际／根技能差异、未知条件与端口，并以生产 `FightBattleExecutor` 执行原 64280 的命中派发和检查点回读。原怪物组 3665 的技能 17392 指向 64280；原生公共战斗专项使用该原模型与技能图，隔离配置仅准备所需状态与属性，不代表完整原怪物技能组或自然遭遇已完成。

吸收项规则、状态生命周期和状态应用回归通过；静态检查、Unity 编译与 `Zjcs.Editor.ProjectTools.Validate` 及 Windows 构建结果同上一项初始化专项。内置屏窗口化 Play Mode 和实际 Windows Player 均通过原开始按钮模拟点击、实际 3665／64280 原生图单次释放、目标 24750 三层移除、自身 24752 从 20 增至 50、检查点回读、胜利结算及原结算页空白区域模拟离开，返回后战斗与结算窗口已释放。原结算页保留战斗对象，验证脚本等待并按实际射线寻找未被模型遮挡的原离开按钮区域。3665 行动条头像来自所选 884_156692 官方 map23 包的原 G_3665，经独立 `StatusGraphMonsterPortraitCatalog` 接入默认查询；保留原像素、Sprite 参数及纹理采样，Windows 使用 RGBA32，两端运行时引用与实际画面均已核实。两端运行图片已查看；返回家园仍有本页已列视觉缺项，本专项不代表完整原怪物技能组、自然遭遇、完整视觉验收或正式发布。

移动动作已开始接入原生实体：迁移所选 884 的 `MyFightActionSkillMove`／`MyActionBase`，复用原逐路径计时、方向参数、临时中间格镜像、末格镜像恢复、可靠轨道停止触发及 End 清理；保留 `FightActionSkillMoveComponent` 的原距离／速度／时长计算。222 项动作配置已逐项与所选官方片段比对一致，移动图和碰撞图按原实体 PPtr 恢复，不用名称推测替代。`IBattleMovementActionView` 区分原生动作与既有显示流程：原生动作负责位置、镜像及动作效果图，场景层保留时序和普通移动技能效果，避免重复插值或重复播放动作效果。绑定、方向快照和无碰撞寿命适配见[原生移动动作来源](../../assets/reference/battle-animation/skill-move-runtime-source-manifest.json)。222 个原动作实体、221 份移动图与 20 份碰撞图绑定已完成 Windows 原生转换，原 1,884 个技能及 2,260 个命中实体绑定保留。方向／连段／旧检查点回归和真实调度器的回退衔接、不同单位时钟、原时长及长帧跨节点检查均通过。Unity 导入／C# 编译、ProjectTools.Validate 与静态检查通过（145,555 个 GUID）；公共 weekly 4800 Play Mode 已通过 11510 常规距离和远距离普通接近两项实际流程，观察到原生移动中间位置、落点复位、动作结束移除、skill_55_2 循环至 skill_55_3 结束动作、原残影、一次施法与检查点，结算和模拟离开亦通过；12260 的原技能图、残影及结算回归通过，运行截图已查看。完整 Windows Player 构建及实际运行已通过：11510 远距离接近后的原生移动阶段、12260 原技能图和残影、13100 六段连招的独立角色动作与影像轨道均通过，三项的原界面模拟输入、一次施法、检查点、结算及离开流程通过，运行截图已逐项查看。本模块尚未正式发布；返回家园的既有空头像、黑色三角、白／灰矩形及角色缺失仍未完成视觉验收。未取得的碰撞信息仍待接入；普通移动实体的当前范围见下段。


受击强制移动已沿用原生动作能力：已结算的合法路径在动作创建时快照被移动目标的 Direction，之后路径转向不改写原图参数，FlipX 独立处理；旧档没有方向快照时保留原回退。角色格子旋转与技能／动作方向参数分离，保留已有原生格子的 Direction，新增代理使用官方 `GridTransformComponent` 的 Up 默认值；原移动 StartImpl 只修改 FlipX，不能把技能瞄准方向写回格子并在 End 时横转角色。规则回归覆盖击退、拖拽、侧移、原概率／抗性／条件、方向快照、检查点及旧档；连段、动作开始触发与真实图调度回归通过。Unity 导入／C# 编译、ProjectTools.Validate 和公共 weekly 4800 Play Mode 已通过隔离的原 13550→13553 流程：目标 Left 方向参数、独立镜像、原短轨迹中间位置、结束落点复位、模型旋转保持及动作移除；一次释放、检查点、结算和模拟离开通过，六张运行截图已查看。短轨迹诊断按固定帧步长采样，避免冷加载大帧跨过整条轨迹，验证结束恢复设置。完整资源构建及最新脚本增量构建已完成，内置屏 Windows Player 已通过同一受击移动流程，六张实际运行截图已查看；最新静态检查通过（145,650 个 GUID）。同一 Player 的远距离普通接近／11510 原生移动回归及 13100 六段连招回归通过，分别查看 8 张及 24 张实际截图；连招记录六段独立演出、原替代分支、一次装备释放和检查点，保留原分身外观与骨骼时钟。本项不代表全部受击路径、缺失碰撞信息或完整技能系统已经还原。

普通移动技能的完整原生实体恢复已扩展至原 `FightSkill/Move` 子树，共 22 个，原技能目录的 1,884 项保持保留。所选 884 的配置已逐项与现有运行配置规范化哈希核对一致；10 个实体拥有原 `myPlayablePrefab`，12 个原本为空，均保留原绑定，不补配其他图。`inspect_battle_skill_graph_bindings.py` 现在同时检查此子树，`test_battle_skill_graph_bindings.py` 验证完整身份集合、原空图和独立引用；资源验证入口另外检查完整实体的 `MyEntity`／`MyFightSkill`、脚本及实际对象引用。原 `FightSkillComponentImplement` 不含结束调度，寿命采用本页已确认的单机规则；资源与生命周期接入分开验证，当前范围见下一段。Windows 原生转换完成，新增 53 个资源及对应 `.meta`，原有素材输出逐项对比不变，目录现含 1,906 个技能实体；原命中及移动动作绑定保留。4 项来源／绑定测试、静态检查（145,638 个 GUID）、Unity 导入／C# 编译及 ProjectTools.Validate 通过；2,296 个图字段、2,226 个独立图 prefab 和 10 个依赖 shader 加载检查通过，没有缺失脚本。公共 weekly 4800 Play Mode 已复验远距离接近、11510 原生移动中间位置、落点复位与移除、循环和结束动作、原残影、一次释放、检查点、结算及模拟离开，9 张运行截图已查看。技能图能力诊断沿用分身诊断的同步 shader 编译和退出恢复，确保首次请求变体时捕获原材质；失败信息现在区分具体移动阶段。包含这 22 个新增实体的完整 Windows 资源构建已完成，随后更新最新脚本并实际通过上述受击移动流程；这证明新资源随验证版交付，不代表 22 个技能的全部使用入口和演出均已通过。完整流程及正式发布仍待完成，返回家园的既有视觉缺项仍保留。

普通移动实体现在通过独立能力接入：`IFightMovementSkillTiming`／`FightOrdinaryMovementTiming` 仅接受上述 22 个原配置，并检查无命中、无额外技能和无技能逻辑图的配置边界；普通 `CreateSkill` 仍拒绝未解析命中图，专用工厂只允许已核实的普通移动实体。角色原 MoveSkillId／BackMoveSkillId 生成移动事件时快照起点、方向、镜像和状态，合法路径结算后保存终点及两个独立计时，检查点保留这些数据；旧记录没有快照时沿用既有显示。接近后的主动技能按普通移动的原 ActionDuration 调度，实体移除按独立 Duration 调度，路径及动作 End 仍使用原公式与销毁延迟。`IBattleMovementSkillView` 负责原 `MyFightSkill` 创建／OnRemove，`IBattleMovementActionView` 继续负责原移动动作；原技能图自然结束，原动作延迟保留，不重复播放既有普通移动效果。规则测试覆盖 22 份原配置、竞争／零／禁用计时、完整 RealDistance／速度／DelayDestroyTime、创建快照和存档；真实调度器测试覆盖先建技能再开动作、非装备槽及同单位连续路径。原动作开始、击退和连段回归通过。Unity 导入／编译、ProjectTools.Validate、静态检查（145,652 个 GUID）和公共 weekly 4800 Play Mode 已通过：15300 非装备槽、先于 15301 创建、独立回收，随后 11510 的原移动／循环／结束动作、残影、一次装备释放、检查点、结算和模拟离开均通过，9 张实际截图已查看。规则结算、场景位置、原生动作和效果共用连续路径的开始边界，保留每次原前置延迟，最终 Play Mode 复验通过。Windows 脚本增量构建及内置屏 Player 同一流程已通过，9 张最终实际运行截图已查看；13100 的 Player 六段演出、一次装备释放和检查点回归通过，23 张截图已查看；13550 受击移动 Player 回归通过并查看 6 张截图。当前仅接通既有普通接近／退后入口；固定速度／特殊移动入口、普通移动技能的共享规则开始／结束通知与相关状态反应、22 个原角色各自完整演出仍待接入或逐项实测，不以本项声明完整普通移动技能已还原。

首次转职闭包的 83 个状态原 prefab 及完整依赖已迁入 `Official/Combat/FirstPromotionStatuses`，生产角色状态目录使用 `Combat/first-promotion-statuses`；地块与秒制状态继续由各自原时钟接管。31 个原空包装组件及原持久护盾／绝对免伤客户端组件保留原分支，演出从不可变快照读取护盾最大／剩余值与触发次数，耗尽时先通知原组件，再退场，不重复执行业务 End。来源见[状态素材](../../assets/reference/battle-animation/first-promotion-status-assets-source-manifest.json)与[原状态组件](../../assets/reference/battle-animation/first-promotion-status-runtime-source-manifest.json)。当前 Unity 导入／编译及 83 个原状态目录加载通过，原暗击实战 Play Mode 通过，14 张运行画面已查看；护盾部分吸收、溢出、消耗、零值通知与绝对免伤次数先更新再移除的规则回归通过。全部 83 个原层级的脚本绑定与 79 个角色状态的原生 Start／Update／End 隔离演出已通过 Play Mode，172 张画面全部查看，包含 24175／24435 的原护盾耗尽演出；地块 11582／11632 与浮空 24269／24270 保留专属宿主。本探针将护盾值设为 100→0、绝对免伤触发次数设为 1 并增加叠层，以检查视图事件；延长隔离战斗上限为 2048 回合，不修改正式规则，不等同于全部技能实战触发。原 MyAction 的实体查询与播放中心已显式绑定，避免状态结束特效脱离原实体。规则回归、Unity 导入／编译、ProjectTools.Validate 与共享静态检查通过（151,872 个 GUID）；1,466 份输出在仓库及导入后的隔离工程均与凭据逐字节相符。Windows 全状态验证入口为 `scripts/test_first_promotion_skill_player.ps1 -DarkStatus -AllStatusViews`；完整原生构建通过 60 个 shader／55,036 个 GPU 阶段检查，四方向暗击与右向 79 状态原生 Start／Update／End 实机回归通过，214 张运行画面已查看。召唤物出生附带状态曾先于其首次施法绑定原角色实体，10712 因缺少原 MyAction 所属实体而失败；现由状态身体解析入口复用 `OfficialBattleSkillEntityPlayback.EnsureUnit`，在原状态动作执行前绑定同一个原角色实体，不等待召唤物首次攻击。修复后的技能图调度回归、Unity 编译、`ProjectTools.Validate` 与 Windows 脚本构建通过；实际 Windows Player 又通过岩灵 10019、图腾 10025、双火兽 10058 的出生附带状态、各召唤物最后一次原攻击、自然退场、原技能回收、磁盘回放及右向 10029 人物施法。合计八组通过的 Windows 流程、279 张实际运行画面均已查看，进程正常退出；失败版本的两张画面也已核对。完整资源构建实际处理 Noise 两个 pass 的四个阶段与 SpriteSequence 两个阶段，原程序均保留；最新共享静态检查通过（151,881 个 GUID）。最终受测工程的 1,480 份相关 Unity 文件已与提交逐字节核对一致；版本已发布到 `D:/Desktop/ZjcsGame/Zjcs.exe`，7,019 个交付文件大小及其中 361 个程序／原生资源文件的 SHA-256 与受测构建一致。桌面快捷方式不带验证参数，保留用户存档。此轮仅完成该状态目录、视图事件接入及列明的实机范围；暗击的完整场景／进程恢复见下段专项，其余专属状态流程及全技能完整迁移仍未完成，返回家园仍有既有空头像与色块。

## 自定义实现

用户要求技能采用能力组合：按多段时序、范围命中、衰减、子技能、状态、地形及动作等职责设计行为接口，由具体能力读取各技能的官方配置；特殊规则由独立实现扩展。基础伤害公式继续复用官方计算。现已拆分的能力及验证范围见上文，其他职责的接口迁移仍待完成。

用户已确认移动动作的两项单机约定：自身技能移动在动作创建、路径结算前快照当前已结算的技能 Direction，保留原固定方向；普通接近／退后各自快照创建时的单位 Direction，不从路径重新推导该参数。FlipX 独立使用已确认的原规则，之后单位转向不改写动作快照，旧检查点缺少快照时保留既有显示回退。APK 的 `MyFightActionSkillMove.GetPlayableParam` 只消费服务器填写的 Direction，填充规则缺失。用户进一步确认：受击强制移动在动作创建时快照被移动目标当时的 Direction，之后不按路径转向；FlipX 仍独立沿用原规则，旧档缺少快照继续回退。路径、概率、抗性和落点保留各自原配置及已确认规则，未取得的路径或碰撞信息不补造。对已结算合法路径且没有碰撞信息的上述自身、普通及受击移动动作，按原公式计算移动总时长，再加原 `DelayDestroyTime`，到期执行原 End→Remove→回收；总寿命为零时立即结束，不补造碰撞。原共享类保留总时长公式及延迟字段注释，但 APK 没有自动结束调度，结束时机按本次确认执行。

用户已确认仅对 22 个普通移动技能采用独立生命周期：技能实体在原移动动作开始前创建，使用非装备槽，不额外扣冷却或次数；固定、AllEnded、AllRelevantEnded 中所有非负结束计时独立生效，取最先到达者。AllEnded 以移动动作 End（包含原 `DelayDestroyTime`）为起点，动作占用时长与实体寿命分别计算；实体移除调用原 `MyFightSkill.OnRemove`，原图自然结束。本规则包括 15300 的固定 0.7 秒与移动结束后 0.1 秒竞争、15320 仅在移动结束时结束，不扩展到其他技能。其计时能力、普通接近／退后原生实体接入及当前实际验证范围见上文；其余入口与共享规则通知仍未完成。

用户已确认三项技能本体图的限定例外：50213 的开始图向原技能目标（主人）施加 24825；50230 的命中图按原主人端口触发 50235，每个原命中节点仅执行一次，不按身体格重复；64450 的瞬时图在目标不满足原 24773 层数条件时，按原 50% 概率施加 24772。即使这三项原 `CanTriggerChild=false`，仍执行各自本体图明确配置的效果，继续核对原条件、目标、概率和来源；后续状态反应、命中配置的自动子技能等继续遵守原子技能开关，不扩大豁免。官方配置与端口说明完整，服务器执行缺失，此为用户确认的限定单机差异；已按开始效果、瞬时效果和主人子技能能力接入执行。生产规则检查通过纯逻辑 50213 只施加一次原状态、50230 多身体格仅一次原回血、原延迟／来源／转盾组合、64450 三个原节点的 50% 随机流和状态条件，并确认自动子技能仍被原开关阻止。窗口化 Play Mode 已在受控战场按原开始／离开按钮完成三项本体图验证：50213 使用原 9220 同格主人锚点，仅附加一次 24825；50230 使用原跟随实体 9222 的 skill_01 动作，多身体格伤害仅触发一次原 50235 主人回血；64450 执行三个原标记节点并实际施加 24772。单次普通释放、来源培养、Unity 检查点、原结算页与返回均已核对并查看运行截图。9220／9222 的 FightRolePetComponent、角色类型 5 及幼年支援技能映射已与所选 APK 原实体／pet_evolution 表逐项核对；9222 的原渲染节点名为 PetModel，验证使用实际骨骼载荷身份判定。该受控单图测试未接入真实宠物跟随行动或实体化流程，不能代表完整宠物 HUD 验收；上述三项本体图也已在实际 Windows Player 通过相同的受控原模型、原按钮模拟输入、动作／命中、主人附加效果、来源、单次普通释放、检查点、结算与退出释放检查，各流程五张运行截图已查看。24825 的宠物实体化变更及 24772 的层数变更后续图尚未迁入。

用户已确认 50210 的单机规则：每段结算前重新合计自身全部有效 24823，不消耗层数；完成原公式、暴击／格挡、减伤和衰减后、护盾与扣血前乘以（1＋0.2×本段层数），伤害与 AbsoluteDamage 向零截断，DeltaDamage 保存新旧差的绝对值；零伤害仍为零。这是本段数值修正，不受子技能开关限制。

`FightHandleChangeStatusStack` 的服务器执行类在官方 APK 中为空，17 项状态图记录保留了过滤器、变化值、乘数、创建和消耗通知字段。用户已确认单机规则：遍历目标的有效状态，按原过滤器匹配，每个匹配状态按黑板值乘以原 `Multiplier` 改层，遵守原层数上限，减到零即结束；开启 `InvokeConsumeEvent` 时仅对实际减层的状态通知一次，携带实际消耗层数。无匹配且创建 ID 与变化量均为正时，按该层数创建，默认继承当前主状态的创建者、根技能源、培养、替换参数与祝福；`UseDefaultSourceInfoOnCreate=true` 使用无来源。未知过滤器或未绑定实体端口不修改状态并停止依赖处理。

`FightHandleAbsorbStatusStack` 的服务器执行类同样为空，原 24774 保留目标端口、状态过滤器、移除开关和输出端口。用户已确认单机规则：技能条件检查本段实际技能 ID，遍历本次伤害目标的有效匹配状态，先快照当前层数总和并写入原黑板，无匹配写零；`RemoveStatus=true` 结束匹配状态，原配置没有消耗通知字段，因此不额外发送消耗事件。未知过滤器或未绑定端口不修改状态、不写输出并停止依赖处理。

用户已确认 `FightStatusHitApplyDamageComponent` 的基础单机规则：每段结算前按原伤害／治疗类型、目标护盾、角色类型、技能 ID 判断，仅把该状态原属性中 `AddPercentPropType` 对应值加到本段属性、`ReducePercentPropType` 对应值减去，其他属性及命中不受影响；充能遵守原门槛、指定技能和数量。60230 限定目标有效护盾，60240 限定召唤物，替身按原 `ApplyTargetCharacterTypeIfSubstitute` 查关联对象类型；24079 对 12860／14070 消耗 24031 的 5 层。入口为 `FightConditionalApplyProperties`，由同一施法上下文接入结算前属性。固定版本共 46 项，其中 38 项基础配置、3 项失血倍率及 4 项技能标签过滤已接入；25098 已接入地块计数与回合结束门槛，规则执行器已生成独立灼烧地块；公共场景的完整原生绑定和跟随父释放节点移除的行为仍未完成。独立回归覆盖护盾耗尽、角色类型／替身、指定技能、减属性、充能、真实护盾吸收前的伤害增幅、临时属性隔离与旧检查点定义恢复；本批已通过 Unity 导入／C# 编译、`Zjcs.Editor.ProjectTools.Validate` 和公共 weekly 战斗的模拟点击；Play Mode 实际执行了 60290 条件增伤并完成胜利结算。实际 Windows Player 也已执行 60290 条件增伤并完成上述持久化与胜利检查；其他条件仍仅完成独立规则回归，返回家园后的视觉缺项见本页验证范围。

用户已确认 60270／25135／25141／25282 的 `SourceEntityGameplayTagFilter` 单机绑定：以本段实际命中技能实体读取官方 `FightGameplayTagComponent`，子技能使用自身实体，不以根技能代替；查询复用已迁移的 `GameplayTagQueryExtension.IsMatch`／`GameplayTagExtension` 四字位运算，按原 All／Any／Without 组合检查，不猜测补标签。查询词和掩码随状态深复制及保存，旧技能检查点缺少标签时从原实体配置恢复。回归覆盖原 8192／4096／2048 标签、四项配置逐项匹配与排除、子技能与根技能区分、缺失标签拒绝及副本隔离。

用户已确认两类本段属性修正（`HitApplyDamage`／`HitDmgAddPer`）不受 `CanTriggerChild`／`CanTriggerStatusEntityIds` 限制：它们只修改当前命中属性，不创建子技能／子状态，仍遵守各自过滤、充能和次数。官方 `FightSkillComponentInfo` 对该开关的注释限定为触发子技能或子状态；25141／25282 匹配的原技能均关闭此开关。真正创建子技能／子状态的触发继续保留既有门禁。

原生技能、命中与移动动作经同一 prefab 实例适配器绑定 `ApplyByGroundType.Host`，复用当前战场 `OfficialBattleGridMapper` 与公共 `OfficialBattleGroundEffectLibrary`，在原 `MyPlayable.Init` 前完成注入；不修改原地表选择与特效生命周期。此绑定已通过海之宫 Play Mode，以及当前世界之树、海之宫和中途退出的 Windows Player 实际战斗流程；机神山末关另暴露下述公共骨骼附着异常，不能据此前述范围声明整个公共技能演出完成。

静态地块本轮补齐原 `3322→3050`：所选 884 的 3050 没有 `FightGridMoverComponent`，其 `FightPropComponent` 为空；原 `17880→17882` 生成该实体并附加 17883。用户确认沿用公共独立快照，复制 `ReferenceType` 参照单位当时的最终属性、等级、位阶及阵营；没有移动组件时不限制地形，仍检查战场边界并保留原阻挡与覆盖配置。目录显式保存移动组件是否存在，不把缺组件误当作禁止全部地形的零掩码。规则回归通过原 17880／3322／3050／17883、非灼烧地形、阻挡／越界、快照隔离与冷恢复，海之宫原失败检查点亦在创建后重开并正常完成；海之宫遗迹已在窗口化 Play Mode 与完整 Windows Player 走完 8 个原房间、6 场真实战斗、结算领奖与冷打开，并查看实际战场、开箱、已领取和首通伙伴画面；正式发布路径仍待验证。

地块的 Unity 读档检查发现并修复了共用检查点问题：`JsonUtility` 将空内联 `VoodooDoll` 字段还原成默认对象，旧的非空判断会把普通角色误认为替身。`FightBattleUnitState.IsVoodooDoll` 统一按有效关联目标 ID 判断，行动、增益限制、伤害／减益传递、触发过滤和胜负统计均使用同一判定，不增加存档版本或丢弃有效旧关联。小型 Unity 工程已通过实际 `JsonUtility` 写入／恢复后的地块到期和死亡移除；独立回归同时通过真实替身的原 12200／4600 创建、溢出伤害传递、护盾绕过、增益拒绝、减益转移及存档，条件命中回归亦通过。本批已通过完整工程 Unity 导入／C# 编译、`Zjcs.Editor.ProjectTools.Validate` 及静态元数据检查。公共战斗的新地块读档流程已通过下述 Play Mode；3051／13052 的 Windows Player 恢复范围见上述公共验证。

用户已确认 `FightStatusHitDmgAddPerComponent` 的条件增伤单机规则：每段命中结算前检查原来源、伤害类型、元素、目标 HP／状态条件和概率；满足后仅为本段临时合入该状态原属性，按配置消耗层数和更新触发计数。持续次数按符合 `OnlySkillActions`／`OnlySkillType` 的一次施法扣一次；`UpdateDurationSkillCountIfTrigger=true` 时仅成功触发才扣，剩余次数保留到检查点。固定版本共 29 项此组件，均以持有者作为命中来源；60280 限定目标 Action 52／57，60290 检查目标减益，24085 消耗 24058 层数。`FightConditionalHitProperties` 使用每次施法独立的上下文和本段属性字典，原技能排序分类取 `skill.SkillSortTypes`，不把 `SkillSortType` 当作 `SkillType`。独立规则回归已覆盖目标动作／减益、属性隔离、多段施法次数、充能倍率、真实伤害增幅及条件消失后的恢复、双段治疗和检查点恢复；Unity Play Mode 已通过公共 weekly 战斗中 60290 的目标减益门槛、实际伤害触发与最终胜利；实际 Windows Player 同一条件命中流程亦已通过；全量正式演出与返回家园后的视觉验收仍未完成，不视为该组件完整完成。其他尚无配置实例的来源类型及逻辑标签不推测执行。

实体被动装配会读取官方 `DeactiveEntityPassiveSkills`，初始未激活的实体被动不合并属性或状态；其余实体被动按宠物已保存品阶、等级和持有者位阶传入实体技能源。

幻兽能量释放先写入根技能施放事件，再执行命中图、子技能和状态触发，保证战报与动画时间轴按根技能到派生效果的顺序消费。

用户已确认 `FightStatusHpIncreaseUnitComponent` 的单机规则：累计实际恢复的 HP，溢出治疗不计；每满配置 `UnitHpPer × MaxHp` 触发一次并保留余量，遵守总次数和单次 HP 变化次数上限，被单次上限限制的余额留待下一次正恢复处理。`TryAtStart` 仅初始化零余额，不把初始生命当治疗。治疗命中、吸血、复活与属性重算导致的实际 HP 增加经独立通知进入该累计器，嵌套恢复按各次变化排队结算。余额与计数写入战斗检查点；入口为 `FightStatusTriggerRuntime.HpIncrease` 和 `FightBattleExecutor.HpIncrease`。

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

移动灼烧、火命中、吸血、暗击四项（详见对应职业详情页）的十八条 Windows 流程使用已发布原生资源及对应战斗代码，仅叠加隔离站位修正；162 张运行截图全部已查看，进程均正常退出。该隔离工程的静态元数据检查（150,483 个 GUID）、Unity 编译及 `ProjectTools.Validate` 通过；最新共享提交代码另通过四项秘法规则与 228 次六职业四向施法回归。本轮修改限于诊断站位与验证文档，后续共享战斗变更的完整原生流程仍须在相应功能中复验。

- 本批 Windows 严格全资源构建已在共享验证工程和提交基线叠加本批修正的隔离工程中成功退出（退出码 0），原 GlobalGameManagers 写入断言未复现。原家园果树 shader 的完整稀疏程序域登记及仅留作来源、全平台禁用的 APK DLL 本地文件名与迁移 asmdef 重名问题已修复，原 shader、DLL 内容和 GUID 保持不变。共享工程已通过 Unity 导入／编译及 ProjectTools.Validate，当前授权可用、D 盘空间充足。构建保持原错误检查；发布输入采用上述隔离工程，三个浮空技能的四方向实际 Player、画面和检查点验证已通过。成品入口为 `D:/Desktop/ZjcsGame/Zjcs.exe`。本批交付包含共用施法镜像及两处原生动作调度修正，其他技能效果与完整界面的缺项继续按本页维护。

- 日常副本机神山当前完整 Windows Player 在末关房间 2115／战场 30115 的公共演出中失败：原 `ApplyByBone.OnPlayableStop` 空引用；最近行动为怪物 3451 的原技能 18730。所选官方 `playable18730` 从施法来源读取 `MonsterModel_3451` 与 `rotate_01`，当前公共完整骨骼目录未包含 3451，不能用吞掉异常或关闭原特效替代原始模型附着绑定。需恢复并核对相关模型／附着入口、修复后重跑末关及完整路线；目前仅确认失败与原资源依赖，未将推测的根因作为修复结论。


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
