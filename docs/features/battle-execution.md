# 战斗执行

[总索引](../README.md)

状态：未完成。通用战斗数据模型、部分官方公式、离线执行器、检查点、HUD 和演出已经接入；原版战斗的完整规则覆盖、逐事件调度、资源闭包、运行验收和 Windows 版本发布仍未完成。

## 功能简介

本页只维护所有战斗共用的会话、阶段、准备、格网、单位、行动队列、技能、命中、伤害／治疗、状态、召唤、胜负、HUD、战报、演出和检查点。具体地图、剧情、任务、掉落、活动入口、养成事务和玩法奖励不属于本页。

“完整还原”表示当前 Unity 代码和资源已经覆盖并完成相应检查；“自定义实现”表示客户端或共享代码没有提供服务器裁决，按已确认的单机规则补足。配置存在、资源已提取或旧引擎测试通过，都不能单独视为通用战斗完成。

### 官方来源与证据边界

原版机制以 [official-source.json](../../official-source.json) 选择的 `871_156598` 客户端／共享反编译代码为主，入口见[当前代码清单](../../.tools/source-packages/current/871_156598/code-manifest.json)。现有战斗配置和部分资源仍来自 `871_156598`，对应[历史反编译目录](../../.tools/source-packages/official-code/)、[历史代码清单](../../assets/reference/official-city/code-manifest.json)、[战斗规则](../../assets/official/battle-rules.json)和[技能规则](../../assets/official/skill-rules.json)。不同版本只按各自来源使用，不混合推断。

客户端能够证明消息消费、数据结构、共享公式、界面和部分生命周期，不能证明服务器如何生成全部步骤或裁决所有结果。`MyFightAI`、部分 AI 组件、随机命中和若干状态触发只有壳或配置；`BattleFormulaHandler.InvokeDamagePhaseProcessor` 为空；部分胜负组件只有标识。因此本页明确区分官方证据和离线补足。

战斗预览对象在编辑模式校验结束时使用 `DestroyImmediate` 清理创建的模型网格、材质、贴图、HUD、挂载父节点及临时 SortingGroup；运行时仍使用 `Destroy`。已有地图角色在释放临时父节点前恢复原父级、姿态及动画，原有 SortingGroup 仅恢复状态。该处理不删除导入资产，避免 `ProjectTools.Validate` 结束时留下编辑模式销毁错误。

公共表现上下文 `OfficialBattleHostPresentation` 捕获调用方相机及可见根节点，战斗前台显示时暂停主城更新并隐藏原场景／HUD，退出或转入后台时恢复原相机投影、姿态和显隐。`OfficialBattleCameraView` 消费原 `GameCameraIniter.Adaptor` 的宽高比和尺寸，并复用已安装的 Cinemachine 及原群组取景字段；256个 GameId 的绑定由 `prepare_battle_camera_bindings.py` 可复现提取，来源见[相机清单](../../assets/reference/battle-scenes/camera-source-manifest.json)。日常、竞技、阶梯、技能测试及主线入口共用该相机实现。日常副本已实际执行至结算并检查新截图；玩家模型的额外1.25倍本地系数已移除，独立 Windows Player 验证显示官方0.3模型比例与脚下血条居中对齐。日常、竞技、阶梯的实际结算与主城恢复集成均已通过，竞技守卫职业服装和头发层级也已在实际战斗截图核对；Map10 已通过共用镜头下的准备／播放、速度与聊天模拟输入、最终命中等待、结束返回及原相机／角色层级恢复，七张实际运行截图已检查；技能试打也已在共用镜头与原生主角模型下完成十回合、结算、玩家／技能统计、技能快照与返回设置，十张新截图已检查。相机手动拖拽／缩放、原镜头偏移混合及全部场景画面尚未验收，不能据此声称完整相机迁移。

场景行动条从公共表现上下文显式取得本场输出相机，覆盖 HUD 立即挂载及延迟挂载路径；不再依赖主城未设置的 `Camera.main`。原 `GameRoundScene` 的屏幕锚点转换、相机尺寸缩放和 prefab 坐标保持原计算，适配仅补足相机引用。位置回归在渲染帧末比较原 HUD 锚点与行动条实际投影，避免在 Cinemachine 本帧更新前读取旧位置。日常、竞技场和塔层的准备／播放投影检查均通过，已检查其实际画面；Windows Player 技能试打的行动条位置也已核对。

Map12 私有日常副本通过 `BeginPrivateMapRoom` 显式传入同一表现上下文，公共模块接管地图相机与宿主场景／HUD 的隐藏和恢复；私图活动期间暂停地图镜头的 `LateUpdate`，避免地图跟随覆盖战斗取景。表现上下文支持额外的原生 HUD 根节点，主线及私图入口均显式传入独立 `WorldCanvas`，避免地图小地图／任务栏叠加到战斗 HUD。Map10 的独立 HUD 显隐及返回恢复断言也已通过。Map12 已通过模拟地图／路线输入、12102及四个后续房间、相机／角色／HUD 恢复与存档冷读，已查看完整450×800战斗和返回截图；具体范围见[山之国](../maps/map-12.md)。

