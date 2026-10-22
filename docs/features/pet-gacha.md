# 幻兽结缘与兑换

[总索引](../README.md)

状态：未完成

## 功能简介

从幻兽页或果树宠物设施进入原结缘页面，使用原材料107，支持抽取、演出、记录、积分／馈赠及商店7。

以上为 Unity / C# 的目标范围，尚未完整接入和验收。

## 完整还原部分

尚无本功能在 Unity 完整接入并运行验收的结论。当前 Unity 家园 `HomeFeatureRuntime.OpenFurniture` 将果树路由至通用占位提示，`Assets/Game/Runtime` 中没有结缘页面控制器、抽取服务、结缘积分／馈赠状态或历史存档。现有幻兽图标、战斗模型及家园家具图标不构成结缘实现。

官方客户端、共享代码、固定版奖池和旧转换载荷已可取得；原代码、配置、已转换素材和旧引擎测试仅是迁移依据，不作为本功能已还原的证据。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

抽奖随机序列和库存持久化由本地替代服务器，类别／物品权重和已知保底取原配置，不猜测额外动态概率。用户要求的价格黑底隐藏及深色价格字是明确界面差异。

抽奖、材料、积分、保底、重复转换和记录在一次事务保存，失败全部回滚；环境特效使用独立随机源，不影响奖励。

## 尚未实现

| 功能 | 官方规则／调用关系 | 当前 Unity 缺项 |
| --- | --- | --- |
| 结缘入口与外壳 | `HomeFruitTreePet.TryCallFunction → PetGachaPanel.Init`；`PetGachaNavigateHandler` 检查已修宠物设施、战斗／世界状态、功能开关；原外壳有结缘／馈赠／商店／活动结缘标签 | 家园果树、幻兽页、定向导航、返回、标签、顶栏、红点及条件反馈未接通；苹果生产与前置修复见[果树页](fruit-tree.md) |
| 抽取与限制 | `GachaType.Pet=3`、箱子6003、107每抽1个；`OpenCounts=[1,10,100]`；`GachaPanelBase.Refresh` 在此配置下使用固定十抽，当前持有107≥100才显示百抽；`PetGachaSubPanel.GachaSend` 检查演出保护与幻兽每日10000次限制 | 本地逐抽结算、扣材、每日计数、余额刷新、百抽确认和结果页继续抽取未实现；这里不套用古遗物的动态2～10抽规则 |
| 结缘积分 | 箱子固定奖励254×1／抽；成就类型66、组16读取 `PetGachaScoreCount` 黑板；12条原始模板含循环段。`ScoreRewardUI` 按上一档阈值计算分段进度，打开 `ScoreRewardDialog`，调用成就领奖并刷新 | 积分累计、分段进度、完整奖励列表、循环模板、领取条件、防重复、入账与保存未接入；不能只显示254的库存 |
| 馈赠 | 原外壳 `Achievement` 标签；类型65、组15通过 `AchievementTaskPetGacha → AchievementTaskGachaBase` 统计结缘次数，6条原始模板含循环段，与积分类型66分开 | 馈赠列表、累计次数、循环奖励、领取状态、红点、返回与存档未接入 |
| 结缘详情 | `PetGachaSubPanel.GachaDetailInfoProvider → GachaDetailDialog`：概率预览／历史标签，按抽取类别设置标题、每日上限与规则说明 | 详情按钮、弹窗、滚动、奖池分组、物品详情、基础／综合概率及保底说明未接入 |
| 历史记录 | `RecordHistory=true`；`GachaHistoryContent.Init(Pet)` 按页查询，切页／关闭取消旧任务；`GachaHistoryItem` 显示物品名称、类型、本地时间及品质颜色。配置记录期180天、分类缓存1000条 | 抽取结果写入、按幻兽类别查询、分页、空态、关闭取消与重进恢复未接入；客户端RPC查询接口不能替代本地记录 |
| 材料购买与兑换 | 107的原快捷价为晨星（物品2）160／个、个人41级条件；商店7使用独立货架、三列卡片、品质背景、倒计时和确认弹窗 | 材料不足、差额购买、交易后继续抽取、结缘商店及库存刷新未接入；通用商城代码不等于此页面已接通 |
| UI、动画与音效 | `PetGachaPanel/SubPanel`、`PetGachaResultPerformer/RewardItemPerformer/ResultPanel`、`PetGetNewPanel`：原角色动作、击打／掉落／收集、单抽与十抽分支、百抽60个场景奖励后展示完整100结果、重复转换与新幻兽介绍 | 原层级／布局、角色原生动画、粒子／材质、保护时间、跳过与关闭清理、原音效及输入遮挡未接入；演出必须消费已提交奖励，不能重新抽奖 |

