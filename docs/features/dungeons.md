# 游记入口与日常副本

[总索引](../README.md)

状态：未完成

## 功能简介

游记提供日常副本、阶梯／羁绊、地图、竞技、素材秘境及每日／每周挑战；具体玩法分别维护。

日常副本与竞技的规则、存档及 Home 接入已实现；官方 UI prefab、界面资源绑定和实际战斗流程尚未完整还原或验收，因此本功能未完成。

## 原始官方实现

游记、每日副本与竞技使用各自的原入口、地图／房间、难度、队伍邀请、评级和奖励配置；原 UI 的引用与脚本字段保持独立。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

`prepare_dungeon_assets.py` 从固定 APK 的 `image_ui_notes_dailydungeon_map_11`、`image_ui_notes_mopup` 提取原始 Sprite：日常场景、建筑、列表缩略图、秘境工具及卡片底图；按 StageType 18 的 `Stage.Icon` 提取八张关卡图。仅解码、裁切和格式转换，新增 12 项实际使用的运行时资源，显式加载清单同步更新。映射见 `assets/official/dungeons.json`，逐资源 bundle / 对象 / 输出哈希见 source-manifest.json。当前共 1,329 项资源、69 个 prefab、77 张完整配置表。

历史核查（2026-10-12）只检查 APK ZIP，因而将游记插画和专用面板记为未打包，并使用本地卡片与物品图标。2026-10-13 已通过固定 manifest `871_156598` 的校验 CDN 缓存实际转换并接入 77 个专用 prefab、662 个相关图片对象和 23 张配置表，替换游记首页、日常副本、四类采集场、幻想阶梯、羁绊冒险及每日 / 每周 / 竞技玩法入口；原采集配置的 GUID 绑定复用 50 份已转换的官方怪物网格，圣兽原始 UI 模型配置另复用七份；龙神的 `TempRTUI.RTTarget` 按原引用解析为原生网格，替代 Unity 专用渲染纹理相机。历史缺项保留在总清单 `resolved` 的 `previous` 中，不能再据此声称官方资源缺失。未使用自绘素材。

`scripts/prepare_battle_arena.py --runtime --verify-apk` 按固定 APK `871_156598` 校验并完整解码 `arena_guard_template`、`arena_rank_template`、`arena_match_rule`、`arena_match_strategy`、`arena_score_ratio`、`arena_daily_award`、`arena_season_award` 与 `item_count_limit` 八张原表。运行配置为 `assets/official/arena-rules.json`；守卫记录包含原始 `AppendAppearances` 的性别分组及皮肤类型／ID。各表源 bundle、对象路径、Luban 构造器、输入字节哈希及当前输出哈希见 [arena-source-manifest.json](../../assets/reference/arena-source-manifest.json)。竞技面板继续使用已转换的原 `ArenaPanel` prefab；未从静态守卫模板推断真人占位或赛季服务端记录。

`scripts/prepare_dungeon_bindings.py` 从固定 manifest `871_156598` 的 `prefabs_ui_dailydungeon`、`prefabs_ui_demonbattlefield`、`prefabs_ui_mopup`、`prefabs_ui_npcadventure`、`prefabs_ui_team` 及 `prefabs_ui_common` 读取原组件字段、RectTransform、Grid／LayoutGroup、StateSwitcher 和嵌入的规则行；原 `rule_tip.g.bytes` 与语言包提供说明与品质／难度／排序名称。生成原始配置数据，未重新设计窗口。

此前未转换的四张原 Sprite 独立解码到 `assets/official/dungeon-ui/`。对象 CAB、bundle／APK或原 CDN 凭据、对象名、输入及输出 SHA256、原 prefab 和配置输入记录在 `assets/reference/dungeon-ui/source-manifest.json`；其余图像复用已转换的同一原对象。来源不包含新版同名包或自绘素材。官方规则和 NPC 战力公式分别来自 DailyDungeonTipInfoDialogItem、RewardRuleInfoParser、NpcFightData、BattleFormulaHandler 等原代码；用户补充的离线规则另见对应功能／地图文档。

