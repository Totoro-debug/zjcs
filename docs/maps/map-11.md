# 11 森之国

[总索引](../README.md)

状态：未完成

## 地图简介

地图 ID：11。原目录：坎斯汀大陆/森之国。场景类型：`map_world`；原区域配置77项，BGM配置11。

当前验收对象为 Unity / C#。

## 完整还原部分

尚无本图在 Unity 完整运行并验收的结论。

已转换并放入 `unity/Assets/Official/World/Map/11/` 的官方载荷：地图定义含 77 区、81 面积配置、1,803 个放置实体及其一一对应的实体组件、导航和 8 条区域开放条件；绑定 110 条实体进度参数、247 条实体显示、128 条实体 UI、5 条阶段动作，以及地图、区域、逻辑实体配置。Map 11 有 17 个普通女神像（`EntityClassId=6010`），其所在区域各配置一组 `DropItems`；另有 49 个失落女神像（`EntityClassId=6011`），其所在区域各配置一组 `GoddessActivePropAwardDict`。1045～1160 范围含 24 条主线记录。地形几何为 14,427 批次，引用的 475 张官方纹理已复制至 `Resources/Map11/Textures/`；小地图为 85 批次并绑定 2 张官方纹理。输入版本固定为 `854_156383`，来源和输出哈希见该目录的 `*-source-manifest.json`。转换器为 `scripts/prepare_unity_map11_rendering.py`、`scripts/prepare_unity_map10_minimap.py --map-id 11` 和 `scripts/prepare_unity_map11_definition.py`。地形视图已支持原始 Map11 几何头格式，保留 Map10 默认格式。

实体载荷含 1,803 个放置记录、105 个模型绑定、161 份官方几何（97 份基础几何和 24 类模型的 64 份状态几何）、27 个 Spine 实例和 203 张实体贴图。状态 `-1` 保留原 `StateGameObjectConfig` 关闭所有切换对象后的常驻内容，空几何也保留来源文件；女神像激活切换原 0／1 状态。补入的 `building_naka1` 和 `building_luzi` 均从固定版本官方 `worldbuilding/6330_11_view` 的 Sprite 引用重新提取，与已有 PNG 逐字节一致；来源见 `entity-source-manifest.json`。`8210–8213` 世界奖励实体按官方 `Map/entity_map_display` 绑定到公共单物品模板；展示表有常规地面与浅水两个版本，当前载荷保留常规地面版本。剩余 9 个无模型实例属于 `5110` 和 `6533`：`6533` 的官方逻辑视图明确无显示实体；`5110` 的正式显示与交互仍待定位。

战斗载荷为 129 个官方阶段、57 个怪物编队数组、129 个展开编队、17 个战斗场景和 591 个实体阶段绑定，保留玩家等级／位阶动态阶段及官方等级属性缺行语义。`prepare_unity_map11_runtime_data.py` 已将 110 类基础实体组件、44 个故事图（666 个节点）、四个子图（64 个节点）、50 条地图对话、35 个逻辑标签、17 个对话角色、10 个可演出资源及 24 条主线写入本地 `Resources/Map11`。原放置组件是 `Lo` 覆盖，不能代替基础实体模板；基础组件与嵌套阶段列表保持官方顺序。转换测试已通过，来源及输出哈希分别见 `battle-source-manifest.json` 与 `assets/reference/official-map/map11-runtime-source-manifest.json`。

`OfficialMap11Tools.ImportAndValidate` 已在 Unity `2022.3.57f1c2` 实际执行并正常退出，生成地形、小地图、实体 prefab，绑定 ClassId 11，并检查 77 区、1,803 实体、14,427 地形批次、85 小地图批次和相邻格寻路。`prepare_unity_map11_ui.py` 恢复 WorldCanvas、两种女神弹窗、两个动态图标、购买确认框、兑换框和默认物品图标，共八份原生 prefab、125 张 Sprite；保留原层级、脚本引用、状态列表、中文、条件颜色与弹窗曲线。动态布局和镜像图片直接迁移官方 `UGUIJszx`／`MirrorImage` 代码；Common 的七个 TMP 图标字形使用原图集、指标和字符表，并注册原序列化哈希及当前名称哈希。来源见 `UI/source-manifest.json`，加载绑定为 `Resources/Map11/UI`。上述导入不能代替整图玩法验收。

