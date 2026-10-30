# 技能祈愿、材料购买与兑换

[总索引](../README.md)

状态：未完成

已接入素材已复核当前云端 `871_156598`：16 个独立粒子材质补齐真实 shader、关键字与纹理采样参数，两个遗留材质属性更新为当前值；恢复官方 Image 的射线开关和透明 `NoDrawingRayCast` 命中区域。来源分别见 `assets/reference/official-map/current-ui-particle-materials-source-manifest.json`、`current-ui-graphics-source-manifest.json`。Unity Play Mode 已通过 EventSystem 坐标／射线模拟选择职业、常驻单抽、关闭结果、进入兑换与返回祈愿；没有控制 Windows 鼠标。该次结果音频未被观测到，不能视为音频播放验证通过。画面复核发现的 prefab 外语占位文字由 `refresh_current_ui_text_bindings.py` 根据当前中文 rawfile 的实际本地化键转换；已在同步当前代码和同一载荷的独立验证副本中重跑模拟输入，并查看结果截图确认中文显示，转换前字节保留在来源记录中。这不代表整套祈愿功能已验收完成。

## 功能简介

女神像或技能页进入常驻／高阶祈愿，范围包含职业选择与自选、技能池、概率与保底、积分、馈赠、兑换、材料购买、详情、历史，以及对应 UI、动画、特效和音频。幻兽及古遗物奖池分别见[幻兽召唤](pet-gacha.md)和[古遗物寻宝](treasure-gacha.md)。

本页同时记录固定 manifest `871_156598` 的官方依据与 Unity / C# 还原结果；对应离线实现见“完整还原部分”，当前运行验收范围见“尚未实现”。友情祈愿保留消费入口，离线获取限制见“自定义实现”。

客户端源码根目录为 `.tools/source-packages/official-code/client-source/`，共享源码根目录为 `.tools/source-packages/official-code/common-source/Common/`；下文类名均相对此入口。表数据见[原规则配置](../../assets/official/rules.json)，页面布局见[原 UI 转换数据](../../assets/official/ui.json)。

## 原始官方实现

原常驻／高阶祈愿、职业选择、技能池、馈赠、历史及商店 4／5／7 使用官方概率、条件、消耗、保底／重复转化和结果演出。

### 原始官方功能：总页与抽取入口

| 编号 | 原始官方行为 | 源码／配置定位 |
| --- | --- | --- |
| W01 | 总页包含“祈愿／兑换／馈赠”，祈愿及馈赠区分常驻、高阶、友情。检查各箱子的 `OpenChest` 功能条件，隐藏未开放页签；默认常驻，按开放数量控制横向滚动与拖拽，动态加载对应页面，维护红点、顶部货币、选中页和关闭清理 | `GachaPanel`、`GachaLoopView`、`AchievementPanelWithTypes`、原 `GachaPanel.prefab` |
| W02 | 常驻 `GachaType.Skill=0`，箱子 `6000`，每抽消耗 `110×1`、获得积分 `251×1`；支持 `1/10/100` 次。读取已解锁的各分支最高阶职业，默认当前职业；切换后更新奖池、规则提示、自选入口及职业消息 | `SkillGachaPanel`、`GachaPanelBase`，`gacha/chest` |
| W03 | 高阶 `GachaType.SkillSenior=2`，箱子 `6002`，每抽消耗 `118×1`、获得积分 `253×1`；支持 `1/10/100` 次。职业列表取全部已解锁职业，当前职业优先，其余按阶级降序及原职业顺序；对应职业名、颜色、提示和女神像皮肤同步变化 | `SkillSeniorGachaPanel`、`FurnitureUISkinProfessionExtension` |
| W04 | 两种技能祈愿按持有材料刷新单抽及十／百抽按钮；能支付百抽时显示百抽模式，不擅自增加配置外的任意次数。抽取前校验每日限额、自选完成状态、非当前职业确认、功能禁用及是否已存在结果窗口；发送前再核对限额 | `GachaPanelBase`、`PlayerOpenSkillChestBtn`、`SkillGachaPanel.OnSkillGacha/GachaSend` |
| W05 | 百抽先弹确认，可勾选“今日不再提示”；按祈愿类型保存忽略时间，同日有效，取消不抽取。三种女神祈愿均为 `GachaClassType.GoddessGacha=0`，共享类别每日上限 `10000`，不是每个页签各有一万次 | `GachaPanelUtils.HundredGachePreRemind`、`GachaBaseSetting.LastIgnoreRemindHundredTickDict`、`GachaTodayCountLimitUI`、`DailyGachaMaxCountDict` |

### 原始官方功能：职业自选、技能池与概率

| 编号 | 原始官方行为 | 源码／配置定位 |
| --- | --- | --- |
| W06 | 常驻达到职业阶级 `8` 后开启前置职业自选，需选 `4` 个职业。候选为所选职业的前置链，排除自身，按阶级降序排列；可选／取消，满额阻止继续添加。按职业分支恢复既有选择，去重、截断、排序；弹窗草稿与已提交状态分离，未满额确认有提示，提交中禁用按钮，成功关闭、失败恢复操作；未完成时抽取返回原提示 `61903` | `SkillGachaCustomProfessionSelectionEntry/Panel/Group`、`PlayerSkillGachaSetCustomSelectionReq`、`SkillGachaCustomSelectionSettings` |
| W07 | 常驻按所选职业及可用前置职业筛选可祈愿技能；`isGacha:true` 使用职业 `Skills`，不能把仅在 `FightUsedSkills` 的技能加入。启用自选后取当前职业及各选中职业的可抽技能并集。高阶严格筛选 `SkillInfo.UnlockProfession == 所选职业`；技能碎片先还原技能 ID 再判断 | `ItemFilterForProfessionSkill`、`ItemFilterForProfessionUnlockedSkill`、职业与技能表 |
| W08 | 奖励规则递归展开随机包、移除无效项，按等级／突破阶缩放数量，职业过滤后重新归一化；展示时合并相同物品和数量并按品质／类型分组。抽取池不能替换为所有技能均匀随机，概率页也不能显示未过滤原包 | `RewardRuleInfoParser`、`RandomRewardInfoParser`、`GachaProbabilityContent/Item` |
| W09 | 品质保底、连续未中记录、职业阶级整技保底及碎片保底分别维护；面板剩余次数、详情规则和结果后刷新使用对应记录。十抽／百抽仍需逐抽更新记录，两个技能池各用自己的共享记录 ID | `PlayerRandomRewardPreviewerUI`、`PlayerRewardRecordPreviewer`、`SkillGachaLowRankRecord`、`SkillGachaLowRankPieceRecord`、`SkillGachaSettings` |

