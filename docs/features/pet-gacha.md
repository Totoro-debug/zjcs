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

### 原始官方功能与逐项待实现清单

下表记录已取得官方来源中的原始功能及对应 Unity 工作，不把官方目标、已确认单机差异与未取得的服务端决策合并为“已实现”。奖池数值和公式边界以上节为准；苹果生产与领取由[果树页](fruit-tree.md#原始官方功能与逐项待实现清单)维护。

| 原始官方功能 | 官方依据与行为 | 待实现内容 |
| --- | --- | --- |
| 家园、幻兽页与定向导航 | 果树子设施路由与 `PetGachaNavigateHandler`；导航检查战斗／世界状态、功能开关，并根据普通树／实际活动可用性调整目标标签，再经 `PopupWindowInHome` 进入 | 真实按钮、条件反馈、返回家园与指定标签打开；不能把“宠物树未修”简化为所有活动入口一律不可用 |
| 外壳标签与返回 | `PetGachaPanel` 的结缘、Achievement馈赠、Shop、ActivityPetGachaPanel；启用时选初始化标签、绑定Escape、刷新树解锁状态，禁用时释放订阅 | 原标签布局、显示状态、返回键、顶栏与页面生命周期；打开商店7时清除其“新功能”标记 |
| 活动结缘入口 | `RefreshActivityPetTab` 仅在 `HasPetTreeGachaActivity` 为真时显示，加载真实首个活动ID并绑定该实例红点 | 保留官方活动加载／导航接口；离线不伪造活动实例、免费次数、奖励或红点；活动玩法本体按[活动小游戏](activity-minigames.md)所属范围处理 |
| 材料、花费与批量按钮 | 107、箱子6003、每抽1个；单抽、固定十抽、持有量足100时的百抽按钮模式，主页面与结果页各自刷新 | 货币图标／数量、费用文本、107余额0／1／9／10／99／100的状态、余额变化事件、原价格显示差异 |
| 百抽二次确认 | `GachaPanelUtils.HundredGachePreRemind`、`MarkableConfirmDialog`、按类别保存的今日忽略标记 | 确认／取消、每日重置、时钟回拨原分支、主页面及结果页共享入口；取消不得先扣材 |
| 材料不足与快捷购买 | `DoGachaItemNotEnough`、107快捷购买、`CostItemConfirmDialog`；晨星2×160／个，个人41级条件 | 差额数量、购买条件、余额不足、确认／取消、购买后继续抽取与失败恢复；不能仅提示材料不足 |
| 抽取请求与提交 | `PetGachaSubPanel.GachaSend` 在演出允许及每日次数允许后发RPC40797；奖励通过原事件／来源路由交给演出 | 本地逐抽服务、重复请求保护、奖励发放、材料与全部派生状态原子保存；结果已提交后才开始演出 |
| 每日上限与显示刷新 | `GachaTodayCountLimitUI` 按类别显示当前／上限；每日幻兽上限10000；原 `OnGacha` 与演出结果刷新 | 每日计数及重置点、超过剩余次数的原拒绝分支、页面／结果／详情一致显示；与累计抽数分开保存 |
| 奖励抽样与保底 | 20401前十抽、20400常规、共享记录20400；七类基础权重、包内候选、硬保底及共享随机算法 | 复用可确定的算法并补全结缘构造链，逐抽更新记录、跨规则切换与批量等价；动态权重缺口见下表 |
| 新幻兽与重复转换 | `AwardData.AwardId/Amount/AgiotageId/Agiotage` 区分原奖励及转换结果；整件Pet且 `AgiotageId==0` 才触发首次介绍 | 新幻兽实际入库与幻兽系统联动；同批重复按逐抽状态处理，保存原物品及转换结果；转换决策未确认前不得只凭演出字段猜数量 |
| 积分获取与主页面进度 | 254×1／抽；`AchievementTaskPetGachaScoreCount` 读取 `PetGachaScoreCount`，`ScoreRewardUI` 按上一档阈值显示当前分段 | 积分黑板累计、奖励预览、可领／已领状态、下一档进度、领奖后刷新；库存254与累计成就值分别处理 |
| 积分详情与循环领奖 | `ScoreRewardDialog → AchievementPanel.SetDataKey`，类型66／组16，原始16001～16012及循环参数 | 完整可滚动奖励表、领取条件与奖励到账、稳定的组／索引领取标识、循环展开和防重复领取 |
| 馈赠次数与循环领奖 | `AchievementTaskPetGacha`、类型65／组15，15001～15006；奖励与计数独立于积分组16 | 原馈赠标签、列表、累计次数、已领／可领／未达状态、红点、循环展开及保存；不与空表 `gacha_additional_awards` 混用 |
| 详情外壳与规则说明 | `GachaDetailDialog` 设置类别标题／描述／每日次数，配置允许时显示历史标签 | 详情入口、原弹窗、概率／历史切换、滚动、关闭与遮挡、原本地化规则文案和保底说明 |
| 概率列表与物品详情 | `GachaProbabilityContent/Item` 展开包、归一化、合并同物品／数量、品质／类型排序；综合概率独立读取 `gacha_pity_probability` | 类别分组、基础／综合概率、数量、百分比格式、地区显隐和点击物品说明；不把展示值用作抽样权重 |
| 历史记录查询与分页 | `GachaHistoryContent` 按Pet分类查询并取消过期请求；`GachaHistoryItem` 显示物品名、类型、本地时间及品质颜色 | 本地查询、页码／上一页／下一页、空态、记录期180天与分类缓存1000条的分别处理；关闭／切页后不让旧结果覆盖新页 |
| 单抽、十抽与百抽演出调度 | `PetGachaResultPerformer` 消费已返回奖励，角色动作／等待队列／计时／操作遮罩；百抽场景只演60个，完整结果仍保留100个 | 原角色动作和击打时序、保护时间、场景奖励队列、跳过、停止音效、禁用清理与重开默认动画；演出数量不等于发奖数量 |
| 掉落、展示与收集 | `PetGachaRewardItemPerformer` 的 Drop→DropEnd→Showing→ShowEnd→Collect；单／十抽延迟、品质特效、特殊缩放、掉落／收集Playable、淡出收集分支 | 原挂点轨迹、Trail排序、延迟与回调、展示音效、快进掉落、收集目标、池化回收与资源释放 |
| 重复奖励转换演出 | 场景奖励延迟后按 `AgiotageId/Agiotage` 换图标，播放转换特效／音效；`PetGachaResultItem` 先显示原物品再 `RefreshTransition` | 场景和结果卡片同时接入原／转换奖励；取消异步任务、跳过后直接显示最终转换态，避免二次发奖 |
| 百抽结果列表与继续结缘 | `PetGachaResultPanel` 使用循环滚动列表、`ListItemShowSequence` 逐项展示并自动滚动；结果按钮重新按余额选择十／百模式 | 100条完整结果、原排列和卡片动画、自动滚动、继续单／十／百抽、关闭结果页后共用原抽取入口 |
| 结果跳过与新幻兽插播 | `OnItemShow` 遇新幻兽暂停序列／滚动，延迟打开介绍，关闭后续播；`OnSkip → TryShowAllNewPet` 仍展示尚未介绍的新幻兽 | 逐项展示、暂停／恢复、跳过全部结果后的新幻兽队列和防重复介绍；“跳过”不能丢结果或吞掉新幻兽页 |
| 新幻兽介绍与技能交互 | `PetGetNewPanel` 显示原模型、品质状态、名字、类型图标、技能图标／名称／说明及超链接；默认幼年期取支援技能，其他阶段取实体技能 | 原出场动画、保护与跳过、关闭按钮时机、技能选择／超链接、模型池借还、页面取消任务和资源释放 |
| 结缘兑换商店 | 商店7的原外壳、货架、三列商品、品质背景、售价、库存、周刷新倒计时、购买确认；蓝色幻兽六选一及职业刷新 | 实际商品生成、库存与周记录、购买扣款／入账／刷新、原购买音效、跨日／跨周／重进和职业变化；无手动刷新 |
| UI动画、材质与输入 | 原独立／嵌入页面、Default／Show／Hide曲线、CanvasGroup点击轨道、原Bloom归属与粒子参数；详见下节 | 全部 prefab 层级／锚点／尺寸／排序、模型／网格／粒子／shader、音频和动态资源清单；检查遮罩、缩放及真实鼠标命中 |
| 提示、红点与状态同步 | 原积分／馈赠领奖状态、商店新功能标记、实际活动实例红点与材料／抽取事件分别驱动对应UI | 按原组件绑定接入，切标签和领奖／购买后刷新；不借用其他抽奖类型的通用红点阈值冒充幻兽规则 |

积分循环原始16004的 `CycleCount=25000`，后接16005～16012八个增量模板，每次增加50，奖励依次为6950／6951／6950／6951／6950／6951／6950／6952各1。馈赠15003的 `CycleCount=25000`，后接15004～15006三个增量模板，每次增加10，奖励依次为301×2／301×2／302×2。`AchievementRawInfoParser.GenerateAchievementInfosCount` 按独立项、循环起始项与增量模板数算出积分200004档、馈赠75003档，`CycleTemplateInfo.GetRawInfoByIndex` 循环选取模板；必须沿用生成索引及上界，不写成只有12／6档或无限奖励。

### 本地状态与尚缺的官方决策

| 状态／决策 | 已知依据 | 尚待实现或核实 |
| --- | --- | --- |
| 奖励事务存档 | 已确认采用本地随机与共同提交 | 107／晨星余额、奖励与幻兽、累计／每日次数、积分黑板、共享保底、历史、重复转换、随机序列共同保存；失败全部回滚，环境随机独立 |
| 页面与领奖存档 | 原按类型每日忽略标记、成就组／索引、商店库存 | 积分／馈赠领取记录、今日百抽提示、周货架／购买次数及版本迁移；UI对象池不进入规则存档 |
| 结缘随机算法绑定 | 共享算法、规则20400／20401和请求／响应已取得 | RPC40797服务端方法体及其共享随机类构造链未取得，保底候选冲突等行为须沿官方参数和分支核实，不能仅由概率表反推 |
| 橙色包动态权重 | 53102含 `WeightScaleFactor=2`、`WeightScaleCountLimit=0`，存在 `IRandomItemWeightAgent` 消费入口 | 具体代理实现与结缘调用未定位，不能擅自填入“重复降权”“未拥有加权”或始终等概率公式 |
| 重复幻兽的转换决策 | 客户端完整消费 `AgiotageId/Agiotage` 并演出转换 | 结缘服务端何时生成转换、具体物品／数量及同批重复处理须继续查配置与发奖调用方；已知展示分支不等于已经掌握决策规则 |
| 时间与记录清理 | 官方每日限制／提示重置、周货架、180天历史期与1000条分类缓存 | 本地重置及重启恢复尚未接入；客户端缓存上限不可直接当服务器历史总上限，异常时间处理不确定部分需明确后实现 |

### 规则与表现恢复要求

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
