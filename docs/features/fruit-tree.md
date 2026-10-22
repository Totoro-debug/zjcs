# 果树生产与领取

[总索引](../README.md)

状态：未完成

## 功能简介

家园生产设施4／物品4016负责生产，宠物设施11／物品4017承接幻兽入口。果实领取与世界地图探寻是独立操作。

范围为日常果树及已有活动加成数据；活动实例触发由对应活动系统接入。

以上为 Unity / C# 的目标范围，尚未完整接入和验收。

## 完整还原部分

尚无本功能在 Unity 完整接入并运行验收的结论。当前已有家园场景中的果树图层、点击区域、通用家具维修框架及配置；`unity/Assets/StreamingAssets/Home/rules.json` 已包含设施4／11、挂机100和40小时初始产出的字段，但不构成苹果生产与领取流程完成。

实际入口 `HomeFeatureRuntime.OnFurnitureClicked → OpenFurniture` 只为床铺、小推车、邮箱打开业务页面，果树4落入 `HomeHudView.ShowFurniturePlaceholder`。`HomeSceneView` 按静态图层创建网格，没有接入 `HomeFruitTree` 的树状态、骨骼循环和成熟苹果实例。原代码、配置、已转换素材和旧引擎测试仅是迁移依据。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

RPC23624 服务端领取方法体未取得。用户明确补证一次收取全部成熟果实，果实56按1:1转为命运果实115，无额外物品；保留小数进度，超容量成熟果实也全部领取。库存、产出时间、费用、奖励和教学在本地事务中共同保存，失败回滚。

用户于2026-10-19确认仅接入已有加成数据的生命周期，对应活动触发留给活动系统。客户端历史分段提供器 `PlayerAutoIncreateItemPropProvider.GetSegmentedIncreaseData` 方法体返回空，服务端结算未取得；本地按活动生效／到期边界先结清上一段，保留小数进度，再更换速度与容量。容量恢复后保留已成熟的超额果实，领取仍按上述规则结算。

## 尚未实现

| 功能 | 官方规则／调用关系 | 当前 Unity 缺项 |
| --- | --- | --- |
| 生产设施开放 | `FurnitureInfo` 的4016／类型4：任意地图女神已激活、31级可修，30级预告，38级自动修复条件也要求女神；原费用为空，`CanLevelUp=false` | `HomeFeatureRuntime.ReloadContext` 未赋值 `AnyGoddessActive`，而 `OfficialHomeRules.IsMet` 直接读取该字段，正常角色无法满足果树条件；自动修复与专属教学未接线。果树不是普通可升级家具 |
| 宠物设施修复 | 4017／类型11：40级预告、50级可修，`AdditionalRepairList=[4016]`，修复教学 `FruitTreeUnlock2`；官方 `HomeMyPlayerData.IsMetFurnitureRepairCondition` 只检查 `UnlockCondition`，未将该列表用作客户端前置条件 | 当前 `OfflineHomeProgression.RepairFurniture` 将该列表强制当作前置条件，又把物品ID4016与存档家具 `Type` 比较，已修复的类型4仍不能通过。附带列表的服务端执行语义尚未取得，不能由字段名推断；需先保留真实ID映射并核对处理分支 |
| 苹果生产 | 物品56的 `ItemEffectAutoIncrease`：每小时0.25、容量15、属性1030；`AutoIncreaseItemUtils` 按小时累积，保留 `Decimal`，容量下降时保留已成熟超额 | 挂机100虽引用奖励规则11050，但 Unity 的 `Home/rules.json.rewardRules` 未导出11050；`BuildIncomeDefinitions` 取不到规则便跳过。通用按储存小时结算的收入类不能代替物品自动增长、容量及余数状态 |
| 首次修复 | `FruitTreeUnlock1`、挂机100的 `InitialExtraRewardSeconds=144000`，按已确认方案首次清零56并结算40小时，防止重入重复发放 | `initialExtraRewardSeconds` 只有数据声明，没有运行时消费；首次生产与教学、库存的共同保存未实现 |
| 苹果领取／交互 | `HomeFruitTree.OnValidClick` 按子设施顺序先尝试领取；`HomeFruitTreeProduceFruit.TryCallFunction` 检查成熟数量、调用RPC23624，成功或错误1243清掉场景果实；无果实时显示下一颗成熟倒计时；已修宠物设施时可转入结缘 | 领取入口、56→115、本地事务、倒计时、奖励条及领取次数事件未接入；不能把点击果树直接等同于打开结缘 |
| 加成生命周期 | 产速／容量加成32／33，使用原产出加成配置、玩家记录与活动／合服映射；按已确认方案分段结算 | 生效、叠加、替换、到期、禁用、超容量与存档恢复未接入苹果状态 |
| UI、动画与提示 | 原树组合状态、子设施四态、三段循环、苹果挂点／实例、维修提示、两段教学、成熟反馈、红点与装潢隐藏 | 需要恢复原 prefab 层级、骨骼／网格、特效、材质、音效及动态加载绑定；现有场景图层与两个家具图标 prefab 不等于完整恢复 |

