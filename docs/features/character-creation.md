# 新建存档与角色创建

[总索引](../README.md)

状态：未完成

## 功能简介

从新建本地存档开始，完成体型选择、外观捏脸、手动命名与初始武器选择；角色创建完成后可打开人物页。正式角色换装与衣柜规则见[人物属性、经验、位阶与衣柜](character.md)。

### 原始官方开场与创角流程

本功能范围包含创角前的初始剧情战斗。以下依据固定版 `854_156383` 客户端反编译代码记录官方调用顺序，不代表这些环节已经迁移到 Unity 离线流程。创角面板自身的溶解、跑动和页面转场动画与这段剧情战斗分别验收。

1. **判定入口。** [MainUIRoot.Start / ShouldSkipNewbieCutscene](../../.tools/source-packages/official-code/client-source/MainUIRoot.cs) 在主玩家存在且名字为空或 `IsPreCreateInit` 为真时，订阅剧情结束事件并延迟隐藏加载页。`mSkipNewbieCutsceneConditionId` 对应的条件满足，或玩家 ID 等于静态字段 `sLastPlayStoryPlayerId` 时直接打开创角；否则调用 `StartStoryFight`。条件 ID 不大于 0，或配置条件不存在时不跳过，后者记录错误。该静态字段是当前进程内标记，不能直接解释成已持久化的每存档观看记录；prefab 中实际条件 ID 和条件内容仍须提取核对。
2. **读取官方开场录像并进入战斗。** `MainUIRoot.StartStoryFight` 订阅场景切换，安装 [CreatePlayerStoryFightController](../../.tools/source-packages/official-code/client-source/CreatePlayerStoryFightController.cs)，读取 `ConfigConst.CONFIG_PATH + "Story/CreatePlayerFightRecord.bytes"`，经 GZip 解压，再由 [GameRecordManager.ParserRecordSaveDataFromBytes / TryPlaybackLocalRecord](../../.tools/source-packages/official-code/client-source/GameRecordManager.cs) 解析为 `FightProcessRecordData` 并启动本地录像回放。这是官方预录战斗流程，不能用临时编排一场普通战斗替代。若 `TryPlaybackLocalRecord` 返回 `false`，原入口清除剧情战斗控制器、打开创角并取消场景订阅；GZip 解压异常只见记录错误，不能据此推定所有加载／解析异常均会安全回退。
3. **播放战前剧情，再开始回放。** [FightMainPanelRecordFight.Start](../../.tools/source-packages/official-code/client-source/FightMainPanelRecordFight.cs) 将玩家置为 `JustWatching`，按录像中的 NPC 设置观战对象，并调用剧情战斗控制器。`CreatePlayerStoryFightController.OnEnableFight` 对未创角玩家播放 `BeforeFight`，等待结束后才调用 `StartPlayFight()`；若剧情入口未成功启动，`PlayStory` 仍调用后续回调。剧情图内部的对白、镜头、单位动作及具体节点顺序须继续从官方资源恢复。
4. **回放与快进。** 控制器仅在 `GameSettings.IsShowRecordFightSkipButton` 为真或玩家为 GM 时请求显示跳过按钮；`FightMainPanelRecordFight` 按 `mShowSkipBtnTime` 延迟显示，按钮调用 `DoFastforward()`。须核对 prefab 实际参数，不能默认按钮始终可用，也不能把快进实现为直接跳入创角。战斗模型、场景、技能、音效和剧情事件应消费同一份官方录像时序。
5. **播放战后剧情并返回主场景。** 战斗结束触发 `CreatePlayerStoryFightController.OnGameEnd`：未创角玩家播放 `AfterFight`；已正式创角玩家走 `ResetGame()` 分支。`MainUIRoot.OnStoryFinish("AfterFight", ...)` 记录 `sLastPlayStoryPlayerId`，再由 `OnPostStoryFinish` 等待 `Fight → Home` 场景事件设置 `mWaitSceneFightToMain`，最多 5 秒；超时记录错误后仍尝试打开创角。战后剧情图中离场与转场节点的实际绑定仍须恢复。
6. **打开创角。** `OpenCreatePlayerPanel` 在未命中配置跳过条件时播放黑幕，打开全局 `CreatePlayerPanel` 后关闭黑幕、释放剩余加载页延迟任务并发出 `OnPrepareCreatePlayerReady`。进入战斗场景时，`OnSceneUpdate` 也会释放加载页延迟任务。销毁入口或战斗面板时须沿原调用关系解除事件订阅、清除控制器并取消剧情令牌，避免重复触发。
7. **创建外观、命名与创角后剧情。** 原 `CreatePlayerPanel` 承接体型、外观与命名；[CreatePlayerSetNameSubPanel.ConfirmCreate / RunFirstStory](../../.tools/source-packages/official-code/client-source/CreatePlayerSetNameSubPanel.cs) 正常新角色分支先提交 `OperPlayerCreateAppearance`，成功后提交 `OperPlayerCreateCharacter`；预创建分支使用 `OperPlayerPreCreateCreateCharacter`。成功后清除预创建状态，关闭创角面板，在 `FirstStory` 存在且 `CheckCanRun` 允许时启动该剧情，使用黑幕等待剧情窗口打开，并发布玩家信息；`MainUIRoot` 收到 `FirstStory` 结束时恢复 Home 音乐。[ChooseInitialWeaponNode.ProcessSucc](../../.tools/source-packages/official-code/client-source/ChooseInitialWeaponNode.cs) 在服务器故事节点成功后写入初始武器，但它与 `FirstStory` 的具体连线及触发位置尚未核对剧情图，不将其推测为固定节点顺序。离线手动命名、初始武器按钮及本地存档差异见下文。

