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

创角过场从固定版本 `CreatePlayerPanel` 官方 prefab 动画数据恢复 13 个命名片段、男女两份 UIMesh 溶解网格和角色层级顺序曲线，生成资产及来源哈希见 [creation performance source manifest](../../assets/reference/character-creation/creation-performance-source-manifest.json)。`CharacterCreationPerformanceTools.BuildAndValidate` 将曲线转为 Unity AnimationClip；`CharacterCreationEffectDissolve.shader` 是官方 `UI/EffectDissolve` 的 Windows HLSL 适配，按原 dissolve 参数、边缘颜色、Blend 和 stencil 设置绘制。Editor 校验已通过；创角面板在 Play Mode 中完成了体型、外观、命名页切换与截图检查，未单独逐帧验收全部溶解曲线和音效。

六类男女外观选项使用官方配置 ID，缩略图来自 `defaultpackage_assets_image_ui_playerskin.bundle`；155 个 Sprite 和输入输出哈希见 [player skin icon source manifest](../../assets/reference/character-creation/player-skin-icons-source-manifest.json)。肤色、发色与瞳色范围、固定颜色及 HSV 配置从官方角色目录读取。

男女创角预览由 [CharacterCreationMeshFramePlayer](../../unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationMeshFramePlayer.cs) 使用 Unity MeshRenderer 播放已校验的官方 24 fps 网格帧，包含 `home/idle_01` 与 `map/run_01`；不依赖 Spine runtime。打包脚本 [prepare_character_creation_meshes.py](../../scripts/prepare_character_creation_meshes.py) 复用固定来源的骨骼转换缓存、贴图及染色遮罩，输出 `StreamingAssets/CharacterCreation/creation-meshes.zip`，输入输出与逐文件哈希见 [native mesh source manifest](../../assets/reference/character-creation/native-mesh-source-manifest.json)。`CharacterCreationMeshFrameDye.shader` 适配官方 HSV 遮罩公式，按原部件层序、选中皮肤和染色参数绘制。此处验证范围为创角预览，不代表地图角色与其他 Animator 调用方已完成迁移。

创角按钮使用的 `Main_Page`、`Main_Cursor_Normal`、`Main_Tag`、`Main_Cursor_Back` 音效已转为本地 OGG 并由 [audio source manifest](../../assets/reference/creation-performance/audio/source-manifest.json) 校验。DIY 恢复默认外观使用官方 `ui_reset_appearance_tip_text` 确认键及官方弹窗 prefab；固定版本地语言资源未包含该键的文本，Unity 离线界面使用中文提示。

### 开场战场、录像单位与剧情排版

官方 `game_902` 战斗阶段是 2D 场景：固定版 `defaultpackage_assets_prefabs_ecentity_fight_fightstage_902.bundle`（80,054 字节，md5 `67c160d271bba532c38a428252304295`，APK 内）中的 `Assets/Prefabs/EcEntity/Fight/FightStage/902/game_902.prefab` 含 144 个 SpriteRenderer、4 个 Tilemap（2,921 块有效瓦片）、23 个 ParticleSystem、2 个正交相机（Main size 7.0），**没有任何 Mesh 或 MeshRenderer**；地面、墙体与装饰都是 sprite 与瓦片。格网不在场景内，来自 `stage` 表 20 行：27×24、origin `(-1.5,-2.5)`、69 个阻挡格、玩家入口 `(13,10)`，16 个怪物出生点与录像 `GridTransformComponent.MyPos` 完全一致。

[prepare_opening_battlefield.py](../../scripts/prepare_opening_battlefield.py) 按 prefab 世界变换把 sprite 与瓦片展平为按图集页、排序层和 order 分组的四边形批次（142 批、15,816 顶点、23,724 索引），导出 10 张官方图集/特效贴图、23 个粒子挂载和两个相机，写入 `StreamingAssets/Opening/battlefield/game_902.json`；来源与逐贴图哈希见 [opening battlefield source manifest](../../assets/reference/character-creation/opening-battlefield-source-manifest.json)。[OpeningBattlefield](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningBattlefield.cs) 在运行时装配批次、粒子与相机，单位根同时携带场景根偏移 `(5000,0,0)` 和格网 origin，录像单位与 NPC 直接使用官方格网坐标。原场景 6 个 shader 只有 Android GLES3 程序且工程未引入 URP，因此批次与粒子改用 Unity 内建 `Sprites/Default` 绘制，属渲染管线适配差异；`Sprites/Ground-Dissolve` 的溶解参数与 `Effect/Noise_Mask_Pro` 的关键字组合尚未迁移。

