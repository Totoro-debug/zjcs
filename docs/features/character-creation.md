# 角色创建与正式换装

[总索引](../README.md)

状态：未完成

## 功能简介

每个新建存档首次进入选择男性／女性、装扮、名字和战士／法师；正式角色从外观入口打开换装。

Unity 已有按存档槽位保存的创角草稿存储层；角色创建界面、正式角色数据和换装流程尚未接入。

### 原始官方开场与创角流程

本功能范围包含创角前的初始剧情战斗。以下依据固定版 `854_156383` 客户端反编译代码记录官方调用顺序，不代表这些环节已经迁移到 Unity 离线流程。创角面板自身的溶解、跑动和页面转场动画与这段剧情战斗分别验收。

1. **判定入口。** [MainUIRoot.Start / ShouldSkipNewbieCutscene](../../.tools/source-packages/official-code/client-source/MainUIRoot.cs) 在主玩家存在且名字为空或 `IsPreCreateInit` 为真时，订阅剧情结束事件并延迟隐藏加载页。`mSkipNewbieCutsceneConditionId` 对应的条件满足，或玩家 ID 等于静态字段 `sLastPlayStoryPlayerId` 时直接打开创角；否则调用 `StartStoryFight`。条件 ID 不大于 0，或配置条件不存在时不跳过，后者记录错误。该静态字段是当前进程内标记，不能直接解释成已持久化的每存档观看记录；prefab 中实际条件 ID 和条件内容仍须提取核对。
2. **读取官方开场录像并进入战斗。** `MainUIRoot.StartStoryFight` 订阅场景切换，安装 [CreatePlayerStoryFightController](../../.tools/source-packages/official-code/client-source/CreatePlayerStoryFightController.cs)，读取 `ConfigConst.CONFIG_PATH + "Story/CreatePlayerFightRecord.bytes"`，经 GZip 解压，再由 [GameRecordManager.ParserRecordSaveDataFromBytes / TryPlaybackLocalRecord](../../.tools/source-packages/official-code/client-source/GameRecordManager.cs) 解析为 `FightProcessRecordData` 并启动本地录像回放。这是官方预录战斗流程，不能用临时编排一场普通战斗替代。若 `TryPlaybackLocalRecord` 返回 `false`，原入口清除剧情战斗控制器、打开创角并取消场景订阅；GZip 解压异常只见记录错误，不能据此推定所有加载／解析异常均会安全回退。
3. **播放战前剧情，再开始回放。** [FightMainPanelRecordFight.Start](../../.tools/source-packages/official-code/client-source/FightMainPanelRecordFight.cs) 将玩家置为 `JustWatching`，按录像中的 NPC 设置观战对象，并调用剧情战斗控制器。`CreatePlayerStoryFightController.OnEnableFight` 对未创角玩家播放 `BeforeFight`，等待结束后才调用 `StartPlayFight()`；若剧情入口未成功启动，`PlayStory` 仍调用后续回调。剧情图内部的对白、镜头、单位动作及具体节点顺序须继续从官方资源恢复。
4. **回放与快进。** 控制器仅在 `GameSettings.IsShowRecordFightSkipButton` 为真或玩家为 GM 时请求显示跳过按钮；`FightMainPanelRecordFight` 按 `mShowSkipBtnTime` 延迟显示，按钮调用 `DoFastforward()`。须核对 prefab 实际参数，不能默认按钮始终可用，也不能把快进实现为直接跳入创角。战斗模型、场景、技能、音效和剧情事件应消费同一份官方录像时序。
5. **播放战后剧情并返回主场景。** 战斗结束触发 `CreatePlayerStoryFightController.OnGameEnd`：未创角玩家播放 `AfterFight`；已正式创角玩家走 `ResetGame()` 分支。`MainUIRoot.OnStoryFinish("AfterFight", ...)` 记录 `sLastPlayStoryPlayerId`，再由 `OnPostStoryFinish` 等待 `Fight → Home` 场景事件设置 `mWaitSceneFightToMain`，最多 5 秒；超时记录错误后仍尝试打开创角。战后剧情图中离场与转场节点的实际绑定仍须恢复。
6. **打开创角。** `OpenCreatePlayerPanel` 在未命中配置跳过条件时播放黑幕，打开全局 `CreatePlayerPanel` 后关闭黑幕、释放剩余加载页延迟任务并发出 `OnPrepareCreatePlayerReady`。进入战斗场景时，`OnSceneUpdate` 也会释放加载页延迟任务。销毁入口或战斗面板时须沿原调用关系解除事件订阅、清除控制器并取消剧情令牌，避免重复触发。
7. **创建外观、命名与创角后剧情。** 原 `CreatePlayerPanel` 承接体型、外观与命名；[CreatePlayerSetNameSubPanel.ConfirmCreate / RunFirstStory](../../.tools/source-packages/official-code/client-source/CreatePlayerSetNameSubPanel.cs) 正常新角色分支先提交 `OperPlayerCreateAppearance`，成功后提交 `OperPlayerCreateCharacter`；预创建分支使用 `OperPlayerPreCreateCreateCharacter`。成功后清除预创建状态，关闭创角面板，在 `FirstStory` 存在且 `CheckCanRun` 允许时启动该剧情，使用黑幕等待剧情窗口打开，并发布玩家信息；`MainUIRoot` 收到 `FirstStory` 结束时恢复 Home 音乐。[ChooseInitialWeaponNode.ProcessSucc](../../.tools/source-packages/official-code/client-source/ChooseInitialWeaponNode.cs) 在服务器故事节点成功后写入初始武器，但它与 `FirstStory` 的具体连线及触发位置尚未核对剧情图，不将其推测为固定节点顺序。离线手动命名、初始武器按钮及本地存档差异见下文。

