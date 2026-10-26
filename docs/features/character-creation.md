# 新建存档与角色创建

[总索引](../README.md)

状态：未完成

## 功能简介

从新建本地存档开始，完成体型选择、外观捏脸、手动命名与初始武器选择；角色创建完成后可打开人物页。正式角色换装与衣柜规则见[人物属性、经验、位阶与衣柜](character.md)。

### 原始官方开场与创角流程

本功能范围包含创角前的初始剧情战斗。以下依据固定版 `854_156383` 客户端反编译代码记录官方调用顺序；后文分别标出已迁移实现和仍未恢复的表现。创角面板自身的溶解、跑动和页面转场动画与这段剧情战斗分别验收。

1. **判定入口。** [MainUIRoot.Start / ShouldSkipNewbieCutscene](../../.tools/source-packages/official-code/client-source/MainUIRoot.cs) 在主玩家存在且名字为空或 `IsPreCreateInit` 为真时，订阅剧情结束事件并延迟隐藏加载页。`mSkipNewbieCutsceneConditionId` 对应的条件满足，或玩家 ID 等于静态字段 `sLastPlayStoryPlayerId` 时直接打开创角；否则调用 `StartStoryFight`。条件 ID 不大于 0，或配置条件不存在时不跳过，后者记录错误。固定版 `defaultpackage_assets_prefabs_ui_main.bundle`（MD5 `5ce6cf884ed57ba5d3aeaf1222681726`）序列化的 `MainUIRoot.mSkipNewbieCutsceneConditionId` 为 `21003`。该静态字段是当前进程内标记，不能直接解释成已持久化的每存档观看记录。
2. **读取官方开场录像并进入战斗。** `MainUIRoot.StartStoryFight` 订阅场景切换，安装 [CreatePlayerStoryFightController](../../.tools/source-packages/official-code/client-source/CreatePlayerStoryFightController.cs)，读取 `ConfigConst.CONFIG_PATH + "Story/CreatePlayerFightRecord.bytes"`，经 GZip 解压，再由 [GameRecordManager.ParserRecordSaveDataFromBytes / TryPlaybackLocalRecord](../../.tools/source-packages/official-code/client-source/GameRecordManager.cs) 解析为 `FightProcessRecordData` 并启动本地录像回放。这是官方预录战斗流程，不能用临时编排一场普通战斗替代。若 `TryPlaybackLocalRecord` 返回 `false`，原入口清除剧情战斗控制器、打开创角并取消场景订阅；GZip 解压异常只见记录错误，不能据此推定所有加载／解析异常均会安全回退。
3. **播放战前剧情，再开始回放。** [FightMainPanelRecordFight.Start](../../.tools/source-packages/official-code/client-source/FightMainPanelRecordFight.cs) 将玩家置为 `JustWatching`，按录像中的 NPC 设置观战对象，并调用剧情战斗控制器。`CreatePlayerStoryFightController.OnEnableFight` 对未创角玩家播放 `BeforeFight`，等待结束后才调用 `StartPlayFight()`；若剧情入口未成功启动，`PlayStory` 仍调用后续回调。剧情图内部的对白、镜头、单位动作及具体节点顺序须继续从官方资源恢复。
4. **回放与快进。** 控制器仅在 `GameSettings.IsShowRecordFightSkipButton` 为真或玩家为 GM 时请求显示跳过按钮；`FightMainPanelRecordFight` 按 `mShowSkipBtnTime` 延迟显示，按钮调用 `DoFastforward()`。须核对 prefab 实际参数，不能默认按钮始终可用，也不能把快进实现为直接跳入创角。战斗模型、场景、技能、音效和剧情事件应消费同一份官方录像时序。
5. **播放战后剧情并返回主场景。** 战斗结束触发 `CreatePlayerStoryFightController.OnGameEnd`：未创角玩家播放 `AfterFight`；已正式创角玩家走 `ResetGame()` 分支。`MainUIRoot.OnStoryFinish("AfterFight", ...)` 记录 `sLastPlayStoryPlayerId`，再由 `OnPostStoryFinish` 等待 `Fight → Home` 场景事件设置 `mWaitSceneFightToMain`，最多 5 秒；超时记录错误后仍尝试打开创角。战后剧情图中离场与转场节点的实际绑定仍须恢复。
6. **打开创角。** `OpenCreatePlayerPanel` 在未命中配置跳过条件时播放黑幕，打开全局 `CreatePlayerPanel` 后关闭黑幕、释放剩余加载页延迟任务并发出 `OnPrepareCreatePlayerReady`。进入战斗场景时，`OnSceneUpdate` 也会释放加载页延迟任务。销毁入口或战斗面板时须沿原调用关系解除事件订阅、清除控制器并取消剧情令牌，避免重复触发。
7. **创建外观、命名与创角后剧情。** 原 `CreatePlayerPanel` 承接体型、外观与命名；[CreatePlayerSetNameSubPanel.ConfirmCreate / RunFirstStory](../../.tools/source-packages/official-code/client-source/CreatePlayerSetNameSubPanel.cs) 正常新角色分支先提交 `OperPlayerCreateAppearance`，成功后提交 `OperPlayerCreateCharacter`；预创建分支使用 `OperPlayerPreCreateCreateCharacter`。成功后清除预创建状态，关闭创角面板，在 `FirstStory` 存在且 `CheckCanRun` 允许时启动该剧情，使用黑幕等待剧情窗口打开，并发布玩家信息；`MainUIRoot` 收到 `FirstStory` 结束时恢复 Home 音乐。[ChooseInitialWeaponNode.ProcessSucc](../../.tools/source-packages/official-code/client-source/ChooseInitialWeaponNode.cs) 在服务器故事节点成功后写入初始武器，但它与 `FirstStory` 的具体连线及触发位置尚未核对剧情图，不将其推测为固定节点顺序。离线手动命名、初始武器按钮及本地存档差异见下文。

### 当前离线开场实现