结缘、积分、馈赠、详情／历史及抽奖概率的范围统一见[幻兽结缘与兑换](pet-gacha.md)，此页只维护生产与领取。

### 原始官方功能与逐项待实现清单

本清单覆盖已取得官方源码、配置和 prefab 绑定中与日常果树有关的功能。下列各项均未完成 Unity 整条流程；已取得的数据、通用维修代码和场景图层只按上文所述范围复用。结缘侧的完整清单由[幻兽结缘页](pet-gacha.md#原始官方功能与逐项待实现清单)维护。

| 原始官方功能 | 官方依据与行为 | 待实现内容 |
| --- | --- | --- |
| 生产设施预告、解锁与自动修复 | 4016／设施4；条件40、3030、3031、3038区分女神激活、30级预告、31级手动与38级自动条件 | 把真实女神状态传入条件上下文，恢复各条件提示与触发时机；不能只按等级放开 |
| 宠物设施预告与修复 | 4017／设施11，40级预告、50级修复；`AdditionalRepairList=[4016]` | 修正物品ID与家具类型的映射；附带修复语义未确认前不得当作前置条件；恢复对应入口和提示 |
| 子设施与整树状态联动 | `FurnitureBase` 的 Locked／RepairNotice／CanRepair／Functional；`HomeFruitTree` 的 None／Repair1／Function1／Repair2／Function2；后序设施可修或预告时会影响前序未修设施的状态锁 | 按 `OnGetStateLock → UpdateState → RefreshState` 恢复联动；枚举五种状态与载荷实际配置不可混作“五套动画” |
| 维修对话与演出 | `FurnitureBase.DoRepair` 优先播放未完成的 `RepairStoryName`，否则打开原维修框；维修特效先显示，按 `mRepairEffectShowTime` 切状态，再按完整时长关闭 | 两段维修故事／教学、维修框、完成通知、特效时序、遮罩、输入与保存；故事与修复重进不重复执行 |
| 初次生产 | 挂机100、规则11050→56、`InitialExtraRewardSeconds=144000`；已确认的本地处理见“自定义实现” | 补导出11050，接首次清零与40小时结算、允许初始超容量、教学推进和一次性标记 |
| 持续生产与容量 | `ItemEffectAutoIncrease`／`AutoIncreaseItemUtils`；成熟整数、`Decimal`、`LastUpdateTime`、速率、自然容量和已成熟超额分别处理 | 正常在线／离线分段结算、成熟增长、满容量、余数、容量变化、重启恢复；不可替换成通用挂机小时仓库 |
| 产速／容量属性与加成 | 属性1030；`AutoIncreaseItemPropUtils`、`AutoIncreaseItemOutputAdditionUtils` 及加成32／33；已有活动记录控制生效区间 | 恢复原浮点公式、总数量限制、新增／替换／失效／禁用与到期分段；活动实例由其所属系统提供 |
| 果实挂点与对象复用 | `HomeFruitTreeHangUpHandlerFruitItem.Init` 按 `min(成熟数, 挂点数)` 显示，逐挂点缓存实例；配置可指定资源GUID，否则取默认 prefab | 按原坐标、缩放、排序、默认／自定义资源恢复果实；表现数量不反向限制库存或收取数量 |
| 成熟刷新与页面生命周期 | 原定时成熟刷新；床铺快速挂机成功、调试时间偏移会刷新；离开自己的家园隐藏，切回重建；禁用／销毁取消任务及订阅 | 接时间与库存事件、生成下一颗果实、清理／复用和返回刷新；不能每帧重复生成或留下旧任务 |
| 树点击的完整优先级 | None时原提示；低于Function1不执行业务；按子设施顺序 `TryCallFunction`，全部失败后才 `TryCallTip` | 原点击区域、成熟果实优先收取、无果时已修宠物设施进入结缘、未能进入时成熟倒计时；维修入口与树主点击分别命中 |
| 领取结果与事件 | RPC23624；错误0／1243重置场景果实；客户端成功回调增加领取次数并触发 `FireOnGetFruitTreeReward`；`WaitGotFruitTreeRewardNode` 教学等待节点消费事件 | 按已确认56→115收取与事务保存，成功后再刷新表现／教学／计数；失败保持状态，防连点重复领 |
| 奖励与条件提示 | `HomeFruitTreeProduceFruit.OnGotAward` 吞掉含56的教学奖励通用展示，FruitTree来源交给 `AwardListPanel`；设施4／11与PetGacha分别检查功能开关 | 原奖励条、来源路由、条件文案、禁用反馈与成熟倒计时；不能把教学苹果额外再弹一次普通奖励 |
| 树、通知与装潢表现 | 原树状态／循环、维修通知、世界UI、教学遮罩、红点与装潢隐藏；来源载荷保留原层级和绑定 | 原骨骼／网格动画、粒子、材质、音频、排序和点击遮挡；恢复通知／红点实际数据驱动及装潢进出行为 |
| 活动外观扩展入口 | `HomeFruitTreeChristmasHangUpEffectItem.Init(index)` 按 `index % 2` 选择外观；挂点支持单独资源 | 记录并保留原变体绑定机制；本次不因此自动创建圣诞活动或替换日常果实，不把活动变体记作日常已开放功能 |

本地状态还须覆盖设施修复、首次产出标记、成熟库存56、奖励库存115、余数与结算时间、加成起止及禁用状态、领取计数与教学状态。它们与主存档的共同保存、失败回滚、旧存档缺字段迁移均未完成；界面缓存与挂点实例不作为存档中的生产事实。

### 尚待核实的官方服务端行为

RPC23624的实际领取结算已经有用户明确补证，使用“自定义实现”中的已确认规则。4017的附带修复列表执行顺序及是否连带首次产出仍缺服务端处理依据；已取得客户端只检查解锁条件，不能从字段名推出额外规则。离线结算的异常时间、总收益上限如需超出当前已确认范围，须另行明确；不把这些缺口写成原版已知行为。

### 规则与表现恢复要求

以下要求均须在 Unity 中重新接入和验证；它们描述迁移目标，不构成当前实现结论。

生产设施要求已激活任意地图女神并达31级，30级预告；38级自动修复条件同样要求女神激活。宠物设施40级预告、50级修复。两级原费用为空，均不可升级。4017另有 `AdditionalRepairList=[4016]`；已取得客户端修复条件没有“先修生产设施”的判断，不将此列表直接解释为前置条件，服务端附带修复分支仍须核实。

首次修复清零果实56并按原配置结算40小时初始生产；基础速率每小时0.25，自然容量15，产速百分比属性1030，首次生产允许超容量。后续维修和迁移不重复发放。

原四态、三段循环、果实实例、维修提示、两段教学和奖励条接入。提示读取真实条件、召唤货币／次数、成就和商店状态，装潢时隐藏；没有实际活动实例时不伪造活动红点。

产出加成32／33读取原108条产出加成配置与活动／合服映射，按实际玩家加成记录判断生效、叠加和到期；原速度／容量公式保留浮点精度及总数量限制。支持新增、替换、显式失效、全局／单配置禁用和存档恢复；日常不创建回归、成长加速或合服活动，也不增加普通果树加速道具。

### 依据与代码定位

官方客户端根目录 `.tools/source-packages/official-code/client-source/`：`HomeFruitTree`、`HomeFruitTreeSubFurniture`、`HomeFruitTreeProduceFruit`、`HomeFruitTreePet`、`HomeFruitTreeHangUpHandlerFruitItem`、`HomeFruitTreeHangUpEffectItem`、`FurnitureBase`、`HomeMyPlayerData`。共享目录 `common-source/Common/`：`ItemEffectAutoIncrease`、`AutoIncreaseItemUtils`、`AutoIncreaseItemPropUtils`、`AutoIncreaseItemOutputAdditionUtils`、`PlayerAutoIncreateItemPropProvider`。RPC23624的客户端声明／回调在 `common-source/PlayerServiceProxy.cs` 和 `PlayerServiceProxyImpl.cs`；回调会增加领取次数并触发 `FireOnGetFruitTreeReward`，不是服务端领取结算实现。

配置依据为 [rules.json](../../assets/official/rules.json) 的 `furniture`、`conditions`、`item`、`hang_up`、`hang_up_reward`、`reward_rule`；条件40／3030／3031／3038，挂机100，规则11050→56。当前代码：[HomeFeatureRuntime.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs)、[HomeSceneView.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeSceneView.cs)、[OfflineHomeProgression.cs](../../unity/Assets/Game/Runtime/Offline/Home/OfflineHomeProgression.cs)、[OfficialHomeRules.cs](../../unity/Assets/Game/Runtime/Offline/Home/OfficialHomeRules.cs)。导出链为 [prepare_unity_home_runtime.py](../../scripts/prepare_unity_home_runtime.py)，其 `REWARD_RULE_IDS` 未纳入11050。

原 prefab、组件字段、状态和转换输出来源见[果树来源清单](../../assets/reference/fruit-tree/source-manifest.json)，载荷 [fruit-tree.json](../../assets/official/fruit-tree.json) 保留树循环、状态、挂点、23个粒子发射器、维修与教学界面；这是旧转换输入，仍须按[Android → Windows流程](../asset-management.md#android-资源迁移到-windows)恢复原生资产与运行绑定。

### 验证范围与完成条件

已对照官方源码、固定版二进制表、当前 Unity 调用方与生产配置，并核验果树主载荷与来源清单的输出哈希。此为代码、数据和资源检查，未执行本模块 Editor 导入／编译、Play Mode 逻辑、真实输入、画面／动画／音频、保存恢复或 Windows Player 验证，不标记功能通过。

后续须按[实际运行验证要求](../asset-management.md#实际运行验证方法与要求)，从家园入口验证女神及30／31／38／40／50级条件、两次修复、40小时只发一次、容量／余数／超容量、无果提示、收取与结缘优先顺序、已有加成生命周期、奖励到账和失败回滚，并在隔离存档重进后核对状态。