机关配置已补齐 116 个相关放置记录、九组条件锁和七座终端，保留自状态、通知、生成、其他实体状态更新与自动进入下一状态的原执行顺序。`OfflineMapPuzzleSession` 已在 Editor 检查九组解锁、七终端解谜／重置、踏板计数、关联状态通知、奖励实体生成及存档冷读；同时检查 413 只跳过已确认的缺失目标 423，其他未知目标仍拒绝执行。窗口化 Play Mode 已用真实鼠标点击石碑 2302，核对其与 2303／2304／2305 的联动并重新进入地图恢复状态；机关动画和终端原界面尚待完整接入，不能据此认定九组玩法完整完成。

窗口化 Play Mode 同时核对了女神像未达成／可激活／已激活三态、激活回执不重复、1001／1005 各增加 100、最近可站立格传送与位置存档恢复，并检查原 HUD 的初始状态开关。验证使用隔离档案，地区完成度由测试设置，尚未验证自然探索到 100% 的循环。静态检查、资源导入／编译与 `ProjectTools.Validate` 已通过；Windows Player 尚未构建验证，正式入口及剩余表现继续实施。

终端购买链已在带图形渲染的后台 Play Mode 检查：首次赠券、免费重置、成功解谜扣一张、购买取消不扣款、支付 50 晨星购一张后继续解谜，以及零晨星时展示固定数量兑换并在关闭后回到原选项。已查看实际运行截图，修正 TMP 图标绑定、官方按钮动态布局、兑换标题对齐和数量栏激活。该检查调用了回调，没有模拟真实鼠标／键盘；新增购买与兑换界面的实际输入检查仍待桌面空闲。另以隔离存档冷读验证未扣款的中断取消、已扣款的中断补完，以及再次冷读不重复扣券。

世界树与援助的实际故事图已检查计数 0／1／2 时的首项优先和无匹配分支，魔女 Switch 已检查原结果匹配、类型转换及默认出口，剧情状态条件已检查 None／Playing／End，并冷读核对计数与路由。后台 Play Mode 已验证无连接的 `Exit` 按原 `StoryGraph.AsyncRun` 正常结束并只记录一次完成；该项使用回调，未覆盖魔女激活次数来源或完整主线。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

原线上组队按已确认原剧情 NPC 资格、等级、位阶和技能适配，不生成真人玩家。公共刷新使用本地时钟；普通路线保留真实败北、产出和等待，后期资格回访不能记作普通角色自然成长。

用户确认 49 座失落女神像（`EntityClassId=6011`）均须在对应地区探索度达到 100% 后激活；首次激活自动增加床的家园经验收益 1% 和小推车的罗拉（金币）收益 1%。两项分别使用 `HangUpExpAddPercent`（1001）和 `HangUpMoneyAddPercent`（1005），每座各增加定点值 `100`（1%）。`HangUpCartAllPercent`（1034）增加小推车全部产出，不能用于代替罗拉专用加成。49 座像分别对应 49 个不同地区，激活按地图／地区持久化，并以事件回执防止重复结算。

此奖励是已确认的单机差异：固定版本官方 `map_region.GoddessActivePropAwardDict` 为全部 49 座配置经验 +1%，另一项按地区分为不同资源，仅 8 座配置罗拉 +1%。`OfflineGoddessPropSource` 在原属性流水线中替换 `LostGoddess` 来源，对森之国失落女神统一汇总 1001／1005 各 100，保留原表及哈希，避免与原表资源加成叠加；普通女神地区没有该字典，不计入家具奖励。家园实际收益与界面仍需运行核验。

女神像传送采用用户确认的单机落点：优先神像所在格；该格不可站立时取最近可站立格，等距按行、列确定，落点写入本地图位置存档。官方 `PlayerWorldTransferRequest` 只提交目标实体 ID，包内没有服务器选落点实现，因此此项是单机规则。森之国官方出口目标为 Map12；用户已确认本次不迁移 Map12，后续按主线地图逐步扩展。

机关终端结果 254／258 的服务器类为空，单机按用户确认依关联锁配置满足解锁状态，重置恢复机关初始状态和踏板计数。锁 413 的官方结果仍保留对 423 设置状态 1 的原始数据；重新反编译并核对原始 EC、MapInfoWrap、客户端实体定位与生成调用后，423 无放置定义或生成入口。用户确认仅跳过这条无目标更新，继续执行生成奖励实体 401 等其余结果；其他未知目标仍报错，不套用此例外。

用户确认一键解谜成功时消耗一张物品 325，重置免费，保留原固定奖励 45 首次赠送的一张。原剧情称其为“黄金甲虫”，物品表中的正式物品名为“破局之匙”，两处原文分别保留。购买券时晨星不足会打开兑换；因原 `ShopBuyIntent` 关闭确认框后没有通知 `ItemQuikBuyNode`，用户确认单机在兑换／商店关闭后走原故事“购买失败”分支，返回机关选项，避免剧情一直等待。

