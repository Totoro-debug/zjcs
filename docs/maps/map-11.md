# 11 森之国

[总索引](../README.md)

状态：未完成

## 地图简介

地图 ID：11。原目录：坎斯汀大陆/森之国。场景类型：`map_world`；原区域配置77项，BGM配置11。

当前验收对象为 Unity / C#。

## 完整还原部分

尚无本图在 Unity 完整运行并验收的结论。

已转换并放入 `unity/Assets/Official/World/Map/11/` 的官方载荷：地图定义含 77 区、81 面积配置、1,803 个放置实体及其一一对应的实体组件、导航和 8 条区域开放条件；绑定 110 条实体进度参数、247 条实体显示、128 条实体 UI、5 条阶段动作，以及地图、区域、逻辑实体配置。Map 11 有 17 个普通女神像（`EntityClassId=6010`），其所在区域各配置一组 `DropItems`；另有 49 个失落女神像（`EntityClassId=6011`），其所在区域各配置一组 `GoddessActivePropAwardDict`。1045～1160 范围含 24 条主线记录。地形几何为 14,427 批次，引用的 475 张官方纹理已复制至 `Resources/Map11/Textures/`；小地图为 85 批次并绑定 2 张官方纹理。输入版本固定为 `854_156383`，来源和输出哈希见该目录的 `*-source-manifest.json`。转换器为 `scripts/prepare_unity_map11_rendering.py`、`scripts/prepare_unity_map10_minimap.py --map-id 11` 和 `scripts/prepare_unity_map11_definition.py`。地形视图已支持原始 Map11 几何头格式，保留 Map10 默认格式。

实体载荷另含 1,803 个放置记录、105 个模型绑定、97 份官方几何、27 个 Spine 实例和 201 张实体贴图。`8210–8213` 世界奖励实体按官方 `Map/entity_map_display` 绑定到公共单物品模板；展示表有常规地面与浅水两个版本，当前转换载荷保留常规地面版本。剩余 9 个无模型实例属于 `5110` 和 `6533`：`6533` 的官方逻辑视图记录明确标记为无显示实体；`5110` 目前未找到实体显示表绑定或模型，需要继续核对其正式交互表现。战斗载荷已转换为 124 个官方阶段、54 个怪物编队数组和对应战斗场景，保留玩家等级／位阶动态阶段及官方等级属性缺行语义；转换器为 `scripts/prepare_unity_map10_battle_data.py --map-id 11`，输出哈希见 `battle-source-manifest.json`。`OfficialMap11Tools` 已扩展为生成地形、小地图和实体 prefab、绑定 ClassId 11 目录并验证定义、几何及寻路，但尚未在 Unity 执行；主资源目录尚未实际写入 ClassId 11。尚无本图导入、编译或 Play Mode 通过的证据；编辑器阻塞须以当前 Console 和编译结果重新核实，不能将旧的 Safe Mode 状态视为持续存在的阻塞。静态来源哈希、数据转换和寻路检查不代表本图完整玩法已接入。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

原线上组队按已确认原剧情 NPC 资格、等级、位阶和技能适配，不生成真人玩家。公共刷新使用本地时钟；普通路线保留真实败北、产出和等待，后期资格回访不能记作普通角色自然成长。

用户确认 49 座失落女神像（`EntityClassId=6011`）均须在对应地区探索度达到 100% 后激活；首次激活自动增加床的家园经验收益 1% 和小推车的罗拉（金币）收益 1%。两项分别使用 `HangUpExpAddPercent`（1001）和 `HangUpMoneyAddPercent`（1005），每座各增加定点值 `100`（1%）。`HangUpCartAllPercent`（1034）增加小推车全部产出，不能用于代替罗拉专用加成。49 座像分别对应 49 个不同地区，激活按地图／地区持久化，并以事件回执防止重复结算。

此奖励是已确认的单机差异：固定版本官方 `map_region.GoddessActivePropAwardDict` 为全部 49 座配置经验 +1%，另一项按地区分为不同资源，仅 8 座配置罗拉 +1%。现有 `OfflineCharacterConfiguredPropSource.CalculateLostGoddess` 仍直接读取该原表，尚未实现上述统一罗拉奖励。后续须在单机适配中显式实现该差异，保留原始配置与来源哈希；原表结算与单机奖励不得同时叠加。普通女神像（`EntityClassId=6010`）仍沿用其各自区域配置，首次激活时自动结算并持久化回执。

## 尚未实现

恢复 77 区、九组踏板、六块战斗碑、森林老人／世界树／狮鹫剧情与 1045～1160 主线；验证个人资格、公共化、全区激活与 6001 出口条件，不能仅按任务 1160 完成推断可以离图。

