# 新建存档与角色创建

[总索引](../README.md)

世界人物皮肤几何补充：`prepare_character_world_append_geometry.py` 从已保存并校验的871原生骨骼数据包补出男性16,588／女性17,358个 region／mesh 附件，保留原坐标、权重、图集及骨骼顺序，并逐项核对原创角几何未变。运行载荷仍为 `CharacterCreation/native-character-geometry.json`，当前派生来源及输出哈希见[世界人物附件清单](../../assets/reference/character-creation/world-append-geometry-source-manifest.json)；原[基础几何清单](../../assets/reference/character-creation/native-geometry-source-manifest.json)保留初始转换阶段的凭据。世界模型工厂按当前穿戴显式装入缺少的附件和原纹理，并按原骨骼槽序设置绘制顺序。不代表全部服饰、附件切换、裁切及动画约束均已完成。

状态：未完成

## 功能简介

从新建本地存档开始，完成体型选择、外观捏脸、手动命名与初始武器选择；角色创建完成后可打开人物页。正式角色换装与衣柜规则见[人物属性、经验、位阶与衣柜](character.md)。

### 当前离线开场实现

[CharacterCreationFlowController.CreateSlot / OpenSlot](../../unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs) 在新建槽位或打开未创角槽位时调用 [CharacterOpeningFlow.Begin](../../unity/Assets/Game/Runtime/Presentation/Opening/CharacterOpeningFlow.cs)；`Begin` 先按 `ShouldSkipNewbieCutscene` 判定跳过，未跳过时依次执行 `BeforeFight` → 官方录像回放 → `AfterFight` → 战斗到主场景等待 → 打开创角面板。提交角色后 [SubmitCharacter](../../unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs) 调用 `PlayFirstStory`，`FirstStory` 结束后再进入本地初始武器选择。开场剧情的跳过节流、回放时钟、快进与失败回退见下文“自定义实现”。

## 原始官方实现

官方创角由开场、体型、外观／染色、命名及初始职业／武器流程组成；模型、色板、部件、肖像与动画均来自官方 prefab、骨骼、配置及客户端调用。

### 原始官方开场与创角流程

本功能范围包含创角前的初始剧情战斗。以下依据固定版 `871_156598` 客户端反编译代码记录官方调用顺序；后文分别标出已迁移实现和仍未恢复的表现。创角面板自身的溶解、跑动和页面转场动画与这段剧情战斗分别验收。

