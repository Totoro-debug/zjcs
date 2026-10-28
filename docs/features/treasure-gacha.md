# 古遗物扭蛋与女神牌

[总索引](../README.md)

状态：未完成

## 功能简介

入口为家园时光扭蛋机（家具类型 `9`）、收藏图鉴跳转及女神牌快捷购买，覆盖四类宝箱、五元素奖池、扭蛋积分、馈赠、详情／概率、历史记录、空商店及抽取演出。

`HomeFeatureRuntime.OpenFurniture` 已打开扭蛋主面板，加载固定版目录和离线服务；`HomeHudView.TreasureGacha.cs` 已接入页面与本地操作。物品详情按官方 `ItemSourceChestGachaAgent` 规则列出国度扭蛋与元素扭蛋来源，沿用 `ItemSourceItem` 原布局、状态图、箭头和背景；点击后分别路由整件／碎片或元素奖池，关闭扭蛋会重读存档并恢复来源物品详情。仅接入扭蛋来源行，不扩展主城储物箱入口或储物箱功能；完整交互仍需真实输入验收。

家具损坏状态及免费修理属于家园家具流程；本页记录扭蛋入口，不把未验证的家具修理演出算作扭蛋验收。

## 原始官方实现

ChestGachaPanel 与结果／概率／历史窗口消费原古遗物扭蛋池、数量、消耗、概率、保底、重复转化、兑换和演出配置。

### 原始官方入口、页面及选择行为

以下记录固定版客户端的实际分支，作为 Unity 流程的逐项对照；接入现状见上方覆盖表。

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

`ChestGachaDrawButton` 的原 prefab 为单抽固定1、多抽动态最少2最多10、百抽动态最少100最多100；箱子 `OpenCounts=[1,2,3,4,5,6,7,8,9,10,100]`，`PreciousChestMaxOpenCount=100`。`GachaChestBase.GetMaxOpenCount` 按持有材料整除单次成本并取按钮上限；`PreciousGachaChest` 只有在当前材料不能抽时才取购买策略的补位次数，原可购买策略返回10，不将晨星余额直接混成可百抽材料。每日剩余数单独记录，发送前以“已抽＋数”校验10000上限；按钮可显示不代表请求可越过日限。

`GachaPanelUtils.HundredGachePreRemind` 对100次及以上显示带“今日不再提示”的 `MarkableConfirmDialog`；取消不发送抽取，勾选后按各 `GachaType` 保存 `GachaBaseSetting.LastIgnoreRemindHundredTickDict` 时间戳并异步落盘，隔日按原时间比较重新提示。宝箱发送前另检查所选系列存在且条件满足；不满足显示 `ui_current_map_has_no_treasure`，不扣材料、不产生结果。

红点包括宝箱总入口、元素总入口、具体抽取按钮、新系列和可领奖状态。`PreciousChestCanOpenRedDotItem/TipDotPreciousChestCanOpenConditionNode` 按配置的类型集合或单类型、`OpenCount`、实际材料与每日上限判断；按钮节点另服从忽略箱子、固定／动态次数。元素节点检查五元素所用卡311；`ShowRedDotMinCount=10` 应按其调用处消费，不一律解释为“有一张就亮”。`TipDotNewChestGachaCanSwitchConditionNode` 在任一开放系列未记入 `TreasureGachaConfirmed` 时提示。库存、抽取、条件、黑板和领奖变化须刷新，销毁时解除订阅。

### 原始官方积分与馈赠奖励

| 类别 | 原配置与累计来源 | 奖励模板与循环 |
| --- | --- | --- |
| 扭蛋积分 | 类型13、组12；累计黑板 `PreciousChestScoreCount`，不以背包现存积分余额替代累计；蓝紫橙金分别每抽1／2／3／5，元素不产生此积分 | 原始ID12001～12060。12001～12030阈值5、10…150；每项给对应卡5张：第12／24项为311，第30项304，其余逢3为303，否则302。12031～12060的 `CompleteParams=null`、增量每项5，卡牌序列同前30项；12030的 `CycleCount=5000`。须通过 `AchievementRawInfoParser` 展开，不能将60条模板直接当60个最终阶段或自行写无限循环。 |
| 宝箱馈赠 | 类型88、组11；合计四类宝箱开箱数，与积分累计独立 | 原始ID11001～11010；首段阈值10、20、30、40、50，每项物品2×15；后5项增量各10，11005的 `CycleCount=20000`。 |
| 元素馈赠 | 类型8、组13；累计黑板 `OpenElementChestCount`，五元素共同累计 | 原始ID13001～13010；首段阈值3、6、9、12、15，每项物品2×50；后5项增量各3，13005的 `CycleCount=6000`。 |