常驻规则 `20000`、高阶规则 `21000` 均依次指向 `50302/50301/50300/50252`，权重 `100/700/2200/7000`，对应传说整技、史诗整技、稀有整技、普通技能碎片；基础类别概率为 `1%/7%/22%/70%`，碎片包每次 `3` 片。具体技能继续按随机包权重及职业筛选，不能据类别概率推断池内每个技能都等概率。

两条规则分别配置 `IndexMininum=[70,30,10,0]`、`RecordSharedId=20000/21000`。默认 `EnsureHightRankConfig` 对品质 `4/3/2` 均为 `3`，`EnsureHighRankPieceCount=3`；职业阶级 `1～4` 的覆盖配置为 `3/7/7`、碎片 `7`，其他阶级走原配置选择函数。常驻详情的职业阶级保底文案对配置次数加 `1`，面板品质剩余次数则沿 `IndexMininum - GetKeepMissScore`；二者不能混用。高阶不使用常驻的职业自选及其自定义规则提供器。离线抽取遵用户确认：技能碎片固定发 `3`，即使该职业配置覆盖为 `7`。

`gacha_pity_probability` 对两个技能池配置 `241/790/2400/6569`，`Formula.Long2Double` 除以 `10000`，对应保底综合展示概率 `2.41%/7.90%/24%/65.69%`。`GachaProbabilityItem.SetPityProbability` 遵循 prefab 的地区隐藏字段；是否显示须恢复实际序列化配置。这组展示值不等于逐抽权重，也不能据此拟合未知动态概率。

### 原始官方功能：积分与馈赠

`W10` 积分条由 `ScoreRewardUI` 读取下一未领奖档位，进度相对前一档门槛计算；显示当前奖励、首档附加提示、可领取／未达成／已领取状态。奖励图标可打开物品信息，积分详情通过 `ScoreRewardDialog` 打开原成就列表和规则文案。`W11` 馈赠是累计抽取次数奖励，与积分领奖分别维护，通过总页切换对应祈愿类别，恢复原成就列表、完成状态、红点及领奖反馈。

原 `achievement` 表的全部相关模板如下。`物品ID×数量` 沿原配置，循环段由 `AchievementInfo/CycleTemplateInfo` 展开；`CycleCount` 是原模板参数，不应擅自解释为无限循环或任意周期。

| 范围／类型 | 分组及原模板 ID | 初始门槛与奖励（按列出顺序对应） | 后续模板与循环参数 |
| --- | --- | --- | --- |
| 常驻积分 `20` | Group `6`，`6001～6010` | `200/250/300/350/400`；首档 `6800×1`，其余 `118×5` | `6005.CycleCount=2800`；`6006～6010` 每档增 `50`、奖励 `118×5` |
| 高阶积分 `60` | Group `8`，`8001～8010` | `50/100/150/200/250`；均 `4×1` | `8005.CycleCount=1600`；后五档每档增 `50`、奖励不变 |
| 常驻馈赠 `7` | Group `5`，`5001～5014` | `10/20/30/40/50/60/70`；`100×3/101×3/102×3/301×2/301×2/302×2/303×2` | `5007.CycleCount=10000`；后七档每档增 `10`，奖励 `301/301/302/301/301/302/303` 各 `×2` |
| 高阶馈赠 `61` | Group `7`，`7001～7008` | `10/20/30/40`；依次为 `(301×2,302×2)`、`(301×2,303×2)`、`(301×2,302×2)`、`(301×2,304×1)` | `7004.CycleCount=10000`；后四档每档增 `10`，奖励重复上述四组 |
| 友情积分 `59` | Group `19`，`19001～19008` | `30/60/90/120`；`1100/1110/1120/1131` 各 `×1` | `19004.CycleCount=100`；后四档每档增 `30`，重复上述奖励 |
| 友情馈赠 `62` | Group `18`，`18001～18010` | `10/20/30/40/50`；均 `301×2` | `18005.CycleCount=200`；后五档每档增 `10`、奖励不变 |

领奖沿 `OperPlayerGetAchievementReward(group,1,receiveSameType:false)` 等原调用与 `AchievementPanelBase` 行为；通用显示数配置 `AchievementPanelShowCount=5`，不能据模板数量自行添加“一键领完”。馈赠奖励消息按 `SourceOperate.Achievement=18`、`AwardStyle.Popup=2` 打开 `AwardPanel`；技能商店商品另按自己的奖励窗口类型处理。

### 原始官方功能：详情与历史