`scripts/prepare_dungeon_ui_bindings.py` 使用 `official_unity_schema.ScriptSchemas` 读取当前官方版本 `871_156598` 的 `TravelNotesPanel`、日常副本列表／条目／确认／详情／路线／房间及 `ArenaPanel` 原 prefab，按其 `Image` 和 TMP 组件序列化字段恢复 1,468 个 Image 与 514 个 TMP 节点，并由 `SpriteCache` 解析 CAB 依赖、导出 152 张实际引用的原始 Sprite。游记 prefab 来源为 APK 中与 `871_156598` 相同的包；副本与竞技 prefab 来自官方 CDN，bundle SHA256 分别为 `45134a83b372998156db5f81d4008c95d81f6c89d2b6b0059738ad02fc8adb47`、`67229760743d40fd8ed0775ba982004dc8ad261628cb05e3d181fd102e0d629d`。逐 prefab 输入对象、bundle、Sprite 来源及 Unity 输出 SHA256 记录于 [prefab-binding-source-manifest.json](../../assets/reference/dungeon-ui/prefab-binding-source-manifest.json)；运行载荷位于 `unity/Assets/StreamingAssets/Dungeons/UI/`，由 `HomeHudView.Dungeons` 按官方节点路径和名称应用。官方滚动生命周期、真实输入及战斗流程仍未完成验收，详见[游记与日常副本](dungeons.md)。

开场对话原生布局与载荷完整性：`prepare_character_creation_opening.py --ui-layout-only` 从固定 APK `871_156598` 的 `defaultpackage_assets_prefabs_ui_story.bundle`（MD5 `5473f9c47986ea5caafb199bd112f7fe`）重新读取 `OptionTalkDialog` 的 22 个布局组件，记录各组件 pathID、原始字节 SHA256 与 bundle／APK 来源，沿已有官方 `UGUIJszx.HorizontalLayoutGroupJSZX` 恢复姓名框。输出 `unity/Assets/StreamingAssets/Opening/ui/opening-ui.json` 的 SHA256 为 `08c4186721aa51ff3103bf283e0060690a4c4c477b9603f6fea03bd5683e5f52`，清单为 `character-creation/opening-source-manifest.json`；原 Sprite、字体、材质和动画资源未替换。开场粒子网格保留 `opening-skill-effects-source-manifest.json` 与运行载荷已有 SHA256，五个文件仅恢复原 LF 字节，Git 属性固定换行，未调整预期哈希。模型沿原 SkeletonDataAsset 的 `0.01` 导入比例，并由 `prepare_opening_model_transforms.py` 提取 APK 内 7 个 FightMonster／FightNpc prefab 的完整模型 Transform 父链（怪物 XY 为 0.3，4201 为 0.3、4202 为 0.23，保留落脚偏移），输出 `Opening/model-transforms.json`，并恢复怪物原材质 `_StraightAlphaInput = 1` 与透明输入，消除透明区域白块，`Resources/Opening/OpeningShaderVariants.shadervariants` 显式保留相应 Windows shader 变体与四个转场 UI shader，避免 Player 剔除动态查找资源后黑屏；清单逐层记录 pathID、原始 SHA256 与 APK 来源，运行时取消按 Boss 高度二次归一缩放；对话选项按官方 `OptionSlot.Reset` 隐藏，Windows 点击推进为用户明确指定的单机交互差异。

开场战斗单位与 HUD：固定 APK `871_156598` 的 FightNpc 4201／4202、Appearance 8401／8402、Boss 3803、GameRoundUI／GameRoundCharacterItem、GamePlayManagerConfig、CharacterDamageUI／DamageSequenceImage 及五套原位图字体已按 `prepare_opening_fighters.py`、`prepare_opening_unit_ui.py`、`prepare_opening_damage_ui.py` 和 `prepare_opening_skill_effects.py` 提取并显式绑定。Boss 原 107 节点／47 粒子、模板缓冲遮罩、网格描边和星空纹理保留；Windows 程序适配保留原材质属性、槽位深度和常量颜色。原生 HP Sprite 的九宫边框与世界单位换算已恢复。输入 APK／bundle／对象、官方类与输出 SHA256 见 [opening-source-manifest.json](../../assets/reference/character-creation/opening-source-manifest.json) 和 [opening-skill-effects-source-manifest.json](../../assets/reference/character-creation/opening-skill-effects-source-manifest.json)。 47 个运行网格保留已有 SHA256，Git 以二进制字节保存；不修改预期哈希。