`ScoreRewardUI.RefreshInfo` 取已领取最大索引的下一项；`SetProgress` 扣除上一阶段阈值计算本段进度，区分未达成、可领取、已领取及最终完成显示。奖励图标支持物品详情，满进度文字使用原颜色，已领取有标记。积分详情 `ScoreRewardDialog` 展示规则、成就列表与完成状态。领取前分别检查条件及是否已领取，原请求 `OperPlayerGetAchievementReward(group, 1, receiveSameType:false)` 后刷新；离线实现须等价维护实际奖励和索引，连续点击、重开窗口、重读存档不能重复发奖。馈赠面板按原两标签和共享成就 UI 恢复，不与积分详情合并成一个奖励表。

### 原始官方历史记录与结果展示

`GachaHistoryContent` 宝箱以具体 `GachaType` 查询，元素以 `PlayerQueryElementGachaHistoryRequest` 汇总查询；原分页参数为每页10条，不能对每一个系列或元素各建一份互不相通的历史。服务端响应到来时更新总页数并重建当前行；切页／关闭时取消旧页更新令牌，响应后检查令牌以防旧页覆盖新页。`GachaHistoryItem` 从实际物品取本地化名称／类型，名称、类型和时间三列都按品质着色，`DateTime.ToLocalTime()` 后格式化。离线规则由用户确认：180日保留期和每类1000条上限同时应用；四种宝箱各自计类，五种元素合并计类，超限删除时间最早的记录。`VerifyHistoryRetention` 用实际抽取事务覆盖过期记录、最旧记录和分类边界。

`ChestGachaPanel` 接收 `OnGacha` 的逐抽数据与奖励消息后，先运行机台演出再打开结果。宝箱／元素奖励与相关购卡奖励使用专用缓存；馈赠奖励在不处于馈赠页时走通用弹窗。离线 UI 直接使用已事务发放的奖励记录，关闭面板也有展示兜底，展示流程不再次发奖。

`ChestGachaAwardPanel.Play` 使用 `FormatAwardList(needMergeItem:false)`；按格式化后的奖励项数为1、2～10、超过10选择单项／少量／大量结果处理器。Unity 按奖励数量使用官方结果面板布局，显示名称、数量、品质和折算字段。用户确认不可重复的重复整件转对应碎片；离线结算按官方 `ItemInfo.CanRepeat/AgioItemId/AgioAmount` 发放蓝／紫／橙／金20／30／40／60片，`VerifyDuplicateRelicConversion` 实际抽取并核对库存与结果字段。百抽行虚拟列表、分阶段开蛋／显示／转碎片与自动滚动属于待验证的动画表现。

结果开始时隐藏再次抽取按钮，跳过按钮下一帧才可交互；流程完成后隐藏跳过、显示原按钮并播放按钮出现动画，库存增加后刷新费用和次数。继续抽取的奖励消息到来时隐藏按钮并关闭当前结果，让主面板接续新流程；关闭按钮调用 mediator 的抽取结束刷新。主页／结果页 `OnBack` 为空实现，不能自行用 Escape 在结算中直接退页。关闭、停用和替换结果须取消等待、释放单项／多项／百抽 handler、归还对象池并注销最高优先级奖励处理器。

额外奖励通过 `GachaAddtionalAwardHandler` 缓存 `SourceOperate.GachaAddition` 并在结果完成后 `Popup()`；固定版 `gacha_additional_awards=[]`，应保留此消息分流边界，不添加未配置的古遗物额外掉落。官方结果结束后另有商店评分调用，它属于联网周边，离线版本不得接回网络。

### 原始官方 UI、动画与音频映射

原层级和序列化数据已作为 UI 载荷加载；下表保留每类节点和演出的复原依据。机台动画已由原网格／UV／帧数据驱动，完整表现仍须按下方验证范围核验。

| 资源／脚本 | 原始表现与 Unity 还原依据 |
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

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