`SelectorNode`／`SwitchNode` 的客户端只消费服务器返回的 `SelectedIndex`，包内没有选择实现。用户确认单机按原配置顺序取首个为真的条件或首个与输入相等的结果，均未匹配时走原 `Exit`；结果比较保留 `SerializableObj.Compare` 的原始类型转换。规则用于世界树、援助和魔女剧情；分支路由接入不代表魔女激活状态和整段剧情已经完成。

## 尚未实现

恢复 77 区、九组踏板、六块战斗碑、森林老人／世界树／狮鹫剧情与 1045～1160 主线；验证个人资格、公共化、全区激活与 6001 出口条件，不能仅按任务 1160 完成推断可以离图。

继续按原配置接通正式入口、区域、放置实体、条件、交互、战斗、奖励和出口前置状态。共享运行时已支持按地图 ID 加载资源、区域开放条件、位置与状态存档，但 Map10 的正常出口与家园返回入口尚未接通本图。原地图 NPC、建筑、世界奖励、交互物、怪物及小地图 bundle 已提取校验；`map11-lighting.json`、`map11-stone-animations.json`、`map11-logic-views.json`、`map11-dialogs.json` 的完整动态行为、特效和音频仍待绑定。地形与小地图原生 prefab 已生成，Windows 实际画面、输入和完整流程仍待验证。

普通女神像与失落女神像是不同机制。共享运行时的 Map11 女神处理器已按基础阶段先检查地区 100%，再依次执行结果 204（激活并持久化回执）与 209（自身状态变为 1）；已激活神像按官方输入分支直接打开弹窗。两种基础模板均没有 `StageResultAwardComponent`，地区 `DropItems` 在 `GoddessTouchDialog.RefreshAward` 中仅生成珍宝预览，不在激活时自动发放。珍宝预览及详情操作限制、普通女神祝福等级派生和提示、失落女神奖励提示动画尚待接通。`WorldTouchDialogLostGoddness` 的 `PlayerGetLostGoddessAwardRequest` 是独立的“找回遗失女神奖励”流程，不能作为像的激活奖励。探索里程碑仍须按 `MapGoddessExpProcessItem` 手动领取；命运果实独立核对日常领取与探索事件。

### 完成方案

以下为拟实施方案，不代表已接入或验证。主要缺口是 Map 11 的实际游玩运行时；当前没有经核实必须由用户补充资料才能继续的外部阻塞。

从 `OfficialMap10GameRuntime` 提取地图加载、移动、点击、战斗调用和存档等公共能力，建立按地图 ID 加载的运行时；森之国的踏板、战斗碑、剧情与出口条件放在地图专属处理器中。提取时保留官方分支、状态变化和执行顺序，并回归 Map 10；不以复制 Map 10 后仅替换地图 ID 代替专属机制实现。

| 部分 | 尚需完成的范围 | 实施要求 |
| --- | --- | --- |
| 地图入口与存档 | 正常进入森之国、地图切换、返回家园、位置恢复 | 以地图 ID 加载资源并接入角色现有存档，消除 Map 10 专用资源路径、存档名和数量断言对本图的限制 |
| 场景与素材 | Unity 导入、场景对象、灯光、水面、原生角色动画、动态动画、特效和音频 | 按官方层级、坐标、材质和动画参数恢复，显式绑定动态资源，核查 Windows shader 与材质兼容性 |
| 区域与实体 | 77 区开放条件、探索度、个人／公共实体状态、九组踏板、六块战斗碑 | 使用官方配置和调用关系驱动交互，持久化解谜、实体完成与地区进度，接入本地任务和剧情条件 |
| 剧情与主线 | 1045～1160 范围的 24 条主线记录、森林老人、世界树、狮鹫剧情 | 恢复条件、对话、镜头、动画、任务推进和中断后的恢复 |
| 战斗与奖励 | 129 个官方阶段、57 个编队数组、NPC 参战、胜负与重复领取保护 | 接现有战斗核心，保留官方等级、位阶、技能、编队和资格条件，完成败北、离场、重进与奖励结算 |
| 女神与探索玩法 | 17 座普通女神、49 座失落女神、探索里程碑、命运果实 | 分别接通入口、状态、界面、动画和结算；失落女神采用已确认的单机奖励，探索里程碑保持手动领取，命运果实单独核对领取、探索事件与奖励链 |
| 地图出口 | 个人资格、公共化、全区激活及 `6001` 出口条件 | 逐条落实官方条件并验证组合分支，不能仅凭任务 1160 完成允许离图 |