开场技能移动与受击：`prepare_opening_action_motion.py` 复用固定 APK `871_156598`，提取 11532／11833／15301 的 EC 配置、移动 prefab、轨迹曲线及 15300 的跑动触发器；按官方 `MyFightSkill` 的技能实体播放顺序恢复 9 个 NPC 专用动作。精英 3481／Boss 3803 的 `MeshMaterialProperty` 绑定、材质与 shader 对象及 `_HITEFFECT` GLES 程序哈希收录在 [opening-source-manifest.json](../../assets/reference/character-creation/opening-source-manifest.json) 的 `action_motion`；Windows 受击适配保留模板遮罩、透明裁剪及预乘混合，输出绑定为 `Opening/action-motion.json` 与 `Resources/Opening/OpeningSpineHit.shader`。`AppearanceRendererLite`／`CustomRenderExistingMesh` 的运行时材质登记也由同一来源清单记录，Boss 描边在受击克隆期间保持原替换关系；四个剧情 UI shader 调整 Windows 裁剪参数声明顺序，输出哈希列于同节。

场景时间轴迁移：`prepare_battle_timeline.py` 按固定 `871_156598` APK 优先及已校验同版依赖恢复 `GameRoundScene`／`GameRoundCharacterItem_Scene` 的原 Transform、实际序列化字段引用、10张 Sprite、3份材质、3种 GLES shader 及原 Animator 控制器／4个片段。源纹理／图集、裁剪与 Sprite 几何、原对象／CAB／pathID、官方代码及运行适配哈希见 [timeline-source-manifest.json](../../assets/reference/battle-hud/timeline-source-manifest.json)；Windows 原生 prefab、材质、Sprite、controller、AnimationClip 与 meta 哈希见 [timeline-native-source-manifest.json](../../assets/reference/battle-hud/timeline-native-source-manifest.json)。显式绑定为 `Combat/OfficialBattleTimelineAssets`，动态头像复用上述摄影参数和当前原生角色网格，原圆形网格实现保持客户端 UV 修正；shader 从实际使用的 GLES 变体转换。其他角色类别和完整事件调度仍未完成，未生成完整素材快照。

场景时间轴粒子：`prepare_battle_timeline_particles.py` 以固定 `871_156598` APK 的 `GameRoundCharacterItem_Scene` 及已校验依赖，经 AssetRipper 1.3.14 保留两组原生 ParticleSystem／Renderer 和四个 Transform，恢复两份材质／两张原贴图。Windows 材质绑定按原 `USE_MAINTEX` 第一粒子 pass 的 GLES 程序转换，保留第二地图投影 pass 原始程序供后续所属流程使用；没有启用原 prefab 关闭的 `UIParticle` 父节点。原 bundle／组件／材质／shader／贴图哈希、解码包、工具／生成器、导出输入和输出见 [timeline-particles-source-manifest.json](../../assets/reference/battle-hud/timeline-particles-source-manifest.json)，显式引用随场景时间轴原生 prefab 打包。

## 完整还原部分

官方资源已按固定版 `871_156598` 转换为本地运行目录：`scripts/prepare_dungeons_runtime.py` 导出日常副本地图、房间、难度条件、奖励规则及战斗场景；`scripts/prepare_battle_arena.py --runtime --verify-apk` 导出竞技守卫（含原性别分组附加皮肤）、名次模板、匹配策略和竞技战斗场景。资源由 `OfflineDailyDungeonSession`、`OfflineArenaSession` 读取并校验。

战斗阵营坐标保留原 `entryPositions`，并由转换器从官方 `EntryPosList` 同步导出首个 `entryPosition` 与完整平铺 `entryPositionsFlat`；关卡波次保留原 `waveMonsterIds` 并同步导出 `waveMonsterIdsFlat`／`waveMonsterOffsets`。Unity `JsonUtility` 丢弃嵌套数组时，`DailyDungeonBattleRunner` 使用这些原值平铺字段还原玩家／助战起点与敌方波次；首个坐标另作玩家起点快速入口。官方未配置波次的关卡仍保持空配置，不自动生成敌人。

`HomeHudView.Dungeons` 实例化官方 `TravelNotesPanel`、日常副本列表／详情／房间图、离开确认及 `ArenaPanel` prefab，并接入本地交互。AssetRipper 导出的 prefab 丢失了 UGUI `Image` 和 TMP 字段；`scripts/prepare_dungeon_ui_bindings.py` 从固定版 `871_156598` 官方 bundle 恢复 8 个 prefab 的 1,255 份 Image 与 437 份 TMP 序列化数据，并提取所引用的 135 张原 Sprite。运行时按 prefab 子节点路径及官方对象名恢复 Sprite、颜色、Image 类型、文本、字号、字重／样式、对齐、自动字号、换行和溢出；TMP 使用 Home 已迁移的官方中文字体。来源和逐项输出哈希见 [prefab-binding-source-manifest.json](../../assets/reference/dungeon-ui/prefab-binding-source-manifest.json)。