### 当前离线流程与缺口

[CharacterCreationFlowController.CreateSlot / OpenSlot](../../unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs) 当前在新建槽位或打开未创角槽位时，读取草稿后直接调用 `ShowCreation()`；未接入上述开场入口、`BeforeFight`、官方战斗录像、`AfterFight`、返回主场景等待及对应黑幕／加载页生命周期。提交角色后直接进入本地初始武器选择及完成页，未接入官方 `FirstStory`。现有 [CharacterCreationPlayModeAcceptance.RunScenario](../../unity/Assets/Game/Editor/CharacterCreation/CharacterCreationPlayModeAcceptance.cs) 从“新建存档 → 体型页”开始，不能作为初始剧情战斗已完成的证据。

## 完整还原部分

Unity [CharacterCreationDraftStore](../../unity/Assets/Game/Runtime/Offline/CharacterCreation/CharacterCreationDraft.cs) 按选中槽位读写 `character-draft.json`，使用版本 4 格式，校验页面、男女两套完整外观字段、颜色格式和字段分组；读取支持现有 JSON 保存层的备份恢复，写入经格式校验并原子替换。该层不生成默认外观、不迁移旧版本草稿，也不覆盖槽位正式进度。

草稿读写还会加载 [CharacterCreationAssetCatalog](../../unity/Assets/Game/Runtime/Offline/CharacterCreation/CharacterCreationAssetCatalog.cs) 中由官方 `appearance-options.json` 解析出的男女发型、眉毛、眼睛、嘴型、妆容和特征 ID 集合；未知或性别不匹配的部件 ID 会使草稿校验失败。官方选项目录结构、非空列表、重复 ID 和身体归属在目录加载时校验。[CharacterCreationDraftJson](../../unity/Assets/Game/Runtime/Offline/CharacterCreation/CharacterCreationDraft.cs) 对草稿拒绝未知及重复字段并校验 JSON 语法。

Unity 载荷现包含原 CreatePlayerPanel 层级／演出目录、男女外观选项目录、正式换装面板字段、衣柜规则及衣柜 UI 数据；Editor 入口 [CharacterCreationAssetTools](../../unity/Assets/Game/Editor/CharacterCreation/CharacterCreationAssetTools.cs) 绑定并核对载荷哈希。以上是数据恢复，不等于原角色 prefab、Spine 骨骼／动画、UI 图片及可运行界面已经接入。

## 自定义实现

以下为已确认的单机处理要求与动画技术适配决策，尚未代表完整 Unity 流程已经接入或验收。