`prepare_relic_draw.py` 从已校验的同版本官方 CDN 缓存（manifest `854_156383`）读取 `prefabs_ui_chestgacha` 及依赖、`chestgachabg_*` 和 `scriptableconfig_modules_treasure`；复用固定 APK 校验及 `Apk` 来源读取器。完整接入 `ChestGachaPanel` 的原始 RectTransform、宝箱 / 元素 / 兑换 / 馈赠四个底部页签、宝箱四种卡片及进度条、元素选择弹层和原始空商店。此前关于扭蛋专用布局尚未取得的记录描述的是当时仅有基础 APK 的状态，此次由匹配清单的 CDN 缓存补齐。

提取 134 项 Sprite / Texture2D 来源记录，包括图集裁切及原始扭蛋机 / 胶囊 Spine 图集解码；脚本合并来源清单和显式加载清单，原始 bundle / 对象、实际 URL、输入校验与输出 SHA256 见 [relic-draw/source-manifest.json](../../assets/reference/relic-draw/source-manifest.json)。按 `TreasureGachaSetting` 的品质分布、元素皮肤和 `shock_01..10` 末帧组合九种奖池、十个胶囊的静止姿势，不调用抽奖 RNG。原版入场动画独立播放，开蛋粒子及完整演出仍未转换，结果使用既有原始通用结果页。

积分 / 规则 / 历史 / 结果弹窗以当前原始扭蛋页为底层，底层不接收指针。馈赠复用原始 `ChestGachaAchievementPanel` 成就条目并绑定滚轮 / 拖动。修正 Unity 无 Graphic 的零高 Mask 容器适配，避免元素图标被误裁空。

`prepare_treasure_gacha.py` 按固定版16个原 prefab 读取 `Image` 组件并核对层级路径，登记245个原 Sprite 引用，解码60张原 UI Sprite；运行时恢复图片类型、色调、PPU、pivot、九切片边框及射线标记。`ImgCostMoneyBtn_Bg` 保留原边框 `[20,19,20,20]`。输入对象、输出 SHA256 与绑定数据见 [treasure-gacha/source-manifest.json](../../assets/reference/treasure-gacha/source-manifest.json)。

`python scripts/prepare_relic_navigation.py` 离线读取固定 APK 与已校验的同版本 CDN 缓存，转换 `ChestMapSwitchPanel`、`ChestMapSwitchItem` 和 `ChestGachaConfig` 的 17 组地图入口图片。40 项 Sprite 依赖继续输出至 `assets/official/sprites/`，两项 prefab 合并至 `assets/official/ui.json`，地图入口配置写入 `assets/official/relic_navigation.json`，均纳入显式本地加载清单。输入 bundle 的真实来源、大小、MD5、SHA256、CDN URL（如适用）、对象名、对象 path ID、转换方式与输出 SHA256 见 [relic-navigation/source-manifest.json](../../assets/reference/relic-navigation/source-manifest.json)。没有使用新版替代包或新增自绘图像。

右上角地图选择接入原版面板与地图图标，确认时持久化所选系列，切换标题、收集进度、背景、图鉴入口及实际宝箱奖池；四类宝箱采用原 `ItemFilterForTreasureGacha` 的系列过滤，元素池保留原元素范围。书本入口复用已转换的 `CollectBook` 古遗物图鉴，概率列表复用 [codex/source-manifest.json](../../assets/reference/codex/source-manifest.json) 登记的原始遗物图标。原扭蛋机、胶囊、收藏框与历史资源来源保留。

概率页沿用原 `GachaDetailDialog`、品质分组与滚动区域，按说明文字高度布局，显示原表基础概率、当前材料和保底进度；这一修正不表示 Unity 完整开蛋演出或服务端动态概率公式已迁移。

`python scripts/prepare_relic_unlock_text.py` 将固定 `854_156383` 的五个中文键迁移为 Git 内代码绑定 ：`PLAYER_FUNCTION_LIMITED`、`home_notice_Level_tip`、`function_limit_mapUnlock`、`Mainland_11`、`confirm`。输入来自固定 APK `assets/yoo/WwiseBnk/a373f2acc39aeb5c4560f1456879b997.rawfile`（launch language，SHA256 `a068ee47db184c96c8923172a104ed90b37f72e2b4adb5d73211d894d7ec9001`）和 `assets/yoo/WwiseBnk/9a0b6ab8c5136050fd005ead8f567863.rawfile`（language，SHA256 `738069eafacd7fe33aae970aa5edacd76f48ed02acdd07ededad1c4a499b93b8`）；APK SHA256 为 `ea17b42477a2ee097837753cb96c2b71f6926c2fbc673bbff547a52707fcc0b9`。输出按 UTF-8/LF 计算 SHA256 为 `2c791a489f18072bfb71307a6029cb27ca1619faa3ec39bcfbdf92a6940d489b`；转换工具严格检查两份输入哈希，不改写源缓存。