1. **判定入口。** [MainUIRoot.Start / ShouldSkipNewbieCutscene](../../.tools/source-packages/official-code/client-source/MainUIRoot.cs) 在主玩家存在且名字为空或 `IsPreCreateInit` 为真时，订阅剧情结束事件并延迟隐藏加载页。`mSkipNewbieCutsceneConditionId` 对应的条件满足，或玩家 ID 等于静态字段 `sLastPlayStoryPlayerId` 时直接打开创角；否则调用 `StartStoryFight`。条件 ID 不大于 0，或配置条件不存在时不跳过，后者记录错误。固定版 `defaultpackage_assets_prefabs_ui_main.bundle`（MD5 `5ce6cf884ed57ba5d3aeaf1222681726`）序列化的 `MainUIRoot.mSkipNewbieCutsceneConditionId` 为 `21003`。该静态字段是当前进程内标记，不能直接解释成已持久化的每存档观看记录。
2. **读取官方开场录像并进入战斗。** `MainUIRoot.StartStoryFight` 订阅场景切换，安装 [CreatePlayerStoryFightController](../../.tools/source-packages/official-code/client-source/CreatePlayerStoryFightController.cs)，读取 `ConfigConst.CONFIG_PATH + "Story/CreatePlayerFightRecord.bytes"`，经 GZip 解压，再由 [GameRecordManager.ParserRecordSaveDataFromBytes / TryPlaybackLocalRecord](../../.tools/source-packages/official-code/client-source/GameRecordManager.cs) 解析为 `FightProcessRecordData` 并启动本地录像回放。这是官方预录战斗流程，不能用临时编排一场普通战斗替代。若 `TryPlaybackLocalRecord` 返回 `false`，原入口清除剧情战斗控制器、打开创角并取消场景订阅；GZip 解压异常只见记录错误，不能据此推定所有加载／解析异常均会安全回退。
3. **播放战前剧情，再开始回放。** [FightMainPanelRecordFight.Start](../../.tools/source-packages/official-code/client-source/FightMainPanelRecordFight.cs) 将玩家置为 `JustWatching`，按录像中的 NPC 设置观战对象，并调用剧情战斗控制器。`CreatePlayerStoryFightController.OnEnableFight` 对未创角玩家播放 `BeforeFight`，等待结束后才调用 `StartPlayFight()`；若剧情入口未成功启动，`PlayStory` 仍调用后续回调。剧情图内部的对白、镜头、单位动作及具体节点顺序须继续从官方资源恢复。
4. **回放与快进。** 控制器仅在 `GameSettings.IsShowRecordFightSkipButton` 为真或玩家为 GM 时请求显示跳过按钮；`FightMainPanelRecordFight` 按 `mShowSkipBtnTime` 延迟显示，按钮调用 `DoFastforward()`。须核对 prefab 实际参数，不能默认按钮始终可用，也不能把快进实现为直接跳入创角。战斗模型、场景、技能、音效和剧情事件应消费同一份官方录像时序。
5. **播放战后剧情并返回主场景。** 战斗结束触发 `CreatePlayerStoryFightController.OnGameEnd`：未创角玩家播放 `AfterFight`；已正式创角玩家走 `ResetGame()` 分支。`MainUIRoot.OnStoryFinish("AfterFight", ...)` 记录 `sLastPlayStoryPlayerId`，再由 `OnPostStoryFinish` 等待 `Fight → Home` 场景事件设置 `mWaitSceneFightToMain`，最多 5 秒；超时记录错误后仍尝试打开创角。战后剧情图中离场与转场节点的实际绑定仍须恢复。
6. **打开创角。** `OpenCreatePlayerPanel` 在未命中配置跳过条件时播放黑幕，打开全局 `CreatePlayerPanel` 后关闭黑幕、释放剩余加载页延迟任务并发出 `OnPrepareCreatePlayerReady`。进入战斗场景时，`OnSceneUpdate` 也会释放加载页延迟任务。销毁入口或战斗面板时须沿原调用关系解除事件订阅、清除控制器并取消剧情令牌，避免重复触发。
7. **创建外观、命名与创角后剧情。** 原 `CreatePlayerPanel` 承接体型、外观与命名；[CreatePlayerSetNameSubPanel.ConfirmCreate / RunFirstStory](../../.tools/source-packages/official-code/client-source/CreatePlayerSetNameSubPanel.cs) 正常新角色分支先提交 `OperPlayerCreateAppearance`，成功后提交 `OperPlayerCreateCharacter`；预创建分支使用 `OperPlayerPreCreateCreateCharacter`。成功后清除预创建状态，关闭创角面板，在 `FirstStory` 存在且 `CheckCanRun` 允许时启动该剧情，使用黑幕等待剧情窗口打开，并发布玩家信息；`MainUIRoot` 收到 `FirstStory` 结束时恢复 Home 音乐。[ChooseInitialWeaponNode.ProcessSucc](../../.tools/source-packages/official-code/client-source/ChooseInitialWeaponNode.cs) 在服务器故事节点成功后写入初始武器，但它与 `FirstStory` 的具体连线及触发位置尚未核对剧情图，不将其推测为固定节点顺序。离线手动命名、初始武器按钮及本地存档差异见下文。

### 官方代码与资源依据

创角流程、配置、prefab、角色资源及其转换凭据见[来源总表](../../assets/reference/SOURCES.md)。功能实现与缺项仅在本页维护。

## 完整还原部分

存档选择的原四级模糊处理在完成与异常退出时恢复进入前的活动 RenderTexture，避免 Windows 后续截屏读取缩小后的模糊纹理；

共享原生模型工厂保留男女骨骼与图集附件的局部坐标、权重、裁剪、排序和染色绑定；创角网格与人物模型共同使用该工厂。完整 Animator、原生动作混合／物理及家园动作仍有缺项，来源见[原生几何清单](../../assets/reference/character-creation/native-geometry-source-manifest.json)。

角色模型、创角网格与动画资源按 `871_156598` 来源清单绑定，输入、转换工具和输出哈希见[来源总表](../../assets/reference/SOURCES.md)。人物完整动作、界面流程仍按本页缺项维护。

地面溶解组件 `Runtime/Official/Opening/GroundDissolve.cs` 按当前官方同名类恢复材质参数更新，`OpeningGroundEffectRuntime` 按 `MyPlayableGroundEffect` 恢复曲线、播放时取实例及结束还原；地面 shader、场景材质与组件的实际绑定及画面验证仍未完成，本段不代表地面溶解表现已还原。

