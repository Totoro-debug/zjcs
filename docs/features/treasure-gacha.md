# 古遗物扭蛋与女神牌

[总索引](../README.md)

状态：未完成

## 功能简介

目标入口为家园时光扭蛋机、古遗物收藏跳转及女神牌快捷购买，覆盖四类宝箱、五元素奖池、扭蛋积分、馈赠、详情／概率、历史记录、图鉴、商店以及抽取演出。

**当前尚未完整迁移还原。** Unity 家园已识别扭蛋机，但 `HomeFeatureRuntime.OpenFurniture` 仅为床、小推车、邮箱打开业务页，扭蛋机类型 `9` 进入 `HomeHudView.ShowFurniturePlaceholder`。未接通本功能的抽取服务、页面控制器及演出播放器。

原机台18级且地图11开放时出现损坏模型／30级提示，30级免费修复家具4007，维修通知、0.2秒换模型和1秒结束效果仍属于待还原目标。家园家具状态的部分接入不代表机内功能已经实现。

## 完整还原部分

尚无本功能在 Unity 完整接入并运行验收的结论。原代码、配置、已转换素材和旧引擎测试仅是迁移依据，不作为本功能已还原的证据。

### 已取得的官方依据及当前接入边界

- 固定版 `854_156383` APK 中的 `gacha/chest/treasure_gacha/reward_rule/random_reward/gacha_pity_probability/achievement/game_settings/item/conditions` 已用原 Luban 构造器重新解码，10张表均与 [rules.json](../../assets/official/rules.json) 一致。
- 已实际读取原 `ChestGachaPanel`、`ChestGachaResultPanel`、`ChestGachaAchievementPanel`、`ChestGachaDetailDialog`、`ChestMapSwitchPanel` 及共享 `GachaDetailDialog/GachaHistoryContent/GachaHistoryItem/ScoreRewardDialog` prefab。元素选择由主页面内的 `ElementChestSelectDialog` 组件控制，不能因未找到同名独立 prefab 就判为源资源缺失。
- 原扭蛋 UI 包 `defaultpackage_assets_prefabs_ui_chestgacha.bundle`、共享详情包 `defaultpackage_assets_prefabs_ui_gachapanel.bundle` 及配置包 `defaultpackage_assets_scriptableconfig_modules_treasure.bundle` 经 `prepare_unity_bundle_inputs.py` 核对：包头 `2022.3.57f1`、目标 Android，361个递归 bundle／CAB 来源完整。这只证明转换输入可用，未证明 Windows 原生脚本、shader 或运行表现。
- [relic_draw.json](../../assets/official/relic_draw.json)、[relic_navigation.json](../../assets/official/relic_navigation.json)、[relic-performance/performance.json](../../assets/official/relic-performance/performance.json) 及 `meshes.json` 是既有转换载荷；演出194个资源文件存在且与所属来源清单的 SHA-256 一致，导航和演出数据输出哈希也一致。`res://` 引用与旧引擎适配不等于 Unity 的加载绑定，当前未找到它们在 Unity 业务代码中的消费者。
- `OfflineMainlineProgressSession.cs` 的 `OfflinePlayerProgress` 有库存和库存随机状态，`OfflineWorldProgress` 有通用开箱次数，但未接入扭蛋选择／确认、共享保底记录、每日计数、积分／馈赠领奖进度和分类历史的完整持久化事务。通用库存与开箱计数不能代替这些状态。

### 实际验证范围

已执行官方表解码比对、原 prefab／序列化字段读取、递归 CAB 来源检查、既有载荷哈希核验及 `python -B scripts/check_unity_project.py`；静态元数据检查通过。未执行本功能的 Editor 导入／编译验收、Play Mode 逻辑、真实输入、画面／动画／音频、存档恢复或 Windows Player 验证；现有占位入口与缺失业务不能记为这些流程通过。本次仅检查并更新文档，没有新增或修复游戏功能。

## 自定义实现

以下为已确认的单机处理要求，尚未代表完整 Unity 流程已经接入或验收。

缺少服务端免费自动修复回调时，已拥有家具，或满足原30级／地图11免费修复资格即可访问收藏跳转、购买和扭蛋子页；只读判断，不伪写所有权。

本地按原可见奖池、权重、范围和保底结算，随机、材料、积分、历史共同提交，不声称取得完整服务端随机序列。

原 `Common.Player.TreasureGacha`、`ElementTreasureGacha` 在可取得共享代码中只检查箱子配置、调用 `RecordOpenChest` 并返回空奖励列表。客户端 `PreciousGachaChestNotGoodsStrategy.OpenChest`、`ElementGachaChest.SendGacha` 请求 `OperPlayerGacha`，结果由奖励消息消费；直接移入上述空奖励函数不能完成离线抽取。已确认的本地结算方向不自动确定多条保底同时触发时的优先级、命中后各计数重置关系等未取得的服务端裁决细节；实现这些分支前须继续核对依据，仍无依据时明确请求规则决定，不从综合概率反推公式。

