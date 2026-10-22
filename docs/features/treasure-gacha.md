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

本地按原可见奖池、权重、范围和保底结算，随机、材料、积分、历史共同提交，不声称取得完整服务端随机序列。尚未确认的规则边界另列于下文，不据此增加商品、改变解锁条件或推定服务端公式。

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

### 原始官方入口、页面及选择行为

以下记录固定版客户端实际分支；全部属于 Unity 待接入范围。

| 场景 | 原始官方功能与代码定位 |
| --- | --- |
| 访问与导航 | `ChestGachaPanelNavigateHandler.CanNavigate` 先检查战斗／世界状态、家具所有权及指定系列条件；不满足时返回对应提示。`Navigate` 无参数打开宝箱页，指定系列携带 `SourceItemId`，元素导航未指定元素时回退光；通过 `PopupWindowInHome` 处理离开地图、家园 UI 根与窗口操作。单机家具资格例外只按上文已确认方案处理。 |
| 物品来源跳转 | `ClientItemSourcePreciousChestGachaHandler` 将碎片还原为整件，按排序选第一个包含该物品且已开放的系列；无匹配则打开普通入口。元素来源由 `ClientItemSourceElementChestGachaHandler` 传递元素。保留来源物品及窗口上下文，不仅打开一个通用空页。 |
| 宝箱默认系列 | `PreciousChestPanel.GetSelection` 优先最后一个已开放且尚未确认 NEW 的系列；否则使用能解析的已存选择，再回退最后一个已开放系列。该存量选择分支本身没有再次校验条件，实际抽取前仍有条件检查。系统推荐、用户来源导航、用户手动确认分别携带原 `TreasureGachaSelectOperType`，不能混成同一种操作。 |
| 宝箱切换 | `PreciousChests.GetRecommandGachaType` 根据当前选择、默认项和序列化顺序寻找材料可抽项；抽取结束重新推荐或刷新，不臆定固定优先金色。`ChestGachaSwitchItem` 显示材料／需求、进度、可抽状态，选中缩放1.15，其余为1。 |
| 宝箱页联动 | 系列／箱子变化同步标题、副标题、前后景、机台、胶囊、费用按钮、卡牌与晨星顶栏、保底预览、收藏数量／总量及 `CollectBookDataTreasure.ChestGachaId` 图鉴入口。库存增加与抽取完成均触发对应刷新。 |
| 系列选择列表 | `ChestMapSwitchPanel.Refresh` 按配置顺序列出所有已开放系列与第一个未开放系列；全部开放时补一个锁定占位，初始滚动到当前选择。行状态为已开放／预告／锁定，展示背景、标题、收藏总进度、分品质进度和 NEW；点击锁定行给原提示，点击开放行先改弹窗内选中态。 |
| 确认与关闭的区别 | 点击确认且选择改变时发布事件更新主页面、写入选择、发送 `OperateByUserSelect` 并提示切换成功。另一个独立分支 `OnDisableAndInited → ConfirmNewTreasureGacha` 在窗口停用时也写入弹窗选择，并把当前所有开放系列写入 `TreasureGachaConfirmed`、调用确认 NEW 接口。因此不能把系列弹窗关闭概括为“取消且不保存”；应逐项保留主页面事件和本地确认记录的不同触发点。 |
| 系列预告 | `ChestMapSwitchPanelItem` 根据阶位开放基准时间、`ServerRankUnlockDaysCondition.Days` 与 `PreviewDuration` 小时进入预告，行倒计时每0.1秒刷新；主面板预告每0.5秒刷新。未达到预告时间仍锁定，不直接展示下一池为可抽。 |
| 元素页初态 | `ElementChestPanel.Init` 未指定元素时为未选择状态并打开选择器；指定元素时直接初始化对应箱子。已选元素在当前页面实例切页时保留，未找到据此证明跨会话持久化的代码。 |
| 元素选择弹层 | `ElementChestSelectDialog` 高亮当前元素，未选择的初态与已有选择的确认／取消按钮状态不同；勾选后确认才改变 mediator 的元素与抽取类型。同元素确认有原提示；取消仅收起动画，不改变已选元素。需还原五项图标、切换动画、按钮命中和遮挡。 |
| 共享 mediator | `ChestGachaMediator` 分发抽取类型、箱子、系列与底栏变化；抽取结束对特殊订阅者调用 `OnGachaEnd`，其余刷新；离开时重置元素／系列／箱子并注销事件，避免重复订阅和旧回调。 |

