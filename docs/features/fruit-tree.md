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

### 待迁移的规则与表现

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