固定版 `871_156598` 的创角数据、`CreatePlayerPanel`、`CreatePlayerPanelDIYContent`、选项条目、颜色滑块及 `ConfirmDialog` 已转换为 Unity 可用资产。客户端行为依据见 [CreatePlayerPanel.cs](../../.tools/source-packages/official-code/client-source/CreatePlayerPanel.cs)、[CreatePlayerDIYContent.cs](../../.tools/source-packages/official-code/client-source/CreatePlayerDIYContent.cs) 和 [AppearanceUtils.cs](../../.tools/source-packages/official-code/client-source/AppearanceSystem.Runtime/AppearanceUtils.cs)。

开场输入由 [prepare_character_creation_opening.py](../../scripts/prepare_character_creation_opening.py) 从固定 APK 与已转换官方数据提取，输出与哈希见 [opening source manifest](../../assets/reference/character-creation/opening-source-manifest.json)：`Assets/Config/Story/CreatePlayerFightRecord.bytes`（bundle `defaultpackage_assets_config_story.bundle`，34,004 字节 GZip，解压后 14 回合、1,168 步、19 个实体、`GameResultMsg=null`）、`BeforeFight`／`AfterFight` 剧情图（`..._story_storyfight.bundle`）、`FirstStory` 剧情图（`..._story_map10story_basicguide.bundle`）、三段共 6 条对白与两名角色（840100 薇兰、840200 绯乐）、`stage` 表 20 行（`Type=33` RecordFight、`GameId=902` → 场景 `game_902`、波次怪物 3803/3282/3281/3283/3481）及网格地面。剧情 UI 由同一脚本从 `prefabs_ui_story`／`prefabs_ui_global`／`prefabs_ui_fight_fightmainpanel_extmodel_fightmainpanelrecordfight` 导出 9 个 prefab 的层级、布局、按钮、文本、图片与材质数据，供 [OpeningUiView](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningUi.cs) 在运行时还原。

创角过场从固定版本 `CreatePlayerPanel` 官方 prefab 动画数据恢复 13 个命名片段、男女两份 UIMesh 溶解网格和角色层级顺序曲线，生成资产及来源哈希见 [来源总表](../../assets/reference/SOURCES.md)。`CharacterCreationPerformanceTools.BuildAndValidate` 将曲线转为 Unity AnimationClip；`CharacterCreationEffectDissolve.shader` 是官方 `UI/EffectDissolve` 的 Windows HLSL 适配，按原 dissolve 参数、边缘颜色、Blend 和 stencil 设置绘制。未单独逐帧验收全部溶解曲线和音效。

六类男女外观选项使用官方配置 ID，缩略图来自 `defaultpackage_assets_image_ui_playerskin.bundle`；155 个 Sprite 和输入输出哈希见 [来源总表](../../assets/reference/SOURCES.md)。肤色、发色与瞳色范围、固定颜色及 HSV 配置从官方角色目录读取。

原版创角外观组合数据与男女 12 个组图已按固定版 `append_appearance_group`、`append_appearance` 表及 APK 图标 bundle 恢复；组名从固定 APK 的 `wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile` 以官方 `LocalizationBinaryFileReader`／`XXHash64` 规则解析。图标、组名、条件 ID、组件 appearance/skin ID 及输入输出哈希见 [来源总表](../../assets/reference/SOURCES.md)。`CharacterCreationFlowController.RenderAppearanceGroups` 显示无条件组并保存草稿中的 `appearance_group_id`；选择组不会改写普通捏脸字段。切换主外观页签时 `SelectTab` 刷新原生预览，使组内 `SkinType=3` 皮肤在“头发／特征”页按官方互斥规则隐藏，并在“整体／脸部”页恢复。固定版 `CreatePlayerPanel.CacheAppendAppearanceDict` 把组的 `AppearanceIdList` 转成追加外观，和 `SkinIdMap` 捏脸字段分开；本离线实现遵循用户确认的“仅用于试穿”：组只影响创建页预览，不写入最终 `WornItems`，普通部件仍写入 `WornSkins`。`AppearanceGroupId` 是所选组，`IsUseAppearanceFunction` 在首次选中非默认组时置为真；服务端对这两个请求字段的额外处理不在客户端代码中，离线保存不推断其行为。`TryOnConditionId=30110` 经官方条件代码解码为 `DomainTypeCondition(LuoTianYi)`，使组 5／6／15／16 依赖服务器 `DomainType`；离线档案没有该字段，因此当前按官方条件隐藏。