提示控件复用已转换的 `ConfirmDialog.prefab`：原版本 CDN bundle `e9fb11a95f5ca61a90de42fc3fd90258`，对象 `5569979765835515756`，实际 URL `https://zhangjcsdl.leiting.com/patch/202609271456857/Android/DefaultPackage/e9fb11a95f5ca61a90de42fc3fd90258.bundle`，输入 bundle SHA256 `872b72513cc17d918891383990fb4dbe92c0bb5add97a4561840197b468578d3`。控件来自 `ui.json` 中现有的 `common_confirm` 绑定，遮罩颜色沿用原公共 PopupWindow 配置；不产生新的图像、素材目录输出或显式资源清单条目。官方 `FurnitureBase`、`HomeMyPlayerData`、`ChestGachaPanelNavigateHandler`、`ConditionNotice` 与 `LanguageUtils.GetConditionTextWrap` 为条件与文字调用依据。

原版 `854_156383` 的 `ChestGachaPanel`、`ChestGachaResultPanel`、`GachaponAwardGo`、各数量布局与 `ChestGachaTenRewardGo` 经 [古遗物演出转换来源](../../assets/reference/relic-performance/source-manifest.json) 转换。Animator、SimpleAnimation、UIParticle、材质与 Wwise 音频均记录原对象及输出哈希。运行资源在 `assets/official/relic-performance/`，来源见 [relic-performance/source-manifest.json](../../assets/reference/relic-performance/source-manifest.json)。未使用新版同名包或自绘素材。

## 完整还原部分

已接入目录、离线规则服务、官方层级 UI、动态图片、机台网格演出和本地存档。当前不标记为完整完成，剩余项目见“尚未实现”。

### 已取得的官方依据及当前接入边界

- 固定版 `854_156383` APK 中的 `gacha/chest/treasure_gacha/reward_rule/random_reward/gacha_pity_probability/achievement/game_settings/item/conditions` 已用原 Luban 构造器重新解码，10张表均与 [rules.json](../../assets/official/rules.json) 一致。
- 已实际读取原 `ChestGachaPanel`、`ChestGachaResultPanel`、`ChestGachaAchievementPanel`、`ChestGachaDetailDialog`、`ChestMapSwitchPanel` 及共享 `GachaDetailDialog/GachaHistoryContent/GachaHistoryItem/ScoreRewardDialog` prefab。元素选择由主页面内的 `ElementChestSelectDialog` 组件控制，不能因未找到同名独立 prefab 就判为源资源缺失。
- 原扭蛋 UI 包 `defaultpackage_assets_prefabs_ui_chestgacha.bundle`、共享详情包 `defaultpackage_assets_prefabs_ui_gachapanel.bundle` 及配置包 `defaultpackage_assets_scriptableconfig_modules_treasure.bundle` 经 `prepare_unity_bundle_inputs.py` 核对：包头 `2022.3.57f1`、目标 Android，361个递归 bundle／CAB 来源完整。这只证明转换输入可用，未证明 Windows 原生脚本、shader 或运行表现。
- 官方 `ItemSourceChestGachaAgent.GenerateSourceData` 从可抽取系列生成整件／碎片来源，并从 `CountAchievementType == OpenElementChestCount` 的元素奖池奖励展开生成元素来源；`ClientItemSourceElementChestGachaHandler` 传递奖励物品的 `ElementType`。Unity 来源解析按此配置验证元素奖励并映射元素奖池，无需额外的 `ItemSourceInfo` 表。
- [relic_draw.json](../../assets/official/relic_draw.json)、[relic_navigation.json](../../assets/official/relic_navigation.json)、[relic-performance/performance.json](../../assets/official/relic-performance/performance.json) 与 `meshes.json` 提供系列、UI 绑定和机台／结果演出数据；生成器把显式选择的资源写入 `StreamingAssets/TreasureGacha`，Unity 以 `HomeAssetCatalog` 和 `TreasureGachaPerformancePlayer` 加载。资源包输入仍按 Android→Windows 迁移流程核验，不把 Bundle 直接作为 Windows 运行时资产。
- `OfflineTreasureGachaProgress` 随玩家存档保存系列选择、保底、每日计数、积分／馈赠领取、百抽提示、随机状态及历史；`OfflineTreasureGachaService.Draw` 将材料、奖励、计数和历史放入同一库存事务。