`W12` `GachaDetailDialog` 提供概率／历史标签，按 `RecordHistory` 控制历史入口，显示对应标题、规则、类别和每日次数说明。常驻 `GachaDetailInfoSkillGachaProvider` 按 `max(职业阶级,1)` 选配置，自选开启后附加原规则；高阶不套用该常驻自定义设置。概率分组展示类别总概率、品质和物品图标；`GachaProbabilityItem` 仅为友情祈愿显示逐物品百分比，基础概率汇总按原精度处理，物品点击沿原详情。

`W13` `GachaHistoryContent/Item` 按祈愿类别加载分页，打开从第 `1` 页开始，使用 prefab 的每页数量；换页取消旧异步任务，按返回总页数更新翻页控件。每行显示物品名、类型、本地日期时间和品质颜色，不能用抽取按钮次数伪造历史行。`PlayerGachaHistoryManager` 分类缓存上限 `1000` 条，配置记录期 `180` 天；服务端分页返回契约需要由本地记录读取适配，不能把缓存上限直接当作永久总记录上限。

### 原始官方功能：结果、动画、音效与重复转化

| 编号 | 原始官方行为 | 源码／资源定位 |
| --- | --- | --- |
| W14 | 结果面板消费当前祈愿 `SourceOperate` 的真实奖励消息（常驻 `75`、高阶 `87`、友情 `86`），从展示列表滤除积分物品但仍结算入账；按实际展示奖励数选择单抽、十抽、百抽表现。空奖励走原异常分支，不能播放假成功；顶部货币、继续抽取、关闭和每日次数同步结果 | `GachaResultPanel`、`GachaPanelBase` |
| W15 | 单抽区分整技／碎片，显示品质背景、名称、职业图标、技能描述／标签与详情链接，新技能提示，渐显、星光、音效及转化延迟；技能碎片详情在结果中隐藏底部合成操作 | `GachaOneRewardGo`、`GachaResultPanel`、`ItemAwardIconGodGacha` |
| W16 | 十抽按星光动画事件填槽与播放品质音效，结束补齐未显示槽位，再按原顺序执行转化；百抽沿原行模板、滚动列表、播放缓存、自动滚动和转化时序；跳过结束演出并补全最终结果，不重抽、不变更数量 | `GachaResultAnimPlayer`、`GachaTenRewardGo`、`GachaHundredContent`、`GachaHundredRewardGo` |
| W17 | 新整技、重复技能转碎片、回收为代币、碎片与代币同时出现分别有图标、数量、过渡效果与声音；以奖励数据的 `Agiotage/RecycleAwards/GetDetailAward` 为依据，不由表现层重新决定奖励 | `AwardData`、`ItemAwardIconGodGacha` 的四种状态、`GachaTenRewardGo` |
| W18 | 演出结束恢复按钮组、刷新保底提示及每日次数，处理额外奖励处理器；关闭／重开时取消主分支异步任务、解绑事件、回收槽位并清理粒子。原额外奖励表当前为空，不能自行添加额外奖励 | `GachaResultPanel`、`GachaAddtionalAwardHandler`（原拼写）、原动画事件、`gacha_additional_awards` |

物品表另提供重复与回收依据：`CanRepeat/AgioItemId/AgioAmount/AgioRuleIdList/RecycleReward/RecycleGroupCount`。例如技能 `10000` 转 `12000×10`，`10001/10002` 分别转对应碎片 `×30`，`10003` 转对应碎片 `×90`；碎片 `12000` 的回收组数量 `1`、奖励 `{20:1}`。这些配置与客户端展示分支不能单独证明服务端对已满阶技能、部分升级及溢出碎片的全部裁决顺序；接入时须与[技能养成](skills.md)及[背包](inventory.md)共用已确定规则，未确定的服务端分支继续查证。

### 原始官方功能：兑换商店

`W19` `SkillExchageShopParent`（官方类名如此拼写）将总页兑换接到商店 `4/5`，按功能条件切子页，初始化／打开对应商店、设置真实语言标题并清除新标。顶部使用原 `PublicTopBar/MoneyUI`；这里不是付费代金券商城，不能用 prefab 中未刷新的静态文字替代运行时标题。

| 编号／商店 | 原始官方规则与交互 | 源码／配置定位 |
| --- | --- | --- |
| W20／商店 `4` | 固定商品 `376` 项（`shop_goods` 的 `2500～2875`），按原商品逐行读取数量、价格、库存、购买条件和限购；无随机组、无手动刷新次数、无周期刷新，随职业重建。列表按已解锁职业筛选并按职业与物品类型分组，支持原品质／元素／类型／职业过滤，默认采用当前职业前置链，不混入不可用技能 | `ShopSubPanelForSkillPiece`、`ShopInfo.GenerateFixedGoods`、`shop/shop_goods`；例如 `2500` 为 `12000×30`、消耗物品 `4×1`，不能据此把所有商品价格写死 |
| W21／碎片详情与购买 | 打开原物品／技能详情并隐藏来源入口；未拥有显示合成需求，已拥有且未满阶显示升阶需求，满阶按原状态隐藏。支持原数量滑条、可负担上限、货币总价与不足颜色、确认／取消；数量乘商品每份数量，按共享购买请求生成器处理库存、限购及奖品窗口 | `SkillPieceShopDetailDialog`、`ShopUIUtils`、`PlayerBuyFromShopRequestList` |
| W22／商店 `5` | 原组 `54001/53001/52001` 各生成 `1/2/6` 格，共九格，按职业过滤；组内递归权重继续解析 `shop_goods_group`，不能按商品列表均匀抽样。日刷新货架、独立格库存、售罄和购买反馈；切页或重开不得任意重掷 | `ShopSubPanel`、`ShopInfo.GenerateRandomGoods`、`ShopPanelGoodsItem`、`ShopGoodsItemIconSkillPiece` |
| W23／刷新 | 每日最多手动 `10` 次，依次消耗物品 `2` 的 `50/100/100/150/150/200/200/200/200/200`；显示剩余次数、下一刷新倒计时、可刷新／不可刷新／次数用完状态。原消耗物品 `2` 需确认，取消不刷新；不足走提示与失败音效，成功才刷新并播放原音效；底部区域按刷新能力决定显隐 | `ShopPanel`、`ShopInfo.GetRefreshCost/GetNextRefreshTime`、`OperPlayerShopManualRefresh`、`ShopRefreshCoinNeedConfirm` |