官方 `DailyDungeonEntryPanel.InitContent()` 通过 `DetailContentStateSwitcher.SetState(false)` 激活初始目录；离线接线已按该初始状态启用 prefab 的 `SelectContent`。隔离 Unity r6 验收通过了区域进度夹具、游记入口、目录行点击、详情、进入路线、已清理房间离开结算及次数重载、竞技场打开与胜场名次重载。该验收由 `HomeAcceptanceDriver` 合成 PointerEvent 并直接记录房间清理，不代表 OS 鼠标输入或实际战斗。实际截图仍显示列表和详情的元素被裁切、偏移；官方 `LoopVerticalScrollRect.RefillCells` 的运行组件来自 AssetRipper 空脚本，当前没有恢复其 cell 生命周期和布局驱动，因此官方 UI 渲染尚未通过验收，不能声称美术完整还原。

日常副本目录只列入口区域进度为 `10000` 的公开副本并排除剧情难度 50；难度页签按原 tab 条件显示，默认选择最高永久解锁难度，锁页显示等级、战力／阶位压制及前难度评级条件，左右翻页按公开副本／可见难度组合顺序推进。日常副本按官方配置检查永久难度解锁、独立入场条件、每日次数和额外次数消耗，保存当前路线、清理房间、评级及战斗检查点；奖励按官方 `RewardRuleInfoParser` 的条件、`Percent`、候选权重及数量结算；`RandomReward` 装备占位项通过固定版 `random_reward` 的候选装备及权重生成实际装备实例，装备等级和祝福等级使用对应地图组、难度的官方范围。主动离开仅结算清理房间配置掉落且不返次数，战斗失败保留当前路线并清除本场战斗检查点以允许重打；已清理房间不能重复挑战。区域公开通过已保存的主线区域进度判定。竞技场按 `ArenaEntry` Type 58 的地图预览／开放门槛显示，读取原匹配区间和守卫权重，支持免费／付费刷新、购买挑战次数、本地挑战名次和胜负奖励；胜利后名次移至对手名次。挑战前按官方守卫战力与 `ArenaCombatRatingDirectWinPercent` 提供正常战斗／直接胜利选择，直接胜利消耗次数并走同一胜场结算；守卫使用模板附加外观，性别按本地均衡随机选择并随挑战保存。`DailyDungeonBattleRunner`、`ArenaBattleRunner` 使用官方战斗配置和场景接入战斗控制器，并保存战斗检查点。
Map12 的 121/50 机神山属于已确认的私有离线边界：原路线与官方房间波次照常使用，战斗胜负写入本地路线存档；失败可重试，胜利清理当前房间，不发奖励、不修改公共次数或购买次数。

副本目录通过 `HomeFeatureRuntime.MapRegionProgress` 读取统一离线主线区域进度接口；进度达到 `10000` 时视为公开。该接口由主线区域目标推进，不扩展地图类 11–23 的运行时接线。

机神山私图详情已接回原解锁、进入按钮和标题栏三组状态切换及无难度奖励标题；状态对象引用和原语言键由官方 prefab／代码提取。该变更仅恢复原显示分支，费用、奖励和次数保持既定边界，实际界面输入仍待验收。

机神山私图19个按钮和149项布局／尺寸／裁切配置、原奖励区横向滚动及顶部安全区已按原 prefab 显式恢复；尺寸上限复用原迁移组件。控件参数恢复不代表各按钮业务回调或可视交互已经完成，显隐动画引用、原模糊遮罩和关闭分支已接回，实际交互待验。

机神山私图原 `PopupWindow` 的两份显隐动画引用已接回迁移播放器，并逐条核对实际播放曲线、时长、输入对象哈希及既有原生转换收据。原模糊遮罩与点击关闭的0.5秒条件已按所选源码接回；显隐画面与模拟输入仍未运行验证。

原组件的路径引用已修复，避免未附名称断言的合法 Graphic／布局引用被错误拒绝；3158节点／31个原按钮 Graphic 引用的实际 C# 方法回放先失败后通过，错误节点名及父层级仍被拒绝。该检查不启动 Unity，不替代原界面点击和画面验收。