[prepare_battle_spine_pages.py](../../scripts/prepare_battle_spine_pages.py) 从固定 APK 中经 `models-source-manifest.json` 验证的 Texture2D 解出 5 只开场怪物（3803／3281／3282／3283／3481）与两名剧情角色（8401／8402）的 9 张图集页，核对 atlas `size:` 后绑定到既有 Spine 材质；`OfficialBattleModelCatalog` 的 identities／identitiesManifest 已指向真实资源。剧情角色不是玩家外观，而是官方 NPC 骨骼：录像中 1017 的 `NpcClassId=8401` 对应 `body_8401`、1018 的 8402 对应 `body_8402`；离线 [OpeningFightPlayback](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningFightPlayback.cs) 按该映射生成单位，与怪物共用同一套动作别名。单位排序显式置于场景排序层之上，避免被 21 层的背景遮住。战前顺序按官方 `GAME_ENTITY_LIST` 先于 `BeforeFight`：进入场景 → 生成实体与 HUD → `BeforeFight` → 录像 → `AfterFight`。

剧情文本改用官方 TMP 序列化字段：[prepare_character_creation_opening.py](../../scripts/prepare_character_creation_opening.py) 按 TMP 3.x 布局逐节点读取 fontSize、fontWeight、fontStyle、alignment（`0xFFFF` 时取水平/垂直轴组合）、margin、wordWrap、overflow、autoSize 范围和 fontColor，并由 [OpeningUi](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningUi.cs) 原样应用；`TalkText` 为 42 px、自动缩放下限 18、TopLeft、无边距，`NpcNameText` 为 48 px 金色居中，不再是此前的近似排版。录像面板的回合文本按官方模板“回合：{0}/{1}”随 `FightRoundNewModification.GlobalRound` 更新。录像没有独立的伤害／技能消息类型，但 `GAME_ENTITY_ADD` 中确有 `FightSkillComponent`、`FightDamageComponent` 与 `FightStatusComponent`：包含来源／目标、技能 ID、`DamageResult`、状态来源与持续信息；当前 `OpeningFightPlayback` 尚未解析和展示这些临时实体，也未接入原 `CharacterDamageUIManager` 的过滤、颜色与 prefab 设置。因此伤害值可从原录像恢复，相关运行时表现仍未完成。

### 官方字体与原生 UI

已核对固定 APK 的 `defaultpackage_assets_textmesh_pro_rawfonts_fzy4k.bundle` 中 `FZY4K` TextAsset：原始方正粗圆 TTF 与 `Assets/Official/Commerce/Art/fonts/FZY4K.ttf` 逐字节一致，SHA256 为 `90023872dffe6a58503de655bd0631bf3334ce3b6b72cc75a6625b685c94c386`，创角使用 `Resources/Combat/FZY4K-Dynamic.asset`，没有使用替代字体。

旧恢复 prefab 缺少原 MonoBehaviour 字段，不能显示完整 UGUI/TMP 内容；[prepare_character_creation_runtime_ui.py](../../scripts/prepare_character_creation_runtime_ui.py) 与 [CharacterCreationUiTools](../../unity/Assets/Game/Editor/CharacterCreation/CharacterCreationUiTools.cs) 从已核验原始字段恢复三个原生 prefab、47 张原 Sprite、RectTransform、TMP 样式、遮罩、布局、滚动、CanvasGroup 及选择状态。页标题沿原 `StepTitles` 与语言包显示“选择体型／装扮形象／取个名吧”，HSV 滑块沿 `EditCustomColorBlock.GenerateTexture` 生成渐变。输入输出及生成工具哈希见 [runtime UI source manifest](../../assets/reference/character-creation/runtime-ui-source-manifest.json) 与 [native UI source manifest](../../assets/reference/character-creation/native-ui-source-manifest.json)。

### 捏脸与本地保存验证