### 原始官方奖池与开放配置

抽取类型、材料、奖励来源及规则必须一一对应。下表每抽均消耗10张对应卡；类型4～12都记录历史，并共用 `GachaClassType=2` 的每日计数。积分奖励是箱子的固定奖励，不是随机包中的额外整件。

| 页面／品质 | `GachaType` | 箱子 | 卡牌 | 常规随机规则 | 每抽积分250 | `SourceOperate` |
| --- | --- | --- | --- | --- | --- | --- |
| 宝箱／蓝 | 4 | 6201 | 301 | 20101 | 1 | 170 |
| 宝箱／紫 | 5 | 6202 | 302 | 20102 | 2 | 171 |
| 宝箱／橙 | 6 | 6203 | 303 | 20103 | 3 | 172 |
| 宝箱／金 | 7 | 6204 | 304 | 20104 | 5 | 173 |
| 元素／火 | 8 | 6300 | 311 | 20304 | 0 | 174 |
| 元素／水 | 9 | 6301 | 311 | 20303 | 0 | 175 |
| 元素／风 | 10 | 6302 | 311 | 20302 | 0 | 176 |
| 元素／光 | 11 | 6303 | 311 | 20301 | 0 | 177 |
| 元素／暗 | 12 | 6304 | 311 | 20300 | 0 | 178 |

固定版17个系列的完整物品 ID 列表在 `treasure_gacha.ChestGachaItems`，不能凭地图重建替代。下表的“项数”是该原始列表长度，图鉴进度须继续使用原统计函数；条件 ID 指 `conditions.ClassId`。“阶位N＋D天”指 `ServerRankUnlockDaysCondition(Rank=N, Days=D, IsOnlyRank=false, Op=>=, UseMergeActivityBoost=false)`，不是角色达到阶位后自行开始计时。

| 系列ID／顺序 | 地图 | 原列表项数 | 原开放条件 | 预告提前小时 |
| --- | --- | --- | --- | --- |
| 11／1 | 11 | 70 | 地图11开放 | 0 |
| 21／2 | 12 | 70 | 地图12开放 | 0 |
| 31／3 | 13 | 70 | 地图13开放 | 0 |
| 41／4 | 14 | 80 | 地图14开放 | 0 |
| 42／5 | 14 | 80 | 条件114与13531：地图14＋阶位5／31天 | 192 |
| 51／6 | 17 | 80 | 地图17开放 | 0 |
| 52／7 | 17 | 80 | 条件118与14030：地图17＋阶位6／30天 | 168 |
| 53／8 | 17 | 80 | 条件118与14060：地图17＋阶位6／60天 | 168 |
| 61／9 | 19 | 80 | 地图19开放 | 0 |
| 62／10 | 19 | 80 | 条件120与14530：地图19＋阶位7／30天 | 168 |
| 63／11 | 19 | 80 | 条件120与14560：地图19＋阶位7／60天 | 168 |
| 71／12 | 21 | 80 | 地图21开放 | 0 |
| 72／13 | 21 | 80 | 条件123与15030：地图21＋阶位8／30天 | 168 |
| 73／14 | 21 | 80 | 条件123与15060：地图21＋阶位8／60天 | 168 |
| 81／15 | 23 | 80 | 地图23开放 | 0 |
| 82／16 | 23 | 80 | 条件127与15330：地图23＋阶位9／30天 | 168 |
| 83／17 | 23 | 80 | 条件127与15360：地图23＋阶位9／60天 | 168 |

### 原始官方次数、费用与红点