机神山私图原模糊遮罩与点击关闭已接回，复用当前 `WorldCanvas` 子层级及有来源记录的原 `ScreenBlur` shader／材质；原按钮参数、1/13目标尺寸、4层模糊、宽高比裁切、容器间隔10／遮罩下一层以及超过0.5秒的点击门槛均保留。进入本地121／50路线后沿原 `StartBattle` 成功分支关闭详情窗，战斗返回不再重新启用旧详情。独立 C# 编译、源绑定复现、路径回放和65502个 GUID 静态检查通过；遮罩实际画面／射线输入与关闭清理的 Play Mode 夹具已补充，仍暂停执行。

机神山私图原 `SFX/PopupWindowSound` 的打开事件为空、关闭事件为 `Main_Cursor_Back`（3686182067），现按原 OnHiding 事件恢复。`prepare_unity_map12_ui_audio.py` 核对当前5份原包，从原 Wwise Event／ActionPlay／Sound 图以固定0 dB主增益解码，保留关闭事件−16 dB及原媒体长度、声道和采样率；来源、播放图、工具及 PCM 哈希见 `assets/reference/official-map/map12-ui-audio-source-manifest.json`。关闭音效的本地播放器保持到事件结束，注册现有 Sound 设置通道；实际播放、音量设置变化、指针关闭和场景退出清理仍待 Play Mode 验证。

机神山私图追加恢复原5个 UIClickSound 与14个 ButtonSound，分别保留5份标签、12份普通点击和2份失败提示事件。左键筛选及 Start／Button.onClick 原订阅方式保留；本地声音沿用关闭音效已接通的 Sound 设置通道。原标签／普通点击／失败事件分别按−6／−6／−8 dB解码为 PCM，导入不归一化，保留资源路径及 GUID；原组件、媒体、播放图与输出哈希均可复现。实际音效指针、音量变化和全部控件业务仍待运行验收；匹配等待事件 `TeamMatch_Waiting_1` 的−3 dB音频已解码，原第5帧事件已接线，实际动画事件与声音仍待运行验证。

机神山私图匹配显示动画现保留原60 Hz／0.9333333373秒 Legacy clip 的31条浮点曲线和5条缩放曲线，逐项核对 float32 关键帧、切线、权重及事件；原第5帧 `PostEvent(0)` 通过迁移的 AnimationSoundPlayer 播放已验证的 TeamMatch_Waiting_1。原播放参数、两处 CanvasGroup 原值与17组实际目标已绑定；共享动画另外6条曲线的原目标在本 prefab 缺席，保留完整原曲线并记录来源。来源见 `assets/reference/official-map/map12-matching-animation-source-manifest.json`。无覆盖复现、独立 C# 编译、路径回放和静态检查通过；实际播放夹具尚未执行，匹配业务和正式控件输入仍待验证。

机神山匹配图另接回两处原 `RotateAnimation`，圆环和光晕分别沿本地 Z 轴以50°／80°每秒更新，保留原启用值及 `Time.deltaTime` 执行方式；组件和原代码哈希并入同一匹配来源清单。实际帧旋转夹具已补齐，随用户暂停的自动运行验收保留待执行；UIMesh 与原扭曲材质的实际运行仍待验证。 旋转来源与绑定无覆盖复现、三项独立 C# 编译及72,510 GUID静态检查通过；自动运行验收仍按用户要求暂停。

机神山匹配两处 UIMesh 已恢复原82顶点／240索引 Mesh、两份原扭曲材质与五张原贴图，并复用来源对象哈希相同的 Windows `UI/EffectDistort`。原 Mesh 缓冲、材质字段、四个 GLES 变体及完整纹理 mip 已逐项核对；原 `UIMesh`／`UIMeshUtils` 的 CanvasRenderer 提交与材质属性更新已迁移，资源按原路径显式加载。来源为 `assets/reference/official-map/map12-matching-mesh-source-manifest.json`。来源与绑定复现、三项独立 C# 编译通过；全工程静态检查通过（72,572 GUID）。原材质／网格和实际画面夹具尚未运行，完整私人副本流程仍未验收。