创角展示使用 [CharacterCreationMeshFramePlayer](../../unity/Assets/Game/Runtime/Presentation/Character/CharacterCreationMeshFramePlayer.cs)，以 Unity MeshRenderer 播放已校验的官方 24 fps 网格帧；当前转换动作是 `home/idle_01` 和 `map/run_01`，不依赖 Spine runtime。打包脚本 [prepare_character_creation_meshes.py](../../scripts/prepare_character_creation_meshes.py) 复用固定来源的骨骼转换缓存、贴图及染色遮罩，并将两种姿势实际包含的全部官方男女皮肤按单皮肤条目纳入 `StreamingAssets/CharacterCreation/creation-meshes.zip`；运行时仅按选中外观读取并逐条校验哈希，清单见 [来源总表](../../assets/reference/SOURCES.md)。家园的 `HomePlayerActorView` 已改用原生世界模型工厂，以复用公共战斗挂载及正确的模型原点；状态与验证边界见[主城](city-and-home.md)。官方 `home/wakeUp_01`、`100/training_01` 尚未转换，其他角色 Animator 也未迁移。

创角按钮使用的 `Main_Page`、`Main_Cursor_Normal`、`Main_Tag`、`Main_Cursor_Back` 音效已转为本地 OGG 并由 [来源总表](../../assets/reference/SOURCES.md) 校验。DIY 恢复默认外观使用官方 `ui_reset_appearance_tip_text` 确认键及官方弹窗 prefab；固定版本地语言资源未包含该键的文本，Unity 离线界面使用中文提示。

### 开场战场、录像单位与剧情排版

官方 `game_902` 战斗阶段是 2D 场景：固定版 `defaultpackage_assets_prefabs_ecentity_fight_fightstage_902.bundle`（80,054 字节，md5 `67c160d271bba532c38a428252304295`，APK 内）中的 `Assets/Prefabs/EcEntity/Fight/FightStage/902/game_902.prefab` 含 144 个 SpriteRenderer、4 个 Tilemap（2,921 块有效瓦片）、23 个 ParticleSystem、2 个正交相机（Main size 7.0），**没有任何 Mesh 或 MeshRenderer**；地面、墙体与装饰都是 sprite 与瓦片。格网不在场景内，来自 `stage` 表 20 行：27×24、origin `(-1.5,-2.5)`、69 个阻挡格、玩家入口 `(13,10)`，16 个怪物出生点与录像 `GridTransformComponent.MyPos` 完全一致。

[prepare_opening_battlefield.py](../../scripts/prepare_opening_battlefield.py) 按 prefab 世界变换把 sprite 与瓦片展平为按图集页、排序层和 order 分组的四边形批次（142 批、15,816 顶点、23,724 索引），导出 10 张官方图集/特效贴图、23 个粒子挂载和两个相机，写入 `StreamingAssets/Opening/battlefield/game_902.json`；来源与逐贴图哈希见 [来源总表](../../assets/reference/SOURCES.md)。APK 中 `you` 粒子系统的 `ParticleSystemRenderer` 材质数组只有空 PPtr（path_id 0），该项保留空材质引用，运行时不绑定贴图；其余 22 个粒子材质贴图均有来源，不存在未找到的粒子纹理。来源清单没有未解析对象或警告。[OpeningBattlefield](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningBattlefield.cs) 在运行时装配批次、粒子与相机，单位根同时携带场景根偏移 `(5000,0,0)` 和格网 origin，录像单位与 NPC 直接使用官方格网坐标。原场景 shader 只有 Android GLES3 程序且工程未引入 URP；场景批次仍以 Unity 内建 `Sprites/Default` 绘制，属渲染管线适配差异。粒子 shader 已按上文改为 Windows HLSL；独立的 `Sprites/Ground-Dissolve` 材质与地面运行时绑定仍待完成。

[prepare_battle_spine_pages.py](../../scripts/prepare_battle_spine_pages.py) 从固定 APK 中经 `models-source-manifest.json` 验证的 Texture2D 解出 5 只开场怪物（3803／3281／3282／3283／3481）与两名剧情角色（8401／8402）的 9 张图集页，核对 atlas `size:` 后绑定到既有 Spine 材质；`OfficialBattleModelCatalog` 的 identities／identitiesManifest 已指向真实资源。剧情角色不是玩家外观，而是官方 NPC 骨骼：录像中 1017 的 `NpcClassId=8401` 对应 `body_8401`、1018 的 8402 对应 `body_8402`。这两具骨骼不在 409 条战斗模型身份表内，因此新增 `Resources/Opening/OpeningNpcModelCatalog` 资产直接引用已转换的 SkeletonDataAsset，[OpeningFightPlayback](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningFightPlayback.cs) 优先按该目录生成单位（动作按官方 clip 同名回退），怪物仍走 `OfficialBattleModelCatalog` 的身份表。单位排序显式置于场景排序层之上，避免被 21 层的背景遮住。战前顺序按官方 `GAME_ENTITY_LIST` 先于 `BeforeFight`：进入场景 → 生成实体与 HUD → `BeforeFight` → 录像 → `AfterFight`。