`ChestGachaDrawButton` 的原 prefab 为单抽固定1、多抽动态最少2最多10、百抽动态最少100最多100；箱子 `OpenCounts=[1,2,3,4,5,6,7,8,9,10,100]`，`PreciousChestMaxOpenCount=100`。`GachaChestBase.GetMaxOpenCount` 按持有材料整除单次成本并取按钮上限；`PreciousGachaChest` 只有在当前材料不能抽时才取购买策略的补位次数，原可购买策略返回10，不将晨星余额直接混成可百抽材料。每日剩余数单独记录，发送前以“已抽＋本次数”校验10000上限；按钮可显示不代表请求可越过日限。

`GachaPanelUtils.HundredGachePreRemind` 对100次及以上显示带“今日不再提示”的 `MarkableConfirmDialog`；取消不发送抽取，勾选后按各 `GachaType` 保存 `GachaBaseSetting.LastIgnoreRemindHundredTickDict` 时间戳并异步落盘，隔日按原时间比较重新提示。宝箱发送前另检查所选系列存在且条件满足；不满足显示 `ui_current_map_has_no_treasure`，不扣材料、不产生结果。

红点包括宝箱总入口、元素总入口、具体抽取按钮、新系列和可领奖状态。`PreciousChestCanOpenRedDotItem/TipDotPreciousChestCanOpenConditionNode` 按配置的类型集合或单类型、`OpenCount`、实际材料与每日上限判断；按钮节点另服从忽略箱子、固定／动态次数。元素节点检查五元素所用卡311；`ShowRedDotMinCount=10` 应按其调用处消费，不一律解释为“有一张就亮”。`TipDotNewChestGachaCanSwitchConditionNode` 在任一开放系列未记入 `TreasureGachaConfirmed` 时提示。库存、抽取、条件、黑板和领奖变化须刷新，销毁时解除订阅。

### 原始官方积分与馈赠奖励

| 类别 | 原配置与累计来源 | 奖励模板与循环 |
| --- | --- | --- |
| 扭蛋积分 | 类型13、组12；累计黑板 `PreciousChestScoreCount`，不以背包现存积分余额替代累计；蓝紫橙金分别每抽1／2／3／5，元素不产生此积分 | 原始ID12001～12060。12001～12030阈值5、10…150；每项给对应卡5张：第12／24项为311，第30项304，其余逢3为303，否则302。12031～12060的 `CompleteParams=null`、增量每项5，卡牌序列同前30项；12030的 `CycleCount=5000`。须通过 `AchievementRawInfoParser` 展开，不能将60条模板直接当60个最终阶段或自行写无限循环。 |
| 宝箱馈赠 | 类型88、组11；合计四类宝箱开箱数，与积分累计独立 | 原始ID11001～11010；首段阈值10、20、30、40、50，每项物品2×15；后5项增量各10，11005的 `CycleCount=20000`。 |
| 元素馈赠 | 类型8、组13；累计黑板 `OpenElementChestCount`，五元素共同累计 | 原始ID13001～13010；首段阈值3、6、9、12、15，每项物品2×50；后5项增量各3，13005的 `CycleCount=6000`。 |

`ScoreRewardUI.RefreshInfo` 取已领取最大索引的下一项；`SetProgress` 扣除上一阶段阈值计算本段进度，区分未达成、可领取、已领取及最终完成显示。奖励图标支持物品详情，满进度文字使用原颜色，已领取有标记。积分详情 `ScoreRewardDialog` 展示规则、成就列表与完成状态。领取前分别检查条件及是否已领取，原请求 `OperPlayerGetAchievementReward(group, 1, receiveSameType:false)` 后刷新；离线实现须等价维护实际奖励和索引，连续点击、重开窗口、重读存档不能重复发奖。馈赠面板按原两标签和共享成就 UI 恢复，不与积分详情合并成一个奖励表。

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

完整的非零综合概率展示值如下；原始整数均除10000再转为百分数，零项不得凭机台装饰补成可抽物品。