已在 Unity `2022.3.57f1c2` 非 `-nographics` Play Mode 实际运行 [CharacterCreationPlayModeAcceptance.PrepareAndRun](../../unity/Assets/Game/Editor/CharacterCreation/CharacterCreationPlayModeAcceptance.cs)，使用隔离存档，检查 720×1280 与 450×800 Game 视图截图：男女体型、女性六类外观、肤色／发色／瞳色固定色板、自定义 HSV、原生动画帧推进、发型选中框、重置取消／确认、返回与草稿恢复、空名拒绝、手动命名、正式 JSON 保存后重新打开、两个槽位独立保存，以及剑／法杖选择均通过。原字体对创角初始文本的字形覆盖检查通过，角色网格、渐变条、文本和底部导航已人工核对截图。

捏脸保存修复了 `OfflineMainlineProgressSession.UniqueAppearanceTypes` 误拒绝官方 `PlayerSkinType.Eye = 0` 的问题；旧逻辑会使已创建角色回读失败。`LocalJsonSaveFile` 对 Windows 短暂占用导致的原子替换错误作有限重试，保留原有原子替换与有效存档。创角启动时移除商城控制器自动创建的额外存档选择遮罩，避免拦截创角输入。

本次按用户指定只验收捏脸与保存：验收脚本隔离创角前开场与创角后 `FirstStory`，不据此证明剧情到初始武器的完整衔接；“幻装试穿”另做。捏脸控件通过 EventSystem 射线命中与软件指针事件验证，文本通过 TMP 输入值验证，尚未以实体鼠标与键盘操作验收；槽位、弹窗及武器按钮部分直接派发按钮事件。未构建或发布 Windows 成品。

## 自定义实现

存档槽、创角草稿和正式角色资料由 [CharacterCreationFlowController](../../unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs)、[CharacterCreationOfflineService](../../unity/Assets/Game/Runtime/Offline/CharacterCreation/CharacterCreationOfflineService.cs)、[CharacterCreationDraftStore](../../unity/Assets/Game/Runtime/Offline/CharacterCreation/CharacterCreationDraft.cs) 与 [ProfileSlotCatalog](../../unity/Assets/Game/Runtime/Offline/State/ProfileSlotCatalog.cs) 接入本地 JSON 保存。各槽独立保存草稿；提交角色时写入选中槽，草稿损坏可保留原文件副本后重新开始，不覆盖正式角色数据。写入失败会阻止页面切换并显示提示。

离线流程不提供随机名字。官方 `CreatePlayerSetNameSubPanel` 的随机名调用服务端 `OperPlayerGetRandomName(Sex)`，没有本地生成规则；按用户确认隐藏随机名按钮，玩家必须手动输入名字。

初始武器选项依据官方 `ChooseInitialWeaponNode.ProcessSucc` 和 `WeaponType` 数值 `200`（剑）、`201`（法杖）写入本地角色。由于官方节点由服务器故事流程触发，离线实现用本地按钮承接该选择；选择完成后进入人物页入口。

官方染色遮罩 `ethnicity_06`、`ethnicity_07`、`ethnicity_22` 至 `ethnicity_27` 共 8 张经用户核对固定 APK、相关原版本 CDN 包及新版同名包仍缺失。按用户确认保留全部选项；缺少遮罩时沿官方空遮罩行为不染色，预览不生成或替代官方遮罩。

开场跳过条件沿用官方 `MainUIRoot.mSkipNewbieCutsceneConditionId = 21003`：`CompositeOrCondition[21001 PlayerChannel(130077), 21002 PlayerMedia(C520612)]`。离线 SDK 桩的渠道／媒体均为空，因此判定为不跳过；`CharacterOpeningFlow.ShouldSkipNewbieCutscene` 保留该白名单比较，未另造“看过一次就跳过”的规则。每槽观看状态按用户确认只做进程内标记（`PlayedSlots`），重启游戏后未创角槽位会重新播放，不写入槽位存档。