### 实施顺序与完成条件

1. **正常进入并操作森之国。** 完成所需资源导入与编译、ClassId 11 目录绑定、正式入口、移动、点击、小地图和位置保存；在 Unity Play Mode 查看真实运行画面并操作输入。
2. **跑通一个地区的完整循环。** 完成打怪、交互和探索度累计，达到 100% 后激活失落女神；核对床的经验收益与小推车罗拉收益各增加 1%，其他产出不被该罗拉加成改变；冷读存档后状态仍在，重复点击不重复结算。此阶段通过后继续其余范围。
3. **扩展到全部地区和主线。** 完成所有区域、踏板、战斗碑、剧情、普通女神、探索奖励、命运果实及出口条件，逐类处理失败和中断恢复。
4. **完成整图验收并发布。** 使用实际鼠标、键盘和涉及的拖拽检查输入、遮挡、渲染、胜败、退出、重进、重复操作、剧情中断及冷读存档；验证出口前置状态并保留 Map12 未迁移边界，回归受影响的 Map10。通过相关检查并提交代码、资源及本页后，构建 Windows Player，实际启动核验受影响流程，再发布到 `D:/Desktop/杖剑传说/杖剑传说.exe`，保护用户原存档并核对桌面快捷方式。

完成结论须分别说明静态检查、Editor 导入／编译、Play Mode 逻辑／输入／画面及 Windows Player 的实际范围。逐功能／地图发布是完成条件；转换数据或静态检查通过均不能替代完整可游玩成果。全部要求实现并验证后才将本页改为“已完成”并移除剩余缺项。

### 待核查技术项与规则决策边界

- `EntityClassId=5110` 的官方显示绑定与正式交互表现：继续查显示表、逻辑视图、实际 bundle 对象、调用方及 CAB 依赖；不能将目前未定位认定为资源缺失。
- 地图个人资格、公共化和实体刷新：继续核对官方条件、请求／响应消费与客户端状态变化，落实已确认的本地时钟及 NPC 方案。
- 大量场景对象的 Unity 加载与运行表现：核查 14,427 批地形和 1,803 个实体的实际开销、加载绑定和状态刷新，必要时调整加载与批处理实现；视觉与玩法保持官方要求，不自行降级。
- 编译及编辑器运行：取得当前编译结果并处理真实错误，再执行 `OfficialMap11Tools.ImportAndValidate` 和实际游玩验证；旧的编辑器状态不能代替当前问题定位。

上述均为可继续查找和实现的技术工作，现阶段不需要用户决定基础架构或重复确认既定规则。若客户端实现、配置、调用方和既有单机约定核查后，仍无法确定具体公式、概率、条件或状态变化，再提供相关类／函数、配置项、资源名称、已查范围和行为缺口，请用户决定；等待时继续不依赖该答案的部分。

### 依据与代码定位

官方转换数据：[regions/11.json](../../assets/official/regions/11.json)、[adventure.json](../../assets/official/adventure.json)、[map11-rendering.json](../../assets/official/map11-rendering.json)、[map11-lighting.json](../../assets/official/map11-lighting.json)、[map11-stone-animations.json](../../assets/official/map11-stone-animations.json)、[map11-logic-views.json](../../assets/official/map11-logic-views.json)、[map11-dialogs.json](../../assets/official/map11-dialogs.json)。Unity 载荷：[map11.json](../../unity/Assets/Official/World/Map/11/map11.json)、[OfficialMap11Tools](../../unity/Assets/Game/Editor/OfficialMap11Tools.cs)。

Unity 共享地图接入：[OfficialWorldMapAssetCatalog](../../unity/Assets/Game/Runtime/Presentation/World/OfficialWorldMapAssetCatalog.cs)、[OfficialWorldMapSession](../../unity/Assets/Game/Runtime/Presentation/World/OfficialWorldMapSession.cs)。map10 运行时仅作架构参考：[OfficialMap10GameRuntime](../../unity/Assets/Game/Runtime/Presentation/World/OfficialMap10GameRuntime.cs)。离线激活与奖励属性：[OfflineMainlineProgressSession](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs)、[OfflineCharacterConfiguredPropSource](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterConfiguredPropSource.cs)。官方地图逻辑与特效参考：[MyPlayableManager](../../.tools/source-packages/official-code/playable-manager-source/GameFramework.MyPlayableManager.decompiled.cs)。