| 页面 | 整件综合概率 | 碎片综合概率 |
| --- | --- | --- |
| 蓝宝箱 | 蓝12.46%、紫1%、橙0.12% | 蓝73.55%、紫11.03%、橙1.84% |
| 紫宝箱 | 紫12.46%、橙1%、金0.12% | 紫69.87%、橙13.79%、金2.76% |
| 橙宝箱 | 橙16.63%、金1.09% | 橙68.09%、金14.19% |
| 金宝箱 | 金18.68% | 金81.32% |
| 各元素 | 紫79.11%、橙16.63%、金4.26% | 无 |

`GachaProbabilityContent` 展开规则／随机包后先过滤、再按剩余权重归一化、合并相同物品ID与数量；按品质降序、类型升序分组。组标题、图标、物品详情、有效位数与百分比文本均取原 UI；古遗物不套用好友扭蛋专用的逐物品百分比样式开关。规则文字由 `LanguageUtils.GetGachaProbilityRule` 提供，详情标题、说明和每日上限由 `GachaDetailDialog` 按类别刷新；保底综合概率还服从 `mNotShowPityTextRegion` 的实际配置。

### 原始官方历史记录与结果展示

`GachaHistoryContent` 宝箱以具体 `GachaType` 查询，元素以 `PlayerQueryElementGachaHistoryRequest` 汇总查询；保留原分页参数与每页10条，不能对每一个系列或元素各建一份互不相通的历史。收到响应更新总页数、重建当前行；切页／关闭时取消旧页更新令牌，响应后检查令牌以防旧页覆盖新页，底层查询并未传入该取消令牌。`GachaHistoryItem` 从实际物品取本地化名称／类型，名称、类型和时间三列都按品质着色，`DateTime.ToLocalTime()` 后格式化。配置180天和1000条是记录期与分类缓存边界，分页排序、过期清理和裁剪须核对数据消费者并用于本地历史；原客户端查询接口不能直接证明服务端排序／裁剪算法已取得。

`ChestGachaPanel` 接收 `OnGacha` 的逐抽数据与奖励消息后，先运行机台演出再打开结果。宝箱／元素来源奖励与相关购卡奖励有专用缓存；馈赠成就奖励在不处于馈赠页时走通用弹窗。主面板停用仍有待展示奖励时使用通用奖励弹窗兜底，不能因为中断动画丢掉奖励，亦不能在演出完成时再次发放。

`ChestGachaAwardPanel.Play` 使用 `FormatAwardList(needMergeItem:false)`；按格式化后的奖励项数为1、2～10、超过10选择 `ChestGachaAwardShowOneHandler/LessTenHandler/GreaterTenHandler`。恢复单项、新获得标记、重复转换、数量与品质显示，转换依据 `AwardData.IsAwardDataAgiotage`，不能只看当前库存是否存在。百抽结果使用行虚拟列表、可见项判定、分阶段开蛋／显示／转碎片与自动滚动；2～9项各有对应排版，10项有专用排版。

结果开始时隐藏再次抽取按钮，跳过按钮下一帧才可交互；流程完成后隐藏跳过、显示原按钮并播放按钮出现动画，库存增加后刷新费用和次数。继续抽取的奖励消息到来时隐藏按钮并关闭当前结果，让主面板接续新流程；关闭按钮调用 mediator 的抽取结束刷新。主页／结果页 `OnBack` 为空实现，不能自行用 Escape 在结算中直接退页。关闭、停用和替换结果须取消等待、释放单项／多项／百抽 handler、归还对象池并注销最高优先级奖励处理器。

额外奖励通过 `GachaAddtionalAwardHandler` 缓存 `SourceOperate.GachaAddition` 并在结果完成后 `Popup()`；固定版 `gacha_additional_awards=[]`，应保留此消息分流边界，不添加未配置的古遗物额外掉落。官方结果结束后另有商店评分调用，它属于联网周边，离线版本不得接回网络。

### 原始官方 UI、动画与音频恢复清单