原随机组 `52001` 含子组 `50001:600、50501:300`，`53001` 再含 `51001:100`，`54001` 再含 `51501:50`；仍需按子组、条件与职业过滤递归展开。固定生成条件与购买时条件分别沿原 `Condition/BuyCondition` 校验。周期计算沿原 `ShopRefreshCycleType/TimeUtils`，离线时用本地时间与已确认时钟策略，不另造刷新周期。

### 原始官方功能：材料来源与快捷购买

`W24` `PlayerOpenSkillChestBtn` 在不足时计算差额，沿 `QuickBuyUtils.TryGetQuickBuyInfo` 检查原购买配置、条件及价格；存在可用快捷购买才进入对应确认流程，否则提示材料不足／原物品来源。`GachaPanelBase.PopupCurrencyPurchaseDialog` 显示缺少数量、购买所得、货币与总价；`ShopBuyIntent.PopupConfirmDialog` 再检查货币，成功才执行购买和成功回调，取消不扣款。购买与抽取为各自步骤，不能自行增加“补足后自动抽取”。

`W25` 原 `QuickBuyDialog` 支持数量增减、滑条、允许时的最大按钮、持有量、每份／合计成本、可负担上限以及限购价格档位；价格按已购买数量及 `PriceBuyCount` 等原字段计算。`ClientItemSourceQuickBuyHandler` 与 `QuickBuyNavigateHandler` 负责物品来源及顶部货币加号导航，需检查购买资格、刷新持有量和监听生命周期，不能把所有货币加号都接到同一商店。

已提取的三条配置：`110` 每份 `1`、消耗 `2×100`，条件为拥有女神像物品 `4004`；共享配置 `301` 消耗 `2×8`、条件为 `4007`，`107` 消耗 `2×160`、等级至少 `41`，后二者属于相邻召唤模块，不应改成高阶祈愿材料。原 `quick_buy` 共 `26` 行，已核实没有 `118/302/303/304/311` 的快捷购买条目；材料不足不授权擅自给这些物品定价出售。

### 原始官方功能：友情祈愿与引导

`W26` `FriendGachaPanel` 无职业选择，`GachaType.Friend=1`、箱子 `6001`、每抽消耗友情币 `3×25`、获得积分 `252×1`，只支持 `1/10` 次。奖励规则 `20080` 是原货币、材料与随机包组合，不使用技能祈愿的 `1/7/22/70%`。显示本周已获得友情币与上限，订阅刷新事件；同样有详情、历史、积分、馈赠及结果流程，材料不足分支没有技能材料快捷购买。

官方友情币配置包含每周上限 `1000`，助战、组队、队长奖励各 `1`，助战日上限 `3`，互动分享奖励 `5`，周未领取邮件 `1300`。这些只记录原始功能，不能据此在单机新增真人好友、模拟玩家或联网领币。

`W27` 女神像维修／自动维修资格、首次进入、首次抽取及教程路由需连同原家园条件和技能获得引导恢复。配置包含 `SkillGachaGuideFixedRewardId=8`、`SkillGachaNewbieTimes=20`、`SkillGachaOnceSupportTimes=[1,10]`；教程输入与来源见[家园祈愿引导](../../assets/reference/home-wish-tutorial/source-manifest.json)。配置 ID 8 对应单独发放的 `110×5` 祈愿券；按用户确认，常规奖池首抽不替换为固定技能，也不推定前二十抽存在额外固定序列。

### 原始官方 UI、资源与加载绑定

`W28` 页面需按实际 prefab 恢复层级、RectTransform、锚点、尺寸、布局组、字体、本地化、职业皮肤、按钮／图标输入、页签、遮罩、滚动、红点及关闭路径。动态职业页、技能／碎片图标、品质背景、弹窗及音效均应列入显式本地加载目录。

| 资源组 | 原始内容与定位 | 当前边界 |
| --- | --- | --- |
| 祈愿主体 | `defaultpackage_assets_prefabs_ui_gachapanel.bundle`：`GachaPanel`、`SkillGachaPanel`、`SkilSeniorlGachaPanel`（原拼写）、`GachaDetailDialog`、`GachaHistoryContent`、`ScoreRewardDialog`、`GachaResultPanel` 等原 prefab；友情页、自选及列表项继续沿原引用绑定 | 官方 prefab 已接入祈愿控制器和显式加载目录；输入与画面验收状态见“尚未实现” |
| 结果演出 | [结果演出来源](../../assets/reference/wish-performance/source-manifest.json)、[转换数据](../../assets/official/wish-performance/performance.json)：单抽、十抽、百抽行、奖励图标、AnimationClip／事件、粒子、品质效果、Wwise 事件到媒体映射 | 旧转换为迁移输入，需恢复 Unity 原生播放、材质与目标平台效果 |
| 视觉与后处理 | [视觉来源](../../assets/reference/wish-visuals/source-manifest.json)、[转换数据](../../assets/official/wish_visuals.json)、[后处理参数](../../assets/official/battle-post/data.json)、原 [Bloom.cs](../../.tools/source-packages/official-code/urp-reference/Bloom.cs) | 来源明确含旧 Godot 粒子投影、Noise／Bloom 近似；不能把旧 `unconverted_modules=[]` 视为 Unity 迁移完成 |
| 兑换／购买共用组件 | [商店组件来源](../../assets/reference/shop-components/source-manifest.json)、[价格按钮来源](../../assets/reference/wish-button-costs/source-audit.json)、[材料来源入口](../../assets/reference/gacha-item-sources/source-manifest.json)、[快捷购买来源](../../assets/reference/gacha-purchase/source-manifest.json) | 沿原列表、详情、确认、批量设置、价格组件、顶部货币及奖励弹窗绑定；共享模块恢复不等于祈愿调用方已接入 |