### 实际验证范围

`python -B scripts/check_unity_project.py` 通过（27,998 GUID；仅静态元数据）。Unity Editor 脚本编译成功；隔离 Editor 验收以退出码0结束，覆盖阶位开放边界、首次抽取、三档保底冲突顺序及命中清零、四品质重复兑换、180日与分类上限、宝箱／元素来源解析、官方机台及结果目录。Play Mode 实际打开主面板并捕获720×1280画面；确认官方四档卡图、机台网格、底栏和九切片图片已渲染，并调用宝箱／元素来源入口、检查选中状态、关闭返回回调。当前 Windows 会话锁定，真实鼠标／滚动尚未验证；音频、长列表操作、存档重开和 Windows Player 也未验证。

## 自定义实现

以下为离线实现所采用的处理规则。客户端没有权威服务端抽奖决策代码的部分只按用户确认的规则执行，不声称复现服务端随机序列。

缺少服务端免费自动修复回调时，已拥有家具，或满足原30级／地图11免费修复资格即可访问收藏跳转、购买和扭蛋子页；只读判断，不伪写所有权。

本地按固定版奖池、权重、数量范围、条件过滤及首次规则结算；材料、随机状态、奖励、积分、保底、日计数与历史共同提交。

用户已确认：多个品质的保底都在同一抽次到期时，将冲突按到期抽次之前的连续抽次依次兑现（两个冲突移到第9、10抽，三个移到第8、9、10抽）；某品质命中后，该品质未命中数归零。阶位天数按本地阶位解锁时间正常计算。每日界为 Windows 本地午夜；保存最大已观察日期，系统时钟回拨不再次重置。

可取得的 `Common.Player.TreasureGacha`、`ElementTreasureGacha` 只检查箱子配置、记录开箱并返回空奖励列表；客户端 `PreciousGachaChestNotGoodsStrategy.OpenChest`、`ElementGachaChest.SendGacha` 请求服务端操作，奖励结果从消息消费。它们不足以支持离线抽取，因此本地服务根据官方配置结算，并对服务端没有公开的行为保留独立决策记录。

## 尚未实现

下表区分已经接入和仍需验证／确认的内容；官方规则详情见后续各表。

| 功能 | 已接入 | 尚需完成 |
| --- | --- | --- |
| 宝箱扭蛋 | `HomeHudView.TreasureGacha.cs` 接入蓝紫橙金类型、官方17系列条件与顺序、系列选择／确认、推荐、NEW、收藏进度、图鉴、费用、抽取和结果。 | 真实输入下复验系列列表、关闭确认和不同玩家解锁状态。 |
| 元素扭蛋 | 火水风光暗类型、311卡消耗、选择／确认／取消、元素奖池条件过滤、皮肤／背景和结果已接入。 | 真实点击复验选择弹层的遮挡、取消与切换后的状态。 |
| 次数与限制 | 单抽、按库存计算的2～10抽、百抽、10000日限、每日百抽确认、301晨星补差快捷购买和费用刷新已接入。 | 验证999／1000卡边界、材料不足、百抽提示记忆和本地午夜回拨。 |
| 积分与馈赠 | 积分组12、宝箱馈赠组11、元素馈赠组13及循环模板、分页显示、领奖校验与防重复事务已接入。 | 真实操作复验循环段滚动、领奖反馈和关闭重开。 |
| 详情与概率 | 原类别规则文字、基础／综合概率、条件过滤后的分组概率、物品详情及每日上限说明已接入。 | 真实操作复验长列表滚动、地区字段开关和遮挡。 |
| 历史记录 | 四宝箱独立历史、五元素合并历史、分页／本地时间／品质色已接入；每条记录同时按180日和每类1000条裁剪，按时间删除最旧记录。Editor 用例覆盖四箱分类、合并元素分类、到期清理和最旧删除。 | 真实滚动、翻页及关闭重开恢复仍需鼠标验收。 |
| 页面与辅助入口 | 家具类型9打开主面板；四底栏、图鉴、女神牌301补差购买、空商店、红点、结果页继续抽取／关闭已接入。库存详情使用原 `ItemSourceItem` 来源行；`InventoryFeatureRuntime` 按官方整件／碎片与元素来源规则提供行，跳转后关闭可重读存档并恢复物品详情。仅接入扭蛋来源行，不扩展主城储物箱入口或储物箱功能；家具损坏／免费修理由家园流程负责，尚未随扭蛋 Play Mode 流程验证。 | 来源行点击及关闭返回需真实鼠标验收。 |
| 演出与音频 | 官方机台双网格和十胶囊品质／元素动画使用显式加载的官方图集与动画帧；结果数量排版和奖励兜底已接入。机台初始化与图集已在 Play Mode 画面检查。 | 真实点击后的结果开蛋阶段、动画跳过、音频和长结果演出仍待实机验收。 |

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