| 资源／脚本 | 原始表现与待接入内容 |
| --- | --- |
| `ChestGachaPanel`、`ChestGachaPanelPreciousChestTabGroupItem_11`、`ChestGachaPanelElementChestTabGroupItem_11` | 主页面、四底栏、顶栏、机台、系列／元素切换、积分、抽取按钮、预告、红点及各状态层级；按原 RectTransform、图集、字体、颜色、遮罩、缩放与射线阻挡绑定。 |
| `ChestMapSwitchPanel/Item`、四品质 `ChestMapSwitchQuality*Badge`、`NewUnLockMark` | 系列滚动、选中与锁定／预告／开放状态、品质收藏百分比、NEW 与倒计时；背景由 `ChestGachaConfig` 加载，不能只覆盖系列11。 |
| `ElementChestSelectDialog` 组件、`ElementChestPanelElementItem` | 主 prefab 内元素弹层、五元素图片及高亮、确认／取消、弹入／收起；元素资源与类型映射来自原配置。 |
| `ChestGachaAchievementPanel`、共享 `ScoreRewardUI/ScoreRewardDialog`、`ChestGachaDetailDialog` | 馈赠两标签、积分与成就列表、奖励图标、达成／已领／完成状态、规则与物品详情弹层。 |
| 共享 `GachaDetailDialog/GachaHistoryContent/GachaHistoryItem/GachaProbabilityItem` | 概率／历史标签、规则说明、分组概率、物品明细、页码／翻页与空态；服从主题品质色、地区字段及记录开关。 |
| `GachaponMachine`、`GachaponMachineView/GachaponUI`、`TreasureGachaSetting` | 双骨骼层及十个机内胶囊，`qiu_{position:D2}`／`shock_{position:D2}` 动画，五品质、含 None 的六种元素皮肤、切换音效与地图／元素配置；机内品质分布和随机乱序仅用于外观。 |
| `TreasureGachaPlayable`、`AspectRatioScaleFitterForGachaponMachine` | `Switch/Open/Reset` Animator 触发与机台两种比例曲线；Open 后摇动／首层光效，在 `mJumpInterval` 后启用跳过，累计2秒切第二层 `ShanGuang` 与闪光音，等待 `mPopupIntervals` 后回调结果，再复位机台。跳过只调用完成回调一次、取消后续等待并清理两层特效。具体序列化时长与曲线以原 prefab 为准。 |
| `GachaponAwardGo/GachaponAwardUI` | 按实际奖励整件的品质与元素换皮，碎片用 None 元素及碎片开启音；`obtain_02` 同步骨骼与 SimpleAnimation，震动音、品质开启音分别触发；不将静止姿势当完整动作。 |
| `ChestGachaResultPanel`、`MultiResultContent` 2～10、`ChestGachaAwardPanelLine/Item` | 单抽／多抽／百抽布局、开蛋、闪光、卡片显现、虚拟行滚动、跳过、再次抽取、关闭与按钮动画；多抽布局脚本为 `ChestGachaMultiResultContent`，按实际奖励项数分派。 |
| `ChestGachaTenRewardGo/GachaTenRewardGo`、`ItemAwardIconChestGacha` | 物品详情、品质底图、数量、NEW／重复转碎片、图标与详情出现特效、转换粒子和音效，延迟转换取消及池复用清理。 |
| 原地图前后景、`TreasureGachaSetting/ChestGachaConfig`、演出音频 | 恢复每个动态选择的背景、机台皮肤、卡牌顶栏映射、元素与类型映射及18个音频事件的本地播放绑定；纳入显式资源清单，核验 CAB、材质、shader、纹理、网格、动画和音频依赖。18是已转换载荷的事件数，不表示只有18个游戏状态。 |

### 本地状态、未确定规则与逐项待实现清单

必须持久化的状态包括：各箱累计开箱次数与首次状态、共享每日计数、四种独立与五元素共享的未命中奖励记录、积分和两类馈赠累计／循环领奖索引、系列选择与已确认 NEW 集合、百抽当日免提示、分类历史、真实库存和随机状态。元素当前选择至少须维护原页面实例生命周期；是否扩展为跨会话记忆没有取得官方依据。存档版本迁移不能把旧的通用开箱次数直接误认成全部古遗物状态。