机神山私人详情的难度标签已恢复原 TabGroup／五个 TabItem／五个 TabAnimatorTransition 序列化字段，并接通当前配置的普通、困难两档显示、前置难度锁定提示、互斥选择和详情／评级范围刷新。入口仍执行已确认的121/50本地路线；展示切换不写存档或公共次数。来源见 `assets/reference/official-map/map12-private-difficulty-source-manifest.json`，复现入口为 `prepare_unity_map12_private_difficulty.py`。来源复现、三项独立 C# 编译与静态检查通过；点击与动画夹具已加入但自动运行验收暂停，原个人最好评级／排行／红点、奖励说明与预览仍未完成。

### 已迁移代码与局部资源

以下为局部代码／资源接入，完整流程仍受本页缺项限制。

Map10 水面 Windows D3D11 编译适配仅为 `TRANSFORM_TEX` 的复合 UV 参数补充整体括号；官方 shader 来源仍为固定版 `871_156598` 的 `defaultpackage_unityshaders.bundle` （bundle hash `cac97355bb4799c415e3cb1ef38b6dc5`、PathID `5093245133389786381`）。原生重建入口为 `unity/Assets/Game/Resources/World/Map10Water.shader`，输出 SHA256 为 `7d8488c9451eb99a4f688d288146dd5059dcc79c8a56c68a6c816536f00ecb43`；已同步 `unity/Assets/Official/World/Map/10/rendering-source-manifest.json` 中的重建输出记录。纹理、官方输入哈希和显式加载路径均沿用原记录，Windows 构建已无水面 shader 编译错误；动态画面仍按地图页验收。

角色附件原生几何：`prepare_native_character_geometry.py` 从当前 `871_156598` 官方 APK 优先读取男女骨骼、分离动画原数据及实际图集，独立使用固定 MIT 格式读取器，保留二进制附件名称覆盖、原骨骼局部顶点、每个影响的权重、region 变换、图集裁剪／旋转及贴图路径。男性654／女性706个现有附件输出至 `Resources/CharacterCreation/native-character-geometry.json`，共享模型工厂绑定 `CharacterNativeAttachmentGeometry` 并校验 SHA256 侧车；原始输入、解码凭据、旧原生 prefab、生成器、运行绑定和输出哈希见 [native-geometry-source-manifest.json](../../assets/reference/character-creation/native-geometry-source-manifest.json)。该几何转换不调用 Spine runtime，也不使用网格帧；既有 Animator 的历史来源继续保留，完整约束／变形／未转换动作不在完成范围。

## 自定义实现

以下为已确认的单机处理要求及实际采用的行为。

服务端进入事务由本地保存替代；次数在实际进入时扣除，主动离开不退还。旧自创幻兽派遣与真人赛事／镜像入口停用，不增加统一体力或角色等级硬门槛。

成功完成至少携带一位本地邀请伙伴的战斗时，固定增加100友情币，不设置每周或累计领取上限；这替代原由服务器下发的好友助战奖励。

沿用已确认的单机世界阶段方案，服务器开服日期条件不计等待；个人等级、战力和个人评级仍按原配置检查，区域官方主线目标完成后公开。难度仅按当前格式中的有效进度和原条件开放；旧格式存档不导入。

竞技入口在离线版采用原 `ArenaEntry` 的 Type 58 地图预览／开放门槛，不使用 `ArenaAreaEntry` 中依赖组队挑战的旁路开放条件。服务端对手资料和赛季同步改由固定版守卫模板及玩家本地名次替代；挑战结算用原每日次数及胜负奖励配置，本地胜场和名次只属于当前玩家，胜利后采用对手名次。守卫数值按用户确认翻倍，附加外观按模板数据加载，性别在官方可用选项中均衡随机并写入活动挑战存档。

山之国剧情私图121／50的胜负与本地路线边界见[山之国](../maps/map-12.md)：不发副本奖励，不修改公共次数或购买次数。该入口和冷读通过 `OfflineDailyDungeonSession.OpenPrivateMap` 读取档案，避免通用 `Open` 的跨日重置影响公共次数；窗口展示按原6103普通副本组件读取21的标题与难度，战斗按原私图组件进入121／50。跨日次数保持及完整输入／战斗／冷读夹具尚未执行。

### 已确认的本地接线与边界

