# 果树生产与领取

[总索引](../README.md)

状态：未完成

## 功能简介

家园生产设施4（物品4016）生产果实56；宠物设施11（物品4017）进入[幻兽结缘](pet-gacha.md)。果实成熟后点击果树领取，当前实现按已确认的单机规则将果实转成命运果实115。活动实例及活动奖励由所属活动系统提供，不在果树内生成。

## 原始官方实现

官方果树包含两级设施、成熟／产出／领取、产出加成、教学和原文字／模型绑定；产出加成按原 output_addition 等表处理。

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

`scripts/prepare_fruit_tree_assets.py` 已实际执行，固定 `871_156598` 的原 `FruitTree.prefab`、两个子家具、`HomeNotice`、果实效果模板和四个原维修/图标 prefab 均按原 bundle/CAB 解析。独立运行数据 `fruit-tree.json` 包含原状态切换路径和可见标志、碰撞范围、6 层 Sprite、三段 `loop_1/2/3` 的 12 fps 原生网格及循环时长、23 个场景粒子发射器、十个果实槽位和原果实效果发射器；维修效果时序为原 0.6 / 1.5 秒。原成功通知的 `mPropItemPrefab` 另精确提取为 `FurnitureProductionProp`，原语言、对象、输入和输出 SHA 保存在 `fruit-tree/source-manifest.json`。

本模块来源共引用 50 张官方 PNG：最初登记 47 项时新增 14 张图像，属性行使引用数增加 3 项，但这 3 项与已有中央登记文件同字节，实际不新增 PNG。此前一次通知将“新增引用”误称“新增图像”，最终差集和修正说明保留于 `fruit-tree/registration-history/registered-v2.json`。该版转换 JSON SHA 为 `ab58e8e997ae5eb0ae68fec1005fc5f8f64cca0164d4b1d195f316dc64cbd5fd`，初版 JSON / 来源指纹和本版完整来源快照保留于该历史目录。

原 RPC 23624 的最终奖算法仍未找到，实际 `fruit_harvest` 原子失败并保留果实、计时和存档；未补造收获礼包。下一素材包仅按实际文件差集纳入该转换结果，功能完成情况单独验收。

已实际执行固定 APK `871_156598` 的转换，前一版 `ab58e8e9…`、元数据版 `c15d78b8…` 和遮罩中间版 `1777289a…` 的原 JSON／来源文件保存在 `fruit-tree/history/`。转换时曾沿 `ProcessGuideHint` 的 MonoBehaviour 指针误用完整组件读取；修正为仅通过原 MonoBehaviour 头定位其 GameObject，原遮罩字节布局仍按完整长度严格校验，未放宽或绕过原来源哈希。

最终 `fruit-tree.json` SHA256 为 `60a63410ff717a22486306170620ecaf0d54a5d6cd7fad623d296702dd5681bc`，来源 SHA256 为 `7e465e21973c79180e08d5154d8a159258fe49241b1b3b3b21aa0a5cce176bef`。保留原状态、网格及粒子，补入 22 条 TMP 换行、溢出、边距／自动字号记录、118 个含实际家园槽位平移的对象矩阵及 4 个原 BoxCollider 的世界边界。原成功通知 `CloseObj/Text` 为 overflow 0，接入现有公共 UI 的不裁切显示；未改公共 UI 文件。

原 `RepairFurnitureNode` 在对白后先等待再次点击真实设施，再打开维修窗口并突出维修按钮。现已接入这个控制顺序，教学遮罩区域来自原模型位置、活跃子 Collider 合并边界及节点 Offset／Scale。原 `StoryMask.MaskAssetProxy` GUID `8578cbcb823445046ba91b8e152fe61b` 对应 `Assets/Prefabs/UI/Story/Mask.prefab`；原 `StoryMaskGraphic` 的圆角半径为 23、每角 10 段，颜色 alpha 为 199/255，射线洞口为矩形。

当前模块引用 57 张官方 PNG，比前一登记增加 7 条模块引用；57 张均与中央登记及先前独立恢复的配套包同字节，实际新增 PNG 为 0，原有 50 张无修改。58 条运行路径已在原显式加载清单内，只更新中央 JSON／来源指纹，保留共享图片原凭据及绑定字节。原两份语言二进制文件均经转换核对；全部 20 个文字键在来源中登记。

果树 prefab 实际没有独立 `HomeDayNight` 组件，`day_night` 为空。夜光来自 `HomePlayableClipBind` 的原 24 秒曲线：接入原家园 30 倍时间及 FunctionType 90 的门槛／锁定 12 点状态，并按实际路径应用两条显隐和三条 Sprite 颜色曲线。三条原 Light2D 强度曲线及原粒子／渲染绑定的后续接入见“家园灯光与材质迁移”；没有按名称添加昼夜开关。

原通知通过 `FurnitureRepairedNotifyDialog → HangUpUtils → BattleFormulaHandler → PlayerPropItem → PlayerComplexProp → LanguageUtils` 先把每小时收益转 long，再格式化。原 hang_up 100 使用 HourlyOutput 0.25、比例属性 1030、整数属性 1029，1029 的 Float／Percent 均为 false，因此无附加属性时显示 0 是原版行为；按原配置与 fixed 的 single 转换取值，不补造小数显示。

原 RPC23624 最终领奖仍无可验证算法，继续保持原子失败；转换与来源登记不表示完整果树玩法完成。

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

### 已确认的本地接线与边界

`prepare_output_additions.py` 按固定版本官方 Luban 构造函数解码 `output_addition`、`activity_output_addition`、`planes_merge_output_addition`，输出 `assets/official/output-additions.json`。原数据、构造函数、公共逻辑与消息代码及输出哈希见 `assets/reference/output-additions/source-manifest.json`。仅迁移已有加成记录及原公式，不生成活动实例；本地分段结算范围与用户确认见果树功能文档。

## 尚未实现

- 活动系统尚无调用点把真实活动记录传给 `SetOutputAddition`。活动触发归活动系统负责，未伪造活动实例或奖励。
- 受影响流程尚未在 Unity Play Mode 完成真实鼠标／键盘输入、运行画面、存档重进及 Windows Player 验证。静态元数据检查不等于该项通过。