| 尚未确定／尚需落实 | 已知证据与实现边界 |
| --- | --- |
| 服务端保底裁决 | 已有基础权重、`IndexMininum`、共享ID、未命中分数消费与首次规则；未取得多条同时触发的优先级、命中后的跨档重置关系及完整服务端随机过程。实现前继续查共享记录更新与调用方；仍不能确定时须请求具体规则决定，不能从综合概率表反算。 |
| 阶位开放时间的离线来源 | 原系列使用服务器阶位开放日期与指定天数，并非仅地图开放。本文已记录全部原条件；未确认以哪个本地日期替代该服务器基准，不能默认全部开放、删除等待或改为角色转职起算。此项需要在落地这些系列时核对所属功能既定决策，仍无依据再请求规则决定。 |
| 日界与时间异常 | 原每日限额、百抽免提示、预告与历史使用相应时间判断；离线须明确所用日界／时区及回拨处理，不把别的离线收益约定自动扩用到本功能。本文不代定缺失规则。 |
| 历史与奖励决策 | 记录期／缓存配置和客户端分页已取得；服务端历史裁剪顺序、重复整件实际转换规则须继续核对共享实现与物品数据。客户端播放转换或查询请求不代替本地结算依据。 |

以下复选项均未完成 Unity 全流程验收；上方“已取得依据”不勾选为功能完成。每项实现后须同步正文真实状态与验证范围。

- [ ] 打通家具修复／业务入口、收藏与物品来源导航、访问资格、战斗／地图限制及窗口上下文。
- [ ] 恢复主面板四底栏、顶栏、页面切换、官方层级与 Windows 竖屏比例／真实输入。
- [ ] 接通四类宝箱、17个系列及全部原条件、预告、NEW、确认／关闭保存分支、收藏进度与图鉴跳转。
- [ ] 接通五元素初态、选择／确认／取消、类型与皮肤联动、当前实例记忆和元素开放范围。
- [ ] 接通单抽、动态2～10抽、百抽、材料不足／原快捷购买、当日免提示及共享10000次限制。
- [ ] 实现本地逐抽随机、首次规则、整件／碎片选择器、条件过滤、去重／空池回退及真实奖励转换。
- [ ] 核定并实现保底更新／触发／重置、四宝箱独立与五元素共享记录、保底剩余次数显示。
- [ ] 将随机状态、扣材、固定积分、保底、每日计数、馈赠、库存、历史作为一次结算提交，覆盖失败回滚、重复提交和中途退出恢复。
- [ ] 实现积分组12及循环展开、当前阶段进度、详情列表、奖励领取与持久化防重。
- [ ] 实现宝箱／元素馈赠组11／13、两标签、循环阶段、领奖显示与防重。
- [ ] 恢复详情双标签、原规则文本、分组物品、基础／综合概率、地区隐藏字段及物品详情。
- [ ] 实现四类宝箱及合并元素历史、分页／取消、品质色／本地时间、空态、保留／裁剪和存档恢复。
- [ ] 恢复图鉴、积分说明、女神牌补差购买、原空商店及奖励消息分流；不添加未配置商品或额外掉落。
- [ ] 接通入口／标签／抽取／系列NEW／领奖红点，补齐订阅、刷新与释放。
- [ ] 恢复原机台、双骨骼、十胶囊、五品质／六元素皮肤、比例曲线、Open／Switch／Reset 与光效。
- [ ] 恢复单项、2～9、10与百抽结果编排、虚拟行／自动滚动、NEW／转换、音效与阶段跳过。
- [ ] 恢复结果页继续抽取／关闭、奖励兜底、同帧连续操作、异步取消与对象池清理，确保不漏发／重发奖励。
- [ ] 完成原资源 Android→Windows 原生恢复及显式动态加载绑定，按来源记录哈希和实际替换状态。
- [ ] 完成 Editor 导入／编译、Play Mode 逻辑与真实鼠标／滚动／遮挡／画面／动画／音频、独立存档恢复验证；按构建影响决定是否验证 Windows Player。