男女角色预览、创角演出与正式换装采用用户已确认的免费方案：将官方 Spine 4.2 角色数据独立转换为 Unity 原生动画，并补齐必要的播放、换装及染色适配。共用实现边界、旧 Spine 工具依赖处理和验证要求统一见[Spine 角色动画迁移决策](../asset-management.md#spine-角色动画迁移决策)。兼容性、实际表现及运行链尚待验证；本次未确认静态预览降级或重绘角色。

离线创角不提供随机生成名字的功能，玩家必须自行输入名字。原版随机名按钮调用服务端 `OperPlayerGetRandomName(Sex)`，没有迁移成本地名字库或生成规则；Unity 草稿不保存随机名状态。

创角草稿与正式进度在本地分别保存，包含男女独立外观；损坏草稿不覆盖正式档，保存失败整体回滚。旧格式兼容迁移不开放，版本不符或缺少必需字段时不得补造外观。无效草稿的恢复界面及失败状态提示仍未接入。

各存档分别保留自己的创角草稿；体型页返回存档选择后可恢复编辑，正式创角只写入所选档。多存档入口及切换规则见[设置、拍照与本地存档](settings-and-save.md#多存档与角色切换)。

官方开场录像流程及待实现范围见上文；旧原型初始战斗的取消不能作为取消官方开场剧情的依据。正式改名通过行囊中的原改名物品流程完成，按实际物品扣除并与本地存档事务一起提交；见[行囊、物品与宝箱](inventory.md)。

## 尚未实现

### 初始剧情战斗及创角衔接

以下环节均待实现或验证，须完成整条流程后才可将初始剧情战斗标为完成。官方流程和分支以上文为准，共用战斗底层的当前能力与缺项见[战斗执行](battle-execution.md)，不在本页重复维护共用规则。

1. **取得并核实完整官方输入。** 从固定 APK、原版本 CDN 及适用的新版同名包按项目来源顺序定位 `Story/CreatePlayerFightRecord.bytes` 的实际所属 bundle、内部 TextAsset 和 CAB 依赖，核验输入大小与哈希；实际执行 GZip 解压和录像解析，确认录像内阶段、场景、单位、外观、技能及事件引用。继续定位 `BeforeFight`、`AfterFight`、`FirstStory` 剧情图、子图、对白和动态资源，以及 `MainUIRoot` 的跳过条件、`FightMainPanelRecordFight` 的快进按钮参数。当前仅确认代码中的资源路径与调用关系，尚未完成这些资源的定位／提取／解析；检索未命中不代表官方内容缺失。
2. **恢复 Windows 可运行资产。** 按[Android → Windows 流程](../asset-management.md#android-资源迁移到-windows)对实际 bundle 使用 `prepare_unity_bundle_inputs.py --bundle <原清单完整包名>` 检查版本、平台和递归 CAB 依赖，恢复场景、地面、镜头、官方 UI、角色／怪物模型、材质、shader、技能特效和音频。角色沿已确认的 Spine 4.2 → Unity 原生动画方案迁移。同步来源清单、输入输出哈希和显式动态加载绑定；准备输入或导入资源不等于回放已运行。
3. **接通本地录像与剧情调度。** 依据官方录像解析器、回放调用方和剧情节点恢复本地播放，接通 `BeforeFight → StartPlayFight → 录像事件 → AfterFight`；保留原单位、数值、时序、镜头、对白、动作、受击／死亡、HUD 及音效顺序，接入共用战斗表现。按官方配置实现观战、延迟显示快进和 `DoFastforward` 的事件推进；不得用重新随机模拟、静态画面或自编战斗替代录像。
4. **接通启动、离场和创角界面。** 将开场入口接到新建存档流程，恢复配置跳过、战后 `Fight → Home` 等待与 5 秒超时行为、黑幕、加载页任务释放和创角就绪通知，处理回放返回 `false` 的官方回退分支；其他输入缺失、解压或解析异常需有明确错误反馈与可恢复路径，不能未经确认静默跳过。补齐取消、销毁、重复进入及事件解绑，使战后仅打开一次创角界面，并保留现有草稿和正式存档保护。
5. **核对离线状态与创角后衔接。** 查清原跳过条件的实际配置和剧情节点所依赖的服务器状态／请求／响应消费，区分可直接迁移的客户端行为与缺少的服务器决策。每槽观看状态是否持久化、退出或崩溃后重播／续播的粒度、旧槽位和已有草稿如何进入，以及服务器条件如何本地判定，尚未确认，不把进程内 `sLastPlayStoryPlayerId` 擅改为永久跳过；取得证据仍不能确定时按项目约定提出具体规则问题。恢复并核对 `FirstStory` 的进入条件、图节点、初始武器节点连线及主场景音乐衔接，保留已确认的本地手动命名与武器选择差异；不据服务器节点名称补造奖励或状态变化。
6. **完成实际流程验收。** 扩展 `CharacterCreationPlayModeAcceptance` 或所属功能集成入口，用独立存档在 Unity 中验证首次进入完整播放、官方条件跳过、快进显示与执行、战后返回和黑幕、创角页面及命名保存、`FirstStory` 与已确认武器选择的衔接。覆盖再次进入、不同槽位隔离、播放中退出、回放失败、解压／解析失败、超时和保存失败；仅对有依据或用户确认的恢复规则作断言。实际检查画面、模型动作、镜头、对白、特效、音效与输入，记录真实验证范围；本项尚未执行，不以现有创角资源校验或静态检查替代。

### 创角部分的其他未完成项

- 按[Spine 角色动画迁移决策](../asset-management.md#spine-角色动画迁移决策)恢复男女骨骼、网格、附件、原动作曲线及事件，接通 Unity 原生播放、动态换装和染色；验证所需约束、遮罩、层级及动画切换的兼容性。角色资源已经恢复或预处理不等于已接通 Unity 预览。
- 将官方创角／换装 UI 图像及音效转换为 Windows 可用载荷，绑定原 prefab 层级、布局、动画事件和按钮音效；当前已恢复的 JSON 目录还没有完整 Unity 可运行界面。
- 迁移男女外观选项、部件染色、角色预览、原动画事件、手动命名、职业选择、正式易容费用、性别冲突确认和衣柜互斥规则，接通 CreatePlayerPanel、ChangeAppearancePanel 与实际角色入口；不实现服务端随机名功能。
- 将正式外观、职业和改名物品交易并入现有槽位进度事务，接入各槽位草稿恢复、失败反馈与回滚。
- 尚未在 Unity 中验证创角规则、正常操作、动画／音效、保存恢复和失败回滚；

### 依据与代码定位

以下为官方来源、既有规则与回归参考：

、[InitialWeaponCondition.cs](../../.tools/source-packages/official-code/common-source/Common/InitialWeaponCondition.cs)、[PlayerProfessionDataEx.cs](../../.tools/source-packages/official-code/common-source/Common/PlayerProfessionDataEx.cs)、[ChooseInitialWeaponNode.cs](../../.tools/source-packages/official-code/client-source/ChooseInitialWeaponNode.cs)、[CreatePlayerPanel](../../.tools/source-packages/official-code/client-source/CreatePlayerPanel.cs)

[CreatePlayerSelectSexContent](../../.tools/source-packages/official-code/client-source/CreatePlayerSelectSexContent.cs)、[CreaterPlayerRunAnimation](../../.tools/source-packages/official-code/client-source/CreaterPlayerRunAnimation.cs)、[CreatPlayerRoleContent](../../.tools/source-packages/official-code/client-source/CreatPlayerRoleContent.cs)、[AppendAppearanceGridItem](../../.tools/source-packages/official-code/client-source/AppendAppearanceGridItem.cs)、[AppearanceUtils](../../.tools/source-packages/official-code/client-source/AppearanceSystem.Runtime/AppearanceUtils.cs)

[wardrobe source manifest](../../assets/reference/wardrobe/source-manifest.json)、[free-parts source manifest](../../assets/reference/character-genders/free-parts-source-manifest.json)

[creation performance source manifest](../../assets/reference/creation-performance/source-manifest.json)

[ItemUseEffectChangeName](../../.tools/source-packages/official-code/client-source/ItemUseEffectChangeName.cs)、[PlayerOperUtils](../../.tools/source-packages/official-code/client-source/PlayerOperUtils.cs)、[character-genders](../../assets/reference/character-genders)

[prepare_map_player_actions.py](../../scripts/prepare_map_player_actions.py)、[official_appearance_catalog.py](../../scripts/official_appearance_catalog.py)