### 官方代码与资源依据

以下记录原客户端／配置和已有转换依据；转换产物不等同于 Unity 玩法已接通。

核对固定 APK 的 GachaPanel 内嵌 ShopContent 与 Shop4 / Shop5，序列化 ShopPanelTabItem.myShopId 分别为 4 / 5；TabItemTopBarSwitcher.ShowTypeList 分别为 PlayerItemType.Jingshenli (1) / SkillGachaByproduct (47)，对应物品 4 / 20。SkillExchageShopParent 按子分页切换面板，不跳转国家材料兑换。ChestGachaPanel.OnTabItemChanged 使用内嵌 Shop 子面板并明确商店 6；原始 shop 表中商店 6 的 FixedGoodsList=null、GroupIdList=[]，不虚构服务器货架。

ScoreRewardUI.RefreshReward 从下一档 AchievementInfo.Reward 构造图标，按分段目标计算圆形进度。现按此绑定原奖励并修正零宽 Slot 的居中；首档技能奖励箱 6800 的 ItemIcon/Box/item_6800 在未打包的 box_skill_noatlas 中，保留奖励名称，不冒用消耗品图像。古遗物页分宝箱 / 元素，使用已取得 ChestElementGacha 图集；完整 ChestGachaPanel、扭蛋机和背景仍未取得，共用原始底部导航和通用结果面板的部分属于本地适配，不声称像素级还原。具体缺项见 source-manifest.json 的 gacha_exchange_interface / wish_first_score_reward_icon。没有新增位图素材。

积分奖励列表再次核对 `ScoreRewardDialogItem` / `AchievementPanelItem.Refresh` 和 `ItemIconDefault`：行宽为 850.2618，标题位于原始背景框内；`AchievementPanelItem.Refresh` 对所有奖励执行 `SetCount`，包含数量 1。原 prefab 确有 `ImgItemIcon_NumBg` 深色数量底（颜色 #222222，alpha 0.851）及内容自适应布局。其 TMP 序列化当前字号为 18.8，基准字号为 36，自动缩放范围为 18–36；本地普通物品数量恢复基准字号和按数字测量的宽度，保留原数量底图。重新校验固定 APK 后提取 `image_itemicon_box_skill_noatlas.bundle` 仍返回 `Not packaged`，列表也按真实名称显示 6800，未声称补齐原图。没有新增位图素材。

物品数量的字号修正继续上移到共用 UI 控件，覆盖普通物品、技能、技能碎片、装备、料理、宝石、古遗物 / 碎片，以及页面内嵌的图标。统一使用 36 基准字号，小槽位按缩放补偿，在标准 450 像素宽窗口中不低于 10 像素；文字和背景按实际数字重排，动态数量更新也触发重排。技能碎片原始 `BgCountText ` 名称带末尾空格，按实际数量节点绑定，避免漏显。古遗物无数量底图的原始样式继续保留，并增加细描边以便辨认；这些最小字号与描边属于本地桌面适配，不声称是原版全部场景的字体规则。原数量底图、碎片标记和图片来源不变，没有新增位图素材。

再次核对 `GachaProbabilityContent` / `GachaProbabilityItem`、`ShopGoodsItemIconBase` / `ShopPanelGoodsItem` 与原始 prefab。祈愿详情现从 `chest.RewardRules` 和实际职业池生成按品质、类别分组的基础概率，并复用详情内的 `RuleRateInfoItemGacha` 图标网格；只读取配置，不推进随机或保底。商品信息使用已提取 `ShopGoodsItemInfoCommon` 的名称、图片、数量节点，禁用被误作名称的额外 `BottomDescGo`，为名称与数量划分空间。馈赠内嵌 `ScrollRect` 缺少滚动脚本，现显式绑定实际奖励列表，支持滚轮及拖动；缺图箱保留真实名称。没有新增位图素材。

按用户要求，常驻与高阶的文字下拉框统一为人物当前转职路线的各阶段；此筛选属于本地行为调整，不沿用 APK 常驻跨路线筛选。两个奖池的记录入口也统一坐标。

底部导航再次从固定 APK 的 `PublicBottomBar.prefab` 核对：`FunctionTextBgImg` 使用原始 `ImgBg_NameSel`，黑色圆角底图只在选中项显示，`PublicBottomBarCell_On` 的最终尺寸为 163 × 42，`Off` 隐藏文字与底图。五个文字底图上的 `VerticalLayoutGroup` 和 `ContentSizeFitter` 原始 `m_Enabled` 均为 0；提取器现保留其禁用行为；重新提取底栏，仅移除对应十个误启用的布局字段。图片、原始边距数据和动画曲线均未替换。主导航在五个主界面中保持可用，输入顺序与显示层级一致。

使用固定 manifest `871_156598`，`prepare_wish_assets.py` 重新解码并逐项比对 13 张 Luban 配置表，记录原始字节 SHA256；同时核对 `SkillGachaPanel`、`SkillSeniorGachaPanel`、`GachaPanelBase`、职业过滤、路线回溯、循环成就和记录客户端逻辑。证据见 `official-city/wish-source-audit.json`，玩法与离线边界见 `docs/features/wishes.md`。