开场播放由 [OpeningStoryPlayer](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningStoryPlayer.cs) 按官方节点语义执行：`StartStoryNode` 不阻断；`WaitForSecondsNode` 在 `NeedMask` 时先挂全屏透明遮罩再等待，遮罩由后续对话或开面板节点的 `Reset` 关闭；`DialogNode` 打开官方 `OptionTalkDialog` 层级，按 `dialog.csv` 的 `StoryName+DialogId`、`Order` 顺序显示对白与角色名，`AutoPlay` 配置按官方 120 秒／2 秒节奏自动推进；`ShowBlackMaskNode`／`CloseBlackMaskNode` 为硬切纯黑遮罩；`HideAllUINode`／`ShowAllUINode` 在节点结束时生效；`OpenShowPanelNode`／`CloseWindowNode` 打开和关闭 `Story/ExitFightTransitionPanel`；`LeaveStoryFightNode` 关闭战斗场景；`PlayBlackScreenStoryNode` 打开 `Global/FirstStoryPanel` 并在 `mIsKeepBlackScreen` 时保持黑幕；`PlayEnterGameTransitionNode` 打开开场转场面板；`PlayPlayableNode` 的两个 GUID（`e52fba61…`、`32bc3f4c…`）不在固定版五包 AssetTable 内，按官方空句柄行为跳过并记录；`HomePlayerPlayActionNode` 在没有家园环境时按官方异常分支 `SafeEnd` 进入快进。`SafeEnd` 后后续只展示类节点按 `ClientRunNode.InternalSkip` 语义直接跳过，仅保留关闭、离开和 UI 环境节点。

录像回放由 [OpeningFightPlayback](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningFightPlayback.cs) 驱动：先 GZip 解压并按 `GameServer`→`Console` 之外的原始 JSON 解析 `FightProcessRecordData`（[OpeningRecord](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningData.cs)），生成实体与回合步骤；回放按官方 `NormalPlaybackStrategy` 的时钟模型逐回合、逐步骤等待 `step.Time`（相对回合起点），并按官方 `CreatePlayerRecordFightSpeed = 1.5` 折算实际等待；`GAME_ENTITY_ADD`／`GAME_ENTITY_REMOVE` 增删单位，`Fight*Round*Modification` 触发当前行动者动作，`GAME_FASTFORWARD` 走跳跃到结尾（应用录像内快照），`GAME_PLAY_STATUS_CHANGE` 与 `GAME_MODIFACTION_RUN_BATTLE_END`／`_RUN_END` 记录状态。快进按钮按官方 `IsShowRecordFightSkipButton || Gm` 决定是否请求，延迟 `mShowSkipBtnTime = 500` 毫秒显示，点击后只推进到结尾而非加速；实装 `IsShowRecordFightSkipButton = false`，正常流程不显示按钮。

事件顺序按官方控制：`BeforeFight` 播放完成后才开始录像，录像结束触发 `AfterFight`，`AfterFight` 内 `LeaveStoryFightNode` 发起战斗到主场景切换；[CharacterOpeningFlow](../../unity/Assets/Game/Runtime/Presentation/Opening/CharacterOpeningFlow.cs) 等待该切换最多 5 秒，超时记录错误后仍打开创角面板，并使同一槽位只打开一次创角、只记录一次已播放。加载失败（缺文件、解析异常）按官方“回放返回 false”分支记录错误后直接打开创角。创角成功后的 `FirstStory` 先关闭创角面板，再以黑幕播放剧情，`FirstStory` 结束（或本轮无剧情可播）后进入本地初始武器选择，初始武器仍按已确认的离线按钮承接官方 `ChooseInitialWeaponNode` 的 `200`／`201` 数值。

已在 Unity `2022.3.57f1c2` 实际运行 [CharacterOpeningPlayModeAcceptance](../../unity/Assets/Game/Editor/CharacterCreation/CharacterOpeningPlayModeAcceptance.cs)（未使用 `-nographics`，720×1280 Game 视图；运行截图作为本次验证依据，不纳入仓库）：官方跳过条件 21003 的命中与不命中分支、三段剧情的 6 条对白与角色名、录像解析（19 实体／14 回合／1168 步）、`game_902` 场景绑定、回放单位解析与快进结束、`AfterFight` 转场与黑幕、创角面板单次打开、同进程再次进入不重播均通过；并断言 `BeforeFight` 官方台词“又到最后的时刻了，我们上吧！”与角色名“薇兰”实际出现在界面上。画面检查确认黑幕与对话正文按官方布局显示。该次运行发生在战场与图页迁移前，回放阶段为黑场；战场、图页与 NPC 单位迁移后的画面须重跑验收，见下。

## 尚未实现

以下缺项按影响范围分列；共用战斗规则与场景装配仍由[战斗执行](battle-execution.md)维护，不在此重复。

### 开场战斗表现资源