开场技能特效由 [OpeningSkillEffectPlayer](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningSkillEffectPlayer.cs) 按官方事件目录播放粒子、网格、材质、音频与 Animator 事件；配置、依赖和哈希见[开场技能特效清单](../../assets/reference/character-creation/opening-skill-effects-source-manifest.json)。其余缺项见下文。

Unity 2022.3 粒子转换将 `boxThickness`、`placementMode`、UV `animationType` 和拖尾材质映射到对应原生模块字段；录像旋转检查四元数长度，抓钩轴向差值先在 Vector3 中计算再转换为 Vector2。

剧情 UI 图集按 4096 上限分页导出（`atlas.png`／`atlas-1.png`／`atlas-2.png`），47 个 Sprite 引用全部解析。`OpeningUi` 使用原 TMP 字号、对齐、边距、颜色及自动缩放字段，`OpeningUiView.PlayAnimation` 按原层级执行 `TalkDialog_Show`（0.333 秒）、`ExitFightTransitionPanel_Show`（1 秒）、`StoryTransitionPanel_Show`（2 秒）及 `StoryPanel_Show`（13.217 秒、124 条曲线）；材质动画由 `OpeningUiMeshGraphic` 承接。六条官方语音从 APK 提取为 PCM，按每句先停止上一条再播放。

`OpeningFightPlayback` 按录像时序分发技能、伤害、状态和动作实体。飘字使用原 `CharacterDamageUI` prefab、五套位图字体与 AnimationClip 曲线，按原元素色、暴击放大及底线显示；数值沿 zh_CN `FormatBattleByFourWidth` 缩略。同目标连续命中按 `DamageUISpacing (0,-0.1,0)` 偏移，确切分组／回收窗口仍未核对。单位动作由技能自己的 `MyFightSkill` playable 触发，通用 Action 20001 的 `myEffect` 为空，不覆盖为统一 `skill_01`。

### 开场对话与模型绑定

`OptionTalkDialogPanel.DoInit` 调用 `OptionSlot.Reset`，隐藏 prefab 中预置的五个选项条目，避免黄色选项覆盖人物名；`OpeningStoryPlayer` 沿此初始化顺序恢复。姓名区从固定 APK 的 `OptionTalkDialog.prefab` 重新提取 `HorizontalLayoutGroupJSZX`、`ContentSizeFitter` 与 `LayoutElement`，共恢复 22 个原组件，复用已经迁移的官方 JSZX 布局代码，保留原 padding、spacing list、布局标志及自适应尺寸。增量提取入口为 `python -B scripts/prepare_character_creation_opening.py --ui-layout-only`，组件来源和输入／输出哈希写入 `opening-source-manifest.json`，不重绘界面或替换 Sprite。

录像单位保留官方 `SkeletonDataAsset` 导入比例，并按 ClassId 绑定原模型缩放与落脚偏移，详见[开场来源清单](../../assets/reference/character-creation/opening-source-manifest.json)。怪物材质保留原 `_StraightAlphaInput`；粒子或抓钩无法解析目标骨骼时按 `AttachPosRotCfg.GetBone` 回退到目标根节点。录像、地面溶解及转场资源仍有未恢复的绑定和表现，见下文缺项。

### 官方字体与原生 UI

已核对固定 APK 的 `defaultpackage_assets_textmesh_pro_rawfonts_fzy4k.bundle` 中 `FZY4K` TextAsset：原始方正粗圆 TTF 与 `Assets/Official/Commerce/Art/fonts/FZY4K.ttf` 逐字节一致，SHA256 为 `90023872dffe6a58503de655bd0631bf3334ce3b6b72cc75a6625b685c94c386`，创角使用 `Resources/Combat/FZY4K-Dynamic.asset`，没有使用替代字体。