### 当前离线开场实现

[CharacterCreationFlowController.CreateSlot / OpenSlot](../../unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs) 在新建槽位或打开未创角槽位时调用 [CharacterOpeningFlow.Begin](../../unity/Assets/Game/Runtime/Presentation/Opening/CharacterOpeningFlow.cs)；`Begin` 先按 `ShouldSkipNewbieCutscene` 判定跳过，未跳过时依次执行 `BeforeFight` → 官方录像回放 → `AfterFight` → 战斗到主场景等待 → 打开创角面板。提交角色后 [SubmitCharacter](../../unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs) 调用 `PlayFirstStory`，`FirstStory` 结束后再进入本地初始武器选择。开场剧情的跳过节流、回放时钟、快进与失败回退见下文“自定义实现”。

## 完整还原部分

固定版 `854_156383` 的创角数据、`CreatePlayerPanel`、`CreatePlayerPanelDIYContent`、选项条目、颜色滑块及 `ConfirmDialog` 已恢复为 Unity 可导入资产。`CharacterCreationAssetTools.Validate` 会绑定目录资产并核对创角 prefab、确认弹窗、音效、155 个选项 Sprite 及来源哈希；该 Editor 入口已在 Unity `2022.3.57f1c2` 执行通过。官方客户端规则定位见 [CreatePlayerPanel.cs](../../.tools/source-packages/official-code/client-source/CreatePlayerPanel.cs)、[CreatePlayerDIYContent.cs](../../.tools/source-packages/official-code/client-source/CreatePlayerDIYContent.cs) 和 [AppearanceUtils.cs](../../.tools/source-packages/official-code/client-source/AppearanceSystem.Runtime/AppearanceUtils.cs)。

开场输入由 [prepare_character_creation_opening.py](../../scripts/prepare_character_creation_opening.py) 从固定 APK 与已转换官方数据提取，输出与哈希见 [opening source manifest](../../assets/reference/character-creation/opening-source-manifest.json)：`Assets/Config/Story/CreatePlayerFightRecord.bytes`（bundle `defaultpackage_assets_config_story.bundle`，34,004 字节 GZip，解压后 14 回合、1,168 步、19 个实体、`GameResultMsg=null`）、`BeforeFight`／`AfterFight` 剧情图（`..._story_storyfight.bundle`）、`FirstStory` 剧情图（`..._story_map10story_basicguide.bundle`）、三段共 6 条对白与两名角色（840100 薇兰、840200 绯乐）、`stage` 表 20 行（`Type=33` RecordFight、`GameId=902` → 场景 `game_902`、波次怪物 3803/3282/3281/3283/3481）及网格地面。剧情 UI 由同一脚本从 `prefabs_ui_story`／`prefabs_ui_global`／`prefabs_ui_fight_fightmainpanel_extmodel_fightmainpanelrecordfight` 导出 7 个 prefab 的 RectTransform 层级、图片颜色、按钮与文本节点，供 [OpeningUiView](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningUi.cs) 在运行时还原。