### 奖池、基础概率与保底

箱子6003按 `ChestInfo.GetRewardRules(openCount)` 在累计抽数小于10时取20401，之后取20400。两条规则基础权重相同，区别在紫色整件保底；均使用 `RecordSharedId="20400"`。批量结算必须逐抽推进，跨过第10抽时切换规则并保留同一记录。

| 奖励包／物品 | 基础权重／概率 | 包内候选／每次数量 | 常规20400的 `IndexMininum` |
| --- | --- | --- | --- |
| 53102 橙色幻兽整件 | 80／0.8% | 1603、1604、1605、1607、1608；1只 | 80 |
| 53100 紫色幻兽整件 | 500／5% | 1600、1601、1602；1只 | 30；前十抽20401为10 |
| 53201 橙色幻兽碎片 | 1500／15% | 1703、1704、1705、1707、1708；3片 | 0 |
| 53200 紫色幻兽碎片 | 900／9% | 1700、1701、1702；3片 | 0 |
| 53323 蓝色能力食材 | 5000／50% | 1530、1540、1550；3个 | 0 |
| 53324 紫色能力食材 | 1600／16% | 1531、1541、1551；1个 | 0 |
| 物品25 | 420／4.2% | 1个 | 10 |

这是七类奖励的基础概率，不是包含保底后的每次实际概率。包内 `WeightOfIdList` 均为100，但53102另有 `WeightScaleFactor=2`、`WeightScaleCountLimit=0`。`RandomRewardInfoParser.GetItemListByPackage` 可调用 `IRandomItemWeightAgent.InitTransformWeights`；已取得代码中有接口与消费入口，尚未定位该权重代理的具体实现及结缘调用方，不能断言橙色候选的最终动态概率始终均等，也不能猜测该字段的公式。

概率页 `GachaProbabilityContent` 通过 `RewardRuleInfoParser.GetRewardItemsProbabilitiesAndQuantitiesExpandRandomReward` 展开奖励包并归一化，合并同物品／同数量，再按品质与类型分组排序。`GachaProbabilityItem` 的综合概率来自独立表 `gacha_pity_probability`：紫／橙整件为6.37%／1.96%，紫／橙碎片为7.96%／13.27%，蓝／紫能力食材为44.24%／14.16%，其他为12.04%；是否显示还受原地区隐藏配置控制。这些展示值不能代替抽奖权重或反推保底公式。

共享代码已有 `RewardRandomGroup`、`StandardRandomGroup`、`RandomObj` 及 `PlayerRewardRecordDataWrap.RecordResult`：包括硬保底候选选择、排序、动态权重、未命中分数和保底触发记录。`RecordResult` 命中时对已有未命中分数采用 `max(旧值 - guaranteedCount + count×missScore, 0)`，不是无条件全部清零。尚未取得RPC40797的服务端结缘方法体，以及20400／20401到这些共享类的完整构造参数和调用链；须区分可复用算法与尚未确认的结缘绑定，不能把整个概率系统写成已恢复，或把已有共享算法误记为完全缺失。

积分组16前四档累计阈值为50／100／150／200，对应6950／6951／6950／6952各1，后续原始模板含50积分递增和循环配置。馈赠组15前三档10／20／30次，对应301×2／301×2／302×2，后续模板按10次递增并循环。实现须沿用 `AchievementRawInfoParser` 的循环展开、排序与领取记录，不把12／6条原始模板误当最终有限档数。固定版 `gacha_additional_awards` 为空；客户端 `GachaAddtionalAwardHandler` 只处理来源 `GachaAddition` 的额外奖励队列，不能凭这个类另造日常馈赠规则。

### 待迁移的规则与表现

以下要求均须在 Unity 中重新接入和验证；它们描述迁移目标，不构成当前实现结论。

原 `PetGachaPanel` 与 `PetGachaResultPerformer` 处理单抽四类角色动作、十抽击打／掉落／收集、百抽60个场景奖励后展示完整100个结果，保护时间、跳过等待、重复转换和首次介绍按原配置。

结缘页隐藏或销毁时按 `PetGachaResultPerformer.OnDisable` 清空等待队列和计时、释放进行中的奖励与特效、停止音效并关闭操作遮罩；重新显示时重放原默认角色与面板动画。常驻环境粒子保留原实例，随页面暂停并在恢复后继续。

演出消费已经提交的逐次奖励，不重新抽奖。百连通过原 `GachaPanelUtils.HundredGachePreRemind` 与 `MarkableConfirmDialog` 再次确认，取消不扣材料；“今日不再提示”按抽奖类型保存，在原每日重置点后重新提示，时钟回拨按原分支处理。主页面和结果页继续抽取共用此入口。百连要求107×100当前持有，不保存为永久解锁；结果继续抽取按剩余余额切换。材料快捷购买单价晨星160、原个人41级条件保留。