旧恢复 prefab 缺少原 MonoBehaviour 字段，不能显示完整 UGUI/TMP 内容；[prepare_character_creation_runtime_ui.py](../../scripts/prepare_character_creation_runtime_ui.py) 与 [CharacterCreationUiTools](../../unity/Assets/Game/Editor/CharacterCreation/CharacterCreationUiTools.cs) 从已核验原始字段恢复三个原生 prefab、47 张原 Sprite、RectTransform、TMP 样式、遮罩、布局、滚动、CanvasGroup 及选择状态。页标题沿原 `StepTitles` 与语言包显示“选择体型／装扮形象／取个名吧”，HSV 滑块沿 `EditCustomColorBlock.GenerateTexture` 生成渐变。输入输出及生成工具哈希见 [来源总表](../../assets/reference/SOURCES.md)。

`OfflineMainlineProgressSession.UniqueAppearanceTypes` 接受官方 `PlayerSkinType.Eye = 0`；`LocalJsonSaveFile` 在 Windows 原子替换遇到短暂文件占用时有限重试。创角启动不额外创建存档选择遮罩。

完整流程需由新建槽位连续进入开场、逐句对白、角色创建、外观编辑、正式存档和初始武器选择；局部入口接线不代表整条流程完成。

创角的完整鼠标、键盘和存档恢复流程仍未贯通，当前缺项见下文。

Windows Player 的存档入口保留列表按钮原有 86 单位高度，关闭纵向布局组对按钮高度的自动控制，避免无布局首选高度时条目被压为零。续玩点击、完整创角／初始武器／家园和地图全流程仍未验收。

开场战斗的两名剧情 NPC 现在按固定 APK `871_156598` 的 FightNpc 4201／4202、外观组合 8401／8402 恢复身体与 `pWeapon_100_G_001`／`pWeapon_200_G_001` 武器皮肤；重建原控制器别名及录像技能 `MyPlayableOverrideAnimator` 引用的骨骼动画，使用已确认的 Spine 原生迁移方案。`prepare_opening_fighters.py` 提取原贴图、骨骼、蒙皮与动作；`OpeningFighterAssetTools` 显式绑定已批准的 Spine shader，避免同名 Android 占位 shader 被动态查找选中。角色名字、双方血条的尺寸、位置、分阵营颜色与九宫边框沿官方 prefab 恢复，血量消费录像内真实伤害与治疗。

`prepare_opening_unit_ui.py` 恢复 `GamePlayManagerConfig.FactionEffectDatas`、NPC／怪物头像和 `GameRoundUI` 参数。行动条在战前显示全部 18 个头像，按录像 NextTime、官方最小间距及 0.2 秒移动参数排列，当前行动者放大，并播放原受击和死亡动画，死亡头像保持 0.5 秒后退出。`prepare_opening_damage_ui.py` 恢复五套原位图字体、字距／UV、元素颜色、普通／暴击 prefab、原曲线、暴击底线与 2×2 序列光圈；`ElementConfrontationUI.Reset` 对内容的隐藏不会被父节点动画重新启用。

Boss 3803 使用官方节点、网格、描边、粒子、模板遮罩与星空纹理；Windows shader 按原 Spine stencil、描边和伤害混合参数绑定。开场资源使用独立版本来源，输入及 Unity 绑定见开场来源清单。

[prepare_opening_action_motion.py](../../scripts/prepare_opening_action_motion.py) 从固定 APK 的 `config_ec`、11530／11830／15300 技能 prefab 提取 11532／11833／15301 移动组件、`MyPlayableTrack` 与 `MyCurveTrack` 的原时长、速度、加权曲线和转向参数，输出 `Opening/action-motion.json`。`OpeningBattleActionPlayer` 消费录像的 11 个移动实体／12 段路径，涵盖 NPC 冲刺、跑动、回位及怪物拖拽；逻辑终点先更新，画面沿原曲线移动，动作结束按 `MyGridTransform` 重置。跑动使用 15300 的原 `run` 触发器；技能按原事件延迟、速度和完整骨骼名播放。薇兰分别使用 `100/skill_20、05、41、06、27`，绯乐使用 `200/skill_13、12、16、11`；以录像 1.5 倍速度更新骨骼与移动时钟。

受击材质沿 `MeshMaterialProperty` 只处理角色外观的独立图集材质，通过 `SkeletonAnimation.CustomMaterialOverride` 绑定与还原；不替换粒子、网格复制及不含受击属性的模板遮罩。`OpeningSpineHit` 按原 `SkeletonEffect-2D` 的 `_HITEFFECT` GLES 片段恢复透明裁剪、乘透明度的颜色混合及预乘输出，保留材质的模板、ColorMask 和输入透明模式，修复精英怪／Boss 受击时透明图集区域变为紫色块。 Windows 显式加载本地 `OpeningSpineHit` shader，先克隆原材质再切换 shader，只保留适用的透明模式关键字；Boss 描边按 `AppearanceRendererLite`／`CustomRenderExistingMesh.AddRuntimeReplacementMaterial` 同步登记受击克隆材质，恢复时回收映射，避免描边变成缺材质的粉色网格。