按原地图配置恢复并接入场景、区域、导航、放置实体、原条件、交互、战斗、奖励和出口。Map11 的区域开放条件已从固定 APK 中提取并写入定义，但尚未与本地任务条件状态联动。原版本中地图 NPC、建筑、世界奖励、交互物、怪物及小地图 prefab bundle 已从固定 APK 提取并校验，保存在 `.tools/source-packages/history/854_156383/extracted/`；现有几何与实体载荷尚未覆盖全部官方组件和运行时绑定。map10 的专用运行时包含地图专属剧情和战斗假设，不能直接代替 map11。`map11-rendering.json`、`map11-lighting.json`、`map11-stone-animations.json`、`map11-logic-views.json`、`map11-dialogs.json` 仍未接入运行时；场景对象、动态动画／特效和音频还需逐项恢复绑定。原 `MapGround_11` 和 `MiniMap_11` prefab 尚未恢复为 Unity prefab，Windows 画面及输入也未验证。

普通女神像与失落女神像是不同机制。`WorldGoddessInput` 打开 `GoddessTouchDialog`；区域进度满足 `StageConRegionProgressFullComponent` 后，`StageTouchUI` 通过地图阶段交互启动激活阶段。客户端 `GoddessActiveableManager.OnMapStatusUpdate` 在收到区域状态变化后弹出祝福等级变化提示；`ActiveGoddessHandler` 监听激活实体更新并显示女神像地图通知。失落女神打开 `LostGoddessTouchDialog`，`LostGoddessFurniturePropComponent.GetPropDict()` 从本区域 `GoddessActivePropAwardDict` 读取官方奖励；激活区域状态更新后客户端显示奖励提示。官方客户端只展示服务端已结算的状态和奖励。单机端 `OfflineMainlineProgressSession.RecordGoddessActivated` 现要求地区进度为 `10000`（100%），以地区键和事件回执持久化激活；该方法尚无地图运行时调用方。`OfflineCharacterConfiguredPropSource.CalculateLostGoddess` 已有从区域配置汇总角色属性的能力，但仍须落实“自定义实现”中确认的统一罗拉奖励，并验证家园收益计算和界面显示。Map 11 的地图交互运行时尚未接入，因此尚不能在地图中触发激活。`WorldTouchDialogLostGoddness` 的 `PlayerGetLostGoddessAwardRequest` 是另一个“找回遗失女神奖励”流程，不是失落女神像激活奖励。地图探索进度里程碑奖励通过 `MapGoddessExpProcessItem` 手动领取；命运果实则是独立的日常领取与探索入口。

### 完成方案

以下为拟实施方案，不代表已接入或验证。主要缺口是 Map 11 的实际游玩运行时；当前没有经核实必须由用户补充资料才能继续的外部阻塞。

从 `OfficialMap10GameRuntime` 提取地图加载、移动、点击、战斗调用和存档等公共能力，建立按地图 ID 加载的运行时；森之国的踏板、战斗碑、剧情与出口条件放在地图专属处理器中。提取时保留官方分支、状态变化和执行顺序，并回归 Map 10；不以复制 Map 10 后仅替换地图 ID 代替专属机制实现。

| 部分 | 尚需完成的范围 | 实施要求 |
| --- | --- | --- |
| 地图入口与存档 | 正常进入森之国、地图切换、返回家园、位置恢复 | 以地图 ID 加载资源并接入角色现有存档，消除 Map 10 专用资源路径、存档名和数量断言对本图的限制 |
| 场景与素材 | Unity 导入、场景对象、灯光、水面、原生角色动画、动态动画、特效和音频 | 按官方层级、坐标、材质和动画参数恢复，显式绑定动态资源，核查 Windows shader 与材质兼容性 |
| 区域与实体 | 77 区开放条件、探索度、个人／公共实体状态、九组踏板、六块战斗碑 | 使用官方配置和调用关系驱动交互，持久化解谜、实体完成与地区进度，接入本地任务和剧情条件 |
| 剧情与主线 | 1045～1160 范围的 24 条主线记录、森林老人、世界树、狮鹫剧情 | 恢复条件、对话、镜头、动画、任务推进和中断后的恢复 |
| 战斗与奖励 | 124 个官方阶段、54 个编队数组、NPC 参战、胜负与重复领取保护 | 接现有战斗核心，保留官方等级、位阶、技能、编队和资格条件，完成败北、离场、重进与奖励结算 |
| 女神与探索玩法 | 17 座普通女神、49 座失落女神、探索里程碑、命运果实 | 分别接通入口、状态、界面、动画和结算；失落女神采用已确认的单机奖励，探索里程碑保持手动领取，命运果实单独核对领取、探索事件与奖励链 |
| 地图出口 | 个人资格、公共化、全区激活及 `6001` 出口条件 | 逐条落实官方条件并验证组合分支，不能仅凭任务 1160 完成允许离图 |