创角过场从固定版本 `CreatePlayerPanel` 官方 prefab 动画数据恢复 13 个命名片段、男女两份 UIMesh 溶解网格和角色层级顺序曲线，生成资产及来源哈希见 [creation performance source manifest](../../assets/reference/character-creation/creation-performance-source-manifest.json)。`CharacterCreationPerformanceTools.BuildAndValidate` 将曲线转为 Unity AnimationClip；`CharacterCreationEffectDissolve.shader` 是官方 `UI/EffectDissolve` 的 Windows HLSL 适配，按原 dissolve 参数、边缘颜色、Blend 和 stencil 设置绘制。Editor 校验已通过，Play Mode 画面和转场事件仍待验收。

六类男女外观选项使用官方配置 ID，缩略图来自 `defaultpackage_assets_image_ui_playerskin.bundle`；155 个 Sprite 和输入输出哈希见 [player skin icon source manifest](../../assets/reference/character-creation/player-skin-icons-source-manifest.json)。肤色、发色与瞳色范围、固定颜色及 HSV 配置从官方角色目录读取。

男女预览模型已从固定版 Spine 4.2 数据转换为 Unity 原生骨骼、网格、材质和 Animator；烘焙输入输出及缺项见 [native character model bake manifest](../../assets/reference/character-creation/native-character-model-bake-source-manifest.json)。

创角按钮使用的 `Main_Page`、`Main_Cursor_Normal`、`Main_Tag`、`Main_Cursor_Back` 音效已转为本地 OGG 并由 [audio source manifest](../../assets/reference/creation-performance/audio/source-manifest.json) 校验。DIY 恢复默认外观使用官方 `ui_reset_appearance_tip_text` 确认键及官方弹窗 prefab；固定版本地语言资源未包含该键的文本，Unity 离线界面使用中文提示。

## 自定义实现

存档槽、创角草稿和正式角色资料由 [CharacterCreationFlowController](../../unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs)、[CharacterCreationOfflineService](../../unity/Assets/Game/Runtime/Offline/CharacterCreation/CharacterCreationOfflineService.cs)、[CharacterCreationDraftStore](../../unity/Assets/Game/Runtime/Offline/CharacterCreation/CharacterCreationDraft.cs) 与 [ProfileSlotCatalog](../../unity/Assets/Game/Runtime/Offline/State/ProfileSlotCatalog.cs) 接入本地 JSON 保存。各槽独立保存草稿；提交角色时写入选中槽，草稿损坏可保留原文件副本后重新开始，不覆盖正式角色数据。写入失败会阻止页面切换并显示提示。

离线流程不提供随机名字。官方 `CreatePlayerSetNameSubPanel` 的随机名调用服务端 `OperPlayerGetRandomName(Sex)`，没有本地生成规则；按用户确认隐藏随机名按钮，玩家必须手动输入名字。

初始武器选项依据官方 `ChooseInitialWeaponNode.ProcessSucc` 和 `WeaponType` 数值 `200`（剑）、`201`（法杖）写入本地角色。由于官方节点由服务器故事流程触发，离线实现用本地按钮承接该选择；选择完成后进入人物页入口。

官方染色遮罩 `ethnicity_06`、`ethnicity_07`、`ethnicity_22` 至 `ethnicity_27` 共 8 张经用户核对固定 APK、相关原版本 CDN 包及新版同名包仍缺失。按用户确认保留全部选项；缺少遮罩时沿官方空遮罩行为不染色，预览不生成或替代官方遮罩。

开场跳过条件沿用官方 `MainUIRoot.mSkipNewbieCutsceneConditionId = 21003`：`CompositeOrCondition[21001 PlayerChannel(130077), 21002 PlayerMedia(C520612)]`。离线 SDK 桩的渠道／媒体均为空，因此判定为不跳过；`CharacterOpeningFlow.ShouldSkipNewbieCutscene` 保留该白名单比较，未另造“看过一次就跳过”的规则。每槽观看状态按用户确认只做进程内标记（`PlayedSlots`），重启游戏后未创角槽位会重新播放，不写入槽位存档。