### 已迁移代码与局部资源

以下为局部代码／资源接入，完整流程仍受本页缺项限制。



## 自定义实现

存档槽、创角草稿和正式角色资料由 [CharacterCreationFlowController](../../unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs)、[CharacterCreationOfflineService](../../unity/Assets/Game/Runtime/Offline/CharacterCreation/CharacterCreationOfflineService.cs)、[CharacterCreationDraftStore](../../unity/Assets/Game/Runtime/Offline/CharacterCreation/CharacterCreationDraft.cs) 与 [ProfileSlotCatalog](../../unity/Assets/Game/Runtime/Offline/State/ProfileSlotCatalog.cs) 接入本地 JSON 保存。各槽独立保存草稿；提交角色时写入选中槽，草稿损坏可保留原文件副本后重新开始，不覆盖正式角色数据。写入失败会阻止页面切换并显示提示。

离线流程不提供随机名字。官方 `CreatePlayerSetNameSubPanel` 的随机名调用服务端 `OperPlayerGetRandomName(Sex)`，没有本地生成规则；按用户确认隐藏随机名按钮，玩家必须手动输入名字。

初始武器选项依据官方 `ChooseInitialWeaponNode.ProcessSucc` 和 `WeaponType` 数值 `200`（剑）、`201`（法杖）写入本地角色。由于官方节点由服务器故事流程触发，离线实现用本地按钮承接该选择；选择完成后进入人物页入口。

官方染色遮罩 `ethnicity_06`、`ethnicity_07`、`ethnicity_22` 至 `ethnicity_27` 共 8 张经用户核对固定 APK、相关原版本 CDN 包及新版同名包仍缺失。按用户确认保留全部选项；缺少遮罩时沿官方空遮罩行为不染色，预览不生成或替代官方遮罩。

开场跳过条件沿用官方 `MainUIRoot.mSkipNewbieCutsceneConditionId = 21003`：`CompositeOrCondition[21001 PlayerChannel(130077), 21002 PlayerMedia(C520612)]`。离线 SDK 桩的渠道／媒体均为空，因此判定为不跳过；`CharacterOpeningFlow.ShouldSkipNewbieCutscene` 保留该白名单比较，未另造“看过一次就跳过”的规则。每槽观看状态按用户确认只做进程内标记（`PlayedSlots`），重启游戏后未创角槽位会重新播放，不写入槽位存档。

开场播放由 [OpeningStoryPlayer](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningStoryPlayer.cs) 按官方节点语义执行：`StartStoryNode` 不阻断；`WaitForSecondsNode` 在 `NeedMask` 时先挂全屏透明遮罩再等待，遮罩由后续对话或开面板节点的 `Reset` 关闭；`DialogNode` 打开官方 `OptionTalkDialog` 层级，按 `dialog.csv` 的 `StoryName+DialogId`、`Order` 顺序显示对白与角色名，Windows 单机按用户要求逐句点击推进；保留源 `AutoPlay` 数据，但不启用空闲自动播放（官方实现先等待 120 秒，再每 2 秒推进）；`ShowBlackMaskNode`／`CloseBlackMaskNode` 为硬切纯黑遮罩；`HideAllUINode`／`ShowAllUINode` 在节点结束时生效；`OpenShowPanelNode`／`CloseWindowNode` 打开和关闭 `Story/ExitFightTransitionPanel`；`LeaveStoryFightNode` 关闭战斗场景；`PlayBlackScreenStoryNode` 打开 `Global/FirstStoryPanel` 并在 `mIsKeepBlackScreen` 时保持黑幕；`PlayEnterGameTransitionNode` 打开开场转场面板；`PlayPlayableNode` 的两个 GUID（`e52fba61…`、`32bc3f4c…`）不在固定版五包 AssetTable 内，按官方空句柄行为跳过并记录。`HomePlayerPlayActionNode` 已连接 `StoryStun`：官方 `player.json` 的 `StoryWakeUp` 图将角色移到 `player_wake_point`、应用 `player_stun`，再以非循环方式播放 3 秒 `home/wakeUp_01`。离线实现用官方 Home 场景、床铺状态、玩家变换与已保存外观创建角色；固定来源中未找到锚点和该动画的可播放网格帧，所以以 3 秒起身摆动近似，不能视为原动画还原。`SafeEnd` 后其余仅展示类节点仍按 `ClientRunNode.InternalSkip` 语义跳过，仅保留关闭、离开和 UI 环境节点。