### 实施顺序与完成条件

1. **正常进入并操作森之国。** 完成所需资源导入与编译、ClassId 11 目录绑定、正式入口、移动、点击、小地图和位置保存；在 Unity Play Mode 查看真实运行画面并操作输入。
2. **跑通一个地区的完整循环。** 完成打怪、交互和探索度累计，达到 100% 后激活失落女神；核对床的经验收益与小推车罗拉收益各增加 1%，其他产出不被该罗拉加成改变；冷读存档后状态仍在，重复点击不重复结算。此阶段通过后继续其余范围。
3. **扩展到全部地区和主线。** 完成所有区域、踏板、战斗碑、剧情、普通女神、探索奖励、命运果实及出口条件，逐类处理失败和中断恢复。
4. **完成整图验收。** 使用实际鼠标、键盘和涉及的拖拽操作检查输入命中、遮挡、真实渲染、胜败、退出、重进、重复操作、剧情中断及冷读存档；验证全区完成后的离图流程，并回归受共享运行时提取影响的 Map 10。核查 Windows 目标素材和 shader，只有需要验证导出链时才构建 Windows Player；完整游戏验收前不发布或覆盖正式成品。

完成结论须分别说明静态检查、Editor 导入／编译、Play Mode 逻辑／输入／画面和按需 Windows Player 的实际范围。规则核心、转换数据、资源目录或静态检查通过均不能替代整图完成；全部要求实现并验证后才将本页改为“已完成”并移除剩余缺项。

### 待核查技术项与规则决策边界

- `EntityClassId=5110` 的官方显示绑定与正式交互表现：继续查显示表、逻辑视图、实际 bundle 对象、调用方及 CAB 依赖；不能将目前未定位认定为资源缺失。
- 地图个人资格、公共化和实体刷新：继续核对官方条件、请求／响应消费与客户端状态变化，落实已确认的本地时钟及 NPC 方案。
- 大量场景对象的 Unity 加载与运行表现：核查 14,427 批地形和 1,803 个实体的实际开销、加载绑定和状态刷新，必要时调整加载与批处理实现；视觉与玩法保持官方要求，不自行降级。
- 编译及编辑器运行：取得当前编译结果并处理真实错误，再执行 `OfficialMap11Tools.ImportAndValidate` 和实际游玩验证；旧的编辑器状态不能代替当前问题定位。

上述均为可继续查找和实现的技术工作，现阶段不需要用户决定基础架构或重复确认既定规则。若客户端实现、配置、调用方和既有单机约定核查后，仍无法确定具体公式、概率、条件或状态变化，再提供相关类／函数、配置项、资源名称、已查范围和行为缺口，请用户决定；等待时继续不依赖该答案的部分。

### 依据与代码定位

官方转换数据：[regions/11.json](../../assets/official/regions/11.json)、[adventure.json](../../assets/official/adventure.json)、[map11-rendering.json](../../assets/official/map11-rendering.json)、[map11-lighting.json](../../assets/official/map11-lighting.json)、[map11-stone-animations.json](../../assets/official/map11-stone-animations.json)、[map11-logic-views.json](../../assets/official/map11-logic-views.json)、[map11-dialogs.json](../../assets/official/map11-dialogs.json)。Unity 载荷：[map11.json](../../unity/Assets/Official/World/Map/11/map11.json)、[OfficialMap11Tools](../../unity/Assets/Game/Editor/OfficialMap11Tools.cs)。

Unity 共享地图接入：[OfficialWorldMapAssetCatalog](../../unity/Assets/Game/Runtime/Presentation/World/OfficialWorldMapAssetCatalog.cs)、[OfficialWorldMapSession](../../unity/Assets/Game/Runtime/Presentation/World/OfficialWorldMapSession.cs)。map10 运行时仅作架构参考：[OfficialMap10GameRuntime](../../unity/Assets/Game/Runtime/Presentation/World/OfficialMap10GameRuntime.cs)。离线激活与奖励属性：[OfflineMainlineProgressSession](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs)、[OfflineCharacterConfiguredPropSource](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterConfiguredPropSource.cs)。官方地图逻辑与特效参考：[MyPlayableManager](../../.tools/source-packages/official-code/playable-manager-source/GameFramework.MyPlayableManager.decompiled.cs)。
