# 果树生产与领取

[总索引](../README.md)

状态：未完成

## 功能简介

家园生产设施4（物品4016）生产果实56；宠物设施11（物品4017）进入[幻兽结缘](pet-gacha.md)。果实成熟后点击果树领取，当前实现按已确认的单机规则将果实转成命运果实115。活动实例及活动奖励由所属活动系统提供，不在果树内生成。

## 完整还原部分

### 入口、条件与修复

`HomeFeatureRuntime.OnFurnitureClicked → OpenFurniture` 已将家具类型4接到果树状态和维修交互，类型11接到结缘；点击果树由 `OnFruitTreeClicked` 先结算生产，再按成熟状态处理领取、提示或结缘。`ReloadContext` 使用家园已激活女神区域填充 `AnyGoddessActive`。

固定版 `furniture`、`conditions` 配置：4016要求任意女神已激活且31级可修，30级显示预告，38级自动修复仍要求女神；4017要求50级可修、40级预告。自动修复只针对4016。费用为空，设施不可升级。维修完成、维修提示、遮罩和 `FruitTreeUnlock1/2` 教学接入本地状态。

用户确认4017只维修自身并开放结缘入口，不连带维修4016，也不替代4016自己的开放／维修条件。离线事务只写入 `PetFacilityRepaired`；4016状态仍由其固定配置条件独立计算。官方客户端 `AdditionalRepairList=[4016]` 的服务端执行语义未取得，因此此处按用户确认的单机规则处理。

### 生产、加成与领取

`OfflineFruitTreeService` 以56、115、初始产出144000秒、基础容量15和每小时0.25为固定参数。首次修复在同一存档事务中清空56并结算40小时初始产出，只执行一次。`OfflineFruitTreeProgress` 保存修复、库存、分数余量、更新时间、累计领取、教学标记及加成记录。生产按本地时间结算，容量变小不会丢掉已成熟超额库存。

`AdvanceTo` 按每次打开游戏的本地时间补算；检测到时间早于果树检查点时返回 `ClockRollback`，UI提示“检测到系统时间早于果树上次存档时间，请不要修改系统时间。”该边界按用户确认的回拨规则处理，不重设检查点。

`Collect` 将全部成熟56按1:1加入115，并在一次 `ApplyInventoryTransaction` 中清空成熟库存、更新累计／领取次数及奖励。交易失败不先清库存。产出加成32／33经 `SetOutputAddition` 先结算旧区间，再保存活动记录；`AdvanceSegments` 按起止边界分段使用官方产速／容量配置。此接口不创建活动实例。

`HomeSceneView` 根据存档果树状态驱动树循环、子设施状态、果实挂点及维修反馈；`HomeHudView.FruitTree.cs` 提供原对话／肖像、逐行教学、目标遮罩和维修界面。果树每5秒刷新成熟提示，成熟库存与场景挂点数量分开处理；离开或销毁时清除教学遮罩及临时 UI。

### 配置与代码

规则数据见 [rules.json](../../assets/official/rules.json) 的 `furniture`、`conditions`、`item`、`hang_up`、`hang_up_reward`、`reward_rule`，以及[果树载荷](../../assets/official/fruit-tree.json)和[来源清单](../../assets/reference/fruit-tree/source-manifest.json)。主要实现为 [OfflineFruitTreeService.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineFruitTreeService.cs)、[OfflineFruitTreeProgress.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineFruitTreeProgress.cs)、[HomeFeatureRuntime.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs)、[HomeSceneView.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeSceneView.cs) 与 `HomeHudView.FruitTree.cs`。

官方依据包括 `HomeFruitTree`、`HomeFruitTreeSubFurniture`、`HomeFruitTreeProduceFruit`、`HomeFruitTreePet`、`FurnitureBase`、`HomeMyPlayerData`、`ItemEffectAutoIncrease`、`AutoIncreaseItemUtils` 和 `AutoIncreaseItemOutputAdditionUtils`。RPC23624只取得客户端请求与回调，未取得服务端结算方法体。

## 自定义实现

本地离线规则由用户确认：4017维修只写自身状态并开放结缘，不连带维修或越过条件开放4016；一次领取全部成熟果实、56按1:1转115、无额外奖励；小数余量及超容量成熟库存保留，失败时奖励、库存和检查点共同回滚。离线产出加成只消费活动系统提供的已有记录，按生效／到期边界分段结算，不自行构造活动。

## 尚未实现

- 活动系统尚无调用点把真实活动记录传给 `SetOutputAddition`。活动触发归活动系统负责，未伪造活动实例或奖励。
- 受影响流程尚未在 Unity Play Mode 完成真实鼠标／键盘输入、运行画面、存档重进及 Windows Player 验证。静态元数据检查不等于该项通过。