断罪魔印累计伤害：`prepare_battle_accumulated_damage.py` 重新解析 `871_156598`／`871_156598` 两版 APK `config_ec` 的 `FightSkill_part703.g.bytes` 与 `FightSkill_part157.g.bytes`，核对24062累计组件、13148技能、13149命中与 Unity `battle-rules.json` 实际字段完全一致。原 bundle、CAB／对象、载荷哈希、反编译组件／枚举、当前中文 `entry_24062_desc` 和生成器哈希见 [accumulated-damage-source-manifest.json](../../assets/reference/battle-rules/accumulated-damage-source-manifest.json)。本地规则按说明累计非真实生命伤害、状态结束时执行原30%自定义伤害链；没有新增或替换美术。服务器完整触发排序未由此取得，已确认的来源分摊、优先选敌与承伤加成接线、施加者阵亡后的结算及剩余实体销毁边界见战斗页。

## 尚未实现

- 恢复官方 `LoopVerticalScrollRect.RefillCells` 组件及初始化行为，修复游记、副本列表／详情／路线及竞技 prefab 的偏移和裁切，再检查官方渲染、点击与遮挡。
- 实际鼠标／键盘输入、战斗胜负与失败重打、奖励结算、路线中断后恢复、竞技排名变化及相关存档恢复均待 Play Mode 核验；当前验收仅通过业务状态断言，驱动器调用回调不等于真实输入或战斗。
- 最新静态工程检查通过；Unity Player 构建、启动与逐项流程验收尚未执行。

## 代码与资源入口

[TravelNotesPanel](../../.tools/source-packages/official-code/client-source/TravelNotesPanel.cs)、[PlayerDailyDungeonDataExt.CanUnlockDailyDungeonDifficulty／CanEntryDailyDungeonDifficulty](../../.tools/source-packages/official-code/common-source/Common/PlayerDailyDungeonDataExt.cs)、[Player.IsUnlockDailyDungeonByInfo](../../.tools/source-packages/official-code/common-source/Common/Player.cs)、[DailyDungeonDifficultyContent.RefreshTabItem](../../.tools/source-packages/official-code/client-source/DailyDungeonDifficultyContent.cs)、[DailyDungeonLockContent.Refresh／OnClickUnlockBtn](../../.tools/source-packages/official-code/client-source/DailyDungeonLockContent.cs)

[ArenaEntry.IsShowPreview／IsUnLock](../../.tools/source-packages/official-code/client-source/ArenaEntry.cs)、[ArenaAreaEntry.IsShowPreview／IsUnLock](../../.tools/source-packages/official-code/client-source/ArenaAreaEntry.cs)、[PlayerUtils.IsFunctionLimit](../../.tools/source-packages/official-code/common-source/Common/PlayerUtils.cs)、[WorldMapProgressCondition](../../.tools/source-packages/official-code/common-source/Common/WorldMapProgressCondition.cs)、[rules.json](../../assets/official/rules.json)

[ArenaGuardTemplateInfo](../../.tools/source-packages/official-code/common-source/Common/ArenaGuardTemplateInfo.cs)、[BattleFormulaHandler.CalcCombatRatingSkill](../../.tools/source-packages/official-code/common-source/Common/BattleFormulaHandler.cs)、[ArenaFightDialog.CheckDirectWin](../../.tools/source-packages/official-code/client-source/ArenaFightDialog.cs)、[ArenaRankTemplateInfo](../../.tools/source-packages/official-code/common-source/Common/ArenaRankTemplateInfo.cs)、[ArenaMatchStrategyInfo](../../.tools/source-packages/official-code/common-source/Common/ArenaMatchStrategyInfo.cs)、[ArenaMatchRuleInfo](../../.tools/source-packages/official-code/common-source/Common/ArenaMatchRuleInfo.cs)、[prepare_battle_arena.py](../../scripts/prepare_battle_arena.py)

[日常副本运行资源来源清单](../../assets/reference/dungeons-runtime-source-manifest.json)、[竞技场运行资源来源清单](../../assets/reference/arena-source-manifest.json)

主要接入代码：[HomeHudView.Dungeons](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.Dungeons.cs)、[OfflineDailyDungeonSession](../../unity/Assets/Game/Runtime/Offline/World/OfflineDailyDungeonSession.cs)、[DailyDungeonBattleRunner](../../unity/Assets/Game/Runtime/Presentation/Home/DailyDungeonBattleRunner.cs)、[OfflineArenaSession](../../unity/Assets/Game/Runtime/Offline/World/OfflineArenaSession.cs)、[ArenaBattleRunner](../../unity/Assets/Game/Runtime/Presentation/Home/ArenaBattleRunner.cs)