常驻选中图 `ImgSel1` 的 500×328 原画布仅有约 500×174.85 的 atlas 裁切片，纵向偏移约 67.08；此前裁切片被拉伸为整幅高度。已恢复 `rect_size`、`trim_offset`、`trim_size`。高阶 `ImgSel` 保留原九宫格。两尊雕像 prefab 均使用 808×988 原画布，已在各自不同宽度的 Slot 中按同一尺寸居中，并恢复常驻雕像裁切参数。积分弹窗保留来源祈愿背景，馈赠释放重复的内嵌顶栏 / 底栏预留，技能商品库存文字与图标分开。未新增自绘图片。

首档自选 1 阶技能箱 `6800` 已补齐并实际用于圆圈、积分弹窗及馈赠，替代历史缺图名称占位。原始 `defaultpackage_assets_image_itemicon_box_skill_noatlas.bundle` 不在基础 APK 内，但现有同版本官方 CDN 缓存可用：MD5 `ea8a28849473cdeb177a30a44b54782e`，大小 512,743 字节，SHA256 `1756ede5d29cbeec9497fecdf06cc64d6abd18111c37948338705f28ef016c2b`。实际 URL 为 `https://zhangjcsdl.leiting.com/patch/202609271456857/Android/DefaultPackage/ea8a28849473cdeb177a30a44b54782e.bundle`。Sprite `item_6800`、path ID `2600191277797411432`，转换输出 `assets/official/sprites/item_6800-15e9a49313.png`，输出 SHA256 `6dc025b9463e576af3628b5ab2cea07ff05ba1b1e7bd5b26ea404d4f65302d19`，已登记来源清单及显式加载清单。

前文的 `Not packaged` 和 `wish_first_score_reward_icon` 是当时仅核对基础 APK 的历史结果，现以其 `resolution` 及清单记录实际替换状态；没有混入新版文件或改变原预期哈希。原客户端完整服务端动态概率公式未取得，`WeightScaleFactor=2` 不据此猜造概率；本地实现明确的基础权重、品质保底和当前阶段保证，并标注这一限制。

对照原版本新增官方下载缓存和固定 APK，补迁此前未转换的祈愿视觉资源。实际使用的 29 项图像依赖均来自固定 APK `871_156598`，没有将已缓存新版特效包当作原版补齐。前一节从同版本 CDN 恢复的技能箱 `6800` 继续使用；其来源与历史缺图记录保留。

`python scripts/prepare_wish_visuals.py` 从原 `SkillGachaPanel / SkilSeniorlGachaPanel / PetGachaSubPanel` 读取常驻、高阶与幻兽结缘环境粒子，从 `GachaTenRewardGo` 读取四组奖励粒子，恢复八个白／蓝／紫／橙品质光效 prefab 和 `GachaLightConfig` 的五种品质 Sprite。输出 20 张原始 Texture2D 解码 PNG 到 `assets/official/wish/`，相关 Sprite 继续使用 `assets/official/sprites/`，参数写入 `assets/official/wish_visuals.json`；所有动态图像均纳入显式本地加载清单。原 bundle、MD5、对象 path ID、操作、输出 SHA256、客户端代码哈希与字段绑定见 [wish-visuals/source-manifest.json](../../assets/reference/wish-visuals/source-manifest.json)。没有新增自绘位图。

常驻／高阶页面已使用环境星光，幻兽页面使用星光与落叶；结果页使用原版品质光、奖励出现闪光、新技能光圈及重复转换碎光。`Fragment` 和 `Aperture` 的用途按原脚本字段与 prefab PPtr 核实，分别对应重复转换和首次获得。视觉随机源独立于抽取随机源，状态快照仅保留在界面实例中，不改变本地结算与存档。

使用原始发射曲线、爆发、颜色、尺寸、寿命、数量上限、纹理滚动、扰动与淡出；Unity 3D 湍流、Bloom 后处理、完整星轨和结缘角色演出仍未逐项还原。

`python scripts/prepare_gacha_purchase.py` 从固定 APK 与已校验的 `871_156598` CDN 缓存读取原 `quick_buy.g.bytes`、`furniture.g.bytes`，转换 `CostItemConfirmDialog` 和 `ShopQuickBuyDialog` 两套购买 prefab。运行数据为 `assets/official/gacha-purchase.json`，图像继续使用原 Sprite 解码文件，30 项依赖均登记并纳入显式本地资源清单。完整 26 条快捷购买原表、3 条本模块使用的材料购买记录、免费自动修复家具条件、bundle 来源与对象、图像处理方式及输入／输出哈希，见 [gacha-purchase/source-manifest.json](../../assets/reference/gacha-purchase/source-manifest.json)。没有使用新版替代包或自绘图片。

原价为星之愿 `110` 每个晨星 ×100、稀有女神牌 `301` 每张晨星 ×8、结缘材料 `107` 每个晨星 ×160。高阶材料 `118`、女神牌 `302–304`、元素牌 `311` 没有快捷购买记录。材料不足时使用原确认弹窗购买差额；数量购买使用原数量滑条、加减与最大按钮。购买后先补入材料，另行点击抽取，保留原流程；顶部余额加号与物品详情为购买入口，祈愿页的“购买材料”为本地便捷入口。

核验命令为 `python scripts/verify_gacha_purchase.py`；此前“在线购买材料不属于离线实现”的范围说明已由本地晨星购买替代，真实支付仍不接入。

固定 `871_156598` APK 的常驻抽取及兑换刷新 prefab 确实默认显示 `ImgCostMoneyBtn_Bg`，`PlayerCostMoneyContent` 在正数成本下显示整个成本区域，核对的余额状态切换配置为空。原资源中存在黑色价格底板，不能声称原版会在材料足够时隐藏它。这属于本地展示调整，既有官方图片像素与来源未改动，没有新增自绘或重新转换图片。