### 当前持久化状态、确认规则与验证缺项

`OfflineTreasureGachaProgress` 保存各类型累计次数、四箱独立与元素共享保底、品质未命中数、每日次数、系列选择与已确认 NEW、积分／馈赠循环领取、百抽免提示、随机状态及分页历史。抽取与领奖均通过 `ApplyInventoryTransaction` 写入库存和扭蛋进度；历史元素查询合并五个元素类型。元素选择仅按官方页面实例生命周期保存，不扩展为跨会话选择。

阶位日数按对应本地阶位解锁时间的日历天计算。每日日期取 Windows 本地时间；`LastObservedLocalDayKey` 只前进不后退，时钟回拨不会再次刷新每日次数。以上规则由用户明确确认。

| 待确认／待验证项 | 证据与当前状态 |
| --- | --- |
| 已确认的离线补充 | 到期保底按连续抽次提前兑现，命中后品质未命中数归零；阶位天数按本地解锁时间；日界为 Windows 本地午夜且回拨不重复重置；历史双重裁剪和重复整件兑换采用本页所述规则。代码及 Editor 用例已覆盖规则服务和抽取事务。 |
| 物品来源实际调用方 | `OfficialInventoryPanelView` 从已核验的 `gacha-item-sources/catalog.json` 生成原始来源行；`InventoryFeatureRuntime` 通过 `OfflineTreasureGachaService.ResolveItemSource` 与 `ResolveElementItemSource` 按官方奖池筛选来源，并调用家园现有宝箱／元素路由。扭蛋关闭时库存重读当前玩家存档、刷新列表并恢复来源物品详情。仅接入扭蛋来源行，不扩展主城储物箱入口或储物箱功能；来源行真实点击与返回仍待桌面 Play Mode 验收。 |
| 机台及输入全流程 | 官方十个胶囊按原创建顺序播放，主界面、卡图、切图与九切片已在 Play Mode 画面核验；来源入口与关闭返回回调通过实际 Play Mode 探针。需在可交互桌面上验证标签／抽取／弹窗、系列和元素切换、滚动、跳过、动画与音频及独立存档恢复。当前 Windows 会话锁定，真实鼠标操作未验证。 |

### 材料购买、资源恢复与运行验收

稀有女神牌301原快捷价晨星8／张，无次数限制，只补实际差额；302～304与311无无限快捷购买记录，不把物品估值当售价。商店6原表无商品，显示原空状态。此处沿用既有材料方案，不因离线迁移添加原表未配置的购买或积分。