原生战斗资源的自动导入不再覆盖已转换的显式绑定；实际强制重导入官方图集和人物 prefab 后，图集文本、Atlas、材质和 prefab 四份载荷逐字节保持一致，且无导入错误。转换维护方式见[素材管理](../asset-management.md#android-资源迁移到-windows)。动态字体构建保护与原0×0图集的处理范围见[职业字体](professions.md)。

修正玩家实体3100的战斗比例后，在独立隔离目录构建并实际启动 Windows Player；以675×1463窗口完成十回合技能试打、结算动画、玩家／技能统计、技能快照和设置返回，模拟点击流程及 `result.json` 均通过。十张实际运行帧均为675×1463，已查看战斗、结算与返回画面，主角恢复官方0.3模型层级比例，脚下血条居中对齐。正式存档未被测试流程使用，本验证 Player 未发布到正式目录。Editor Play Mode 的共享日常／竞技／塔层入口回归目前未通过：行动条锚点断言将3840×2160物理屏坐标与675×1463战斗投影比较，故该 Editor 结果不能替代 Player 画面；测试捕获到的技能面板同样因该坐标错配而被裁在画面边缘。编辑器 Player 构建流程的技能面板测试也曾在另一次运行因原开场淡入断言失败，该轮没有进入战斗。当前候选的 shader 筛选与Windows D3D 独立阶段验证见前文，均不表示全部战斗资源、入口或玩法通过。正式 Windows 入口为 `D:/Desktop/杖剑传说/杖剑传说.exe`；主城仍有异常几何与环境显示缺项，技能试打通过不表示全部公共战斗入口完成验收。

## 原始官方实现

战斗以官方 Fight 共享实现、实体组件、技能／状态配置及客户端表现为依据；公式、执行阶段、目标选择、冷却、护盾、状态与结束链保持原顺序。客户端与共享代码不包含的服务器统计、合并及结算规则按本页已确认的本地裁决接线。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

战斗准备区使用当前 `871_156598` 官方输入，`scripts/prepare_battle_preparation.py` 先核对 `Assembly-CSharp.dll`／`Console.dll` 解码哈希及反编译来源，再由 APK 优先、当前云端校验缓存其次解析 `prefabs_ui_fight_changebattleposarea` 和角色3100拖拽组件引用；解码9266条阶段范围与阶段类型偏移，恢复128×128原 Sprite、九宫格、PPU、SpriteRenderer、Sprites/Default材质、2.5秒原透明度动画及原玩家碰撞体。载荷为 `unity/Assets/Official/Combat/Preparation/`，原生绑定为 `unity/Assets/Game/Resources/Combat/Preparation/`，输入对象与输出哈希见[准备来源清单](../../assets/reference/battle-rules/preparation-source-manifest.json)。只恢复已取证的组件，未用替代素材补齐拾起／放下、高亮或NPC交互；实际验证范围与缺项见[战斗执行](battle-execution.md)。

战斗 Boss HP 层数表单独从固定 APK `871_156598` 的 `defaultpackage_assets_config_luban.bundle` 对象 `Assets/Config/Luban/monster_hp_plies.g.bytes` 解码，164 行输入 SHA256 为 `66b6496732f115b7370bc4fa3114d76c66f552de1fa73f70c06fa59b485e342e`。`python -B scripts/prepare_unity_combat_hp_plies.py` 通过固定版 `TbMonsterHpPliesInfo` 构造器生成 `unity/Assets/Official/Combat/monster-hp-plies.json`；输出、APK 对象、bundle 和转换版本记录在 [Boss HP 层数来源清单](../../assets/reference/official-map/monster-hp-plies-source-manifest.json)。

`prepare_fidelity_battle.py` 从固定 871_156598 的原始表和 config_ec 转换 7 张表、45 个怪物等级属性类、7,417 个原实体，共 139 类组件，当前解码无失败且严格核对全部组件字节。包含 471 种怪物及 2,699 组技能／属性配置。原有同 ID 的旧格式实体优先使用相同客户端内的正式配置，选择、跳过范围、原始来源、格式化器及哈希均保留在 [battle-rules/source-manifest.json](../../assets/reference/battle-rules/source-manifest.json)。运行数据为 `assets/official/battle-rules.json`，纳入显式资源清单。

、 移植原伤害公式与怪物属性计算。`generate_battle_reference.py` 编译原 C# 方法生成 97 组伤害及 8 组怪物属性对照，只去掉日志并提供配置读取适配；输出和源码／校验程序哈希见 [damage-reference.json](../../assets/reference/battle-rules/damage-reference.json)。追加 [secondary-reference.json](../../assets/reference/battle-rules/secondary-reference.json) 的 72 组原 C# 治疗、效果命中、生命消耗、护盾换算等对照；副本执行器尚未接入完整实体流程；素材／数据转换和独立公式通过不代表实际战斗已经还原。没有新增自绘素材。

`prepare_fidelity_battle.py` 补入固定 `871_156598` 的 `FightCharacter_part1.g.bytes` 和 `FightMonster_part1` 至 `part21`，与既有 FightSkill／FightStatus／FightPet／FightOther 使用相同原生成格式读取器。运行 `battle-rules.json` 包含原角色体积、旋转中心、移动配置、角色类型和初始状态；完整输入对象、版本、APK／校验 CDN 来源及输入输出 SHA256 见 `battle-rules/source-manifest.json`。此前转换范围与原输出哈希保存在 `battle-rules/history/source-before-character-monster.json`，不将既有未转换实体记为官方缺失。没有新增自绘素材。

`prepare_fidelity_battle.py` 增量转换固定 manifest `871_156598` 的原 stage_npc、npc_level_detail、npc_profession_detail、npc_template、FightNpc 及幻兽额外属性。battle-rules/source-manifest.json 记录输入对象、真实APK/CDN来源及输入输出哈希，前一来源收据保存为 battle-rules/history/source-before-npc.json。

`python scripts/prepare_battle_presentation.py` 从固定原男角色 SplitBinarySO 提取216段100／200战斗及配套动作，复用当前官方可定制皮肤、贴图和原始染色遮罩，采样为本地原生网格帧；4,672段怪物／NPC／召唤体动作复用已登记原模型骨骼、皮肤和贴图。原技能预制体 AnimationClip 覆盖名称用于绑定动作。当前初始剑／杖仍使用既有官方位图及网格，按原pWeapon_R2骨骼相对已有地图站姿变换接入；这是本地挂点适配，不是新绘素材或新的官方原武器网格。逐对象来源及输出SHA256见 official-map/battle-presentation-source-manifest.json，既有模型／角色来源收据哈希保留。

`python scripts/prepare_battle_effects.py` 按原 MyPlayableSequence 时间、FollowBone／Track 挂点和 ParticleSystem／材质转换技能及伤害粒子资源。3,543组绑定、4,226组粒子配置复用原资源和通用粒子转换器；原版本缺包依赖通过独立校验的新版同CAB资源解析，真实864_156496凭据与原版本来源分开保存在 official-map/battle-effects-source-manifest.json 的 counterparts 字段；未填入或改写原版本缓存。声音保留原Wwise事件引用，尚未将其等同于已转换音频。上述来源收据记录实际输出和缺失依赖，运行只使用本地转换后的显式资源。

`prepare_battle_stages.py` 从固定 manifest `871_156598` 的 FightStage EC 与原 `stage_monster_array`／`ai_priority` 表转换256个战场、474个阵型及40组AI优先级；原始表字节与APK内TextAsset逐字节核对。输入对象、APK／校验CDN来源、构造器及输出哈希见 [战场配置来源](../../assets/reference/official-map/battle-stages-source-manifest.json)。

`prepare_battle_scenes.py` 按 `FightComponent.SceneName` 解析229个原预制体，跟随 `FightEnvironmentLoader` 的昼夜GUID和父变换，转换基础层、Sprite／Tilemap／Mesh、Spine及ParticleSystem到本地二维资源。677份场景层包含1354个JSON／几何文件，统一目录为 `assets/official/battle-scenes/`；入口为 `battle-scenes.json`。原BG02副本的atlas键不匹配时，唯一同名且Rect／Pivot／Border／PPU完全相同的原atlas Sprite用于解析，原副本、atlas与实际对象分别登记，没有另绘位图。完整对象、处理及输入输出SHA256见 [战场环境来源](../../assets/reference/official-map/battle-scenes-source-manifest.json)。

`scripts/prepare_unity_battle_scenes.py` 校验上述固定版目录和256个阶段绑定，恢复229个唯一基础场景、非空昼夜网格及6,376张贴图，共6,656项资源；Unity 输出及每项SHA256记录在 `unity/Assets/Official/Combat/Scenes/scene-assets-source-manifest.json`。运行目录 `BattleScenes/catalog.json` 将GameId绑定到 `Resources.Load` 路径；[`OfficialBattleEnvironmentView`](../../unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleEnvironmentView.cs) 以官方网格顶点、UV、颜色、排序层和贴图建立场景，并按调用方覆盖值或原prefab默认昼夜加载环境几何。昼夜运行时规则见固定版 `FightTimeTypeSwitcher`：消费 `StageInfo.TimeType`，`MapActivedAuto` 再检查地图区域激活；当前Map10入口尚未传入运行时昼夜值。Unity端目前用 `Sprites/Default` 建立材质；原材质／shader、环境粒子与Spine，以及真实Editor／Play Mode画面仍未验证。

固定 APK／匹配 871_156598 的 Appearance 与 FightMonster／FightNpc／FightCharacter／FightPet／FightSkill 资源，经 prepare_battle_animation.py 读取原控制器常量、m_TOS、默认参数、状态与过渡、覆盖片段和 MyPlayable 绑定，输出 assets/official/battle-animation/catalog.json。输入对象、程序集字段、原客户端代码及输出 SHA256 见 battle-animation/source-manifest.json。没有按动作名称前缀编造技能动画。现有 Spine 网格按 12 fps 采样，相同附件拓扑的顶点混合为引擎适配，逐骨骼混合的精度边界在对应功能／地图文档如实记录。

`prepare_unity_battle_skill_playables.py` 从上述固定目录提取 1,451 个含默认 Playable 的技能实体、1,480 个动作 cue（1,191 个 Animator 参数 cue、289 个 clip override），生成 `Combat/skill-playables.json` 并将官方 AnimationClip 复制到 `unity/Assets/Game/Resources/Combat/SkillAnimations/`。8 个 NPC／召唤物 clip 均由固定版技能 prefab 的 `OverrideClip` PPtr 解析到原始对象：7 个来自 APK 内 `defaultpackage_assets_appearanceassets_npc_npc_middle_model_anim.bundle`，另 1 个 `npc_10206/skill_01` 来自固定版本 CDN 的 `defaultpackage_assets_appearanceassets_npc_npc_big_model_anim.bundle`。7 个中型 NPC 动画来自既有 AssetRipper 缓存；新增大体型动画从该固定 bundle 单独转换，按 bundle 名、`m_Name` 和 `m_StopTime` 与原对象核对（1.8333 秒），原始 AnimationClip SHA256 为 `92777a742fd1146f6725d7b9d9a84686ac470bdc18f0a331caff6e5fbdf380c4`，导出 `.anim` SHA256 为 `b331f45ad0117359f3e34242bb01f8195612c014c93d8bf388c2c18295e410f7`。所有 132 个包内 clip 的来源文件／来源对象、APK 或 CDN bundle provenance、输入／输出 SHA256 及目录 GUID 写入 `battle-animation/skill-playables-source-manifest.json`。Unity Editor 导入和 Play Mode 动画结果尚未验证。

`scripts/prepare_battle_native_mesh_pack.py` 将固定 APK 版本 `871_156598` 的已校验原 Spine 抽帧 JSON 规整为 Unity 可读网格帧，并仅绑定哈希匹配的官方 atlas 页，生成 `unity/Assets/StreamingAssets/Combat/native-battle-meshes.zip`。包内含403个模型、4609条动画记录及其引用纹理页；未找到匹配 atlas 页或模型 Spine 绑定的记录保留在来源清单的失败／跳过项，不补画替代素材。该包是原动画帧数据的运行载荷，不包含 Animator 状态机／过渡，也不代表完整战斗画面验收；内置屏  Play Mode 当前只验证岩灵／替身代表模型及姿态采样。

依据：`unity/Assets/Official/Combat/native-battle-meshes-source-manifest.json`。

`prepare_battle_hud.py` 从固定 871_156598 的 FightMainPanel、速度／托管、首领、行动条、战场状态说明、技能／幻兽与 StatusUI prefab 转换原 RectTransform、SafeArea、组件引用、Sprite、原生动画及字体。场景飘字直接转换 FightDamageSettings.EffectUIPoolPresents 的 13 个真实 prefab、TextMesh 位图字形和元素提示动画；保留原状态可见性、颜色、格式化、叠层／回合及首领分层生命／护盾计算。资源与对象输入输出哈希见 `battle-hud/source-manifest.json`，运行纹理在 `assets/official/battle-hud/`。

`prepare_battle_pet_models.py` 转换 40 个 FightPet 世界模型、40 个骨骼和 391 个原动画，保留世界层级、缩放、皮肤及静态几何；来源见 `battle-pets/source-manifest.json`，运行纹理、网格和动画在 `assets/official/battle-pets/`。没有用 UI 幻兽模型或自绘图片代替世界模型。世界模型与 HUD 使用独立显式 PNG 清单；动画绑定另见 `battle-animation/source-manifest.json`。

原控制器、方向 playable、生成／死亡、冻结／恢复在战斗时间线中执行；ShowSkillReplay、CDModify、技能扣血和元素提示绑定取原客户端与公共组件。尚未解析的图片地址原样列在 HUD 来源清单；没有替换或伪装缺项。共享导入、统一素材包和 Windows 正式成品需在资源锁交接后更新。

从1884个原 `MyFightSkill` 对象的 `PreferredPosToPlayable` 生成（输出SHA256 `5b661a9efbbd304977374446aa1c3b064b2b80605ff50b9fd88e8c3e7b426a90`），输入对象逐项匹配 `battle-audio/bindings-source-manifest.json` 的原对象哈希；文件内保留所用对象哈希集合的SHA256，重建入口为 `prepare_battle_audio.py --targets-only`。实体目标类型按原 `FightSkillComponentInfo.IsEntityTargetType` 的1／4／5迁移，用于保持Wwise对象范围和随机容器归属。

音量路由另读取 APK 的 `launch_assets_launch_scriptableconfig_wwisemanagerconfig.bundle`（MD5 `c68c296caddce110b1797a0f4c48e2e8`，SHA256 `a21c3dbc422bc531d05893c4085be52202e8ceaeb9e01cc12fd67b2ec00ab478`）：原 `Volume_ALL`／`Volume_SFX`／`Volume_Voice` 的ID分别为3240620755／3673881719／3676256460；`prepare_battle_audio.py` 的 `routing`／`check` 直接核验原引用与名称哈希。

`scripts/prepare_battle_audio.py` 沿固定 `871_156598` 的 MyFightSkill、全部 MyFightHit 系列、MyFightDamage、MyFightStatus、MyActionAgent、MyFightRoleCharacter 与 DefaultMoveEffectSupport 的 Playable 引用，提取方向、暴击／格挡、状态叠层、出场／死亡、序列时间、持续时间及停止事件；另从五种原阶梯楼层 prefab 读取 `StageDemonBattlefieldPanelItemPlayerPlayable` 的 Wwise 引用。从 28 个已校验原版 Wwise 分包（含 Init）转换 2172 个 Event、2068 份 Vorbis；新增阶梯 `Note_FantasyLadder_Flash`（2367806325）和 `Note_FantasyLadder_Elevator`（828673327）。wwiser 展开原 Event／Action／随机容器及原 dB 增益，vgmstream 解码，保留循环边界、随机变体、防重复／洗牌、停止对象范围与淡出。未使用自动音量归一化或替代音频。运行资源在 `assets/official/battle-audio/`，通过该目录 `resources.json` 和 显式加载；原包 URL／MD5／SHA256、原 Playable 对象／类型与输出哈希分别登记在 `battle-audio/source-manifest.json`、`battle-audio/bindings-source-manifest.json`。`--check` 校验来源包、全部输出哈希、循环与显式绑定覆盖。

prepare_home_ui.py 从固定 871_156598 的 FurnitureFastHangUpDialog、其 ItemPrefab 指针 FurnitureSpeedUsedItem 迁移原层级／状态／滑条与长按字段，共60个窗口／条目、269张相关原图；原客户端／公共程序集的加速请求、预览和四类处理器代码输入 SHA256 登记在 home-completion/source-manifest.json 的 fast_sources。／／ 使用这些真实配置与代码；服务端普通结算体未取得，直接到账和超过设施储存上限的处理由用户经验补证，具体边界仅记对应功能／地图文档。未添加自绘素材或自定义布局。

`scripts/prepare_unity_battle_monster_portraits.py` 按固定 manifest `871_156598` 提取 Map10／Map11 所需 38 种 Monster.IconId (`G_<id>`)。APK 来源包：`defaultpackage_assets_image_itemicon_gameheadicon_monster_map4countries_noatlas.bundle`，MD5 `0479892d6d89bf17fd2e592af3f03a49`，SHA256 `b07fe5bd436a230d6c31f8a88a07e6f3cd3cbcee95ebf1aee56cbd763020c10e`；`defaultpackage_assets_image_itemicon_gameheadicon_monster_mapsp_noatlas.bundle`，MD5 `2683984d4e613c8eb9411bc16ea483cb`，SHA256 `7b71fe37fd62f6255aaa3c42a4bccf181426cbfe0d1a230067e2e192b2e486e2`。魔女召唤所需 `G_3703` 位于 `monster_maptherion_noatlas.bundle`；APK 实际包内没有该 bundle，复用已缓存官方云端文件，MD5 `735124c8a8db27ba8846a5a6770c7f89` 与固定版索引一致，SHA256 `933bd8b252ec9949d39d28057deca746f830fb5b0f0e5ad9ddcfb5580dd18c07`。缓存下载版本 `871_156598` 与所选索引 `871_156598` 分别记录，不更改预期哈希。源输入、PNG／Sprite meta 和输出目录哈希见 `assets/reference/battle-hud/monster-portraits-source-manifest.json`，原 PNG 与 `.meta` 纳入 `unity/Assets/Official/Combat/MonsterPortraits/`，显式绑定 `Combat/OfficialBattleMonsterPortraitCatalog`；catalog SHA256 为 `f67e051f5dc1febb3d4f561f7b8c8502bdc6912668a9b5025bd72af2fc6d010f`。

`scripts/prepare_unity_battle_report_assets.py` 校验固定 APK／CDN manifest `871_156598` 的7个报告 bundle SHA256，并将个人、全员、玩家统计行、技能统计行及分隔线7个 prefab 和34项依赖资产导入 `unity/Assets/Official/Combat/BattleReport/`；7个 Unity Resources prefab 位于 `unity/Assets/Game/Resources/Combat/BattleReport/`。Prefab 的 UGUI／TMP 脚本 GUID 映射至工程内 Unity 包，所有外部 PPtr 均在转换后检查；逐 bundle URL、输入／导出／运行时 prefab SHA256 和资产路径见 [report-panels-source-manifest.json](../../assets/reference/battle-hud/report-panels-source-manifest.json)。其中8个 AssetRipper MonoBehaviour 文件是缺少 IL2CPP 脚本重建后的空壳，不含行为。官方客户端面板逻辑见 `.tools/source-packages/official-code/client-source/FightEndReportPanelProvider.cs`、`FightEndReportPanel.cs`、`FightEndReportPlayerTabView.cs`、`FightEndReportSkillTabView.cs` 及同目录相关 Item／Base 类；业务代码依赖官方服务器报告结构和客户端框架，尚未接入本地执行器，具体功能缺项维护于[通用战斗页](battle-execution.md)。

固定版 `871_156598` 的 `FightUtilsHandler.IsSkillMeetProfession` 经 `ProfessionBaseInfoParser.IsSkillAvailableForProfession` 计算玩家主动／被动技能可用性；对应官方共享实现位于 `.tools/source-packages/official-code/common-source/Common/FightUtilsHandler.cs`、`FightSkillData.cs`、`PassiveSkillData.cs`、`ProfessionBaseInfoParser.cs` 与 `ProfessionUtils.cs`，数据来自 `assets/official/rules.json` 的 `profession_base`。离线目录按 `Skills`、`FightUsedSkills` 和 `PrePro` 继承链计算同一标记；职业为0时依据 `OfflineEquipmentProfile` 的装备位置10解析当前武器类型，按官方 Sword→Zhanshi（1）、Wand→Fashi（10）映射优先回退当前武器，再回退初始武器。Map10、日常副本、幻想塔和竞技场玩家技能绑定均写入 `ProfessionAvailability`。客户端 `FightMainPanelSkillUI` 将不匹配技能显示为 `GameSkillIcon.State.ProfessionLimit`。未找到施放执行链消费 `IsAvailable` 并拒绝技能的证据，故本地只迁移可用性标记与 HUD 状态，不添加释放禁令。

通用战斗准备与规则补全：`871_156598` 原战斗 prefab 的407项输入／初始化绑定由 `prepare_battle_hud_graphic_bindings.py` 提取并在 Resources 显式加载；404项为 Graphic／CanvasGroup 字段，3项为官方组件字段指向的初始隐藏对象，见 [graphic-bindings-source-manifest.json](../../assets/reference/battle-hud/graphic-bindings-source-manifest.json) 和 [session-source-manifest.json](../../assets/reference/battle-hud/session-source-manifest.json)。`prepare_battle_prop_defaults.py` 从 APK 的 `config_luban` 重新读取73条默认属性，迁移 `LevelPropParser.TryGetPropDict` 的缺键补齐机制，编译绑定至 `FightOfficialLevelPropDefaults`，输入、源码及生成哈希见 [prop-defaults-source-manifest.json](../../assets/reference/battle-rules/prop-defaults-source-manifest.json)。`prepare_battle_rule_texts.py` 从当前 `871_156598` 已校验语言文件提取5149条原文，输出 `Assets/Official/Combat/battle-texts.json`，版本与输入输出记录见 [text-source-manifest.json](../../assets/reference/battle-rules/text-source-manifest.json)，详情弹窗绑定待接。战斗验证依赖的累充原生曲线从已有官方 bundle 经 AssetRipper 恢复，保留曲线并适配 Legacy Animation，见 [动画来源](../../assets/reference/commerce-components/accumulated-panel-animation-source-manifest.json)。

通用战斗场景和单位绑定：`scripts/prepare_battle_scene_bindings.py` 按 APK 优先、固定版 `871_156598` 云端缓存回退，恢复原 TagManager 排序层、344种单位模型 Transform 父链／MeshRenderer／血条排序及256个战斗场景的原相机位置和尺寸，严格解码 `GameCameraController` 依赖。输入对象、CAB、pathID、原始哈希和运行绑定哈希见 [layout-source-manifest.json](../../assets/reference/battle-scenes/layout-source-manifest.json)，载荷由 `Combat/battle-scene-bindings.json` 及 SHA256 侧车显式加载。现有场景网格恢复向下 Y／纹理 V，模型采样网格恢复纹理 V，并保留原 prefab 模型节点变换；这些是历史载荷的渲染修复，不代表已替换旧 Spine 转换依赖或完成原生角色动画。真实鼠标准备、运行至结算和场外节点／镜头恢复通过，角色、头像及完整 HUD 缺项仍见战斗所属页。

战斗快捷入口状态：`prepare_battle_function_entry.py` 复用固定 `871_156598` APK 内 `defaultpackage_assets_animation_ui_functionentrysui.bundle` 的原控制器及三个片段，从已校验同版 FightMainPanel 缓存恢复 StateSwitcher、两个按钮及两项 SelfRect 引用；输入对象、bundle／APK 哈希、官方源码和九个可选缺目标通道的哈希记录见 [function-entry-source-manifest.json](../../assets/reference/battle-hud/function-entry-source-manifest.json)。`OfficialBattleFunctionEntryTools.Restore` 保留原60fps曲线、整数参数、三个状态／转换及收展事件，生成 `Assets/Official/Combat/HUD/Resources/Combat/FunctionEntrys.controller` 与三个原生 AnimationClip；输入、转换器和资产／meta SHA256 见 [function-entry-native-source-manifest.json](../../assets/reference/battle-hud/function-entry-native-source-manifest.json)。`OfficialBattleHudView` 按原节点显式绑定官方控件逻辑及桌面指针适配；没有绘制替代面板或恢复消息服务。

战斗 HUD Sprite 导入：`OfficialBattleHudSpriteTools.Restore` 从上述30个固定版 prefab 节点读取88张实际引用图片的尺寸、边界、像素枢轴及 PPU，恢复38份受影响导入器；其中25张原九宫格边界先前未应用。`OfficialBattleHudAssetTools` 同步修复扁平树初始化和资源路径／文件名查找。原节点、官方来源清单／载荷、PNG、meta 及转换器 SHA256 见 [sprite-import-source-manifest.json](../../assets/reference/battle-hud/sprite-import-source-manifest.json)，显式绑定继续使用 `OfficialBattleHudCatalog`。聊天服务和动态头像仍未迁移完毕，新增元数据未生成完整素材快照。

战斗头像拍摄：`prepare_battle_portraits.py` 分别读取 `871_156598`／`871_156598` 官方 APK 配置，确认头照摄影参数与场景分支开关相同，再从固定871 APK 提取摄影棚和两个场景时间轴 prefab 的原层级／组件引用。76项输入对象、版本、原始字节、官方代码、生成器、运行适配及 JSON 哈希见 [portrait-source-manifest.json](../../assets/reference/battle-hud/portrait-source-manifest.json)。运行载荷为 `Assets/Official/Combat/Portraits/Resources/Combat/battle-portraits.json`；`CharacterNativePortraitCapture` 以当前原生附件网格和已有官方姿势拍摄透明头像，不使用采样帧，不生成替代素材。此阶段尚未绑定完整场景时间轴及其材质、粒子与动画，新增载荷未生成完整素材快照。

公共人物头像：`OfficialBattleCharacterPortraits.ForProfile` 复用上述摄影参数、原生人物网格、已转换姿势及既有原 `SkeletonDye-2D` shader 的 Windows 摄影适配，供主线地图、日常／每日挑战／Map12 私图、竞技场、幻境之塔和技能试打共用。主角按当前存档外观拍摄；竞技守卫由玩法适配器提供其真实外观，并应用相同的官方摄影排除部件。每个战斗单位只拍摄一次，100×120 透明头像随本场世界根节点释放，连续塔层各自持有。`OfficialBattleRequest.TimelinePortrait` 支持局部覆盖，未提供的头像继续走公共怪物目录及原空图；场景行动条按原角色类型区分玩家和怪物，守卫不再因带有怪物属性模板而进入怪物分支。临时摄影模型保留原摄影棚坐标，并恢复男女原图集全部937份材质一致的世界裁剪高度 -1,000,000，避免 shader 默认零平面将负坐标摄影模型整体裁掉；当前884来源单独记录于[头像材质来源](../../assets/reference/battle-hud/character-portrait-material-source-manifest.json)，不重写旧摄影参数的版本凭据。内置屏 Play Mode 已通过日常、竞技场、塔层的公共 Runner 实战与头像复用／释放检查，Map10 女性角色原模拟输入、双尺寸技能槽及结束恢复，以及 Map12 私图连续房间与冷读回归；已查看相关实际战斗画面。此前 Windows Player 在内置屏以 Direct3D 12 通过主城技能入口、试打设置、十回合战斗、结算动画、玩家／技能统计、技能快照及返回设置的模拟点击流程，退出码为0；本次移除已确认错误的1.25倍本地比例后，Player 构建及战斗画面待重新验证。该旧构建基于提交 `ad5d7abe6`；静态检查、Unity 导入／编译与 `ProjectTools.Validate` 曾通过。不代表全部时间轴事件、NPC 头像、材质和动画完成。

战斗 NPC 时间轴头像：`prepare_battle_npc_portraits.py` 以固定 `871_156598` APK 的 `npc.g.bytes` 与官方构造器解码148条配置，通过 APK 优先及已校验同版云端 bundle 提取89张原 Texture2D／Sprite，保留原尺寸、中心枢轴、PPU和采样／环绕参数。配置、资源清单、原对象／bundle／APK或云端哈希、官方代码、生成器、显式目录及输出见 [npc-portraits-source-manifest.json](../../assets/reference/battle-hud/npc-portraits-source-manifest.json)，绑定入口为 `Combat/OfficialBattleNpcPortraitCatalog`，由通用场景时间轴的 NPC 分支按 `NpcClassId` 解析。战斗两表引用61个非零NPC，已覆盖53个；11015—11022的8张配置图片经871／871清单和全部9个头像 bundle 实际对象核对仍未定位，查找输入分别记录，未混用版本或替换素材；49条 MapExploreBoss 历史图路径及2条空 Icon 保留未绑定。

战斗技能槽状态：`prepare_battle_skill_slots.py` 从固定版 `871_156598` 的 FightMainPanel／GameSkillIconItem 原 prefab 提取4个有序技能槽引用、5份 StateGameObjectConfig、图标／品质／CD文字引用，记录实际组件和 GameObject 原始哈希。`WorldGameSkillIcon` 直接保留官方状态枚举、对象共享判断和切换次序；主面板使用原槽对象，不追加替代卡片，不改坐标和重复节点名；主面板0—6与独立卡0—7的原配置差异原样保留。宠物解锁另核对原 `function_limit` Type 62 的 `HasAnyPetCondition`、`HasAnyPetCondition.IsMet` 与 `FunctionType`，直接读取本地持有数量，迁移 `FightMainPanelSkillUI.RefreshView` 的显隐和 `offsetMax.x=0/70`，输入行、配置和控制器哈希记入同一清单。技能槽状态绑定未新增或替换美术；禁用来源、完整技能事件、宠物图标／能量及详情仍见战斗页。

依据：[skill-slots-source-manifest.json](../../assets/reference/battle-hud/skill-slots-source-manifest.json)。

战斗技能品质：`scripts/prepare_battle_skill_quality.py` 依据原 `GameSkillIcon.Init`、`LanguageUtils.GetItemQualityImage`、Quality／ItemType 枚举及固定871物品／技能／皮肤配置，生成1910条原物品品质绑定与6张官方品质图。输入为 APK 内 `defaultpackage_assets_image_ui_itemquality.bundle`，记录实际 Sprite、SpriteAtlas、Texture2D、CAB、依赖与原始对象哈希；PNG 与既有官方导出逐字节核对。独立原生载荷为 `Assets/Official/Combat/SkillQualities/`，显式目录为 `Resources/Combat/OfficialBattleSkillQualityCatalog`，使用 FullRect／RGBA32、原采样参数及浮点留白／UV，避免自动 tight mesh 二次裁切、额外压缩或透明扩色。详见 [skill-quality-source-manifest.json](../../assets/reference/battle-hud/skill-quality-source-manifest.json)。运行 JSON 用随包 SHA256 侧车核验，输出保护拒绝覆盖已修改资产，Git 对相关哈希输入固定 LF；品质7原加载关系仍未取得，不替换为其他品质图。

- 战斗伤害飘字数值：`scripts/prepare_battle_damage_display.py` 核对871／871两版 `CharacterDamageUIManager`、`CharacterDamageUI`、`DamageResultParameter` 与 `FightDamageComponentImplement`，记录原始显示值和实际 HP 结算的分离；[来源清单](../../assets/reference/battle-hud/damage-display-source-manifest.json) 固定代码、原 HUD 层级及运行绑定哈希。继续使用固定871原数字 prefab；仅恢复数值输入、检查点字段和旧记录回退，不新增替代素材。

- 战斗世界空间数字：`scripts/prepare_battle_world_damage.py` 从固定871官方配置的 GUID 定位7个数字型 `DamageScene/*_Combine_Scene` prefab，复用已核实的 `world-ui.json` 层级及动画，并重新读取原 Transform、TextMesh、DamageNumberUI、CharacterDamageUI、DamageFloatSize、SortingGroup、Font、Material、Texture 对象。输出5套原位图字体与7个层级的显式本地绑定，[来源清单](../../assets/reference/battle-hud/world-damage-source-manifest.json) 记录 APK／缓存来源和输入输出哈希；Windows 材质复用已有 `UI/GameDamage` 程序转换。未更换为普通字体或新布局；纯提示型飘字和合并逻辑仍待完整迁移。

- 战斗元素反馈：`scripts/prepare_battle_element_feedback.py` 从固定871官方6个数字型世界 prefab 重新读取 `ElementConfrontationUI`、文本／背景引用、Animator、原生 TMP 字体、流式字体配置、Sprite／贴图／材质，复用经长度／MD5核实的原 FZY4K TTF；`scripts/prepare_battle_world_shaders.py` 将选定原 GLES 程序逐支转换为 Windows HLSL。复用两版官方文本／显示分支与背景自适应算法，原描边换算依据 APK 恢复的 `TMPEffect.DoSetup`／回调和 `get_atlasPadding` ARM64 指令，方法地址、元数据索引、字节哈希、输入对象及输出／运行绑定见[元素反馈来源](../../assets/reference/battle-hud/element-feedback-source-manifest.json)。仅覆盖这6类飘字的实际单描边分支，未声明完整 TMPEffect；数字源码提取同时保留原 RectTransform 的轴心／锚点，避免旧默认轴心造成文字与数字重叠。

`scripts/prepare_battle_damage_visibility.py` 核对固定871 APK／对应已缓存官方依赖的实际对象，扫描3,708个技能和角色 bundle，严格读取1,367组 `MyFightDamage` 显示开关及551个角色 prefab 根组件。当前规则引用的1,331个伤害实体均已绑定；3组隐藏、318组关闭零值，怪物3933的原 `CustomDamageUITypeFilter` 仅过滤零伤害免疫。两版客户端的隐藏／元素／非零／过滤／零值分支、提示类型与颜色顺序保持可核对来源；运行回执分别保存伤害 prefab 身份和运行序号，旧回执不推造身份。输出 `Resources/Combat/damage-visibility.json`、输入对象／bundle／配置和代码哈希见 [来源清单](../../assets/reference/battle-hud/damage-visibility-source-manifest.json)，接线和验证边界见[战斗执行](battle-execution.md)。

战斗状态计数：`scripts/record_battle_status_counter.py` 记录官方 `FightStatusController.CalcStatusRemainRound`、`FightStatusComponent.RoundComp`、`FightStatusUtilsHandler.GetStatusTargetByType` 与 `FightStatusItemBase`／`FightStatusUIItem` 的计数和节点显示分支，复用已绑定原图标／文字资源；源代码、图标清单输入及运行代码哈希见 [status-counter-source-manifest.json](../../assets/reference/battle-hud/status-counter-source-manifest.json)。此次未新增或替换美术，受控 Play Mode 像素与正常模拟点击回归范围写回[战斗执行](battle-execution.md)。

战报分页依赖：`scripts/restore_battle_report_tabs.py` 按固定871原包／严格同哈希缓存读取7份战报 prefab 的536个组件，补回31个分页组件的原序列化字段、标准布局、Graphic 输入标志和2处 SafeArea；迁移官方 `TabItem／TabGroup／TabTransition／TabAnimatorTransition`，复用既有官方 NoDrawingRayCast／Windows SafeArea，原 Animator Controller 和层级不变。PPtr 转为 Unity 行内格式，原输入、对象、代码、转换工具及运行输出哈希见 [report-tabs-source-manifest.json](../../assets/reference/battle-hud/report-tabs-source-manifest.json)。此清单在既有 `report-panels-source-manifest.json` 的原始导出之上记录运行 prefab 的后续转换。5组分页的16次模拟点击、回调顺序、重复点击、提交输入与原动画通过 Play Mode。

战报图形字段另见 [report-graphics-source-manifest.json](../../assets/reference/battle-hud/report-graphics-source-manifest.json)：从同批官方原 bundle 严格读取279个 Image／MirrorImage／TMP，恢复36个原生 Sprite、完整尺寸／轴心／裁剪偏移、原图集和本地字体引用。既有 AssetRipper 原生导出的几何／图集位置逐项核对，修复紧裁剪 Sprite 尺寸缩小；Knob 由 APK 内置资源的原生 typetree 直接恢复。最终图集使用固定 UnityPy 解码；复用已核对原对象哈希的 FZY4K，MPLUS 原字体按 FontAssetFontConfig 长度和 MD5验证，空动态 atlas 按原配置分配。脚本 `restore_battle_report_graphics.py` 和 Unity 导入工具显式绑定原生引用；本清单覆盖更新后的运行 prefab 输出哈希。279图形／36几何及5组16次分页模拟点击的 Play Mode 检查通过，表格布局、数据行及正式入口尚未完成。

战报布局／缩放／滚动和标准控件：`restore_battle_report_foundation.py` 严格读取同版原组件字段，新增迁移客户端 `LayoutElementClamper／MaintainGlobalScale／MaintainGlobalScaleUI`，复用原 Grid／横向布局、已提交的完整 LoopScroll 代码及 Unity UGUI。此前新增战报图集和字体尚未生成完整素材快照。

依据：[report-foundation-source-manifest.json](../../assets/reference/battle-hud/report-foundation-source-manifest.json)。

战报同步滚动：`restore_battle_report_scroll_sync.py` 从同版原组件严格读取3处表头／列表引用，迁移 `FightEndReportSkillTabView` 的限制、归一化复位和比例跟随方法。本地初始化入口适配原 UIBase 生命周期，保留原布局刷新次序；模拟列表拖拽与两端越界、普通战斗统计和结算返回通过。真实数据提供者、下拉框数据与正式入口仍待迁移。

依据：[report-scroll-source-manifest.json](../../assets/reference/battle-hud/report-scroll-source-manifest.json)。

战报玩家下拉控件：`restore_battle_report_dropdown.py` 严格恢复同版 `UIDropdown／DropdownItem`、所需状态对象／颜色分支及文本键值分支，迁移 APK 公共包内原对象3171787996812696172的行 prefab；保留原层级和五个预览行，三组原始变换及3项共享图形均核对，原动画／控制器复用既有载荷。28个组件／343字段、源对象、代码、managed-reference 适配和运行输出见 [report-dropdown-source-manifest.json](../../assets/reference/battle-hud/report-dropdown-source-manifest.json)。六条控件样本、8次模拟点击、行复用／新建、两档颜色及开关动画终态通过；语言／字体／效果服务、正式全局输入、真实数据和 Wwise 音效尚未完成。`battle_report_font_receipt.py` 将已提交字体载荷与编辑器自动生成的动态字形缓存分别记录，核对原种子字形与所有非缓存参数不变；缓存哈希不代表已提交素材。

战报中文文本：`restore_battle_report_texts.py` 从固定871版两份 WwiseBnk 中文语言 rawfile 严格按该版大小及MD5读取，仅输出7份原战报 prefab 所需25个条目；恢复121处原 `MultiLanguageText` 的完整字段、组件位置及绑定，不改原图形／布局。沿用原 `LocalizationTextService` 查表／回退／文本处理与原键值格式化顺序；完整字体／语言切换、替换提供者及文字效果仍待实现。

依据：[report-text-source-manifest.json](../../assets/reference/battle-hud/report-text-source-manifest.json)。

玩家战报数值行：`restore_battle_report_player_row.py` 从871 APK 的原 `FightEndReportPlayerTabViewItem` 对象迁移10字段／39项字段与引用、原本人底色 managed reference 及组件位置，保留原布局和图形。数值刷新、本体／召唤／宠物三类相加、队伍四项分母和中文紧凑格式复用官方 `FightEndReportPlayerTabView／Item`；未知历史显示空白、未知队伍分母清空对应条。原始 PlayerIcon 引用作为头像／资料待接端点保留；完整头像、未确认代理类型归组和正式窗口仍未完成。四条原 SlicedFilledImage 复用既有官方网格移植和经原始对象 SHA-256 核实的 ImgBgTitle 原生 Sprite，恢复完整字段与四种原色；图形、纹理与代码均无替代。原 bundle／对象／字段及代码／输出哈希见 [report-player-row-source-manifest.json](../../assets/reference/battle-hud/report-player-row-source-manifest.json)。

战斗召唤继承使用已取得的官方 `BattleFormulaHandler.CalcSummonMonsterInheritProp`、`SummonRankAdditiveFactorParser` 和详情合并调用。当前871与历史871相应反编译文件逐字节一致，运行时保持871已校验配置；输入哈希、程序集与转换入口见 [召唤继承清单](../../assets/reference/battle-rules/summon-inheritance-source-manifest.json)，生成脚本为 `scripts/prepare_battle_summon_inheritance.py`。

战斗岩灵／替身模型：模型3181／4600、原骨骼与atlas文本、完整纹理mip、原shader对象及岩灵头像Sprite原网格按APK优先和当前871云端来源恢复，转换／输入／输出哈希见[模型来源清单](../../assets/reference/battle-rules/summon-models-source-manifest.json)；Unity原生材质、骨骼数据、 shader及原URP管线显式绑定哈希见[模型绑定清单](../../assets/reference/battle-rules/summon-models-binding-source-manifest.json)。Play Mode只证明这两个模型和直接姿态采样，Animator过渡、其他模型、完整特效及Windows Player仍待实现，未生成完整素材快照。

## 完整还原部分

官方战斗模型索引 `Assets/Official/Combat/Presentation/models.json` 按来源清单逐字节保留 `assets/official/battle-presentation/models.json`，包括 UTF-8 BOM；`OfficialBattleModelCatalog` 使用 `TextAsset.bytes` 对照原始 SHA256，避免 `TextAsset.text` 解码时丢失 BOM 导致来源校验失败。`prepare_unity_battle_assets.py` 的 `copy_verified` 负责恢复并拒绝哈希不符的输入。Unity `ProjectTools.Validate` 会验证该绑定。

技能伤害测试的被动技能快照保留战斗装配时的原物品、等阶、完整槽级和来源小阶；统计触发的子实体按原物品关联到详情，不要求子实体与根技能实体相同。快照与主动行动槽分开，状态消失后仍保留在内存检查点。规则检查覆盖状态清除后的检查点往返及不增加主动槽；实际交互和 Windows 验证范围见[技能](skills.md)。

- 怪物单位使用战斗实际计算的等级和段位建立状态，并将同一等级交给主动／被动技能组装；使用玩家等级的关卡不再把导出时的占位等级 1 写回单位。Map14 数据 Play Mode 已执行 1,649 个实例的属性与技能构造，其中动态实例为 87 个；该夹具不代替完整战斗画面、输入、奖励与存档验证。

当前 Unity 工程已经形成可复用的战斗内核，但还不是完整原版战斗交付：

- `FightBattleState` 保存单位、属性、HP／护盾／韧性、技能、状态、格网、回合、召唤链接、事件和战报来源；`FightBattleExecutor.Begin/Restore/Step`、`FightTurnQueue`、`FightBattlePhaseMachine` 提供会话、阶段、回合推进、暂停边界和重复回执保护。
- `FightDamageFormula`、`FightDamageResolver`、`FightDamageSettlement`、`FightStatusLifecycle`、`FightStatusTriggerRuntime` 和 `FightResilienceFormula` 已接入官方数据可证明的部分，包括 HP／护盾差值、绝对伤害、暴击／格挡输入、元素反馈、状态计时／叠层、累计伤害、CD、破韧和随机流。
- `FightBattleGrid`、`OfficialBattleGridMapper`、`FightBattlePreparationPositions` 支持格网、身体占格、移动掩码、合法落点和已授权单位的原子换位；失败操作不修改检查点。
- `FightBattleCheckpointSerializer` 提供版本化 JSON 保存／读取，并在恢复时校验；检查点保留战斗状态、事件序号、随机种子和统计，重复或旧步骤不会再次结算。
- `OfficialBattleRuntimeController`、`OfficialBattleSceneView`、`OfficialBattleHudView`、`OfficialBattleRoundDisplay` 和相关 Catalog 已接入场景、时间轴、血条、Boss、技能槽、状态、战报行、伤害飘字、元素反馈、镜头和公共控件。官方战斗 prefab／材质／字体按来源清单绑定。
- 公共战斗模块为 `OfficialBattleRuntimeController`。所有现有运行入口（主线地图、日常副本／每日挑战、竞技场、幻境之塔、技能伤害测试）通过 `Begin(OfficialBattleRequest)` 启动或恢复同一实现；请求提供检查点、世界挂点、HUD 挂点及实际有差异的配置／适配器。模块内部建立格网映射、默认技能图标／头像／模型目录和朝向，管理 HUD、战斗环境、跳跃移动、技能演出、CD／回合／行动条、胜负与统计。角色职业动作组、地图专属头像／NPC 模型作为适配器注入，避免公共战斗反向依赖角色界面程序集。

- 公共战斗属性装配器 `OfficialBattlePassivePropertyAssembler` 已接入主线地图、日常副本、竞技场、幻境之塔和技能伤害测试。入口只提供玩家存档／技能槽或模式专属怪物被动绑定；装配器统一按官方技能目录求取被动的品阶、槽级、位阶参数并以 checked 规则合并战斗属性，缺失技能项保持显式失败。幻兽能量支援技能仍由 `OfficialBattlePetEnergyAssembler` 保留其专用的能量与继承规则。这样各入口不再复制同一套被动属性公式，新增战斗模式可通过绑定列表接入公共实现。
- 公共格网装配器 `OfficialBattleGridAssembler` 已接入日常副本、竞技场、幻境之塔、技能伤害测试和主线地图。各玩法只适配自己的场景 cell 数据；公共实现统一生成 `FightBattleGrid`、阻挡格、可行走 `MoveMask` 和空格网校验，保持原地面类型与高度阈值规则一致。
- 公共属性装配器 `OfficialBattlePropertyAssembler` 已接入主线地图、日常副本、竞技场、幻境之塔、技能伤害测试及幻兽能量装配。它统一属性列表排序、重复属性合并、缺失值读取和速度等整数属性的 checked 窄化；幻兽的能量、继承和被动合并仍由其专用适配器负责。
- 公共技能装配器 `OfficialBattleSkillAssembler` 已接入上述现有战斗入口的玩家、怪物和竞技守卫单位。各入口只提供技能槽、实体、装备／皮肤、职业可用性、等级和被动绑定；公共实现统一按本场属性计算技能属性快照与 CD，并创建被动状态和战斗技能状态。主线保留官方零值属性列表语义，其他入口保留原有非零过滤语义。
- 公共单位装配器 `OfficialBattleUnitAssembler` 已接入上述入口的角色和怪物初始化。入口提供位置、阵营、波次、怪物／NPC 标识、方向和 MaxHpPlies 等玩法字段；公共实现统一从属性初始化 HP／MaxHP／属性列表、移动距离和行动条速度，保持主线零值属性与其他入口非零属性的既有语义。
- 公共检查点装配器 `OfficialBattleCheckpointAssembler` 已接入日常副本、竞技场、幻境之塔、技能伤害测试和主线地图。各入口只提供阶段类型、回合限制、随机种子、网格、试伤规则和模式专属结果规则；公共实现统一创建可序列化的阶段状态快照及检查点默认结构，新增周常／活动适配器可复用同一契约。
- 各入口每帧只调用 `Tick(deltaTime)` 请求推进。准备、暂停和当前演出由模块统一门控；初始等待默认为既有本地入口的0.8秒，后续推进等待当前演出完成。删除独立的 `OfficialBattleProgressDriver` 和各 Runner 的战斗计时器；主线保留的延迟只用于地图／剧情过渡。所有实时结果交接均等待 `CanCompleteBattle`，步骤回执只保存检查点，防止致命一击尚未播完就销毁场景。已经结束的每日挑战存档在挂载之前按检查点结算恢复。
- 家园各入口传入各自官方场景 GameId，并复用公共技能动作解析，移除始终返回 `idle_01` 的适配器。玩法适配器继续负责角色原挂点恢复、入口隐藏、消耗／奖励原子事务及持久化；战斗胜负、战报计数和检查点结构复用已有规则实现。请求支持后续周常／活动入口使用同一模块，但尚未接入的玩法不计为已迁移。主线 Map10 与技能试伤入口已通过模拟输入运行验证；日常副本、竞技场和幻境之塔已验证公共 Runner 启动、HUD 开始／倍速输入、真实胜负及主城恢复，三个入口窗口的完整点击流程仍待验证。
- `OfficialBattleNativeAnimationLibrary` 和 `OfficialBattleNativeModelView` 已能加载已恢复的原生模型数据；固定版原动画采样包包含403个模型的4609条动画数据，纹理页按来源清单校验。岩灵／替身模型、HUD 绑定和部分骨骼姿态已做局部  Play Mode 检查；抽帧网格不等同 Animator 状态切换、逐骨骼混合或完整战斗演出。
- 战报构建和统计已保留施放、命中、治疗、护盾、承伤、破韧、状态及来源信息；缺失的旧存档字段保持未知，不从剩余 HP／护盾倒推。

### 已迁移代码与局部资源

以下为局部代码／资源接入，完整流程仍受本页缺项限制。

状态属性初始化与叠层刷新（局部实现）：迁移 `884_156692` 的 `FightStatusComponentImplement.InitProps`／`SetStackedCount`、`FightStatusUtilsHandler.GetStatusProps`、`FightStatusPropBaseComponent.GetProps` 及共享属性公式。先将实体成长属性换算为状态配置属性，再用词条原始 Long 值覆盖（此处不按组件参数的 Float 类型除万），随后按配置叠层、对当前状态实体施加祝福并截断；属性组件随后执行属性转换、第二次状态换算及最终叠层。无属性组件的护盾直接使用初始化结果。保留原代码的边界：初始化零层得到空状态属性，后续缩放到零或空配置时不清空已有值；后续叠层更新从配置属性重算，不再次施加初始化祝福；关闭初始叠层缩放时，祝福同时影响原本别名引用的配置属性。状态保存原值、祝福、配置属性和状态属性，施加时深复制，旧检查点缺少初始化字段时重建；未计算的投影缓存保持 null，叠层和重放消耗会使缓存失效并刷新人物属性。装备被动携带的 `SkillPropMapFactorIds` 现在随状态触发、子技能、附加技能、召唤状态及技能施加状态传递，并在对应实体属性计算后按实体／属性键缩放；检查点保存和恢复保留该映射。实现入口为 `FightStatusPropertyRuntime.Initialization.cs`、`FightStatusPropertyModifiers.cs`、状态生命周期／触发运行时与执行器派生技能适配。`test_battle_status_properties.ps1` 在会话临时目录直接编译原方法作为对照，覆盖16个真实配置状态及受控边界、两阶段换算、原值替换、祝福截断、零层、属性转换、护盾倍率、实际人物攻击变化和检查点续接；成长计算由既有目录提供，本项对照不验证共享叠层仲裁和PVP缩放。战报回归的两次施放夹具显式配置一次重放上限，适配已确认的连续重放规则，不改变统计断言。来源与适配哈希见[状态属性清单](../../assets/reference/battle-rules/status-properties-source-manifest.json)。状态施加、触发链、护盾统计和连续重放回归通过；全量静态检查通过（84,379个GUID），内置屏D3D12导入／编译与 ProjectTools.Validate 通过。首次宿主进程在系统内存不足期间加载怪物头像时原生崩溃；确认进程退出后重跑，同一套三宿主验收通过，退出码0：日常副本123步、竞技场10步、幻境之塔11步均真实结算，每场检查攻击原值17→祝福截断25→两层投影50、缓存刷新和检查点往返；独立重放22／2／4次、原60171子技能23／2／6次，模拟公共HUD开始／倍速、两像素时间轴投影及人物／相机恢复通过。已查看11张实际入口／准备／播放／返回截图，人物、怪物和背景可见；主城黑白几何、游记白块／占位文字及公共HUD缺项仍存在。入口和本项状态由隔离夹具建立，不计作正式穿戴词条或完整菜单输入验收，未构建／发布本项Windows Player。装备被动状态装配和祝福向已接入的派生状态／技能传播已完成；正式穿戴词条收集和其余组件执行仍未完成，不代表完整技能系统或完整装备效果已还原。

通用状态伤害处理（局部实现）：按官方 `FightStatusDamageProcessComponentInfo` 与 `FightStatusDamageProcessNoIndependentMultiplierComponentInfo` 的目标、阶段、结果类型、来源状态动作和敌我过滤恢复 `BeforeShield` 分支。状态实例保存组件快照，25108／25126 的 `Ridicule(58)` 来源条件、25136 的治疗增幅和 24198 的召唤者增伤均在护盾／结算前执行；24062 继续走已有“施加者伤害”专用公式，避免重复倍率。`test_battle_damage_process.ps1` 新增四个真实状态配置的伤害／治疗断言，并保留原 24062、护盾、累计伤害和检查点回归。官方未提供服务器执行体的其他阶段或未列入当前配置的组件仍不推定为已实现。

状态组件参数替换（局部实现）：复用当前 `884_156692` 的 `FightStatusBaseComponent.GetPropertyReplaceValue` 和 `FightStatusUtilsHandler.TryGetPropertyReplaceValue`，由 `scripts/prepare_battle_status_parameters.py` 从原访问器、Info 字段类型及组件继承生成53类组件／120项含继承字段的读取契约；字段重载按 Info 声明类型选择，不能以访问器返回类型推断。状态装配先在实例配置副本中应用显式类型替换，再交给既有解析器；共享官方配置不变。保留 Float 的万分比、Long 原值、整数窄化、类型不匹配时原转换、首项匹配与缺值回退；只有已找到原访问器的字段才被解析，未知映射继续保留给既有未支持检查。重放改用这条通用读取链。规则回归通过真实60170／60470的概率与次数替换、60170→60171回合开始子技能收集、实例隔离、60531未支持字段保护，以及受控配置中的长整型边界／重复映射；连续重放与被动FIFO回归通过。全量静态检查通过（84,374个GUID）。最终Unity内置屏D3D12导入／编译与 `ProjectTools.Validate` 通过，退出码0。内置屏D3D12隔离存档Play Mode通过日常副本128步、竞技场10步、幻境之塔11步的实际结算，原60170参数置为必定命中的隔离夹具分别执行60171子技能23／2／6次；独立被动FIFO重放、检查点往返、模拟公共HUD开始／倍速点击、人物／相机恢复和两像素投影检查通过，进程退出码0。已查看11张实际入口／准备／战斗／返回截图，人物、怪物和背景可见；主城几何异常、游记白块／占位文字和公共HUD缺项仍存在。宿主入口及本项状态由夹具装入，不计作正式穿戴词条或完整菜单入口点击验收；未构建／发布本项Windows Player。来源见[状态参数清单](../../assets/reference/battle-rules/status-parameters-source-manifest.json)。尚未执行的组件不会因为字段读取契约已提取而被视为已还原；状态属性中的原值替换／祝福缩放见本节对应段落，正式穿戴词条宿主接入仍未完成，装备参数范围见[装备功能](equipment.md)。

19个原版本缺失CAB依赖按既有独立校验机制读取新版官方同CAB包，真实 `864_156496` 版本、URL、MD5／SHA256和所用对象登记在上述收据的 `counterparts` 字段；其余场景主体保持871版本。没有修改原APK／CDN缓存或原版本185个缺项。场景入口保留原预制体根Transform，逻辑格经地面原点和根矩阵映射到原世界坐标；已转换几何保持同一世界坐标。转换资源加入显式加载清单及Windows导出过滤，运行时无需原Unity包、Python或网络。

通用战斗复用角色绑定：`OfficialBattleCharacterAnimationTools.Build` 检查既有男女原生 prefab 与实际 AnimatorController，各绑定26个非空 Motion，排除442／449个空状态；`Resources/Combat/OfficialBattleCharacterAnimationCatalog.asset` 按控制器引用和片段名称显式加载。输入 prefab／控制器哈希、既有转换链凭据、生成器及输出哈希见 [character-animation-binding-source-manifest.json](../../assets/reference/battle-models/character-animation-binding-source-manifest.json)。3100模型父链复用上述场景凭据，角色退场还原世界层级、变换、显隐和动画状态。挂接时清除地图视图根节点及直接角色子节点上的局部缩放与偏移，由官方战斗父链独立控制模型尺寸，避免把地图的0.3比例与战斗模型的0.3比例相乘；验收同时检查战斗比例和地图变换恢复。没有重新烘焙 Spine 数据，历史转换依赖和未转换动作继续保留原结论；人物头像接线及验证范围见上文“公共人物头像”。

战斗主面板材质：`prepare_battle_hud_materials.py` 从固定 `871_156598` 官方 FightMainPanel prefab 的三处实际 Image 引用取得序列帧、加亮及胶囊边框材质，沿引用取得原图、Shader 和全部16个 GLES 程序。Windows HLSL 转换保留选定分支的原运算与12个 UI 裁剪变体、材质属性和渲染状态，另外4个原分支归档保留；不复用缺少循环分支的其他 shader。输入对象／CAB／bundle／版本、转换器、运行绑定和输出哈希见 [materials-source-manifest.json](../../assets/reference/battle-hud/materials-source-manifest.json)，Unity 导入参数与原生材质／目录／meta 见 [materials-native-source-manifest.json](../../assets/reference/battle-hud/materials-native-source-manifest.json)。

## 自定义实现

被动重放（局部实现）：用户确认同次技能结束时各重放状态独立判定并各自产生重放；本次命中的重放先全部排队执行，其新触发的重放追加队尾。原 `FightStatusSkillReplayComponentInfo`／`FightStatusSkillReplayComponent` 提供概率、失败增概率、每回合上限、动作／阶段／技能标签过滤、词条替换值及 `LimitedTimes` 开关，但未取得服务器执行实现。当前新增五项原状态配置绑定、四段64位标签查询、显式类型替换值、生命周期深复制与旧检查点补齐；执行器统一使用队列处理自身与被动重放，状态分别保存回合次数和失败增概率，成功归零额外概率。被动重放只响应原槽技能；先应用 `CanTriggerChild`／白名单和过滤条件，再判定概率。原技能槽限定释放次数与排队中的释放共同限制可触发次数，实际执行后增加原释放计数。规则回归通过五项原状态映射、类型替换值、A→B→A队列、独立与共享上限、首次／重放释放次数、白名单／动作／阶段／标签过滤前不消耗随机数、失败增概率、跨个人回合重置、深复制及旧检查点补齐；保存后恢复并继续四步，与原执行器的状态、随机数和战报逐项一致。连续重放、冷却状态与触发链回归通过，84,304个GUID静态检查通过。内置屏D3D12 Unity导入／编译、`ProjectTools.Validate` 和隔离存档的三宿主Play Mode通过：两个必定命中的隔离被动状态各自每个人回合限一次，日常副本298步／42次重放、竞技场26步／4次重放、幻境之塔35步／9次重放，均真实结算；模拟公共HUD开始／倍速点击、队列来源、次数、检查点、人物／相机恢复及时间轴投影检查通过，进程退出码0。已查看11张实际入口／准备／战斗／返回截图，人物、怪物和战场背景可见；主城几何异常、游记白块／占位文字与公共HUD缺项仍存在。宿主入口由隔离服务夹具建立，不计作完整菜单入口点击验证，未构建／发布本项Windows Player。词条替换值已贯通被动技能绑定与状态装配；原装备参数计算与剩余宿主接入范围见[装备功能](equipment.md)，不能把无替换值的60150基础零概率配置称为已产生正式词条效果。

连续重放（局部实现）：用户已确认每次重放结束后重新按配置判定，直到概率未中、条件不满足或达到每回合次数上限；`ReplaceSkillArrayOnReplay` 按本回合重放次数依次取用。官方 `FightSkillReplayComponentImplement` 为空，原配置、`FightSkillReplayCfg`、`FightSkillComponentInfo` 与按条件槽提供者的注释是证据入口；槽提供者明确从全部符合条件的槽随机选一项。当前执行器移除一层重放上限，在技能结束状态处理后重新判断，绑定原概率、条件槽、替换实体与每回合上限；同槽替换技能共用个人回合计数并写入检查点，替换保留原培养、物品、外观与职业信息，再执行实际替换技能的后续条件。规则测试已验证20层两次重放、零次／有限上限、200次有限链、概率与条件槽、13220→13223停止条件、13104～13108五项顺序替换，以及完整检查点恢复；五项测试仅在隔离内存配置中将原关闭的概率置为1，正式数据未修改。替换数组越界暂显式报错，不擅自循环或复用末项。本项重放规则回归与84,296个GUID静态检查通过，Unity导入／编译和 `ProjectTools.Validate` 通过。完整入口 Play Mode 在进入战斗前被日常副本窗口 `Content` 原动画的 `PostEvent` 缺少接收组件中断；已查看实际游记截图，仍有白块与占位文字，不计作完整入口或界面通过。独立战斗宿主验证另发现HUD布局完成前的时间轴投影滞后：适配增加 `Canvas.willRenderCanvases` 后重投影并在禁用时解订阅，检查移至当帧渲染结束后，保留原两像素误差要求；内置屏D3D12 Play Mode重跑通过：隔离存档中模拟公共战斗HUD开始／倍速点击，日常副本382步、竞技场27步、幻境之塔54步均真实结算，每场恰好重放两次、耗尽20层、遵守上限并通过检查点往返及场外人物／相机恢复检查。三场时间轴均通过两像素投影检查，已查看11张实际入口／准备／战斗／返回截图，人物／怪物／背景可见；主城黑白几何、游记白块／占位文字与公共HUD缺项仍存在，不计作完整界面或动作还原。验证返回编辑模式后手动停止本会话编辑器，未构建或发布本项 Windows Player。被动重放状态的后续接入见本节对应段落。

技能伤害测试（StageType 43）采用用户确认的本地怪物属性方案：`FightDamageTestRules` 只在测试关卡消费固定伤害 1／无限生命选项。怪物伤害在护盾结算前变为 1；无限生命保持怪物 HP，保留完整命中伤害和原通知顺序供战报统计，并免除怪物的生命施法费用。选项写入战斗检查点，普通关卡继续使用原结算路径。`scripts/test_skill_damage_test.ps1` 已验证连续致死命中、伤害统计、普通关卡隔离、关闭选项和检查点恢复；`scripts/test_unity_combat.ps1` 的 97 组公式及战斗规则回归通过。技能入口、测试画面、结算界面与发布验证见[技能页](skills.md)，本段不表示完整测试流程已经完成。

以下内容是因为未取得服务器代码而做的本地替代，不宣称为原版服务器规则：

- 用户确认的单机技能选向规则：技能目标沿用现有候选优先级，从选择范围内首个目标确定上／下／左／右朝向，再只保留同向范围内的目标；方位采用目标偏移主轴判定，等轴时沿用既有水平优先规则。实现位于 `FightBattleTargeting.SelectInSkillRange`，不声称恢复服务器 AI 的面向策略。
- 离线执行采用固定随机种子、步骤序号、回执和版本化检查点，以便单机续算和重复输入去重；客户端只提供了消费消息和部分公式。
- `FightBattleResultRules` 提供标准清场、显式保护目标和显式玩家对决三种可配置结果策略；主角死亡、保护目标死亡、双方全灭和回合上限的优先级属于离线策略，调用方必须明确参与者，不能从地图或同阵营猜测。
- `FightBattlePreparationPositions` 用调用方提供的控制名单替代服务器站位请求，保留官方格网／身体占格校验，允许合法受控单位交换位置。
- `FightVoodooDollRules` 按已确认方案实现替身：传导完整结算伤害（包括溢出）为绕盾真实伤害，禁止治疗传导，替身结束时转移减益并保留原状态计时／叠层语义；来源和同步实体 ID 仍使用官方配置。
- 用户确认子技能不单独培养，原技能变化时子技能随之变化。`DamageChildSkillCfg` 命中子技能与 `FightSkillExtSkillComponent` 动作结束附加技能继承触发技能的品阶、完整技能等级及位阶，再按各自官方 `entity_prop` 的品阶／等级表与 `PropFactors` 计算，概率和血量条件继续读取原配置；不用受创者或施放者的人物等级替代技能槽级。官方包只取得配置类，未取得服务器继承实现，因此此继承接线记录为用户确认的单机方案；用户另确认命中子技能沿用原施放者和本次受击目标：保留该目标为位置锚点，不再次从全场选敌，自身增益按子技能配置回到原施放者，敌友、范围和效果仍取子技能原配置。
- 用户确认连续触发按配置继续执行，不再把命中子技能、动作结束附加技能及被动触发链统一限制为一层。`FightSkillTriggerPolicy` 在被动状态响应技能、消耗随机数或更改计数前检查当前技能：`CanTriggerChild=true` 允许响应；关闭时仅 `CanTriggerStatusEntityIds` 中的状态 **classId** 允许响应。每个派生技能使用自己的开关和白名单，仍遵守状态本身的概率、条件及次数上限。当前已接技能开始／结束、HP 阈值、受伤累计阈值、累计伤害记录、受创不屈、响应式减伤、开启触发检查的伤害自定义属性和衰减，以及护盾破裂技能；护盾自身仍正常吸收伤害。命中子技能属于自动子触发，父技能关闭 `CanTriggerChild` 时不释放；技能直接配置的伤害／治疗、状态和动作结束附加技能照常执行，不用此被动响应开关删掉直接效果。共享 `FightSkillComponentInfo` 提供字段及白名单，`DamageSkillCanTriggerStatusFilter` 仅提供端口／操作码，未取得服务器过滤执行代码；上述开关组合与连续执行口径属于用户确认的单机接线。
- 服务器未提供的 AI 评分、随机命中、复杂触发顺序、伤害阶段钩子、位移条件、召唤数量／寿命、战报聚合和部分元素／减伤组合只实现了有明确配置或已确认口径的分支，其余保持未决，不把近似结果写成官方规则。
- 同一培养方案用于 `FightStatusHpDecreaseUnitComponent` 的血量阈值触发技能：收集请求时保留该状态记录的来源品阶、技能等级和位阶，再按触发技能的原配置计算属性；不再只传技能 ID 后重置品阶／套用持有者人物等级。原血量门槛、次数和触发者／目标规则继续使用既有接线。

### 已确认的本地接线与边界

子技能参数边界：父技能未填写位阶时，命中子技能按继承规则归一到官方最低位阶 1，避免实体属性表缺少 0 阶行而产生零治疗或零伤害；原技能已填写的品阶、等级和位阶仍原样继承。

状态投影兼容：旧检查点若只有非空 `ResolvedProperties` 而没有初始化配置缓存，恢复时保留该官方投影并补建配置缓存；空投影继续按状态实体属性重新计算。

本次连续触发回归另覆盖关闭 `CanTriggerChild` 时不释放自动子技能；长链夹具将各级父技能显式设为允许，保留五次施放和检查点恢复断言。

连续触发规则回归：`test_battle_trigger_chains.ps1` 以隔离内存配置验证五段命中子技能／附加技能链、两层被动技能、技能开始重入至配置次数、关闭时状态白名单、直接伤害／状态保留、来源培养、护盾破裂继承、响应式减伤与衰减，以及拦截前的随机数／次数／累计量不变；存档恢复后的完整结果与原执行一致。原子技能、附加技能、HP、技能开始／结束、减伤、累计伤害、护盾战报和检查点规则回归通过。内置屏 Direct3D 12 Play Mode 在日常副本、竞技场和幻境之塔的隔离存档中接入连续触发夹具，模拟公共 HUD 开始／倍速输入，分别执行到 313／30／52 步真实结算并核对场外人物／相机恢复；三场均产生两次状态技能，命中子技能／附加技能分别为 81／96、8／11、20／21，来源品阶 6、技能等级 17、位阶 10 及检查点往返保存检查通过。首次夹具使用原 50% 增益，两次未命中导致培养断言失败；改为原 12110 必定施加的 24201 增益后重跑通过，没有改变正式配置或概率。Unity 导入／编译、`ProjectTools.Validate`、静态检查通过，编辑器正常退出。已查看全部11张实际入口、准备、战斗和返回截图；战斗人物／怪物／背景可见，主城黑白几何、游记白块／占位文字及公共 HUD 缺项仍存在，不计作界面或完整动作通过。通用 97 组回归仍受下文所述原始伤害参考文件缺失阻塞。本项未构建或发布 Windows Player，不表示完整延迟调度、重放次数、全部状态响应或技能演出已完成。

血量阈值触发培养回归：`test_battle_hp_trigger.ps1` 按来源清单的固定版本／输出哈希校验规则，验证原 60680→60681、25070→50010、血量门槛、次数和开场状态，以及隔离夹具的 63434→24658 来源品阶 6、技能等级 17、位阶 10。规则夹具中持有者人物等级为 83、位阶 23，检查点恢复前后仍沿用来源技能参数；原 11050 的实际伤害随来源培养变化。带画面 Play Mode 在日常副本、竞技场及幻境之塔的隔离夹具中挂入血量触发，人物等级 40、位阶 4 时仍保留来源技能的 6／17／10；三场分别观察到 2、2、1 次原 63434 施放，并核对原 24658 增益参数，检查点往返一致。三个公共 Runner 按现有配置直接进入播放、模拟倍速并运行至真实胜负和主城状态恢复，已查看实际战斗与返回截图；不把入口窗口点击或完整界面／演出计为通过。Unity 导入／编译、`ProjectTools.Validate`、子技能、附加技能、累计伤害、韧性、状态施加、战报来源、检查点回归与静态检查通过；夹具未写回交付配置，本项未构建或发布 Windows Player，完整技能系统仍未完成。

派生技能培养回归：`test_battle_child_skill.ps1`／`test_battle_extra_skill.ps1` 按来源清单的固定版本及输出哈希加载规则，验证原 63434→24658 的来源品阶 6、等级 17、位阶 10 及状态检查点恢复，另以原 11050 的实际伤害检查培养参数参与数值计算。带画面 Play Mode 使用隔离回归夹具将原 63434／24658 接到测试技能的命中子技能和动作结束附加技能路径，模拟开始／倍速后，后续状态均保留原技能品阶 6、完整槽级 13 和位阶 10，三入口执行至真实胜负并返回主城，已查看实际截图；夹具不修改交付配置，不表示原 10014 配有这些效果。Unity 导入／编译、`ProjectTools.Validate`、状态施加、技能开始／结束及检查点回归通过；未构建或发布本项 Windows Player，不能据此声明完整子技能演出或递归链已完成。

命中子技能的来源／目标回归：`test_battle_child_skill.ps1` 验证原 11050 的伤害来自原施放者、范围命中各自保留受击目标、原 63434→24658 自身增益归属及检查点恢复、原 12733 对原友方目标实际治疗，概率与血量门槛继续生效。带画面 Play Mode 仅在隔离夹具中把原 11050 接到测试技能 10014，三个公共战斗入口模拟开始／倍速并运行至真实胜负及主城恢复，分别核对每日副本 82、竞技场 8、幻境之塔 12 次子技能的施放者和对应目标，已查看实际运行截图。静态检查、Unity 导入／编译和 `ProjectTools.Validate` 通过；本项未构建或发布 Windows Player，完整递归、动作时序及子技能演出仍未完成。

prepare_treasure_tasks.py补充固定871_156598的BattleEventType、原TreasureDataBattleStatus／TreasureDataGameDestroyMonsterKind、BlackboardDataType及MapEntityWorldStageType来源继续保留，75件选择键和配置未改变。本项只有代码输入及运行时接线，没有新增素材。服务端事件写入方法未取得，实际触发按用户确认迁移为本地接线，确认原文与实施核对仅记录于对应功能／地图文档。

固定版 `871_156598` 的 `assets/official/battle-rules.json` 中状态实体 `25275` 配置 `FightStatusUnflinchingOnDamageComponent.Rate=0.5`；来源与输出哈希由 `assets/reference/battle-rules/source-manifest.json` 核验。官方共享代码 `.tools/source-packages/official-code/common-source/Common/FightStatusUnflinchingOnDamageComponent.cs` 在状态 `OnInvoked` 时向持有者发出 `EStatusCustomAction.Unflinching`，枚举值为 2（`EStatusCustomAction.cs`）；服务端调用时机未取得。离线实现按用户确认的推断在每次实际 HP 损失时掷配置率，并将动作 2 写入战斗步进事件／回执；固定 HUD world prefab 数据的文案绑定为“霸体”。

固定版 `871_156598` 的 `assets/official/battle-rules.json` 为随机命中实体 `13992`、`59361`、`11202` 配置 `MiniHitTargetCount` 与 `NotSameTarget=true`。官方共享类 `.tools/source-packages/official-code/common-source/Common/FightHitRandomTargetComponentInfo.cs` 定义随机范围、HitList 和去重字段；`.tools/source-packages/official-code/common-source/Common/FightHitRandomTargetComponent.cs` 仅提供这些配置，客户端 `.tools/source-packages/official-code/client-source/MyFightHitRandomTarget.cs` 是空包装，未取得服务端选敌器。离线按用户确认的规则，在同一组件的 HitList 子命中间按格网选择：`NotSameTarget` 排除已选单位，`NotSameEmptyScope` 排除已选空格，`NotSameGrid` 排除已选格子；候选耗尽时跳过后续子命中。`FilterOutOfGroundScope=true` 时越界格在分配前滤除，关闭时越界格可作为空格候选；`AllowEmptyScope` 允许空格消耗子命中名额，`NotAllowHitEmpty` 禁止空格命中，空格本身不造成伤害或召唤。选中单位作为子范围命中中心，召唤子命中仅消费选中的有效单位。执行和覆盖测试位于 `FightBattleHitCollector`、`FightBattleTargeting`、`FightBattleExecutor` 及 `scripts/test_unity_combat.ps1`；`FilterCondition` 等其他区域过滤仍未实现。

魔印来源归因与优先选敌：两版24062的 `FightStatusComponent.AI.MoreHit` 已从实际 EC 载荷解码并与运行配置核对；官方 `FightStatusUtilsHandler.DistributeLongProportional` 直接保留整数取整及末项余数。输入、官方代码、运行实现哈希和用户确认的两条本地规则写入 [accumulated-damage-source-manifest.json](../../assets/reference/battle-rules/accumulated-damage-source-manifest.json)。触发实际生命伤害按原角色／技能贡献分摊，独立魔印事件保留；候选与嘲讽／混乱限制后优先标记，再沿原技能排序。这两处服务器接线没有取得，按已确认单机规则补足，不作为服务器源码已还原的结论。

魔印承伤加成：同一生成器已逐字段核对两版 `FightStatusDamageProcessComponent`，并记录官方实体属性、阶级／等级输入、`DamageProcessPhase.BeforeShield` 和空的 `BattleFormulaHandler.InvokeDamagePhaseProcessor`。用户确认先百分比后固定值，故本地仅接24062的已知组合：施加者本人造成的伤害读取独立231／235属性，在扣盾前处理；其他来源、治疗与未命中不执行。用户另确认施加者已阵亡、目标存活且战斗继续时仍结算魔印，原施加者继续作为来源；本地仅对该结束链放行死亡来源，保留其0生命和原技能追加统计，普通死亡单位的攻击资格不变。一阶一级官方输入经缺键默认值、阶级缩放及实体系数得到20%和4789，未把3505实体系数直接当固定伤害。

依据：[accumulated-damage-source-manifest.json](../../assets/reference/battle-rules/accumulated-damage-source-manifest.json)。

战斗元素抗性：`scripts/prepare_battle_element_confrontation.py` 从871／871两版官方 `config_ec` 中重新解码 `FightSkill_part753.g.bytes`，逐字段核对25051—25060的状态、动作及20条 `ElementInfos` 与固定871运行载荷；分别记录两版 APK／缓存、bundle、对象、原始载荷及反编译代码哈希。单状态保留原 float 值，多个状态按用户确认的本地规则先判断元素免疫，否则将各自的伤害倍率乘算；不推定按状态层数重复乘算。共享公式、扣盾前计算、实际 HP 差、结果与检查点保持原路径；服务端绑定和合并器未取得，此处明确记录为经确认的本地补足，旧缺项回执不改写。规则脚本、实际 Play Mode 专项和运行代码哈希见 [element-confrontation-source-manifest.json](../../assets/reference/battle-rules/element-confrontation-source-manifest.json)；元素文字与动效的单独来源见下文。

战斗世界提示：`scripts/prepare_battle_world_tips.py` 按固定871 APK 实际配置和对象恢复免疫、回避、未命中、重置冷却、自定义动作和驱散六类世界 prefab，保留原语言、层级、TMP 字体／材质、单描边、完整动画、排序、相机缩放与堆叠参数。复用已核实的原 FZY4K TTF 及世界 TMP shader，不引入替代素材；共享动态字形缓存、两版客户端、原对象及运行绑定哈希见 [world-tips-source-manifest.json](../../assets/reference/battle-hud/world-tips-source-manifest.json)。重置冷却另保留渐变、三层 Sprite 及13条旧式动画曲线，原 Sprite 与既有 HUD 贴图逐点核对；1,906个技能重放提示开关直接读取原 prefab。提示锚点按两版 `CharacterDamageUI.UpdateAttach／UpdateOffset` 在创建和堆叠时读取实际实体位置并归零本地Z；`DamageFloatSize` 的镜头引用、尺寸缓存、101执行顺序及暂停中的缩放按原客户端恢复。类型11另直接绑定 `DamageCustomActionUI.mIcon`、`EStatusCustomAction` 三条原语言及6条动画曲线；仅 CDModify 显示原箭头。成功驱散通知的发出条件由用户确认。语言直接读取原 APK；旧视图清理后重建通过，保留云端只复用满足原871 MD5／大小及下载凭据SHA256的相同字节，运行载荷不变。受控及实际战斗的验证边界写回[战斗执行](battle-execution.md)。

战报平均数的原代码证据及本地接线见 [report-statistics-source-manifest.json](../../assets/reference/battle-rules/report-statistics-source-manifest.json)。原客户端读取两项平均数，共享数据类仅合并服务器结果，未取得单场生成逻辑；按用户确认使用伤害命中／释放次数及伤害命中／每次释放的去重目标数之和，闪避不计、免疫与全额盾吸收计入。原始计数及旧检查点未知标记随本地存档保存。有效护盾量另按用户确认取成功添加量，三类占比以同施放者各技能对应有效量之和为分母，治疗／护盾的生命比例以施放者最大生命值为分母；新状态在原属性解析后仅归因一次，保留历史缺项标记。已接原技能数值行及用户确认的显示取整、伤害／治疗／增益／减益次数，正式入口和完整来源归因仍待完成。

技能战报数据行：`restore_battle_report_skill_row.py` 严格读取固定871版原 `FightEndReportSkillTabViewItem`，恢复35处序列化字段／引用与 managed-reference 显隐配置；原组件位置、布局及图形不变。迁移原数值刷新顺序、颜色和 Slider；直接复制5个原 `NumberFormatHelper` 方法并接入原中文缩写／百分比路径。平均数的显示取整为已确认本地规则；原始比值不变，伤害次数与治疗次数采用用户确认规则，满血治疗仍计次；增益／减益逐目标成功施加计次，叠层／刷新各计1、丢弃／失败不计，异常减益并入减益；旧存档未知项不补造，待记记录独立于活动实例并随检查点保存。另恢复3处原表头 GM 显隐配置，与数据行保持一致。图标管理器／详情／快照及正式窗口仍待接入。

依据：[report-skill-row-source-manifest.json](../../assets/reference/battle-hud/report-skill-row-source-manifest.json)。

战报间接状态来源：复用已取得871客户端／共享反编译结果，核对 `BattleReportDistributor`、`FightReportComponent`、`BattleReportCollectorApplyStatusByStatus／Cfg` 及 `FightStatusHpAccDecreaseComponentInfo／FightStatusSkillEndComponentInfo`；前者没有服务器聚合实现，后者明确累计损血触发的来源／目标均为持有者。按用户确认，状态直接触发新状态继承既定根来源，独立技能从该次技能起算，未知来源保留未归因。`FightBattleStatusReportSource` 将统计来源与实际施加者／属性／衰减字段分开，接入 HP 阈值、累计损血、技能结束追加状态及其次数／盾量统计；不修改官方资产或配置。专项回归及模拟输入 Play Mode 验证原60840→60841和检查点来源保存，完整被动初始化／未接事件仍待迁移。

依据：`assets/reference/battle-rules/report-statistics-source-manifest.json`。

玩家战报状态评分：直接提取871 APK 的 `defaultpackage_assets_scriptableconfig_modules_fight.bundle`（MD5 `5d9d9996fe22bcca8f9d3bee08280c8c`）中 FightSettings 对象 `-8095793369706998440` 的完整 `ReportSetting`，输出 `unity/Assets/Official/Combat/report-score-settings.json` 及对应加载来源清单，绑定 `OfficialCombatAssetCatalog`。复用反编译 `FightSettings.GetReportApplyStatusScore` 与 `FightReportUIUtils.GetApplyStatusScore`；保留5／5／10状态分、破韧系数1、原阶段列表、null字典提前返回和原计算顺序。官方对象／代码、输出与绑定哈希见 `assets/reference/battle-rules/report-score-settings-source-manifest.json`。服务端计数代码缺失处按用户明确确认实现：BeAbsoluteDamage 包含护盾、溢出及防御／减伤抵消部分，累计既有官方 AbsoluteDamage 字段；破韧按敌方韧性正数归零次数计1。保留普通／异常减益分类次数并接到原评分公式，已知施加者无根技能仍进入角色总分；旧存档没有对应历史保持未知。计数及原配置已通过专项回归，头像、未确认代理类型分组及正式入口仍未完成。

玩家战报列表及归组：从同版官方 `fightendreportplayertabview.bundle` 的 `FightEndReportPlayerTabView` 恢复原 `PlayerSlot／PrefabObj` 两字段、槽与行资源绑定，复用清理子项、实例化、队伍四项合计和重新启用刷新。用户确认 NPC 各占一行、召唤物的召唤物归最终主人的召唤物报告；本地按报告快照 MasterId／CharacterType 归组，保留阵亡／离场单位和未归组原因，不修改玩法主人关系。缺失主人、循环、跨阵营及未确认代理类型不默认归到主角，也不生成完整队伍分母。官方服务器聚合器为空，因此该部分为用户确认的本地补足；宠物召出的召唤物按用户确认归宠物栏，进一步召唤沿该来源分类；代理类型仍有缺项。源对象、字段、代码和输出哈希见 [report-player-view-source-manifest.json](../../assets/reference/battle-hud/report-player-view-source-manifest.json)，计数快照见战报统计来源清单。头像／名称／点击资料、正式窗口及完整战斗发布尚未完成。

战斗替身使用官方12200／12201／4600实体及12202寿命、10140同步技能，原始组件、配置、描述与用户确认的单机裁决分别记录于 [替身来源清单](../../assets/reference/battle-rules/voodoo-doll-source-manifest.json)。`scripts/prepare_battle_voodoo_doll.py` 记录输入；固定真伤绕盾、溢出传导与减益转移为用户已确认的离线执行接线，原生模型和完整演出尚未完成。

### 公共模块重构验证范围

竞技场磁盘恢复：`HomeBattleHostPlayModeAcceptance` 在真实战斗第3步暂停，核对玩法实际保存的完整检查点后销毁 runner 与主页，再用相同隔离档案重新创建主页及 runner。恢复后的检查点序列化逐字一致，覆盖回合、单位位置／生命、技能 CD、状态、随机状态和统计；不保留临时暂停，不重走准备阶段。恢复后继续实际战斗至结算，挑战次数与战绩只更新一次，并检查相机及主页显隐恢复。已通过 Unity 编译、`ProjectTools.Validate`、静态检查与带画面的 Play Mode；同进程内重新打开磁盘档案不等同于 Windows Player 整进程重启，其余玩法及准备阶段的恢复仍待验证。

技能试伤调用方已在主工程内置屏带画面 Play Mode 通过：从主页人物／技能入口模拟点击进入原参数页，实际运行10回合公共战斗，显示胜利结算、动态胜利姿势、玩家／技能统计与技能快照详情，返回设置后关闭。隔离档案物品数量保持不变且未创建战报历史目录；十张运行画面已查看。受伤姿势另作模型显示探针，不计为实际败北流程验证。启动工具显式打开 Bootstrap、隔离模式使用自身档案并跳过被抑制的应用启动等待，普通启动等待有30秒上限，资源校验失败自动退出1。主页入口画面仍见主角缺失与黑白多边形，记录于[主城与家园](city-and-home.md)，不据此宣称主页画面通过。

现有 `OfficialMap10BattlePlayModeAcceptance` 已增加公共接口回归：无效时间参数不改变会话、重复启动不替换现有会话，以及准备／暂停／当前步骤演出期间的大时间步禁止越过门控；原有末次命中等待和结束阶段检查继续保留。测试通过调用方同一 `Begin/Tick/Checkpoint` 接口检查结果，不读取执行器或播放计时器内部字段。该组新增回归已在主工程内置屏 Play Mode 模拟输入中通过，进程退出码为 0。测试存档创建在 `ZJCS_VALIDATION_DIR` 指定的隔离会话内。验收入口将运行时错误记录到待处理状态，由下一次编辑器更新结束测试，避免在原生资源加载的日志回调栈内退出引擎；对照失败场景已正常返回退出码 1，未再出现栈溢出。输入工具只通过实际 EventSystem 射线与指针事件分发模拟点击，已移除真实鼠标和抢前台分支；批处理下保留单个 Game View，截图尺寸与输入视口同步。

已执行并通过：`test_battle_step_timing.ps1`、`test_battle_phase_machine.ps1`、`test_battle_checkpoint_persistence.ps1`、`test_battle_cooldown_status.ps1`、`test_battle_report.ps1`（使用 PowerShell 7）。它们验证已有规则时序、阶段、序列化恢复、CD 与统计，不代表新请求接口或场景接线已通过运行验收。综合 `test_unity_combat.ps1` 因缺少未受 Git 管理的 `assets/reference/battle-rules/damage-reference.json` 停止，未生成替代预期值。

Unity 2022.3.57f1c2 许可证已恢复，内置屏启动的主工程已完成运行时及编辑器程序集编译；用户授权手动补入的 `OfficialMap13NpcFollowerView` 和创角验证程序集的 State 依赖修复了类型编译缺口。`Zjcs.Editor.ProjectTools.Validate` 已通过战斗规则、配置、HUD 与 Map10 资产检查。首次全量导入另出现既有 `TMPEffect.OnValidate` 空引用和 Spine 自动导入原 Atlas 的异常；前者已按字体绑定就绪状态修复，并通过原文字效果完整 Play Mode 验证，后者仍属于原 Atlas 自动导入问题，故不将首次导入称为零异常导入。Map15 档案多余的构造参数已修复并经再次编译确认不再报错。Map10 战斗已通过模拟指针回归并实际查看七张准备／播放／缩放／聊天／返回地图画面；日常副本、竞技场和幻境之塔已另通过直接调用公共 runner 的实际战斗与结算回归；完整游记 UI 入口、冷存档恢复及 Windows Player 构建发布仍须验证，不能将接口重构标为完整战斗迁移完成。Map13 手动显示实现及限制维护于[泽之国](../maps/map-13.md)。`python -B scripts/check_unity_project.py` 已通过，此前发现的六个既有资产目录缺少 `.meta` 已补充稳定目录 GUID，未改动资产载荷。

NPC 外观复用验证：`OfficialMap12NpcBattleModelView` 是现有 Map12 队伍战斗的原生模型适配器。隔离 Unity 2022.3.57f1c2 内置屏 Play Mode 使用当前编译程序集和同版原 NPC 数据，调用其构造、`Play("idle_01")`、`Tick` 并实际渲染8402／8440／8441／8442，分别得到469／483／606／444个网格顶点；四者均绑定 `Texture2D.whiteTexture`，四张运行画面均为白色多边形，原贴图检查4／4失败。因此它尚不能作为已验证可用的公共 NPC 显示实现。本次未将其复制为 Map13 跟随者。代码核对另确认 `BuildMaterials` 使用白纹理并按批次序号取材质，`EvaluateNativeAnimation` 只应用局部动画通道，未执行 NPC 的 IK／transform／path／physics 约束。须先恢复原图集页、原材质及批次页／混合模式绑定和完整约束，再验证实际技能动作、位移、受击、结束与存档流程；数据读取、非空网格和待机名称解析不能作为可用证明。用户授权手动实现的 Map13 显示类另行恢复了原图集、材质和部分待机约束，四类彩色动态网格检查已通过；完整物理约束和正式流程未通过前不替换现有战斗适配器，具体范围见[泽之国](../maps/map-13.md)。

## 尚未实现

战斗挂接角色的 Animator 参数播放接入既有官方 `AnimationController`／`MyPlayableAnimator` 与新增迁移的 `MyPlayableOverrideAnimator`，由 `OfficialBattleAnimatorPlayback` 绑定原 cue 字段并驱动官方 `MyPlayableCenter` 的两个帧阶段；移除本地重复的参数栈和片段覆盖实现。原组件负责前置状态检查、参数层、持续时间、速度及停止恢复，片段覆盖保持在前置状态检查之前。每个兼容的挂接角色保留独立控制器副本及既有覆盖，退出恢复共享控制器。新增类来自当前 `884_156692` 已核验客户端；三个既有依赖的当前原源码与其历史迁移凭据逐字节一致，原技能 cue／clip 载荷仍保留各自历史版本，不混改资产来源。代码／依赖输入与生成哈希见 [Animator 运行来源](../../assets/reference/battle-rules/animator-runtime-source-manifest.json)。独立参数夹具已在带图形设备的 Play Mode 验证无限／有限／零时长、零速与倍速、参数恢复、前置状态、覆盖顺序及副本恢复，Unity 导入／编译、`ProjectTools.Validate` 和 132 个片段的固定来源校验通过；夹具不代表原战斗模型的动作画面。

男女主角资源进入战斗时仍从原生预览 prefab 创建，但挂接后会由 `CharacterNativeAttachmentGeometry.SourceSex` 识别男女，并按 `100/200` 职业组换入已核验的 `Male/Female_Zhanshi/Fashi_Controller`；离开战斗时恢复预览控制器。没有几何性别来源或控制器资源时保持原控制器并跳过参数播放，不伪造参数或把调用成功计为动作完成。完整序列／淡入淡出字段、技能结束移除通知、骨骼／粒子／音频及各战斗模型仍未迁移齐全。本项未构建或发布 Windows Player，完整技能演出仍未完成。

原版战斗控制器：`AppearanceAddressUtils.GetPlayerAnimatorPath` 按 `PlayerPhase.Game`、性别与基础职业选取 `Male_Zhanshi_Controller.controller`／`Male_Fashi_Controller.controller` 及对应女性 override。`prepare_battle_game_animators.py` 从当前 `884_156692` 原生转换缓存恢复四份控制器及33个片段到 `Assets/Official/Combat/GameAnimators/Resources/Combat/GameAnimators/`，只重绑定外部 GUID；逐文件核验转换凭据，并从 APK 优先／校验云缓存的原对象核对参数顺序、类型和默认值，状态／动作／默认状态，过渡顺序、条件、时间和目标，及女性 override 的原空覆盖项。战士原22参数／21状态／39过渡，法师原19参数／21状态／37过渡，未统一删改。来源对象、包／APK、原生转换和输出哈希见 [Game 控制器来源](../../assets/reference/battle-animation/game-animators-source-manifest.json)。Unity 导入／编译、`ProjectTools.Validate`、静态元数据检查和内置屏 Direct3D 12 Play Mode 通过；四份原控制器实际进入原待机、技能和受击状态，官方 Playable 成功覆盖原 `Skill` 槽并恢复共享控制器，进程正常退出。`OfficialBattleSceneView` 现在从原生人物几何组件读取性别，按 `100/200` 职业组自动绑定相应 Game 控制器，并在战斗释放时恢复预览控制器；怪物／Spine 模型不走此替换。现有技能片段仍保留历史来源版本；控制器片段的原 Spine Mecanim 名称载荷需要独立原生骨骼消费者，完整技能特效、音频、正式 Player 流程和 Windows Player 发布仍未完成。新增小型载荷随代码提交，未生成素材完整快照，不声称已备份。

原生约束通道：`ProfessionNativeAnimationChannels.Constraints` 按需绑定 `ProfessionNativeConstraintAnimation`，读取原 IK 的 mix／softness／离散弯曲、伸缩标记及 transform 的六轴混合、局部／相对标记和偏移，保留原约束顺序、皮肤筛选与第一帧前的 setup 值；支持线性、阶跃及各轴独立贝塞尔时间曲线。复用原 `871_156598` 已校验、无损打包的人物数据，不从旧 Spine runtime 复制／翻译代码，也不把它改标为新控制器的 `884_156692`。原数据、既有凭据、独立采样实现和夹具生成哈希见 [约束通道来源](../../assets/reference/battle-animation/constraint-channels-source-manifest.json)。内置屏 Direct3D 12 Play Mode 完成男性445／女性451条动画的绑定及14,868个关键帧、区间和延迟首帧前采样检查（男性7,481、女性7,387），其中原关键帧6,743个；贝塞尔期望值由独立数学求根生成，另核对原 `100/skill_06` 的 IK 关闭／恢复、离散标记、皮肤筛选、约束顺序与非法输入。Unity 导入／编译、`ProjectTools.Validate` 和静态元数据检查通过，进程正常退出。本项只恢复约束参数采样；尚未接完整世界约束求解、物理、混合、人物渲染或正式战斗动作，也未构建／发布 Windows Player。

约束求解前后接线：`ProfessionNativeConstraintPose.EvaluateBeforePhysics` 将原局部骨骼／插槽／附件通道与已恢复的多帧约束采样接入既有独立几何求解器，骨骼和约束使用同一个实际归一化时间，每次从原局部姿势重新求值。按已核实的原人物顺序先执行 IK、再执行 transform；显式保留皮肤筛选，拒绝交错顺序、未知世界约束类型及尚未实现的 IK 分支，不将物理约束静默当作已执行。原输入与独立求解器绑定哈希见 [约束姿势来源](../../assets/reference/battle-animation/constraint-pose-source-manifest.json)。内置屏 Direct3D 12 Play Mode 通过男女 `100/skill_06`、`200/skill_06`、`100/skill_50`、`100/skill_42_XX` 在0／0.4／0.7／1.1秒的32个采样点，检查满权重两骨末端到目标／可达边界、原关闭 IK 后的局部旋转、重复求值无累积、各动画实际归一化循环时间和非法时间输入；已查看两张技能06的原服装网格截图，Unity 导入／编译、`ProjectTools.Validate` 与静态检查通过，进程正常退出。循环测试使用同一浮点取模结果，避免把阶跃边界两侧误判为相同时间。此入口明确是未混合的物理前阶段；本段夹具仅含服装，完整核心外观覆盖见下文，物理、动作混合与正式 Player 尚未接通；既有非单位缩放／剪切 IK、局部缩放／剪切、相对 transform 与部分骨骼继承限制仍保留，不代表全部技能或皮肤动作完成。本项未构建／发布 Windows Player。

局部服装约束与原图集：补齐原 `body_0_G_014`、`body_0_G_012` 两类皮肤的局部绝对旋转／XY 约束，按原偏移和独立平面矩阵在服装父骨骼坐标中求值，区别于世界坐标复制；语义核对 [原变换约束说明](https://en.esotericsoftware.com/spine-transform-constraints)，没有读取或复制动画 SDK 实现。`ProfessionNativeBonePose` 显式记录世界覆盖及其失效，已受世界约束的目标／骨骼需先恢复 applied pose 分解，当前明确拒绝，避免把旧动画局部值作为求解后局部值。共享实现与原字段哈希见 [约束姿势来源](../../assets/reference/battle-animation/constraint-pose-source-manifest.json)。`prepare_battle_local_clothing_textures.py` 按历史 `871_156598` 高分辨率加载器映射恢复6张原主图／染色图／占位图，为男女追加4个本地图集绑定，保留已有15项。先核对当前 APK 的实际包／对象，再核对已下载云缓存；主图和染色图的整包 MD5／尺寸／SHA256及对象原字节 SHA256 与旧凭据一致，当前缓存只作为相同字节的新存放位置。原占位包与当前包不同，另从官方 CDN 取回旧包，按既有整包 SHA256 校验后提取，不替换原历史身份；来源与全部输入输出哈希见 [局部服装图集](../../assets/reference/battle-animation/local-clothing-textures-source-manifest.json)。内置屏 Direct3D 12 Play Mode 核对6张原纹理／6个 GPU mip、8,569,112字节的精确读回哈希、原采样设置及完整 GUID／占位图引用，并对男女两类皮肤的4个原技能各取4个时点，共64个局部约束采样点核对父坐标矩阵；不同父节点和分轴混合的独立数值夹具、世界覆盖失效／拒绝旧局部值检查通过。原常规服装32个约束点、循环时间及复位回归通过，已查看新增四张原服装网格和两张常规服装回归截图；Unity 导入／编译、`ProjectTools.Validate`、静态检查通过，编辑器正常退出。首次缺图集绑定及后续目录 YAML 解析失败均已修复后重跑。画面仍是未混合的物理前衣装夹具，未接完整头部／复合人物、物理、动作混合或正式战斗／Windows Player；新增素材未进入完整快照，不声称已备份。

完整核心外观的战斗动作检查：复用原 `BuildDefaultAppearanceData → ProfessionAppearancePreview → AppearanceCharacter → AppearanceSpine`、实际本地角色资源和染色链，按原 `AppendSkinType.Game` 只追加 `performFace`，不混入 UI／地图的 `map_01`。男女战士／法师四组原默认核心皮肤均包含头发、脸型、眼睛、眉毛、嘴、肤色和职业衣装；没有手填皮肤名或配色。`RunCorePoses` 将这些完整组合接入上述 `EvaluateBeforePhysics` 阶段，核对原全部435条 `100/`、`200/` 战斗动作，在原 IK／transform／physics 参数关键时点及区间／末尾共2,322个采样点完成几何、有效顶点、原页材质绑定和来源时长检查（男性737／422、女性737／426）。输入、原加载／外观链凭据、运行与夹具哈希见 [核心人物动作来源](../../assets/reference/battle-animation/core-pose-source-manifest.json)。内置屏 Direct3D 12 Play Mode、Unity 导入／编译、`ProjectTools.Validate` 和静态检查通过，编辑器正常退出；8张原技能06／50／13的完整核心人物 GPU 图均已查看，使用已恢复的原染色 shader 和实际原图集。这是诊断取景下的未混合、物理前姿态覆盖，不是原版完整演出对照；独立武器／部件、物理模拟、动作混合、正式战斗角色绑定、输入／存档及 Windows Player 构建／发布仍未完成。

场景武器／部件应用调用链：迁移当前 `884_156692` 的 `AppearanceRendererPartHandler`、`AppearanceMecanimPartHandler`、`AppearanceAnimationPartHandler` 及两个原部件条目结构；复用既有原 `AppearancePartHandler`、部件提供者、资源地址和 `AppearanceSkeletonPart`。保留按类型及皮肤名复用、主武器／背饰池化、替换前归还、特效仅扫描直接子节点、挂点去重／动态分隔、先重建网格再初始化跟随者、完整释放时卸载标记与清除分段材质的原分支和顺序。动作通知按原动画对象身份去重，不按名称去重，也不额外过滤层、零权重、时间或倒放；换装保留上次动画对象，停用解绑并在启用时恢复订阅。原 `AppearanceSkeletonPart` 仍按配置构建源动作白名单并以源动作名循环播放，未擅自改为目标动作名。SDK／框架调用通过显式端口适配，没有复制动画 SDK；输入程序集、6份原应用源码、既有依赖与迁移输出哈希见 [场景部件来源](../../assets/reference/battle-animation/renderer-parts-source-manifest.json)。内置屏 Direct3D 12 Play Mode 使用隔离端口探针核对上述复用／替换／释放、分隔顺序、材质回调单次订阅、部件原缩放恢复、动作身份／低权重通知、启停订阅及真实原配置的源动作白名单；Unity 导入／编译、`ProjectTools.Validate` 和静态检查通过，进程正常退出。本项仅验证原应用调用链，尚未接场景武器 prefab、实际分段渲染／Mecanim 后端、真实武器画面、输入／存档或正式战斗／Windows Player，不能视为武器功能完成。

场景武器资源与轻量渲染器：`prepare_battle_scene_weapon_sources.py` 按当前 `884_156692` 索引先核对 APK 内实际剑／法杖 Integrated prefab，保留两个完整四节点层级、原组件字段／指针、挂点与变换；通过模型、图集和高分辨率加载器对象的完整字节身份复用已有原生资源。当前场景 shader 与历史 UI 武器 shader 不同，按当前包原对象身份复用已校验的 Windows HLSL 转换，保留544组原程序、两个 pass、状态和材质参数，仅改 ShaderLab 资产名以隔离旧版资源。来源、原始与输出哈希及显式目录绑定见 [场景武器输入](../../assets/reference/battle-animation/scene-weapon-source-manifest.json)。`prepare_battle_scene_renderer_runtime.py` 迁入当前原应用的 `AppearanceRendererLite`、`SkeletonRendererTextureLoadHandler` 和材质替换组件，SDK 材质页查找／渲染调用使用显式端口，框架句柄接本地资源；保留共享／独立选择、去重、按需纹理、引用、现有网格替换和销毁顺序，见 [轻量渲染代码来源](../../assets/reference/battle-animation/scene-renderer-runtime-source-manifest.json)。内置屏 Direct3D 12 Play Mode 完成544组原程序的1,088个 D3D 阶段编译，男女战士／法师435条原动作共1,305个物理前采样帧，核对原附件对象显隐、骨骼位置跟随、有限网格、共享转独立材质和按需纹理释放；其中1,239帧武器可见，8张完整核心人物与剑／法杖的实际 GPU 合成图均已查看，原场景 shader 的武器单独像素检查通过。Unity 导入／编译、`ProjectTools.Validate` 与静态检查通过，编辑器正常退出。首次层级夹具将 JSON 根节点的空父级当作对象键，已修正后完整重跑通过。这些来源层级尚不是正式场景 prefab，当前端口与分段合成仅用于诊断；完整 MeshRenderer 字段应用、人物分段深度、完整物理／动作混合、正式战斗输入／存档及 Windows Player 构建／发布仍未完成。

当前场景人物源数据：`prepare_battle_scene_core_sources.py` 从所选 `884_156692` 索引按 APK 优先恢复 `PlayerModel_Game` 原三节点、完整组件字段和资源 GUID，独立保存原核心骨骼及全部分离动作；APK 本身仍记为 `881_156673`，没有把历史 `871_156598` 资源改标。当前男性为1,665骨骼／507皮肤／448动作，女性为1,926骨骼／508皮肤／455动作；完整原附件、图集映射、IK／transform／physics 字段及皮肤约束归属均保留。原场景绑定 `character_model_TextureLoader_Low`（702项占位映射、原 Low／染色 Low 路径），没有将此前高分辨率诊断夹具当作场景默认值。输入、原对象／包／APK、独立 MIT 格式读取器、历史凭据、输出哈希和加载绑定见 [当前场景人物来源](../../assets/reference/battle-animation/scene-core-source-manifest.json)。`Combat/SceneCharacters` 的独立载荷由 `ProfessionNativeSceneCharacterAsset` 按当前版本及双内容哈希加载；既有加载入口仍严格要求历史版本，两个版本互换均拒绝。内置屏 Direct3D 12 Play Mode 实际加载男女完整数据，绑定全部903条动作的局部／颜色／约束／变形／物理参数通道，在起点／中点／终点检查2,709个局部姿态采样点、有限数值与插槽绘制顺序；Unity 导入／编译、`ProjectTools.Validate` 和静态检查通过，进程正常退出。此项未做当前人物 GPU 画面验证，三节点记录还不是可运行场景 prefab；当前纹理／材质接入、Mecanim／分段渲染后端、物理、动作混合、正式战斗输入／存档及 Windows Player 构建／发布仍未完成。新增资源未进入完整快照，不声称已备份。

原生物理参数与开关：`ProfessionNativeAnimationChannels.Physics` 按需绑定独立的 `ProfessionNativePhysicsAnimation`，直接读取原 `871_156598` 无损人物数据中的3,301个物理约束，保留骨骼、全局索引、原执行顺序、皮肤归属、作用轴、原单位限制／步长、惯性／强度／阻尼／质量倒数／风力／重力、混合权重和全局标记。缺省值按固定 MIT 二进制格式读取器的无损投影核对，区分 setup 权重默认为1和动画帧省略值为0；全部实际动画仅包含 mix 通道，其他未接通通道或全局目标明确拒绝。未读取、复制或执行动画 SDK；原载荷、格式读取器、独立运行实现与夹具哈希见 [物理参数来源](../../assets/reference/battle-animation/physics-channels-source-manifest.json)。内置屏 Direct3D 12 Play Mode 完成男女896条动画、774条 mix 通道、全部初始值及513份皮肤归属检查，核对1,318个原关键帧和区间／首帧前／末帧后共3,724个采样点（男性1,370、女性2,354），覆盖原技能06的物理关闭／恢复、倒序求值、动作切换、快照隔离、非法输入和共享通道接线。Unity 导入／编译、`ProjectTools.Validate` 和静态检查通过，进程正常退出；IL 后处理器首次连接重试后恢复。本项仅恢复未混合的物理参数，不推进物理模拟或改变骨骼世界矩阵；物理积分、完整人物动作／混合、正式战斗与 Windows Player 构建／发布仍未完成。

原生附件变形：`ProfessionNativeDeformAnimation` 从同一份已校验的 `871_156598` 无损人物数据绑定全部8条单关键帧通道（男女各4条），按原皮肤、插槽和共享附件对象归属采样，不把同名的其他附件绑定为同一附件。`ProfessionNativeAppearanceSkeleton` 将源单位偏移传入顶点／裁剪批次；各骨骼影响先加偏移再乘原矩阵与权重，保留未归一化的原权重及最终 DataScale，切换动作、附件或 setup 时清除旧变形。原数据、独立实现和生成夹具哈希见 [附件变形来源](../../assets/reference/battle-animation/deform-source-manifest.json)。内置屏 Direct3D 12 Play Mode 完成全部896条动画的附件通道绑定、8条原偏移及逐顶点矩阵结果、对象隔离、延迟关键帧、复位和非法输入检查；原 `100/skill_50` 男女服装裁剪批次实际产生不同顶点，四张重跑截图含原图集像素并已查看，编辑器正常退出，Unity 导入／编译、`ProjectTools.Validate` 和静态检查通过。首次拍摄夹具把相机与网格一起移动导致空图，已修正并重跑；当前图片仅是约束求解前的原服装网格／遮罩夹具，未接头部等完整复合外观、世界约束、物理或动作混合，不计作正式技能演出。源数据的8条均为单关键帧，其他多帧附件变形明确不支持。本项未构建／发布 Windows Player。

此接线后的内置屏 Play Mode 回归覆盖日常副本、竞技场与幻境之塔公共 Runner：模拟倍速输入，分别运行至计数388／26／62的真实结算，核对场外角色／相机恢复；已查看三场战斗及全部11张准备、返回与入口画面，缺失 Animator 参数警告为0。Direct3D 11 的流程断言通过但编辑器退出挂起；Direct3D 12 重跑相同流程通过且正常退出，不能据此认定前者的退出问题已修复。此前三处探针确认预览控制器参数为0；本次运行时绑定会在人物几何已初始化且性别可读时换入官方 Game 控制器，因此公共流程回归同时覆盖控制器选择与释放恢复，仍不把怪物／Spine 或未接完整特效路径计为技能演出完成。主城黑白几何、游记界面与公共 HUD 的既有缺项仍保留。入口窗口点击与完整界面表现也未纳入本项完成范围。

- **会话和准备**：完整就绪／取消就绪／方案刷新、所有控制权与中途加入接口、完整阵型／高度／身体占格、入场／结束过渡、离开／后台条件和准备冷启动恢复。
- **回合、AI 和技能调度**：完整 `AiType` 评分、嘲讽／混乱、所有技能资格条件、方向与位移规则、逐 Delay 分帧执行、实体等待、负时长、中断／销毁，以及其余事件的重入／完整触发顺序。连续触发开关及状态白名单已按上文确认规则接入；当前仍有同一 Step 内同步处理的路径。
- **命中、伤害和状态**：Scope 旋转／逐格延迟／过滤条件、其余命中组件、完整伤害阶段钩子、全部状态事件／来源约束／快照／转移／持续变化，以及护盾、HP 限制、CD、韧性、元素、固定直伤和耗盾组合的全量联动。
- **召唤和结果**：宠物、格子物件、智能物件及其他召唤的完整继承、数量、寿命、销毁、复活消费者和非命中 HP 变化；各外部玩法的结果策略、资源事务和奖励由所属页接入。
- **HUD、战报和公共控件**：全部时间轴事件和头像、真实 Boss 视野、单位／阶段状态详情、技能品质／能量、正式战报入口、回放／快进／托管／背包／观战／聊天及完整语言字体服务；公共请求／推进接口已覆盖现有入口，跨玩法运行回归仍待完成。
- **画面和音频**：完整复合外观、Animator 状态切换／过渡／混合、生成／复活／召唤特效、Playable 精确停止／中断、范围／弹道／挂点／排序、相机环境、粒子、材质、shader、飘字合并和音频速度同步。
- **验证和发布**：通用战斗全流程 Play Mode 模拟输入、真实画面／遮挡／缩放／声音、重启续战和 Windows  Player 验证；完成前不能声称战斗功能完成或发布对应版本。

当前场景外观应用生命周期：`prepare_battle_scene_appearance_runtime.py` 将当前官方 `AppearanceSpine`、`AppearanceRenderer`、`AppearanceMecanim` 迁入 `Zjcs.Official.CombatAppearance`，通过显式程序集引用接入既有外观程序集，历史871 UI 基类仍独立保留。直接复用原皮肤合并／染色、按 GUID 先加载纹理加载器再加载皮肤配置、同资源跳过／异资源释放后初始化、一次性组件订阅和缓存创建、换肤时先 Mecanim Update(0)／缓存通知再归还材质并重建网格、结束与销毁的原分支和执行顺序；框架资源句柄及渲染／Mecanim／缓存使用显式端口。纠正既有分段材质端口为官方 `void(Material[])`：就地替换整组材质、忽略空项、按原材质去重并共用实例，只在新增原材质时通知，不把分段重新克隆成独立一套。来源、原共享类型、程序集／迁移／端口／生成器与夹具哈希见 [场景外观运行时](../../assets/reference/battle-animation/scene-appearance-runtime-source-manifest.json)，分段端口凭据同步 [场景部件来源](../../assets/reference/battle-animation/renderer-parts-source-manifest.json)。内置屏 Direct3D 12 Play Mode 使用实际当前男性完整骨骼数据和隔离资源／渲染器／Mecanim／缓存探针，核对 GUID 句柄顺序、同资源与同布局异资源初始化、订阅和缓存只创建一次、主网格／分段数组共享、空材质与重复页、换肤归还、完整释放、销毁顺序，并重跑原全部部件生命周期检查；Unity 导入／编译、`ProjectTools.Validate` 和静态检查通过，编辑器正常退出。缺少程序集引用及探针 Update 消息签名问题均已修正后重跑；生成器复现时保留相同内容的源码和既有元数据，避免触发无关重编译。此项仅验证应用调用链，渲染器与外观组件按原同对象生命周期使用；没有执行正式 GPU 场景、跨性别骨骼切换、骨骼缓存算法、物理或动作混合。当前纹理／材质、Mecanim／分段渲染与缓存后端、原场景 prefab、正式战斗输入／存档及 Windows Player 构建／发布仍未接通。

当前动态场景分段：`prepare_battle_scene_separator_runtime.py` 从当前官方应用源码迁入 `DynamicSkeletonRenderSeparator`，保留按分隔槽数加一／额外段数初始化、倒序回收／FIFO 复用、只对新对象设置排序、可见性通知、仅销毁活动列表并保留池的原分支。SDK 基类操作通过显式端口接到独立 `ProfessionNativeSceneMeshSeparator`／`ProfessionNativeSceneMeshPart`：按实际绘制顺序消费已在分隔槽处断开的原生批次，生成 Unity 网格／子网格，整组材质交由原外观回调就地替换，各段共用实例；空段清理旧网格，部件销毁释放自有 Mesh，后端释放活动与池对象。来源和原应用／端口／原生后端／验证入口／生成器哈希见 [动态场景分段来源](../../assets/reference/battle-animation/scene-separator-source-manifest.json)。Unity 导入／编译、`ProjectTools.Validate`、静态检查和内置屏 Direct3D 12 Play Mode 通过；使用已核验当前来源的原剑、杖模型、各自原 prefab 初始循环动作、原裁剪／PMA 参数、原世界材质及贴图，完整与分段网格 GPU 像素逐字节相同，四张实际截图已查看。实际相机渲染验证可见分支，空列表验证不可见分支；同时核对池顺序、复用对象保留排序、新对象排序、共享材质、活动对象销毁后池仍可复用、活动／池网格释放及重复释放。验证中错误的技能动作名称已改为原 prefab 初始配置；批处理模式使用显式相机渲染请求，重跑后编辑器退出码为0。此项消费已完成的几何数据，未实现 SDK 的 OptimizeRender 算法、动作采样／混合／物理或生产人物渲染器；原场景人物、实际换装／武器跟随中的分段绑定及正式战斗／Windows Player 仍未接通。

当前场景 Low 资源：`prepare_battle_scene_texture_sources.py` 按所选 `884_156692` 的 APK 优先与已核验同版云缓存，恢复公共 Low 加载器的702组有序占位图映射、977个主图／染色图 GUID、764条皮肤配置和男女原图集的937份独立材质。原 ASTC 按 mip 解码为 Windows 原生 RGBA32，保留尺寸、色彩空间、采样、可读标记及全部材质参数／关键字／纹理变换；原世界 shader 字节与已验证的当前场景 shader 相同，按身份绑定。`ProfessionNativeSceneAppearanceResources` 只保存主图／染色图的独立本地 Resources 地址，`OfflineProfessionSceneTextureResources` 按原 GUID 同步加载，跨作用域共享实际资源并独立计数，最后一个句柄释放才卸载；作用域结束后已有句柄仍有效。来源与完整输入输出、工具、配置及绑定哈希见 [场景纹理来源](../../assets/reference/battle-animation/scene-texture-source-manifest.json)。静态检查、Unity 导入／编译、`ProjectTools.Validate`、内置屏 Direct3D 12 Play Mode 通过：1,681张原纹理的1,681个 mip GPU 字节哈希全部相同，937份原材质参数／关键字／纹理绑定及702组有序 GUID 映射通过，重复释放、跨作用域存活和原加载器按需释放通过；编辑器退出码0。已查看男女战士／法师四张原衣装与头发的物理前网格诊断图，未拼接完整面部、独立武器或默认复合外观。验证程序集缺少解析器引用已补齐；两项历史效果依赖缺少原生采样序列化版本，已从原字节单独恢复当前载荷后完整重跑。正式人物 prefab、Mecanim／缓存后端、物理、动作混合、正式战斗／存档及 Windows Player 构建／发布仍未接通；新增素材尚未进入完整快照，不声称已备份。

### 原版战斗系统

以下 F01—F43 是必须保留的通用功能范围，不代表当前全部完成。类名相对于上述官方客户端／共享源码目录。

### 会话、阶段、准备和结果

| 编号 | 原版功能 | 官方机制 |
| --- | --- | --- |
| F01 | 建立、加载、释放 | `FightRoot`、`FightSceneRoot`、`GamePlayManager` 创建战斗上下文、实体容器和场景对象，结束时停用并释放。 |
| F02 | 主面板与延迟 | `FightMainPanelLoader` 按普通、记录或回放模式加载面板，消费 `ShowMainPanelDelay`，取消时停止加载并释放。 |
| F03 | 阶段 | `GameStatus` 包含 Preparing、PlayStart、Play、Pause、EntryTransition、BattleEnd、BattleEndTransition、End 等状态；模式可跳过未使用阶段。 |
| F04 | 准备与就绪 | 准备倒计时、自动就绪、取消就绪、开始权限和 `SkipPrepareStatus/UserStart` 由准备面板与团队倒计时消费。 |
| F05 | 波次、回合和上限 | 波次、个人回合、全局回合分别维护；`GameProgressUI` 显示 `MaxRound`、个人回合上限和临界提示。 |
| F06 | 胜负与结束 | 玩家／敌方死亡、守护目标失败、决斗结果等由结果组件输出，公共层播放结束演出、清理并交接结果。 |
| F07 | 离开与切出 | `FightMainPanelLeaveHandler` 和切出控件按阶段、身份、观战／录像状态及离开策略处理确认和离开。 |

### 格网、站位、移动和目标

| 编号 | 原版功能 | 官方机制 |
| --- | --- | --- |
| F08 | 场地、阵型、占格 | `FightSceneInfo`、`MyFightGridGround`、`MyFightRoleGrid` 保存坐标、高度、身体占格、地形／边界阻挡、出生偏移、方向和翻转。 |
| F09 | 准备拖拽 | `PrepareBattlePlayerDrag` 校验准备阶段、控制权、拖拽阈值、射线格点、最近合法格、占位冲突、回退和拾放演出。 |
| F10 | 调整区域与交换 | `ChangeBattlePosHandler` 按调整区域、出生列表、队伍偏移和控制权决定合法落点及占位交换。 |
| F11 | 中途加入站位 | `ChangePosHandler`、`PrepareBattlePlayerSoulDrag` 按 `JoinInMiddle` 和加入权限预览并提交位置。 |
| F12 | 寻路与位移 | `AStarUtils`、`MyFightGridMover`、`MyFightActionSkillMove` 消费 MoveDist、MoveMask、占格、路径、时长和朝向；技能移动另有延迟、条件和方向。 |
| F13 | 目标与施法方式 | `HitTargetType` 区分敌友、自己、主人和召唤物；`SkillTargetType` 区分点、实体、可站立点、方向、跟随和锁定，范围按近到远展开。 |
| F14 | AI、嘲讽、混乱 | `AiPriorityId/LastAiPriorityId` 引用策略列表，并结合属性最值、角色分组、忽略状态和控制状态选择动作。完整评分器属于服务器缺口。 |
| F15 | 单位详情 | `FightRoleClickInput` 校验查询条件后打开单位详情或执行回调；普通点击不等同手动攻击。 |

### 技能、命中、结算、状态和召唤

| 编号 | 原版功能 | 官方机制 |
| --- | --- | --- |
| F16 | 技能装配 | 技能提供者按物品、实体、等级、品阶、属性、CD、被动和来源创建 `FightSkillAgentComponent`。 |
| F17 | 消耗、CD、次数 | `CostType`、有效消耗、起始重置、限定次数和波次重置共同决定施放资格；状态可修改或停止 CD。 |
| F18 | 动作生命周期 | `ActionDuration`、`Duration` 和等待 Hit、Move、Damage、子技能的条件分别决定动作结束；负时长可表示不自动结束，中断另有通知。 |
| F19 | 额外、重放、子技能 | `FightSkillExtSkill`、`FightSkillReplayCfg`、重放次数、子技能和状态白名单限制来源、条件、递归和次数。 |
| F20 | 命中图 | Fixed、NormalNext、Chained、RandomTarget、CustomTarget、TriggerSkill 等组件展开目标、范围、追踪、延迟、临时属性和附加效果。 |
| F21 | 伤害与治疗 | 共享公式处理攻防、等级／位阶、元素、职业／类别、暴击、格挡、衰减、HP、护盾、绝对伤害和反馈标记。 |
| F22 | 状态生命周期 | 状态支持计时、无限持续、平行／替换、可驱散、叠层／叠时及上限、同来源、属性快照、死亡禁用和复活重启。 |
| F23 | 状态触发 | 开始／结束、个人／全局回合、行动、技能、命中／受伤、死亡／击杀、层数变化、条件检查、转移、移除、持续变化和累计伤害都可触发。 |
| F24 | 控制与防护 | 属性变换、行动条、HP 限制、CD、重放、普通／持续／一次性／无敌护盾、韧性和多层 HP 按组件协同。 |
| F25 | 召唤与附属单位 | 怪物、宠物、格子物件、智能格子物件、替身保留主人、来源、属性继承、数量、寿命、存活和销毁关系。 |

### HUD、公共控件和战报

| 编号 | 原版功能 | 官方机制 |
| --- | --- | --- |
| F26 | 回合时间轴 | `GameProgressUI`、`GameRoundUI` 按 NextTime 队列显示回合、阵营、头像、主角色标识及行动／受击／死亡／插入移除动画。 |
| F27 | 名称、血条、死亡 | `GamePlayerHUD`、`GameCharacterHpUI`、`FightRoleFightHpView` 和 `BattleHpAndShieldHandle` 绑定名称、HP、护盾和死亡标识。 |
| F28 | Boss、韧性、多层 HP | `FightMainPanelBossUI` 显示敌方视野内最多两个 Boss，绑定名称、头像、状态、多层 HP／盾和韧性；最大韧性为0时隐藏。 |
| F29 | 状态详情 | `FightStatusController` 池化状态图标，响应应用、结束、禁用、层数、回合和自定义计数，并提供详情输入。 |
| F30 | 阶段状态 | `FightMainPanelStageStatus` 汇总阶段和实际状态、去重并消费显示黑名单；点击后打开状态明细。 |
| F31 | 技能槽和宠物能量 | `GameSkillIcon` 显示 Ready、Applied、InCD、Ended、LimitedTimes、ProfessionLimit、Disabled；辅助技能另有品质、能量和无宠物占位。 |
| F32 | 准备方案入口 | 准备时可取消就绪、打开方案、校验职业／禁用／宠物提示并刷新战斗快照；方案编辑由技能功能页维护。 |
| F33 | 倍速与锁定 | `FightMainPanelSpeedUI` 根据条件和身份生成档位，分离文案与倍率，并在结束或停用时复位。 |
| F34 | 快进 | `FightMainPanelFastForwardUI` 处理阶段门槛、权限、延迟、防重、请求／执行、失败提示和复位；快进独立于倍速。 |
| F35 | 镜头 | `FightMainPanelCameraUI` 在准备、观战和默认锁定条件下切换跟随／自由镜头。 |
| F36 | 托管、背包 | `FightMainPanelHostingUI/BagUI` 处理显示条件、状态、弹窗和红点；入口不代表任意换装或使用物品。 |
| F37 | 录像、回放、跳过 | 保存意愿、记录流、回放和剧情跳过信号分别处理；录像不是战斗检查点。 |
| F38 | 观战、聊天、扩展 | 公共面板切换观战对象、技能提供者和聊天入口，模式扩展通过外部适配器接入。 |
| F39 | 战报与统计 | 报告提供者按配置选择个人／全员，展示施放、命中、伤害、治疗、护盾、状态次数和占比。 |

### 模型、演出和资源

| 编号 | 原版功能 | 官方机制 |
| --- | --- | --- |
| F40 | 单位外观 | `MyFightRoleCharacter`、`PlayerAnimatorAppearance` 绑定职业、装备、武器、皮肤和单位模型，保留骨骼、网格、贴图、挂点、尺寸、方向和层序。 |
| F41 | 生成、移动、动作 | 单位、技能、命中、伤害 Playable 处理生成、移动、施放、受击、死亡、复活、召唤和清理。 |
| F42 | 特效、飘字、声音 | 范围、弹道、跟踪、偏移、延迟、材质、粒子、声音和飘字由配置与 Playable 驱动。 |
| F43 | UI 生命周期 | `FightMainPanel` 动态装配控件、订阅／解绑事件，保留官方坐标、锚点、字体、遮罩、滚动、按钮命中、旋转和动画。 |

### 原版执行链

原版分为规则状态、过程消息和 Unity 表现三层。`Common.EcEntity/EcComponent` 保存逻辑数据，`MyEntity/MyComponent` 把实体绑定到 GameObject、Playable 和 UI。一次技能可以生成多个命中，一个命中可以产生多个目标结果；技能、动作、命中、伤害和状态有各自结束时点。

```mermaid
flowchart TD
    A[MsgGameRound 分片] --> B[FightComponent 暂存]
    C[MsgGameRoundEnd] --> D[合并 StepList 入队]
    B --> D
    D --> E[GamePlayManager 播放回合]
    E --> F[按 Time 等待]
    F --> G[DispatchStep 分发]
    G --> H[实体、动作、状态变更]
    H --> I[模型、HUD、声音、统计]
```

`FightRoundDriverComponent` 按 NextTime 升序插入，严格保持同刻步骤的原顺序。客户端可见的行动间隔为 `100000 / sqrt(max(1, Speed) × 境界 SpeedScale)`；属性速度和播放倍率分开处理。技能实例先处理消耗、CD、次数和开始／中断／结束事件，再展开命中与结果。结算结果保留 `SkillId`、`HitId`、`TargetId` 和来源实体，状态保留创建者、持有者、计时者、来源技能和生命周期。

### 实现概括

| 分类 | 当前结论 |
| --- | --- |
| 已完整实现的通用子项 | 战斗状态与检查点模型、确定性回合执行的基础、官方可取得的部分伤害／HP／护盾／元素／状态／韧性公式、格网和受控换位、时间轴／HUD／战报／飘字的已绑定子项，以及部分原生模型加载和局部 Play Mode 检查。 |
| 因没有服务器代码而自定义 | 离线随机流与回执、结果策略、受控站位请求替代、替身传导／减益转移，以及有明确证据的部分 AI、触发、元素、减伤和统计接线。所有近似分支都在本页标明为本地规则。 |
| 仍待实现 | 原版完整准备、逐事件回合调度、AI、命中／状态／召唤／胜负、完整 HUD／公共控件／战报、原生动作／特效／音频、全流程 Play Mode、存档恢复和 Windows Player 发布。 |

### 代码与来源入口

- 规则与执行：`unity/Assets/Game/Runtime/Official/Combat/`
- 离线替代：`unity/Assets/Game/Runtime/Offline/Combat/`
- 表现与 HUD：`unity/Assets/Game/Runtime/Presentation/Combat/`
- 战斗来源清单：`assets/reference/battle-rules/`
- 官方代码与配置：`.tools/source-packages/current/871_156598/official-code/`、`assets/official/`