录像回放由 [OpeningFightPlayback](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningFightPlayback.cs) 驱动：先 GZip 解压并按 `GameServer`→`Console` 之外的原始 JSON 解析 `FightProcessRecordData`（[OpeningRecord](../../unity/Assets/Game/Runtime/Presentation/Opening/OpeningData.cs)），生成实体与回合步骤；回放按官方 `NormalPlaybackStrategy` 的时钟模型逐回合、逐步骤等待 `step.Time`（相对回合起点），并按官方 `CreatePlayerRecordFightSpeed = 1.5` 折算实际等待；`GAME_ENTITY_ADD`／`GAME_ENTITY_REMOVE` 增删单位，`Fight*Round*Modification` 更新行动头像，单位动作消费录像 ActionComponent 与官方 playable，`GAME_FASTFORWARD` 走跳跃到结尾（应用录像内快照），`GAME_PLAY_STATUS_CHANGE` 与 `GAME_MODIFACTION_RUN_BATTLE_END`／`_RUN_END` 记录状态。快进按钮按官方 `IsShowRecordFightSkipButton || Gm` 决定是否请求，延迟 `mShowSkipBtnTime = 500` 毫秒显示，点击后只推进到结尾而非加速；实装 `IsShowRecordFightSkipButton = false`，正常流程不显示按钮。

事件顺序沿用官方控制：`BeforeFight` 后开始录像，录像结束触发 `AfterFight` 和战斗到主场景切换；切换最多等待 5 秒后打开创角面板，同一槽位只打开一次。录像缺失或解析失败时直接打开创角。创角成功后关闭创角面板，播放 `FirstStory`，再进入本地初始武器选择；选项承接原 `ChooseInitialWeaponNode` 的 200／201 数值。对白、材质、粒子和动画的完整运行表现尚未恢复。

### Spine 迁移决策

创角与开场使用已确认的免费原生迁移方案，保留官方贴图、骨骼、网格、权重、附件和动画数据；通用方案及兼容边界见[素材管理](../asset-management.md#spine-角色动画迁移决策)。本页仅记录创角与开场接入和缺项。

## 尚未实现

以下缺项按影响范围分列；共用战斗规则与场景装配仍由[战斗执行](battle-execution.md)维护，不在此重复。

### 开场战斗表现资源

1. **战场地面溶解材质。** 开场粒子事件目录和 295 种粒子材质已接入 Windows shader 适配，独立的 `Sprites/Ground-Dissolve` shader 仍只有固定包中的 Android 编译程序；`OpeningBattlefield` 也尚未把地面材质、`GroundDissolve` 曲线与 `OpeningGroundEffectRuntime` 绑定。该项待用户确认 Windows 2D HLSL 适配后实现并验证。
开场角色以外的大量战斗骨骼图集页仍未转换；所需图集未绑定时相应模型无法渲染，具体资源范围见来源清单。
3. **玩家外观单位。** 录像剧情角色按官方 NPC 骨骼渲染；自建角色进入战斗时的官方复合外观、换装与战斗动作仍未接入，模型目录目前只覆盖怪物与 NPC。
4. **其余状态语义。** 开场两名 NPC 的武器、控制器动作和技能替换动画、单位血条、行动头像与原生伤害飘字已恢复。`Blinding`／`SuperArmor` 等其余动作、完整状态图标与 Boss 阶段控制仍须继续核对，不将开场表现修复扩大为通用战斗全部表现完成。

### 剧情表现资源

`FirstStory` 的对白到初始武器选择及 `StoryStun` 家园节点已接通。家园节点使用官方场景和角色网格，但 `player_wake_point` 与 `home/wakeUp_01` 的固定版动画数据未进入现有原生预览包，当前以 3 秒起身动作近似。AfterFight 的两个 Playable GUID 在固定包内缺失，且位于纯黑遮罩期间、官方节点不等待资源时长；离线按官方空句柄行为跳过，不改变可见画面及剧情时序。

### 当前输入与异常分支

- 超时、保存失败和中断恢复分支尚未完整接通。
- 完整输入流程仍需从新建槽位、对白、体型／外观选择、命名和保存连续到初始武器，并覆盖键盘与拖拽操作。