1. **技能特效与战斗音效。** 场景 sprite／瓦片／粒子与相机已迁移（见上），但回放仍未把录像里的技能特效实体（`GAME_ENTITY_ADD` 中大量临时实体）与战斗音效（battle Wwise 包）接入；`Sprites/Ground-Dissolve`、`Effect/Noise_Mask_Pro` 的官方材质参数与 URP 2D 光照仍以 `Sprites/Default` 替代，属于渲染差异。
2. **其余怪物 Spine 图集页。** 开场 5 只怪物与 2 名剧情角色的 9 张图集页已补齐并绑定；1678 个战斗骨骼引用的 2,348 个页名中仍有 2,301 张（约 98%）未导出，其他玩法遇到未补页的模型仍会跳过渲染。
3. **玩家外观单位。** 录像剧情角色按官方 NPC 骨骼渲染；自建角色进入战斗时的官方复合外观、换装与战斗动作仍未接入，模型目录目前只覆盖怪物与 NPC。
4. **行动时间轴、伤害与状态表现。** 当前仅按 `FightRoundUpdateDataModification` 的 `Data`／`State` 驱动回合行动，表现统一使用 `attack_01`。官方 `GAME_ENTITY_ADD` 中带有技能、伤害、状态和死亡组件，含精确技能／来源／目标 ID、`DamageResult` 与伤害结果标志；运行时尚未按官方组件生命周期消费它们，未接入 `CharacterDamageUIManager` 对应的伤害样式／过滤、技能演出绑定、状态图标与状态触发反馈。逐击伤害值可由原录像恢复；技能槽与行动时间轴是否能完整恢复仍需核对原 `FightSkillAgentComponent.SlotSkillItemDatas`、战斗 HUD 与录像数据绑定。

### 剧情表现资源

5. **剧情 UI 动画与个别代理贴图。** 6 个剧情面板与录像面板的原生 RectTransform、TMP 字段与 47 张 Sprite 已随 `opening-ui.json`／`atlas.png` 导出并有运行时绑定（`/Mask/BloomBg`、`SaveToggle/Btn` 为 alpha 0 的隐藏态；立绘表情与 `BorderActive` 由运行时代理取图）；仍缺各 prefab 的显示／关闭动画曲线，转场面板以 0.2 秒等待代替原展示动画，`StateSpriteAssetProxyConfig` 上的状态图未逐一导出。
6. **立绘与语音的完整表现。** 对白角色 8401／8402 的官方立绘 prefab、`Main` 与 cry3 表情已由 `prepare_character_creation_opening.py` 导出并由 `OpeningStoryPlayer` 挂到官方左右挂点；6 条 Wwise 语音事件已从固定 APK 的 `voice_prologue` bank 解出（`Opening/voices/*.wav` 供运行时播放，`Opening/voice/*.ogg` 为可复现导出），见 [voice source manifest](../../assets/reference/character-creation/opening-voice-source-manifest.json)。尚未逐条试听核对口型/节奏，也未验证全部表情与语音的停止切换。

### 仍需验证的部分

7. **本轮改动的 Play Mode 验证。** 战场装配、Spine 图页绑定、NPC 单位、TMP 官方排版与战前顺序的代码与数据已完成，但本轮结束时主工程编译被其他会话未完成的 `CharacterPropCalculator`／商城模块编译错误阻塞，`CharacterOpeningPlayModeAcceptance` 未能运行；上述画面上限结论尚待编译恢复后实测。
8. **异常分支。** 录像解压失败、实体列表缺失、剧情图缺失、窗口资源缺失、超时与保存失败仅按代码路径实现并记录日志，未逐项构造故障做 Play Mode 验收。
9. **实际输入。** 开场验收通过脚本调用按钮 `onClick`／`pointerClickHandler` 推进，尚未用真实鼠标点击验证对话推进、快进按钮与创角面板的命中区域。
10. **共用战斗目录修复。** `OfficialBattleModelCatalog.asset` 的 `sourceModels`／`sourceManifest`／`identities`／`identitiesManifest` 已全部指向真实资源 GUID；`Zjcs.Editor.ProjectTools.Validate` 的其余部分仍受 Map10 资源校验与其他会话未完成模块影响，未整体跑通。

### 本次明确排除的功能

“幻装试穿”入口尚未接通，原说明仍显示 `{0}` 占位内容；用户已确认本次只验收捏脸与保存，试穿另做。