官方层级、动态资源清单及本地抽取事务已接入；当前演出复用官方机台网格与图集。剩余表现验证须核对初始化位置、切换、抽取动画和跳过清理，不让 UI 表现参与或重复结算。Spine 网格／动画迁移遵循[既定免费迁移路线](../asset-management.md#spine-角色动画迁移决策)。

剩余验收按[实际运行验证方法与要求](../asset-management.md#实际运行验证方法与要求)从玩家入口验证：四档宝箱和五元素、2～10抽与100抽、999／1000张边界、首次／常规／保底、系列与元素切换、积分／馈赠循环领奖、详情／历史翻页、跳过与返回、奖励到账及保存恢复。需使用真实鼠标／滚动操作并查看运行画面和动画；静态检查及旧引擎测试不能代替这些结果。

## 代码与资源入口

官方客户端根目录为 `.tools/source-packages/official-code/client-source/`，共享代码为 `.tools/source-packages/official-code/common-source/Common/`。上文的类与方法为具体恢复入口；接口另见 `PlayerGachaRequest/GachaTreasureParam/PlayerQueryGachaHistoryRequest/PlayerQueryElementGachaHistoryRequest`。

关键原文件：[入口导航](../../.tools/source-packages/official-code/client-source/ChestGachaPanelNavigateHandler.cs)、[主面板](../../.tools/source-packages/official-code/client-source/ChestGachaPanel.cs)、[宝箱面板](../../.tools/source-packages/official-code/client-source/PreciousChestPanel.cs)、[系列选择及关闭确认](../../.tools/source-packages/official-code/client-source/ChestMapSwitchPanel.cs)、[元素选择](../../.tools/source-packages/official-code/client-source/ElementChestSelectDialog.cs)、[积分进度及领取](../../.tools/source-packages/official-code/client-source/ScoreRewardUI.cs)、[成就循环展开](../../.tools/source-packages/official-code/common-source/Common/AchievementRawInfoParser.cs)、[概率内容](../../.tools/source-packages/official-code/client-source/GachaProbabilityContent.cs)、[历史分页](../../.tools/source-packages/official-code/client-source/GachaHistoryContent.cs)、[结果面板](../../.tools/source-packages/official-code/client-source/ChestGachaAwardPanel.cs)、[机台演出](../../.tools/source-packages/official-code/client-source/TreasureGachaPlayable.cs)、[系列阶位时间条件](../../.tools/source-packages/official-code/common-source/Common/ServerRankUnlockDaysCondition.cs)。`.tools` 为本机官方源码缓存，重建按 [SOURCES.md](../../assets/reference/SOURCES.md) 的入口执行，不随游戏交付。

当前 Unity 实现：[HomeFeatureRuntime.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs)、[HomeFeatureRuntime.TreasureGachaSource.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.TreasureGachaSource.cs)、[HomeHudView.TreasureGacha.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.TreasureGacha.cs)、[OfficialInventoryPanelView.cs](../../unity/Assets/Game/Runtime/Presentation/Inventory/OfficialInventoryPanelView.cs)、[InventoryFeatureRuntime.cs](../../unity/Assets/Game/Runtime/Presentation/Inventory/InventoryFeatureRuntime.cs)、[OfficialInventoryAssetCatalog.cs](../../unity/Assets/Game/Runtime/Presentation/Inventory/OfficialInventoryAssetCatalog.cs)、[HomeAssetCatalog.cs](../../unity/Assets/Game/Runtime/Presentation/Home/HomeAssetCatalog.cs)、[TreasureGachaPerformancePlayer.cs](../../unity/Assets/Game/Runtime/Presentation/Home/TreasureGachaPerformancePlayer.cs)、[OfficialTreasureGachaCatalog.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfficialTreasureGachaCatalog.cs)、[OfflineTreasureGachaService.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureGachaService.cs)、[OfflineTreasureGachaProgress.cs](../../unity/Assets/Game/Runtime/Offline/Progression/OfflineTreasureGachaProgress.cs)、[OfficialTreasureGachaCatalogAcceptance.cs](../../unity/Assets/Game/Editor/TreasureGacha/OfficialTreasureGachaCatalogAcceptance.cs)、[OfficialInventoryAssetTools.cs](../../unity/Assets/Game/Editor/Inventory/OfficialInventoryAssetTools.cs)。素材导入入口为 [prepare_unity_inventory_assets.py](../../scripts/prepare_unity_inventory_assets.py)，扭蛋素材入口为 [prepare_treasure_gacha.py](../../scripts/prepare_treasure_gacha.py)。

来源及转换依据：[扭蛋页面与机台](../../assets/reference/relic-draw/source-manifest.json)、[系列选择](../../assets/reference/relic-navigation/source-manifest.json)、[物品来源行](../../assets/reference/gacha-item-sources/source-manifest.json)、[演出与音效](../../assets/reference/relic-performance/source-manifest.json)、[百抽字段](../../assets/reference/hundred-draws/source-audit.json)、[快捷购买](../../assets/reference/gacha-purchase/source-manifest.json)。现存导航转换工具为 [prepare_relic_navigation.py](../../scripts/prepare_relic_navigation.py)，原始包与 CAB 核验使用 [prepare_unity_bundle_inputs.py](../../scripts/prepare_unity_bundle_inputs.py)；这些工具不证明玩法已恢复。