## 尚未实现

以下均为从官方代码和配置核对的目标，尚未在 Unity 接通。

| 功能 | 官方规则／调用关系 | 当前缺项 |
| --- | --- | --- |
| 宝箱扭蛋 | `PreciousChestPanel/PreciousChests/PreciousGachaChest`：蓝紫橙金 `GachaType=4～7`，箱子 `6201～6204`，女神牌 `301～304` 每抽10张；系列来自 `TreasureGachaInfoParser.ForeachSortedInfos`，固定版17组，按条件与排序选择；切换刷新背景、收藏进度、图鉴、保底与顶栏 | 页面、系列选择／确认保存、箱子推荐／切换、费用与真实奖励未接入 |
| 元素扭蛋 | `ElementChestPanel/ElementChestSelectDialog/ElementGachaChest`：火水风光暗 `GachaType=8～12`、箱子 `6300～6304`，均消耗 `311×10`；选择后确认生效，取消只播放收起动画；奖池按元素、品质与开放条件过滤 | 五元素页面、选择／取消、条件过滤、抽取与奖励未接入 |
| 抽取次数与限制 | `ChestGachaDrawButton/GachaChestBase`：单抽固定1，多抽按材料动态为2～10，百抽仅可抽满100次时出现；每抽10张，1000张显示百抽、999张不显示；箱子允许 `1～10/100`；共用古遗物每日上限10000与百抽确认 | 动态按钮、限额提示、百抽确认、材料不足处理与再次抽取未接入；不能只实现固定十抽 |
| 扭蛋积分 | `PreciousChestPanel.Awake/GetScoreRuleText`、`ScoreRewardUI/ScoreRewardDialog`：积分物品250，蓝紫橙金每抽分别给1／2／3／5；成就类型13读取 `PreciousChestScoreCount` 黑板累计，配置组12有60条原始模板，含循环段；按上一领奖阈值计算本段进度并防重复领取 | 累计、分段进度、循环模板、奖励列表、领取与保存未接入；元素 `RewardScoreId=0` 且无箱子固定奖励，不增加宝箱积分 |
| 馈赠 | 原 `ChestGachaAchievementPanel` 的 `AchievementPanelWithTypes/AchievementToggleType` 两个有效标签为类型88／8；前者累计四类宝箱开箱数，后者读取 `OpenElementChestCount` 黑板；两类各10条原始模板，均含循环段，与积分奖励分开 | 宝箱／元素馈赠切页、累计进度、循环奖励、领取状态与重复领取保护未接入 |
| 详情与概率 | `ChestGachaInfoBoard.OnRuleBtnClick → GachaDetailDialog.Init`，带入抽取类型和系列ID；概率／历史双标签，说明与每日上限随类别刷新；`GachaProbabilityContent` 展开奖励包、过滤后归一化、合并同物品同数量，再按品质／类型排序；`GachaProbabilityItem` 分开显示基础与综合概率并服从地区隐藏配置 | 弹窗、规则文字、分组列表、物品详情、概率与保底提示未接入 |
| 历史记录 | `GachaDetailDialog/GachaHistoryContent/Item`：四类宝箱分别查询，五元素调用 `InitElementGacha` 合并为 `ElementTreasure` 类别；原 prefab 每页10条，配置记录期180天、分类缓存1000条；显示名称、类型、品质颜色及本地时间 | 写入、分类／分页、时间／颜色、查询取消、空态、重进恢复与保存未接入；不能拆成五份互不相通的元素历史 |
| UI与辅助入口 | 原 `ChestGachaPanel` 四底栏为宝箱／元素／馈赠／商店；另有系列选择、图鉴跳转、积分详情、女神牌快捷购买和结果页 | 官方层级、锚点、尺寸、遮挡、滚动、红点、返回与入口路由未完整恢复；商城或背包有物品不等于扭蛋已实现 |
| 动画／音效 | `GachaponMachineView/TreasureGachaPlayable/ChestGachaAwardPanel` 及 `ChestGachaAwardShowOneHandler/LessTenHandler/GreaterTenHandler`：机台双骨骼层、十胶囊摇动／飞出、五品质与六种元素外观、开蛋闪光、卡片、新获得／重复转碎片；单抽、2～10抽及百抽采用不同结果编排，百抽按可见行开蛋与自动滚动，跳过受阶段限制 | Unity 原生动画、网格／材质／特效、原音效、结果按钮、关闭与中断清理未接入，旧载荷存在不代表已播放 |

### 奖池、基础概率与保底

`reward_rule` 的基础权重归一化后如下；这是包级基础概率，不包含首次规则、保底修正，也不是每一件具体古遗物的最终概率。