开场播放由 [OpeningStoryPlayer](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningStoryPlayer.cs) 按官方节点语义执行：`StartStoryNode` 不阻断；`WaitForSecondsNode` 在 `NeedMask` 时先挂全屏透明遮罩再等待，遮罩由后续对话或开面板节点的 `Reset` 关闭；`DialogNode` 打开官方 `OptionTalkDialog` 层级，按 `dialog.csv` 的 `StoryName+DialogId`、`Order` 顺序显示对白与角色名，`AutoPlay` 配置按官方 120 秒／2 秒节奏自动推进；`ShowBlackMaskNode`／`CloseBlackMaskNode` 为硬切纯黑遮罩；`HideAllUINode`／`ShowAllUINode` 在节点结束时生效；`OpenShowPanelNode`／`CloseWindowNode` 打开和关闭 `Story/ExitFightTransitionPanel`；`LeaveStoryFightNode` 关闭战斗场景；`PlayBlackScreenStoryNode` 打开 `Global/FirstStoryPanel` 并在 `mIsKeepBlackScreen` 时保持黑幕；`PlayEnterGameTransitionNode` 打开开场转场面板；`PlayPlayableNode` 的两个 GUID（`e52fba61…`、`32bc3f4c…`）不在固定版五包 AssetTable 内，按官方空句柄行为跳过并记录；`HomePlayerPlayActionNode` 在没有家园环境时按官方异常分支 `SafeEnd` 进入快进。`SafeEnd` 后后续只展示类节点按 `ClientRunNode.InternalSkip` 语义直接跳过，仅保留关闭、离开和 UI 环境节点。

录像回放由 [OpeningFightPlayback](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningFightPlayback.cs) 驱动：先 GZip 解压并按 `GameServer`→`Console` 之外的原始 JSON 解析 `FightProcessRecordData`（[OpeningRecord](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningData.cs)），生成实体与回合步骤；回放按官方 `NormalPlaybackStrategy` 的时钟模型逐回合、逐步骤等待 `step.Time`（相对回合起点），并按官方 `CreatePlayerRecordFightSpeed = 1.5` 折算实际等待；`GAME_ENTITY_ADD`／`GAME_ENTITY_REMOVE` 增删单位，`Fight*Round*Modification` 触发当前行动者动作，`GAME_FASTFORWARD` 走跳跃到结尾（应用录像内快照），`GAME_PLAY_STATUS_CHANGE` 与 `GAME_MODIFACTION_RUN_BATTLE_END`／`_RUN_END` 记录状态。快进按钮按官方 `IsShowRecordFightSkipButton || Gm` 决定是否请求，延迟 `mShowSkipBtnTime = 500` 毫秒显示，点击后只推进到结尾而非加速；实装 `IsShowRecordFightSkipButton = false`，正常流程不显示按钮。

事件顺序按官方控制：`BeforeFight` 播放完成后才开始录像，录像结束触发 `AfterFight`，`AfterFight` 内 `LeaveStoryFightNode` 发起战斗到主场景切换；[CharacterOpeningFlow](../../unity/Assets/Game/Runtime/Presentation/Opening/CharacterOpeningFlow.cs) 等待该切换最多 5 秒，超时记录错误后仍打开创角面板，并使同一槽位只打开一次创角、只记录一次已播放。加载失败（缺文件、解析异常）按官方“回放返回 false”分支记录错误后直接打开创角。创角成功后的 `FirstStory` 先关闭创角面板，再以黑幕播放剧情，`FirstStory` 结束（或本轮无剧情可播）后进入本地初始武器选择，初始武器仍按已确认的离线按钮承接官方 `ChooseInitialWeaponNode` 的 `200`／`201` 数值。

已在 Unity `2022.3.57f1c2` 实际运行 [CharacterOpeningPlayModeAcceptance](../../unity/Assets/Game/Editor/CharacterCreation/CharacterOpeningPlayModeAcceptance.cs)（未使用 `-nographics`，720×1280 Game 视图；运行截图作为本次验证依据，不纳入仓库）：官方跳过条件 21003 的命中与不命中分支、三段剧情的 6 条对白与角色名、录像解析（19 实体／14 回合／1168 步）、`game_902` 场景绑定、回放单位解析与快进结束、`AfterFight` 转场与黑幕、创角面板单次打开、同进程再次进入不重播均通过；并断言 `BeforeFight` 官方台词“又到最后的时刻了，我们上吧！”与角色名“薇兰”实际出现在界面上。画面检查确认黑幕与对话正文按官方布局显示，回放阶段为黑场（原因见下文缺项 1～2），未出现报错或卡死。

## 尚未实现

以下缺项按影响范围分列；共用战斗规则与场景装配仍由[战斗执行](battle-execution.md)维护，不在此重复。

### 开场战斗表现资源