### 材料购买、资源恢复与运行验收

稀有女神牌301原快捷价晨星8／张，无次数限制，只补实际差额；302～304与311无无限快捷购买记录，不把物品估值当售价。商店6原表无商品，显示原空状态。此处沿用既有材料方案，不因离线迁移添加原表未配置的购买或积分。

后续须恢复官方 Unity 脚本、原 prefab 层级、原生动画／shader 和动态资源清单，将随机、扣材、积分、保底、每日计数、真实奖励及历史放入同一本地事务，完成重复领取保护与失败回滚。演出按原序列化时序与阶段实现，跳过不能跳掉结算或留下失效回调；角色／机台 Spine 数据转换遵循[既定免费迁移路线](../asset-management.md#spine-角色动画迁移决策)。

完成后按[实际运行验证方法与要求](../asset-management.md#实际运行验证方法与要求)从玩家入口验证：四档宝箱和五元素、2～10抽与100抽、999／1000张边界、首次／常规／保底、系列与元素切换、积分／馈赠循环领奖、详情／历史翻页、跳过与返回、奖励到账及保存恢复。需使用真实鼠标／滚动操作并查看运行画面和动画；静态检查及旧引擎测试不能代替这些结果。

### 依据与代码定位

官方客户端根目录为 `.tools/source-packages/official-code/client-source/`，共享代码为 `.tools/source-packages/official-code/common-source/Common/`。上文的类与方法为具体恢复入口；接口另见 `PlayerGachaRequest/GachaTreasureParam/PlayerQueryGachaHistoryRequest/PlayerQueryElementGachaHistoryRequest`。

关键原文件：[入口导航](../../.tools/source-packages/official-code/client-source/ChestGachaPanelNavigateHandler.cs)、[主面板](../../.tools/source-packages/official-code/client-source/ChestGachaPanel.cs)、[宝箱面板](../../.tools/source-packages/official-code/client-source/PreciousChestPanel.cs)、[系列选择及关闭确认](../../.tools/source-packages/official-code/client-source/ChestMapSwitchPanel.cs)、[元素选择](../../.tools/source-packages/official-code/client-source/ElementChestSelectDialog.cs)、[积分进度及领取](../../.tools/source-packages/official-code/client-source/ScoreRewardUI.cs)、[成就循环展开](../../.tools/source-packages/official-code/common-source/Common/AchievementRawInfoParser.cs)、[概率内容](../../.tools/source-packages/official-code/client-source/GachaProbabilityContent.cs)、[历史分页](../../.tools/source-packages/official-code/client-source/GachaHistoryContent.cs)、[结果面板](../../.tools/source-packages/official-code/client-source/ChestGachaAwardPanel.cs)、[机台演出](../../.tools/source-packages/official-code/client-source/TreasureGachaPlayable.cs)、[系列阶位时间条件](../../.tools/source-packages/official-code/common-source/Common/ServerRankUnlockDaysCondition.cs)。`.tools` 为本机官方源码缓存，重建按 [SOURCES.md](../../assets/reference/SOURCES.md) 的入口执行，不随游戏交付。

当前 Unity 入口与状态定位：[HomeFeatureRuntime.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs)、[HomeHudView.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.cs)、[OfflineMainlineProgressSession.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs)。

来源及转换依据：[扭蛋页面与机台](../../assets/reference/relic-draw/source-manifest.json)、[系列选择](../../assets/reference/relic-navigation/source-manifest.json)、[演出与音效](../../assets/reference/relic-performance/source-manifest.json)、[百抽字段](../../assets/reference/hundred-draws/source-audit.json)、[快捷购买](../../assets/reference/gacha-purchase/source-manifest.json)。现存导航转换工具为 [prepare_relic_navigation.py](../../scripts/prepare_relic_navigation.py)，原始包与 CAB 核验使用 [prepare_unity_bundle_inputs.py](../../scripts/prepare_unity_bundle_inputs.py)；这些工具不证明玩法已恢复。