原 `ShopPanel.SetRefreshUI` 显示 `RefreshCountsLimit - RefreshedCount`，原商店 5 为每天十次付费刷新；现显示完整剩余整数 `10→0`，并为徽标、文字、晨星和价格分配独立边界。四项原始成本组件字段、21 项 prefab 图片绑定、6 项复用 Sprite 哈希、客户端代码哈希及原刷新价格数组见 [wish-button-costs/source-audit.json](../../assets/reference/wish-button-costs/source-audit.json)。

固定APK及匹配871_156598 CDN中的prefabs_ui_gachapanel、prefabs_ui_item、prefabs_ui_pet与原AnimationClip、ParticleSystem、Sprite、TMP字段和Wwise事件链由scripts/prepare_wish_performance.py转换，输出assets/official/wish-performance/。完整对象来源、客户端类SHA256、布局输入、图片／音频处理及输出哈希保存在[来源清单](../../assets/reference/wish-performance/source-manifest.json)。界面图片直接解码，动画保留原曲线与AnimationEvent，音频按原Event→Action→Sound→媒体解码为Vorbis；原停止事件登记为stop，无替代音效。LoopScrollRectBase来源于已解码的原LoopScrollRect.Runtime.dll，作为原百抽滚动方向和首屏行数计算依据。pet相关prefab绑定为继续迁移结缘所需的原始输入，提取完成本身不代表已接入运行时。

prepare_pet_exchange.py 从固定871_156598 APK／匹配原版本CDN提取PetGachaPanel商店区、ShopGoodsItem、ShopGoodsItemInfoCommon、ShopGoodsItemShowConfig及七档原品质图，保留原RectTransform、LayoutGroup／LayoutElement、StateSwitcher、ListItemShowSequence和中文键。资产写入assets/official/pet-exchange/，288个源对象、70个输出的bundle／对象、输入输出SHA256、原客户端与Common源码及两份语言包哈希统一在pet-exchange/source-manifest.json登记。ShopOpenController实际mShopId=7；当前商品类型使用原默认Common图标模板，名称按原ShowName显示，没有绘制替代图或添加第二套商品坐标。

购买成功／失败声音由ShopGoodsItemShowConfig的实际事件引用追到原UI／Minimum Wwise包的Event→Action→Sound／DIDX媒体，转为Vorbis并保留包哈希和原媒体哈希；Main_Cursor_Fail位于Minimum包，未用其他声音替换。购买确认及模糊遮罩复用已有原ConfirmDialog与默认BlurMask资源。源校验：python scripts/prepare_pet_exchange.py --check；运行规则、测试、尚未迁移项与素材包／正式发布状态仅记对应功能／地图文档。

`prepare_gacha_item_sources.py` 从固定871_156598 `ItemDetailDialog` 的 `ItemSourceGo.ItemSourcePrefab` 转换实际引用的 `ItemSourceItem`，保留原布局、可用／不可用状态、箭头与背景贴图；来源枚举文案由同版本简中语言包提供，包括国度扭蛋获取与元素扭蛋获取。资源位于 `assets/official/gacha-item-sources/`，原组件、调用脚本、语言包哈希及输出哈希见 [gacha-item-sources/source-manifest.json](../../assets/reference/gacha-item-sources/source-manifest.json)。Unity 库存导入工具通过 `prepare_unity_inventory_assets.py` 校验并接入该来源行目录与三张图。

## 完整还原部分

### 本地流程、规则与存档

- `unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs` 将家具类型 `5` 接到 `OpenWishes`；家园女神像仍按官方家具 `4004` 的解锁／维修流程显示，维修后进入 `WishScreenController.Open`。关闭祈愿会销毁页面并刷新家园顶部货币。
- `unity/Assets/Game/Runtime/Offline/Progression/OfficialWishRules.cs` 读取固定 APK 解码的祈愿、随机包、职业、技能、物品、成就、商店和设置表；`OfflineWishService.cs` 按递归随机包与职业过滤计算本地奖励，使用本地原子事务结算消耗、技能／碎片、积分、保底、成就、历史及商店状态，并以 receipt 防止重放重复扣款或发奖。
- `OfflineWishProgress.cs` 将各奖池次数、品质及职业保底、撞期排程、引导奖励领取状态、分类历史、成就领取索引、商店每日状态和逐格货架存入角色档案。抽取结果在事务提交后才展示；重开读取已提交的相同结果，不重新随机。
- 常驻、高阶与友情祈愿分别使用官方奖池和消费配置；常驻、高阶支持单／十／百抽及职业选池，常驻职业自选按前置职业技能并集筛选。友情入口保留，但只消费存档中已有友情币，不发放友情币，也不模拟好友、组队、助战或分享。
- 品质保底、整技职业阶级保底及碎片保底分开记录；已确认的多个保底撞期时，按目标抽次向前排程（两项分到第 N-1、N 抽，三项分到第 N-2、N-1、N 抽）。命中只清零该品质的未中次数。技能碎片结果固定为 3 片，即使职业阶级覆盖表另写 7。
- 首次进入由 `SkillGachaGuideFixedRewardId=8` 对应的 fixed reward 单独发 `110×5` 祈愿券并显示教程奖励；首抽仍走常规池。已有技能按 `CanRepeat/AgioItemId/AgioAmount` 转化；满阶后新增碎片按物品 `RecycleReward/RecycleGroupCount` 配置回收。
- `WishScreenController` 接通祈愿、兑换、馈赠／积分领奖、概率／历史、职业自选、过滤、购买确认、快捷购买和缺额提示。固定商店按官方 `2500～2875` 商品数据生成；日常商店按官方权重生成九个独立货格，并按本地日切换库存、刷新次数及倒计时。