| 奖池／规则 | 整件基础概率 | 碎片基础概率／数量 | `IndexMininum`／`RecordSharedId` |
| --- | --- | --- | --- |
| 蓝 `20101` | 蓝5%、紫0.9%、橙0.1% | 蓝80%、紫12%、橙2%，每次3片 | `[10,250,2000,0,0,0]`／`20101` |
| 紫 `20102` | 紫5%、橙0.9%、金0.1% | 紫76%、橙15%、金3%，每次5片 | `[10,250,2000,0,0,0]`／`20102` |
| 橙 `20103` | 橙12%、金1% | 橙72%、金15%，每次5片 | `[10,250,0,0]`／`20103` |
| 金 `20104` | 金15% | 金85%，每次5片 | `[10,0]`／`20104` |
| 五元素 `20300～20304` | 紫85%、橙12%、金3% | 无 | `[0,10,40]`／五元素均为 `20300` |

四类宝箱的 `ChestInfo.GetRewardRules(openCount)` 在累计开箱次数小于1时改用 `20107～20110`，其首项权重为100，其余为0，分别给蓝／紫／橙／金整件；之后才取常规规则。批量抽取须逐抽推进次数与保底，不能整批只读取一次首次规则，也不能将这条首次规则误作额外加送一次。

`RandomItemRangeTreasureGachaSelector` 先按开放系列、品质、元素及整件／碎片类型构造范围；候选默认权重100，重复物品去重，范围为空才尝试 `DefaultGachaIds=[11]`。宝箱概率页还使用 `ItemFilterForTreasureGacha(GachaId)` 限定所选系列，碎片转回整件ID再判归属；元素选择器本身限定元素，固定配置 `IsNeedMet=true`，不能把所有未开放地图物品混入奖池。固定版 `IsElementGachaBlockInactiveMap=true`，仍须核对其本地结算消费。

`gacha_pity_probability` 是概率页的综合概率展示表，例如元素整件紫／橙／金 `7911/1663/426` 经 `Formula.Long2Double` 得 `79.11%/16.63%/4.26%`，不是每抽基础权重；`GachaProbabilityItem.SetPityProbability` 还会按地区配置隐藏。实际保底状态涉及 `RewardRuleInfo.RecordSharedId` 与奖励记录，五元素切换须共用 `20300`，四类宝箱分别使用各自记录。原 `PlayerRandomRewardPreviewerUI` 根据记录的未命中分数显示剩余次数，机内胶囊品质装饰分布不能代替真实抽奖概率。

### 材料购买、资源恢复与运行验收

稀有女神牌301原快捷价晨星8／张，无次数限制，只补实际差额；302～304与311无无限快捷购买记录，不把物品估值当售价。商店6原表无商品，显示原空状态。此处沿用既有材料方案，不因离线迁移添加原表未配置的购买或积分。

后续须恢复官方 Unity 脚本、原 prefab 层级、原生动画／shader 和动态资源清单，将随机、扣材、积分、保底、每日计数、真实奖励及历史放入同一本地事务，完成重复领取保护与失败回滚。演出按原序列化时序与阶段实现，跳过不能跳掉结算或留下失效回调；角色／机台 Spine 数据转换遵循[既定免费迁移路线](../asset-management.md#spine-角色动画迁移决策)。

完成后按[实际运行验证方法与要求](../asset-management.md#实际运行验证方法与要求)从玩家入口验证：四档宝箱和五元素、2～10抽与100抽、999／1000张边界、首次／常规／保底、系列与元素切换、积分／馈赠循环领奖、详情／历史翻页、跳过与返回、奖励到账及保存恢复。需使用真实鼠标／滚动操作并查看运行画面和动画；静态检查及旧引擎测试不能代替这些结果。

### 依据与代码定位

官方客户端根目录为 `.tools/source-packages/official-code/client-source/`，共享代码为 `.tools/source-packages/official-code/common-source/Common/`。上文的类与方法为具体恢复入口；接口另见 `PlayerGachaRequest/GachaTreasureParam/PlayerQueryGachaHistoryRequest/PlayerQueryElementGachaHistoryRequest`。

当前 Unity 入口与状态定位：[HomeFeatureRuntime.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs)、[HomeHudView.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.cs)、[OfflineMainlineProgressSession.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs)。

来源及转换依据：[扭蛋页面与机台](../../assets/reference/relic-draw/source-manifest.json)、[系列选择](../../assets/reference/relic-navigation/source-manifest.json)、[演出与音效](../../assets/reference/relic-performance/source-manifest.json)、[百抽字段](../../assets/reference/hundred-draws/source-audit.json)、[快捷购买](../../assets/reference/gacha-purchase/source-manifest.json)。现存导航转换工具为 [prepare_relic_navigation.py](../../scripts/prepare_relic_navigation.py)，原始包与 CAB 核验使用 [prepare_unity_bundle_inputs.py](../../scripts/prepare_unity_bundle_inputs.py)；这些工具不证明玩法已恢复。