[CharacterCreationFlowController.CreateSlot / OpenSlot](../../unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs) 在新建槽位或打开未创角槽位时调用 [CharacterOpeningFlow.Begin](../../unity/Assets/Game/Runtime/Presentation/Opening/CharacterOpeningFlow.cs)；`Begin` 先按 `ShouldSkipNewbieCutscene` 判定跳过，未跳过时依次执行 `BeforeFight` → 官方录像回放 → `AfterFight` → 战斗到主场景等待 → 打开创角面板。提交角色后 [SubmitCharacter](../../unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs) 调用 `PlayFirstStory`，`FirstStory` 结束后再进入本地初始武器选择。开场剧情的跳过节流、回放时钟、快进与失败回退见下文“自定义实现”。

## 完整还原部分

共享原生模型工厂的附件几何已按当前 `871_156598` 官方骨骼／图集恢复：男性654、女性706个现有附件保留原骨骼局部坐标、权重、贴图路径、裁剪／旋转、排序与染色绑定；旧男性 prefab 中498处失效网格引用已可重建。实现与来源见[战斗执行：现有实现](battle-execution.md#完整还原部分)及[原生几何清单](../../assets/reference/character-creation/native-geometry-source-manifest.json)。全附件非零几何、有效旧 UV 一致性、骨骼变换及恢复检查通过，男女正式创角存档进入战斗的实际画面与真实开始输入、结算返回已检查。该项没有重建既有 Animator，也未将创角／家园的历史逐帧方案声明为已替换；各自完整原生动作和界面交互仍按本页缺项维护。

当前资源输入已切换至热更新 `871_156598`：男女模型重新导出并保留 GUID，创角网格包及男体原生变体按新版骨骼／附件数据核对转换，女体 451 个动画独立解码后与原版一致。转换工具与输入输出哈希见[素材管理](../asset-management.md#官方来源与转换)及 `assets/reference/character-creation/current-mesh-source-manifest.json`。Editor 已导入并编译，Play Mode 检查了男女外观、固定／自定义染色、命名、草稿恢复和初始武器，已查看男女预览截图；创角控件组合验收以 EventSystem 模拟为主，外观组 14 另经真实系统鼠标点击验证。该结果不扩大本页其余未完成范围。

地面溶解组件 `Runtime/Official/Opening/GroundDissolve.cs` 按当前官方同名类恢复材质参数更新，`OpeningGroundEffectRuntime` 按 `MyPlayableGroundEffect` 恢复曲线、播放时取实例及结束还原；已通过脚本编译。地面 shader、场景材质与组件的实际绑定及画面验证仍未完成，本段不代表地面溶解表现已还原。

固定版 `854_156383` 的创角数据、`CreatePlayerPanel`、`CreatePlayerPanelDIYContent`、选项条目、颜色滑块及 `ConfirmDialog` 已恢复为 Unity 可导入资产。`CharacterCreationAssetTools.Validate` 会绑定目录资产并核对创角 prefab、确认弹窗、音效、155 个选项 Sprite 及来源哈希；该 Editor 入口已在 Unity `2022.3.57f1c2` 执行通过。官方客户端规则定位见 [CreatePlayerPanel.cs](../../.tools/source-packages/official-code/client-source/CreatePlayerPanel.cs)、[CreatePlayerDIYContent.cs](../../.tools/source-packages/official-code/client-source/CreatePlayerDIYContent.cs) 和 [AppearanceUtils.cs](../../.tools/source-packages/official-code/client-source/AppearanceSystem.Runtime/AppearanceUtils.cs)。

开场输入由 [prepare_character_creation_opening.py](../../scripts/prepare_character_creation_opening.py) 从固定 APK 与已转换官方数据提取，输出与哈希见 [opening source manifest](../../assets/reference/character-creation/opening-source-manifest.json)：`Assets/Config/Story/CreatePlayerFightRecord.bytes`（bundle `defaultpackage_assets_config_story.bundle`，34,004 字节 GZip，解压后 14 回合、1,168 步、19 个实体、`GameResultMsg=null`）、`BeforeFight`／`AfterFight` 剧情图（`..._story_storyfight.bundle`）、`FirstStory` 剧情图（`..._story_map10story_basicguide.bundle`）、三段共 6 条对白与两名角色（840100 薇兰、840200 绯乐）、`stage` 表 20 行（`Type=33` RecordFight、`GameId=902` → 场景 `game_902`、波次怪物 3803/3282/3281/3283/3481）及网格地面。剧情 UI 由同一脚本从 `prefabs_ui_story`／`prefabs_ui_global`／`prefabs_ui_fight_fightmainpanel_extmodel_fightmainpanelrecordfight` 导出 9 个 prefab 的层级、布局、按钮、文本、图片与材质数据，供 [OpeningUiView](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningUi.cs) 在运行时还原。

创角过场从固定版本 `CreatePlayerPanel` 官方 prefab 动画数据恢复 13 个命名片段、男女两份 UIMesh 溶解网格和角色层级顺序曲线，生成资产及来源哈希见 [creation performance source manifest](../../assets/reference/character-creation/creation-performance-source-manifest.json)。`CharacterCreationPerformanceTools.BuildAndValidate` 将曲线转为 Unity AnimationClip；`CharacterCreationEffectDissolve.shader` 是官方 `UI/EffectDissolve` 的 Windows HLSL 适配，按原 dissolve 参数、边缘颜色、Blend 和 stencil 设置绘制。Editor 校验已通过；创角面板在 Play Mode 中完成了体型、外观、命名页切换与截图检查，未单独逐帧验收全部溶解曲线和音效。

六类男女外观选项使用官方配置 ID，缩略图来自 `defaultpackage_assets_image_ui_playerskin.bundle`；155 个 Sprite 和输入输出哈希见 [player skin icon source manifest](../../assets/reference/character-creation/player-skin-icons-source-manifest.json)。肤色、发色与瞳色范围、固定颜色及 HSV 配置从官方角色目录读取。

原版创角外观组合数据与男女 12 个组图已按固定版 `append_appearance_group`、`append_appearance` 表及 APK 图标 bundle 恢复；组名从固定 APK 的 `wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile` 以官方 `LocalizationBinaryFileReader`／`XXHash64` 规则解析。图标、组名、条件 ID、组件 appearance/skin ID 及输入输出哈希见 [appearance group icon source manifest](../../assets/reference/character-creation/appearance-group-icons-source-manifest.json)。`CharacterCreationFlowController.RenderAppearanceGroups` 显示无条件组并保存草稿中的 `appearance_group_id`；选择组不会改写普通捏脸字段。切换主外观页签时 `SelectTab` 刷新原生预览，使组内 `SkinType=3` 皮肤在“头发／特征”页按官方互斥规则隐藏，并在“整体／脸部”页恢复。固定版 `CreatePlayerPanel.CacheAppendAppearanceDict` 把组的 `AppearanceIdList` 转成追加外观，和 `SkinIdMap` 捏脸字段分开；本离线实现遵循用户确认的“仅用于试穿”：组只影响创建页预览，不写入最终 `WornItems`，普通部件仍写入 `WornSkins`。`AppearanceGroupId` 是所选组，`IsUseAppearanceFunction` 在首次选中非默认组时置为真；服务端对这两个请求字段的额外处理不在客户端代码中，离线保存不推断其行为。`TryOnConditionId=30110` 经官方条件代码解码为 `DomainTypeCondition(LuoTianYi)`，使组 5／6／15／16 依赖服务器 `DomainType`；离线档案没有该字段，因此当前按官方条件隐藏。

创角展示与家园主角共用 [CharacterCreationMeshFramePlayer](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterCreationMeshFramePlayer.cs)，以 Unity MeshRenderer 播放已校验的官方 24 fps 网格帧；当前转换动作是 `home/idle_01` 和 `map/run_01`，不依赖 Spine runtime。打包脚本 [prepare_character_creation_meshes.py](../../scripts/prepare_character_creation_meshes.py) 复用固定来源的骨骼转换缓存、贴图及染色遮罩，并将两种姿势实际包含的全部官方男女皮肤按单皮肤条目纳入 `StreamingAssets/CharacterCreation/creation-meshes.zip`；运行时仅按选中外观读取并逐条校验哈希，清单见 [native mesh source manifest](../../assets/reference/character-creation/native-mesh-source-manifest.json)。家园通过 [`HomePlayerActorView`](../../unity/Assets/Game/Runtime/Presentation/Home/HomePlayerActorView.cs) 使用该组件显示存档穿戴／染色。官方 `home/wakeUp_01`、`100/training_01` 尚未转换，其他角色 Animator 也未迁移。

创角按钮使用的 `Main_Page`、`Main_Cursor_Normal`、`Main_Tag`、`Main_Cursor_Back` 音效已转为本地 OGG 并由 [audio source manifest](../../assets/reference/creation-performance/audio/source-manifest.json) 校验。DIY 恢复默认外观使用官方 `ui_reset_appearance_tip_text` 确认键及官方弹窗 prefab；固定版本地语言资源未包含该键的文本，Unity 离线界面使用中文提示。

### 开场战场、录像单位与剧情排版

官方 `game_902` 战斗阶段是 2D 场景：固定版 `defaultpackage_assets_prefabs_ecentity_fight_fightstage_902.bundle`（80,054 字节，md5 `67c160d271bba532c38a428252304295`，APK 内）中的 `Assets/Prefabs/EcEntity/Fight/FightStage/902/game_902.prefab` 含 144 个 SpriteRenderer、4 个 Tilemap（2,921 块有效瓦片）、23 个 ParticleSystem、2 个正交相机（Main size 7.0），**没有任何 Mesh 或 MeshRenderer**；地面、墙体与装饰都是 sprite 与瓦片。格网不在场景内，来自 `stage` 表 20 行：27×24、origin `(-1.5,-2.5)`、69 个阻挡格、玩家入口 `(13,10)`，16 个怪物出生点与录像 `GridTransformComponent.MyPos` 完全一致。

[prepare_opening_battlefield.py](../../scripts/prepare_opening_battlefield.py) 按 prefab 世界变换把 sprite 与瓦片展平为按图集页、排序层和 order 分组的四边形批次（142 批、15,816 顶点、23,724 索引），导出 10 张官方图集/特效贴图、23 个粒子挂载和两个相机，写入 `StreamingAssets/Opening/battlefield/game_902.json`；来源与逐贴图哈希见 [opening battlefield source manifest](../../assets/reference/character-creation/opening-battlefield-source-manifest.json)。APK 中 `you` 粒子系统的 `ParticleSystemRenderer` 材质数组只有空 PPtr（path_id 0），该项保留空材质引用，运行时不绑定贴图；其余 22 个粒子材质贴图均有来源，不存在未找到的粒子纹理。来源清单没有未解析对象或警告。[OpeningBattlefield](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningBattlefield.cs) 在运行时装配批次、粒子与相机，单位根同时携带场景根偏移 `(5000,0,0)` 和格网 origin，录像单位与 NPC 直接使用官方格网坐标。原场景 shader 只有 Android GLES3 程序且工程未引入 URP；场景批次仍以 Unity 内建 `Sprites/Default` 绘制，属渲染管线适配差异。粒子 shader 已按上文改为 Windows HLSL；独立的 `Sprites/Ground-Dissolve` 材质与地面运行时绑定仍待完成。

[prepare_battle_spine_pages.py](../../scripts/prepare_battle_spine_pages.py) 从固定 APK 中经 `models-source-manifest.json` 验证的 Texture2D 解出 5 只开场怪物（3803／3281／3282／3283／3481）与两名剧情角色（8401／8402）的 9 张图集页，核对 atlas `size:` 后绑定到既有 Spine 材质；`OfficialBattleModelCatalog` 的 identities／identitiesManifest 已指向真实资源。剧情角色不是玩家外观，而是官方 NPC 骨骼：录像中 1017 的 `NpcClassId=8401` 对应 `body_8401`、1018 的 8402 对应 `body_8402`。这两具骨骼不在 409 条战斗模型身份表内，因此新增 `Resources/Opening/OpeningNpcModelCatalog` 资产直接引用已转换的 SkeletonDataAsset，[OpeningFightPlayback](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningFightPlayback.cs) 优先按该目录生成单位（动作按官方 clip 同名回退），怪物仍走 `OfficialBattleModelCatalog` 的身份表。单位排序显式置于场景排序层之上，避免被 21 层的背景遮住。战前顺序按官方 `GAME_ENTITY_LIST` 先于 `BeforeFight`：进入场景 → 生成实体与 HUD → `BeforeFight` → 录像 → `AfterFight`。

开场技能载荷现从 `battle-effects-source-manifest.json` 校验并导出录像涉及的完整官方事件目录：139 个事件条目、35 个 `effectType:ClassId`、258 张贴图和 47 个网格；其中含 44 个粒子事件（538 个主粒子系统、38 个跟随子系统及 2 个 SubEmitter）、39 个音效事件、31 个 `special` 事件、16 个 Animator 与 9 个 OverrideAnimator 事件。[OpeningSkillEffectPlayer](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningSkillEffectPlayer.cs) 恢复粒子源矩阵、主模块、Emission、Shape、Renderer、Noise、UV、Velocity、Size/Color over lifetime、Trail、贴图与网格；295 种官方粒子材质由 Windows HLSL `Zjcs/Opening/NoiseMaskPro` 承接，保留源材质参数及贴图。跟随粒子、嵌套 SubEmitter、31 个特殊事件和 31 个本地音频文件均纳入资源清单；Animator/OverrideAnimator 按官方触发名或覆盖动画名解析已有 Spine 动画。可见 Play Mode 验收已实例化并检查 578 个粒子系统及材质、贴图、网格和音效绑定；录像中技能粒子与状态特效有画面截图，战斗音效及 Animator 事件也实际触发。来源与哈希见 [opening skill effect source manifest](../../assets/reference/character-creation/opening-skill-effects-source-manifest.json)。

Unity 2022.3 粒子参数适配：源 `boxThickness` 写入 `ShapeModule.boxThickness`，`placementMode` 写入 `meshShapeType`，UV `animationType` 写入 `TextureSheetAnimationModule.animation`，拖尾材质绑定到 `ParticleSystemRenderer.trailMaterial`。录像旋转用 `Quaternion.Dot(q, q)` 检查零长度；抓钩轴向先在 Vector3 中相减再转换为 Vector2。上述代码已通过 Unity Editor 导入编译；下述开场 Play Mode 复验确认战场粒子、技能特效和状态特效均可见。

隔离最小工程（仅 Opening/Combat 程序集与官方载荷）在 Unity `2022.3.57f1c2` Play Mode、3840×2160 Game 视图实测：142 个战场批次与 23 个粒子挂载、官方正交相机（size 7.0）全部装配且批次均有贴图；录像 18 个单位（16 怪物 + 2 剧情角色）按官方格网生成并渲染；`BeforeFight` 官方台词、角色名与两名官方立绘显示；快进结束。实测修掉三处迁移缺陷：单位缩放重复应用了 SkeletonDataAsset 已包含的 0.01 导入缩放（渲染仅 0.0006）、NPC 8401 缺少 `body_8401` skin 且其 SkeletonDataAsset 指向未转换的 atlas GUID。随后在隔离主工程副本的可见 Play Mode 中复验完整录像交接：`game_902` 的 142 个场景批次、23 个挂载粒子、18/18 录像单位、官方战前/战后剧情、快进与创角页交接均通过；技能和状态特效、伤害数字与战斗音效已通过画面／事件断言。独立视觉用例实例化 578 个开场粒子系统，并截图核对战场、立绘、技能特效和伤害帧。运行截图不纳入仓库。

剧情 UI 图集按 4096 上限分页导出（`atlas.png`／`atlas-1.png`／`atlas-2.png`），47 个 Sprite 引用全部解析。`OpeningUi` 使用原 TMP 字号、对齐、边距、颜色及自动缩放字段，`OpeningUiView.PlayAnimation` 按原层级执行 `TalkDialog_Show`（0.333 秒）、`ExitFightTransitionPanel_Show`（1 秒）、`StoryTransitionPanel_Show`（2 秒）及 `StoryPanel_Show`（13.217 秒、124 条曲线）；材质动画由 `OpeningUiMeshGraphic` 承接。六条官方语音从 APK 提取为 PCM，按每句先停止上一条再播放。

`OpeningFightPlayback` 按录像时序分发技能、伤害、状态和动作实体。飘字使用原 `CharacterDamageUI` prefab、五套位图字体与 AnimationClip 曲线，按原元素色、暴击放大及底线显示；数值沿 zh_CN `FormatBattleByFourWidth` 缩略。同目标连续命中按 `DamageUISpacing (0,-0.1,0)` 偏移，确切分组／回收窗口仍未核对。单位动作由技能自己的 `MyFightSkill` playable 触发，通用 Action 20001 的 `myEffect` 为空，不覆盖为统一 `skill_01`。

### 开场对话与模型绑定修复

`OptionTalkDialogPanel.DoInit` 调用 `OptionSlot.Reset`，隐藏 prefab 中预置的五个选项条目，避免黄色选项覆盖人物名；`OpeningStoryPlayer` 沿此初始化顺序恢复。姓名区从固定 APK 的 `OptionTalkDialog.prefab` 重新提取 `HorizontalLayoutGroupJSZX`、`ContentSizeFitter` 与 `LayoutElement`，共恢复 22 个原组件，复用已经迁移的官方 JSZX 布局代码，保留原 padding、spacing list、布局标志及自适应尺寸。增量提取入口为 `python -B scripts/prepare_character_creation_opening.py --ui-layout-only`，组件来源和输入／输出哈希写入 `opening-source-manifest.json`，不重绘界面或替换 Sprite。

录像单位保留官方 SkeletonDataAsset 的 `0.01` 导入比例，取消按首只怪物高度再归一为 `1.2` 的额外缩放。`scripts/prepare_opening_model_transforms.py` 从 APK 的 7 个战斗实体 prefab 提取完整父链：3281／3282／3283／3481／3803 的模型 XY 缩放为 0.3，4201 为 0.3、4202 为 0.23；落脚 Y 偏移分别保留 -0.5、-1、-2.5。`Opening/model-transforms.json` 按录像 ClassId 显式绑定，清单记录每层 Transform 的 pathID、原始 SHA256 与 APK 来源，未用截图估算尺寸。怪物原材质的 `_StraightAlphaInput = 1` 同步提取并恢复，避免把直通透明贴图当作预乘透明输入而出现白块；`Resources/Opening/OpeningShaderVariants.shadervariants` 显式保留对应 Windows shader 变体，同时保留转场所需的四个 UI 网格 shader，防止只通过运行时 `Shader.Find` 使用的资源被构建剔除而在战后黑屏；原贴图未重绘。播放结束释放资源前保留累计特效计数，使完整回放的验证结果不被清零。开场粒子网格的五个 CRLF 文件恢复为原 LF 字节，逐项匹配 `skill-effects.json` 中既有 SHA256；`.gitattributes` 固定这些载荷的换行，未改预期哈希。粒子与抓钩附着点无法解析为 Spine 骨骼时，沿 `AttachPosRotCfg.GetBone` 回退目标根节点，避免错误的 `BoneFollower` 在每帧重试并刷错；尚未恢复的 prefab 附着别名不计为已完整还原。`scripts/test_opening_payload_integrity.py` 覆盖所有声明的网格哈希、UI 来源绑定、录像角色与官方 prefab 变换覆盖。

本次修复已通过 4 项载荷／Windows shader 打包回归检查、`check_unity_project.py`（39,859 GUID）、Unity `2022.3.57f1c2` 导入编译和 `Zjcs.Editor.ProjectTools.Validate`。非 `-nographics` Play Mode 完整回放通过：578 个粒子系统绑定可实例化，165 条伤害、17 次死亡动作、358 次特效事件与 1,548 个特效系统，录像自然结束。Windows Player 已实际启动并核对 450×800 画面、薇兰／绯乐姓名、空闲对白保持、真实鼠标点击推进、越过第 2 回合、战后两段对白及最终体型选择页；修复了仅在 Player 出现的透明变体剔除和战后 UI shader 缺包黑屏。发布入口为 `D:/Desktop/杖剑传说/杖剑传说.exe`，桌面快捷方式目标已核对；重启后测试槽可重新进入开场。测试仅使用新增槽位，原有槽位 4 个文件的 SHA256 均未变化；新增测试槽已移出游戏存档目录并保留临时备份。此版本包含当前工作区既有测试内容，完整创角、完整单机迁移和本页所列材质缺项仍未完成。

### 官方字体与原生 UI

已核对固定 APK 的 `defaultpackage_assets_textmesh_pro_rawfonts_fzy4k.bundle` 中 `FZY4K` TextAsset：原始方正粗圆 TTF 与 `Assets/Official/Commerce/Art/fonts/FZY4K.ttf` 逐字节一致，SHA256 为 `90023872dffe6a58503de655bd0631bf3334ce3b6b72cc75a6625b685c94c386`，创角使用 `Resources/Combat/FZY4K-Dynamic.asset`，没有使用替代字体。

旧恢复 prefab 缺少原 MonoBehaviour 字段，不能显示完整 UGUI/TMP 内容；[prepare_character_creation_runtime_ui.py](../../scripts/prepare_character_creation_runtime_ui.py) 与 [CharacterCreationUiTools](../../unity/Assets/Game/Editor/CharacterCreation/CharacterCreationUiTools.cs) 从已核验原始字段恢复三个原生 prefab、47 张原 Sprite、RectTransform、TMP 样式、遮罩、布局、滚动、CanvasGroup 及选择状态。页标题沿原 `StepTitles` 与语言包显示“选择体型／装扮形象／取个名吧”，HSV 滑块沿 `EditCustomColorBlock.GenerateTexture` 生成渐变。输入输出及生成工具哈希见 [runtime UI source manifest](../../assets/reference/character-creation/runtime-ui-source-manifest.json) 与 [native UI source manifest](../../assets/reference/character-creation/native-ui-source-manifest.json)。

### 捏脸与本地保存验证

已在 Unity `2022.3.57f1c2` 非 `-nographics` Play Mode 实际运行 [CharacterCreationPlayModeAcceptance.PrepareAndRun](../../unity/Assets/Game/Editor/CharacterCreation/CharacterCreationPlayModeAcceptance.cs)，使用隔离存档，检查 720×1280 与 450×800 Game 视图截图：男女体型、女性六类外观、肤色／发色／瞳色固定色板、自定义 HSV、原生动画帧推进、发型选中框、重置取消／确认、返回与草稿恢复、空名拒绝、手动命名、正式 JSON 保存后重新打开、两个槽位独立保存，以及剑／法杖选择均通过。原字体对创角初始文本的字形覆盖检查通过，角色网格、渐变条、文本和底部导航已人工核对截图。

捏脸保存修复了 `OfflineMainlineProgressSession.UniqueAppearanceTypes` 误拒绝官方 `PlayerSkinType.Eye = 0` 的问题；旧逻辑会使已创建角色回读失败。`LocalJsonSaveFile` 对 Windows 短暂占用导致的原子替换错误作有限重试，保留原有原子替换与有效存档。创角启动时移除商城控制器自动创建的额外存档选择遮罩，避免拦截创角输入。

`CharacterCreationPlayModeAcceptance.PrepareAndRun` 覆盖创角资产绑定、体型、外观、染色、命名、草稿恢复、正式 JSON 存档与初始武器；该用例跳过创角前开场，但保留创角后 `FirstStory`。隔离复验通过系统鼠标点击外观组 14，确认组选择不改普通捏脸字段、头发／特征互斥预览刷新，并运行两段薇兰对白、三秒 `StoryStun` 家园动作及初始武器交接；保存读回断言普通发型写入 `WornSkins`、试穿组未写入 `WornItems`，染色也与磁盘记录一致。`CharacterOpeningPlayModeAcceptance.Run` 在隔离主工程副本另行覆盖战前对白、录像解析与单位生成、快进、战后剧情和创角面板交接；两处真实系统鼠标点击（开场 NextBtn、组 14）分别通过。当前仍是分段验收，未执行一次连续的 Bootstrap→完整录像→创建→捏脸物理输入流程；动画／服务器差异按本页缺项说明。

捏脸各项控件的组合验收以 EventSystem 射线命中和软件指针事件驱动，系统鼠标另行实际点击过组 14；文本通过 TMP 输入值验证，尚未用实体键盘逐字符输入；槽位、弹窗及武器按钮部分直接派发按钮事件。正式存档验证为同一 Play Mode 内重新打开服务读取磁盘，未覆盖退出 Play Mode 或重启游戏后的完整恢复流程。隔离工程副本的 Unity Editor 编译与 Play Mode 已运行；根工程静态入口 `scripts/check_unity_project.py` 既有检查通过（29,733 GUID），不替代 Editor 导入、编译与运行。该次捏脸验收未构建 Windows 成品；本次开场修复的 Windows 发布与实际运行范围见上文，完整捏脸流程仍未作重启后的连续验证。

Windows Player 的存档入口保留列表按钮原有 86 单位高度，关闭纵向布局组对按钮高度的自动控制，避免无布局首选高度时条目被压为零。通过新建测试槽实际进入开场场景，最终构建重启后已在实际画面确认存档条目显示；续玩点击、完整创角／初始武器／家园和地图全流程仍未验收。

开场战斗的两名剧情 NPC 现在按固定 APK `854_156383` 的 FightNpc 4201／4202、外观组合 8401／8402 恢复身体与 `pWeapon_100_G_001`／`pWeapon_200_G_001` 武器皮肤；重建原控制器别名及录像技能 `MyPlayableOverrideAnimator` 引用的骨骼动画，使用已确认的 Spine 原生迁移方案。`prepare_opening_fighters.py` 提取原贴图、骨骼、蒙皮与动作；`OpeningFighterAssetTools` 显式绑定已批准的 Spine shader，避免同名 Android 占位 shader 被动态查找选中。角色名字、双方血条的尺寸、位置、分阵营颜色与九宫边框沿官方 prefab 恢复，血量消费录像内真实伤害与治疗。

`prepare_opening_unit_ui.py` 恢复 `GamePlayManagerConfig.FactionEffectDatas`、NPC／怪物头像和 `GameRoundUI` 参数。行动条在战前显示全部 18 个头像，按录像 NextTime、官方最小间距及 0.2 秒移动参数排列，当前行动者放大，并播放原受击和死亡动画，死亡头像保持 0.5 秒后退出。`prepare_opening_damage_ui.py` 恢复五套原位图字体、字距／UV、元素颜色、普通／暴击 prefab、原曲线、暴击底线与 2×2 序列光圈；`ElementConfrontationUI.Reset` 对内容的隐藏不会被父节点动画重新启用。

Boss 3803 按官方模型 prefab 恢复 107 个节点、47 个粒子系统、AttachmentFollower、原网格复制与描边、两组模板缓冲遮罩及星空纹理。Windows shader 由官方 `Spine/Default-Stencil`、描边和 `UI/GameDamage` 编译程序适配；保留原深度间隔、模板值与混合参数，正确读取 Unity 的 0／1 开关及常量粒子颜色，修复白色翼形遮挡。原版保留的细描边仍存在。开场数据保持固定版，与本页创角 `871_156598` 资源分开。输入对象、官方类、输出字节及 Unity 绑定哈希见 `assets/reference/character-creation/opening-source-manifest.json` 与 `opening-skill-effects-source-manifest.json`；47 个运行时网格按原清单字节以二进制 Git 属性保存，避免换行转换导致离线加载哈希失败。

[prepare_opening_action_motion.py](../../scripts/prepare_opening_action_motion.py) 从固定 APK 的 `config_ec`、11530／11830／15300 技能 prefab 提取 11532／11833／15301 移动组件、`MyPlayableTrack` 与 `MyCurveTrack` 的原时长、速度、加权曲线和转向参数，输出 `Opening/action-motion.json`。`OpeningBattleActionPlayer` 消费录像的 11 个移动实体／12 段路径，涵盖 NPC 冲刺、跑动、回位及怪物拖拽；逻辑终点先更新，画面沿原曲线移动，动作结束按 `MyGridTransform` 重置。跑动使用 15300 的原 `run` 触发器；技能按原事件延迟、速度和完整骨骼名播放。薇兰分别使用 `100/skill_20、05、41、06、27`，绯乐使用 `200/skill_13、12、16、11`；以录像 1.5 倍速度更新骨骼与移动时钟。

受击材质沿 `MeshMaterialProperty` 只处理角色外观的独立图集材质，通过 `SkeletonAnimation.CustomMaterialOverride` 绑定与还原；不替换粒子、网格复制及不含受击属性的模板遮罩。`OpeningSpineHit` 按原 `SkeletonEffect-2D` 的 `_HITEFFECT` GLES 片段恢复透明裁剪、乘透明度的颜色混合及预乘输出，保留材质的模板、ColorMask 和输入透明模式，修复精英怪／Boss 受击时透明图集区域变为紫色块。 Windows 显式加载本地 `OpeningSpineHit` shader，先克隆原材质再切换 shader，只保留适用的透明模式关键字；Boss 描边按 `AppearanceRendererLite`／`CustomRenderExistingMesh.AddRuntimeReplacementMaterial` 同步登记受击克隆材质，恢复时回收映射，避免描边变成缺材质的粉色网格。四个剧情 UI shader 的 `_ClipRect` 声明移到公共裁剪函数之前，确保 Windows 裁剪变体编译通过。

可见 Play Mode 已自然播放完整 14 回合，核对 18 单位／血条／头像、两组星空遮罩、165 条飘字、死亡保持及音效。新增回归检查验证透明像素保持 RGBA(0,0,0,0)、不透明像素仍有受击色；旧 shader 在相同检查中输出 RGBA(0.698,0.098,1,0) 并失败。两名 NPC 的实际根节点位移超过 4 格和 5 格，9 个专用技能动画全部触发，受击期间 Boss 模板材质和子粒子材质保持原绑定，描边网格各子网格的材质均非空。Windows 构建、`ProjectTools.Validate` 与真实鼠标输入复验已通过：自然播放开场战斗，连续采样的 100 帧未再出现透明紫块或缺材质粉色网格，战后对白可进入创角并恢复测试槽草稿；用户原有存档槽的四个文件哈希保持一致。发布所需的 11 处既有编译依赖修复（商城、家园、存档入口及视频模块）已按用户确认纳入：保留已实现的周期卡接口、家园路径与角色节点入口、商城原生动画事件接收及视频模块，修正方法签名和数值转换。同步最新地图提交后，补齐编辑器对官方 UI 程序集的引用，并将地图界面截图协程与未提交的神像检查拆开；这些连接修复不改变开场规则。合入所需代码的隔离基线已通过静态检查、完整开场 Play Mode、`ProjectTools.Validate` 和 Windows 构建；随后地图小地图标记提交只增加状态分支，不改变开场代码。正式更新须以本次提交重新构建，并核验桌面 Player 输入及画面。

## 自定义实现

存档槽、创角草稿和正式角色资料由 [CharacterCreationFlowController](../../unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs)、[CharacterCreationOfflineService](../../unity/Assets/Game/Runtime/Offline/CharacterCreation/CharacterCreationOfflineService.cs)、[CharacterCreationDraftStore](../../unity/Assets/Game/Runtime/Offline/CharacterCreation/CharacterCreationDraft.cs) 与 [ProfileSlotCatalog](../../unity/Assets/Game/Runtime/Offline/State/ProfileSlotCatalog.cs) 接入本地 JSON 保存。各槽独立保存草稿；提交角色时写入选中槽，草稿损坏可保留原文件副本后重新开始，不覆盖正式角色数据。写入失败会阻止页面切换并显示提示。

离线流程不提供随机名字。官方 `CreatePlayerSetNameSubPanel` 的随机名调用服务端 `OperPlayerGetRandomName(Sex)`，没有本地生成规则；按用户确认隐藏随机名按钮，玩家必须手动输入名字。

初始武器选项依据官方 `ChooseInitialWeaponNode.ProcessSucc` 和 `WeaponType` 数值 `200`（剑）、`201`（法杖）写入本地角色。由于官方节点由服务器故事流程触发，离线实现用本地按钮承接该选择；选择完成后进入人物页入口。

官方染色遮罩 `ethnicity_06`、`ethnicity_07`、`ethnicity_22` 至 `ethnicity_27` 共 8 张经用户核对固定 APK、相关原版本 CDN 包及新版同名包仍缺失。按用户确认保留全部选项；缺少遮罩时沿官方空遮罩行为不染色，预览不生成或替代官方遮罩。

开场跳过条件沿用官方 `MainUIRoot.mSkipNewbieCutsceneConditionId = 21003`：`CompositeOrCondition[21001 PlayerChannel(130077), 21002 PlayerMedia(C520612)]`。离线 SDK 桩的渠道／媒体均为空，因此判定为不跳过；`CharacterOpeningFlow.ShouldSkipNewbieCutscene` 保留该白名单比较，未另造“看过一次就跳过”的规则。每槽观看状态按用户确认只做进程内标记（`PlayedSlots`），重启游戏后未创角槽位会重新播放，不写入槽位存档。

开场播放由 [OpeningStoryPlayer](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningStoryPlayer.cs) 按官方节点语义执行：`StartStoryNode` 不阻断；`WaitForSecondsNode` 在 `NeedMask` 时先挂全屏透明遮罩再等待，遮罩由后续对话或开面板节点的 `Reset` 关闭；`DialogNode` 打开官方 `OptionTalkDialog` 层级，按 `dialog.csv` 的 `StoryName+DialogId`、`Order` 顺序显示对白与角色名，Windows 单机按用户要求逐句点击推进；保留源 `AutoPlay` 数据，但不启用空闲自动播放（官方实现先等待 120 秒，再每 2 秒推进）；`ShowBlackMaskNode`／`CloseBlackMaskNode` 为硬切纯黑遮罩；`HideAllUINode`／`ShowAllUINode` 在节点结束时生效；`OpenShowPanelNode`／`CloseWindowNode` 打开和关闭 `Story/ExitFightTransitionPanel`；`LeaveStoryFightNode` 关闭战斗场景；`PlayBlackScreenStoryNode` 打开 `Global/FirstStoryPanel` 并在 `mIsKeepBlackScreen` 时保持黑幕；`PlayEnterGameTransitionNode` 打开开场转场面板；`PlayPlayableNode` 的两个 GUID（`e52fba61…`、`32bc3f4c…`）不在固定版五包 AssetTable 内，按官方空句柄行为跳过并记录。`HomePlayerPlayActionNode` 已连接 `StoryStun`：官方 `player.json` 的 `StoryWakeUp` 图将角色移到 `player_wake_point`、应用 `player_stun`，再以非循环方式播放 3 秒 `home/wakeUp_01`。离线实现用官方 Home 场景、床铺状态、玩家变换与已保存外观创建角色；固定来源中未找到锚点和该动画的可播放网格帧，所以以 3 秒起身摆动近似，不能视为原动画还原。`SafeEnd` 后其余仅展示类节点仍按 `ClientRunNode.InternalSkip` 语义跳过，仅保留关闭、离开和 UI 环境节点。

录像回放由 [OpeningFightPlayback](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningFightPlayback.cs) 驱动：先 GZip 解压并按 `GameServer`→`Console` 之外的原始 JSON 解析 `FightProcessRecordData`（[OpeningRecord](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningData.cs)），生成实体与回合步骤；回放按官方 `NormalPlaybackStrategy` 的时钟模型逐回合、逐步骤等待 `step.Time`（相对回合起点），并按官方 `CreatePlayerRecordFightSpeed = 1.5` 折算实际等待；`GAME_ENTITY_ADD`／`GAME_ENTITY_REMOVE` 增删单位，`Fight*Round*Modification` 更新行动头像，单位动作消费录像 ActionComponent 与官方 playable，`GAME_FASTFORWARD` 走跳跃到结尾（应用录像内快照），`GAME_PLAY_STATUS_CHANGE` 与 `GAME_MODIFACTION_RUN_BATTLE_END`／`_RUN_END` 记录状态。快进按钮按官方 `IsShowRecordFightSkipButton || Gm` 决定是否请求，延迟 `mShowSkipBtnTime = 500` 毫秒显示，点击后只推进到结尾而非加速；实装 `IsShowRecordFightSkipButton = false`，正常流程不显示按钮。

事件顺序按官方控制：`BeforeFight` 播放完成后才开始录像，录像结束触发 `AfterFight`，`AfterFight` 内 `LeaveStoryFightNode` 发起战斗到主场景切换；[CharacterOpeningFlow](../../unity/Assets/Game/Runtime/Presentation/Opening/CharacterOpeningFlow.cs) 等待该切换最多 5 秒，超时记录错误后仍打开创角面板，并使同一槽位只打开一次创角、只记录一次已播放。加载失败（缺文件、解析异常）按官方“回放返回 false”分支记录错误后直接打开创角。创角成功后的 `FirstStory` 先关闭创角面板，再以黑幕播放剧情，`FirstStory` 结束（或本轮无剧情可播）后进入本地初始武器选择，初始武器仍按已确认的离线按钮承接官方 `ChooseInitialWeaponNode` 的 `200`／`201` 数值。

已在 Unity `2022.3.57f1c2` 隔离主工程副本运行 [CharacterOpeningPlayModeAcceptance](../../unity/Assets/Game/Editor/CharacterCreation/CharacterOpeningPlayModeAcceptance.cs)（未使用 `-nographics`，720×1280 Game 视图）：官方跳过条件 21003、三段剧情与对白、19 实体／14 回合／1168 步录像、`game_902` 场景、142 个 SpriteRenderer 批次、23 个粒子挂载、18 个有 Spine 动画的录像单位、快进、战后转场、创角页交接与同进程单次播放均通过。独立视觉验收实例化 578 个开场粒子系统；录像技能特效与状态粒子可见，伤害数字、状态动画、音效与单位动作均通过断言。验收另核对 `TalkDialog_Show` 0.333 秒、`ExitFightTransitionPanel_Show` 1 秒、`StoryTransitionPanel_Show` 2 秒、`StoryPanel_Show` 13.217 秒与 124 条曲线，实际检查转场中间帧、最终透明度和 FirstStory 材质动画。另以 Win32 系统鼠标点击官方开场 `NextBtn`，对白推进通过。运行截图不纳入仓库。

## 尚未实现

以下缺项按影响范围分列；共用战斗规则与场景装配仍由[战斗执行](battle-execution.md)维护，不在此重复。

### 开场战斗表现资源

1. **战场地面溶解材质。** 开场粒子事件目录和 295 种粒子材质已接入 Windows shader 适配，独立的 `Sprites/Ground-Dissolve` shader 仍只有固定包中的 Android 编译程序；`OpeningBattlefield` 也尚未把地面材质、`GroundDissolve` 曲线与 `OpeningGroundEffectRuntime` 绑定。该项待用户确认 Windows 2D HLSL 适配后实现并验证。
2. **其余怪物 Spine 图集页。** 开场 5 只怪物与 2 名剧情角色的 11 张图集页已补齐并绑定；1678 个战斗骨骼引用的 2,348 个页名中仍有 2,301 张（约 98%）未导出，其他玩法遇到未补页的模型仍会跳过渲染。
3. **玩家外观单位。** 录像剧情角色按官方 NPC 骨骼渲染；自建角色进入战斗时的官方复合外观、换装与战斗动作仍未接入，模型目录目前只覆盖怪物与 NPC。
4. **其余状态语义。** 开场两名 NPC 的武器、控制器动作和技能替换动画、单位血条、行动头像与原生伤害飘字已恢复。`Blinding`／`SuperArmor` 等其余动作、完整状态图标与 Boss 阶段控制仍须继续核对，不将本次开场表现修复扩大为通用战斗全部表现完成。

### 剧情表现资源

`FirstStory` 的对白到初始武器选择及 `StoryStun` 家园节点已接通。家园节点使用官方场景和角色网格，但 `player_wake_point` 与 `home/wakeUp_01` 的固定版动画数据未进入现有原生预览包，当前以 3 秒起身动作近似。AfterFight 的两个 Playable GUID 在固定包内缺失，且位于纯黑遮罩期间、官方节点不等待资源时长；离线按官方空句柄行为跳过，不改变可见画面及剧情时序。

### 仍需验证的部分

5. **异常分支验收。** [CharacterOpeningFaultPlayModeAcceptance](../../unity/Assets/Game/Editor/CharacterCreation/CharacterOpeningFaultPlayModeAcceptance.cs) 已在隔离工程通过：缺失录像、缺失剧情、损坏图集页、缺失战场载荷各自抛出文档记录的异常类型，且载荷失败后流程仍回调打开创角。超时与保存失败分支仍未构造。
6. **实际输入验收范围。** [CharacterOpeningRealInputPlayModeAcceptance](../../unity/Assets/Game/Editor/CharacterCreation/CharacterOpeningRealInputPlayModeAcceptance.cs) 以 Win32 `SetCursorPos`/`mouse_event` 聚焦并点击官方 `NextBtn`，确认 `BeforeFight` 对白推进且不调用 `onClick`；创角验收也已真实点击组 14 并确认切换结果。两处系统鼠标输入分别通过；本次 Windows Player 另核对开场、完整录像、战后对白到体型选择页。体型选择后连续操作到捏脸页的单次物理输入仍未验收，键盘输入亦未单独覆盖。