### 官方界面与资源接入

- `unity/Assets/Game/Runtime/Presentation/Wishes/WishScreenController.cs` 从 `OfficialWishAssetCatalog` 装载并实例化官方祈愿 prefab，保留原层级和按钮／图标位置；按本地服务状态绑定祈愿、详情、历史、成就、商店及弹窗。官方规则、历史、积分奖励 prefab 均没有关闭节点时，按各自对话框尺寸在底部复用职业选择 prefab 的 `CancelBtn`；奖励反馈使用该官方对话框壳并显示物品图标／数量，隐藏未用的职业选择控件。价格按钮按已确认差异隐藏黑底并使用深色价格字。
- 单抽、十抽、百抽逐项执行已结算奖励的对应动画；十抽消费原星光事件和品质音效，百抽按原列表曲线滚动，跳过只结束演出、不重抽。技能重复转化和碎片回收直接展示服务返回的转换奖励。
- 官方资源由 `OfficialWishAssetTools` 导入 Unity 原生可用的祈愿 prefab、图标、动画数据和音频；导入 588 个官方祈愿 sprite 和 17 段音频；`OfficialWishAssetCatalog.asset` 及 `Resources/Wishes/wishes-runtime.json` 作为显式本地加载目录。`commerce_ui_conversion.flatten_ui_node` 按节点组件生成 `has_image/has_text` 等运行时标记；固定版主页 739 个节点包含 330 个图像和 161 个文本组件，加载器校验图文组件存在。原始来源与转换依据继续见本页上方清单、`assets/reference/wish-performance/`、`wish-visuals/` 及 `assets/reference/SOURCES.md`。

### 来源与验证

固定 APK 由现有 `Apk` 读取器核验；`gacha/chest/reward_rule/random_reward/profession_base/skill/item/achievement/game_settings/gacha_pity_probability` 解码结果与 `assets/official/rules.json` 一致。祈愿原 prefab、演出及视觉来源沿本页上方记录恢复，没有把新版同名包或自绘内容冒充固定版本资源。

`Zjcs.Editor.Wishes.OfficialWishAcceptance.Run` 使用隔离临时档案覆盖配置引导奖励、常规抽取、receipt 幂等、品质计数、友情币不足、九格商店、本地日切、双项撞期 `9/10`、三项撞期 `8/9/10`、职业覆盖为 `7` 时仍固定发三片、重复技能转化、满阶碎片回收及存档重开；Unity Batchmode 规则验收已通过。`python -B scripts/check_unity_project.py` 静态 GUID 检查已通过（27,935 个 GUID）。Unity 2022.3.57f1c2 独立图形 Editor 已重导入并运行祈愿 UI；在 `450×800` Game View 实际渲染了祈愿主体、引导奖励标题、`110×5` 奖励行和单一关闭按钮，主页 739 节点的目录校验及图文组件断言通过。真实输入首次尝试中女神像点击落到 Explorer（PID `10152`），未打开维修窗；验收器现校验前台与命中窗口 PID，之后因 Editor 未获前台而安全超时、未发送点击。单／十／百抽连续演出、音频和关闭重开存档的可见流程仍未通过真实鼠标验收。

## 自定义实现

以下仅为用户已确认且服务端源码不可见时的离线决策；其余可确定的数值、公式、条件和顺序沿用官方配置与客户端表现。

- 按官方已知权重、配置保底和客户端筛选逻辑本地裁决，不拟合展示用综合概率；无服务器随机序列的单机状态通过本地原子事务持久化。
- 首次进入只领取固定奖励表 ID 8 的五张祈愿券，教程不改变首抽奖池。碎片产出固定为三片，保底冲突按用户确认的临近抽次排程。
- 友情祈愿消费入口保留，仅使用现存友情币；不提供离线范围之外的真人好友、组队、助战或分享获取流程。
- 价格按钮保留数量、货币图标和可用／不足状态，按用户确认隐藏原黑底并改用深色价格字。

### 服务器边界

`SkillGachaPanel.GachaSend` 调用 `OperPlayerGacha`，结果由奖励消息返回；APK 共享代码 `Common.Player.SkillGacha` 未包含完整服务端随机裁决。已按客户端实际使用的奖池、权重和条件，以及用户对缺失服务端分支的明确答复落实离线逻辑；不把消息结构误当作服务端算法。

首登发奖、首抽流程、重复技能转换、满阶回收、碎片数量、保底命中清零范围及同时触发排程均已按用户确认补齐；不得将这些先前的待确认文字当作当前缺项。

## 尚未实现

- [ ] Windows 输入桌面可用，但 Unity Editor 未获前台焦点。重新运行隔离 Editor 后先确认其为前台，再以真实 OS 鼠标从家园维修并点击女神像，关闭教程奖励，执行常驻抽取并打开结果／历史／兑换页；确认真实扣款、历史写入、弹窗输入及重开存档。前一次点击命中 Explorer（PID `10152`），不能记作通过。
- [ ] 可见验收完成后，复核 `W01～W28` 相关动态加载、输入遮挡、竖屏缩放和单／十／百抽演出范围；按[Android → Windows 资源流程](../asset-management.md#android-资源迁移到-windows)核对依赖。Windows Player 仅在涉及导出链或成品行为时构建验证。

原表及总页来源另见[祈愿原始来源](../../assets/reference/official-city/wish-source-audit.json)和[素材来源总表](../../assets/reference/SOURCES.md)。验证结果持续维护于本功能页，不另建验收报告。
