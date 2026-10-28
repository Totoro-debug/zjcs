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

历史核查（2026-10-12）只检查 APK ZIP，因而将游记插画和专用面板记为未打包，并使用本地卡片与物品图标。2026-10-13 已通过固定 manifest `854_156383` 的校验 CDN 缓存实际转换并接入 77 个专用 prefab、662 个相关图片对象和 23 张配置表，替换游记首页、日常副本、四类采集场、幻想阶梯、羁绊冒险及每日 / 每周 / 竞技玩法入口；原采集配置的 GUID 绑定复用 50 份已转换的官方怪物网格，圣兽原始 UI 模型配置另复用七份；龙神的 `TempRTUI.RTTarget` 按原引用解析为原生网格，替代 Unity 专用渲染纹理相机。历史缺项保留在总清单 `resolved` 的 `previous` 中，不能再据此声称官方资源缺失。未使用自绘素材。

`scripts/prepare_battle_arena.py --runtime --verify-apk` 按固定 APK `854_156383` 校验并完整解码 `arena_guard_template`、`arena_rank_template`、`arena_match_rule`、`arena_match_strategy`、`arena_score_ratio`、`arena_daily_award`、`arena_season_award` 与 `item_count_limit` 八张原表。运行配置为 `assets/official/arena-rules.json`；守卫记录包含原始 `AppendAppearances` 的性别分组及皮肤类型／ID。各表源 bundle、对象路径、Luban 构造器、输入字节哈希及当前输出哈希见 [arena-source-manifest.json](../../assets/reference/arena-source-manifest.json)。竞技面板继续使用已转换的原 `ArenaPanel` prefab；未从静态守卫模板推断真人占位或赛季服务端记录。

`scripts/prepare_dungeon_bindings.py` 从固定 manifest `854_156383` 的 `prefabs_ui_dailydungeon`、`prefabs_ui_demonbattlefield`、`prefabs_ui_mopup`、`prefabs_ui_npcadventure`、`prefabs_ui_team` 及 `prefabs_ui_common` 读取原组件字段、RectTransform、Grid／LayoutGroup、StateSwitcher 和嵌入的规则行；原 `rule_tip.g.bytes` 与语言包提供说明与品质／难度／排序名称。生成原始配置数据，未重新设计窗口。

此前未转换的四张原 Sprite 独立解码到 `assets/official/dungeon-ui/`。对象 CAB、bundle／APK或原 CDN 凭据、对象名、输入及输出 SHA256、原 prefab 和配置输入记录在 `assets/reference/dungeon-ui/source-manifest.json`；其余图像复用已转换的同一原对象。来源不包含新版同名包或自绘素材。官方规则和 NPC 战力公式分别来自 DailyDungeonTipInfoDialogItem、RewardRuleInfoParser、NpcFightData、BattleFormulaHandler 等原代码；用户补充的离线规则另见对应功能／地图文档。

## 完整还原部分

官方资源已按固定版 `854_156383` 转换为本地运行目录：`scripts/prepare_dungeons_runtime.py` 导出日常副本地图、房间、难度条件、奖励规则及战斗场景；`scripts/prepare_battle_arena.py --runtime --verify-apk` 导出竞技守卫（含原性别分组附加皮肤）、名次模板、匹配策略和竞技战斗场景。资源由 `OfflineDailyDungeonSession`、`OfflineArenaSession` 读取并校验。

`HomeHudView.Dungeons` 实例化官方 `TravelNotesPanel`、日常副本列表／详情／房间图、离开确认及 `ArenaPanel` prefab，并接入本地交互。AssetRipper 导出的 prefab 丢失了 UGUI `Image` 和 TMP 字段；`scripts/prepare_dungeon_ui_bindings.py` 从固定版 `871_156598` 官方 bundle 恢复 8 个 prefab 的 1,255 份 Image 与 437 份 TMP 序列化数据，并提取所引用的 135 张原 Sprite。运行时按 prefab 子节点路径及官方对象名恢复 Sprite、颜色、Image 类型、文本、字号、字重／样式、对齐、自动字号、换行和溢出；TMP 使用 Home 已迁移的官方中文字体。来源和逐项输出哈希见 [prefab-binding-source-manifest.json](../../assets/reference/dungeon-ui/prefab-binding-source-manifest.json)。