结缘页 `PetGachaSubPanel_Default／Show／Hide` 使用原透明度 Hermite 曲线和 `CanvasGroup.m_BlocksRaycasts` 离散轨道：隐藏立即停用子控件点击，显示结束后恢复。独立页与主页面嵌入副本都须接入，当前旧载荷中的两个副本不等于 Unity 已接通。共享 Hide 动画另有两个在原完整201节点层级中不存在的目标，按原引擎保留未绑定状态，不生成替代控件。

兑换使用原商店7独立货架、结缘外壳、三列卡片、品质背景、倒计时和确认弹窗。蓝色幻兽从原六候选选一，每周刷新，无手动刷新；同周开关／跨日保留库存，职业变化按原 `RefreshByProfession` 更新。

原结缘外壳、场景、结果和新幻兽介绍 prefab 中的 `UIBloomGroup`、`UIBloomPlayerElement`、`UIBloomGraphicElement` 归属路径及 `_BloomFactor` 粒子材质参数已随转换数据保留；原结缘 prefab 不含 `UIBloomRequirer`，迁移须沿用原组件层级。

### 依据与代码定位

官方客户端根目录 `.tools/source-packages/official-code/client-source/`，共享代码目录 `common-source/Common/`。具体恢复入口为上文所列类／方法；RPC40797声明和客户端回调在 `common-source/PlayerServiceProxy.cs`、`PlayerServiceProxyImpl.cs`，请求／响应为 `PlayerGachaRequest/Response`，历史为 `PlayerQueryGachaHistoryRequest/Response`。积分与馈赠另查 `AchievementTaskPetGachaScoreCount`、`AchievementTaskPetGacha`、`AchievementRawInfoParser`、`ScoreRewardUI/Dialog`。

固定版数据 [rules.json](../../assets/official/rules.json) 及[二进制表来源清单](../../assets/reference/official-city/rules-manifest.json)：`gacha`、`chest`、`reward_rule`、`random_reward`、`gacha_pity_probability`、`achievement`、`game_settings`。本地现状定位为 [HomeFeatureRuntime.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs)、[HomeHudView.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.cs)；主存档由 [OfflineMainlineProgressSession.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs) 管理，未接上述结缘状态。

[演出来源](../../assets/reference/pet-gacha-performance/source-manifest.json)与载荷 [performance.json](../../assets/official/pet-gacha-performance/performance.json) 保留原角色、16个特效 prefab、10组 playable、音频映射和结缘／结果／新幻兽等7类界面；[兑换来源](../../assets/reference/pet-exchange/source-manifest.json)与载荷 [exchange.json](../../assets/official/pet-exchange/exchange.json) 保留独立外壳、货架、图标及购买音效。共享详情／历史／积分框层级见 [ui.json](../../assets/official/ui.json) 的 `gacha_rules`、`gacha_history`、`gacha_history_row`、`gacha_score`、`gacha_score_item`。快捷购买来源见[购买清单](../../assets/reference/gacha-purchase/source-manifest.json)。这些是恢复输入，仍须完成原生资产、脚本、shader、动态加载清单及 Windows 运行绑定。

### 验证范围与完成条件

已从固定版 `.tools/source-packages/official-data/*.g.bytes` 重新解码并核对 `gacha`、`chest`、`reward_rule`、`random_reward`、`gacha_pity_probability`、`achievement`、`furniture` 七表及原始字节SHA256，与现有 `rules.json` 一致。演出来源清单128项输出、兑换70项输出均存在且哈希一致；这不证明 Unity 导入或运行通过。

`python -B scripts/check_unity_project.py` 已通过，检查23936个GUID的项目元数据；所属页结构、链接与差异检查通过。这些静态结果不证明结缘玩法或画面已恢复。

未执行本模块 Editor 导入／编译、Play Mode 逻辑、真实输入、画面／动画／音频、存档恢复或 Windows Player 验证。既有编辑器实例在执行其他模块任务，未干预它们，也未以直接调用奖励回调代替结缘流程验收。

后续须按[实际运行验证要求](../asset-management.md#实际运行验证方法与要求)，从真实入口覆盖单／十／百抽、107持有99／100边界、前十抽规则切换与共享保底、每日限制、材料不足／购买、积分与馈赠循环领奖、详情与历史翻页／空态、演出跳过／关闭／重进、重复幻兽转换、结果继续抽取，以及同一事务失败回滚、重启恢复和独立特效随机源。