1. **战场几何与演出。** 官方 game_902 场景网格、材质、shader、粒子与技能特效尚未按[Android → Windows 流程](../asset-management.md#android-资源迁移到-windows)迁移为 Unity 运行资产，回放期间只有空的录像战斗面板，没有地形、镜头推拉、技能特效与战斗音效。
2. **怪物 Spine 贴图页。** 固定版战斗 Spine 来源清单只包含 47 张图集页，monster_3803 等模型的 .atlas.txt 引用的贴图页未在清单内，对应材质的 _MainTex 为空。为避免出现无贴图白色网格，[OpeningFightPlayback](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningFightPlayback.cs) 对缺少图集页的模型记录一次日志后不生成视图，因此当前回放画面没有怪物模型；补齐共用战斗 Spine 载荷后会自动恢复渲染。
3. **玩家／剧情角色单位。** 录像中 1017／1018 两名 Npc 角色的外观由玩家外观管线渲染，离线尚无战斗角色外观绑定，当前只记录诊断日志、不生成模型。
4. **单位 HUD 与回合条。** 录像战斗面板只挂载官方 FightMainPanelRecordFight 面板（且其图片 sprites 未导出），回合文本、行动时间轴、技能槽、伤害数字与受击表现未接入回放事件。

### 剧情表现资源

5. **剧情 UI 贴图与动画。** 官方 TalkDialog、StoryMask、Mask、ExitFightTransitionPanel、EnterGameStoryTransitionPanel、FirstStoryPanel 的图片在 prefab 内通过 StateSpriteAssetProxyConfig 等运行时代理取图，导出的布局因此没有 sprite 引用；对话底板、黑幕图、转场美术与 prefab 动画（显示／关闭曲线）目前缺失，对话正文与角色名直接绘制在官方 RectTransform 位置上，转场面板以 0.2 秒等待代替原展示动画。
6. **TMP 排版字段。** 固定版 bundle 未保留 TMP 序列化类型树，字号／对齐／边距无法按字段读取；当前正文使用左上对齐与固定边距、角色名居中，属于待核对的排版近似，不是官方数值。
7. **立绘与语音。** 对白角色 840100／840200 的官方立绘 prefab（UI/Portrait/Npc_Portrait_84xx）与 cry3 表情未迁移；Wwise 事件 prologue_main_1_01、1_02、2_01、2_02、3_01、3_02 未导出为本地音频，OpeningStoryPlayer 只记录缺失日志。
8. **战前单位预生成时序。** 官方在场景加载后先落 GAME_ENTITY_LIST 实体、再播 BeforeFight；当前实现在 BeforeFight 结束后才生成回放单位，等待补齐战场装配后按官方顺序调整。

### 仍需验证的部分

9. **FirstStory 端到端画面。** 创角提交流程已接入 FirstStory（黑幕、对话、HomePlayerPlayActionNode 的 SafeEnd 快进、开场转场、显示全部 UI），但 [CharacterOpeningPlayModeAcceptance](../../unity/Assets/Game/Editor/CharacterCreation/CharacterOpeningPlayModeAcceptance.cs) 只覆盖创角前开场；创角后剧情与初始武器选择的衔接仍须在完整流程中实测。
10. **异常分支。** 录像解压失败、实体列表缺失、剧情图缺失、窗口资源缺失、超时与保存失败仅按代码路径实现并记录日志，未逐项构造故障做 Play Mode 验收。
11. **实际输入。** 开场验收通过脚本调用按钮 `onClick`／`pointerClickHandler` 推进，尚未用真实鼠标点击验证对话推进、快进按钮与创角面板的命中区域。
12. **创角提交存档回读。** 现有 [CharacterCreationPlayModeAcceptance](../../unity/Assets/Game/Editor/CharacterCreation/CharacterCreationPlayModeAcceptance.cs) 在本次运行中于初始武器选择处失败：`CharacterCreationOfflineService.TrySetInitialWeapon` 通过 `OfflineMainlineProgressSession.OpenCharacter` 读回的 `CharacterCreated` 为 false，提示“请先完成角色创建”。该读回与 `OfflineMainlineProgressSession`／`CharacterCreationOfflineService` 的并行未提交修改同时存在，不属于本次开场流程范围，未在本页擅自改动其存档语义；开场流程的跳过标记与创角后 `FirstStory` 调用使用 `SkipOpeningForAcceptance`／`SkipFirstStoryForAcceptance` 验收开关，不依赖该回读。
13. **共用战斗目录修复。** OfficialBattleModelCatalog.asset 原本未序列化 identities／identitiesManifest，导致怪物身份表无法装配；本次在资源上补齐这两个引用，战斗模块的 Editor 校验仍受 Map10 资源校验失败影响，未整体跑通 Zjcs.Editor.ProjectTools.Validate。