官方 `DailyDungeonEntryPanel.InitContent()` 通过 `DetailContentStateSwitcher.SetState(false)` 激活初始目录；离线接线已按该初始状态启用 prefab 的 `SelectContent`。隔离 Unity r6 验收通过了区域进度夹具、游记入口、目录行点击、详情、进入路线、已清理房间离开结算及次数重载、竞技场打开与胜场名次重载。该验收由 `HomeAcceptanceDriver` 合成 PointerEvent 并直接记录房间清理，不代表 OS 鼠标输入或实际战斗。实际截图仍显示列表和详情的元素被裁切、偏移；官方 `LoopVerticalScrollRect.RefillCells` 的运行组件来自 AssetRipper 空脚本，当前没有恢复其 cell 生命周期和布局驱动，因此官方 UI 渲染尚未通过验收，不能声称美术完整还原。

日常副本目录只列入口区域进度为 `10000` 的公开副本并排除剧情难度 50；难度页签按原 tab 条件显示，默认选择最高永久解锁难度，锁页显示等级、战力／阶位压制及前难度评级条件，左右翻页按公开副本／可见难度组合顺序推进。日常副本按官方配置检查永久难度解锁、独立入场条件、每日次数和额外次数消耗，保存当前路线、清理房间、评级及战斗检查点；奖励按官方 `RewardRuleInfoParser` 的条件、`Percent`、候选权重及数量结算；`RandomReward` 装备占位项通过固定版 `random_reward` 的候选装备及权重生成实际装备实例，装备等级和祝福等级使用对应地图组、难度的官方范围。主动离开仅结算清理房间配置掉落且不返次数，战斗失败保留当前路线并清除本场战斗检查点以允许重打；已清理房间不能重复挑战。区域公开通过已保存的主线区域进度判定。竞技场按 `ArenaEntry` Type 58 的地图预览／开放门槛显示，读取原匹配区间和守卫权重，支持免费／付费刷新、购买挑战次数、本地挑战名次和胜负奖励；胜利后名次移至对手名次。挑战前按官方守卫战力与 `ArenaCombatRatingDirectWinPercent` 提供正常战斗／直接胜利选择，直接胜利消耗次数并走同一胜场结算；守卫使用模板附加外观，性别按本地均衡随机选择并随挑战保存。`DailyDungeonBattleRunner`、`ArenaBattleRunner` 使用官方战斗配置和场景接入战斗控制器，并保存战斗检查点。

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

## 自定义实现

以下为已确认的单机处理要求及实际采用的行为。

服务端进入事务由本地保存替代；次数在实际进入时扣除，主动离开不退还。旧自创幻兽派遣与真人赛事／镜像入口停用，不增加统一体力或角色等级硬门槛。

成功完成至少携带一位本地邀请伙伴的战斗时，固定增加100友情币，不设置每周或累计领取上限；这替代原由服务器下发的好友助战奖励。

沿用已确认的单机世界阶段方案，服务器开服日期条件不计等待；个人等级、战力和个人评级仍按原配置检查，区域官方主线目标完成后公开。难度仅按当前格式中的有效进度和原条件开放；旧格式存档不导入。

竞技入口在离线版采用原 `ArenaEntry` 的 Type 58 地图预览／开放门槛，不使用 `ArenaAreaEntry` 中依赖组队挑战的旁路开放条件。服务端对手资料和赛季同步改由固定版守卫模板及玩家本地名次替代；挑战结算用原每日次数及胜负奖励配置，本地胜场和名次只属于当前玩家，胜利后采用对手名次。守卫数值按用户确认翻倍，附加外观按模板数据加载，性别在官方可用选项中均衡随机并写入活动挑战存档。

山之国剧情私图121／50的胜负与本地路线边界见[山之国](../maps/map-12.md)：不发副本奖励，不修改公共次数或购买次数。该入口和冷读通过 `OfflineDailyDungeonSession.OpenPrivateMap` 读取档案，避免通用 `Open` 的跨日重置影响公共次数；窗口展示按原6103普通副本组件读取21的标题与难度，战斗按原私图组件进入121／50。跨日次数保持及完整输入／战斗／冷读夹具尚未执行。

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
